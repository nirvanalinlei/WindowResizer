# Virtual Desktop Route Options

## Runtime Finding

On March 28, 2026, a real-window runtime probe in this repository showed that
the current official `IVirtualDesktopManager` path is reliable for reads, but
not sufficient as the production write path in the current code path and
runtime environment:

- `GetWindowDesktopId` works.
- `TryIsWindowOnCurrentDesktop` works.
- `MoveWindowToDesktop` fails with `HRESULT 0x80070005` on real top-level
  windows, including a retry after switching to the window's current desktop.

That means the current implementation can detect virtual desktops, but it
cannot currently be trusted as the move/write path for cross-desktop restore.

## Route Comparison

### Route A: Keep the official API as-is

- Source: Microsoft Learn `IVirtualDesktopManager` and Microsoft Q&A describe
  the interface as limited in scope, mainly for auxiliary-window scenarios,
  not general desktop orchestration.
- Repository finding: the current runtime probe confirms that the read path is
  usable, but the write path does not satisfy product needs.

Decision: reject as the primary restore path. Keep it only as a read-only
backend.

### Route B: Vendor a maintained build-specific implementation

- Source: `MScholtes/VirtualDesktop` is an actively maintained C# tool for
  Windows 10/11 with explicit window move commands and build-specific source
  files such as `VirtualDesktop.cs`, `VirtualDesktop11.cs`, and
  `VirtualDesktop11-24H2.cs`.
- Source: `MScholtes/PSVirtualDesktop` documents `Move-Window`,
  `Get-DesktopFromWindow`, pin/unpin, and frequent build fixes through 2025.
- Inference: this is the strongest near-term candidate for an in-process
  adapter that may preserve the current `net47/net48` delivery path.

Pros:

- Best fit for the current repository architecture.
- Avoids a GUI/CLI platform migration.
- Evidence of active build-specific maintenance.

Risks:

- Uses undocumented Explorer/internal COM APIs.
- Requires our own compatibility matrix and fallback contract.
- License, attribution, target-framework fit, and packaging assumptions must be
  verified in a spike before adoption.

Decision: recommended near-term candidate, contingent on a spike.

### Route C: Migrate to a newer Windows-only managed wrapper

- Source: `Grabacr07/VirtualDesktop` documents moving windows from the same
  process and another process, but requires
  `net5.0-windows10.0.19041.0` or later because of C#/WinRT.

Pros:

- Cleaner managed API.
- Better long-term ergonomics.

Risks:

- Forces a platform migration for GUI, CLI, packaging, and support matrix.
- Implicitly drops the current .NET Framework delivery path.

Decision: valid long-term route, but too large for the current corrective work.

### Route D: Keyboard simulation or lightweight native helper DLLs

- Keyboard simulation is heuristic, focus-stealing, and not desktop-ID based.
- `Ciantic/VirtualDesktopAccessor` is promising, but the current route would
  add native DLL integration and still needs a separate compatibility story.

Decision: reject for now. Useful only as a spike or emergency fallback.

## Recommended Architecture

### Service Boundaries

The corrective path should preserve a single application-facing abstraction,
but it needs an explicit capability model.

- Keep `IVirtualDesktopService` as the only service used by coordinators.
- Replace the current coarse `IsSupported` meaning with explicit capabilities:
  - `CanReadDesktopId`
  - `CanMoveWindow`
- Keep existing read/move methods so `RuleRestoreCoordinator` and
  `LayoutSnapshotRestoreCoordinator` remain stable consumers.
- Keep `WindowsVirtualDesktopService` as the official read-first backend.
- Add `ExplorerVirtualDesktopService` as the candidate write-capable backend.

If the chosen backend needs navigation fallback instead of direct move, add a
separate boundary such as `IVirtualDesktopNavigationService` for:

- reading current desktop identity
- switching current desktop
- reporting whether navigation fallback is available

Navigation must not be hidden behind a move-only interface.

### Backend Selection

Add a runtime selector, for example `VirtualDesktopServiceFactory`, with these
rules:

- coordinators depend only on `IVirtualDesktopService`
- vendored code is never referenced directly outside the adapter layer
- unknown or unhealthy backends degrade to `CanMoveWindow = false`
- if official reads still work, preserve `CanReadDesktopId = true`

### SavedDesktopId Compatibility

- `SavedDesktopId` remains a GUID string in config and snapshots.
- Existing configs must remain readable without migration.
- If the vendored backend uses a different identity object internally, the
  adapter must translate at the boundary.
- If a saved GUID cannot be resolved on the current machine/build, restore must
  fall back to placement-only behavior and log the reason.

### Vendoring Boundary

Only vendor the minimum code needed for:

- resolving a window's current desktop
- resolving a desktop object from a saved GUID
- moving a window to a target desktop

Do not vendor unrelated features such as pin/unpin, rename desktop, wallpaper,
notifications, hotkeys, or CLI surface.

Suggested isolation boundary:

- `src/WindowResizer.Core/VirtualDesktop/Backends/MScholtes/`

Repository code must call only a local adapter layer, not vendored types.

## Recommended Plan

### Phase 0: Freeze the current write path

- Keep `WindowsVirtualDesktopService` for read-only behavior.
- Stop treating official `MoveWindowToDesktop` as production-ready.
- Add capability split and backend identity logging.

Exit criteria:

- official move is no longer treated as a production write path
- logs show backend name plus `CanReadDesktopId` and `CanMoveWindow`
- restore code respects `CanMoveWindow = false` and performs placement-only
  restore without pretending the move succeeded

### Phase 1: TDD the contract before implementation

- Add contract tests for:
  - move to saved desktop succeeds
  - direct move failure plus navigation fallback is logged and surfaced
  - unsupported backend becomes `Fallback`, not silent success
  - invalid saved desktop IDs stay non-fatal
- Add selector tests for:
  - known Windows build selects the expected backend
  - unknown build degrades to read-only
  - unhealthy vendored backend falls back to the official read backend
  - restore coordinator keeps placement restore when `CanMoveWindow = false`
- Promote the runtime probe into a repeatable integration harness.

Exit criteria:

- contract tests and selector tests exist and start red
- test doubles cover capability split, backend selection, and fallback paths
- if navigation fallback is chosen, it has its own tests through a separate
  interface boundary
- the runtime probe can run without touching user windows outside the harness

### Phase 2: Build a vendored adapter spike

- Pull only the minimum source needed from the maintained candidate route.
- Wrap it behind `ExplorerVirtualDesktopService`.
- Validate license terms, attribution requirements, and `net47/net48` fit.
- Keep the public application boundary stable.

Exit criteria:

- vendored code is isolated under a dedicated backend directory
- license and attribution requirements are documented
- the adapter compiles against the current targets
- at least one known Windows build passes a real-window move probe through the
  new backend

### Phase 3: Integrate with guarded rollout

- Detect Windows build at runtime.
- Select the matching build-specific backend.
- Fall back to read-only mode on unknown build, Windows Server, Explorer rebind
  failure, or backend initialization failure.
- Add structured logs for backend selection, move failures, and fallback
  reasons.

Supported build contract should be explicit, for example:

- Windows 10 22H2: `10.0.19045.x`
- Windows 11 23H2: `10.0.22631.x`
- Windows 11 24H2: `10.0.26100.x`

Fallback contract:

- preserve read support when the official backend still works
- set `CanMoveWindow = false`
- emit warning logs
- surface a non-blocking restore summary when movement is disabled

Exit criteria:

- GUI and CLI resolve through the same selector/factory chain
- unknown-build and unhealthy-backend paths are covered by tests
- logs clearly state which backend was selected and why movement was disabled

### Phase 4: Validate on real desktops

Required matrix:

- Windows 10 22H2 (`10.0.19045.x`)
- Windows 11 23H2 (`10.0.22631.x`)
- Windows 11 24H2 (`10.0.26100.x`)

Required scenarios:

- unique titles
- duplicate titles
- minimized windows
- cloaked windows
- elevated windows
- unknown-build fallback
- Explorer restart followed by restore retry

Exit criteria:

- the matrix records pass/fail plus fallback behavior for each target build
- at least three target environments have archived probe output
- unknown-build behavior is verified as read-only degradation, not crash or
  silent move failure

## Decision

The recommended corrective path is:

1. Keep the official Microsoft interface for read-only detection.
2. Replace the move/write path with a vendored, build-specific candidate
   backend behind the existing application service boundary.
3. Validate the candidate backend with a spike before committing to it as the
   production write path.
4. Treat a future migration to `Grabacr07/VirtualDesktop` as a separate
   platform modernization project.

## Sources

- Microsoft Learn: `IVirtualDesktopManager`
  - https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ivirtualdesktopmanager
- Microsoft Learn: `IVirtualDesktopManager::MoveWindowToDesktop`
  - https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ivirtualdesktopmanager-movewindowtodesktop
- Microsoft Q&A: current-desktop ID discussion
  - https://learn.microsoft.com/en-us/answers/questions/1361084/how-to-find-the-current-virtual-desktop-id
- Raymond Chen: virtual desktops are for end users, not general orchestration
  - https://devblogs.microsoft.com/oldnewthing/20201123-00/?p=104476
- Grabacr07 VirtualDesktop
  - https://github.com/Grabacr07/VirtualDesktop
- MScholtes VirtualDesktop
  - https://github.com/MScholtes/VirtualDesktop
- MScholtes PSVirtualDesktop
  - https://github.com/MScholtes/PSVirtualDesktop
