# 1.7.1 本地兼容性修正（2026-10-26）

用户试用 1.7.0 后反馈：没有再感到卡顿，汉化显示正常，但控制台出现过一段报错。此次依据用户提供的日志修正两个图片路径问题。用户随后确认 1.7.1 使用正常；该反馈不扩大为全功能验收。

## 历史诊断日志结论

- 进入首次 BattleStart 后，同一个 `Image.set_sprite → Graphic.SetAllDirty → Graphic.SetLayoutDirty` 空引用异常连续记录 14 次，均经 Il2CppInterop native→managed trampoline。堆栈落在被 Hook 的原 setter 内，没有进入本插件 postfix 的证据。不能从堆栈证明具体哪个对象为空，也不能断言原游戏完全无关；移除这条非必需 detour 是本次兼容性处理。
- `[UI Images] queue: Method unstripping failed` 是已确认的插件调用缺陷。本机 interop 的无参数 `Scene.GetRootGameObjects()` 方法体直接加载这条错误信息并 throw。整个日志 30 个统计窗口中 `SceneDiscovery` 都为 0，图片覆盖主要通过启用/初始化事件完成，因而用户未必看到漏翻。
- 异步读回已启用，日志含实际读回统计。进入战斗的第一个统计窗口存在 334.5ms 峰值及 3 个 >50ms 帧；该窗口含切换和异常，不能仅凭汇总归因。随后连续 6 个 `battle=True` 窗口 P99 约 17ms、无 >33ms 帧，图片逻辑最大耗时不超过 0.58ms。标记包含战斗管理器仍存活的阶段，不能强行等同于每一帧都在交战。
- GitHub 连接失败影响在线资源更新；日志确认保留本地资源。与上述空引用和补漏异常分开处理，没有更改用户网络设置。

用户原始日志仅保存在忽略目录 `.local/perf-171/`，未作为仓库文件发布。

## 修正

1. 移除 `Image.set_sprite` 和 `Image.set_overrideSprite` 的 Harmony detour，保留启用、Spine 初始化和场景事件。
2. 已发现的 Image 用有上限的组件观察队列跟踪原 Sprite 与有效 override 的变化；每帧最多检查 32 个队列条目，约 0.2ms 软预算并受图片总预算限制。同一组件正常检查间隔至少 100ms。队列容量 4096，不搜索全局对象，不启动 GPU 操作；战斗中暂停。未启用对象移出观察队列，重新启用时再注册。
3. 修正补漏遍历为实际存在且有可执行方法体的 `GetRootGameObjects(List<GameObject>)`；组件查询使用非泛型 Type 重载，减少对恢复泛型包装的依赖。
4. 图片队列异常记录完整堆栈，继续按键限量输出，便于后续精确定位。

保留 1.7.0 的异步读回、后台像素处理、文字缓存、换包保护及独立图片汉化开关。主线移除运行时性能检测和诊断专用开关；检测代码与完整说明保留在 `codex/performance-diagnostics` 分支。

## 验证与限制

- 本地 Release 构建通过，程序集版本 1.7.1.0；仍只有单个 TSKHook.dll。
- 图片运行时代码的 18 项生命周期回归通过。新增/调整用例覆盖：没有 setter 通知的底栏换图、OnEnable 后才赋图、无参场景 API 明确抛错时经 List 重载完成补漏、补漏过程中无兼容性警告。
- 检查最终 DLL 的 IL 和实际游戏 interop：没有两个 Image setter 的 Harmony 注解；Discover 确实调用 List 重载和非泛型 GetComponent；所选 Scene 重载没有 unstripping throw，占位缺陷可在本地静态排除。
- 用户复测后确认使用正常；本地测试和这次复测仍不代表所有游戏场景或设备均已覆盖。

关闭游戏、备份原 DLL，再替换 1.7.1；字体、配置和翻译包保持原样。优先复测进战斗，以及菜单底栏选中/未选中图片状态。主线不再输出性能统计；如出现新异常，可保留 BepInEx/LogOutput.log 供排查。
