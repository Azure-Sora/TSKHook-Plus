# 本轮交付与验证（2026-10-13）

实现分支：`codex/ui-translation-modernization`。上游 PR 尚未创建。10 月 13 日已安装到游戏并完成下列基础交互实测；未覆盖的验收仍单独保留。

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

## 后续安装与实际启动（2026-10-13 13:23）

- 安装前完整备份现有 plugins、config 及旧启动日志；本地证据目录 `.local/game-validation/20261013-132254`，对应备份 `.local/game-backups/20261013-132254`。
- 安装两个插件 DLL 和 ConfigurationManager；安装后 DLL 哈希与 release 完全一致，旧 config.json 哈希未变，原字体、译文和捕获文件保留。
- 用户指定的 DMMGamePlayerFastLauncher 计划任务成功启动游戏，实际标题画面可见。
- 游戏当前 BepInEx `6.0.0-be.733` 成功加载 TSKHook `1.4.0` 与 ConfigurationManager `19.1.9`；字体与 45,414 条译文加载成功，更新检查完成，已观察启动日志无 Error/Fatal/Exception。
- 新配置保留原 1920×1080、FPS 60、速度步长 0.5、缩放 1、翻译开启及捕获关闭。
- 自动发送两次 F1 与一次标题页点击均无可见响应。只读进程检查确认游戏以管理员权限运行、当前执行环境为普通权限；存在输入权限隔离的可能。用户随后明确确认手动 F1 能打开设置窗口（用户实机证据）；日志也记录设置管理器收集 48 个配置项，HomeData 实际替换 50 个文本值。其余交互测试暂未完成。
- 用户重启 Codex 后再次检查：18 个 ChatGPT 进程、2 个 codex 进程仍为普通权限，游戏仍为管理员权限，自动 F1 和关闭按钮点击仍未生效。实际观察到设置窗口及中文配置标签正常渲染；尚未验证设置修改、捕获开关等交互。

## 普通权限启动后的实机测试（2026-10-13 13:36—13:42）

- 用户关闭游戏后，直接运行已配置 FastLauncher，沿用原任务参数但不通过提权计划任务。只读检查确认游戏与执行环境均为普通权限；自动按键和点击恢复正常，未修改计划任务或 Windows 安全设置。
- **F1 与配置渲染通过**：多次打开/关闭，展开 TSKHook 设置，中文标签和数值显示正常。
- **输入隔离通过（所测点）**：设置窗口打开时按 F11，翻译保持开启；在任务页打开设置后点击窗口外的主页按钮，底层页面未跳转。
- **捕获即时开关通过**：游戏启动时关闭捕获，在窗口中开启后加载 164,620 个既有去重键；进入主页新增并保存 20 条真实文本。文件由 101,838,811 增长到 101,855,101 字节，随后在窗口中关闭捕获并确认保存为 false。
- **磁盘配置重载通过**：将磁盘 SpeedStep 从 0.5 临时改为 0.75，使用维护菜单重新读取，界面显示 0.75，日志确认应用，操作自动回到无操作。再用界面 Reset 恢复 0.5。
- **F11 同步通过**：设置窗口关闭后 F11 关闭翻译，cfg 与日志同步；再次 F11 恢复开启。
- **真实译文显示通过（任务页）**：HomeData 替换 50 个文本值；MissionList 替换 2,894 个文本值。实际页面显示“每日、每周、成就、登录游戏、消耗10体力”等中文，已观察文字无缺字方框。图片标题和未覆盖的角色对白仍有日文，不声称全页面汉化。
- **手动更新检查通过**：维护菜单触发检查，状态显示“翻译包已是当前版本”，日志重新加载 45,414 条。因现有包与公开基线内容相同，此项不证明游戏中不同版本的实际切换与回退。
- 最终比对 cfg：除维护结果文本外，所有配置值与交互测试前一致。游戏留在任务页，未主动领取奖励、消费资源或开始战斗。仅更新本地验证记录与文档，未改插件代码。

日志观察：捕获开启时，LoginBonusList、AppealMovieData、HomeData 的部分 FormatData 回调产生非 JSON 警告；后续有效 JSON 的翻译与 20 条捕获仍成功，需另行评估是否收窄该阶段的告警。MissionList 另有一条跳过不可读 TMP atlas 字形插入的信息，所观察中文正常显示。未观察到 Error/Fatal/Exception。

## 仍待验收

### 1.4.1 日志过滤与后续测试方式

- 用户后续实测反馈普通权限启动会因缓存写入失败报错。因此保留上面的历史验证记录，但不将普通权限启动作为日常使用建议；后续由用户以管理员权限启动并手动测试。
- 用户捕获期间日志出现 `Il2CppInterop` 的 native→managed 异常，IL2CPP 堆栈起始为 `Sprite.get_textureRect → RaycastMask.IsRaycastLocationValid`。堆栈指向游戏原生命中检测，但尚未独立复现确定根因，不宣称已修复游戏缺陷。
- 按用户要求，1.4.1 只在 BepInEx 日志分发处过滤上述明确签名的 Error；首次命中发出一条简短 Warning。未修改 RaycastMask、EventSystem 或点击判定。其他来源、级别、异常类型及不匹配堆栈继续输出。
- `LogFilterSmokeTest` 用保存的完整异常堆栈验证实际 Harmony/BepInEx 日志分发，覆盖重复抑制、首次提示、LF 换行、插值日志重载和 8 类非目标事件；分别通过构建依赖 be.785 与当前游戏实际 be.733 核心程序集测试。这是进程外测试，游戏内过滤效果仍待用户复测。
- 捕获入口跳过 FormatData 的非 JSON 中间数据；损坏的对象/数组仍报警，真实 JSON 捕获逻辑保留。CaptureLifecycleTest 现为 7 项断言，通过。
- 1.4.1 已使用本机游戏程序集构建，并同步两个 release DLL；尚待退出当前游戏后安装。

### 未覆盖项目

- 原有变速、截图、FPS、窗口、剧情翻译、立绘缩放与 Ctrl 跳过行为。
- 捕获多轮反复开关、带未刷盘记录退出、工作台读取真实新增记录。
- 本地预览、不同内容公开包的实际切换、回退后重新进入页面的显示。
- 真实模型接口鉴权、限流及翻译语义质量；当前仅通过协议替身测试。

以上未测项目不能由本次基础实机测试或既有托管测试替代。
