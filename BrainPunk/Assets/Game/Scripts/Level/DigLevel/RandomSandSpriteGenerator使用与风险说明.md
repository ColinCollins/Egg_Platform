# RandomSandSpriteGenerator 使用与风险说明

## 需求与职责

用户要求在限制区域内，从一组 Sprite 随机生成指定数量的沙土装饰，并随 Sand 挖洞一起擦除。采用当前关卡 Prefab 的编辑器制作方式：Odin Generate 保存随机布局，运行时复用已生成节点。没有每次进关重新随机、运行时生成节点、碰撞/耗墨/存档或全局事件功能。

此前 Sand 仅渲染自身 Sprite，SceneSpriteBrushClear 的双 RenderTexture 每章交换；直接给装饰套旧 MaskRTSprite 会把整张 Sand 遮罩映射到每张装饰自身UV，洞口位置错误。新装饰 Shader 用世界位置换算 Sand 本地UV，装饰原纹理仍使用自己的 Sprite/图集UV，从而部分擦到时只消失相应像素。对象和Renderer保持在场景中，不按命中整张删除。

## Inspector 制作步骤

1. 在目标关卡 Prefab Mode 的 Sand 节点上添加 `RandomSandSpriteGenerator`；也可放在独立的区域节点上。
2. 显式绑定 `Sand Sprite`（Sand 的 SpriteRenderer）与 `Sand Visual`（同一节点的 SceneSpriteBrushClear）。没有运行时层级/名称查找。
3. 把装饰图填入 `Sprites`，设置 `Count` 和 `Region`。Region 是 Generator 本地矩形；挂在单位Sprite的Sand上时默认 `(-0.5,-0.5,1,1)` 覆盖整个Sand。Scene选中组件所在节点时显示青色框。
4. `Sprite Scale` 按原始PPU尺寸提供世界缩放，生成时抵消父节点的横纵缩放；较大图不能完整放入区域时，在清理旧布局前报错。非中心pivot也算入边界。父节点尺寸/比例改变后重新 Generate 可恢复全图限制和世界比例。
5. `Sorting Order Offset` 默认1，使用Sand同一Sorting Layer，放在沙土上方；需确保角色的排序高于装饰。`Decoration Material` 可以留空，Generate 按材质GUID自动绑定默认资源，当前路径为 `Assets/Game/ArtSrc/Sprites/Dig/DigSandDecoration.mat`。保留meta移动/重命名后仍能定位；自定义模板也必须使用 `Dig/SandDecoration` Shader。
6. 点击 `Generate`，保存Prefab。再次生成替换本组件保存的输出引用，不按名称扫描，不删除手工节点；Count0清输出。支持整次Undo。初始空洞由现有InitialMask控制，编辑器预览和运行时都会镂空，不需要为装饰重新InitMap。

只有 Generate 创建GameObject/SpriteRenderer，且只允许非播放态Prefab Stage；不会改变既有Prefab、地图、美术组或自动添加到其它关卡。本轮未给任何正式Prefab选择图片或挂组件，以上绑定属于美术使用步骤。

## 文件与数据流

- `RandomSandSpriteGenerator.cs`：显式Sand/材质/输出引用、运行时材质生命周期、局部遮罩同步。Sprite组/区域/数量/生成样式仅Editor编译，玩家只持有保存好的输出与遮罩绑定。
- `RandomSandSpriteGenerator.Editor.cs`：本地配置校验、System.Random选图/位置、完整Sprite边界、Odin Generate/Undo、Prefab数据写入、区域Gizmo及MaterialPropertyBlock预览。
- `DigSandDecoration.shader` 与 `DigSandDecoration.mat`：世界坐标到Sand UV，读取共用遮罩；采用Unity6000的UnitySprites.cginc处理Sprite UV、RendererColor/Flip与ETC1拆Alpha分支，颜色不继承沙土的棕色Tint。
- 共享 `SceneSpriteBrushClear.cs` 仅新增 `MaskTexture` 只读属性与 `MaskTextureChanged` 局部事件，在原ApplyMaskToMaterial后通知当前RT，ReleaseRTs后通知null。旧盖章、进度、完成、输入、重置和释放算法保持原源码。

运行时每个Generator创建一份独立材质，所有输出共享它；OnEnable读取当前RT/初始图并订阅事件，逐章只更新该材质的纹理，不逐张扫描或进行CPU/GPU读回。不新建遮罩RT。初始对象启用顺序由当前纹理查询与后续Reset事件覆盖；RT交换/Reset/重新启用沙土均更新。

禁用Generator会取消订阅、隐藏其输出并释放自有材质；重新启用恢复输出和当前遮罩。多个区域可引用同一Sand，材质各自持有同一RT。整关重建恢复Prefab布局与烘焙空洞。沙土/关卡在运行时默认保持静态；运行中改变其变换后需重新启用Generator刷新映射，本轮没有增加每帧运行时矩阵或布局重算。

ExecuteAlways使用 `Application.IsPlaying(gameObject)` 区分实际运行对象与Prefab编辑对象。非播放分支只读取序列化引用和本地烘焙图，缓存一个MaterialPropertyBlock作预览；不创建玩法节点/运行时材质，不触发DB、存档、单例或服务。OnValidate仅刷新这条预览。生成采用RegisterCompleteObjectUndo保存组件及输出引用，避免新节点RegisterCreatedObjectUndo打断延迟RecordObject导致撤销引用未恢复。

## 验证与证据边界

在TEMP隔离项目中使用实际Unity6000.0.59f2、URP17、生产脚本/Shader、真实Prefab Stage和RenderPipeline.SubmitRenderRequest：

- 20项Editor检查通过：Shader编译支持；精确数量/图组/随机位置；非中心pivot完整边界；16×8父节点下世界横纵比例；排序/无新增碰撞；编辑器无运行时材质；重复生成不累积且手工节点保留；Undo恢复前一布局/引用；过大Sprite先拒绝；Count0；实际Prefab预览红装饰和初始洞镂空；保存重开引用和布局。
- 30项PlayMode检查通过：复用已保存输出/共享本区域材质/单次订阅；实际红/绿同纹理不同子Rect渲染；初始空洞/部分擦除未挖像素保留；连续RT交换/重复空章/Reset；Generator禁用重启；多个区域和销毁订阅；Sand释放重启；关卡平移/旋转后重新启用映射；完整新实例恢复布局/初态。
- 原耗墨与正圆70项真实隔离检查复跑通过，含预算/门禁/物理/重复空洞、不同尺寸遮罩、GPU二值对照及64方向正圆。测试GPU读回只存在TEMP验证代码。
- 当前新增源码Editor宏0错误/113警告，Android宏0错误/118警告。收尾发现并行任务新增DigTargetTrigger/DigVictoryCondition及Actor/Level接口修改，已把新增类型纳入最新源码编译；隔离夹具没有替该任务验证新的胜利语义。不是APK/IL2CPP/iOS构建或完整主工程验收。
- 首次Undo检查发现RecordObject记录未及时保存输出引用，改CompleteObjectUndo后复跑通过，失败日志保留；不把初次失败列为通过证据。

产物位于 `C:/Users/admin/AppData/Local/Temp/brainpunk-random-sand-20261012/`：editor-checks/runtime-checks、unity与compile日志、initial/partial/prefab-preview/transformed截图；ink-regression目录保留70项回归。所有临时Sprite/Prefab/相机/验证脚本只在TEMP，没有回写正式地图。

## 风险、性能和回滚

- 装饰数量对应实际GameObject/SpriteRenderer；共用现有遮罩与每区域一份材质，但仍有Renderer、透明叠加和纹理切换成本。完全擦空时只是像素透明，节点/绘制资源保留，不代表渲染成本归零。真实资源数量下的DrawCall/GPU耗时、Android/iOS横屏和帧率为uncertain，需实际关卡Profiler验证。
- 本轮验证同纹理不同Sprite子Rect，未覆盖真实SpriteAtlas自动旋转打包、ETC1拆Alpha硬件/ARGB32遮罩回退、复杂带剪切父级矩阵、反复100次重建内存稳态、正式美术布局、冷首帧RT问题与生产HUD/SDK事件系统。SpriteAtlas/ETC1有源码支持分支，不能据此声称上述硬件已验收。
- 源码角色、碰撞、墨水/UI/存档/统计职责不改，50项装饰检查与70项旧路径回归只覆盖其明确范围。
- 回滚时移除目标Prefab的Generator与它保存的装饰节点，撤新增Generator/Editor partial/Shader/材质及各meta，再撤共享擦除本轮7行API/事件通知。保留已提交的尺寸/正圆/空洞耗墨和其它并行改动，禁止整文件回退；本轮未写Packages/GuruSDK/GF。
## 2026-10-12 材质移动后默认加载修复

用户移动了DigSandDecoration，旧Assets/Game/ArtSrc/Dig路径不存在，源码固定路径Load返回null却报Shader不符。当前材质meta GUID为8a17503625cd40cea308cfcda9dd412a，Shader引用f7107fd8c6d54a2b8eeb473a7af5b60b正常；当前文件在Assets/Game/ArtSrc/Sprites/Dig，保留用户移动结果。

Editor partial增加稳定GUID常量，仅显式Generate通过GUIDToAssetPath读取当前路径。先以局部变量解析/校验材质，成功后才开始Undo并写绑定、清旧输出；区分默认材质找不到与显式Shader不符。正确显式模板不被覆盖，Runtime/装饰渲染/地图/布局算法不改。Prefab Mode等Unity完整编译后重新点击Generate即可；默认字段可留None，不需把资源移回旧目录。

14项真实隔离Unity Prefab Stage检查通过：旧路径空返回复现、GUID新位置、当前Shader正确、None自动绑定/数量、再次移动重命名、显式自定义保留、错Shader拒绝并保持旧节点/引用/绑定、默认资源非Material时明确缺失并保持布局、恢复后生成、Undo恢复。Editor/Android宏源码编译均0错误；正式主Editor当前实例重新生成/保存仍待用户复测。本修复没有重新验收真实美术/移动端或其它关卡流程。

产物：C:/Users/admin/AppData/Local/Temp/brainpunk-sand-material-move-20261012/ 的checks.txt、unity.log、compile-editor/compile-android.log及修改前源码/status。只修改Editor partial与相关说明，无资源/Prefab/地图写入；回滚只撤GUID加载/校验顺序及本章节，会恢复对旧路径的依赖，不回退用户的目录整理。