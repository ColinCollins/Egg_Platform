# ILevel Group01～14 五主题背景功能与风险记录

> 当前状态（2026-09-09）：2026-09-08 的关卡美术调整重新分配了部分五关组的主题，当前映射以本文表格和各 Prefab 实际启用的主题根为准。本次全量颜色审计已把 70 个正式关卡的 `colorSettings` 同步到当前主题；下文标注为历史验证的范围不再代表当前主题分组。

## 需求与范围

- 日期：2026-09-07。
- 依据用户提供的四张参考图，结合已验收的第一组夕照方案，为 IWanner 的 14 个 Group 整理五套共享配色与分层 Mesh 背景。
- 同主题关卡继续共用一套 Mesh、Material 与 `IWannerLevelColorSettings`；当前主题按五关组分配，具体范围见下表。
- 每个关卡保留 4 个一级语义层，关卡差异由背景根与一级层的位移、镜像和缩放表达，不复制整张大图。需要单独调整的不同物件在语义层下使用独立 Mesh 子节点。
- 旧第六关 11 层工厂试作因复杂度过高已移除；第一关旧单关夕照资源已整理为正式共享主题。
- 本次仅修改背景表现和 `IWannerLevelColorSettings` 引用，没有新增运行时脚本、Collider、事件、存档、打点或资源加载入口。

## Group 与主题映射

| Group | 关卡 | 主题 | 参考来源 |
| --- | --- | --- | --- |
| Group01、05、08 | ILevel_001～005、021～025、036～040 | IndustrialMint | `C:/Users/admin/Desktop/20260907-154812.png` |
| Group02、06、09 | ILevel_006～010、026～030、041～045 | DesertAqueduct | `C:/Users/admin/Desktop/20260907-154809.png` |
| Group04、07 | ILevel_016～020、031～035 | JungleTemple | `C:/Users/admin/Desktop/20260907-154759.png` |
| Group03、10～12 | ILevel_011～015、046～060 | MoonCastle | `C:/Users/admin/Desktop/20260907-154804.png` |
| Group13～14 | ILevel_061～070 | SunsetMountains | 第一关第一组暖橙夕照方案 |

## 资源结构与配色

- 共享资源统一放在 `Assets/Game/Prefabs/WannerLevel/ILevel_Theme_Backgrounds/`。每个主题在 `Materials/` 目录下按 4 个一级语义层配置 4 个 Material，总计 20 个 Material；全部共用 `IWanner背景材质渐变.shader`。一级语义层保留为构图父节点，不同物件使用独立 Mesh 子节点。
- IndustrialMint：共 7 个 Mesh、193 顶点。一级层为 Sky、FarStructures、ProcessTanks、FactoryFrames；其中 ProcessTanks 下拆为 ProcessTankLeft／ProcessTankRight，FactoryFrames 下拆为 FactoryFrameLeft／FactoryFrameRight／FactoryFrameBars。Ground／Skip `#1C2327`，相机 `#95BFA7`，HUD `#D7E4D8`。管道采用两级口径：连续主管全程宽 0.5，三个转角使用同半径圆角；两只罐体颈管统一宽 0.64，作为接入容器前的一级放大。没有连接关系的独立立管已删除。
- DesertAqueduct：共 8 个 Mesh 资源、每关 13 个 Renderer、597 个可见顶点。左右远城均保留完整 Mesh；“两侧立柱＋上方拱顶”构成一个完整拱单元，左侧水道共用 `AqueductArchPillarUnitLeft` 4 次，右侧共用 `AqueductArchPillarUnitRight` 3 次，两侧 `MiddleBar` 水平长条分别独立；Ground／Skip `#2F201E`，相机 `#F6D3A1`，HUD `#FFF0D1`。
- JungleTemple：共 11 个 Mesh、448 顶点。DistantCanopy 拆为左右远树冠，Temple 拆为主体／门洞，PalmsAndCanopy 拆为四棵棕榈和左右近树冠；Ground／Skip `#162724`，相机 `#CDD68D`，HUD `#F1F0B5`。
- MoonCastle：共 9 个 Mesh、256 顶点。DistantCity 拆为城墙底座和左右远城，TowersAndBanners 拆为四座独立塔楼；Ground／Skip `#2F1F41`，相机 `#C8AADD`，HUD `#F2E5CF`。
- SunsetMountains：共 8 个 Mesh 资源、每关 8 个 Renderer、93 个可见顶点。左右台地均保留完整 Mesh；DistantMountains 仍包含地平线、中央峰和左右台地，NearRidges 保持左右近山；Ground／Skip `#382321`，相机 `#F6AD74`，HUD `#FBD5A0`。
- 五套主题不包含大型位图。背景 Mesh 的顶点色保留原有明暗关系，日常换色由 Material 的“暗部颜色／亮部颜色”完成；Shader 根据顶点色亮度在这两种颜色之间插值。Mesh 不再承担主题色编辑入口。
- 五个配色资产仍保留首次整理时的 `ILevel_GroupXXToYY_<Theme>_ColorSettings.asset` 文件名和 GUID；后续主题重排后，文件名中的旧 Group 范围不再等于当前使用范围，实际归属以主题名和上表为准。背景颜色由主题语义层 Material 管理；修改共享 Material 或配色资产会影响当前绑定该主题的全部关卡。
- 每关背景根以该关 Camera 为中心，整体缩放使用 `orthographicSize / 5`，使 size=4～7 的关卡保持接近参考图的视觉占比。每个五关小组使用不同的层位移、镜像或轻微缩放，保持同主题内的构图差异。

## 旧资源迁移与删除

- 第一关原 `ILevel_001_Sunset` 中的分散 Mesh、材质和 Shader 已合并为 `ILevel_Theme_Backgrounds/SunsetMountains` 的 4 层资源；夕照主题当前移动到 Group13～14。
- 第六关旧 `FactoryBackground`、`ILevel_006_Factory` 目录和 `ILevel_006_Factory_ColorSettings.asset` 已删除。2026-09-08 关卡美术调整后，第六关当前使用 DesertAqueduct 共享方案。
- 删除前后均通过 Unity AssetDatabase 检查旧路径与旧资源引用。当前验收结果中 `oldFactoryRemoved=true`、`oldSunsetConsolidated=true`。
- 原来的五个 `*VertexColor.mat` 已从正式主题目录移除。其中四个没有剩余引用并已删除；`JungleTempleVertexColor.mat` 仅被非正式副本 `ILevel_048 1.prefab` 引用，因此保留在 `LegacyVertexColorMaterials/`，不参与 ILevel_001～070 正式关卡。
- 原 `IWanner背景顶点色.shader` 原位重命名为 `IWanner背景材质渐变.shader`，保留 `.meta` GUID，旧材质和 Prefab 不会因路径调整丢失 Shader 引用。
- 早期拆分时移除的合并 Mesh 中，JungleTemple 的 DistantCanopy／Temple／PalmsAndCanopy 因非正式副本 `ILevel_048 1.prefab` 仍引用而移动到 `LegacyCombinedMeshes/JungleTemple/` 保留 GUID。`DistantCityLeft`、`DistantMesaLeft`、`DistantMesaRight` 已按原 GUID 与原文件恢复；左右水道原合并 Mesh 最终替换为完整拱单元与水平横杠。

## 数据流与隔离边界

- Prefab 直接引用主题 Mesh、语义层 Material 和配色；背景节点仅包含 Transform、MeshFilter、MeshRenderer，排序为 -120～-117，Z=5。
- Ground、Trap、相机和 HUD 继续使用原 `IWannerLevelCtrl` 配色链。预览时只在未保存的 Prefab 副本上把 Ground／Triangle／Trap／Spring 临时着色，用于接近运行时显示。
- 主题共用一个无光照材质渐变 Shader。Material 保存可直接调整的暗色和亮色，Mesh 顶点色只提供 0～1 明暗权重；不使用大尺寸背景纹理。
- 同主题的正式关卡共用 4 个 Material。需要单关独立改色时，应复制该关实际使用的 4 个 Material 并只改绑该 Prefab，不能直接修改共享主题 Material。
- Material 迁移时 ILevel_050 正在 Prefab Stage 中编辑，迁移脚本直接在当前 Stage 内容上改绑并保存，没有用磁盘旧副本覆盖用户编辑。
- IndustrialMint 的两个旧合并 Mesh `ProcessTanks.asset`、`FactoryFrames.asset` 已拆成 5 个独立物件 Mesh 并删除。15 个关卡继续通过原一级层 Transform 控制整体位置，子物件可以单独移动、缩放、停用或另绑 Material。
- 用户截图明确的最终编辑边界是：红框中的“两侧立柱＋上方拱顶”必须保持为一个完整小结构，单独拆出并重复使用；穿过中间的水平长条不属于该单元，继续作为 `MiddleBar` 独立。AqueductLeft／Right 仅作为整座水道父节点，统一控制全部完整拱单元和横杠。
- 左右旧水道合并 Mesh、错误中间态 `AqueductBody*`、`AqueductPillarsBody*` 与 7 个独立弧顶资源均已删除。正式水道资源只有 `AqueductArchPillarUnitLeft`、`AqueductArchPillarUnitRight` 和左右 `AqueductMiddleBar`。左右单元因立柱高度和拱形比例不同而各保留一个精确 Mesh，各自在同侧复用。

## 验证证据

- 2026-09-07 的历史结构专项验证曾覆盖当时映射下的 ILevel_016～030 与 ILevel_061～070：DesertAqueduct／SunsetMountains 每关背景 MeshRenderer 分别为 13／8；该条只作为 Mesh 结构证据，不再代表 2026-09-08 调整后的主题范围。
- 14 个 Group 分别检查背景子层 Transform 指纹，每个 Group 都有 5 个不同构图，对应组内 5 个小关；跨 Group 仍复用相同 Mesh 和材质。
- 当前按 IndustrialMint／DesertAqueduct／JungleTemple／MoonCastle／SunsetMountains 顺序，覆盖数量为 15／15／10／20／10；20 个 Material 均启用材质配色并引用 `BrainPunk/IWanner/ThemeMaterialGradient`，Shader 编译诊断为 0。
- Material 迁移前后 YAML 块逐字审计覆盖 69 关，共保持 793 个 BoxCollider2D、1525 个 MonoBehaviour 和 789 个 PrefabInstance 不变。ILevel_050 因迁移时处于 Prefab Stage 且保存了用户当前编辑内容，不做这次逐字对比，只参与背景专项检查。
- 使用各 Prefab 自身 Camera 在隔离 SceneCullingMask 中完成 16:9 代表关卡对照和五套主题的 20:9 检查。最终对照图为 `DevelopMD/ILevel_Group01-14_五主题背景对照_16x9.png`。
- 薄荷工业管径修正后的 20:9 专项预览为 `DevelopMD/ILevel_Group01-03_薄荷工业管径调整_20x9.png`，用于检查主管连续性、弯头半径和罐体颈管比例。
- IndustrialMint 拆分前后总顶点数保持 193。以拆分前即时备份为基线，ILevel_001～015 的 133 个 BoxCollider2D、238 个 MonoBehaviour 和 113 个 PrefabInstance YAML 块逐字一致；16:9／20:9 隔离相机复验未发现背景外观变化。
- 其余四主题拆分前后总顶点数分别保持 597／448／256／93。ILevel_016～070 中除当前 Prefab Stage 的 ILevel_050 外，54 关的 660 个 BoxCollider2D、1287 个 MonoBehaviour 和 676 个 PrefabInstance YAML 块逐字一致。ILevel_050 在拆分期间由用户把 `waitDuration` 从 4 调整为 5，本轮保留该改动，只验证其 9 个背景 Renderer 和资源引用。
- 最终修改前后 25 关的 305 个 BoxCollider2D、631 个 MonoBehaviour、306 个 PrefabInstance YAML 块逐字一致。相对原合并水道结构，每关新增 9 个背景子节点并移除左右 Aqueduct 父节点上的 MeshFilter／MeshRenderer；15 关合计新增 135 个 GameObject／Transform、净增 105 个 MeshFilter／MeshRenderer。DesertAqueduct／SunsetMountains 每关可见顶点总数仍为 597／93。
- 左右水道旧合并 Mesh 与最终完整 ArchPillarUnit 重复实例＋MiddleBar 的三角形位置在 0.001 世界单位精度一致，顶点颜色逐值一致；`DistantCityLeft`、`DistantMesaLeft`、`DistantMesaRight` 与拆分前备份文件逐字节一致。背景外观因此保持不变，预览仍使用 `DevelopMD/ILevel_016_水道细拆验证_16x9.png` 与 `DevelopMD/ILevel_061_台地复用验证_16x9.png`。
- 本轮没有修改 C# 运行时代码；Unity 资产解析和 Shader 诊断可作为资源层验证，不能代替 PlayMode 或真机验证。

## 风险与未验证项

- `uncertain`：尚未完成 PlayMode 的进入、死亡、Reset、Replay、切关和 HUD 最终颜色验证。
- `uncertain`：尚未在 Android／iOS 横屏和 YooAsset 构建中验证 Mesh、材质及 Shader 是否完整收集。
- DesertAqueduct 每关由 7 个完整拱单元与左右 `MiddleBar` 表达水道，背景 Renderer 由 6 增至 13；SunsetMountains 保持 8。新增节点继续使用原 Aqueduct Material；Draw Call、SRP Batcher、GPU 时间与内存尚未设备实测。
- 自定义 Shader 仍是必要依赖：它只负责把 Mesh 既有的明暗权重映射到 Material 的暗／亮颜色。若以后改用纯色标准材质，可以移除这层插值，但会失去当前层内渐变。
- 极窄地形开口会遮挡较多背景，这是保留原关卡地形后的实际构图结果；以后改变相机尺寸或地形时应重新渲染目标关卡。

## 回滚

- 单关回滚：只恢复该关背景 MeshRenderer 的材质引用；若连主题也回滚，再移除该关主题根并恢复原 `colorSettings`。不要覆盖整个 Prefab，以免撤销同时发生的玩法编辑。
- 单主题回滚：恢复对应范围 Prefab 的材质引用，再确认 `Materials/` 内资源无引用后删除。Material 迁移前资源备份位于 `Temp/ILevelThemeBackgrounds-before-layer-materials/`。
- IndustrialMint 部件拆分回滚：以 `Temp/ILevel001To015-before-industrial-part-split/` 为基线，只恢复 ProcessTanks／FactoryFrames 背景块和两个原 Mesh；不要整份覆盖关卡 Prefab。
- 其余主题部件拆分回滚：以 `Temp/ILevel016To070-before-background-part-split/` 为基线定向恢复对应背景块和旧合并 Mesh。ILevel_050 必须保留用户当前 `waitDuration=5`，不能用备份整份覆盖。
- 水道横杠分离回滚：以 `Temp/AqueductCityMesa-before-small-part-split/` 为基线，定向恢复 ILevel_016～030 的 AqueductLeft／AqueductRight 和原左右水道 Mesh；远城与台地当前已与该基线一致。不要整份覆盖关卡 Prefab，以免撤销并行玩法编辑。
- 完整拱单元复用回滚：以 `Temp/Aqueduct-before-arch-unit-reuse-correction/` 为基线，可恢复截图纠正前的错误“立柱主体＋独立弧顶”结构；若要回到 Body＋MiddleBar，则使用 `Temp/Aqueduct-before-arc-unit-split/`。两者都只定向恢复 Aqueduct 背景块和对应 Mesh，不整份覆盖关卡 Prefab。
- 全量回滚可参考 `Temp/ILevel001To060-before-theme-batch/` 和 `Temp/ILevel001To070-before-five-theme-remap/`，但第 47 关必须按背景块定向回滚，不能用备份覆盖当前玩法内容。
