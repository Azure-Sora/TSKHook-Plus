# UI Sprite 图片汉化

这里存放普通 `UnityEngine.UI.Image` 使用的译制 Sprite。Spine `SkeletonGraphic` Atlas 不在当前运行时替换范围内。

## 目录

- `ui_sprite_overrides.json`：运行时匹配清单。
- `files/`：译制后的透明 PNG。

构建后会复制为：

```text
bin/Release/net6.0/TSKHook/ui_sprite_overrides.json
bin/Release/net6.0/TSKHook/ui_textures/*.png
```

安装时需要同时复制新版 `TSKHook.dll` 和输出目录中的整个 `TSKHook/` 文件夹。

## 清单字段

```json
{
  "id": "footer-quest",
  "enabled": true,
  "spriteName": "btn_quest",
  "textureName": "原始 Texture 名",
  "width": 196,
  "height": 64,
  "objectPathSuffix": "FooterRoot/FooterView(Clone)/Quest",
  "replacementFile": "ui_textures/btn_quest.png"
}
```

- `spriteName`、`width`、`height` 必填并严格匹配。
- `textureName` 建议填写，防止同名 Sprite 误替换。
- `objectPathSuffix` 可选；只需要填写稳定的路径末尾，不要依赖最外层临时节点。
- `replacementFile` 必须是相对于清单所在目录的路径，不能使用绝对路径或 `..` 离开目录。
- PNG 尺寸必须与 `width`、`height` 完全一致，并保留透明通道。

修改游戏目录中的清单或 PNG 后按 F1 即可重新加载。F1 会先恢复原 Sprite，再销毁旧的运行时资源，因此不会持续累积 Texture/Sprite。按 F11 关闭翻译时也会恢复原图，重新开启后会再次应用。
