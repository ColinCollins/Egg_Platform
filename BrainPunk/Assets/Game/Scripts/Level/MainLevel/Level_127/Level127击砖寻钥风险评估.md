# Level 127 击砖寻钥风险评估

> 与 `Level127击砖寻钥功能说明.md` 成对；关卡正式标题未确认（`uncertain`）。本文不重复玩法，只说明什么会坏、现有控制、证据边界与回滚。

## 原有行为基线

实现前 Level 127 只有骨架：双门（左 StartDoor、右 door_0@ (8.25,-1.2)）、`Ground_long`、`BaseLevelCtrl`（actor/successAnim/enterDoorAnim/GamePlayPanel_000 已接线）、`PlaceholderBrickWall` 下 14 块占位砖（仅 SpriteRenderer，无碰撞、无脚本）、door0 下无 DoorLock。占位砖与骨架均为本关私有实例，本次改动不触碰任何共享 Prefab/脚本。

## 影响文件与系统

- 本关专属资产：`MainLevel/Level_127/Level127Ctrl.cs`、`Level127Brick.cs` 及 meta；`Level127WhiteFlash.shader/.mat` 及 meta；`ArtSrc/Sprites/L127/`（4 张 PNG + meta）；碎裂图集、材质与粒子 Prefab。原 `Level127SkinDrop.cs` 及 meta 已随皮肤玩法移除。
- 修改（本关专属）：`Level_127.prefab`（door trigger 禁用覆写、DoorLock/Key 实例、KeyLandingPoint、Level127Ctrl 接线、两个 inactive `Postcard_Star` 模板实例和一个 `Tips.prefab` 墙面文字占位实例）、`Level_127/Prefabs/Level_127_PlaceholderBrick.prefab`（贴图替换、BoxCollider2D、Level127Brick）。
- 当前调整：`Level127Ctrl.cs` 移除皮肤分配/加载/穿戴，普通道具变为 `12+k` 件（`k` 为已永久收集星数量）；`Level_127.prefab.propSprites` 在原 11 种上新增 `Sp_fxyq_2__04`、`Sp_mfmz_1`、`Sp_l61_dj_img_04` 三张直接 Sprite 引用，并移除 `skinParts`。Ctrl 使用实例内 Collider 列表，在后落地道具恢复模拟后补齐本关普通道具的成对碰撞忽略。星星、钥匙、合成、砖墙位置、碎裂特效与共享碰撞矩阵不变；同步维护 Context、路由关键词与本目录文档对。
- 只读复用：`Key.prefab`、`DoorLock.prefab`、`door_0.prefab`、`Actor.prefab`、`BaseLevelCtrl`、`ActorCtrl`、`AudioManager`、`GamePlayPanel` Reset 链、Epic Toon FX `sparkle_ADD.mat`、共享 `Tips.prefab`、共享 `Postcard_Star.prefab`、`BoxBroken.prefab` 及 14 张道具源素材，均未修改。新增素材精确路径/GUID 见功能说明。

## 风险项

### P1

1. **Key→DoorLock 事件链手工 YAML 无运行证据**（`Level_127.prefab` Key 实例 8 项 items 覆写、stripped 引用）。触发条件：任一 stripped fileID、落地后 Collider 门禁或 items 顺序错误。影响：钥匙半空中被提前拾取、拾取后不开门或卡关。已有控制：Key 根 Collider 在 Ctrl `Start` 与合成期间保持 disabled，1.3s 下落完成回调才启用；实体钥匙初始根 scale 改为 0.75，拾取缩放仍走本关既有绝对目标 0.25；门锁事件顺序与 083 对齐。剩余缺口：0.75→0.25 的拾取缩放观感、实际碰撞回调和开门链未运行验证。
2. **掉落候选缺失或重复引用**（`propSprites` / `CollectPropSprites`）。触发条件：后续删素材、丢失 GUID 或误重复配置。影响：重复外观，全部为空时无普通掉落。已有控制：当前 14 种直接引用覆盖最多 14 个普通槽位，代码过滤空引用并去重，少于 14 种会警告，不足时循环复用。剩余缺口：磁盘 GUID 闭合不等于 Unity 导入成功，三张新增素材的实际透明边距、视觉尺寸与落地接触仍未验证。
3. **碰撞隔离作用域扩大**。若为取消堆叠而关闭整个 TriggerItem 层关系，可能影响星星/钥匙拾取和其它关卡。当前只在本 Ctrl 创建的普通道具 Collider 之间调用 `Physics2D.IgnoreCollision`，未改全局矩阵、未把星星或钥匙纳入列表；Ground 接触与原 Actor 排除保留。实际触发与物理结果仍未验证。

### P2

4. **砖墙物理挡人**（`Level_127_PlaceholderBrick.prefab` BoxCollider2D 1.32×1.22，layer Default）。触发条件：主角跳跃撞墙、或落在墙顶。影响：物理阻挡符合预期，但砖层不在 `whatIsGround`(328) 内，站墙顶时 `isGrounded` 判定失效，可能播放空中动画；侧撞不判方向计击，贴墙跳跃可能连续消耗两击（0.5s 冷却内只计一次）。已有控制：冷却 0.5s；墙底距地面约 2.1 世界单位，站立不可达。剩余缺口：实际跳跃可达性与观感未验证。
5. **普通道具落地时的碰撞对建立时序**。普通道具最大边仍为 1.2，飞行阶段关闭模拟和 Collider；落地先同步刚体位置、清零速度，再启用 Collider/模拟，随后在同一回调内排除 Actor 并忽略其它已落地道具。先落地者跳过尚未启用者，由后落地者补齐配对，避免对停用 Collider 提前设定后失效；同帧落地也依次补齐。未新增道具池化/禁用重启入口，Reset 随关卡销毁列表及对象；若以后引入池化，必须重新建立忽略关系。实际先后落地、同点重叠、角色穿行、Ground 承托与视觉遮盖仍未运行验证。
6. **第二击碎裂粒子大小、可见性与性能**（`Level127_BrickBroken`，5×2 图集、burst 8、缩放 1.2，为此前 0.6 的 2 倍、3s 自销毁）。用户观察到第二击后没有看到碎片集；静态检查表明 Prefab/材质/图集引用非空，但旧 ParticleSystemRenderer 为 `Default/0`，位于砖墙 `SceneLayer/90` 后方。当前本关粒子 Prefab 两个 Renderer 固定为 `SceneLayer/92`，运行时还按被击砖排序层重写为 order+2。剩余缺口：静态排序和缩放值只能证明配置关系，不能证明 Unity 导入、材质透明度、粒子尺寸、扩散范围和实际画面已经达到用户目标。
7. **强化合成演出的运行观感与相机恢复**。合成 Sequence 名义时序改为 1.2s 停顿 → 1.2s 渐强真实白化 → 约 1.333s 蓄力/收缩 → 0.3s 局部白闪切换 → 0.3s 钥匙回弹 → 1.3s 下落，约 5.633s；0.08s 蓄力包含在约 1.333s 内，回弹包含在 0.3s 停留内，不创建全屏白纹理。切换峰值额外生成 16 颗暖白星光并对主相机做 0.22s/0.12/18 次短震；完成、Kill 与关卡销毁路径均恢复缓存的相机 localPosition。风险包括白化 Shader 在目标渲染管线中表现异常、淡光遮盖砖面、星光过强/过弱、相机跟随与直接震动竞争、0.75 钥匙或 0.825 回弹过大。静态代码与 Prefab 参数不能证明主观观感和运行时恢复，保持 `uncertain`。
8. **`passwordCorrect` 合成音效时机**（tag 指向 `l5/passwordCorrect.ogg`）。触发条件：用户已确认这是一次性合成确认音而不是背景音乐；当前调用只位于 `RevealKeyAtTransitionPeak`，在 `SwapFormationForKey` 激活实体钥匙的同一回调播放一次，未切换或打断 `musicInGame`，音量沿用 `AudioManager.PlaySound` 默认值 `0.4`。剩余缺口：静态调用链不能证明音频资源实际加载、峰值听感和音画同步。

### P3

9. **共享 Key 自动浮动与本关下落 Tween 冲突（已做双层隔离）**：`Key.prefab` 的 `MoveFloatHandle` 脚本 GUID `f79fb363863a1254e9f64f480e3ffbff` 对应仓库现有 `Level_2/MoveFloatHandle.cs`，共享默认 `autoStart=true`。旧 127 实例未禁用时，它会从钥匙激活位置启动无限 Yoyo，与 Ctrl 下落同时写同一根 Transform，符合用户观察到的“落下后又浮回中间”。当前 `Level_127.prefab` 实例覆写 `m_Enabled=0`，Ctrl `Start`/切换前再次 `StopMove()+enabled=false`；共享 Key 不修改。剩余缺口：仍需人工确认落地后位置稳定。
10. **两类掉落物抛物线与星位替代**：普通道具和固定星使用 0.45~0.6s 弧线，落点只探测 Ground；星星水平保持砖中心，飞行中可收集，落地后浮动。已收集星位进入普通池，本局收集不追加掉落。正常进关分配为 12/13/14 件不同普通道具；若外部在进关分配后、击砖前更改星星存档，既有 `SpawnReplacementPropDrop` 防御仍从候选池随机补位，可能重复，不将这种异常时序扩大为正常不重复保证。存档查询、标准收集反馈和实际落点未运行验证。
11. **2026-08-26 用户人工观察对应调整**：早期观察对应①第二击碎裂图集不可见——增加 Prefab 与运行时双重前景排序；②对应掉落物不可见——为所有掉落根增加前景 SortingGroup，皮肤异步阶段增加可见占位；③全屏不应闪白——删除全屏白纹理实现；④砖阵到钥匙过快——改为砖阵自身整体收缩后局部切换；⑤钥匙落下后回浮——本关禁用 `MoveFloatHandle`；⑥钥匙过大——当时缩至旧实现 50%。本次后续观察确认缩小前砖阵闪白实际不可见、隐藏物过小且整体演出偏平，因此改用 alpha 轮廓白化、强化局部合成爆发、钥匙根 scale 0.75 和隐藏物 1.5 倍；2026-08-31 再把合成前闪白与砖阵收缩按 1.5 倍速度缩短，并加入固定钥匙砖星星模板。所有新调整仍只有静态证据，需人工复测。
12. **Reset 后运行时对象重建与相机现场恢复**：`Level127Drops`、`Level127Effects`、白化 overlay、一次性星光、合成 Sequence、普通道具 Collider 列表和本局状态均由关卡实例持有，Reset/离关销毁后重建，不写跨局物理设置。镜头轻震仍在完成、Kill 与 Ctrl 销毁时恢复相机 localPosition；实际重建和相机跟随并发效果未运行验证。
13. **正式标题与投放未确认**：`tbleveldata` 已登记 10127，但玩家排序入口未接入（Context 边界），主线可达性 `uncertain`。

14. **关卡创建阶段 `BaseLevelCtrl` 获取为空（用户已提供运行证据，共享层保持上游不变）**：2026-08-26 用户提供的堆栈显示 `ResourceManager/YooAssetLoader.LoadAsync<GameObject>` 已成功返回，随后 `PlayCtrl.CreateLevelAsync` 在 `RefreshGamePanel` 的 `LevelCtrl.GamePlayPanelName` 表达式处 NPE；这说明 `LevelCtrl` 在进入面板刷新前为 null，而不是 `GamePlayPanel.Create` 返回 null。静态解析当前 `Level_127.prefab` 确认根节点有 `BaseLevelCtrl` 脚本 GUID `c946c0238f55b1d408febf481ab1d627`、`m_Enabled=1`、`gamePlayPanelName=GamePlayPanel_000`；因此具体原因仍 `uncertain`，可能来自运行时实例层级、资源导入/缓存或实际加载版本与工作区文件不一致。按用户要求，`PlayCtrl.cs` 已恢复到上游版本，本次不在共享层增加回退解析或空值防御；剩余证据缺口是修复 127 根节点/资源状态本身，或取得运行时实例检查日志来确定为何静态存在的组件未被解析。

15. **砖图尺寸与合成包围盒联动（P3）**：14 块实例中心间距仍为横向 `1.36`、纵向 `1.26`，顶部第 4 格留空。新版完整砖为 125×118、裂纹砖为 124×121 像素，100 PPU 不变；完整砖名义间隙变为横向 `0.11`、纵向 `0.08`，碰撞体仍为 1.32×1.22。第一击换图可能产生边缘变化，`formationBounds` 读取 Renderer Bounds，素材尺寸也会影响合成目标倍率；不通过改缩放/碰撞体静默补偿。透明边距、实际画面间隙、碰撞贴合与合成轮廓均未运行验证。

16. **固定隐藏星与已收集星位替代时序（P3）**：两个 inactive 标准模板仍固定绑定 `R02_C02`/`R03_C04` 的索引 1/2，保留原缩放、飞行 Trigger 与落地浮动；已永久收集时进入普通道具槽，当前局收集不追加掉落。引用、存档查询或浮动启用时序异常仍可能造成替代物缺失、重复星星或提前浮动，静态证据不能证明实际收集反馈。
17. **墙面 TMP/I2 占位文字的替换边界（P3）**：`Level127WallTextPlaceholder` 按 083 的根级 `Tips.prefab` 方式接入，当前 `mTerm=S_LevelTips_Des_level_83`，后续正式文字应只替换该实例 override。若误改为对话 key、修改共享 `Tips.prefab` 或把文字接入砖块点击，可能造成语义误读、跨关污染或输入冲突；TMP 字体、I2 Awake 刷新、世界空间位置和 SceneLayer/100 合成仍未运行验证。

- 共享 Prefab/脚本零改动：`Key.prefab`/`DoorLock.prefab`/`door_0.prefab`/`Actor.prefab`/`BaseLevelCtrl`/`ActorCtrl`/`SkinUtils`/`MoveFloatHandle.cs` 等均未修改；星星模板状态、普通道具碰撞隔离、自动浮动、拾取缩放和门链调整都只存在于 `Level_127.prefab` 实例覆写或本关 Ctrl。
- 14 砖实例共享同一 prefab 源，组件/碰撞体改动自动传播，无逐实例漂移；钥匙砖识别用实例名（Prefab 覆写保留），不依赖顺序。
- 严格合成条件在代码层显式短路（`keyBrokenThisRun`），不会出现"多碎后仍合成"的歧义行为。
- 已移除本关皮肤分配、异步加载、占位视觉、穿戴回调和 `Level127SkinDrop` 组件，不改变主角原有装备或商店/存档。
- 道具 layer `TriggerItem(9)` 不在 `ActorCtrl.whatIsStuff(256)` 内；普通道具保留 Ground 物理，排除 Actor 与本实例其它普通道具，不扩大到共享层。固定星星不使用普通道具倍率，保留标准 Postcard Trigger 链。

## 验证证据

- 命令与结果（静态）：
  - 本轮星星与文字实例核对：`Level_127.prefab` 保留两个标准 `Postcard_Star.prefab` inactive 模板，`Postcard_Star_Level127_01`/`02` 的索引分别为 1/2，分别通过 `Level127Ctrl.starTemplates` 绑定 `R02_C02`/`R03_C04`；模板根/`icon` 没有尺寸覆写。`Level127WallTextPlaceholder` 复用 `Tips.prefab`，`mTerm=S_LevelTips_Des_level_83`、`m_AnchoredPosition=(0,2.0)`、local scale `0.65`、SortingGroup 使用 `SceneLayer`。
  - 布局与资源尺寸：14 个砖实例位置保持横向 `-2.72/-1.36/0/1.36/2.72`（顶行跳过第 4 格）、纵向 `2.52/1.26/0`；新版完整砖名义尺寸为 `1.25×1.18`，名义间隙为横向 `0.11`、纵向 `0.08`。PNG 头尺寸解析及源/目标字节比较通过，两张图片与用户提供文件完全一致，复制前后 meta 字节不变；这些证据不代表 Unity 视觉验收。
  - 当前掉落池检查：一次性 `python3` 读取 Prefab、源码和 PNG meta，14 个 GUID 均不重复、各自唯一解析到存在的单 Sprite PNG；正常槽位为 `12+k≤14`，由去重候选洗牌后依次分配，无需循环复用。新增三张引用与 `skinParts` 删除之外的 Prefab 内容相对修改前 HEAD 字节一致，钥匙判定/合成与星星生成方法内容不变。
  - 当前碰撞调用链静态阅读：飞行期关闭模拟/Collider，落地启用后在同一回调内排除 Actor 并对实例列表中其它已落地普通道具调用 `Physics2D.IgnoreCollision`；后落地者负责补齐碰撞对。没有 `IgnoreLayerCollision` 调用，不修改 Ground 或星星/钥匙 Trigger。
  - `rg -n '7e0f9e5ce1034da2838578670d8c1a89|Level127SkinDrop' BrainPunk/Assets --glob '*.cs' --glob '*.prefab' --glob '*.unity' --glob '*.asset'`：无匹配，已删除皮肤组件无资产/代码残留引用。
  - 本轮音效核对：`AudioManager.PlaySound(synthesisSfxTag)` 的唯一调用位于 `RevealKeyAtTransitionPeak`，在 `SwapFormationForKey` 激活钥匙的同一回调执行，未切换 `musicInGame`；未传入音量参数，沿用默认 `0.4`。
  - 本轮碎裂特效核对：`Level_127.prefab` 序列化 `shatterVfxScale=1.2`，`shatterBurstCount=8` 保持不变；图集、材质和两个 Renderer 引用未改。
  - 当前 `git diff --check`：通过，包含本次 `Level_127.prefab` 差异；不将历史版本的空字段尾随空格检查失败当作当前结果。
  - 用户运行堆栈（2026-08-26）：直接支持“资源加载完成后在共享 `PlayCtrl.RefreshGamePanel` 的 `LevelCtrl.GamePlayPanelName` 处发生 NRE”这一事实；不支持具体资源导入/缓存原因，也不证明 127 侧修复结果。
  - `rg -n 'flowchart (LR|RL)|graph (LR|RL)' <127 Context/功能说明/风险评估>`：无匹配；文档 Skill 要求的 `rg -n '^#|^##|^###|uncertain|missing evidence|回滚|验证' <功能说明.md> <风险评估.md>` 有标题、证据、未验证和回滚段落。
  - 一次性静态解析脚本（非测试、未写入仓库）分别检查序列化字段、YAML anchor/fileID、C#/Shader 分隔符和 GUID：以下结果为脚本输出。
  - Unity YAML 结构化解析：`Level_127.prefab` 157 blocks，本轮新增 2 个星星模板 GameObject stripped block；`Level127WhiteFlash.mat` 1 block；目标文件均为 0 重复 anchor、0 悬空本地 fileID。
  - 当前序列化字段对齐：`Level127Ctrl` 42 个 `[SerializeField]` 字段与 Prefab 字段集合完全一致，缺失、未知和重复字段均为 0（已删除 `skinParts`）。
  - GUID 与资源链：`Level127WhiteFlash.shader` GUID、白化材质 GUID 与共享 `sparkle_ADD.mat` GUID 各只命中一个 `.meta`；Prefab→白化材质→Shader 与 Prefab→sparkle 材质引用闭合。白化 Shader fragment 只读取 sprite alpha 并输出白色 RGB，不再叠加原砖 RGB。
  - Key 与实例覆写：共享 Key `SortingGroup=SceneLayer/201`，运行时星光排序取 204；Key 源 `MoveFloatHandle` 与 `ItemScaleListener` fileID 仍存在，127 实例分别覆写为 `m_Enabled=0` 与 `targetScale=(0.25,0.25,0.25)`，Ctrl 的 `keyFinalScale=0.75`。
  - 数值静态计算：隐藏物有效最大边 `0.8×1.5=1.2`，标准星星不放大，钥匙回弹峰值 `0.75×1.1=0.825`；合成名义时序约 5.633s（1.2s 停顿、1.2s 闪白、约 1.333s 蓄力/收缩、0.3s 切换、0.3s 回弹停留、1.3s 下落）。旧版砖图对应的砖阵包围盒、收缩倍率与尺寸误差不再作为当前证据；新版运行时 Renderer Bounds 与合成结果未验证。
- 上述星星、布局、音效、Shader、YAML block 和名义几何记录为既有静态证据，本次只复核直接影响的掉落池、字段、调用链与差异边界；未修改 Mermaid 主流程。当前 `git diff --check` 通过，不将历史检查扩大为本次运行验证。
- 证据边界：以上只能证明源码、序列化值、引用闭合和名义几何关系；不证明 Unity 编译、资源导入、Tween 实际执行、碰撞回调、视觉观感或开门结果。

## 未验证 / 剩余风险汇总

运行时全部 `uncertain`：新增三张素材导入、透明边距与最大边归一化后的视觉大小，12/13/14 件普通道具分配，先后/同时落地后的互不碰撞、重叠遮盖、Ground 承托、主角穿行和 Reset 清理均无本次运行证据。此前关卡创建 NRE、GameplayPanel/Loading 状态、星星复制/收集/存档/浮动、文字显示、点击/跳撞、碎裂/合成/相机演出、钥匙落地/拾取/开门仍未在本次验证。按项目约束不执行编译、测试、探针或 Play 验证；扩大验证范围为 N/A。

## 回滚路径

本次取消皮肤、扩充掉落池与取消道具互碰：选择性恢复 `Level127Ctrl.cs`、`Level127SkinDrop.cs/.meta` 和 `Level_127.prefab` 的本次差异，同步对应 Context、路由关键词与文档对；不删除三张既有源素材、不回退其它关卡或共享碰撞设置。以下为此前独立功能的历史回滚边界，不是本次建议执行的回滚范围。

仅回滚本轮固定藏星、已收集星位替代、普通道具角色碰撞隔离与合成加速时：从 `Level_127.prefab` 移除 `starTemplates` 字段、两个星星模板的 `m_IsActive=0` 覆写及对应 GameObject stripped 引用，恢复两颗星作为原有开局场景实例；从 `Level127Ctrl.cs` 移除星星永久收集状态查询、固定星映射、星星掉落与 Actor Collider 排除逻辑，并恢复普通道具随机池、`synthesisFlashCycleDuration=0.6`、`synthesisScaleDuration=2`、`synthesisAnticipationDuration=0.12`；同步恢复本 Context、功能说明和本风险评估中的当前事实。不得修改共享 `Postcard_Star.prefab`、`Tips.prefab`、`I2Languages.asset`、Actor、PlayCtrl 或其它关卡玩法。

仅回滚本轮 127 砖阵间距、普通道具飞行碰撞、合成音效时机和碎裂粒子放大时：将 `Level_127.prefab` 的 14 个砖实例恢复为原横向中心位置 `-2.9/-1.45/0/1.45/2.9`、纵向位置 `2.82/1.41/0`，把 Prefab 与 `Level127Ctrl.cs` 的 `shatterVfxScale` 恢复为 `0.6`/`0.6f`；恢复 `Level127Ctrl.cs` 中合成音效的原前置调用，并撤销普通道具飞行期间对 `BoxCollider2D` 的显式关闭及落地刚体收口逻辑。同步恢复本 Context、功能说明与风险评估中的对应当前事实；不得回退此前已批准的 1.5 倍隐藏物视觉倍率、白化合成演出、钥匙 0.75 缩放或共享系统。

仅回滚此前 2026-08-26 合成演出与隐藏物尺寸调整时，恢复 `Level127Ctrl.cs`、`Level_127.prefab`，删除本关新增 `Level127WhiteFlash.shader/.mat` 及其 meta，并同步恢复 127 Context 与文档对；不得修改共享 `Key.prefab`、`sparkle_ADD.mat`、`PlayCtrl.cs`、DoorLock、Actor、SkinUtils 或其它关卡。`PlayCtrl.cs` 本次没有交付差异，保持上游版本。更早的整关回滚仍只涉及 Level 127 专属脚本、Prefab、素材、粒子资源与文档。
