# 多虚拟桌面支持计划

## 1. 需求更新与总体结论

用户已明确要求：**`Save All / Restore All` 的跨虚拟桌面恢复必须在 V1 完成。**

这会直接改变实现边界。基于当前代码结构，V1 不能只是在现有 `WindowSize` 规则上追加一个 `SavedDesktopId` 字段，然后把 `Save All / Restore All` 接进去。原因很明确：

- 当前规则模型以 `process + title` 为核心
- `Save All` / `Restore All` 复用共享规则
- `UpdateOrSaveConfig` 会联动更新 `FullMatch` / `PrefixMatch` / `SuffixMatch` / `WildcardMatch`

如果继续沿用这套模型，虚拟桌面 ID 会被共享规则污染，最终无法可靠实现批量跨桌面恢复。

**因此本计划的结论是：V1 必须采用“双模型方案”。**

- **规则模型**：继续服务单窗口 `Save / Restore` 和自动恢复
- **布局快照模型**：专门服务 `Save All / Restore All` 的跨虚拟桌面恢复

这不是额外复杂化，而是实现你这个新要求的最小正确方案。

## 2. 术语定义

### 2.1 精确标题规则

精确标题规则指 `Title` 与窗口标题 **完全相等** 的规则。

例如当前窗口标题是 `README.md - Visual Studio Code`：

- 精确标题规则：`README.md - Visual Studio Code`
- 前缀通配规则：`*Visual Studio Code`
- 后缀通配规则：`README.md*`
- 全通配规则：`*`

### 2.2 布局快照项

布局快照项不是现有 `WindowSize` 规则，而是 `Save All` 时对某一个真实窗口实例做出的独立快照，包含：

- 进程名
- 精确标题
- 窗口类名
- 保存时所属虚拟桌面 ID
- 位置与状态
- 在重复窗口集合中的捕获顺序

`Restore All` 将基于这些快照项做批量恢复，而不是回写到共享规则里。

## 3. 平台事实与硬边界

V1 只使用 Windows 公开 API `IVirtualDesktopManager`：

- `GetWindowDesktopId`
- `IsWindowOnCurrentVirtualDesktop`
- `MoveWindowToDesktop`

同时补一个轻量 DWM 诊断层，用于识别 `DWMWA_CLOAKED`，确保跨虚拟桌面枚举和日志可定位。

硬边界：

- 最小支持系统：**Windows 10 desktop apps**
- Windows 7/8.1：能力自动降级为关闭，不报错，不改旧行为
- V1 不使用 undocumented 的 `IVirtualDesktopManagerInternal`
- V1 不自动切换用户当前所处虚拟桌面
- V1 不支持桌面枚举、桌面命名、桌面切换通知

## 4. 现有代码约束

当前仓库有四个必须前置处理的约束：

1. `WindowUtils`、`ConfigFactory`、`Resizer` 的静态耦合过重，无法直接稳定做 TDD。
2. 自动恢复和手动恢复共用 `ResizeWindow` 路径，若不拆分，手动跨桌面逻辑会污染 `OnWindowCreated`。
3. 现有 `Save All` / `Restore All` 基于规则模型，而不是实例快照模型。
4. 枚举窗口时不能只看当前桌面上的普通可见窗口，跨桌面批量恢复需要识别其他桌面的顶层窗口与 cloaked 状态。

因此，**测试工程、编排层拆分、批量快照模型** 都是 V1 前置项，不是“如果有空再做”的优化。

## 5. TDD 执行原则

本功能严格遵循 TDD：

1. 先建测试工程和 fake 边界
2. 先写失败测试
3. 再补最小实现
4. 最后做手工验证和回归

TDD 的重点放在业务决策层：

- 何时允许记录单窗口的桌面 ID
- 何时允许 `Restore All` 跨桌面搬运
- 何时必须降级为只恢复位置
- wildcard 规则是否被错误写入桌面 ID
- 批量重复窗口如何做确定性配对
- 自动恢复是否被明确隔离
- 旧配置迁移是否正确补齐新字段

### 5.1 测试工程前置任务

先新增测试工程，例如：

- `tests/WindowResizer.Base.Tests`
- `tests/WindowResizer.Configuration.Tests`

建议测试项目引用 `net6.0-windows` 目标，降低测试夹具成本。

首批可替身边界至少包括：

- `IVirtualDesktopService`
- `IWindowEnumerator`
- `IWindowMetadataService`
- `IWindowPlacementService`
- `IConfigRepository`
- `ILayoutSnapshotRepository`

## 6. 架构调整

### 6.1 编排层拆分

新增以下协调器：

- `RuleSaveCoordinator`
- `RuleRestoreCoordinator`
- `LayoutSnapshotSaveCoordinator`
- `LayoutSnapshotRestoreCoordinator`
- `AutoRestoreCoordinator`

职责划分：

- `RuleSaveCoordinator`：前台窗口 `Save`
- `RuleRestoreCoordinator`：前台窗口 `Restore` 和 CLI `resize`
- `LayoutSnapshotSaveCoordinator`：`Save All`
- `LayoutSnapshotRestoreCoordinator`：`Restore All`
- `AutoRestoreCoordinator`：窗口创建事件，V1 不允许跨虚拟桌面搬运

### 6.2 核心服务层

新增目录建议：

- `src/WindowResizer.Core/VirtualDesktop/`
- `src/WindowResizer.Base/Coordinators/`
- `src/WindowResizer.Configuration/LayoutSnapshots/`

其中 `Resizer` 继续负责 placement，虚拟桌面服务只负责桌面 ID 获取与窗口跨桌面移动。

## 7. 数据模型设计

### 7.1 规则模型

规则模型继续保留，并只增加：

- `WindowSize.SavedDesktopId: string?`
- `Config.EnableVirtualDesktopRestore: bool`

规则模型在 V1 中只承担：

- 单窗口 `Save / Restore`
- CLI `resize`

并且只对 **精确标题规则** 使用 `SavedDesktopId`。

### 7.2 布局快照模型

V1 新增独立快照模型，不复用 `WindowSize`：

- `Config.CurrentLayoutSnapshot: WindowLayoutSnapshot?`
- `WindowLayoutSnapshot.CapturedAt`
- `WindowLayoutSnapshot.Entries`

单个快照项建议包含：

- `ProcessName`
- `ExactTitle`
- `WindowClassName`
- `SavedDesktopId`
- `Rect`
- `State`
- `MaximizedPosition`
- `CaptureOrder`

如果后续验证发现 `WindowClassName` 不足以降低歧义，再追加更强特征，但 V1 先保证这套模型跑通。

### 7.3 快照生命周期合同

`CurrentLayoutSnapshot` 的生命周期必须在 V1 中明确写死：

- **按 profile 持久化**：快照属于当前 profile，跟随 `Config` 一起落盘
- **应用重启后保留**
- **profile 切换时一起切换**
- **配置导入/导出时一起带上**
- **未执行过 `Save All` 或快照为空时**，`Restore All` 必须 no-op，并给出明确 toast / log

这部分必须先定义，才能写出稳定的失败测试。

## 8. V1 行为设计

### 8.1 单窗口 Save / Restore

V1 仍然支持单窗口跨虚拟桌面恢复，但边界不变：

- 仅精确标题规则写入 `SavedDesktopId`
- wildcard / prefix / suffix 一律不写入、不消费桌面 ID
- 恢复时若命中精确标题规则且全局开关开启，则先尝试 `MoveWindowToDesktop`，再恢复位置与状态
- 失败时回退为“只恢复位置”

### 8.2 Save All

V1 的 `Save All` 必须升级为“**跨桌面布局快照保存**”，而不是简单循环写规则：

1. 枚举所有候选顶层窗口，不只看当前桌面
2. 读取每个窗口的虚拟桌面 ID
3. 为每个窗口创建独立快照项
4. 保存到 `CurrentLayoutSnapshot`

规则：

- `EnableVirtualDesktopRestore == false` 时，`Save All` 仍保存布局快照，但 **不写入任何 `SavedDesktopId`**
- `EnableVirtualDesktopRestore == true` 时，`Save All` 才记录每个快照项的 `SavedDesktopId`
- `Save All` **完全不改写 `WindowSizes`**，包括不改写位置、状态、标题条目和虚拟桌面信息
- `Save All` 继续保留当前“默认跳过最小化窗口”的语义
- 快照保存完成后，要有“保存了多少窗口、涉及多少桌面”的可观测信息

这条“完全不改写 `WindowSizes`”是 V1 的硬约束，用来防止“布局快照 + 规则表”混合副作用。

### 8.3 Restore All

V1 的 `Restore All` 必须升级为“**按快照恢复跨桌面布局**”：

1. 枚举当前所有候选顶层窗口，不只看当前桌面
2. 基于 `(ProcessName, ExactTitle, WindowClassName)` 建立候选集
3. 对重复项按 `CaptureOrder` 与当前枚举顺序做 **确定性配对**
4. 对每个已配对窗口：
   - 先尝试移动到保存时虚拟桌面
   - 再恢复位置与状态
5. 未配对项记录日志和结果统计

额外合同：

- `EnableVirtualDesktopRestore == false` 时，`Restore All` 仍可消费快照中的位置与状态，但 **不得调用跨虚拟桌面移动**
- `RestoreAllIncludeMinimized` 沿用现有含义：当它为 `false` 时，最小化的当前候选窗口不参与 `Restore All` 配对与恢复；当它为 `true` 时，最小化窗口可参与恢复
- 若没有快照，则 `Restore All` 必须 no-op，并提示“当前 profile 没有保存的布局快照”
- `IVirtualDesktopManager` 初始化失败、`SavedDesktopId` 无效或权限不足时，**仅当前窗口项** 回退为“只恢复位置与状态”，不得导致整批 `Restore All` 中断
- 部分成功/部分失败时，其余已配对窗口继续执行；最终必须输出成功数、位置回退数、未配对数和失败数

### 8.4 重复窗口语义

你要求 V1 支持 `Save All / Restore All` 跨虚拟桌面恢复，这在“多个完全同名窗口”场景下不可能做到绝对身份还原，因此必须明确 V1 语义：

- **唯一窗口**：要求高置信度恢复
- **重复窗口**：要求“确定性 best-effort”恢复，而不是承诺完美一一还原

也就是说：

- 同进程、同标题、同类名的多个窗口，V1 采用稳定配对规则
- 行为必须可预测、可测试、可记录
- 但不承诺跨应用重启后仍能识别“原来的那一个具体实例”

这个限制必须写进验收标准和用户文案，不能藏起来。

### 8.5 自动恢复

即便 `Save All / Restore All` 被纳入 V1，自动恢复仍然不应一起跨桌面化。

V1 明确要求：

- `AutoRestoreCoordinator` 不消费 `SavedDesktopId`
- 窗口创建事件不触发跨虚拟桌面移动
- 自动恢复与手动恢复的编排路径分离，并有测试保证

## 9. 启用入口与用户可见行为

V1 需要最小闭环，不接受“功能在代码里存在，但用户无法稳定启用”的半成品。

最少包含：

- 全局配置开关 `EnableVirtualDesktopRestore`
- GUI 中的显式开关
- `Save All` / `Restore All` 的结果反馈
- CLI `resize --verbose` 的桌面移动结果

建议补充的 GUI 信息：

- 当前是否启用虚拟桌面恢复
- 最近一次 `Save All` 快照时间
- 最近一次 `Save All` 快照包含的窗口数

不建议在 V1 展示虚拟桌面 GUID 原文。

全局开关语义必须统一：

- 关闭时：只保存/恢复位置与状态，不记录也不消费任何虚拟桌面 ID
- 开启时：单窗口规则和布局快照都允许记录/消费虚拟桌面 ID

不能出现“单窗口受控、批量操作失控”的双重语义。

## 10. 异常处理与可观测性

首个可试点版本就必须覆盖以下异常：

- 系统低于 Windows 10
- `IVirtualDesktopManager` 初始化失败
- `SavedDesktopId` 无效
- 目标窗口权限不足
- 目标窗口在非当前桌面且被 cloak
- `Restore All` 部分成功、部分失败
- 目标窗口数量和快照数量不一致

对应输出至少包括：

- 日志
- toast
- `Restore All` 汇总结果
- CLI verbose 文本

## 11. 分阶段实施

### 阶段 0：测试基座与编排层拆分

- 新建测试工程
- 抽出 5 个协调器
- 抽出虚拟桌面、窗口枚举、窗口元数据、placement、配置/快照仓储接口
- 写失败测试：
  - 配置迁移
  - 手动/自动恢复路径分离
  - 规则模型与快照模型分离
  - 快照按 profile 持久化、切换与重启保留的生命周期合同

这一阶段完成后，才允许开始功能实现。

### 阶段 1：单窗口虚拟桌面恢复

- 写失败测试：
  - 精确标题规则写入 `SavedDesktopId`
  - wildcard / prefix / suffix 不写入 `SavedDesktopId`
  - 手动 `Restore` / CLI `resize` 成功跨桌面
  - 失败时回退为只恢复位置
  - Win7/8.1 自动降级
- 接入 `IVirtualDesktopManager`
- 接入规则模型上的 `SavedDesktopId`
- 加入基础日志与基础 toast

### 阶段 2：`Save All / Restore All` 跨虚拟桌面布局快照

这是 **V1 必须完成** 的部分。

- 写失败测试：
  - `Save All` 保存独立快照项，而不是污染规则模型
  - `Save All` 完全不改写 `WindowSizes`
  - `EnableVirtualDesktopRestore` 对批量快照的记录/恢复生效
  - 快照导入/导出后仍保持可恢复
  - 无快照 / 空快照时 `Restore All` 正确 no-op
  - `Restore AllIncludeMinimized` 在快照模型下延续原语义
  - `Restore All` 基于快照模型执行跨桌面恢复
  - 无效 `SavedDesktopId`、权限不足、虚拟桌面服务初始化失败时按“逐窗口回退、整批继续”执行
  - 重复窗口按确定性规则配对
  - 未配对窗口有日志和结果统计
  - 部分成功/部分失败时统计值正确且不误操作
  - 自动恢复仍不跨桌面
- 新增 `WindowLayoutSnapshot`
- `Save All` 改为保存跨桌面布局快照
- `Restore All` 改为按快照恢复跨桌面布局
- 完成 GUI / toast / 结果汇总最小闭环

### 阶段 3：用户可见完善

- GUI 展示快照状态
- CLI verbose 完整输出
- 手工验证文档
- 回归多显示器 + 多虚拟桌面组合场景

**V1 发布条件 = 阶段 0、1、2、3 全部完成。**

## 12. 测试计划

自动化测试至少覆盖：

- 新配置直接反序列化补默认值
- `LoadOldConfig` 手工迁移补字段
- 精确标题规则与通配规则的桌面 ID 分支
- `Save All` 不向 `WindowSizes` 写虚拟桌面数据
- `Save All` 不改写 `WindowSizes` 的位置、状态和条目
- 全局开关关闭时批量快照不记录桌面 ID
- `Save All` 正确生成布局快照项
- 快照按 profile 持久化，并在重启、切换、导入导出后保持一致
- `Restore All` 正确调用跨桌面移动
- 无快照 / 空快照时 `Restore All` 正确 no-op
- 虚拟桌面服务初始化失败、无效桌面 ID、权限不足时按逐窗口回退执行
- 重复窗口的稳定配对
- 未配对窗口的统计与日志
- 部分成功 / 部分失败时统计值正确且不误操作
- `RestoreAllIncludeMinimized` 在快照模型下的语义保持不变
- 自动恢复路径不调用跨桌面移动
- Windows 7/8.1 自动降级

手工测试至少覆盖：

- Windows 10
- Windows 11
- 多显示器 + 多虚拟桌面组合
- 唯一窗口跨桌面恢复
- 重复窗口 best-effort 恢复
- 提权窗口失败
- 其他桌面上的 cloaked 窗口参与 `Save All / Restore All`

测试执行要求：

- 每个阶段先提交失败测试，再提交实现
- 后台测试超时上限 60s
- 无法稳定自动化的行为必须在 PR 中列为手工验证项

## 13. 验收标准

只有同时满足以下条件，V1 才算完成：

- 已建立独立测试工程与可替身边界
- TDD 证据完整：先失败、再转绿
- Windows 10/11 上，精确标题规则可跨虚拟桌面保存与恢复
- `Save All` 保存的是独立布局快照，而不是污染规则模型
- `Save All` 完全不改写 `WindowSizes`
- 布局快照按 profile 持久化，并在重启、切换、导入导出后保持一致
- `Restore All` 可依据布局快照执行跨虚拟桌面恢复
- 无快照 / 空快照时 `Restore All` 不误操作并有明确提示
- 虚拟桌面移动失败时按逐窗口回退执行，整批恢复不中断
- wildcard / prefix / suffix 规则不会被写入或消费虚拟桌面 ID
- 重复窗口按文档定义的确定性规则 best-effort 恢复
- 部分成功 / 部分失败时结果统计正确
- `RestoreAllIncludeMinimized` 在新模型下保持既有语义
- Windows 7/8.1 上不报错、不改变旧行为
- 自动恢复不会误跨虚拟桌面移动
- 失败时有日志、toast 和结果汇总
- 未引入 undocumented Windows 虚拟桌面依赖

## 14. 审核状态

- 之前基于旧范围的 reviewer 通过结论已失效
- 原因：V1 范围已从“单窗口 + CLI”升级为“必须包含 `Save All / Restore All` 跨虚拟桌面恢复”
- 新范围下最终复审结果：**3/3 PASS**
- Reviewer A：PASS，确认架构、双模型边界、批量快照合同和 TDD 前置条件自洽
- Reviewer B：PASS，确认全局开关、规则/快照职责边界、生命周期和用户可见行为闭环
- Reviewer C：PASS，确认快照生命周期、逐窗口回退、部分成功统计和 TDD 闭环已补齐
- 当前状态：**已通过，可作为实施基线**
