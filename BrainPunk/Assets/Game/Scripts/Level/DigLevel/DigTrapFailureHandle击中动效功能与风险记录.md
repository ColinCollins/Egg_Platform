# DigTrapFailureHandle 击中动效功能与风险记录

## 2026-10-15 公共失败基类

- 后续追加：FailArea默认跳过角色死亡上跳、直接下坠；Trap覆写PlayDeathHop=true，保留原上跳再下坠。45项隔离轨迹／物理／结果检查和Editor／Android编译通过；详见失败条件记录“FailArea跳过死亡上跳”章节。

- DigTrapFailureHandle现继承可直接挂载的DigFailAreaHandle。公共字段level／trapCollider和物理接触判定移入基类，原字段名／脚本GUID保留；Trap只覆写失败接受后的Knock／trap音效，原OnDisable粒子清理保留。未修改生产Trap资源。
- 不需要区域音效和Knock的FailArea直接挂基类；角色统一死亡／失败重开仍由DigLevelCtrl负责。原生SharpTrap字段读取与26项真实物理／粒子隔离检查通过，Editor／Android编译0错误；正式混音和完整关卡仍uncertain。用法、证据及回滚见同目录《DigGame失败条件功能与风险记录》本日章节。

## 2026-10-14 主线同款 Trap 音效：修改前核对

- 需求与验收：DigGame Actor 首次有效触碰 Trap 时播放与主线陷阱一致的音效；Trigger／Collision 的 Enter、Stay 不能重复播放，外来对象、暂停／后台、已到达 Actor、已有胜负结果或无效失败请求不能误播。原 Knock 命中点、Actor 死亡、约 0.5 秒失败提交和重建链保持。
- 一手代码证据：主线 `Assets/Game/Scripts/Level/MainLevel/Level_27/OnTriggerTrapHandle.cs` 的 `PlayKnock` 在首次死亡门禁后调用 `AudioManager.PlaySound("trap")`；`Assets/SoundsGood/Data/Collections/SoundCollection.asset` 已有唯一 `trap` 标签并绑定现有 AudioClip。Dig 当前 `DigTrapFailureHandle.ReportContact` 只有 `level.TryFail(failingActor)` 接受后调用 `PlayKnock`，没有音效。
- 拟修改位置与理由：仅为 Dig 陷阱脚本引用现有 `Game.Scripts.Common.AudioManager`，在 `TryFail` 返回 true 后与 Knock 粒子同帧调用 `PlaySound("trap")`。复用统一失败门禁即可保证一次播放，不另加布尔状态、序列化字段、音频资源、全局事件或运行时查找；继续尊重玩家全局 SFX 开关。
- 影响和验证：不修改主线 Trap、SoundCollection、AudioManager、Prefab、Actor 或失败时序。静态核对调用只位于成功分支；执行 Editor／Android 条件编译，并用隔离运行桩验证接受一次、Stay 重复拒绝、不属于本关／暂停／结果锁均不播放。正式混音听感、完整重建和 Android／iOS 真机仍为 `uncertain`。CodeGraph 索引存在但本机 CLI 不可调用，037 feature plan 路径缺失，本次以当前源码、资源和既有 Dig 陷阱文档补足依据。
- 回滚：只删除 Dig 陷阱脚本的 AudioManager 引用和单次 `PlaySound("trap")` 调用，保留原粒子、Collider、失败 API 和用户关卡配置。

### 实施结果与验证

- `DigTrapFailureHandle` 仅在 `level.TryFail(failingActor)` 返回 true 后，先播放原 Knock 粒子，再调用既有 `AudioManager.PlaySound("trap")`。音效与主线 `OnTriggerTrapHandle` 使用同一标签、同一 `SoundCollection` 资源和全局 SFX 开关；没有新增 Inspector 参数、音频资源或生命周期状态。
- `TryFail` 是本关唯一结果门禁，因此首次有效 Enter/Stay 只接受一次；后续 Stay、暂停／后台、已到达 Actor、外来对象和已有胜负结果均不会进入音效调用。未绑定 Knock 粒子时，失败和音效仍正常执行。
- Unity 6000.0.59f2 隔离 Physics2D／DOTween／ParticleSystem 共 96 项通过；其中新增 4 项音效计数验证覆盖首次 `trap` 一次、Stay 不重复、暂停拒绝不播、胜利锁后不播。计数使用等价 AudioManager 桩，只证明调用条件与标签；真实 SoundsGood 混音听感仍需主工程试听。真实项目引用下 Editor／Android 条件编译均为 0 错误。
- 没有修改 `DigGame_SharpTrap.prefab`、主线 Trap、`SoundCollection.asset`、`AudioManager`、Actor、失败时长或重建链。详细证据见 `DevelopMD/验证记录/Dig陷阱音效_2026-10-14/验证记录.md`；回滚边界沿用上节。

## 2026-10-13 修改前核对

- 用户需求：DigTrapFailureHandle在击中角色时播放一个knock动效。验收包括有效失败首次播放、正确落点、Trigger及普通碰撞、子Collider、拒绝／重复接触不播放、禁用／重建清理、原死亡和结果链保持。
- 原流程／维护边界：脚本以同节点trapCollider和attachedRigidbody中的DigActorCtrl上报DigLevelCtrl.TryFail；本关参与／未到达／非暂停后台／非锁结果才接受，BeginFailure关闭Actor CircleCollider并启动0.5秒死亡，结束后玩法原统计／广告／重开。没有原粒子字段或生命周期逻辑。新增定位须在TryFail关闭碰撞体之前取点，避免读取到失效bounds。
- 参考证据：DrawLevel/Level4/DLevel4RockTrapHandle.GetContactPosition／PlayKnock和石头碰撞特效说明；Level32／Level61局部脚本使用ParticleSystem.Stop(Clear)、移动位置、Play(true)，禁用清粒子。复用Assets/Game/ArtSrc/Particles/Prefabs/Knock_Explosion.prefab（GUID972fa0cfdd6f5e04a8c9fde50e1bb924），根及子系统均PlayOnAwake=0、looping=0，排序210／105，原共享资源只读复用。
- 当前资源：用户已有但尚未跟踪的DigGame_SharpTrap.prefab只含根Sprite、PolygonCollider、DigTrapFailureHandle和BaseDigComp，没有子动效；level在资源中为空，由关卡实例显式绑定，trapCollider已绑同节点。仅本源资源新增nested Knock_Explosion并把ParticleSystem明确绑给脚本；保留原GUID、根姿态／Collider路径、出生洞和level引用，不重写001～005关卡或Actor。源共享粒子、Draw、HUD／计时、数据／统计／SDK／Packages／GF不改。
- 拟改：可选SerializeField ParticleSystem knockEffect；Failure返回true后Stop清／移动XY且保留现有Z／Play(true)。Collision使用GetContact(0).point，无contact及Trigger用trapCollider.ClosestPoint；位置先读取再TryFail。OnDisable只在Application.isPlaying下StopAndClear。本陷阱独占子粒子显式绑定，不Find、不扫层级、不开启全局事件或运行时Instantiate；没有knockback、音效或失败延迟改动。
- 验证：Unity隔离真实ParticleSystem／Physics2D／DOTween检查播放、实际点／Z、重复／无效／已到达／暂停后台、禁用清粒子、子系统及旧结果回归；native PrefabImporter／SerializedObject确认nested来源、字段、PlayOnAwake／loop、Collider和原字段；主程序集Editor／Android条件编译，定向资源块对比。正式关卡真实渲染、材质／排序／视觉观感、广告／存档、双端横屏及性能仍uncertain。CodeGraph不可调用、037 plan仍缺失，用当前源码及模块说明补足依据。
- 回滚：定向撤粒子字段／播放／停止和Collision坐标转发，移除SharpTrap新增nested实例／Transform child／字段绑定；保留原TryFail、Collider和用户并行美术／关卡。快照保存在TEMP before；不整文件回滚。

## 当前使用方法

DigGame_SharpTrap.prefab已内嵌一份既有Knock_Explosion，并把根ParticleSystem绑定到DigTrapFailureHandle的Knock Effect；在实际关卡实例中仍需将Level拖入本关DigLevelCtrl。没有改共享粒子本体、关卡布局、Actor或失败时长。

其它自建陷阱在Inspector的Knock Effect拖入本陷阱独占的ParticleSystem实例，关闭Play On Awake／Loop；不要直接拖粒子Prefab资产。可以为空，空时保留原失败。建议粒子为陷阱子节点，重建随旧关一起销毁。

正常碰撞用第一处contact；Trigger用陷阱Collider对角色bounds中心的ClosestPoint。必须在TryFail关闭角色Collider前取点，接受失败后才StopAndClear→移动XY（保留Z）→Play(true)。重复Enter／Stay、暂停／后台、外来／非Actor、已到达者以及其它胜负已锁时均不播放；脚本或关卡禁用时清掉父子粒子。沿原0.5秒死亡与结果／重开链，不新增音效、击退、延迟或全局事件。

## 实际验证、并行改动与风险

- Unity6000.0.59f2隔离92项通过：原62项结果回归＋30项粒子／资源检查，实际Physics2D／ParticleSystem／DOTween和PrefabImporter。包括nested来源和字段、初始不自播、旧Collider数据、Trigger／Collision接触点及Z、子Collider／子粒子、拒绝／去重、禁用清理和原0.5秒一次Failed。
- 主程序集1222份源码独立条件编译：Editor 0错误／113警告，Android 0错误／118警告；iOS仍2处既有Guru.UniKit.Apple引用缺失，不改只读SDK。定向diff检查通过；陷阱源码、当前SharpTrap与共享Knock资源和实际测试副本哈希相同。完整证据在DevelopMD/验证记录/Dig陷阱Knock_2026-10-13/验证记录.md。
- 实施期间SharpTrap又发生工作区重存，根scale从1变为0.8064517，nested引用ID重写。归属uncertain，保留最新值；没有用旧快照覆盖这份配置。对当前资源重跑92项；静态原6块只变化Transform／Trap脚本2块、其它4块逐字保持，但变化的Transform含上述并行scale，所以不宣称完整资源只改了本任务。原meta／GUID、Collider本地路径、根位置／旋转保持，共享粒子源不改。
- P1／uncertain：正式关卡的材质／颜色、深度／Sorting、子文字观感及死亡叠加、广告／存档／包加载、Android／iOS横屏／性能尚未实测。NativeImporter和粒子播放状态不是最终视觉验收；当前整体缩放由制作人按关卡效果复核。

## 局部回滚

只撤脚本粒子字段／播放／停止及Collision点转发，删除SharpTrap的Knock nested实例／child／字段绑定。保留当前根缩放、Collider、level、BaseDigComp及meta；不整文件恢复旧资源或改共享Knock源。事前快照用于核对，不能覆盖并行编辑。
