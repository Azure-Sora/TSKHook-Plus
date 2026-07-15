# 技能文本批量采集模式

该模式在游戏已经登录的会话内，串行调用只读的 EX 技能强化数据接口，把服务器返回的技能文本交给现有 UI 文本采集器。它默认关闭，不会影响正常翻译，也不会调用强化执行、Debug 或其他写操作接口。

## 安装

将 Release 输出中的以下内容复制到游戏目录对应位置：

```text
BepInEx/plugins/TSKHook.dll
BepInEx/plugins/config.json
BepInEx/plugins/TSKHook/ui_translations.jsonl
```

编辑游戏目录下的 `BepInEx/plugins/config.json`：

```json
{
  "uiCapture": true,
  "skillBatchCapture": true,
  "skillBatchDelayMilliseconds": 1200
}
```

- `skillBatchCapture` 默认是 `false`，必须主动改为 `true`。
- `uiCapture` 必须为 `true`，否则接口响应不会写入采集文件。
- `skillBatchDelayMilliseconds` 是两次请求之间的间隔，允许范围为 500–10000 毫秒。建议保留 1200，不要为了速度降低间隔。
- 修改这些设置后请重启游戏。F1 可以重新读取一般配置，但不能补做启动时未初始化的 UI 采集器。

## 使用步骤

1. 登录游戏并进入角色列表一次。日志出现 `[Skill Batch] Cataloged ... owned units` 表示角色 ID 已收集。
2. 按 `F9`，等待提示进入被动校准状态。
3. 正常打开任意一个角色的 EX 技能强化页面一次。Mod 只观察游戏自己发出的请求，不会主动调用网络接口。
4. 查看日志中的 `Passive calibration completed` 或 `Observed request ID ...`。当前安全诊断版不会继续批量发送；取得真实请求格式后才能恢复批采。

如果 F9 被拒绝，请按弹窗处理：

- `Disabled in config.json`：启用 `skillBatchCapture` 后重启。
- `No UnitList observed`：先打开角色列表。
- `Request observed, but unit_id was unreadable`：请求体不是当前已知格式，把 `LogOutput.log` 和报告交给开发者继续分析。
- `uiCapture must be enabled`：启用 `uiCapture` 后重启。

## 输出文件

采集文本写入：

```text
BepInEx/plugins/TSKHook/ui_capture/ui_texts_v2.jsonl
```

进度与失败原因写入：

```text
BepInEx/plugins/TSKHook/ui_capture/skill_batch_report.json
```

`skill_batch_report.json` 中 `status=completed` 表示队列结束；同时检查 `failed` 是否为 0。原始响应不会落盘，采集器仍只保存经过隐私过滤的日文文本。

把采集结果复制回仓库后，可继续运行现有编译流程：

```powershell
dotnet run --project .\tools\CompileUiTranslations\CompileUiTranslations.csproj -c Release -- `
  .\.local\ui_texts_v2.jsonl `
  .\translations\ui\source\ui_source.jsonl
```

## 覆盖范围与限制

该模式能够自动采集每个已拥有角色的当前 EX 技能等级，以及服务器返回的未来等级。它不能保证补齐角色升级前的所有历史中间等级；真实接口会裁剪低于当前等级的数据。

例如角色技能当前为 7 级时，服务器可能只返回 7、8、9、10 级，不返回 1–6 级。1 级文本可以继续从图鉴、卡池或商店预览采集；2–6 级仍可能需要低等级账号或其他玩家的采集数据合并。

批量请求仍属于 Mod 行为，可能违反游戏或平台服务条款。请自行评估账号风险，不要缩短请求间隔或反复连续运行。
