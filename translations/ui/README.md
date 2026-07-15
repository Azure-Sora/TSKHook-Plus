# UI 翻译数据工作流

## 目录

```text
translations/ui/
  source/
    ui_source.jsonl        # 正式待翻译主文件
    ui_conflicts.jsonl     # 稳定键冲突；正常应为空
    compile_summary.json   # 本次编译统计
    by_category/           # 按领域拆分的译文工作文件
```

原始游戏采集文件位于本机 `.local/`，不应提交。正式仓库只需要保存去除用户运行状态后的 `source/` 数据。

## 主文件字段

每行是一个独立 JSON 对象：

- `key`：稳定身份键，例如 `skill:skill_id=...:lv=...:skill_detail`。
- `category`：`skill/item/equip/unit/reward/shop/misc`。
- `identity`：构成稳定键的结构化 ID，不含玩家存档 ID。
- `source` / `sourceHash`：日文原文及其 SHA-256。
- `translation`：中文译文，初始为空。
- `status`：`new/translated/reviewed/verified/stale/conflict`。
- `origins`：原始 API 和 JSON 路径，便于定位 UI 场景。
- `occurrences`：多份采集记录合并到此键的次数。

## 翻译规则

供大模型批量翻译时，请先把 [`LLM_TRANSLATION_GUIDE.md`](LLM_TRANSLATION_GUIDE.md) 的完整内容作为工作规范提供给模型。

1. 在 `source/by_category/*.jsonl` 中只编辑 `translation` 和 `status`，不要手工修改 `key/source/sourceHash/identity`。
2. 机器初译完成后使用 `translated`；人工审校后使用 `reviewed`；进游戏确认排版和语义后使用 `verified`。
3. 保留富文本标签、换行、占位符、数值符号和格式控制字符。
4. 同一术语优先统一译法；技能等级之间只改动原文实际变化的数值或语句。
5. `stale` 表示游戏更新后原文变化，必须重新核对，不能直接当成有效译文发布。

## 重新编译采集

```powershell
dotnet run --project .\tools\CompileUiTranslations\CompileUiTranslations.csproj -c Release -- `
  .\.local\ui_texts_v2.jsonl `
  .\translations\ui\source\ui_source.jsonl
```

编译器先读取旧 `ui_source.jsonl`，再用现有 `by_category/*.jsonl` 覆盖其中的译文和状态，因此分类文件中的人工修改不会被追加采集覆盖。覆盖后再按 `key + sourceHash` 合并新采集；相同键的原文变化时保留旧译文并标记为 `stale`。

分类文件是人工译文的工作源。若其中存在损坏的 JSON、缺失的 `key/sourceHash` 或重复 key，编译器会在写入任何输出文件之前停止。若要撤销某条译文，应保留该行并将 `translation` 清空、`status` 改回 `new`，不要直接删除该行。

任何 `ui_conflicts.jsonl` 非空的情况都应先修复键设计，再进入翻译。

## 编译运行时翻译资产

分类文件完成初译后运行：

```powershell
dotnet run --project .\tools\CompileUiRuntime\CompileUiRuntime.csproj -c Release -- `
  .\translations\ui\source\ui_source.jsonl `
  .\translations\ui\source\by_category `
  .\translations\ui\runtime\ui_translations.jsonl
```

运行时编译器以 `ui_source.jsonl` 的 key 和 sourceHash 为权威数据，只从分类文件读取 `translation/status`。只有非空且状态为 `translated/reviewed/verified` 的条目会进入运行时资产；`new/stale/conflict` 自动跳过。

`translations/ui/runtime/compile_summary.json` 会记录未翻译数量、受保护原文差异以及富文本、占位符、数字和换行警告。富文本或占位符警告必须修复后再发布；数字和换行警告应人工判断。

Release 构建会把资产复制为：

```text
bin/Release/net6.0/
  TSKHook.dll
  TSKHook/
    ui_translations.jsonl
```

安装时必须同时复制 DLL 和 `TSKHook/ui_translations.jsonl`。游戏内按 F10 可重新加载本地 UI 译文，按 F11 可切换总翻译开关；配置项 `uiTranslation` 可单独控制 UI 翻译。
