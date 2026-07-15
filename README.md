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
| uiTranslation | true      | Enable/Disable structured UI text translation                |
| uiCapture | true          | Enable/Disable structured UI text capture                    |
| uiCaptureFlushSeconds | 5 | UI capture file flush interval in seconds                     |
| uiSpriteDump | false      | Allow F9 to export active UI Image sprites                    |

## Key binding

| Key  | Type        | Description                                                                   |
|------|-------------|-------------------------------------------------------------------------------|
| F1   | Reload      | Reload TSKHook config, useful when you need to resize game window size        |
| F5   | Freeze      | Freeze game, mean set game speed to 0x                                        |
| F6   | Reset       | Reset game speed to 1x/normal                                                 | 
| F7   | Decrease    | Decrease game speed (2-0.5 etc), depends on your `speed` config               | 
| F8   | Increase    | Increase game speed (1+0.5 etc), depends on your `speed` config               |
| F9   | UI assets   | Export unique sprites used by active UI Images (requires `uiSpriteDump=true`)  |
| F10  | Translation | Clear translation cache                                                       |
| F11  | Translation | Enable/Disable translation feature                                            |
| F12  | Screenshot  | Screenshot current frame and save to Pictures(`C:\Users\<username>\Pictures`) |
| Ctrl | Skip text   | Skip text via Ctrl button, just like Galgame control system                   |

### Exporting packed UI sprites

Set `uiSpriteDump` to `true`, press F1 to reload the config, open the UI page whose assets you want, and press F9. TSKHook renders every unique sprite referenced by an enabled, active `UnityEngine.UI.Image` on that page, including Tight Packed sprites that UnityExplorer cannot crop correctly. It also exports complete atlas textures referenced by active Spine `SkeletonGraphic` controls.

Each run is written to `BepInEx/plugins/TSKHook/ui_sprite_dump/<timestamp>/`. The root folder contains Sprite PNG files and `manifest.json`. Spine resources are placed in `spine_atlases/`, with complete transparent atlas PNG files, available `.atlas.txt` region data, and `spine_manifest.json`. Keep the dimensions and layout of edited Spine atlas PNGs unchanged. The exporter does not modify loaded game resources. Set `uiSpriteDump` back to `false` when finished.

## Contributing

You're free to contribute to TSKHook as long as the features are useful, such as battle stats log, 360 stamina alert or
something else, except modifying battle data.

## Disclaimer

Using TSKHook violates Twinkle Star Knights and DMM's terms of service.

I will NOT be held responsible for any bans!
