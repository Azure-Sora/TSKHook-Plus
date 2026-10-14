# 翻译包协议 v1

插件支持独立于 DLL 的 UI 翻译包。工作台见 [TSKHook-UITranslations](https://github.com/Azure-Sora/TSKHook-UITranslations)。

## 清单

`manifest.json`：

```json
{
  "schemaVersion": 1,
  "version": "2026.10.13.1",
  "minimumPluginVersion": "1.4.0",
  "downloadUrl": "https://github.com/OWNER/REPO/releases/download/translations-2026.10.13.1/ui_translations.jsonl",
  "sha256": "64 lowercase hexadecimal characters",
  "size": 12345,
  "entries": 123
}
```

使用 HTTPS，清单不超过 64 KiB，包不超过 128 MiB。包版本是显示用标识，包内容由 SHA-256 标识。工作台发布唯一标签的附件，`releases/latest/download/manifest.json` 作为稳定入口。

## 运行时 JSONL

每行包含 `schemaVersion: 1`、唯一 `key`、原文 SHA-256 `sourceHash`、非空 `translation`。使用 UTF-8。未知协议、缺失字段、重复 key、错误哈希或行数不一致都拒绝整个包；失败不清空旧内存索引。

从插件 1.4.6 起，静态 UI 可选 `renderedOnly: true`。这类条目只在 TMP 显示层使用，不进入网络 JSON 的精确或原文哈希匹配。普通历史条目缺省为 false，行为不变。

少量拼接标签还可携带 `sourceTemplate`，例如 `スタミナが {0} 回復します。必要数: {1} 個`。此时必须为 renderedOnly，sourceHash 必须为模板本身的 SHA-256。模板仅支持 {0}..{3}，两侧占位符一致；参数限于有界数字、日期和时间字符，不能匹配任意用户名、富文本或整句。匹配始终锚定整段文本；冲突模板不替换。每包最多 64 条模板，普通 UI 原文仍优先精确匹配。带这些字段的包清单最低版本必须为 1.4.6。

`Extensions/Core` 是读取、身份键与包校验规则的权威源码，不依赖 Unity。插件将这份源码直接编入 `TSKHook.dll`，发行时仅需一个插件 DLL；工作台通过独立核心项目复用同一份源码。

## 本地目录

相对 `BepInEx/plugins/TSKHook/`：

- `ui_capture/ui_texts_v2.jsonl`：捕获记录（含 API、路径、上下文、原文、原文 hash、首次捕获时间）。
- `ui_translations.jsonl`：兼容旧版的本地初始翻译包；没有远程包时读取。
- `packages/<sha256>.jsonl`：不可变的远程翻译包。
- `packages/active.json`：当前与上一版本的单个原子状态文件。
- `ui_preview.jsonl`：工作台预览文件，开启本地预览模式后每两秒检测变化。该模式暂停公共更新。

回退交换当前/上一版本指针。旧包保留，便于排障；不会删除原始捕获。热更新只保证之后的替换使用新索引，已打开的游戏页面可能需要重新进入。


图片资源另用 image-manifest.json 和 ui_images.zip，版本标签为 images-<版本>，与此文字包无关。图片和工作台 Release 不得设为 latest，确保旧文字入口仅跟随文字版本。图片协议、校验及独立回退见 [资源更新与简繁配置](resource-updates-and-script.md)。
