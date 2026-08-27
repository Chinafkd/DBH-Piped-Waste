# DBH: Piped Sewage Processing 0.1.5

这是一个面向 RimWorld 1.6 与 Dubs Bad Hygiene 3.1.2800 的发布版本，作者为 Chinafkd。

当前提供三个独立建筑：

- 管道堆肥桶：priority 100，250 污水缓冲，5 条各 50 单位的独立发酵线路。
- 管道燃料精炼机：priority 100，225 污水容量；同时提供原版木材和有机物配方、DBH 粪便污泥配方，以及一个每次消耗 75 污水并生产 35 化学燃料的专用管道配方。
- 地下存粪坑：priority 50，7500 污水容量，支持自动每批抽取 75，以及由同一殖民者持续执行、每 120 ticks 最多抽取 75 的可开关“紧急抽空”。

三者仍通过 `CompSewageHandler` 接入 DBH 管网，因此厕所等设施产生的新污水仍由 DBH 的 `PlumbingNet.PushSewage()` 负责公共分配。已经进入本 Mod 存储中的污水属于 DBHPW 内部库存：自动供污和手动转移只在本 Mod 的组件之间直接扣减/增加，不调用 DBH 的二次传送，也不会修改其它 Mod 的 `Blocked` 状态。

存粪坑的默认容量为 7500 L，Overflow 保护始终以组件当前配置的 `Capacity` 为准。由于 DBH 原生 `PushSewage()` 本身不执行容量截断，极少数情况下可能产生少量超额污水；存粪坑会在每个 tick 尝试把超额部分重新 `PushSewage()` 到其它可用接收端。若超额状态连续 3000 ticks 仍未释放，则将超额部分均匀排入存粪坑占地格的地面污水网格，并从坑内扣除，避免库存永久高于当前容量。

管道堆肥桶和管道燃料精炼机也使用相同的超额保护：当 DBH 外部入口导致其缓冲超过各自配置容量（250 L / 225 L）时，先尝试向其它接收端 `PushSewage()`；连续 3000 ticks 无法释放时，将超额部分排入设备占地格的地面污水网格并扣除。

管道燃料精炼机明确使用普通 tick，与堆肥桶复用同一套管道接收、30-tick 自动申请和 10-tick 手动转移逻辑。配方菜单从建筑端注册标准精炼配方和一个专用管道污水配方；专用配方不会通过 `recipeUsers` 再次注册，因此不会重复显示。

殖民者生产重新使用 RimWorld 原版精炼器的 `WorkGiver_DoBill` 流程；Mod 只在原料检查阶段把精炼器缓存中的 75 L 污水视为本次虚拟原料，在任务开始时预留，并在成品生成阶段扣除。旧版专用 JobDef 仍保留，只用于兼容升级时可能仍在执行的旧任务。

原料检查补丁兼容 RimWorld 只查询“能否生产”而不提供缺料列表的扫描路径；账单列表变化与生产完成后的下一轮工作搜索不会再因空的 `missingIngredients` 参数报错。

## 开发者测试工具

开启 RimWorld 开发者模式后，选中任意一个本 Mod 建筑，会出现“调试：注入污水”按钮。点击后可在 1–10000 之间选择注入量，默认是单次精炼配方所需的 75。

工具调用 DBH 原生 `PlumbingNet.PushSewage()`，用于调试公共污水入口的 priority、同级平均分配和 `Blocked` 行为。它不代表自动供污路径；自动供污不会调用该 API。

## 存粪坑自动供污

每台管道堆肥桶或管道燃料精炼机独立地每 30 ticks 自检一次；只要设备尚未有效满载，就会发起一个最多 10 L 的直接库存请求。发起请求的设备就是本次唯一接收端，不会把污水分配给管网中的其它设备。

脉冲只选择同一管网中当前储量最高的一个可用地下存粪坑，储量相同则按稳定的建筑 ID 选择；来源不足时不会再找第二个坑补足。实际移动量为 `min(10 L, 来源坑储量, 发起设备剩余容量)`，成功后同时更新来源和目标库存。其它 Mod 的接收端、优先级和 `Blocked` 状态不会参与这条内部物流路径。

存粪坑不会在两次脉冲之间保持自动转移状态。玩家手动“转移污水”仍独立保持每 10 ticks 传送 1 L；开启手动转移的存粪坑不会成为自动脉冲来源。

## 测试安装

1. 确认启用 Harmony 与 Dubs Bad Hygiene。
2. 将 `Demo/DBHPipedWaste` 整个文件夹复制到 RimWorld 的 `Mods` 目录。
3. 加载顺序使用：Harmony → Dubs Bad Hygiene → 可选兼容 Mod → DBH: Piped Sewage Processing。
4. 第一次测试建议新建地图，并按 `Docs/TEST_MATRIX.md` 逐项记录。

当前仓库提供三类交付物：`Release/DBHPipedWaste-0.1.5.zip` 是可直接安装的运行时 Mod 包，`Release/WorkshopUpload/DBHPipedWaste` 是 Steam Workshop 上传目录，`GitHub/DBH-Piped-Waste-Source-0.1.5.zip` 是包含源码、文档和构建脚本的源代码包。源码项目使用 `build.ps1` 编译，使用 `build_demo.ps1` 重新生成干净测试目录，使用 `build_release.ps1` 一次性重建发布包。

本版本已内置由项目游戏截图制作的 Workshop 预览图，文件位于 `About/Preview.png`。
