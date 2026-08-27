# DBH Piped Waste 0.1.5-test 静态验证报告

验证基准：RimWorld 1.6.4871 rev591、Dubs Bad Hygiene 3.1.2800、Harmony 2.4.2.0；本机检查日期 2026-08-26。

## 已完成

- C# 7.3 / .NET Framework 4.7.2 Release 编译成功，零编译错误，生成 `DBHPipedWaste.dll`。
- 13 个分发 XML 均可由 XML 解析器读取。
- 程序集直接引用为 `0Harmony`、`Assembly-CSharp`、`BadHygiene`、`mscorlib`、`System.Core`、`UnityEngine.CoreModule`、`UnityEngine.IMGUIModule`；没有 DBH for Medieval 或 Medieval Overhaul 程序集引用，也没有复制依赖 DLL。
- 已安装 DBH 中存在 `FecalSludge`、`Biosolids`、`Plumbing`、`SewageSludgeComposting` 与 `SepticTanks`。管道精炼机现使用本 Mod 的零实体原料专用配方，不再依赖 DBH 原 `Make_ChemfuelFromFecalSludge` 的原料搜索结构。
- 已安装 Medieval Overhaul 中存在 `DankPyon_IronIngot` 与 `DankPyon_ComponentBasic`。
- 英文与简体中文 Keyed 文件各 32 个键，键集合完全一致；旧的跨 tick“正在自动供污”状态键已同时移除。
- 开发者污水注入工具只在 `Prefs.DevMode` 为真时生成 Gizmo；注入路径直接调用所选建筑当前 `CompPipe.pipeNet.PushSewage(amount)`，没有直接改写任一建筑缓冲或 DBH 管网列表。
- 自动供污由每台生产设备独立地每 30 ticks 发起一次最多 10 L 的直接库存请求；来源按污水储量最高、再按稳定 `ThingID` 选择，目标只有发起请求的设备，正常脉冲不调用 DBH 原生 `PushSewage()`。仅当 Pit 因 DBH 外部入口出现超过 7500 L 的超额库存时，才会调用该 API 尝试排出超额部分。
- 自动脉冲实际移动量为 `min(10 L, 来源坑储量, 发起设备剩余容量)`，使用 0.01 L 容差判断有效空闲容量；不再计算同级平均份额，也不检查或修改未知 Mod 接收端。
- 内部转移在所有校验通过后同时提交来源扣减与 DBHPW 目标增加；不会临时修改任何第三方 `Blocked` 状态。手动转移仍按 DBHPW 目标优先级和容量安全地逐轮分配。
- 公式场景复核通过：单台设备剩余 4.25 L 时只从最高储量坑扣 4.25 L；剩余 0.8 L 时只传 0.8 L；两个设备分别脉冲时按各自请求顺序从当前最高储量坑扣减。
- 旧 `automaticSupplyRequestExpiresAt`、`AutomaticSupplyActive`、`RequestAutomaticSupply()` 与 `RequestPitSupply()` 已从源码及分发文本移除；玩家手动转移仍独立保持每 10 ticks 1 L。
- 管道精炼机现在明确声明 `tickerType=Normal`，并让 `CompPipedRefinerySewage` 继承堆肥桶接收组件；因此 30-tick 自动申请与 10-tick 手动转移会实际执行。精炼任务改由原版 `WorkGiver_DoBill` 派发，旧专用 JobDef 仅保留旧存档兼容。
- `TryFindBestBillIngredients` 前置补丁对 `chosen` 与 `missingIngredients` 均使用空值安全清理，兼容原版只查询可执行性时传入空缺料列表的路径，避免账单变化和生产完成后的下一轮工作搜索产生空引用。
- 专用配方只在精炼机 `<recipes>` 中注册；`recipeUsers` 重复入口已删除，运行时还会把精炼机配方列表防御性重设为唯一一项。失去当前有效任务的污水预留会在组件 tick 中自动释放，避免旧预留永久阻止转出。
- 手动转移只在 DBHPW 自有组件之间按目标优先级和剩余容量计算安全移动量，避免超过容量；存粪坑的紧急抽空改为可开关的 `Command_Toggle`。
- 紧急抽空只使用 `FloorToInt()` 转换完整污水单位；小数余量保留在 Pit 中，且没有可转换的完整单位时自动结束紧急请求，避免 `CeilToInt()` 造成资源增殖。
- 已通过 Postfix 覆盖 DBH `Alert_BlockedSewer.GetReport()`：有有效的非 DBHPW culprit 时完全尊重 DBH 原始结果；只有原结果完全由堆肥桶/燃料精炼器造成时才过滤误报或补回真实堵塞排污口；原结果为空时仅检查 Pit 可能掩盖的真实 `CompSewageOutlet` 故障。无显式排污口时，Pit 及其它 DBH 污水处理器仍保留兼容的全堵塞判断。
- Refinery 专用配方已采用 Fail-Closed：结构校验或 Refinery Harmony 目标失败时，从 Refinery Def 与 Recipe 用户列表移除配方；WorkGiver、Job 预留、Vanilla 成品生成包装和旧版兼容 Job 均会阻止禁用的专用 Bill，避免无污水消耗却产出 Chemfuel。
- Refinery 启动校验已集中为 `ValidatePipedRefineryStructure()`，以结构化失败原因区分配方、Def、Harmony、ingredient、product 与组件问题；旧存档账单清理只会移除已知 Vanilla Refinery 配方、禁用的专用污水配方和重复专用账单，不会误删其它自定义账单。
- Refinery Harmony 初始化现在只在启动阶段输出一次诊断信息：RimWorld、DBH、Harmony 程序集版本，各目标方法的预期签名与实际候选方法；若补丁失败，日志会标明失败目标和 Fail-Closed 结果，不进入 Tick 热路径刷屏。
- Settings 的供电规则现在实时读取当前值：切换 `sewageIntakePumpsRequirePower` 后会立即遍历所有已加载地图的 `CompPipedSewageHandler` 并刷新目标功耗；不再显示 Reload 提示。
- 目标功耗统一由 `baseMachinePowerConsumption + (PumpsRequirePower ? pumpPowerConsumption : 0)` 计算，且无条件写入 `PowerOutput`；XML 中堆肥器基线为 0 W、精炼器基线为 170 W。
- 代码结构整理首项已完成：`CompPipedSewageHandler` 不再隐藏 DBH 基类的 `Props` 属性，改用明确的 `PipedProps`，相关调试与组件引用已同步，行为保持不变。
- Refinery 预留状态已收紧：`reservedSewage` 与 `reservationPawn` 改为私有 backing field，仅通过预留、消费、释放方法修改；对 Job 检查提供只读的 `ReservedSewage` 与 `ReservationPawn`，存档字段名保持兼容。
- Pit 目标液位状态已收紧：`targetPercent` 改为私有 backing field，对外提供只读 `TargetPercent` 和统一的 `SetTargetPercent()`；滑块修改会自动进行百分比清理并立即刷新自动抽取状态，存档键保持不变。
- 管道组件的 `transferMode` 已收紧为私有 backing field，对外提供只读 `TransferMode` 和统一的 `SetTransferMode()`；手动切换会集中刷新阻塞状态，存档键保持不变。
- Pit 的 `autoExtractionActive` 已收紧为私有状态，对外仅提供只读 `AutoExtractionActive`；状态仍由 `UpdateAutoExtractionState()` 统一维护，存档键保持不变。
- Pit 的 `autoExtract` 与 `manualExtractionRequested` 也已收紧为私有状态，对外通过 `AutoExtract`、`ManualExtractionRequested` 只读访问，并分别通过 `SetAutoExtract()`、`SetManualExtractionRequested()` 修改；关闭手动抽空时会统一清理自动抽取活动状态。
- `Building_PipedComposter.RepairLinesAfterLoad()` 已重命名为 `EnsureFermentationLineInvariant()`，准确表达该方法在生成、读档和发酵 Tick 中持续保证发酵线结构有效的职责；逻辑未改动。
- `Command_SetSewageTarget` 已从 `PipedSewageComponents.cs` 独立到 `Command_SetSewageTarget.cs`，仅调整文件归属与项目编译清单，UI 命令行为未改变。
- `CompProperties_PipedSewageHandler` 已从 `PipedSewageComponents.cs` 独立到 `CompProperties_PipedSewageHandler.cs`，保留原有 `compClass` 绑定与属性字段，仅调整文件归属。
- `CompPipedComposterSewage` 已从 `PipedSewageComponents.cs` 独立到 `CompPipedComposterSewage.cs`，保留 Overflow、资源结算和存档字段逻辑，仅调整文件归属。
- `CompPipedRefinerySewage` 已从 `PipedSewageComponents.cs` 独立到 `CompPipedRefinerySewage.cs`，保留预留、消费、回滚、存档恢复和旧账单兼容逻辑，仅调整文件归属。
- 基础组件 `CompPipedSewageHandler` 已从 `PipedSewageComponents.cs` 独立到 `CompPipedSewageHandler.cs`，保留容量、阻塞、供污、手动转移、调试注入和资源结算逻辑，仅调整文件归属。
- `CompUndergroundSewageStorage` 已从原汇总文件 `PipedSewageComponents.cs` 移至同名文件 `CompUndergroundSewageStorage.cs`；组件拆分完成，原文件已移除，存档字段与运行逻辑保持不变。
- 堆肥桶建筑的资源结算入口已由 `SettleContents()` 统一命名为 `SettleResources()`，与组件层的 `SettleResources()` 语义对齐；结算顺序和资源去向未改变。
- `1.6/Defs/ThingDefs_Buildings/DBHPW_Buildings.xml` 已完成样式整理：嵌套 Def 字段改为一行一个标签，字段值、顺序和 XML 语义未改变；全套 1.6 XML 已重新解析通过。
- Refinery 成品生成异常时，`RestoreConsumed()` 现在完整恢复已扣除的污水，即使设备在事务开始前已经超过容量；Overflow 不再在回滚阶段被截断，而是交由既有的低频 Overflow relief 路径处理。
- Refinery 配方注册已统一为单一入口：启动校验成功时只保留 `RefineryDef.recipes` 中的专用配方，并从 `RecipeDef.recipeUsers` 移除该 Refinery，避免同一个 RecipeDef 在账单菜单中重复显示；校验失败时仍会从两侧移除。
- Emergency Extraction 的小数残量条件已统一为 `HasWholeSewageUnit`：WorkGiver、Job `FailOn` 与循环 `JumpIf` 均与实际 `FloorToInt()` 抽取规则一致，`0.7 L` 不再创建无效抽取 Job。
- Refinery Fail-Closed 最终一致性审计已完成：WorkGiver、Job 创建/预留、Vanilla 成品生成包装、旧版 JobDriver、Cleanup、存档 Bill 与 Def 配方索引均具备一致的专用配方可执行性检查；未发现新的免费产出或预留泄漏路径。
- Refinery 出现两个配方的原因已确认：`DBHPW_PipedBiofuelRefinery` 继承 `BenchBase` 时原 `<recipes>` 节点未关闭继承，导致 Vanilla 配方被带入；已改为 `<recipes Inherit="false">`，仅保留专用管道污水配方。
- 堆肥桶与燃料精炼器均对超过自身配置容量的 DBH 外部输入执行超额保护：每 60 ticks 使用 `IsHashIntervalTick(60)` 错峰尝试 `PushSewage()` 转出，连续 3000 ticks 无法释放后回收到设备占地格的 `SewageGrid`，且仅在回收成功后扣减本地缓冲。
- Pit Overflow 的网络处理已改为 `parent.IsHashIntervalTick(60)` 错峰检查；无接收端时不再每个 tick 调用 `PushSewage()`，超时使用真实 `overflowStartedTick` 计算。
- DBHPW 内部转移完成后会调用接收端的 `NotifyPotentialSewageIncrease()`；Pit 若被内部路径检测到可能增加，会立即尝试处理 Overflow，60-tick 检查继续负责 DBH 或第三方直接写入的兜底。
- Pit Overflow 超时已改为保存真实 `overflowStartedTick`，按游戏 Tick 计算 3000 ticks；重复事件通知或 60-tick 检查不会重复累加计时。
- Auto Extraction 状态更新与 Pit Overflow 共用 60-tick 错峰入口；切换自动抽取或修改目标百分比时通过 `RefreshAutoExtractionState()` 立即刷新，玩家操作不会等待下一个周期。
- `overflowPending` 冗余字段已删除；Pit 是否处于 Overflow 状态统一由 `overflowStartedTick >= 0` 推导，Debug 模式下通过 `OverflowPendingForDebug` 输出。
- Pit Overflow 现在使用组件的 `Capacity` 计算，不再保留独立的固定 7500 L 阈值；因此 XML 容量调整后，`Blocked`、自动供污可用容量和 Overflow 判断保持一致。
- 当前 RimWorld 程序集中的 Harmony 目标签名已反射确认：
  - `WorkGiver_DoBill.TryFindBestBillIngredients(Bill, Pawn, Thing, List<ThingCount>, List<IngredientCount>) -> bool`
  - `JobDriver_DoBill.TryMakePreToilReservations(bool) -> bool`
  - `Toils_Recipe.FinishRecipeAndStartStoringProduct(TargetIndex) -> Toil`
  - `JobDriver.Cleanup(JobCondition) -> void`
  - `Building_AssignableFixture.Working(float) -> AcceptanceReport`
  - `Building_Latrine.Working(float) -> AcceptanceReport`
- 隔离的 Harmony + DBH + 本 Mod 组合已使用 Direct3D 11 隐藏启动；`Player-validation-014.log` 确认载入 `Chinafkd.DBHPipedWaste` 并输出唯一专用配方标记，没有匹配到归属于本 Mod 的 Config、Def、XML、Harmony或静态构造异常。玩法场景仍以用户实机测试为准。
- 无图形设备的附加尝试在 RimWorld 全局纹理图集阶段产生空显卡异常；这些堆栈均来自 `GlobalTextureAtlasManager`，不是本 Mod。随后使用 Direct3D 11 隐藏窗口复测，未再出现这些异常。

## 五种组合的静态边界

- Core + DBH 与 DBH Lite：核心 Def 只引用 DBH 3.1.2800 已存在的 Lite 标记资源/研究；基础加载已执行，Lite 玩法切换仍需用户实机确认。
- DBH for Medieval：其 Processor Framework 替换目标是原 `BiosolidsComposter`，不会匹配 `DBHPW_PipedComposter`；本程序集不引用其 DLL。
- Medieval Overhaul：条件目录只在包 `DankPyon.Medieval.Overhaul` 激活时载入，五个成本 XPath 均指向本 Mod 的现有节点，替换 Def 在当前安装中存在。
- 两个可选 Mod 同开：DBH for Medieval 不修改本 Mod Def，MO 成本补丁只执行一次，因此没有相互覆盖的本 Mod 补丁。

## 尚未声称通过

建筑放置、真实管网优先级、每个 Job、保存/载入、温度损坏、拆除/摧毁与所有可选 Mod 组合的玩法场景尚未由用户实机完成。请以 `TEST_MATRIX.md` 记录；静态验证不把这些项标为通过。
