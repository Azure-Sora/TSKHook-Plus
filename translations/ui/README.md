# UI 翻译数据工作流

## 目录

```text
translations/ui/
  source/
    ui_source.jsonl        # 正式待翻译主文件
    ui_conflicts.jsonl     # 稳定键冲突；正常应为空
    compile_summary.json   # 本次编译统计
    by_category/           # 按领域拆分的只读/分工视图
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

1. 只编辑 `translation` 和 `status`，不要手工修改 `key/source/sourceHash/identity`。
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

编译器按 `key + sourceHash` 保留已有译文和状态；相同键的原文变化时保留旧译文并标记为 `stale`。任何 `ui_conflicts.jsonl` 非空的情况都应先修复键设计，再进入翻译。
