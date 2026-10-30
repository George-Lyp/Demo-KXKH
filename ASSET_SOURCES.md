# 素材来源与授权

本项目使用了三套第三方像素素材。**所有素材均可免费用于个人学习与 demo**，
但授权条款不同，改动或再发布前请先看下表。

| 素材包 | 作者 | 链接 | 授权 | 本项目用在哪 |
|---|---|---|---|---|
| 2D Platformer AssetPack | Pixel AP | https://pixel-ap.itch.io/2d-platformer-assetpack | 个人/商用可用；**禁止再分发或当作自有素材包发布**；**禁止用于训练 AI** | 主角骑士、蝙蝠、史莱姆 |
| Sunny Land | ansimuz | https://ansimuz.itch.io/sunny-land-pixel-game-art | **CC0 1.0**（可商用、可改、不强制署名） | 地形瓦片、三层视差背景、装饰道具、特效、收集物 |
| Kings and Pigs | Pixel Frog | https://pixelfrog-assets.itch.io/kings-and-pigs | **CC0**（可商用、可改、不强制署名） | 猪敌人、King Pig Boss、血条与数字、情绪气泡、炸弹/大炮/门 |

## 注意

1. 2D Platformer AssetPack 的授权**禁止再分发**。本仓库在验收结束前保持 **private**。
2. 三套素材的像素网格不完全一致：骑士包与 Sunny Land 都是 **16×16**，
   Kings and Pigs 的地形是 **32×32**，所以 K&P 的地块（14-TileSets）**没有导入**。
3. 所有贴图统一按 **PPU 16 / Filter Point / 不压缩** 导入，
   通过菜单 `Tools -> 像素画导入设置 (PPU 16)` 批量处理。

## 主角骑士动画帧表

贴图：`Assets/2D Platformer Sprites/2D Platformer Sprites/2D Platformer Character/2D Platformer Character SpriteSheet 1.png`
网格：48 × 48，共 10 列 × 6 行 = 60 帧，Unity 切片后命名为 `_0` ~ `_59`。

| 动画 | 帧范围 | 建议 FPS |
|---|---|---|
| Idle | _1 – _5 | 8 |
| Walk | _6 – _15 | 12 |
| Run | _16 – _21 | 14 |
| Jump | _28 | 单帧 |
| Mid Air | _32 | 单帧 |
| Fall | _33 | 单帧 |
| Hit | _38 – _40 | 12 |
| Death | _41 – _46 | 10 |
| Attack 1 | _47 – _49 | 14 |

> Jump / Mid Air / Fall 各只有 1 帧，用 Rigidbody2D.velocity.y 在三个单帧状态间切换，
> 不要当成逐帧序列来播。

