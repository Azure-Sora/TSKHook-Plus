# TSKHook

## [English](README.md)

星騎士mod (DMM Game Player版)

## 功能

1. 更改遊戲速度 (更快的Ready GO不好玩嗎?^^)
   ![3x speed](./img/3x.gif)
2. 內置遊戲截圖
3. FPS設定
4. [中文翻譯](Translation.md)
5. 遊戲視窗大小設定
6. 更改圖鑑放大縮小倍率

## 需求

1. Windows 10或以上
2. 星騎士 DMM Game Player版本

## 安裝方法

下載[Release](https://github.com/TSKModding/TSKHook/releases)
並解壓縮至您的星騎士安裝位置 `C:\Users\<username>\Twinkle_StarKnightsX`

## 設定

如果您不喜歡預設設定，可以編輯config.json(`./BepInEx/plugins/config.json`).

| 欄位        | 預設值  | 說明                         |
|-----------|------|----------------------------|
| speed     | 0.5  | 加快/減慢遊戲速度 (每按一下)           | 
| fps       | 60   | 更改FPS值, 只會在遊戲開啟60FPS的情況下生效 |
| translate | true | 閣啟/關閉中文翻譯功能                |
| width     | 1280 | 遊戲視窗寬度                     |
| height    | 720  | 遊戲視窗高度                     | 
| zoom      | 1.0  | 角色立繪放大縮小倍率                 |
| uiTranslation | true | 開啟/關閉結構化 UI 文字翻譯          |
| uiCapture | true | 開啟/關閉結構化 UI 文字擷取             |
| uiCaptureFlushSeconds | 5 | UI 擷取檔案寫入間隔（秒）       |
| uiSpriteDump | false | 允許 F9 匯出目前啟用的 UI Image Sprite |

## 綁定鍵

| 按鍵   | 類型 | 說明                                       |
|------|----|------------------------------------------|
| F1   | 重載 | 重載TSKHook設定，可用於即時更改遊戲視窗大小                |
| F5   | 靜止 | 將遊戲暫時凍結                                  |
| F6   | 重設 | 將遊戲重設至正常速度                               | 
| F7   | 減少 | 減慢遊戲速度，視乎您的 `speed` 設定                   | 
| F8   | 增加 | 加快遊戲速度，視乎您的 `speed` 設定                   |
| F9   | UI 資源 | 匯出目前啟用 UI Image 使用的 Sprite（需設 `uiSpriteDump=true`） |
| F10  | 翻譯 | 刪除翻譯快取                                   |
| F11  | 翻譯 | 開啟/關閉中文翻譯功能                              |
| F12  | 截圖 | 截取當前的遊戲畫面至`C:\Users\<username>\Pictures` |
| Ctrl | 跳過 | 按Ctrl能跳過文字，就像Galgame的Ctrl skip功能         |

### 匯出 Packed UI Sprite

將 `uiSpriteDump` 設為 `true`，按 F1 重新載入設定，開啟要擷取資源的 UI 頁面後按 F9。TSKHook 會渲染該頁所有已啟用且正在顯示的 `UnityEngine.UI.Image` 所引用的不重複 Sprite，也能正確處理 UnityExplorer 無法裁切的 Tight Packed Sprite；同時也會匯出目前啟用的 Spine `SkeletonGraphic` 所引用的完整 Atlas Texture。

每次匯出會寫入 `BepInEx/plugins/TSKHook/ui_sprite_dump/<時間戳>/`。根目錄包含 Sprite PNG 與 `manifest.json`；Spine 資源位於 `spine_atlases/`，包含完整透明 Atlas PNG、可取得時的 `.atlas.txt` 分塊資料，以及 `spine_manifest.json`。編輯 Spine Atlas 時必須保持原始尺寸與版面不變。匯出器不會修改遊戲載入的資源。完成後建議將 `uiSpriteDump` 改回 `false`。

## 貢獻

您可以PR一些有用的功能，例如戰鬥紀錄、360滿體提示之類，除了修改戰鬥數值

## 免責聲明

使用修改程式進行遊戲，均屬違規行為，可能導致帳號被封鎖。

本人概不負責。
