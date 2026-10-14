# SkeletonGraphic 大按钮图片汉化研究

日期：2026-10-14。范围为用户指定的任务页及后续星骑士、故事、商店页导航大按钮。采用实际游戏调试桥研究，不合入旧实验分支，不改正式插件的图片替换路径。

后续用户授权全量常驻 UI 图片汉化，研究结果已进入正式插件 `1.5.2`，包括 16 张 Spine 导航/主线卡片，具体实现、排版精修与实机验收见 [常驻 UI 图片汉化](ui-image-localization.md)。下文保留最初研究与两张试做的证据。

## 结论

可采用“独立标题汉化 → 同步高亮轮廓 → 原图集局部回填 → 每个 SkeletonGraphic 使用替换图集”的路线。文字本身是独立 RegionAttachment，因此不用重画整张卡片，也不用改 `.skel` 骨骼和动画。标题的原始矩形、裁剪偏移、旋转和图集坐标必须保留；人物、背景、底板、星光、遮罩等继续使用游戏原素材和动画。

首轮研究已完成导出和附件定位。用户随后授权开始试做，主任务与联合演习的标题汉化及图集替换已通过实机验证，详见下方 0.1.6 试做记录；其余 12 个大按钮仍未汉化。

## 实机证据

Unity 2021.3.25f1，UI Inspector 0.1.5.0，游戏 PID 38968，截图 1280×720。按钮 SkeletonData 的导出版本均为 4.0.37；运行库操作以本机 interop 的实际签名为准，不能直接套新版 4.3 文档。

| 页面 | 按钮 | 图集资源 | 提取标题数 |
| --- | --- | --- | --- |
| 任务 | 任务、活动、巡逻、联合演习、种族交流会、菲尼斯塔 | btn_quest_main / btn_quest_event / btn_search / btn_maneuvers / btn_belong / btn_finis | 12 |
| 星骑士 | 队伍编成、星骑士强化、装备强化 | btn_team / btn_chara2 / btn_equip | 5 |
| 故事 | 主线故事、星骑士故事、活动故事档案 | btn_mainstory / btn_charastory / btn_eventstory | 6 |
| 商店 | 购买部、兑换所 | btn_purchase / btn_exchange | 4 |

共 14 个 SkeletonGraphic、14 张图集、27 份标题素材，桥读取零错误。每个按钮都有 `cut_01`、`cut_02`、`cut_03` 三个动画。实际 TeamBtn 悬停从 `cut_01` 切到 `cut_02`，截图中人物和特效变化，悬停说明仍由正式文字翻译包显示中文；移出后回到 `cut_01`。没有点击这些大按钮或触发消费。

`cut_03` 的用途本轮未触发或判定，不能直接称为按下动画；后续验收应包含实际按下与禁用状态。

按钮根节点有 `ButtonWrapper_Spine`、`TKSButton`、`RaycastMask` 等组件，子节点才是 SkeletonGraphic。汉化应保留按钮事件、命中检测和状态切换。

## 标题和高亮

典型标题名为 `quest_main/btn_quest_main_title`，对应的 `_w` 不是普通 TMP 文本，也不是另一套文字排版：导出图像显示它是标题的白色高亮轮廓。更换普通标题后，需要同步新字形的高亮层，避免闪光时出现原日文轮廓。

星骑士强化按钮是例外：标题名为 `chara/btn_chara_name2`，含两行文字，当前 atlas 未发现对应 `_w` 标题，因此这一项只提取一份。商店购买部标题名为 `purchase_ver2/btn_name_purchase`，不能只查找 `_title` 后缀。

举例：主任务标题原画布 360×76，但实际打包区域只有 128×76，并居中偏移；联合演习标题原画布 230×77，打包区域 192×74，旋转 90 度。将生图输出直接铺到整张图集会移动 UV 对应的区域。应分别编辑标题，再确定性缩放、裁剪到分配区域、按原旋转回填，保持整张图集尺寸和其他区域的像素。

本机 AtlasRegion 中可出现 `rotate=false` 而 `degrees=90`，应以 `degrees` 和 atlas 文本为准。不能套用 Image Sprite 导出的 `textureRect` 或把原始矩形误当图集坐标。

## 运行时替换路线

本机 `spine-unity.dll` 的 SkeletonGraphic 提供 `OverrideTexture` 与 `CustomTextureOverride`。后续对照 [Spine 4.0 官方组件源码](https://github.com/EsotericSoftware/spine-runtimes/blob/4.0/spine-unity/Assets/Spine/Runtime/spine-unity/Components/SkeletonGraphic.cs) 并实机验证，确认单渲染器读取 `OverrideTexture`，多渲染器的网格更新读取 `CustomTextureOverride`。因此准备同尺寸、同布局的汉化图集后，分别设置单实例的对应覆盖入口，不能统一只写字典。无需修改共享的原始 atlas material、SkeletonData 或 Skin，也不会影响其他实例。

另一个路线是克隆 Skin/Attachment，只替换标题的 AtlasRegion，但它需要处理动画 attachment key、各状态和 RegionAttachment/MeshAttachment 的 UV 更新，本轮不优先采用。

图集 PNG 存在 alpha=0、RGB 仍有颜色的 bleed 像素；实际渲染 shader 为 `Spine/SkeletonGraphic`。下一步替换前需读取材质的 Straight Alpha/PMA 配置并匹配回填数据，不能仅凭 shader 名假定 alpha 格式。Spine 官方说明材质与贴图的 alpha 工作流必须匹配：[资源与 Alpha 工作流](https://esotericsoftware.com/spine-unity-assets)、[渲染文档](https://esotericsoftware.com/spine-unity-rendering)。

## 0.1.6 两按钮试做记录

游戏 PID 29756，Inspector 0.1.6.0；正式插件仍为原来的 1.4.6。内置 imagegen 分别把 `任務` 改为 `任务`、`合同演習` 改为 `联合演习`，首版中文字形和整体风格满足试做目标。确定性缩回原打包矩形后，按原旋转回填正常层和高亮层，并刷新 1px 图集边缘。

本机逐像素比较发现，两个原始标题的普通层与 `_w` 层 alpha 完全相同（联合演习 17,710/17,710 像素一致，主任务 27,360/27,360 一致），因此白色高亮由同一张中文图的 alpha 构造，避免另一次生图造成字形不对齐。两个实际材质 `_StraightAlphaInput=1`、包含 `_STRAIGHT_ALPHA_INPUT` 关键词，使用直通透明格式，不做 PMA 预乘。

| 验证 | 实机结果 |
| --- | --- |
| 原图集替换 | 单、多渲染器均应用成功，截图外观正常 |
| 原图集恢复 | 2 成功、0 跳过 |
| 汉化显示 | 两个按钮分别显示“任务”“联合演习”，无明显裁切或黑边 |
| 原素材保留 | 除两套标题区域及 1px 边缘外，所有 atlas RGBA 像素不变；保存后 PNG 解码像素也完全一致 |
| 悬停 | 两个按钮实际进入 `cut_02`，人物/物体、特效及文字说明仍正常 |
| 点击与返回 | 主任务进入真实第 71 任务关卡列表；联合演习进入真实演习列表；均通过头部返回按钮返回，未选择关卡或列表项目 |
| 汉化恢复 | 2 成功、0 跳过，单实例覆盖属性和字典读回原状态，截图恢复日文 |
| 错误尺寸 | 以标题小图冒充完整 atlas，被 422 拒绝，未写入 |

当前实机悬停截图记录在进入动画约 0.22 秒时，白色标题 slot alpha=0；高亮贴图及透明轮廓已同步，但未捕获闪白精确帧。`cut_03`/禁用状态仍未独立验证，不能把普通与悬停通过扩展为所有状态验收。没有执行战斗、挑战、领取、跳过或消费。

开发工具构建和 10 项 CLI 测试通过；正式 `TSKHook.dll` 不变。预览只作用于当前实例，完整切页销毁后需重新读身份应用；本机 `trial-01/Apply-Preview.ps1` 自动取当前任务顶部页的两个实例再应用，失败不会盲目重复点击。

首轮素材、两套完整提示词、图集回填脚本、逐像素验证、原图替换/恢复、汉化静止/悬停/列表入口截图保存在 `.local/spine-ui-localization/trial-01/`。下一步可由用户审阅首版外观，再扩展其余按钮和持久加载。

## 建议先做的验证

1. 先用导出的原图集作替换，验证回填/加载/恢复与单、多渲染器行为不改变视觉。
2. 选择联合演习（单渲染器、含旋转标题）和主任务（多渲染器）各一项。通过内置 imagegen 汉化独立标题，允许多次生成择优，目标是接近原游戏风格和清楚可读。
3. 同步高亮轮廓，在原位置回填，验证普通、悬停和按下状态、动态角色/特效、退出重进和恢复。
4. 两种渲染模式通过后，再批量处理剩余 12 个按钮并讨论正式插件的持久加载方式。

## 本机材料

忽略目录 `.local/spine-ui-localization/` 保存四页截图及 `*-spine.json`，14 张 `*-atlas.png` 和 `*.atlas.txt`，27 张标题 `*-original.png`，`title-manifest.json` 和可重复执行的 `Extract-Titles.ps1`。提取脚本只按 atlas 恢复原图区域和偏移，没有改画或生成文字。

通知图标另用内置 imagegen 生成 v2，修正白色圆形底板，88×92 预览已在实际主页检查。任务和礼物保留用户认可的 v1；完整提示词和主页截图留在 `.local/home-icon-localization/`。
