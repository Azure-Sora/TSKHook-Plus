# 1.7.0 性能优化（2026-10-26）

本版针对战斗中间歇性停顿改造图片与文字处理路径。用户试用后反馈未再出现卡顿、汉化正常；这不等同于全场景验收。1.7.1 继续修正图片兼容性问题。

## 改动

- 图片发现：1.7.0 在 Image 换图/启用、Spine 初始化/启用时入队（1.7.1 改为启用事件与有预算的已知组件观察，见对应说明），按组件去重。移除每 250ms 两次 `Resources.FindObjectsOfTypeAll`；启动、场景加载和战斗退出请求一次分帧场景遍历，战斗外每 30 秒分帧补漏。每帧最多 32 个发现步骤，图片队列最多 64 个对象，约 0.8ms 软预算；Unity 上传等单个调用不能被中途打断。
- 图片准备：图片包的文件读取、校验、PNG 解码在加载阶段或后台完成；换包准备过程串行，旧准备结果不能覆盖新请求。运行时只上传已解码 RGBA。
- 图集：同一源纹理共用 GPU 读回结果（CPU 快照缓存上限目标 128 MiB；单个超限图集不会长期缓存），最多两个像素准备任务。像素提取、SHA-256 和 Spine 区域合成在后台执行，不从工作线程访问 Unity 对象。支持旋转图集与相反的 GPU 行方向，仍须通过全部原始像素哈希。
- 异步读回：当前游戏为 Unity 2021.3，互操作程序集缺失 Request 包装，但引擎保留相关 icall。适配器仅在 64 位 Unity 2021.3、平台支持且全部入口可解析时启用；逐帧轮询完成后复制数据，保持 RenderTexture 至请求结束，不调用同步等待。关闭/换包后丢弃旧结果。其他环境回退为战斗外共享同步读回，不在战斗中启动该操作。
- 战斗保护：跟踪实际 `TSKBattleManager.Start/OnDestroy` 和活动状态；期间停止补漏遍历、新图片准备/上传和准备好的资源包切换。已经准备完成的图片仍可直接绑定；战斗前已提交的读回会完成清理。结果界面可能随同战斗管理器生命周期延后准备，退出战斗页后恢复。
- 资源生命周期：保留等价图片包的现有覆盖；仅恢复自己仍拥有的 override；停止/换包取消旧任务；定期释放失效或长期不再绑定的替换资源。关闭图片开关后可重新启用。
- 文字：每组件保留原文与最终显示值，无变化直接跳过。共享查询缓存同时保存命中与未命中，容量 4096；纯数字不占共享缓存。换包、简繁设置及开关变化触发有预算的已知组件刷新；最终相同文字不重复写回。组件记录同样有容量上限，淘汰时恢复仍由其持有的译文。
- 更新：运行时校验、构建文字索引和解码图片包在后台串行完成，主线程只发布准备好的结果；新请求覆盖旧请求。主线程通知队列限量处理，下载进度合并，弹窗只在文字或进度改变时刷新。

首次打开页面时，新图片可能在后台准备完成后才切换为中文。原始像素校验失败仍保留游戏原图，禁止仅凭文件名替换或覆盖活动插画。

## 本地验证

构建针对 `E:\Games\DMM\Twinkle_StarKnightsX\BepInEx\interop`，目标保持 .NET 6 / BepInEx IL2CPP。输出仍为单个 `TSKHook.dll`，不新增运行时第三方依赖。构建沿用既有 NU1603 依赖解析和本机 LIB 路径警告。

- 原有文字、模板、控制标识符保护、更新/回滚、简繁、配置、捕获及日志过滤回归。
- `PerformanceRuntimeTest`：容量、命中/未命中、版本/简繁失效、恢复原文、队列去重、数值过滤、像素方向/旋转/边缘挤出、热缓存零托管分配。真实 72 张 PNG 与独立 Pillow 解码结果逐字节比对。
- `ReadbackLifecycleTest`：编译真实读回控制代码，以模拟 native 入口验证共享、非阻塞提交、战斗门禁、资源保留/取消、错误及兼容路径。
- `ImageRuntimeLifecycleTest`：编译真实图片服务，以 Unity 替身验证换图、热缓存、战斗门禁、关闭/重开、外部 override、等价包保持、旧任务失效、像素不匹配、单/多 renderer Spine。

上述替身测试不是实际 Unity/显卡集成测试。更广泛的 Hook 调用覆盖、图片显示和显卡兼容性仍需游戏内验收。完整本地执行日志保存在 `.local/perf-170/`。

```powershell
dotnet build TSKHook.csproj -c Release '-p:GamePath=E:\Games\DMM\Twinkle_StarKnightsX'
python Extensions/Tests/PerformanceRuntimeTest/png_oracle.py E:\Codes\TSKHook-UITranslations\translations\images\ui_images .local/perf-170/png-oracle.json
dotnet run --project Extensions/Tests/PerformanceRuntimeTest -c Release -- E:\Codes\TSKHook-UITranslations\translations\images\ui_images .local/perf-170/png-oracle.json
dotnet run --project Extensions/Tests/ReadbackLifecycleTest -c Release
dotnet run --project Extensions/Tests/ReadbackLifecycleTest -c Release -- --fallback
dotnet run --project Extensions/Tests/ImageRuntimeLifecycleTest -c Release
```

## 安装与试用

退出游戏，备份原来的 `BepInEx/plugins/TSKHook.dll`，用本地测试版 DLL 替换。保留字体、配置、文字/图片包；不要同时安装两份 TSKHook。若仍有旧版 `TSKHook.Translation.Core.dll`，按单 DLL 版本迁移说明移出 plugins 目录。回退时退出游戏并换回备份 DLL。

F1 提供独立“图片汉化”开关，默认开启；关闭后恢复插件仍持有的图片覆盖，重新开启后恢复处理。文字补译由翻译总开关与 UI 汉化控制。

主线不包含运行时性能采样、耗时汇总或诊断开关。需要进一步排查卡顿时，使用 `codex/performance-diagnostics` 分支；其中保留完整检测代码和测试说明。
