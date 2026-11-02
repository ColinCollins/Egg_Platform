# Level_009 岩浆泡泡功能与风险记录

## 需求与原有行为

2026-10-10 首版：在用户已制作的岩浆表面添加循环鼓泡、爆裂表现，挂在 `Level_009/Water` 下。首版未修改浮力和死亡。

同日用户修订（当前有效）：不用序列帧，保留程序鼓泡/爆裂，换一张明亮新素材；岩浆进入即死亡，不上浮，边下沉边变黑、冒烟，播放用户提供的 `shaojiao.ogg`。因此仅本关的浮力与 Actor 组件作局部替换，其余地形、移动和胜负/重开协议保持原样。修改前核对见 `DevelopMD/已有功能设计与使用记录/2026-10-10.md`。

同日上一轮反馈：减速后仍几乎看不到泡体，整体偏小，水面缺少熔岩感。随后修正泡体 Rect 配置、放大气泡并增加仅本关使用的动态熔岩表面材质。

同日美术修订：截图已可见泡体，但用户认为观感不佳、鼓起过慢、部分太大。泡体改为干净橙色渐变及一块柔和高光，鼓起到爆裂为 1～1.4 秒。

同日最新反馈：达到最大尺寸后的停顿不自然、大小接近、固定三滴飞溅缺少变化。本轮取消等待段，直径范围改为 0.35～0.9（保持大泡上限，小/中/大混合），飞溅改为每轮 2～6 滴独立随机轨迹。水面及死亡流程保留，不操作用户窗口。

同日后续死亡修复（当前适用范围）：用户已把岩浆放到 009／011／012／019，并确认补齐四关烧焦接入。修复首次音效订阅空引用，三关显式复用本目录 Actor 子类和 FallArea；不修改共享 ActorCtrl，不复制 009 的移动参数覆盖其它关。

## 文件与职责

### 2026-10-10 共享水材质串改：事后补查

用户反馈 10016 水变岩浆后，确认 `af0a87d70` 提交包含 `Level_16/Shader Graphs_Water.mat` 的红橙色、透明度及波浪参数改动，共影响 17 个普通水关卡的 19 处水体。此前“本任务没有修改共享资源”的核对仅描述局部编辑边界，不能证明最终工作区中的共享水原本正确；遗漏了共享材质已有改动及所有消费者的回归检查。

本次仅将该共享材质六行恢复至 `af0a87d70^`；009／011／012／019 的 Renderer 和 `InteractableWater.WaterMaterial` 均使用独立 `Level9LavaSurface.mat`，本轮岩浆资源和四份 Prefab 不变。完整引用清单、静态验证及运行验收缺口见 [共享水材质恢复与风险记录](../Level_16/共享水材质恢复与风险记录.md)。后续核对最终共享材质/Shader/贴图状态及全部引用，不能只核对任务自身增量。

- `Assets/Game/Prefabs/Level/Level_009.prefab`：`Water/LavaBubbles`；原 `Buoyancy` 节点改名 `LavaFallArea`，保留原范围，禁用浮力及旧进出水回调；添加局部触发器。
- `Level9LavaBubbles.cs`：本关局部表现组件，读取水面、更新一份动态网格；没有 Collider、Rigidbody、全局事件或存档/SDK 访问。
- `Level9LavaBubbles.mat`、`Level9LavaBubble.shader`、`Level9LavaBubble.png`：仅使用单张 PNG 的 alpha 作为手绘轮廓，泡体 RGB 由材质的底色/顶色/柔和高光生成，不再显示原图的密集亮纹。程序连续改变宽高；负 UV 的破口、小滴和涟漪仍使用顶点色，不切帧。导入最大尺寸 512、关闭 mipmap、Clamp、保留透明度。
- `Level9LavaSurface.shader/.mat`：009 独立水面表现，缓慢不规则橙黄热流、炽热亮边及红橙深层；顶点仍沿用原水面公式与网格，不改交互波浪脚本。Prefab 同时绑定 Renderer 材质和 InteractableWater.WaterMaterial，避免 Start 覆盖回共享材质。
- `Level9ActorCtrl.cs`：在 009／011／012／019 通过移除原组件、添加子类的 Prefab override 显式接入；各关保留原基础参数，关卡、进门和皮肤绑定仍引用各自同一主角。009 二段跳开启、力度 16；011／019 二段跳关闭、力度 11；012 二段跳关闭、力度 16。
- `Level9LavaFallArea.cs`：按显式主角 Rigidbody 身份判定，一局仅派发一次现有 OnTriggerFailAreaEvent，再启动本关烧焦动作。
- `shaojiao.ogg`：用户音效，原文件逐字节复制；44.1 kHz、1.57 秒，导入转单声道。私有非循环音源遵守 SfxOn，角色销毁时停止，不改共享音效表。
- `Level_16/InteractableWater.cs`、`Water.shadergraph`、原 Water 材质、`WaterTriggerHandler`、共享 ActorCtrl/BaseLevelCtrl 和 Level144Smoke 材质只读复用；四个指定关卡以外的关卡、GF、Packages 均不修改。

## 使用方式

选中 `Level_009 → Water → LavaBubbles`，在 **Level 9 Lava Bubbles** 组件调整：

| 参数 | 当前值 | 含义 |
| --- | --- | --- |
| Bubble Count | 12 | 同时循环的槽数；包括等待中的泡泡，修改数量后重新运行 |
| Diameter | 0.35–0.9 | 世界单位直径，小/中/大三档按 55%/35%/10% 抽样；大泡上限不增，不被 Water 的 X=2.04 拉伸 |
| Idle Seconds | 0.4–1.8 | 每次鼓泡前的随机等待时间 |
| Grow Seconds | 1–1.4 | 全程持续鼓起，到达最大值即爆；没有饱满等待段，首轮不跳过 |
| Burst Seconds | 0.48–0.72 | 破口、小滴飞溅、回落和消散时间 |
| Edge Margin | 0.2 | 两端世界距离留白；另按半径×2.9 预留随机飞溅与涟漪范围 |
| Surface Offset | -0.015 | 泡泡根部轻微沉入表面，避免悬空缝隙 |
| Rim / Lava / Highlight Color | 亮橙 / 橙色 / 黄色 | 仅控制破口、小滴与涟漪；完整泡体颜色在 Level9LavaBubbles.mat 调整 |
| Bubble Texture Rect | 0.02 / 0.15 / 0.96 / 0.7 | 去除单张图片透明留白的 UV 范围，不是切帧；YAML 必须含 serializedVersion: 2 |

水面、源 MeshFilter、源 MeshRenderer、本节点 MeshFilter 已在 Prefab 显式绑定；不要清空引用。大小/节奏参数按每个泡泡的下一轮重新抽取。仅在运行时显示，编辑态 MeshFilter 的 Mesh 为 None 属于正常情况；未加入 ExecuteAlways 或编辑态实例创建。

水面颜色/流动可调整本关 `Level9LavaSurface.mat`：内部热流速度 0.12、纹路密度 0.45、表层厚度 0.055；波速 0.22、密度 1.45、高度 0.13。主体 Alpha=0.98 控制深层不透明度；浅层保留原约 0.788 透光度，以便看清下沉中的黑化。不要改回共享 Water 材质，也不要为了本关修改 Level16 的资源。

## 数据、渲染与生命周期

1. OnEnable（仅运行态）分配固定数量泡体状态、每槽最多六滴的参数缓存及一份动态网格；各泡泡以 0～1.5 秒随机等待错开，首轮不再从接近爆裂阶段开始。独立 System.Random 不消费游戏全局随机数。
2. LateUpdate 只读水面上排顶点，并按 Level9LavaSurface（保留原 Water.shadergraph 公式）计算波浪位移，再在线段间插值。公式为 `sin((x + Time.time * WaveSpeed * WaveFlowDirection) * WaveCount) * WaveSize`；上排 UV.y=1。泡体左右根部分别采样高度，大泡底边跟随斜坡，不只采中心。
3. 按水面宽度分区随机布点；每轮位置、大小、形状、等待与持续时间均变化，随机高度比例为 0.85～1.05。生长曲线为 t×(0.65+0.35t)，末段保持正增长，不再提前封顶。爆裂后完整泡壳消失，保留短暂破口、随机 2～6 滴和消散涟漪。OnEnable 对无效 Rect 范围给出一次警告，并在本实例恢复素材默认范围。
   - 小/中/大对应范围内 0～30% / 45～70% / 85～100% 的取值，概率为 55% / 35% / 10%；当前实际直径约 0.35～0.515 / 0.598～0.735 / 0.818～0.9。
   - 飞溅的起点、横向距离、抛起高度、半径、长宽比、起飞延迟和飞行时长在 RestartBubble 中抽取一次。DrawBurst 只读取缓存并推进，不逐帧随机；至少第一滴无延迟，全部在本轮 Burst 结束前消散。
4. 世界尺寸转换回子节点坐标，消除 Water 非等比缩放对泡泡形状的影响。Renderer 在 Water 的 SortingGroup 内以 order=1 绘制在水体（order=0）之上；无新 SortingGroup。
5. 状态、顶点、索引等缓冲复用；循环没有 Instantiate/Destroy，没有物理力或 Splash。OnDisable 清空本地网格，OnDestroy 释放自己创建的网格，不释放共享材质或源网格。节奏使用 Time.deltaTime，跟随 Unity 游戏时间；没有另加游戏暂停事件。

## 首版验证证据（不等同于本次烧焦流程验证）

- 修改前保存了当前用户 Prefab 内容用于差异核对；去掉新增对象块和 Water 新增的一条 child 引用后，剩余内容逐字比较一致；本地 fileID 无重复。不是拿 Git HEAD 覆盖用户已修改的岩浆。
- Unity 6000.0.59f2 / Windows DX11 / Android 编辑器目标：资源刷新、脚本编译/域重载后，Inspector 显示新组件且四个引用完整，独立材质正确载入。
- 通过游戏主线选关进入 UI Level 9，调试显示资源路径 `Level_009`，实际看到岩浆表面不同位置/大小的半球、爆裂小滴及循环变化；不是只在 Scene 视图推测。
- 运行超过一轮周期后点击关内重开按钮：时长复位，岩浆与爆裂效果仍正常出现，无新增本组件空引用或 Shader 错误日志。
- 静态检查确认新组件无存档、全局事件、角色/胜负操作；原水面、浮力组件和所有已有对象的序列化内容保持不变。共享水面代码、材质、127 Shader、GF 和 Packages 没有由本任务修改。
- `git diff --check` 的原 Prefab 新增水面区块已有空字段尾空格（本任务开始前存在）；仅清理本次新增块的对应尾空格，未重排用户内容。

## 风险与未验证范围

### 2026-10-10 最新：取消停顿、大小分层与随机飞溅

- **代码原因：** 原生长使用 SmoothStep(Clamp(t/0.75))，从 75% 开始恒定最大值；尺寸区间过窄；飞溅固定三滴，方向/大小/高度主要由索引决定。已分别改为全程增长、三档混合尺寸和每轮缓存独立飞溅参数。
- 本轮只改 `Level9LavaBubbles.cs`、009 的 diameter 及本地记录；单图/Shader/材质、物理、下沉黑化、烟音和失败重开不变。最大泡仍为 0.9，Grow 仍为 1～1.4，Burst 仍为 0.48～0.72；扩展尺寸差异不再次增大大泡。
- **验证：** 独立 C# 编译成功，仅原四个 Inspector 字段的 CS0649。固定种子、复写同公式的隔离数值模拟：324 组帧率/时长/形状/相位检查均为持续增长；10,000 次爆裂抽样得到小/中/大 5448/3579/973 次、2～6 滴各 1978/2013/1969/2067/1973 次，40,044 组不同飞溅参数。逐滴轨迹非负、延迟+寿命不超过一轮、半径归一化最远范围 2.564 小于预留 2.9；检查无失败。该模拟不等于 Unity 实例/真实随机流或手机运行。
- 最大 24 槽同帧爆裂、每槽六滴的网格上界为 3696 顶点/9792 索引，低于现有 8192/24576 缓冲容量；新参数数组上限 144 个 Drop，仅 OnEnable 分配。源码确认 DrawBurst 不调用随机函数、不创建数组或对象；真机性能未实测。
- Prefab 逐字比较仅 diameter 从 0.6～0.9 改为 0.35～0.9；泡体/表面 Shader、材质、Actor/FallArea 哈希不变。**uncertain：持续鼓起是否自然、大小混合及飞溅观感仍需用户重开第 9 关确认。** 本轮未操控窗口，未执行 Unity 场景或移动端测试。
- 回滚只涉及本轮：恢复旧尺寸/生长曲线和三滴索引轨迹，去除 Drop 缓存，边距倍率恢复 1.8；不要回滚简化材质、Rect 或死亡逻辑。现有旧注释仅在与新数据流冲突处同步修改。

### 2026-10-10 上一轮：简化泡体、收小与提速

- 用户截图证明上一轮已能显示完整泡体；但其艺术风格、速度和部分尺寸未获接受。本轮明确修订这些视觉行为，不能把上一轮编译通过解释为观感验收。
- Shader 保留原 PNG alpha，不改图片文件；泡体 RGB 改为橙色渐变及单块暖黄柔和高光，不再采样原图 RGB。负 UV 的爆裂分支先返回原顶点色，未改爆裂算法/时长、数量、等待、首轮或水面贴合。尺寸 0.6～0.9、Grow 1～1.4 与 Prefab 同步；高度随机上限 1.05，避免部分泡体过高。
- 独立 C# 编译通过（仅原四个 Inspector 引用 CS0649）；泡泡和水面 Shader 共四个 DX11 阶段通过隔离编译，限制同下一节。54 组帧率/时长/直径/形状数值检查通过，饱满期 0.25～0.35 秒，最大直径减少约 38%，平均鼓起到爆裂耗时减少 52%。Prefab 逐字比对只变 growSeconds 和 diameter 两项。
- 本轮前后水面 Shader/材质、Actor、FallArea 的 SHA256 一致。没有改原 PNG、碰撞、下沉黑化、烟音或重开，也没有操控电脑窗口。**uncertain：新颜色/节奏的实际观感、Unity 导入和移动端运行尚未验收。** 用户重开第 9 关观察是否还有碎亮纹、过大泡体或慢吞吞停留即可验证本轮目标。
- 本轮局部回滚：恢复直径 0.85～1.45、Grow 2～3、Shape 上限 1.2；泡泡 Shader 恢复 RGBA × 顶点色采样，材质恢复原 _Color。无需回滚水面、Rect 修复或死亡逻辑，也不应整文件还原 Prefab。

### 2026-10-10 上一轮：泡体可见性与熔岩表面

- **事实与推断边界：** 旧 Prefab 的泡体 Rect 缺少版本信息，而正常工程 Rect 包含 `serializedVersion: 2`。完整泡体依赖该 UV，爆裂使用纯顶点色。PNG 的 UV(0.02,0.15) 与 (0,0) alpha=0，而 (0.5,0.5)、(0.5,0.8)、(0.1,0.2)、(0.9,0.2) alpha 均为 253。这与 UV 退化导致只看到爆裂的现象吻合，但没有读取用户运行实例的实际 Rect；**根因是否唯一仍 uncertain，不能把格式修复当作已完成视觉验收。**
- 已修正 Rect 序列化并增加无效范围保护；直径增至 0.85～1.45；前 75% 鼓起、后 25% 饱满；左右根部贴合波浪。保留原单张 PNG、爆裂时长、12 槽、开场完整生长和世界单位尺寸。
- 新局部 Shader 在原水体 Renderer 上绘制慢速热流纹路、厚薄不一的亮边；不加 Renderer、额外纹理或物理对象。浅层透光保留、深层更实，Actor=200 / Water=201 / Smoke=203 的排序不变。新旧 Shader 波浪公式一致，仅本关波浪参数减慢。
- **独立编译：** Unity 自带 Roslyn 对修改后的气泡脚本编译成功，仅四个 Inspector 引用有非 Unity 编译器的 CS0649 警告；Prefab 已逐一绑定。测试响应文件来自现有 Bee 缓存，排除了两个已不存在且本文件不依赖的旧包引用；未修改工程响应文件或包文件。输出全部在 TEMP，不冒充 Unity 全工程编译。
- **Shader 隔离检查：** 使用 Windows D3DCompile + 项目实际只读 URP Core 头文件，两种 Shader 的 Vert/Frag 四项均生成 DX11 字节码。测试器启用旧 half 兼容，并仅在内存中去掉 FXC 不支持的未使用空 PopMarker 宏；项目/Package 源文件不变。检查期间发现并修正了新 Shader 中 `point` 保留字命名。这不等同于 Unity ShaderLab 导入、Android GLES/Vulkan 或 iOS Metal 编译通过。
- **数值/资源：** 30/60/120 fps × 三种 Grow × 三种开场等待 × 三种直径，共 81 组时序/尺寸检查通过，饱满期为 0.5～0.75 秒。Prefab 共 116 个块，无重复 ID/悬空本地引用；与本轮前逐字比较仅 Renderer 材质、WaterMaterial、泡泡参数三个组件块改变。新 GUID 唯一且只有 009 使用。共享 Water 材质/脚本及 Actor/FallArea 的前后 SHA256 一致。
- 验证临时文件位于 `C:/Users/Crow/AppData/Local/Temp/codex-level009-lava-20261010/`：`lava-polish-compile.rsp`、`Level9LavaPolishCheck.dll`、`CheckLavaShader.ps1`。没有打开、点击、刷新或操控 Unity；现有 Editor.log 不是本次新 Shader 的导入证据。历史 Prefab 空字段尾空格仍保留。
- **待用户手动验收：** 重开 Level009 后观察 10 秒，应有可辨认的大半球连续鼓起、饱满停留、爆裂；泡根贴合水面、纹路缓慢流动，角色入岩浆仍边黑化边沉降、冒烟和播放烧焦声。若仍只看到爆裂，需要检查当前运行实例 UV/材质是否已更新，不能继续只调时间。移动端观感、60 fps、后台恢复与资源构建未测。
- **本轮局部回滚：** 只把 009 的两个水面材质引用恢复为原 `3fca6e338bdf7d842af78bbe8e05de09`，恢复旧尺寸及生长曲线/中心底边采样；必要时移除本关新表面 Shader/材质。建议保留正确 Rect 格式及校验。对照 TEMP 的 `Level_009.before-lava-polish.prefab` 逐块处理，禁止整文件覆盖用户后来修改。

### 2026-10-10 气泡膨胀减速

- 原值 0.75～1.55 秒，首轮初始 Age 允许接近 Grow 的 90%；用户反馈看不清鼓起过程。现改为 2～3 秒，首轮 Age 仅在 -1.5～0 秒间错开等待，不跳过膨胀。
- 只改本关脚本默认值、初始化相位和 Prefab growSeconds；大小、数量、爆裂 0.48～0.72 秒、素材、水面及烧焦逻辑不变。平均鼓起时长由 1.15 秒增至 2.5 秒（约 2.17 倍）；因此单位时间爆裂次数减少属于预期效果。
- 验证：逐字比较确认 Prefab 本轮只变更 growSeconds；脚本与 Prefab 一致。按原 Age/Grow 计算进行 30/60/120 fps、三种鼓起时间、三种开场等待的 27 组数值检查通过，首轮不会早于完整鼓起结束而爆裂。仅为数值/静态验证，没有操控窗口、没有本轮 PlayMode 或真机观感验收。
- 局部回滚：growSeconds 的脚本与 Prefab 恢复 0.75～1.55，初始化 Age 恢复 Range(-1.5f, Grow * 0.9f)；不需要回滚其它功能。

- **uncertain：** Android/iOS 真机 60 fps、GC/DrawCall 实机采样、热更资源包构建和完整过关/存档/打点回归未测。此前编辑器实际进入、显示、循环及关内重开只覆盖首版，不能覆盖最新贴图/表面优化。
- 网格缓存容量覆盖上限 24 个泡泡；默认仅一份新 Mesh/Renderer，未引入持续创建对象，但不能据此宣称真机零开销或已达到 60 fps。
- 波浪采样与本关 Level9LavaSurface 顶点公式耦合（继承原 Water.shadergraph 公式）；将来改变位移或顶点排列时，需要重新核对采样。当前水面应维持水平 XY 布局、正宽度，非水体通用组件。
- 游戏暂停菜单是否继续显示水面动画沿用现有水体时钟，不新增暂停协议；后台切换、原生广告覆盖恢复未实测。
- Editor.log 原有 SDK 请求、个别旧 meta 和设置文件警告未在本任务处理，不宣称整个工程无错误。

## 回滚

不能整体还原 `Level_009.prefab`，其中有用户的岩浆和地形修改。只回滚本次增量：恢复 009 内的原 Actor 组件及皮肤引用、移除新增 stripped 引用与局部 FallArea 组件、恢复 Buoyancy 名称/浮力/回调及 UsedByEffector，泡泡按需要恢复原材质/DrawGrowing。修改前副本在本机 TEMP 的 `codex-level009-lava-20261010/Level_009.before-texture.prefab`，只用于逐块比对，不覆盖后来用户修改。旧序列图已移出 Assets 到该 TEMP 目录，不再参与工程导入；生成原图仍保留在 Codex 输出目录。

## 当前烧焦流程与风险

### 2026-10-10 四关接入修复与证据

- **修复前事实：** 011／012／019 的 LavaFallArea 两个角色引用为 0，主角仍为 ActorCtrl，触发函数在 guard 返回。009 引用完整，但 `Editor.log:456104–456108` 确认 `BeginLavaBurn:70` 空引用，首次 `events.Subscribe` 前未初始化。异常发生在创建烟雾和启动音频之后，不能把它扩展解释为所有表现缺失的唯一原因。
- **代码变更：** 订阅前调用项目现有 `EventsUtils.ResetEvents(ref events)`，确保首次创建并清理旧订阅；OnDisable 沿用同一工具注销。只更新两份脚本的适用范围注释（原“仅 009”已不符合此次复用事实），FallArea 运行逻辑不变。
- **Prefab 变更：** 011／012／019 每份仅替换 Actor 组件、对应 PrefabInstance 的组件 override 和 LavaFallArea 引用三个已有块，增加九个显式 stripped 引用；保留 Actor 本地 fileID，补内嵌皮肤组件 actor override。009 Prefab 本轮不改；不调整已有地图、水面、泡泡和碰撞范围。
- **完整静态检查：** 分段读取四份完整 YAML，009／011／012／019 分别 116／278／177／418 个块，均无重复 fileID 或悬空本地引用。校验主角根刚体、PolygonCollider、Spine、推物体节点和三个服饰挂点来自各自 Actor 实例；皮肤、BaseLevelCtrl、StartDoor 指向替换后的同一组件。四关烟雾材质与用户烧焦音频 GUID 均解析到本地资源。LavaFallArea 的 BoxCollider 是 Trigger、UsedByEffector=0，旧浮力禁用。
- **基础参数保留：** 对照共享 Actor 源 Prefab 加每关原 overrides，三关的全部 Actor 基础序列化字段一致；原位置/旋转、装扮挂点及其它 Actor overrides 一致。009／012 可与修改前完整捕获逐块比对；011／019 首次整文件工具输出曾截断，随后改为分页完整读取验证，不能声称已对这两份未捕获区间做修改前逐字对比。本次应用的补丁只涉及上述三个目标块及九个新引用。
- **隔离编译：** Unity 6000.0.59f2 自带 Roslyn 编译当前 Actor/FallArea，0 错误。10 个 CS0649 是非 Unity 编译器不识别 Inspector 赋值；1 个 CS0436 来自隔离编译同时引用现有 Assembly-CSharp 和新源码，编译器明确采用新源码。响应文件与输出在 `C:/Users/Crow/AppData/Local/Temp/codex-level009-lava-20261010/lava-burn-compile.rsp` 和 `Level9LavaBurnCheck.dll`，不是 Unity 全工程编译。
- `git diff --check` 报告三关原复制水面区块的空字段尾空格；这些在本轮前已存在，不为通过格式检查而重排用户区块。工作区原有其它修改保留，没有提交、切分支或操作窗口。
- **uncertain／待运行：** 四关实际走入/跳落后的黑化可辨识度、烟音、持续按键不弹起、失败后重开、静音/中途静音、死亡中退出、Android/iOS 与资源包构建尚未验收。仍保留原失败约 1.2 秒后重建，1.57 秒音频的尾音会随角色销毁停止；没有延长共享失败时间。
- **本轮回滚：** 只撤回事件订阅初始化和三份 Prefab 本次组件替换/绑定，将三关 Actor 恢复其原 stripped 组件、移除本次九个引用与皮肤 actor override，并恢复原基础参数 overrides；不整体还原关卡、不删除用户新增岩浆。009 原接入和最新泡泡参数不在此次回滚范围。

1. 岩浆 BoxCollider 原范围为 13.6×6，随 Water 的 X=2.04 缩放；浮力组件禁用，BoxCollider 的 UsedByEffector=false，旧 EnterWater/ExitWater 触发器禁用（原回调目标已为空）。水面波浪触发器不变。
2. 本关 FallArea 对照显式 Rigidbody 引用识别主角，先派发标准失败事件，再调用 BeginLavaBurn。共享 BaseLevelCtrl 仍等待 0.2 秒进入失败、再等 1 秒发 Failed Reset；不新增全局事件或重开/打点入口。
3. Actor 子类调用已有 Die，关闭主角 PolygonCollider 和推物体 Collider；重力设为 1，初始向下 3，每个物理步将 Y 速度限制在 -4.5～-3。烧焦时屏蔽普通 OnUpdate 和 JumpButton，不可能从本关动作施加向上的力。
4. 0.4 秒内主角 Spine RGB 渐变至炭黑，alpha 不变；从显式服饰挂点枚举 Skin 系统运行时 SpriteRenderer，一起变黑。未修改皮肤存档或共享材质。
5. 烟雾复用现有 Level144Smoke 材质，运行时新建一个 ParticleSystem，最多 28 粒子，世界空间向上漂散；发射点随角色下降，已生成烟团留在原位置。排序位于 Water 上方。音频仅在死亡入口播放一次，关闭音效立即停，重新打开不重播。
6. OnDisable 恢复本实例颜色、停止私有音源、销毁自建烟雾、注销音效事件；正常 Failed Reset 销毁重建本关，不复用死亡实例。

### 当前验证结果

- Unity 在 11:09 完成脚本编译、域重载和 Level_009 Prefab 导入；日志已出现 Level9ActorCtrl 的正常进门 StopMoving/ResumeMoving。未检出本次 C# / Shader 编译错误。
- 静态验证：116 个本地对象块无重复 ID、无悬空本地 fileID；所有新增 Actor/Skeleton/刚体/碰撞/挂点/音频/材质引用已核对。除局部 Actor 替换、浮力/FallArea 和泡泡字段外，其余原有对象块与本轮修改前一致。
- 单张 PNG 为 1774×887 RGBA，角点 alpha=0；运行时导入限制最大 512。无序列帧切换逻辑；弃用的序列图不在 Assets。
- 音频与用户源文件 SHA256 一致，时长 1.57 秒。标准失败窗口约 1.2 秒，重建时会截断音效剩余尾音，防止串入下一局；本次没有为了播完整声音延长共享失败时序。
- 用户要求不再操控电脑后，停止所有窗口、鼠标和键盘操作。**实际落入岩浆后的沉降/变黑/烟雾观感、声音听感、落入后自动重开均未人工验收**；前面的首版泡泡 PlayMode 结果不能替代这些检查。未执行真机、资源构建和广告/后台恢复测试。
- 现有 Prefab 用户区块中仍有 Unity YAML 空字段尾空格；不为消除历史 whitespace 提示重排这些内容。原有 SDK/设置文件警告不在本任务处理范围。

### 建议手动验收

- 正常走入岩浆和跳落岩浆各一次，持续按住方向/跳跃，确认不浮起，0.4 秒内黑化且冒烟、烧焦声只响一次，随后按原流程重开。
- 重开后主角及衣服恢复正常色、泡泡继续随机循环；音效关闭时不播放烧焦声；死亡期间退出关卡不留烟和声音。

## 单张素材生成记录

- 方式：内置 image_gen（非 CLI）；透明背景新生成，未使用旧泡泡素材，没有分帧。
- 工程最终素材：`Assets/Game/Scripts/Level/MainLevel/Level_9/Level9LavaBubble.png`。
- 源文件：`C:/Users/Crow/.codex/generated_images/01a07ad8-38a8-7831-8f68-0b78d3e707b8/exec-942ca49f-e206-453b-9211-5d5ef9e2c5be.png`。
- 最终提示词（原文留档）：

> Use case: stylized-concept. Asset type: one standalone transparent 2D Unity game sprite, NOT a sprite sheet, NOT animation frames. Subject: a single side-view semicircular molten lava bubble dome emerging from an invisible horizontal lava surface. Bright golden yellow highlights, luminous tangerine orange body, thin warm-orange outline; never dark brown, red-black, or sooty. Playful hand-painted casual puzzle game art, subtly irregular ink contour, simple readable broad highlights, no photorealism, no glass. The dome has a straight horizontal flat bottom and rounded semicircular top, approximately twice as wide as tall. Center it with small transparent margin, fills most image width. Absolutely no extra bubbles, droplets, ripples, base puddle, background, shadow, text, grid or labels. True transparent background. It will be deformed by procedural mesh animation for inflation; create exactly one intact bubble.
