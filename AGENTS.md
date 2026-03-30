# Repository Guidelines

## Project Structure & Module Organization
- `src/WindowResizer`: WinForms tray application and settings UI.
- `src/WindowResizer.CLI`: command-line entry point for resize actions.
- `src/WindowResizer.Base`, `src/WindowResizer.Core`, `src/WindowResizer.Common`, `src/WindowResizer.Configuration`: shared window, hotkey, DPI, and config logic.
- `installer`: release scripts, nuspec, and portable config template.
- `packaging/WindowResizer.Packaging`: Microsoft Store packaging project and image assets.
- `.github/workflows`: tag-based release automation.

Keep feature logic in the shared libraries when it can be reused by both the GUI app and CLI. Treat `*.Designer.cs`, `Resources.Designer.cs`, and `.resx` files as generated artifacts.

## Build, Test, and Development Commands
- `dotnet restore`: restore NuGet packages for the solution.
- `dotnet build WindowResizer.sln -c Release`: compile all projects.
- `dotnet run --project .\src\WindowResizer.CLI\ -- resize -h`: inspect CLI behavior locally.
- `pwsh .\installer\build.ps1 1.2.3`: build the GUI app, create Squirrel packages, and create the portable zip.
- `pwsh .\installer\build-cli.ps1 1.2.3`: publish and zip the CLI release.

Release scripts expect a semantic version and local tooling such as `nuget` and `7z`.

## Coding Style & Naming Conventions
- Use 4 spaces for indentation, `UTF-8`, and `CRLF`; this matches `.editorconfig`.
- Follow existing C# naming: `PascalCase` for types, files, methods, and properties; `camelCase` for locals and parameters.
- Keep one primary type per file unless the project already uses WinForms partial classes.
- Preserve the namespace style already used in each project instead of mixing styles within the same file set.
- Add comments only for non-obvious interop, hotkey, or window-management behavior.

## Testing Guidelines
There is currently no dedicated `tests/` project. At minimum, contributors should run `dotnet build WindowResizer.sln -c Release` and manually verify the changed flow on Windows x64. For reusable logic added to shared libraries, prefer adding a focused test project alongside the affected module rather than embedding ad hoc checks in the app.

## Commit & Pull Request Guidelines
Recent history uses Conventional Commit prefixes such as `feat:`, `fix:`, `docs:`, and `chore:`. Keep commits focused and describe the user-visible change, for example `fix: guard NotifyIcon text length`.

Pull requests should include a short summary, linked issues when applicable, manual verification steps, and screenshots or GIFs for tray or settings UI changes. Do not commit personal config files, generated release folders, or packaged binaries.
