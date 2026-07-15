# TSKHook

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

You can edit config.json(`./BepInEx/plugins/config.json`) if you don't like default settings.

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
| F1   | Reload      | Reload TSKHook config, useful when you need to resize game window size        |
| F5   | Freeze      | Freeze game, mean set game speed to 0x                                        |
| F6   | Reset       | Reset game speed to 1x/normal                                                 | 
| F7   | Decrease    | Decrease game speed (2-0.5 etc), depends on your `speed` config               | 
| F8   | Increase    | Increase game speed (1+0.5 etc), depends on your `speed` config               |
| F10  | Translation | Clear translation cache                                                       |
| F11  | Translation | Enable/Disable translation feature                                            |
| F12  | Screenshot  | Screenshot current frame and save to Pictures(`C:\Users\<username>\Pictures`) |
| Ctrl | Skip text   | Skip text via Ctrl button, just like Galgame control system                   |

## UI Sprite overrides

TSKHook can replace selected `UnityEngine.UI.Image` sprites with translated PNG files without modifying the original game atlas. Runtime files live under `BepInEx/plugins/TSKHook/`:

- `ui_sprite_overrides.json`: explicit, fail-closed target selectors.
- `ui_textures/`: replacement PNG files.

The bundled first rule replaces the 196x64 footer `btn_quest` sprite. Press F1 after editing the manifest or PNG files to restore the original sprites, reload assets, and apply the new files. F11 temporarily restores original sprites when translation is disabled. Replacement PNG dimensions must exactly match the manifest; paths must remain relative to the manifest directory.

## Contributing

You're free to contribute to TSKHook as long as the features are useful, such as battle stats log, 360 stamina alert or
something else, except modifying battle data.

## Disclaimer

Using TSKHook violates Twinkle Star Knights and DMM's terms of service.

I will NOT be held responsible for any bans!
