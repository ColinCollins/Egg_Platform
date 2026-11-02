# DLevel_088 脚本结构记录

## 基本信息与参考基线

2026-09-15。20088 / DLevel_088 / 反重力区域，用户要求不再复用弹性系列物理属性，新增可调整矩形、圆形、多边形和颜色的反重力区域。

参考 D002 的猫夹球首笔释放与现有 Draw 生命周期、D087 的组件职责记录。本次接收的 D088 地图已是普通物理基线，不是 D087 的高弹配置：没有 Level84 限速/补弹和新线出生保护，也没有 Level85 尖刺处理器及系列专用材质。保留这份地图的全部原节点和参数，不从 D087 覆盖或修改共享组件。当前整关解法验收为 `uncertain`。

## 脚本树与职责

```text
DLevel_088
├─ DrawLevelCtrl                 既有状态、输入、成功/失败与标准重建
├─ DrawLineFeature               多笔画线，20 墨水，strokeMass=200、最小质量比例0.25
├─ DrawLevelCountdownCtrl        首笔开始15秒失败倒计时
├─ DLevel2Ctrl                   猫夹球可见入场、首笔释放
├─ level2 / Alien / 黄球          普通质量10，gravityScale=1，阻力0.2/0.5，无系列专用材质
├─ 成功门、场景、相机、提示线和出界区（用户布局保留）
└─ DLevel088_ReverseGravityZone
   ├─ DLevel88GravityZone        区域参数、重力所有权和局部填色
   ├─ PolygonCollider2D         唯一触发轮廓，不承载物体
   ├─ MeshFilter                只在运行时持有本实例 Mesh
   ├─ MeshRenderer              本关透明底色＋内部手绘平铺，Background / order 2
   └─ Outline / LineRenderer    预置子对象，重复纹理描边，Background / order 3
```

- `DLevel88ZoneGeometry`：无状态的本地几何校验/耳切剖分，支持顺逆时针和凹多边形；无 DB、场景对象或物理依赖。
- `DLevel88GravityZoneEditor`：中文 Inspector、可拖多边形顶点、Scene 填色和轮廓；只读局部字段或修改明确绑定的 Collider，提供 Undo，不创建编辑器 Mesh/GameObject，不访问运行时单例。
- 没有增加空壳 DLevel88Ctrl、共享接口、全局事件、Actor 动作或 HUD 改动。

## 生命周期与关键调用

1. Draw 标准加载链创建关卡、HUD，D002 完成可见入场；区域 OnEnable 只在 Play 状态创建自己的 Mesh、配置 Trigger 和颜色属性块。
2. 首笔由既有 D002 释放球到 Dynamic；画线按模板形成同一刚体下的多个 Collider。区域 FixedUpdate 用明确绑定的 Collider 做局部 Overlap，过滤 Trigger、静态/运动学、未模拟刚体和配置层外对象。
3. 每个区域按 Rigidbody 去重，多个区域通过仅本类使用的重力租约共同持有：首次接触捕获原 gravityScale 并取负，最后离开才恢复。原重力为0保持0，负数会变正。不改变线/角速度、质量、阻力、材质或约束。
4. 离开、所有实体 Collider 停用、刚体改为 Kinematic/停止模拟或被销毁，会在下一物理步释放；区域自身 Disable/Destroy 立即释放。没有依赖可能漏发的 OnTriggerExit。其它机制主动改写了不同的 gravityScale 时，退出不覆写其新值。
5. 成功门、出界、倒计时、失败/重开/退出沿原 Draw 流程；新组件不接管结算。旧关销毁时释放租约和本实例 Mesh；SubsystemRegistration 清理禁用域重载时的静态残留。不修改共享 Reset/Replay/胜利转场。

## 序列化资源与配置方式

- 关卡 Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_088.prefab`，原 GUID `bb1e619720a3b5a4daacf594775a1c5d` 保持。
- 可单独拖入的区域 Prefab：`Assets/Game/Prefabs/DrawLevel/Shared/DLevel088_ReverseGravityZone.prefab`，GUID `28708e2742eeaa04289e119786a027e3`。
- 本关实例：根的子对象，位置 `(0,-0.5,0)`，默认矩形 `6×3`，蓝色 Alpha `0.28`。这是可编辑的初始区域，不代表整关路线已验收。
- 区域脚本 `regionCollider / regionMesh / regionRenderer` 分别显式绑定 `8005534297100225288 / 7992833471236719941 / 2030652945647274334`；全部与脚本同一节点，不做名字搜索或层级扫描。
- 选择区域的脚本 Inspector：矩形改“矩形宽高”，圆形改“圆形半径”，多边形编辑本地顶点数组或拖 Scene 顶点。支持3～64个不重复、不自交顶点，不支持孔洞。
- 改 Transform 调整位置/旋转；缩放也参与范围。圆形以64边轮廓逼近，碰撞和可见填色使用同一轮廓；非等比缩放为椭圆。
- “区域颜色（含透明度）”控制颜色与 Alpha；编辑态 Scene 预览需开 Gizmos，Game 运行时由实际 Mesh 显示。没有使用 ExecuteAlways 或编辑态实例化。
- 不要单独编辑底层 PolygonCollider 的路径，脚本参数才是唯一形状来源；Inspector 会同步碰撞体，运行时重建一次。颜色用每实例 PropertyBlock，不改共享材质。

## 数据、UI、资源与事件流

- LevelData.xlsx / draw_level 第68行新增20088，位于20087后、20999前。路径DLevel_088，标题反重力区域，1.14.0，levelType0、lockType1、墨水20、星级0/5/10，与本关原 Inspector 阈值一致。
- LevelSort.xlsx / drawLevelSort 第68行新增序号63→20088。两份 Draw JSON 与 Luban 实际生成逐项一致；源表旧数据/类型/样式和无关 XML 部件保持。
- Tips 由本地 I2 的 `S_DrawLevel_Tips_Level_88` 显示，新增12种现有语言位置的翻译，其余语言槽沿原空槽 convention。未修改云端表或 RemoteConfig。
- 区域配置完全保存在 Prefab；无运行时 DB、存档、资源加载、UI/global event、Analytics 或 SDK 新入口。普通球、画线、门和 HUD 继续沿既有加载/结算边界。

## 结构复盘与验证边界

局部采用：形状由一个参数源同时生成碰撞和填色；复合 Collider 按刚体去重，多区域共享本类内的所有权。几何预览用托管数据和 Handles，运行时才创建 Mesh。保留为候选：未来其它关确实需要同类区域时再讨论共享，不迁移旧关。

静态证据、编译、真实物理/渲染结果及未覆盖风险统一见本目录 `DLevel_088功能与风险评估.md`。进入完整主工程后的提示画法、整关通关路线、连续失败重开、解锁/打点、Android/iOS 真机均需实机补验，不能用隔离 PhysicsScene2D 代替整关验收。

回滚：修改前快照在 `E:/WorkSpace/outputs/dlevel88-gravity-20260915/`。只撤销本关区域实例/新增组件资源、88标题词条、20088和排序63，并同步两份Draw JSON；保留用户后续地图改动和其它工作区改动。

## 2026-09-15 区域画线层修正

- 区域物体物理Layer固定配置为Draw(14)，不改变Renderer的SceneLayer。DrawLineFeature会阻挡非Draw层Collider（含Trigger）；Default层不适用于这种允许在内部画线的区域。
- 只修改区域资产的一行m_Layer。DLevel_088当前实例没有Layer覆盖，自动继承；用户已调整的位置(7.56,0.05)和尺寸12.15×12.45保留，没有回填初始值。
- 画线阻挡检测与区域重力检测是两条独立路径：前者排除Draw层，后者继续用自身Overlap和目标层过滤器识别进入的动态刚体。没有关闭Trigger或全局Trigger查询，不放行普通墙体。
- 增加三种形状的实际Physics2D阻挡判定对照和重力回归，测试不覆盖完整触控/HUD。证据与回滚见同目录功能与风险评估“补修”章节及 outputs/dlevel88-draw-layer-20260915。

## 2026-09-15 填色背景排序

- 当前有效配置：物理Layer=Draw(14)，MeshRenderer Sorting Layer=Background、Order=2。上节保留SceneLayer是当时的修复范围，现按用户新要求调整显示顺序；两个层各管输入/物理与渲染，不可互换。
- 仅改区域资产的Renderer配置，不改树、脚本、显式引用、生命周期或重力所有权。当前D088实例无排序覆盖；用户最新位置/尺寸/颜色保持。
- 静态增量与Unity URP正式资产前景/场景组渲染对照通过。证据、未验证项和仅两行的回滚路径见功能与风险评估“填色置于背景层”及 outputs/dlevel88-background-20260915。

## 2026-09-15 重复贴图描边与新填色

- 原图导入 `Assets/Game/ArtSrc/Sprites/DrawLine/Sp_hx_x_01.png`。保持11×200原像素、原色、透明边，不旋转重采样；作为独立Texture使用，不进Sprite Atlas。沿V轴Repeat、U轴Clamp，不缩放NPOT、不压缩、不生成MipMap。
- 新增本目录 `DLevel088_Outline.shader/.mat`。LineRenderer的U沿边界长度增长，Shader交换UV以适配竖条；每2单位重复一张素材，不随整个周长拉长图片。描边保留原图颜色与Alpha，填色独立为 `#98A0FA`、Alpha0.5。
- 区域Prefab预置Outline子对象（GameObject/Transform/LineRenderer ID依次880880001/880880002/880880003），父脚本显式绑定 `regionOutline`；不在运行时按名称搜索或新增对象。无额外Collider，根/子物理Layer均Draw。填色Background/order2、描边同层order3，仍低于SceneLayer。
- `RefreshRuntimeShape` 把同一组轮廓顶点交给 `RefreshOutline`；闭合LineRenderer、本地坐标、TransformZ对齐，三个拐角细分点。只在启用/参数变更时更新，不给FixedUpdate增加每帧描边重建。停用组件和非法形状同步隐藏描边。
- Inspector新增“描边宽度”（默认0.11）、“描边重复长度”（默认2）、“描边渲染器”引用。编辑态仍使用原Handles辅助轮廓，实际重复材质在Play显示；没有新增ExecuteAlways、编辑态实例化、DB或存档链。
- 默认脚本/区域Prefab/填色材质与Shader的颜色同步更新；当前D088实例只改四个颜色通道，位置、尺寸、形状及其它地图参数保持。原重力所有权和画线阻挡方法保持，无共享脚本/配置表/多语言/UI/事件改动。
- 编译、56项描边验证和30项画线/重力回归通过；具体证据、未覆盖风险和回滚见本目录风险评估的“重复素材描边与配色”及 `outputs/dlevel88-outline-20260915/`。

## 2026-09-15 内部手绘平铺与宽度预览

- 新增 `Assets/Game/ArtSrc/Sprites/DrawLine/Sp_hx_x_02.png` 及meta（GUID a3713a89b8a34c0b9ad0e667915f5293），保持共享盘原图233×201、双轴Repeat、无NPOT缩放/压缩/MipMap。材质 `_MainTex` 显式绑定，不新增Renderer、Mesh、Collider、资源查找或运行时加载链。
- 填色Shader按顶点本地坐标/`patternTileSize`平铺，直接由既有形状网格裁切。区域增大只增加纹理重复次数，移动不滑动；Transform缩放/旋转会带动纹理。透明像素保留底色，按原图RGB/Alpha及`patternStrength`叠加笔触；不把纹理Alpha乘进整个区域Alpha。
- Inspector新增“内部纹理平铺尺寸”（默认2.33×2.01，越小越密）、“内部纹理强度”（默认4，范围0～8，0恢复纯色）。两项通过实例PropertyBlock传递，不修改共享材质实例。
- 用户截图中宽度为0.3，磁盘旧值0.01不能证明其当前Inspector值。按截图同步D088实例宽度0.3，保留用户后来保存的Alpha0.31764707和全部地图其它参数；共享Prefab默认宽0.11不批量改。
- 修正Scene辅助描边：按`OutlineWidth/2`绘制粗边和圆角，不再固定DrawLine细线。新增`RefreshRuntimePreview`只允许运行中的启用实例，在Inspector提交后立即刷新，所以Time.timeScale=0也无需等FixedUpdate；Prefab资产不进入运行时刷新。非运行态仍是辅助轮廓，内部实际纹理需Play查看，没有新增编辑态实例化。
- 重力查询、所有权、Collider和描边贴图算法保持。44项平铺/宽度验证、30项画线/重力回归、主工程编译通过；Scene辅助线实际编辑器操作和完整关卡/真机仍需补验，见风险评估“内部手绘平铺”及 outputs/dlevel88-pattern-20260915。

## 2026-09-15 最终纹理方案确认

按用户最新要求恢复 `Sp_hx_x_02.png` 直接双轴Repeat；生成图与试验性跨边混合不采用。Shader恢复单次纹理采样，材质/脚本/Inspector/两个Prefab均与试验前一致，不改变结构与配置。原图字节及恢复哈希验证、保留参数、风险和后续变更边界见功能与风险评估“最终选择”及 outputs/dlevel88-seamless-20260915/restored-validation.json。
