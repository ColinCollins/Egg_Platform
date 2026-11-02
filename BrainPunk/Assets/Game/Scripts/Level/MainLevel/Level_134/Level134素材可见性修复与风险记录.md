# Level134 素材可见性修复与风险记录

## 2026-09-09：相机子节点 Square 被近裁剪面裁掉

- 需求：用户在 Level134 的 GameCamera 下加入气泡素材 Square，Scene 可见但运行时不可见，确认只调整其深度。
- 基线：`Assets/Game/Prefabs/Level/Level_134.prefab` 中 Square 的 Transform（fileID `4867557340486105637`）本地位置为 `(7.43, 1.77, 0)`，父节点为 GameCamera。相机沿用 `Assets/Game/Prefabs/LevelItems/GameCamera.prefab` 的近/远裁剪面 `0.3/1000`；Square 的相机空间深度为 0，因此在裁剪范围外。
- 修改：仅把 Square 本地 Z 改为 `10`，使其处于相机前方有效深度内。X/Y、缩放 `(2.3969836, 1.7832981, 2.3103456)`、父节点、贴图、材质、排序层与 Order 保持不变；用户已有的文字、门及 Actor 调整全部保留。
- 资源与行为边界：继续引用原气泡 Sprite（GUID `6dd2d289f019c204c9d55ddff918c67f`），不新增运行时查找、控制脚本或对象；不改共享相机、其它关卡、角色、碰撞、UI 生命周期、配置、存档、打点或平台流程。

## 验证与剩余风险

- 对修改前整个 Prefab 做仅替换目标 Z 的预期内容，再与修改后整个 Prefab 比较 SHA256（仅统一 CRLF/LF）：两者均为 `B8C952885B956E73662575417C4F53D42BD21EC8945EF9199941C168D2C5568A`，确认本轮无其它内容变化。
- 静态检查：Square 本地 Z 为 10，满足 `0.3 < 10 < 1000`；正交相机下仅改变深度不改变其 X/Y 投影和尺寸。没有改 C#，不需要重新编译逻辑。
- `git diff --check` 提示 Prefab 第 3799 行用户已有门覆盖中的空 `value:` 行末空格；该行已存在于本轮修改前，按保留用户改动的原则未处理。本轮只改 Z 的范围由上述全文预期哈希确认。
- 未操作 Unity PlayMode，实际层级遮挡、不同宽高比及 Android/iOS 显示为 `uncertain`。验收：重新进入 Level134，检查气泡可见，X/Y 和尺寸保持预期；攀爬时跟随相机。若后续要求气泡与按宽高比重定位的文字/门始终对齐，需单独确认适配范围，本轮不扩展脚本。
- 回滚：仅将上述 Square Transform 的本地 Z 从 10 改回 0；会重新产生近裁剪问题。不可整份恢复旧 Prefab，以免覆盖用户已有摆放。

## 2026-09-09：吸入门时距离文字提前消失

- 需求与基线：用户反馈 Actor 开始被门吸入时，气泡仍在，但文字瞬间消失。`TryEnterDoor` 在标准门判定通过后调用 `BeginDoorHandoff`，后者直接执行 `rewardProgressText.gameObject.SetActive(false)`，并非气泡遮挡或素材深度再次出错。
- 修改范围：只移除 `Level134Ctrl.BeginDoorHandoff` 末尾对文字的提前隐藏，补充中文说明。文字保持当前内容及可见状态，与气泡留在原相机下，随原关卡退场统一消失；不增加渐隐、缩放、计时器、全局事件或运行时对象查找。
- 原流程保留：门判定、Actor 停止物理并交给吸入动画、台阶暂停、相机冻结、Finished 状态、输入清理不变。已核对 `DevelopMD/关卡打点生命周期约定.md` 第 2.3 节，本次不改变成功次数、事件发送顺序、统计清理或胜利界面生命周期；没有修改共享成功脚本、其它关卡或 Prefab。
- 验证：源码差异仅为删除提前隐藏的四行并增加注释；Prefab SHA256 保持 `E391506C22583E4CC23764AB726BFF151A1063E04DBF30472635E9C4729F8023`，没有覆盖用户新增摆放。定向差异检查通过。
- 编译：系统 `dotnet build` 因仅安装运行库、缺少 SDK 而未执行；改用 Unity 6000.0.59f2 的 `NetCoreRuntime/dotnet.exe` 与 `DotNetSdkRoslyn/csc.dll`，读取当前 `Library/Bee/artifacts/1300b0aE.dag/Assembly-CSharp.rsp`，并将 dll/ref/pdb 输出重定向至 `E:/WorkSpace/.codex-temp/level134-door-text/`。完整运行时编译退出 0，0 个错误、110 个警告，Level134 诊断为 0；未覆盖 Unity 自身编译产物。
- 剩余风险与验收：尚未重新操作 PlayMode，普通门与最终门吸入、胜利界面覆盖、Replay 及 Android/iOS 为 `uncertain`。需确认吸入开始和全过程不再出现空白气泡，原门动画/角色移动继续完成，离关后无文字残留。
- 回滚：只在 `BeginDoorHandoff` 原位置恢复文字非空判断及 `SetActive(false)`，会恢复提前消失表现；不要回退 Square 的 Z 或整份 Prefab。

## 2026-09-09：砖块放大 1.2 倍并同步关联尺寸

- 需求：本关全部砖块按当前尺寸放大至 1.2 倍，关联碰撞、台阶路线、跳跃和接载也需匹配；不放大角色、门、气泡或背景。
- 修改范围：`Level134Ctrl.cs` 的默认参数及 `Assets/Game/Prefabs/Level/Level_134.prefab` 中本关控制器序列化参数同步修改。实际运行优先使用 Prefab 参数，不能只改源码默认值。

| 参数 | 修改前 | 修改后 |
| --- | --- | --- |
| `stepScale` | `(1, 1, 1)` | `(1.2, 1.2, 1.2)` |
| `horizontalStepDistance` | 1.28 | 1.536 |
| `verticalStepHeight` | 0.57 | 0.684 |
| `smallJumpArcHeight` | 1.22 | 1.464 |
| `firstStepLandingTolerance` | 0.45 | 0.54 |
| `carrierHorizontalTolerance` | 0.65 | 0.78 |
| `carrierVerticalTolerance` | 0.36 | 0.432 |

- 自动关联链路已核对：`AcquireStep → Level134Step.Initialize` 对新砖及池复用统一设置缩放；原 BoxCollider2D 的局部尺寸与顶部偏移随 Transform 放大，不重复乘尺寸。站立点使用 `step.TopY + actorFootOffset`，最终门落点使用 `currentStep.TopY - triggerBottomOffset`；拖拽拾取、屏幕夹取、底部回收按实际 VisualBounds/Collider Bounds 计算。因此无需另改 Step、相机跟随或共享资源。
- 小跳仍用现有解析抛物线按下一块实际落点、弧高及角色重力计算速度；动画阶段时长沿用该计算结果。常规路线、最终段和高空坐标重定位继续使用统一 `verticalStepHeight`，未另写高度常量。
- 保持不变：首块中心 `(-1.41, -1.41, 0)`、路线安全半宽 5、重力、屏幕及回收边距、贴地/顶部穿透容差、10000 层目标、对象池、时间及状态机。事件、成功/失败打点、资源引用、存档与平台流程未改；共享 Actor、GameCamera、砖块 PNG 和导入参数未改。
- 用户已有摆放以本轮开始时文件为准。当前 Square 已不再使用第一节历史局部坐标；本次没有将其恢复到旧坐标，也没有覆盖用户的父节点、旋转、缩放或门/文字摆放。

### 验证、风险与回滚

- 执行 `E:/WorkSpace/.codex-temp/level134-step-scale/Validate-StepScale.ps1`：检查七项参数及默认值一致、只放大一次、碰撞顶部几何关系、实际 Bounds 引用、小跳解析落点及下降着陆，结果 PASS。实际 PNG 为 130×57、100 PPU，放大后的完整像素包络为 1.56×0.684 世界单位；该包络用于保守验证，不声称替代 Unity 导入后的原生 Bounds。
- 检查全部六个内置路线模板和安全回退模板；在 4:3、16:9、2340:1080、2520:1080 下各计算一条 10000 层完整路线，共 40000 个位置。最大中心横向距离为 4.734，小于原安全半宽 5，完整砖块包络也未超出屏幕边距。小跳按现有重力解析飞行约 0.4455 秒；上述是静态/数学验证，不是运行 10000 层的 PlayMode 实测。
- 使用 Unity 6000.0.59f2 自带 Roslyn 与 `Library/Bee/artifacts/1300b0aE.dag/Assembly-CSharp.rsp` 编译，输出 dll/ref/pdb 重定向到上述本地临时目录。退出 0，0 错误、110 个警告，Level134 诊断为 0；未改写 Unity 编译产物。
- Prefab 和 Ctrl 全文与本轮基线仅替换七个目标字段的预期 SHA256 一致（统一 CRLF/LF），分别为 `EF4D132525E5994A9EC3280953CDA6054E6321A97537756B7DD6CEC096C9219E`、`7D0990A3757E7A1ED6385C70A2D4C2C974075D623CEADA1608357C35ADDE69AE`。Step、相机跟随、共享 Actor/相机、砖块 PNG/meta 六项原始哈希未变。
- `git diff --check` 仍报告 Prefab 第 3800 行已有空 `value:` 的行末空格；本轮目标字段以外的内容经全文哈希确认未变，未清理用户已有改动。
- 待验收（`uncertain`）：Unity PlayMode 的首块起跳、连续左右小跳、下落接载、边缘拖拽、落地、最终门与 Replay，以及 Android/iOS 触摸和实际遮挡。尤其检查更大的砖块不会挡住角色或门，角色落脚稳定且终点交接正常；静态检查不替代真实物理接触验证。
- 回滚：只把上表七项在源码默认值和本关 Prefab 中恢复为修改前值；不要整份恢复文件，以免丢失用户摆放、文字吸入修复及其它既有改动。本轮未改资源、全局参数或共享物理设置。

## 2026-09-09：进度多语言适配位置占位符

- 需求与基线：用户已将 S_LevelTips_Des_level_134_3 改为 {0}/{1}，要求保留该拼接方式；原 Ctrl 写 currentLayer/totalLayers 命名参数，I2 默认仅替换 {[参数名]}，无法填入当前词条。
- 修改范围：Level134Ctrl.cs 与 Level_134.prefab。保留现有 Localize 组件和词条，在 Inspector 显式绑定 rewardProgressLocalize，并把 LocalizeEvent 绑定到 FormatProgressTranslation；删除本关不再使用的 LocalizationParamsManager 组件及命名参数代码。未修改用户已有 I2Languages.asset、localization.csv 或任何共享脚本。
- 当前流程：初始化和原有层数变化入口调用 Localize.OnLocalize(true)；I2 获取翻译后通过本关回调用 string.Format(InvariantCulture, template, currentLayer, TotalLayers) 填入数字，再由 I2 完成排版与 TMP 赋值。语言切换自然复用该回调，不新增全局订阅、逐帧刷新或 Reset 清理。公共回调仅在 Application.isPlaying 时格式化，不新增编辑器数据访问。
- 数字含义：沿用原逻辑的当前层数/10000，未擅自改为剩余层数。中文词条的“还剩”与当前层数表达存在语义差异，产品文案/数字含义待用户确认；本次只修复占位符适配。
- 验证：Unity 6000.0.59f2 自带 Roslyn 使用现有 Assembly-CSharp.rsp 编译，dll/ref 输出至系统临时目录，退出码 0；12 个非空翻译分别格式化 0、1、9999、10000，均得到对应当前层数/10000。Prefab diff 确認 Localize、Ctrl 引用及无参运行态回调相互对应；git diff --check 通过。
- 风险与未验证：实际 PlayMode 层数变化、切换语言、门吸入期间文字、重开和 Android/iOS 布局仍为 uncertain。翻译必须保留合法 {0}/{1}；未增加吞异常或覆盖中文的兜底。I2 组件和既有排版管线继续负责字体/布局。
- 回滚：仅撤回本次 Ctrl 和 Prefab 的本地化字段、回调与组件差异；不要撤回用户翻译资源。旧实现恢复后与 {0}/{1} 词条仍不匹配。
