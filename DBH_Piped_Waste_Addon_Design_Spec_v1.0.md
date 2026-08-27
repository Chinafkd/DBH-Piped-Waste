# DBH 管道粪污自动化附属 Mod —— 设计规格 v1.0

> 目标游戏：RimWorld 1.6  
> 必需前置：Dubs Bad Hygiene（DBH）3.1.2800  
> DBH packageId：`Dubwise.DubsBadHygiene`  
> 文档状态：**玩法方案冻结 / 可进入实现阶段**  
> 整理日期：2026-08-25

---

## 1. 项目目标

本 Mod 是 Dubs Bad Hygiene（以下简称 DBH）的附属 Mod，核心目标是把 DBH 已有的“厕所产生污水 → 管网处理/排放”流程扩展为一套优先利用粪污的自动化生产链：

```text
厕所 / 卫生设施产生污水
        ↓
[最高优先级]
管道堆肥桶 / 管道燃料精炼机
        ↓ 生产设备满载、关闭或无法继续接收
[中优先级]
地下存粪坑
        ↓ 存粪坑满载
[更低优先级]
DBH 原生污水处理设施
        ↓
DBH 原生排污口 / 其他最终出口
```

设计原则：

1. **尽可能复用 DBH 原生管网，而不是重写管网。**
2. 生产设备优先利用污水，地下存粪坑负责大容量兜底，原生排放设施负责最终溢流。
3. 不修改 DBH `PlumbingNet.PushSewage()` 的既有分配行为。
4. 资源在正常拆除时尽量守恒；暴力摧毁时污水类资源泄漏到 DBH `SewageGrid`。
5. 新建筑全部独立存在，**不直接修改原建筑**，但复用原建筑贴图和视觉语言。
6. DBH 是硬依赖。缺少 DBH 时直接由 RimWorld 报告缺少依赖，不提供降级运行模式。

---

## 2. 已核对的 DBH 3.1.2800 行为

以下结论来自本次提供的 DBH 3.1.2800 反编译源码与接口参考。

### 2.1 污水接收器自动进入管网

`PlumbingNet.InitNet()` 会遍历管网中的 `ThingWithComps`，发现任何继承自：

```csharp
DubsBadHygiene.CompSewageHandler
```

的组件后，自动加入：

```csharp
PlumbingNet.Sewers
```

之后按：

```csharp
CompSewageHandler.Props.priority
```

从高到低排序。

因此本 Mod 的三个污水接收建筑应通过正常的 `CompPipe + CompSewageHandler` 体系接入 DBH，而不应手工修改 `PlumbingNet.Sewers`。

---

### 2.2 `PushSewage()` 已经提供优先级分流

DBH 3.1.2800 的核心逻辑可概括为：

```text
找出所有 Blocked == false 的污水接收器
        ↓
取得其中最高 priority
        ↓
只向这一优先级的接收器输送
        ↓
同 priority 的接收器平均分摊本次污水
```

因此本 Mod 不需要实现自己的污水寻路器。

推荐固定优先级：

```text
生产设备：priority = 100
地下存粪坑：priority = 50
DBH 原生设备：保持 DBH 自己的 priority
```

目前源码中至少能看到 DBH 自己存在 `priority = 2` 的化粪处理配置，而 `CompProperties_SewageHandler` 默认 priority 为 0，因此 100 / 50 为本附属 Mod 留出了足够明显的层级。

玩家**不能在 UI 中修改这些优先级**。

---

### 2.3 不修正 DBH 的“过量灌入”行为

DBH 当前 `PushSewage()` 在分配时不会检查每个接收器的实际剩余容量，而只是把污水直接加到：

```csharp
sewageBuffer
```

本 Mod **明确不修改这一逻辑**。

原因：

- 修改后会改变整个 DBH 管网的全局行为；
- 兼容其他 DBH 附属 Mod 的难度会显著增加；
- 工作量与维护风险远高于本 Mod 的目标；
- 本项目应当是 DBH 管网的扩展消费者，而不是管网重写 Mod。

本 Mod 自己的组件仍会及时更新 `Blocked`，但不会 Harmony Patch `PushSewage()` 做容量安全重分配。

---

### 2.4 `CompSewageHandler.WorkingNow`

DBH 原类已经综合检查：

- `FlickUtility.WantsToBeOn(parent)`
- 电力
- 燃料
- 故障状态

本 Mod 的生产设备污水接收组件应尽量复用这套语义。

但需要额外满足本 Mod 的规则：

```text
生产设备关闭 → 停止从管道接收污水
生产设备断电 → 停止从管道接收污水
发酵槽中已经开始的堆肥 → 不因关闭/断电停止发酵
```

---

### 2.5 原版堆肥桶的等价关系

DBH `Building_Composter`：

```csharp
MaxCapacity = 250
```

原料进入时增加 `wortCount`。

完成后 `TakeOutBeer()`：

```csharp
thing.stackCount = wortCount;
```

也就是：

```text
1 FecalSludge → 1 Compost
```

所以本 Mod 五条发酵线路每条固定为：

```text
50 sewage-equivalent
        ↓
完整发酵
        ↓
50 Compost
```

总计 5 条：

```text
250 → 250 Compost
```

与 DBH 原版物质比例保持一致。

---

### 2.6 原版堆肥时间

原版：

```csharp
pertick = 3.3333333E-06f
```

并在 `TickRare()` 中按 250 ticks 推进。

理想温度下完整进度约为：

```text
300,000 ticks
≈ 5 个 RimWorld 游戏日
```

本 Mod **每一条 50 容量发酵线路都使用完整的原版发酵时间**。

拆成 5 条线路不会把发酵时间缩短为五分之一，因此不会凭空获得五倍理论吞吐量。

---

### 2.7 DBH 污水 → `FecalSludge` 的原生换算

DBH 原版化粪池与茅坑在将污水实体化时采用：

```csharp
Mathf.CeilToInt(sewageBuffer)
```

然后生成相同 `stackCount` 的：

```csharp
DubDef.FecalSludge
```

因此本 Mod统一采用：

```text
实体化数量 = CeilToInt(污水量)
```

正常整数状态下即：

```text
1 sewage = 1 FecalSludge
```

浮点余量在实体化时遵循 DBH 原生向上取整行为。

---

### 2.8 DBH 水罐的“转移内容物”语义

`CompWaterStorage` 原生拥有 `TransferStorageTank` 开关。

其行为不是瞬间删除内容物，而是：

```text
开启转移模式
→ 周期性尝试把内容物送回当前管网
→ 能送出多少就送多少
→ 送不出去的内容物仍保留
```

本 Mod 的污水“转移内容物”应模仿这一交互，而不是做成殖民者工作，也不是在网络无接收端时禁用按钮。

---

### 2.9 DBH 化粪池摧毁泄漏

`CompSepticTank.PostDestroy()` 会把剩余污水平均分配到建筑占地格，并加入：

```csharp
MapComponent_Hygiene.SewageGrid
```

本 Mod 暴力摧毁时沿用这一资源泄漏语义。

---

## 3. 新增建筑总览

新增三个独立建筑：

| 建筑 | 定位 | 污水容量 | 需要管道 | 是否生产 | 可人工抽取 |
|---|---|---:|---|---|---|
| 管道堆肥桶 | 自动堆肥 | 250 缓冲 + 5×50 发酵 | 是 | Compost | 否 |
| 管道燃料精炼机 | 污水直接参与粪便燃料配方 | 3 个配方批次 | 是 | Chemfuel | 否 |
| 地下存粪坑 | 大容量缓冲 | 7500 | 是 | 无 | 是，75/次 |

三个建筑：

- 使用各自对应原建筑的贴图/视觉资源；
- 使用新的 `ThingDef`；
- 不替换、不 Patch 原建筑 Def；
- 全部 `minifiable = false`；
- 不允许卸载、打包、整体搬运；
- 正常搬迁方式为“拆除 → 重建”。

---

# 4. 管道堆肥桶

## 4.1 定位

管道堆肥桶是 DBH 原版堆肥桶的自动化管道版本。

它：

- 只从污水管接受原料；
- **不接受殖民者手工搬运 `FecalSludge`**；
- 保留原版堆肥的温度概念、发酵时间与产物比例；
- 通过五条独立线路解决“250 整桶发酵期间完全无法接收下一批污水”的问题。

---

## 4.2 内部结构

```text
污水管
  ↓
[缓冲槽 250]
  ↓ 每次 50
┌──────────────┐
│ 发酵线 1：50 │
│ 发酵线 2：50 │
│ 发酵线 3：50 │
│ 发酵线 4：50 │
│ 发酵线 5：50 │
└──────────────┘
  ↓
完成后等待殖民者卸载
  ↓
Compost
```

### 缓冲槽

```text
capacity = 250
```

使用污水接收 Comp 的 `sewageBuffer` 保存。

### 五条发酵线

每条：

```text
batchSize = 50
```

状态：

```text
Empty
Fermenting(progress 0..1)
Completed(progress = 1)
```

五条完全独立。

---

## 4.3 自动投料

只要：

```text
buffer >= 50
AND
存在 Empty 发酵线
```

就自动：

```text
buffer -= 50
该线路 occupied = true
progress = 0
```

不需要 Pawn 搬运。

关闭或断电只影响**从管道吸污**。

已经位于内部缓冲槽中的污水仍可以继续进入空闲发酵线；已经开始的发酵也继续运行。

---

## 4.4 发酵规则

每条线路独立执行 DBH 原版发酵速度逻辑：

```text
每条 50
完整原版发酵时间
完成后变成 50 Compost-equivalent
```

五条线路可以处在完全不同的进度，例如：

```text
Line 1: 100%
Line 2: 82%
Line 3: 47%
Line 4: 10%
Line 5: Empty
```

完成的线路**继续占用该线路**，直到被卸载。

不会因为完成而自动腾出线路。

---

## 4.5 卸载成品

只要至少一条线路为 `Completed`，新的卸载 WorkGiver 即可生成工作。

一次卸载工作：

```text
把当前所有 Completed 线路一次性全部卸载
```

例如：

```text
Line 1 = Completed
Line 2 = Completed
Line 3 = Fermenting
Line 4 = Completed
Line 5 = Empty
```

殖民者一次操作后生成：

```text
150 Compost
```

并把 1、2、4 三条线路重置为空。

未完成线路不受影响。

卸载后按照 RimWorld 常规储存逻辑把成品搬运到合适储存区。

---

## 4.6 不允许人工加入粪便

管道堆肥桶是“管道专用版本”。

因此：

```text
WorkGiver_FillComposter
```

不能把本 Mod 的新建筑当作目标。

最稳妥实现是新建筑使用独立 `ThingDef` 与独立 `Building` 类，而不是让 DBH 原 WorkGiver 识别它。

---

## 4.7 为什么不直接继承 `Building_Composter` 的内部状态

DBH 原 `Building_Composter` 的关键状态：

```text
wortCount
progressInt
```

为单槽模型，而且多个关键成员是 private / 非 virtual。

本 Mod 则是：

```text
250 buffer
+ 5 个独立 fermentation line
```

直接强行继承并复用原字段容易产生双重状态与 WorkGiver 冲突。

推荐：

```csharp
Building_PipedComposter : Building
```

自行保存五线路状态，但复用：

- DBH `CompComposter` 中的 Material/Product Def；
- DBH 发酵时间公式；
- DBH 温度逻辑；
- 原版贴图；
- 原版工作动画/交互风格。

---

## 4.8 UI

InspectString 建议：

```text
Sewage buffer: 173 / 250
Fermentation: 82%, 41%, 16%, 3%, —
```

如果有完成线路：

```text
Fermentation: Ready, 82%, 41%, —, —
```

不单独制作复杂窗口。

---

## 4.9 “转移内容物”

只允许转移：

```text
污水缓冲槽 sewageBuffer
```

不能转移：

- 正在发酵的 50；
- 已经完成、等待卸载的 Compost。

转移模式使用 DBH 水罐的同类交互方式：

```text
Command_Toggle
```

开启后：

1. 本设备暂时 `Blocked = true`，防止自己重新吸回刚排出的污水；
2. 周期性尝试 `pipeNet.PushSewage(amount)`；
3. 成功才减少自身 `sewageBuffer`；
4. 如果没有下游接收器，不扣除污水；
5. 按钮不因为“当前送不出去”而禁用；
6. 转移模式保持开启，直到玩家主动关闭，表现类似 DBH 水罐阀门。

---

# 5. 管道燃料精炼机

## 5.1 定位

新增一个“管道版燃料精炼机”，复用原燃料精炼机贴图。

只对 DBH 的：

```text
FecalSludge → Chemfuel
```

相关配方启用污水直供。

**不修改普通木材 / 食物 → Chemfuel 配方。**

---

## 5.2 内部污水槽

容量不是硬编码为任意数字，而定义为：

```text
单次目标配方所需 FecalSludge × 3
```

当前设计基准：

```text
单批 = 75
容量 = 225
```

实现时优先从目标 `RecipeDef` 的 FecalSludge ingredient count 解析真实单批需求，避免未来 XML 调整后容量与配方脱节。

---

## 5.3 污水接收

只要：

```text
设备手动开启
AND
（需要电力模式关闭 OR 当前供电正常）
AND
内部污水槽未满
AND
当前不处于转移模式
```

则：

```text
Blocked = false
```

否则：

```text
Blocked = true
```

即使没有任何 Bill，设备也继续优先吸污水作为缓存。

---

## 5.4 Bill 行为

UI 中仍然保留原粪便配方概念：

```text
75 FecalSludge → Chemfuel
```

但在管道精炼机上：

```text
Bill 系统不再去地图寻找 FecalSludge
```

而是检查：

```text
internalSewage >= recipeRequiredSludge
```

殖民者可以直接走到机器前开始工作。

---

## 5.5 不影响普通燃料配方

Harmony 逻辑必须同时满足：

```text
工作台拥有本 Mod 的管道污水组件
AND
当前 RecipeDef 是目标 DBH 粪便燃料配方
```

否则立刻放行原版逻辑。

因此：

```text
木材 → Chemfuel
食物 → Chemfuel
其他 Mod 配方
```

全部继续使用正常实体原料搜索与消耗。

---

## 5.6 原料扣除时机

污水**不在工作开始时扣除**。

规则：

```text
工作中断
断电
Pawn 离开
任务失败
```

都不造成污水损失。

只有在配方真正成功完成、准备产生成品时：

```text
再次验证内部污水 >= required
然后 sewageBuffer -= required
最后允许产品生成
```

实现中建议使用“逻辑预留”而不是提前扣除，以防工作过程中玩家开启转移导致最后一刻原料不足：

```text
reservedSewage = required
```

这不是消耗，只用于保证活动中的 Bill 对这批污水拥有优先权。

任务取消/失败后释放 reservation。

---

## 5.7 Harmony 范围

确定使用 Harmony，但 Patch 范围必须尽量小。

建议拆成三层：

### A. 原料可用性检查

当：

```text
工作台 = 管道燃料精炼机
配方 = DBH 粪便燃料配方
```

时，把机器内部污水视为已就位的目标原料。

### B. 实体原料搜索 / 搬运

只跳过该配方中的：

```text
FecalSludge
```

不跳过配方中可能存在的其他实体原料。

### C. 完成时消费

在产品真正生成前：

```text
revalidate
consume internal sewage
```

若最终检查失败：

```text
不生成产品
不允许负数污水
```

具体 Harmony 目标方法应在引用 RimWorld 1.6 `Assembly-CSharp.dll` 后再次确认签名；不要只凭方法名硬写补丁。

---

## 5.8 “转移内容物”

转移目标为：

```text
内部所有可转移污水
```

同样采用阀门式持续尝试，而不是 Pawn 工作。

如果存在活动中的逻辑预留量：

```text
可转移量 = sewageBuffer - reservedSewage
```

避免完成中的 Bill 因玩家转移而凭空失去已确认的原料。

网络无可接收端时：

```text
污水保持原位
按钮仍可保持开启
```

---

## 5.9 UI

InspectString：

```text
Sewage: 150 / 225
```

如果容量由配方动态解析，则显示实际值。

如有活动中的逻辑预留，可只在开发/调试模式显示：

```text
Reserved sewage: 75
```

正常玩家 UI 不必增加复杂信息。

---

# 6. 地下存粪坑

## 6.1 定位

地下存粪坑是一个：

```text
大容量
被动
无处理能力
无需电力
```

的污水缓冲建筑。

“地下”仅为视觉和设定表现，类似化粪池。

它仍然有正常的地面建筑入口/检修口和正常占地。

---

## 6.2 容量

固定：

```text
capacity = 7500
```

因为 DBH `FecalSludge.stackLimit = 75` 的当前设计基准下：

```text
7500 / 75 = 100 stacks
```

满容量拆除时会产生非常直观的“100 组粪便”结果。

这是刻意保留的玩法后果，不做隐藏删除或数量上限。

---

## 6.3 无需电力

地下存粪坑：

- 不需要电；
- 不会因为停电停止接收；
- 不主动处理/减少污水；
- 是生产设施停止工作时的安全缓冲层。

不应继承 `CompSepticTank` 的自动处理能力。

推荐：

```csharp
CompUndergroundSewageStorage : CompSewageHandler
```

---

## 6.4 Blocked

普通状态：

```text
Blocked = sewageBuffer >= 7500
```

转移模式：

```text
Blocked = true
```

使其不会把自己正在重新注入管网的污水再次吸回。

---

# 7. 地下存粪坑：人工抽取

## 7.1 每次固定抽取

每个抽粪 Job：

```text
75 sewage
        ↓
75 FecalSludge
```

工作时间：

```text
120 ticks
```

与 DBH 原版化粪池清空工作的交互节奏保持接近。

---

## 7.2 不足 75 时

正常抽取要求：

```text
sewageBuffer >= 75
```

如果只剩：

```text
40
```

则不能执行标准 75 单位抽取 Job。

这些余量仍然可以：

- 等待更多污水进入；
- 正常拆除时实体化；
- 暴力摧毁时泄漏。

---

## 7.3 “立即抽取”

Gizmo：

```text
立即抽取
```

不是瞬间生成物品。

点击后生成一个 forced job：

```text
最近的可用殖民者
→ 前往存粪坑
→ 工作 120 ticks
→ 生成 75 FecalSludge
→ sewageBuffer -= 75
```

**强制抽取无视自动抽取阈值。**

但仍必须满足：

```text
sewageBuffer >= 75
```

---

# 8. 地下存粪坑：自动抽取

## 8.1 控件

提供：

```text
自动抽取：On / Off
目标储量：0%–100%
```

目标值表示：

> 自动抽取最终希望保留的最低污水水位。

---

## 8.2 防抖 / 滞回机制

假设：

```text
capacity = 7500
target = 60% = 4500
```

不是一超过 4500 就立即派 Pawn。

启动阈值固定比目标高：

```text
250
```

即：

```text
startThreshold = target + 250
               = 4750
```

状态：

```text
0 .. 4499        → 不抽
4500 .. 4749     → 滞回区，不重新启动
>= 4750          → 启动连续自动抽取
```

---

## 8.3 抽到目标值为止

自动抽取启动后持续产生 75 一组的工作。

但下一组若会跌破目标值，则停止。

例如：

```text
target = 4500
current = 4540
```

如果再抽：

```text
4540 - 75 = 4465
```

低于目标，因此不再安排这一组。

也就是说自动抽取保证：

```text
不会主动把储量抽到玩家目标线以下
```

---

## 8.4 防止多个 Pawn 重复安排

同一个地下存粪坑：

```text
同时最多允许一个抽粪 Job
```

使用 RimWorld 对建筑目标的 reservation 体系即可实现。

不需要维护多个 Pawn 的复杂“预扣 75”队列。

每个 Job 完成后重新检查状态，再决定是否产生下一项工作。

---

# 9. “转移内容物”统一规范

以下三个建筑都提供类似 DBH 水罐的阀门式：

```text
转移内容物
```

但可转移对象不同。

| 建筑 | 可转移内容 |
|---|---|
| 管道堆肥桶 | 仅缓冲槽 sewageBuffer |
| 管道燃料精炼机 | 未被活动 Bill 逻辑预留的内部 sewageBuffer |
| 地下存粪坑 | sewageBuffer |

---

## 9.1 行为

建议每 10 ticks 尝试一次小批量转移，以接近 DBH `CompWaterStorage` 的操作感。

伪代码：

```csharp
if (transferMode && parent.IsHashIntervalTick(10))
{
    Blocked = true;

    float amount = Mathf.Min(sewageBuffer - reservedAmount, transferRate);

    if (amount > 0f && PipeComp?.pipeNet != null)
    {
        if (PipeComp.pipeNet.PushSewage(amount))
        {
            sewageBuffer -= amount;
        }
    }
}
```

关键点：

```text
PushSewage() 返回 false
→ 不扣自身污水
→ 下次继续尝试
```

因此按钮永远不需要因为“当前管网放不出去”而提前禁用。

---

# 10. 电力与中世纪兼容

## 10.1 管道生产设备泵功耗

已确定基准：

```text
50 W
```

建议理解为**污水管道泵本身的额外功耗**。

管道燃料精炼机原本作为工作台所需的其他电力仍遵循其自身 Def。

---

## 10.2 Mod 设置

提供：

```text
Require power for sewage intake pumps
```

默认：

```text
On
```

开启：

```text
断电 → 管道堆肥桶/管道精炼机不再接收污水
```

关闭：

```text
管道泵不消耗这 50 W
污水接收忽略电力条件
```

用于中世纪/低科技玩法。

无论该设置如何：

```text
手动关闭建筑
→ 停止管道吸污
```

管道堆肥桶中已经开始的发酵仍继续。

---

# 11. 污水优先级

固定层级：

```text
priority 100
┌────────────────────┐
│ 管道燃料精炼机     │
│ 管道堆肥桶         │
└────────────────────┘
          ↓ 全部 Blocked
priority 50
┌────────────────────┐
│ 地下存粪坑         │
└────────────────────┘
          ↓ 满
DBH 原生 priority
┌────────────────────┐
│ 化粪/处理/排污设施 │
└────────────────────┘
```

---

## 11.1 生产设备之间不竞争自定义顺序

管道堆肥桶与管道燃料精炼机：

```text
完全相同 priority
```

不规定：

```text
精炼 > 堆肥
```

也不规定：

```text
堆肥 > 精炼
```

让 DBH `PushSewage()` 自己对同优先级接收器平均分摊。

---

## 11.2 多台同类设备

例如：

```text
Refinery A
Refinery B
Composter C
```

全部：

```text
priority = 100
Blocked = false
```

本次厕所推送的污水由 DBH 原逻辑平均分配。

不额外做：

- 最空优先；
- 最近优先；
- 指定建筑优先；
- 容量安全补差；
- Round-robin。

---

# 12. 正常拆除

正常拆除必须尽量资源守恒。

---

## 12.1 管道燃料精炼机

拆除时：

```text
全部内部 sewageBuffer
→ FecalSludge
```

数量：

```csharp
Mathf.CeilToInt(sewageBuffer)
```

按 `FecalSludge.stackLimit` 分组，并放置在建筑附近。

活动中的 Bill 因为没有提前扣污水，不需要额外返还原料。

---

## 12.2 地下存粪坑

拆除时：

```text
全部 sewageBuffer
→ FecalSludge
```

不设数量上限。

满容量：

```text
7500
→ 100 × 75 FecalSludge
```

忠实生成。

不得因为物品过多而：

- 删除；
- 截断；
- 转成抽象资源；
- 只生成部分。

---

## 12.3 管道堆肥桶

拆除时分三类：

### 缓冲槽

```text
sewageBuffer → FecalSludge
```

### 未完成线路

每条：

```text
50 fermentation material
→ 50 FecalSludge
```

无论当前：

```text
1%
50%
99.9%
```

只要没有达到完成状态，都重新视为等量粪污原料。

### 已完成线路

每条：

```text
50 completed
→ 50 Compost
```

不会因为拆除而退回 `FecalSludge`。

因此不会出现：

> 已经完全发酵好的堆肥，拆一下建筑又重新变成粪便。

---

# 13. 暴力摧毁

暴力摧毁与正常拆除严格区分。

原则：

> 仍然属于污水/粪污状态的资源进入 `SewageGrid`；已经成为最终成品的资源作为物品掉落。

---

## 13.1 管道燃料精炼机

```text
全部 sewageBuffer
→ SewageGrid
```

不生成 `FecalSludge`。

---

## 13.2 地下存粪坑

```text
全部 7500（或当前值）
→ SewageGrid
```

参考 DBH `CompSepticTank.PostDestroy()`：

```text
总量 / 建筑占地面积
```

平均加入各占地格。

---

## 13.3 管道堆肥桶

泄漏：

```text
缓冲槽污水
+
所有尚未完成的发酵线路（每条 50）
→ SewageGrid
```

保留成品：

```text
所有 Completed 线路
→ 对应数量 Compost 掉落
```

---

## 13.4 DestroyMode 注意事项

实现时建议明确区分至少：

```text
DestroyMode.Deconstruct → 正常资源返还
DestroyMode.KillFinalize → 暴力泄漏
```

对于地图卸载、特殊销毁等 `Vanish` 路径，不应无条件在无效地图上生成大量资源。

最终处理函数必须具备：

```text
idempotent
```

防止同一资源在 Building 和 ThingComp 的多个销毁回调中重复返还。

---

# 14. 不允许 Minify

三个建筑全部：

```xml
<minifiable>false</minifiable>
```

不允许：

- 卸载重装；
- MinifiedThing 携带内部污水；
- 把满载 7500 的地下存粪坑装箱搬家；
- 利用搬运绕过管网。

这样可以避免保存 `ThingComp` 内大量液体状态在 MinifiedThing 中产生额外边界问题。

---

# 15. UI 规格

## 15.1 管道堆肥桶

Inspect：

```text
Sewage buffer: 173 / 250
Fermentation: 82%, 41%, 16%, 3%, —
```

Gizmo：

```text
Transfer contents [Toggle]
```

可选 Debug：

```text
Fill sewage
Complete all lines
```

仅 DevMode/GodMode。

---

## 15.2 管道燃料精炼机

Inspect：

```text
Sewage: 150 / 225
```

Gizmo：

```text
Transfer contents [Toggle]
```

Bill UI 继续显示 DBH 粪便燃料配方。

---

## 15.3 地下存粪坑

Inspect：

```text
Sewage: 5,325 / 7,500 (71%)
Auto extraction target: 60%
```

Gizmo：

```text
Transfer contents [Toggle]
Extract 75 now [Action]
Auto extraction [Toggle]
Target storage level [Slider]
```

目标水位 Slider 建议使用 DBH `Command_SetTargetDrainLevel` 相同的 UI 风格，但不直接依赖其 `CompSepticTank` 类型字段；实现一个本 Mod 自己的 Command 类更安全。

---

# 16. 建议代码结构

```text
Source/
├─ Core/
│  ├─ PipedWasteMod.cs
│  ├─ PipedWasteSettings.cs
│  ├─ PipedWasteDefOf.cs
│  └─ PipedWasteConstants.cs
│
├─ Sewage/
│  ├─ CompProperties_PipedSewageReceiver.cs
│  ├─ CompPipedSewageReceiver.cs
│  └─ SewageMaterializationUtility.cs
│
├─ Composter/
│  ├─ Building_PipedComposter.cs
│  ├─ CompPipedComposterReceiver.cs
│  ├─ FermentationLine.cs
│  ├─ WorkGiver_UnloadPipedComposter.cs
│  └─ JobDriver_UnloadPipedComposter.cs
│
├─ Refinery/
│  ├─ CompPipedRefinerySewage.cs
│  ├─ PipedRefineryRecipeUtility.cs
│  └─ Harmony/
│     ├─ Patch_DoBillIngredientSearch.cs
│     ├─ Patch_DoBillIngredientConsumption.cs
│     └─ Patch_DoBillCompletion.cs
│
├─ Storage/
│  ├─ CompProperties_UndergroundSewageStorage.cs
│  ├─ CompUndergroundSewageStorage.cs
│  ├─ Command_SetSewageTarget.cs
│  ├─ WorkGiver_ExtractSewage.cs
│  └─ JobDriver_ExtractSewage.cs
│
├─ UI/
│  └─ TransferSewageGizmoUtility.cs
│
└─ Compatibility/
   └─ MultiplayerCompatibility.cs   // 如后续决定支持 MP
```

---

# 17. 核心 Comp 设计

## 17.1 通用污水接收器

推荐：

```csharp
public class CompPipedSewageReceiver : CompSewageHandler
```

扩展状态：

```csharp
bool transferMode;
float reservedSewage;
```

核心职责：

1. 维护 `Blocked`；
2. 污水转移；
3. InspectString；
4. 正常拆除实体化；
5. 暴力摧毁泄漏；
6. 防止自己重新接收正在转移的污水。

---

## 17.2 Blocked 更新

生产设备：

```csharp
Blocked =
    transferMode
    || !IntakeEnabled
    || sewageBuffer >= Props.capacity;
```

其中：

```text
IntakeEnabled =
手动开关开启
AND
(设置关闭电力需求 OR 当前有电)
AND
无故障/满足 DBH WorkingNow 所需条件
```

存粪坑：

```csharp
Blocked =
    transferMode
    || sewageBuffer >= Props.capacity;
```

---

# 18. 管道堆肥桶存档结构

不要尝试把五条线路挤进 DBH 原 `wortCount/progressInt`。

推荐：

```csharp
public class FermentationLine : IExposable
{
    public bool occupied;
    public float progress;
}
```

建筑：

```csharp
List<FermentationLine> lines;
```

固定保证：

```text
Count = 5
```

`ExposeData()`：

```text
Scribe_Collections.Look(... LookMode.Deep)
```

载入旧存档时做修复：

```text
null → 建立 5 条空线路
少于 5 → 补空线路
多于 5 → 只在明确迁移规则下处理，不静默丢资源
progress clamp 0..1
```

缓冲污水继续由：

```text
CompSewageHandler.sewageBuffer
```

保存。

---

# 19. 管道堆肥桶 Tick 状态机

推荐在 `TickRare()` 中运行，贴近 DBH 原桶。

伪代码：

```text
1. 尝试从 buffer 向空线路装入完整 50
2. 对每条 Fermenting line：
      progress += originalDBHRate × temperatureFactor
      clamp 0..1
3. progress >= 1：
      标记 Completed
4. Completed 不自动清空
5. 下一次有 Pawn 卸载时统一取出所有 Completed
```

装入条件只允许：

```text
buffer >= 50
```

不会出现：

```text
12.3 单位的半批发酵
```

---

# 20. 温度逻辑

目标是沿用 DBH 原堆肥桶的温度表现：

- 低于理想温度时减速；
- 理想温度以上按原规则运行；
- 使用原版 `CompTemperatureRuinable` / 温度参数；
- InspectString 保留温度和理想温度提示风格。

原版 `Building_Composter` 的温度毁坏逻辑是单槽模型。

五线路实现时应避免“新装入一条线路 Reset 温度组件导致其他四条线路被意外保护”的副作用。

因此建议：

- 温度速度因子复用；
- 温度毁坏信号由 `Building_PipedComposter` 自己统一处理；
- 不让每次装入一条新线都重置全建筑已有线路的风险累计。

这是实现层面的兼容重点，需要专门测试。

---

# 21. 配方数量解析

燃料精炼机启动时或 Def 初始化后解析目标 DBH RecipeDef：

```text
找到 ingredient filter 包含 FecalSludge 的目标粪便燃料配方
读取 count
```

得到：

```csharp
requiredSewagePerBatch
```

容量：

```csharp
capacity = requiredSewagePerBatch * 3f;
```

当前设计预期：

```text
required = 75
capacity = 225
```

如果 DBH 将来调整 XML 数量，本 Mod 不应因为硬编码 225 而与配方脱节。

---

# 22. FecalSludge / Compost 实体生成

生成大量物品时必须手工按 `stackLimit` 分组。

伪代码：

```csharp
while (remaining > 0)
{
    int stack = Mathf.Min(def.stackLimit, remaining);

    Thing thing = ThingMaker.MakeThing(def);
    thing.stackCount = stack;

    GenPlace.TryPlaceThing(
        thing,
        parent.Position,
        map,
        ThingPlaceMode.Near
    );

    remaining -= stack;
}
```

不能创建一个：

```text
stackCount = 7500
```

的单一 Thing 并期待所有逻辑自动正确拆分。

---

# 23. 正常拆除与摧毁统一资源处理器

建议统一工具：

```csharp
SewageDisposalUtility.MaterializeSludge(...)
SewageDisposalUtility.SpillToGrid(...)
SewageDisposalUtility.PlaceStacks(...)
```

并在建筑/组件中使用：

```csharp
bool finalizedResources;
```

确保只处理一次。

---

# 24. 地下存粪坑 WorkGiver 状态机

核心字段：

```csharp
bool autoExtract;
float targetPercent;
bool autoExtractionActive;
```

绝对值：

```csharp
targetAmount = capacity * targetPercent;
startAmount = targetAmount + 250f;
```

启动：

```csharp
if (!autoExtractionActive
    && autoExtract
    && sewageBuffer >= startAmount)
{
    autoExtractionActive = true;
}
```

继续派工：

```csharp
autoExtractionActive
&& sewageBuffer >= 75
&& sewageBuffer - 75 >= targetAmount
&& building currently not reserved
```

停止：

```csharp
if (sewageBuffer - 75 < targetAmount)
{
    autoExtractionActive = false;
}
```

Forced Job：

```text
无视 autoExtract
无视 targetPercent
无视 250 启动差
只要求 sewageBuffer >= 75
```

---

# 25. 与 DBH 原 WorkGiver 的关系

## 25.1 堆肥桶

DBH 原：

```csharp
WorkGiver_FillComposter
WorkGiver_UnloadComposter
```

都围绕：

```text
DubDef.BiosolidsComposter
Building_Composter
```

实现。

新管道堆肥桶不应让原 `FillComposter` 接管。

新建：

```text
WorkGiver_UnloadPipedComposter
JobDriver_UnloadPipedComposter
```

并一次卸载所有已完成线路。

---

## 25.2 存粪坑

DBH 原：

```csharp
WorkGiver_emptySepticTank
JobDriver_emptySepticTank
```

会：

```text
一次清空整个 CompSepticTank
```

而本 Mod 要求：

```text
每次只抽 75
```

所以不应直接继承并依赖原 JobDriver。

新建专用：

```text
WorkGiver_ExtractSewage
JobDriver_ExtractSewage
```

但工作时长、走位、reservation 风格可以参考 DBH 原实现。

---

# 26. Def 设计原则

三个建筑都使用新 DefName，例如：

```text
PWA_PipedComposter
PWA_PipedBiofuelRefinery
PWA_UndergroundSewagePit
```

不要覆盖：

```text
DBH 原堆肥桶
Vanilla/DBH 原燃料精炼机
DBH 原化粪池
```

---

## 26.1 管道组件

均加入：

```xml
<li Class="DubsBadHygiene.CompProperties_Pipe">
  <mode>Sewage</mode>
</li>
```

污水接收组件使用本 Mod 自定义 `CompProperties`，运行类继承 DBH `CompSewageHandler`。

---

## 26.2 贴图

复用相应原建筑的：

```text
texPath
graphicClass
drawSize
```

但不复制贴图文件进本 Mod。

这样：

- 包体小；
- 视觉一致；
- DBH 为硬依赖，因此资源一定存在。

若未来 DBH 修改路径，需要随版本更新本 Mod。

---

# 27. 研究与建造原则

本 Mod 不建立一套与 DBH 平行的大型研究树。

建议：

- 管道堆肥桶：要求 DBH 原堆肥相关研究；
- 管道燃料精炼机：要求原燃料精炼研究 + DBH 粪便燃料配方实际所需研究；
- 地下存粪坑：要求 DBH 基础管道/污水处理阶段的对应研究。

新建筑建造成本以对应原建筑为基准，再为管道自动化增加少量材料成本。

**具体 XML 的 ResearchProjectDef 与 costList 在实现阶段应从 DBH 当前 Def 文件再次核对，不要仅根据名字猜测。**

---

# 28. Mod 设置

最少只提供真正有玩法价值的设置：

```text
[✓] Sewage intake pumps require power
```

默认：

```text
true
```

说明：

```text
When enabled, piped production buildings require 50 W for their sewage intake pump.
Disable this option for low-tech or medieval colonies.
```

不开放：

- priority 数值；
- 堆肥批次大小；
- 五条线路数量；
- 存粪坑容量；
- 单次抽取 75；
- 自动抽取滞回 250。

这些属于本设计的固定平衡规则。

---

# 29. DBH 缺失行为

`About.xml`：

```xml
<modDependencies>
  <li>
    <packageId>Dubwise.DubsBadHygiene</packageId>
    <displayName>Dubs Bad Hygiene</displayName>
  </li>
</modDependencies>

<loadAfter>
  <li>Dubwise.DubsBadHygiene</li>
</loadAfter>
```

程序集直接引用：

```text
BadHygiene.dll
```

建议：

```xml
<Private>false</Private>
```

不把 DBH DLL 复制进本 Mod。

DBH 缺失：

```text
直接报缺少依赖
```

不做：

- 反射式无 DBH 运行；
- 空壳建筑；
- 自动禁用所有功能后继续加载。

---

# 30. Harmony 原则

只对“管道燃料精炼机使用 DBH 粪便燃料配方”这一无法用普通 Comp 完成的环节使用 Harmony。

不 Harmony：

```text
PlumbingNet.PushSewage()
PlumbingNet.InitNet()
DBH 原生污水优先级
DBH 原生化粪池
DBH 原排污口
DBH 原堆肥桶
```

Harmony Patch 必须采用：

```text
目标建筑拥有本 Mod Comp
+
目标 RecipeDef 精确匹配
```

双条件守卫。

任何其他工作台/配方立即走原逻辑。

---

# 31. 推荐开发顺序

## Phase 1：最小管网验证

实现一个测试 `CompSewageHandler`：

```text
capacity = 100
priority = 100
```

确认：

- 自动进入 `PlumbingNet.Sewers`；
- 厕所优先向其推污；
- 满后 `Blocked = true`；
- 下一级接收器开始工作。

---

## Phase 2：地下存粪坑

优先完成最独立的建筑：

- 7500 容量；
- priority 50；
- InspectString；
- 正常拆除；
- 暴力泄漏；
- 转移内容物。

用它作为全项目的管网测试基座。

---

## Phase 3：抽粪系统

实现：

- 75/次；
- 120 ticks；
- forced job；
- auto toggle；
- target slider；
- +250 滞回；
- 单建筑单 Job reservation。

---

## Phase 4：管道堆肥桶

实现：

- 250 buffer；
- 5×50 lines；
- TickRare；
- 原版温度速度；
- 1:1 Compost；
- 一次卸载全部完成线路；
- 拆除/摧毁资源状态转换。

---

## Phase 5：管道燃料精炼机

先实现：

- 3 批缓存；
- priority 100；
- 转移；
- InspectString。

最后才加入 Harmony Bill 支持。

---

## Phase 6：电力设置与 UI

加入：

- 50W 管道泵；
- 设置切换；
- Flick / power / Blocked 联动；
- Gizmo 图标与本地化。

---

## Phase 7：兼容测试

测试：

- DBH 正常完整模式；
- 断电；
- 手动关闭；
- 无排污口；
- 只有一个接收器；
- 多个同 priority 接收器；
- 存粪坑满；
- 转移时无下游；
- 转移时下游后来恢复；
- 保存/读档；
- 正常拆除；
- 暴力摧毁；
- 7500 满粪坑拆除；
- 活动 Bill 中断；
- 活动 Bill 期间切换转移；
- 温度异常；
- 五条线路不同进度；
- 多条线路同时完成。

---

# 32. 必测边界案例

## 32.1 DBH 同级平均分配导致超容量

场景：

```text
A 剩余容量 2
B 剩余容量 100
同 priority
一次 PushSewage(10)
```

DBH 可能：

```text
A +5
B +5
```

A 超容量。

**预期：不修。**

只保证下一次 Tick 后 A 正确设置 `Blocked`。

---

## 32.2 转移时只有自己

场景：

```text
存粪坑 3000
无其他 SewageHandler
开启 Transfer
```

预期：

```text
PushSewage → false
存量仍为 3000
Transfer 保持开启
```

不得删除污水。

---

## 32.3 管道堆肥桶 5 条全部 Completed

预期：

```text
缓冲可以继续吸到 250
但无法再把 50 装入任何线路
```

当缓冲也满：

```text
Blocked = true
```

污水自动流向存粪坑。

殖民者一次卸载 5 条：

```text
250 Compost
```

之后五条恢复空闲，缓冲可以继续向线路投料。

---

## 32.4 停电中的堆肥桶

假设：

```text
buffer = 150
line1 = 50 @ 30%
line2 = 50 @ 10%
line3-5 = empty
```

停电后：

```text
不再从管道接收
```

但：

```text
buffer 中已有的 100 可继续填 line3/4
所有已占用线路继续发酵
```

符合“断电只停止吸污”的定义。

---

## 32.5 正常拆除堆肥桶

状态：

```text
buffer = 37.2
line1 = Completed
line2 = 80%
line3 = 10%
line4 = Empty
line5 = Completed
```

预期：

```text
Ceil(37.2) = 38 FecalSludge
100 FecalSludge（两条未完成线）
100 Compost（两条完成线）
```

不把 Completed 退回粪便。

---

## 32.6 暴力摧毁同一状态

预期：

```text
37.2 + 50 + 50 = 137.2 sewage
→ SewageGrid

100 Compost
→ 物品掉落
```

---

## 32.7 自动抽取目标

```text
capacity = 7500
target = 60% = 4500
start = 4750
```

污水：

```text
4749 → 不开始
4750 → 开始
```

连续抽：

```text
4750 → 4675 → 4600 → 4525
```

下一组：

```text
4525 - 75 = 4450 < 4500
```

因此停止在：

```text
4525
```

---

## 32.8 强制抽取

当前：

```text
sewage = 200
target = 90%
autoExtract = false
```

玩家点击“立即抽取”。

预期：

```text
照常生成 forced job
200 → 125
生成 75 FecalSludge
```

自动阈值完全不参与。

---

# 33. 性能原则

本 Mod 不需要逐 tick 做昂贵全网搜索。

避免：

```text
每个建筑每 tick：
PipeNets.SelectMany(...)
全图查找接收器
全图查找 Pawn
```

优先：

- 使用 `CompPipe.pipeNet`；
- 让 DBH `PushSewage()` 自己分配；
- `TickRare` 处理发酵；
- `IsHashIntervalTick(10)` 处理转移；
- WorkGiver 按 RimWorld 正常扫描周期处理抽粪。

---

# 34. 兼容性原则

## 34.1 对 DBH

依赖当前公开类：

```text
CompSewageHandler
CompProperties_SewageHandler
CompPipe
PlumbingNet
MapComponent_Hygiene
CompComposter
DubDef.FecalSludge
```

DBH 升级后重点复查：

```text
PushSewage
InitNet
CompSewageHandler
Building_Composter
目标 FecalSludge → Chemfuel RecipeDef
```

---

## 34.2 对其他 Mod

本 Mod不应：

- 遍历并重写其他 Mod 的 `CompSewageHandler.priority`；
- 修改所有 DBH 接收器的 `Blocked`；
- Patch 全局 `PushSewage`；
- 替换 vanilla `JobDriver_DoBill` 的默认逻辑；
- 假设自己是管网中唯一的附属 Mod。

生产 priority 100 / 存储 priority 50 是本 Mod 自己的政策；其他 Mod 如果使用更高 priority，应允许其正常生效。

---

# 35. 本地化

至少提供：

```text
Languages/English/
Languages/ChineseSimplified/
```

建议 Key：

```text
PWA_PipedComposter_Label
PWA_PipedComposter_Desc
PWA_PipedRefinery_Label
PWA_PipedRefinery_Desc
PWA_UndergroundPit_Label
PWA_UndergroundPit_Desc

PWA_SewageBuffer
PWA_Fermentation
PWA_TransferContents
PWA_ExtractNow
PWA_AutoExtraction
PWA_TargetStorage
PWA_PumpPowerSetting
PWA_NoSewageToExtract
```

代码中不硬编码玩家可见文本。

---

# 36. Debug / 开发辅助

DevMode 下建议加入：

```text
Fill sewage
Empty sewage
Set all fermentation lines to 99%
Complete all fermentation lines
Spill all sewage
```

便于快速验证：

- 优先级；
- 卸载；
- 拆除；
- 爆满；
- 泄漏；
- 自动抽取。

正式模式隐藏。

---

# 37. 最终玩法示例

殖民地拥有：

```text
2 × 管道燃料精炼机
2 × 管道堆肥桶
1 × 地下存粪坑
1 × DBH 原生排污口
```

厕所产生污水。

第一层：

```text
四台生产设备 priority = 100
```

只要它们开启、有接收能力，就由 DBH 同级平均分流。

精炼机：

```text
最多缓存 3 批
```

堆肥桶：

```text
先缓存 250
自动装入 5×50 发酵线路
```

生产端全满/关闭后：

```text
地下存粪坑 priority = 50
```

开始积累。

如果存粪坑自动抽取设置为：

```text
target = 60%
```

达到：

```text
4750
```

后殖民者逐次抽取 75，直到不会再安全抽出一组而保持 ≥4500。

如果生产恢复，后续新污水再次优先进入生产端。

如果存粪坑也达到 7500：

```text
Blocked = true
```

DBH 自动继续尝试更低 priority 的原生设施和排污口。

整个过程中本 Mod 没有接管 DBH 的管网算法。

---

# 38. 最终冻结参数

| 参数 | 最终值 |
|---|---:|
| 管道堆肥桶污水缓冲 | 250 |
| 发酵线路数量 | 5 |
| 每线路批量 | 50 |
| 每线路产出 | 50 Compost |
| 发酵时间 | DBH 原版完整周期，理想温度约 5 游戏日 |
| 生产设备污水 priority | 100 |
| 地下存粪坑 priority | 50 |
| 管道精炼机容量 | 目标配方单批污水 × 3 |
| 当前设计单批基准 | 75 |
| 当前设计精炼机容量基准 | 225 |
| 地下存粪坑容量 | 7500 |
| 抽粪单次量 | 75 |
| 抽粪工作时间 | 120 ticks |
| 自动抽取启动滞回 | 目标值 +250 |
| 管道泵功耗 | 50 W |
| 管道泵电力要求 | 可在 Mod 设置关闭 |
| 正常拆除污水处理 | 实体化 FecalSludge |
| 暴力摧毁污水处理 | 泄漏进 SewageGrid |
| Minify | 禁止 |
| 玩家修改 priority | 禁止 |
| DBH | 必需硬依赖 |
| 管网 Harmony | 不使用 |
| Bill 兼容 Harmony | 使用，且仅限定管道精炼机 + DBH 粪便配方 |

---

# 39. 实现前最后核对清单

进入编码前只需进行“源码/Def 名称核对”，不再属于玩法设计问题：

- [ ] 确认 DBH 当前 `BiosolidsComposter` 的贴图路径。
- [ ] 确认 DBH 当前 Compost ThingDef。
- [ ] 确认 DBH 当前 `FecalSludge` stackLimit。
- [ ] 确认 DBH 粪便 → Chemfuel 的精确 RecipeDefName。
- [ ] 确认该 RecipeDef 当前 FecalSludge ingredient count 是否为 75。
- [ ] 确认原燃料精炼机 ThingDef 与贴图路径。
- [ ] 确认 DBH 原相关研究 DefName。
- [ ] 引用 RimWorld 1.6 Assembly-CSharp 后确认 `JobDriver_DoBill` / ingredient search 的实际 Harmony 目标签名。
- [ ] 验证 `DestroyMode.Deconstruct` 与 `KillFinalize` 回调顺序，防止双重资源返还。
- [ ] 验证 DBH 更新管网 dirty 状态后自定义 Comp 会正常重新加入 `Sewers`。
- [ ] 保存/载入后验证五条发酵线路始终恢复为 5 条。
- [ ] 验证 7500 拆除能可靠按 stackLimit 生成全部物品。
- [ ] 验证 Transfer 开启且没有下游时资源绝不减少。
- [ ] 验证活动 Bill 的逻辑预留在 Job 取消后释放。

---

# 40. 明确不做的内容

v1.0 不做：

- 自定义污水寻路；
- 修复 DBH `PushSewage()` 超容量；
- 玩家自定义 priority；
- 自动跨管网转移；
- 存粪坑主动净化污水；
- 管道堆肥桶手工加入 `FecalSludge`；
- 管道精炼机替代普通木材/食物燃料配方；
- 可搬运的满载生产设备；
- 无 DBH 独立运行；
- 把未完成发酵进度按比例折算成部分 Compost；
- 暴力摧毁时把污水安全打包成 FecalSludge。

---

# 41. 一句话架构结论

本 Mod 应当被实现为：

> **三个新的 DBH `CompSewageHandler` 接收端 + 一个五线路堆肥状态机 + 一个 75/次的存粪坑抽取系统 + 一组极窄范围的燃料 Bill Harmony Patch。**

污水的网络优先级与路径选择继续完全交给 DBH：

```text
Production → Storage → Native treatment → Outlet
```

本 Mod只决定：

```text
“我现在能不能接污水”
以及
“接到之后我要怎么使用/保存它”
```

而不决定：

```text
“整个 DBH 管网应该怎样重新设计”
```

这应当是兼容性、开发量与玩法目标之间最稳妥的实现边界。
