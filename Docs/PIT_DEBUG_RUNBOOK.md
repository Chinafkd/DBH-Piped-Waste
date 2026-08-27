# DBHPW Pit 根因调试实验手册

本轮只用于区分 Pit 的三类状态问题。当前版本已删除 Fixture workaround；Debug Patch 只记录 `Nosewage` 状态，不修改网络或 `AcceptanceReport`，也不进行大型管网性能测试。

## 准备

1. 使用 `Demo/DBHPipedWaste` 作为 RimWorld Mod 目录中的测试 Mod，确保加载顺序为 Harmony → Dubs Bad Hygiene → DBH: Piped Sewage Processing。
2. 打开 RimWorld Developer mode。
3. 新建测试地图，先只放置一条 DBH 管道、一个地下存粪坑和一个会产生污水需求的 Fixture（建议先用 Latrine）。
4. 选中存粪坑，点击 `调试：导出存粪坑状态`。该按钮只写日志，不修改游戏状态。

## 日志位置

默认日志路径：

```text
C:\Users\<你的用户名>\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log
```

PowerShell 查看最近的 Pit 日志：

```powershell
$logPath = Join-Path $env:USERPROFILE 'AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log'
Select-String -Path $logPath -Pattern 'PIT DEBUG|MANUAL PIT DEBUG|Pit ' -Context 0,2
```

每个对象每个游戏 tick 最多自动输出一次，避免 Fixture 高频调用导致日志无限增长。

## 实验顺序

每次实验前先点击一次存粪坑调试按钮；Fixture 第一次返回 `Nosewage` 时，Debug Patch 会自动输出一份日志。该日志钩子不会修改任何状态。

1. **刚建成**：记录 `inPipedThings`、`inSewers`、`Blocked`、两个 NetID 和对象 hash。
2. **刚接管**：等待至少一个地图更新周期，再导出一次。
3. **第一次 Fixture.Working**：观察 `FIXTURE NOSEWAGE DEBUG` 区块。
4. **重建**：拆掉并重新连接一根管道，导出状态，观察 Pit 是否仍在同一组 `PipedThings/Sewers` 中。
5. **Split**：让网络拆成两段，分别在 Pit 和 Fixture 上导出状态，比较 `ReferenceEquals(selectedNet,pitNet)`。
6. **Merge**：重新连接两段网络，再比较 NetID、对象 hash 和 Sewers membership。
7. **变满/抽空**：让 Pit 变满后导出，再抽出一批污水后立即导出，重点观察 `Blocked` 是否恢复；若出现超过 7500 L，观察 `overflowAgeTicks` 是否增长以及超时后的地面污水变化。
8. **TransferMode**：打开、关闭存粪坑的转移模式，各导出一次，观察 `Blocked` 是否随状态恢复。
9. **保存/读档**：分别在 `Blocked=false` 和 `Blocked=true` 时保存，读档后立即导出，再等待一个 tick 后导出。

## 快速分类

- `inPipedThings=true`、`inSewers=true`、`Blocked=true`：优先怀疑 Blocked 生命周期。
- `inPipedThings=true`、`inSewers=false`：优先怀疑 DBH rebuild / Sewers 缓存同步。
- Fixture 与 Pit 的 Net hash 不同或 `ReferenceEquals=false`：优先怀疑网络实例或 split/merge 生命周期。
- `AllComps` 中没有 `isCompSewageHandler=true`：优先检查 XML、程序集版本和运行时类型身份。

请把以下完整区块贴回：

```text
=== FIXTURE NOSEWAGE DEBUG ===
--- Pits ---
--- Sewers (...) ---
```

不要只截取 `Nosewage` 一行，因为根因判断依赖 membership、网络身份和每个 Handler 的 `Blocked` 状态。
