# DLevel_088 反重力区域：功能与风险评估

## 需求、原始基线与本次范围

2026-09-15 用户制作 D088，要求不再使用前面弹性系列的物理属性，新增可控制矩形/圆形/多边形范围和颜色的区域，物体进入后重力反向。默认离开区域后恢复原重力；不瞬间反转速度、不增加弹射。

接收的 D088 已使用普通画线模板和猫夹球组件，没有 D084～087 的限速、补弹、出生保护及专用材质，因此本次没有强行重新设置球/线的其它物理参数。保留用户原地图、15秒倒计时、20墨水、0/5/10星级、线质量200/按长度缩放和球默认物理参数。只给根新增区域实例并更换88标题。

任务开始时工程有大量既有未提交改动（包括 GF gitlink、字体、Actor、Gradle、资源和配置）；这些不属于本次。指向的 `specs/037-remote-resource-loading/plan.md` 不存在，本次以实际 Prefab、D002代码、D087结构记录、Draw规范和 Editor 约束建立局部基线，未改 RemoteConfig 或 GF。

## 受影响文件与数据/资源流

- 新增本目录 `DLevel88GravityZone.cs`、`DLevel88ZoneGeometry.cs`、`Editor/DLevel88GravityZoneEditor.cs`、区域材质/Shader及文档和meta。
- 新增 `Assets/Game/Prefabs/DrawLevel/Shared/DLevel088_ReverseGravityZone.prefab`，接入现有 `DLevel_088.prefab`。
- 本地 LevelData/LevelSort 各新增一行，20088、排序63；仅同步两份 Draw JSON。本地 I2 新增88标题12种翻译，不写云端翻译源。
- 参数由区域 Inspector 显式序列化；几何同时生成触发器与可见填色，Mesh和颜色属性块由实例持有。只使用 Unity Rigidbody2D.gravityScale，不引入新的物理材质。
- 游戏UI、成功/失败、解锁、存档、打点、转场及平台SDK仍由原组件负责，无新增全局事件或共享脚本修改。
- 完整树、引用ID和配置方法见 `DLevel_088脚本结构记录.md`。

## 行为与边界

- 以任一非 Trigger 碰撞体接触区域为进入，不要求刚体中心进入；多个子 Collider 只算一个刚体。
- 首次进入取原 gravityScale 的负值，重叠区域不再次翻转；离开最后一区域才恢复。重力大小、质量、速度和角速度不变，物体先减速再转向。
- 静态地面、Kinematic、未模拟刚体、Trigger和配置层外碰撞体不受影响；首笔转 Dynamic 后会被下一次物理扫描纳入。
- 区域停止/销毁立即恢复自己持有的对象，目标禁用/离开/销毁在下一物理步清理。其它机制若主动设置不同重力，退出不覆盖其新值。
- 矩形最小边长0.05，圆形最小半径0.05；圆形64边近似，非等比Transform缩放会变椭圆。多边形3～64点、顺逆时针均可、可凹不可自交，不支持带孔轮廓。
- 编辑态不创建运行时对象或Mesh；Scene开启Gizmos后有色块预览，多边形可拖点。Game中只在运行时显示。直接编辑底层Collider不会成为脚本形状源，应编辑区域脚本参数。

## 验证证据

证据目录：`E:/WorkSpace/outputs/dlevel88-gravity-20260915/`。

1. `static-validation.json`：D088原有YAML对象逐块保持，只改标题和新增区域；新旧prefab内部引用、GUID唯一性和外部资源可定位；10个前关/共享核心文件SHA256不变；没有弹性系列组件/材质引用混入。
2. `workbook-validation.json`：两表仅新增D088；旧数据、类型、样式和其它ZIP/XML部件完全保持。Artifact Tool编辑前后渲染检查、错误扫描通过；Luban实际验证/生成成功，两份运行时JSON逐项一致。
3. `compile-final.log`：主工程当前Assembly-CSharp响应文件加本关新源码、Unity6000.0.59f2 Roslyn完整运行时编译0错误、113项现有警告。本关不再使用已弃用OverlapCollider API。编辑器脚本在隔离Unity导入编译通过。
4. `physics-results.txt`、`unity-physics-final.log`：使用生产脚本副本及真PhysicsScene2D，不用物理桩；覆盖矩形进入/离开、真实速度由下转上、复合Collider、重叠区域、逐个停用、重新启用、Kinematic转Dynamic、关闭模拟、Collider停用、0/负重力、对象销毁、Trigger过滤、图层过滤、圆形边界、最小圆、多边形凹口/反向顶点/非法轮廓、移动区域、20个子Collider的画线及正式Prefab实例生命周期。
5. `render-results.txt`、`three-shapes.png`：Unity6000.0.59f2、URP17.0.4 Renderer2D，用正式Prefab/材质/Shader运行时生成三种形状并读取渲染图，色块与凹口可见，本Shader无编译错误。测试用不同色便于区分；正式默认仍是蓝色Alpha0.28。隔离项目首次导入其它URP材质有警告，不代表主项目零警告。

## 风险与未验证项

| 风险 | 控制/证据 | 尚待验证 |
| --- | --- | --- |
| 覆盖用户地图或旧系列行为 | 原节点逐块比对、10个保护文件哈希不变、不改共享脚本 | 用户后续编辑后应重新核对 |
| 复合碰撞体、多区域导致双重反转或恢复错误 | 刚体去重+所有权计数，真实物理测试 | 多机关同时主动改gravityScale的未来组合 |
| 凹多边形渲染和触发不一致 | 同一顶点源、耳切剖分、凹口物理和渲染检查 | 极薄/巨大坐标多边形应避免 |
| 编辑态初始化管理器/生成实例 | 无ExecuteAlways，OnValidate仅本地字段，Handles预览不生成Mesh | 主工程Prefab Stage人工编辑/Undo体验 |
| 移动端性能和资源变体 | Mesh仅初始化/改参数重建，FixedUpdate复用集合，不扫描整个关卡；目标60fps | Android/iOS实机帧耗、GC、Shader变体和触控 |
| 连续高速越过极薄区域 | 每物理步做实际Overlap，不改变速度 | 没有做扫掠检测；区域应有足够厚度，至少覆盖物体约1～2物理步位移 |
| 场景难度与完整生命周期 | 原D002、门、出界、倒计时链保留 | 完整进入/提示画法/失败/重开/暂停/通关/下一关/退出/解锁/打点未完整试玩，为uncertain |

区域初始位置 `(0,-0.5)`、矩形 `6×3` 是便于编辑的起始值，不宣称已有唯一可通关解法。没有修改提示线的用户摆放；如果新机制需要对应提示路线，应在确认最终区域范围后再配置。

## 回滚

修改前D088、I2、两表、JSON和中央文档快照保存在证据目录。回滚仅移除本次新增区域实例/资源与88词条、20088和排序63并重新生成Draw JSON。不要用整份旧工作区快照覆盖后续用户地图、表格或文档编辑，不改GF或前关资源。无提交、推送、云控发布或平台配置改动。

## 2026-09-15 补修：区域内不能画线

### 根因、范围与行为

用户反馈区域内无法起笔。实际 DrawLineFeature 的阻挡过滤器明确使用 useTriggers=true，并只排除 Draw 层；区域 Prefab 原物理 Layer=Default(0)，所以区域 Trigger 被当成画线障碍。此前30项物理测试覆盖重力/形状，没有覆盖画线输入门禁，属于测试缺口，不能作为区域内可画线的证据。

本次仅将 DLevel088_ReverseGravityZone.prefab 根 GameObject 的 m_Layer 从0改为14（项目Draw层）。Collider的Trigger、形状、颜色、材质、所有重力代码、公共DrawLineFeature和全局Physics2D不变。注意这是物体的物理Layer，不是Renderer的Sorting Layer；原SceneLayer/order2保持。

DLevel_088当前区域实例没有物体Layer覆盖，直接继承修复。用户已将位置改为(7.56,0.05)、矩形尺寸改为12.15×12.45，本次不编辑关卡Prefab，完整保留这些调整。当前Prefab引用扫描仅DLevel_088使用该区域资产。

### 验证、剩余风险与回滚

证据/修改前快照：E:/WorkSpace/outputs/dlevel88-draw-layer-20260915/。验证脚本从当前DrawLineFeature原文摘取 IsBlockedAt、IsBlockedBetween、GetWorldBlockingRadius 和阻挡过滤器，保留判定逻辑，在隔离Unity的真Physics2D中对照旧Default层与新Draw层，并使用正式区域Prefab/生产重力脚本。覆盖三种形状的区域内起笔、画入、画出、跨越、反转/恢复以及普通实体墙和非Draw Trigger仍被阻挡。

Unity6000.0.59f2隔离验证30项全部通过，旧Default层能复现问题，新Draw层解除区域阻挡，反重力/离区恢复和普通障碍规则保持；测试脚本编译无错误。最终静态结果见validation.json。

这不等于完整鼠标/触控、HUD、整关解法或手机自动化；这些仍为uncertain。静态核对要求仅上述一行物理Layer变化，关卡地图和受保护脚本/配置哈希不变。回滚只把区域Prefab的m_Layer改回0（会再次产生不能画线的问题），不覆盖用户当前地图，不恢复整套旧资源。

## 2026-09-15 表现调整：填色置于背景层

- 需求与基线：用户要求色块放到背景。原区域 MeshRenderer 为 SceneLayer/order2，晚于标题 SceneLayer/order1 和部分场景组，导致前景被区域颜色覆盖。
- 修改仅涉及 `Shared/DLevel088_ReverseGravityZone.prefab` 的 Renderer 排序层：改为 Background（ID944539463、索引1），order2不变。位于 Background/order1 的背景上方、SceneLayer 前景下方。根 GameObject 物理层仍为 Draw(14)，Trigger、重力/画线代码和所有物理规则不变；没有新的资源、数据、UI、事件或平台流程。
- 本次读取到用户区域位置(6.59,0.05)、矩形12.4×12.45、颜色RGBA(0.42745098,0.81278527,0.972549,0.54509807)。DLevel_088没有覆盖区域的排序/物理层，直接继承资产；本次不编辑地图，完整保留这些参数。
- 验证：`E:/WorkSpace/outputs/dlevel88-background-20260915/verify.mjs` 与 `validation.json` 确认资产仅两行排序配置变化、Draw/Trigger/order保留、地图完全不变。引用扫描只有D088引用此资产。
- 渲染证据：同目录 `render-results.txt`、`background-sorting.png`、`old-sorting-control.png`。Unity6000.0.59f2/URP17.0.4隔离Play验证使用正式Prefab、材质、Shader；Background/order1纸背景、SceneLayer/order1前景及SceneLayer/order-1的SortingGroup。新排序下区域可见、两类前景保持白色；旧SceneLayer/order2对照复现两类前景染色，全部断言通过。
- 未验证：对照使用标记Sprite，不是整关截图；主工程当前画面、完整触控和Android/iOS仍需试玩确认。本次没有再次运行上一节的完整物理测试，物理相关文件/字段未改。
- 回滚：仅把区域Renderer的ID/索引改回1752186269/2；不要改物理Draw层，不要用旧整张地图覆盖用户编辑。修改前快照与验证脚本同目录保存。

## 2026-09-15 重复素材描边与配色

### 需求、基线与范围

用户提供共享盘 `\\192.168.1.5\DW-ArtSource\BrainPunk_001\原画\9月\画线\Sp_hx_x_01.png`，要求导入并沿整个区域重复描边，同时把内部区域改为#98A0FA、透明度50%。原运行时只有填色Mesh，没有贴图描边；物理Draw层和背景渲染层继续保留。

影响限于D088：原图导入DrawLine美术目录及meta，新增Outline材质/Shader及meta，修改区域脚本、Inspector、区域Prefab、填色材质/Shader默认色和D088实例四个颜色值。区域预置Outline子节点并显式绑定，不修改已验收关卡、公共DrawLineFeature、GF、Physics2D、配置表、I2、存档、事件、打点或SDK。

### 实现与配置

- 原图SHA256为 `4c1530fb76dc3f6327b977a994a7f75cf1cdc408d9fdaa92600c6ae1b18255e5`，工程副本完全一致。Texture保留11×200，V Repeat/U Clamp、NPOT不缩放、无压缩无MipMap，避免窄边被压缩或图集采样干扰。
- LineRenderer使用与填色/触发器相同的轮廓点，闭合、按长度Tile；Shader交换UV以直接使用竖条原图。宽度默认0.11、重复长度2，对应100PPU，可在Inspector调整。不会把图片拉长至整圈。描边保持素材原色，不乘内部填色Alpha。
- 描边只负责显示，位于Background/order3，填色order2；描边居中于原边界，不扩大触发范围。轮廓位置/转角随区域移动，矩形/圆形/简单凹多边形均走同源点列，非法轮廓与停用状态同步关闭两种显示。
- 颜色在脚本默认值、资产默认值、材质/Shader及当前实例全部对应RGBA(152/255,160/255,250/255,0.5)。用户的地图位置和尺寸没有重写。
- 性能增量为每个区域一个预置LineRenderer和小纹理，初始化/参数改变时更新；每帧无新增描边重建、无新的托管轮询分配。现有重力查询保持，未宣称真机帧率通过。

### 验证证据与未覆盖项

证据和修改前快照：`E:/WorkSpace/outputs/dlevel88-outline-20260915/`。

1. `validation.json` / `verify.mjs`：原图字节一致、关卡Prefab只改RGBA四值、原触发器块完全不变、物理/形状方法原文不变、显式描边绑定及Draw/Background层保留。画线测试摘取的阻挡方法仍与当前生产源码一致。
2. `compile.log`：使用主工程当前Assembly-CSharp响应文件与Unity6000.0.59f2 Roslyn编译，0错误、113项现有警告；运行时程序集输出仅在证据目录。Inspector和两Shader在隔离Unity实际导入编译通过。
3. `render-results.txt`、`unity-outline.log`、`outline-three-shapes.png`：正式Prefab/纹理/材质/Shader在URP17.0.4 Renderer2D Play验证，56项断言通过。覆盖原图导入设置、三形状同源边界、闭合、真实BakeMesh UV跨度=周长/2、重复长度减半次数翻倍、宽度调整、停用/重新启用、非法轮廓、颜色和排序、14处边界实际像素取样；图像人工查看确认矩形/圆形/凹角连续描边。非法多边形测试会产生预期Warning；退出隔离相机有RenderTexture清理Warning，不作主工程零警告声明。
4. `draw-results.txt` / `unity-draw.log`：正式新增描边Prefab重新运行30项真实Physics2D画线阻挡与反重力回归，全部通过，覆盖区域内起笔/画入/画出/跨区、进入反重力/离区恢复、普通墙和普通Trigger仍阻挡。不是完整触控端到端测试。

未覆盖：主工程完整关卡Play、Android/iOS构建/NPOT纹理采样和实机性能仍需补验；极尖细、自交或宽度大于局部间距的轮廓可能造成描边重叠，应保持合理宽度。Scene非运行态仍是原辅助轮廓，不显示实际重复纹理。完整通关、平台/打点回归不在本次局部修改验证范围。

### 回滚

只撤销本次Outline子对象/序列化引用、描边显示代码/Inspector字段、新描边材质Shader和导入素材，并恢复本次之前的区域颜色；不要改变Draw物理层和Background排序。D088地图只逆向改四个颜色通道，不用整张快照覆盖用户后续编辑。新增素材删除前应再次检查引用；未执行提交、推送或云端发布。

## 2026-09-15 内部手绘平铺、0.3描边与调参刷新

### 需求、基线与边界

用户提供Sp_hx_x_02，希望整个区域内部平铺手绘画线效果；并用Inspector截图澄清描边宽度是0.3。此前内部为纯色，Scene预览固定画细线；运行时参数通过OnValidate标脏后等待FixedUpdate，暂停时不会自动刷新。先前仅依据磁盘0.01回答当前Inspector状态证据不足，不能据此断言用户没有设置0.3。

本次只导入新图、扩展D088填色Shader/材质/区域参数和Inspector预览刷新，区域Prefab新增两项平铺参数；D088实例只把旧宽度同步为用户截图的0.3。用户最新Alpha0.31764707、RGB、位置(6.59,0.05)、尺寸12.4×12.45及其它地图配置保留。Draw物理层、Background填色order2/描边order3保持，不动公共画线/旧关/GF/表格/多语言/打点/存档/SDK。

### 素材与表现

- 原图 `\\192.168.1.5\DW-ArtSource\BrainPunk_001\原画\9月\画线\Sp_hx_x_02.png` 导入 `Assets/Game/ArtSrc/Sprites/DrawLine/`，SHA256为57815f782e59b56b6d5a7f1d5278fbd7cb5a815d05c08e113d6160a674de774b，原始233×201不重采样。素材RGB基本恒定，手绘线信息在Alpha中，最高33/255，约13%。
- 双轴Repeat；按本地2.33×2.01平铺，尺寸可调。原图Alpha乘纹理强度（默认4）仅用于底色与原图颜色之间混合，最终输出Alpha仍完全由区域颜色控制，避免整块透明度被低Alpha纹理再次相乘。强度0恢复原纯色。透明底与手绘笔触都受原网格形状裁切，不额外建立图块对象。
- Scene辅助边线接入真实宽度，圆角也随宽度变化；编辑态不创建Mesh或预览对象。内部贴图仍在运行画面查看。Inspector修改后调用带`Application.IsPlaying(gameObject)`和启用检查的公开刷新入口，运行暂停时立即更新，不主动推进任何物理步。
- 每区域仍是原1个填色MeshRenderer与1个描边LineRenderer；填色Shader新增一次纹理采样，没有逐帧几何重建或新增物理查询。参数仍显式序列化、材质纹理显式绑定，颜色/平铺控制通过实例PropertyBlock传递。

### 验证与风险

证据/修改前快照：`E:/WorkSpace/outputs/dlevel88-pattern-20260915/`。

1. `validation.json`：原图字节一致、图尺寸保留、D088只有宽度值改为0.3，最新透明度和其它地图数据完全保持；区域资产只有两项平铺字段变化。FixedUpdate、重力所有权、形状、触发器编辑方法及原描边算法逐方法不变。Scene宽度属性已接入、运行刷新保护存在。
2. `render-results.txt`、`unity-pattern.log`：Unity6000.0.59f2/URP17.0.4正式资产隔离Play，44项断言通过。矩形/圆形/凹多边形均验证Time.timeScale=0时走SerializedObject提交及Inspector使用的公开刷新入口，宽度0.1→0.3后真实BakeMesh外接宽增加0.2；纹理/层级绑定、颜色Alpha保持、纹理XY周期像素匹配、凹口无越界、Prefab资产不会被运行时入口修改均通过。
3. `pattern-three-shapes.png`、`plain-thin-control.png`：正式纹理/材质的图像对照，3186处内部采样出现笔触差异，且宽0.3相对0.1的实际描边像素不同；人工查看可见重复斜线，证明不只是字段赋值。
4. `draw-results.txt`：三种形状的30项真实Physics2D画线阻挡与重力反转/恢复回归全部通过，普通墙/Trigger阻挡规则保持。
5. `compile.log` / `compile-errors.log`：主工程当前Assembly-CSharp编译0错误、113项已有警告，输出只存证据目录。Inspector在隔离Unity导入编译通过，Shader无编译错误。

剩余风险：不能确认用户此前查看的是未运行Scene、运行暂停还是另一个实例；本次覆盖已确认的预览缺口和暂停刷新路径，不把推测当原现场复现。Scene实际GUI操作/Undo、完整主工程关卡、移动端NPOT采样/性能未实机验收。原图不是无缝纹理，平铺连接处会保留原图笔触断续感，未擅自编辑美术源图。纹理强度与平铺尺寸可继续人工调节；极细/巨大缩放仍需实机观察锯齿和密度。

### 回滚

只逆向撤销本次填色Shader/材质纹理引用、平铺字段、Inspector宽度辅助预览与公开实时刷新入口，按需移除新导入图及meta；不要移除上一轮已验收的重复描边。关卡宽度0.3是用户明确值，不应因回滚自动变回磁盘旧0.01；保留其最新透明度和地图。无提交、推送或远端改动。

## 2026-09-15 最终选择：恢复 Sp_hx_x_02 原图直接平铺

- 用户在无缝手绘生成试验后明确要求恢复所附 `Sp_hx_x_02.png` 方案。生成图不采用；临时加入 D088 填色 Shader 的跨边混合已完整撤回。最终仍为原图双轴 Repeat、原单次纹理采样和原 Alpha 混色方式，不承诺原图边缘无缝。
- 材质始终绑定原图 GUID `a3713a89b8a34c0b9ad0e667915f5293`；生成图只在证据/隔离测试目录，未进入生产美术目录。原图工程副本与本次共享盘文件 SHA256 相同：`57815f782e59b56b6d5a7f1d5278fbd7cb5a815d05c08e113d6160a674de774b`。
- `DLevel088_Region.shader/.mat`、区域脚本、Inspector、D088地图及共享区域Prefab共六个文件与试验前快照逐字节一致。描边0.3、用户当前Alpha0.31764707、颜色、尺寸、位置、Background排序、Draw物理层保持；不改重力/画线、事件、数据、资源加载、UI、存档或平台流程。
- 静态恢复验证：`E:/WorkSpace/outputs/dlevel88-seamless-20260915/verify-restored.mjs` 与 `restored-validation.json`。此前原图方案的44项渲染/30项物理测试证据仍见上一节；本次未重新运行整关或真机，不把已弃用的生成图渲染试验作为当前方案验证。
- 回滚/继续：本节是用户指定的最终基线。后续若需改纹理须重新按新要求实施，不能自动重新接回候选生成图或跨边混合代码。保留旧原图和所有用户地图修改，无提交或发布。
