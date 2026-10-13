# TSKHook-Plus

基于 [TSKModding/TSKHook](https://github.com/TSKModding/TSKHook) 的 UI 文本汉化插件。

## 插件与翻译工作台

本仓只维护游戏集成、配置、捕获、字体兼容和翻译包更新。原文、译文、术语、批量工具及独立 Windows 工作台已迁至 [TSKHook-UITranslations](https://github.com/Azure-Sora/TSKHook-UITranslations)。图片导出与替换实验已退出维护范围。

## 安装与配置

保留游戏已有的 BepInEx 6 / IL2CPP 安装。将发行包的 `TSKHook.dll` 和 `TSKHook.Translation.Core.dll` 一起放入 `BepInEx/plugins`。安装 [anosu/ConfigurationManager 的 IL2CPP 发行版](https://github.com/anosu/BepInEx.ConfigurationManager/releases)，其中 `ConfigurationManager.dll` 和 `BepInEx.KeyboardShortcut.dll` 必须一起安装。不要用构建依赖替换游戏 BepInEx 核心程序集。

按 **F1** 打开设置；管理器负责此按键，原插件不再用 F1 重载。首次运行将旧 `plugins/config.json` 导入 BepInEx 配置，旧文件保留但不再作为配置源。所有设置和 F11 使用同一份配置。需要重新读取磁盘配置时，在 **Maintenance / 执行操作** 中选择“重新读取并应用配置”；操作执行后自动恢复“无操作”。

界面中有设置生效时机说明。ConfigurationManager 未安装时仍可在关闭游戏后编辑 `BepInEx/config/` 中的插件配置。安装兼容性、字体和游戏内输入仍需要按本游戏验证。

## UI 汉化与更新

默认启动后检查翻译包，之后每小时检查一次；失败保留已有译文。维护菜单可手动检查、重载或回退上一份远程翻译包。旧 `plugins/TSKHook/ui_translations.jsonl` 可继续作为离线初始译文。F10 保留剧情缓存清理并重新加载当前 UI 包。

维护者启用捕获后，插件将记录写入 `plugins/TSKHook/ui_capture/ui_texts_v2.jsonl`；工作台自动整理与翻译。开启“本地预览模式”后读取工作台生成的 `ui_preview.jsonl` 并暂停公共包更新。新包不会强制刷新所有已打开页面，必要时重新进入页面。

文本捕获仍受游戏按需加载影响；未加载的角色/技能及图片文字不在自动覆盖范围。

## 开发

- [实施方案](docs/ui-translation-modernization.md)
- [翻译包协议](docs/translation-package-format.md)
- [交付与验证记录](docs/verification.md)
- 构建：`dotnet build -c Release -p:GamePath=游戏目录`
- 发布前：`./scripts/Stage-Release.ps1 -GamePath 游戏目录`，审阅并提交两个 DLL，再创建插件版本标签。
- 纯托管验证入口位于 `tools/`；这些测试不代表游戏内验证。

以下保留上游功能介绍；配置入口以本页说明为准。

## [中文](README_TC.md)

Twinkle Star Knights mod for DMM Game Player version

## Feature

1. Game speed (more fun if Ready Go is faster, is not?^^)
   ![3x speed](./img/3x.gif)
2. In-game Screenshot
3. FPS setting
4. [Translation](Translation.md) (Traditional Chinese only)
5. Game window size setting
6. Picture Book zoom ratio

## Requirement

1. Windows 10 or newer
2. Twinkle Star Knights DMM Game Player version

## Installation

Download and extract [Release](https://github.com/TSKModding/TSKHook/releases) zip to your Twinkle Star Knights install
location `C:\Users\<username>\Twinkle_StarKnightsX`

## Config

Press F1 to open ConfigurationManager. Legacy JSON settings are imported once; use the generated BepInEx configuration afterward.

| Name      | Default Value | Description                                                  |
|-----------|---------------|--------------------------------------------------------------|
| speed     | 0.5           | Increase/Decrease game speed each step (per click)           | 
| fps       | 60            | Override FPS setting, take effects when value bigger than 60 |
| translate | true          | Enable/Disable translation feature                           |
| width     | 1280          | Game window width                                            |
| height    | 720           | Game window height                                           |
| zoom      | 1.0           | Character standing zoom in/out ratio                         |

## Key binding

| Key  | Type        | Description                                                                   |
|------|-------------|-------------------------------------------------------------------------------|
| F1   | Settings    | Open ConfigurationManager; use Maintenance to reload configuration |
| F5   | Freeze      | Freeze game, mean set game speed to 0x                                        |
| F6   | Reset       | Reset game speed to 1x/normal                                                 | 
| F7   | Decrease    | Decrease game speed (2-0.5 etc), depends on your `speed` config               | 
| F8   | Increase    | Increase game speed (1+0.5 etc), depends on your `speed` config               |
| F10  | Translation | Clear translation cache                                                       |
| F11  | Translation | Enable/Disable translation feature                                            |
| F12  | Screenshot  | Screenshot current frame and save to Pictures(`C:\Users\<username>\Pictures`) |
| Ctrl | Skip text   | Skip text via Ctrl button, just like Galgame control system                   |

## Contributing

You're free to contribute to TSKHook as long as the features are useful, such as battle stats log, 360 stamina alert or
something else, except modifying battle data.

## Disclaimer

Using TSKHook violates Twinkle Star Knights and DMM's terms of service.

I will NOT be held responsible for any bans!
