# 本轮交付与验证（2026-10-13）

实现分支：`codex/ui-translation-modernization`。上游 PR 尚未创建，游戏目录未安装本轮文件。

## 已验证

- 插件以本机游戏程序集编译成功；两个发布 DLL 已同步，BepInEx IL2CPP 依赖固定为 `6.0.0-be.785`。
- RuntimeSmokeTest 14、CaptureSmokeTest 10、ConfigSmokeTest 11、CaptureLifecycleTest 5、PackageSmokeTest 16 个断言通过。配置测试使用实际 BepInEx ConfigFile；生命周期测试使用托管替身。
- 包测试覆盖首次安装、相同版本、损坏缓存重下载、失败保留、回退、最低版本、重复身份拒绝、受保护字段及首次更新回退到本地基线。
- 使用真实 HttpClient 和插件 TranslationPackageStore 下载 [首个公开译文包](https://github.com/Azure-Sora/TSKHook-UITranslations/releases/tag/translations-2026.10.13.1)，完成大小、SHA-256、JSONL 和 45,414 条记录校验，并确认再次检查不重复切换。
- anosu ConfigurationManager IL2CPP `v19.1.9` 的官方压缩包 SHA-256 与发布方一致；其 WindowChecks 使用本游戏程序集完成 461 个方法体、740 个可达方法检查，CompatibilityChecks 通过。这是程序集兼容性证据，不是游戏内窗口实测。
- 翻译工作台和新仓的验证见 [新仓记录](https://github.com/Azure-Sora/TSKHook-UITranslations/blob/main/docs/verification.md)。

构建保留 .NET 6 已结束支持、当前机器 LIB 环境变量旧路径以及 Cpp2IL 传递依赖解析警告；本轮构建成功，未为消除警告替换游戏运行时。

## 本地测试包

`.local/dist/TSKHook-modernization-preview.zip` 包含插件两个 DLL、ConfigurationManager IL2CPP 的两个 DLL 与其许可证、基线译文及安装说明；不包含 BepInEx 核心或游戏程序集。它用于本轮验收，未发布为稳定插件版本。

关闭游戏并备份现有 `BepInEx/plugins`、`BepInEx/config` 后，将包内 `BepInEx` 合并到游戏目录。若已在别的位置安装 ConfigurationManager，保留唯一一套，避免重复插件。旧 config.json 保留用于首次导入。插件包可在下载失败时使用附带的基线译文。

## 待验收（NOT_RUN）

- 游戏内 F1 打开/关闭、中文字体、鼠标和键盘输入不穿透设置窗口。
- 原有变速、截图、FPS、窗口、剧情翻译、立绘缩放与 Ctrl 跳过行为。
- 配置即时应用、修改磁盘配置后的菜单重载、F11 同步。
- 捕获反复开关、退出刷盘、工作台读取真实新增记录。
- 本地预览、公开更新、回退后重新进入页面的译文显示。
- 真实模型接口鉴权、限流及翻译语义质量；当前仅通过协议替身测试。

尚未进行上述游戏和真实模型验收，不能把托管测试或界面截图当作这些项目的通过证据。
