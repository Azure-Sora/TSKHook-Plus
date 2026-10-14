# 翻译资源更新与简繁转换（开发版 1.6.0）

整理和实机验收后，用户授权公开发布；插件 v1.6.0、文字 2026.10.14.2 与图片 2026.10.14.3 已于 2026-10-14 独立发布。插件仍输出单个 `TSKHook.dll`；正式发行 DLL 已按 Stage-Release 暂存及校验。用户随后授权安装及重启，开发版已完成限定实机验收，详见 verification.md。

## 仓库边界

- TSKHook-Plus：游戏运行时、字体、配置、文本与图片包协议、更新弹窗及隔离测试。
- TSKHook-UITranslations：工作台、文字数据、`translations/images/ui_images` 图片、`tools/UiInspector` 调试桥及其 `tests/UiInspectorSmokeTest`。

调试桥不属于正式插件依赖。图片资源不再复制进插件的构建/发行目录。迁移前的 90 文件 SHA-256 清单在翻译仓 `docs/inspector-image-migration.json`；路径适配后的 README 和测试文件允许与迁移前不同。原实机证据仍留在主仓忽略的 `.local` 目录。

## 独立版本和渠道

| 资源 | 版本标签与附件 | 默认清单 | 本地状态 |
|---|---|---|---|
| 文字 | `translations-<版本>`；ui_translations.jsonl、manifest.json、validation.json | `releases/latest/download/manifest.json` | `TSKHook/packages/active.json` |
| 图片 | `images-<版本>`；ui_images.zip、image-manifest.json | `releases/download/image-channel/image-manifest.json` | `TSKHook/image-packages/active.json` |

图片发布与工作台发布必须使用 `latest=false`，保留旧插件的文字更新入口；文字继续由现有工作台 ReleasePublisher 发布。图片清单的下载 URL 固定到不可变的 `images-<版本>` 附件；image-channel 仅保存当前图片清单。不需要文字、图片和插件三个版本号相同。

图片 ZIP 只允许扁平 manifest.json 与 PNG 文件，限制数量、解压大小及路径。下载校验大小与 SHA-256，解压后再校验每张图片、尺寸、源指纹与内外版本一致性。校验通过才原子切换 active.json；失败不改变当前指针。首次远程更新前保留已有离线图片包，支持单独回退。规则有变化时恢复旧覆盖并释放自有纹理，再按原有源指纹规则重新应用；规则相同、仅版本/目录改变时保留已有覆盖，避免导航闪回原图。

启动 15 秒后及每小时分别检查文字、图片。Maintenance 手动检查两类资源，回退分别提供“回退上一版文字包”和“回退上一版图片包”。本地预览模式暂停公共资源更新。image-channel 已启用；渠道不可用时检查显示失败并保留离线图片，文字渠道仍能独立成功。

## Unity 更新提示

插件创建独立 ScreenSpaceOverlay Canvas，右下角 320×90 面板；显示检查、实际已下载字节/总字节与百分比、校验、应用及最终结果。完成或失败后 6 秒隐藏，不受游戏 timeScale 影响；复用已有 TMP 中文字体，不增加字体包。没有 GraphicRaycaster，所有 Graphic 的 raycastTarget=false，不挡点击和悬停。异步网络结果排队回 Unity 主线程，字体尚未加载时等到可用再显示。关闭插件销毁面板和自有资源。

## 简繁配置

F1 → Translation → 中文简繁转换，对应配置项 `ChineseScript=Original|Simplified|Traditional`，默认保持译文原样。

只对已命中的 UI 译文、剧情、角色名和标题逐字转换，不处理词语、地域用词和一对多语义。标签、标签属性、花括号占位符、数字和补充平面字保持原样；受保护的 BattleEnd 控制字段、未匹配的游戏原文和 JSON 键不转换。文字源文件、sourceHash、包校验和不改变；图片文字按图片资源包提供的字形显示。已有缓存/页面需重新加载或请求后采用新选择。

只内置 OpenCC 常用汉字区的单字首选对应关系：繁转简 2,853 个、简转繁 2,593 个；不依赖 OpenCC 运行库。排序源字符差分编码后 GZip 字表 14,520 字节，Apache-2.0 许可证也压缩嵌入 DLL；与同一代码、不嵌入字表和许可的对照构建相比，DLL 增量为 17,920 字节（17.5 KiB）；完整许可和来源固定提交见 `Extensions/Core/Data/`。多义字如“发”按固定对应转换，不保证语义回转。维护脚本 `Extensions/Core/Data/build-character-map.py` 使用固定源与校验和重建。

## 本地构建与验证

```powershell
dotnet build TSKHook.csproj -c Release '-p:GamePath=E:\Games\DMM\Twinkle_StarKnightsX'
dotnet run --project Extensions/Tests/ResourceUpdateTest -c Release -- E:\Codes\TSKHook-UITranslations\translations\images\ui_images
dotnet run --project Extensions/Tests/UiImagePackageTest -c Release -- E:\Codes\TSKHook-UITranslations\translations\images\ui_images
```

本轮已重启真实游戏并核对右下角弹窗、实际本机 HTTPS 下载过程、三种简繁模式的菜单和弹窗区域下的页面点击；范围、限制及恢复记录见 [验证记录](verification.md)。旧图片翻译的实机验收见 [图片汉化记录](ui-image-localization.md)。
