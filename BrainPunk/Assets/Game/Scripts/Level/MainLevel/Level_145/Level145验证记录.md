# 正式关卡 145 验证记录

日期：2026-09-17。证据位于 `E:/WorkSpace/BrainPunk/Temp/Level145/`。

## 编译

`compile.py` 使用 Unity 6000.0.59f2 自带 `dotnet.exe / csc.dll`，以当前 Bee `Assembly-CSharp.rsp` 补入本关三个新脚本，产物只输出 Temp。最终退出码 0，0 error，112 条已有工程 warning，145 无诊断。原工程的程序集目录与构建配置未改。输出：`compile.rsp / compile.log`。

## 静态资源

`verify_static.py` 的 356 项检查通过（`static-results.json`）。

| 资源 | 唯一对象定义 | 本地引用 |
| --- | ---: | ---: |
| Level_145 | 389 | 2568 |
| GamePlayPanel_145 | 33 | 236 |
| Level145Dog | 26 | 170 |
| Level145DoorBreak | 8 | 100 |
| Level145Hit | 8 | 77 |

无重复定义或悬空本地引用。新绑定类型、资源路径、狗副本外部 fileID、point_hit 与 025 动画名有效。Scene 原有 10 个对象块发生计划内修改，其余原有对象块与原稿一致；HUD 原本地对象保持 000 基线，新增按钮使用独立 ID。066 狗移动组件完整保留，113 英文粒子关闭、爆点缩放 1.35；088 碎门为 18 片且 Clouds 关闭。原钥匙执行器的飞行、解锁、启用出口、销毁引用保持原稿。

## 隔离 Unity 与截图

历史验证更正：本节及下文“截图反馈后的表现验证”使用的旧 Actor 测试桩在 Awake 初始化刚体，漏掉正式 ActorCtrl.Start 的初始化时序，不能证明动态出狗在正式环境中正常追击和碰撞死亡。该部分结论以末节“狗追击与碰撞死亡修复”的真实 Actor 运行结果为准；其它检查的桩边界保持原记录。

`unity-check` 使用同版本 Unity、原 URP、真实本关 Prefab/资源、三个本关脚本、Spine、DogEnemyCtrl、PuppyEnemyCtrl、MoveFloatHandle。共用 Actor 移动、BaseLevelCtrl、输入/音频/存档服务及钥匙 TriggerExecutor 为最小测试桩，因此验证边界是本关状态和真实资源/组件接口，不是完整主游戏。

`Level145Probe.Run` 的 80 项检查通过：关键 Inspector 引用、初始隐藏、按钮单次释放、单选/收绳、落地位置、左木槌自动拾取与挂点、中间两颗原星星、右狗等待/追击/跟跳及原 PuppyEnemyCtrl 的实际碰撞死亡、暂停、锁 +/-15 度、左门破坏而不启用出口、两次点击藏钥匙、钥匙在角色后方渲染、钥匙一次接触拾取、持槌保持、重建和不选盒子直接解谜。补查出生演出期间不抢占角色，以及通用恢复移动不能解开本关动作中的跳跃锁。木槌和钥匙接触检查把角色保持在原稿地面根高度 Y=-2.07，仅改变横坐标，不把角色抬到道具中心代替地面拾取。逐项结论见 `runtime-checks.txt / runtime-complete.log`。

已查看 `hanging.png / landed-left.png / hammer-held.png / birth-door-broken.png / key-out.png / stars-out.png / dog-out.png`。初次隔离截图因缺原 URP 未渲染 Sprite，不作为有效画面证据；补齐原渲染资源后重拍。此轮画面复核发现木槌初始复制了盒子子图的 Default 图层，改成 Stuff/201 后重拍确认左右朝向可见。截图里角色位置由探针摆放，不作为真实走路或跳跃体感证据；出生门最终图仍保留门框，与 088 最终资源一致。

`Level145HudProbe.Run` 加载真实私有 HUD，检查新增敲击键四角处于 16:9 视口内；`hud.png / hud-check.txt` 确认木槌图标位于原跳跃键上方、移动/跳跃键保留。截图只在隔离项目把 CanvasGroup 调为可见，不修改生产面板入场动画。

## 未验证（uncertain）

正式主游戏加载/HUD 连续触摸、通关结算、真实存档与星星去重、音效听感、失败重开、广告返回、Android/iOS 和 Safe Area 未实测。原钥匙正常开锁链已保留且静态核对，但隔离执行器只记录调用次数，不能将其写成真实主游戏开锁过关已通过。完整开盒粒子遮图的动态观感仍需正式试玩。

## 追加 Data / Sort 配置验证（2026-09-17）

用户要求补入 data 和 sort 后，已登记 `LevelData: 10145 → Level_145` 和 `LevelSort: 138 → 10145`，紧接 144；总星数 3、通关星数 1、lockType=1。未改本地化文本或远端排序。

证据目录：`E:/WorkSpace/outputs/level145-config-20260917/`。

- Artifact Tool 编写两张源表的新行，并复核修改前后渲染。保留原工作簿其它 ZIP 部件，逐单元格核对所有旧值、类型和样式一致，见 `workbook-validation.json`；未检出公式错误。
- `dotnet E:/WorkSpace/BrainPunk/Luban/Luban/Luban.dll -t client -d json --conf E:/WorkSpace/BrainPunk/Luban/Configs/luban.conf -x outputDataDir=E:/WorkSpace/outputs/level145-config-20260917/generated` 退出码 0，见 `luban.log`。生成产物只有两份目标 JSON 安装到生产配置目录。
- `install_configs.py` 验证新旧记录、ID 唯一、排序尾项与 Prefab 路径，写入前确认目标文件未被并行编辑。Data 原 138 条记录、Sort 原 137 条记录全部逐项一致；新总数分别为 139 / 138。安装后字节与 Luban 产物一致，见 `config-validation.json`。
- 此次只增加配置，未重跑已有玩法测试；上文正式主游戏、远端排序生效与移动端验证缺口仍保留。

## 截图反馈后的表现验证（2026-09-17）

证据目录：`E:/WorkSpace/outputs/level145-polish-20260917/`。用户要求木槌倾斜右移、修正层级与挥击方向、密码交互键位置、碎门方向、钥匙中间落点，并增加缩小抖动/弹性开箱和星星、关门彩纸表现。

- `compile.log`：完整 Assembly-CSharp 编译 0 错误，112 条原有警告，本关无诊断。
- `static-results.json`：369 项检查通过，包含新彩纸资源引用、所有本地对象 ID 与外部资源、原钥匙执行链和狗参数保持。相对此次修改前，Level_145 只改六个对象块，HUD 只改敲击按钮一个 RectTransform。
- `runtime.log / runtime-checks.txt`：真实 Unity 6000.0.59f2、实际本关脚本/Spine/粒子/物理资源检查通过；包括缩小、暂停、放大超调与复原、实际星星和彩纸粒子存活、倾斜木槌在正常地面高度自动拾取、角色穿盒、左右挥槌蓄力/接触、碎门、中央钥匙，以及原三盒单选、狗死亡、暂停、摇锁、拾取与重建回归。新增缩放动画使开箱时间增加，旧探针的出物等待相应延长。`final-verification.json` 中去重后 85 个检查标签通过；重复的逐帧及多盒检查保留在原始记录中。
- 已查看 `opening-shrink / opening-pop / hammer-out-after / actor-box-overlap / hit-right-windup / hit-right-contact / hit-left-contact / birth-door-after / key-center.png`。先用角度采样复现原 hit 木槌反向，再以握柄 180° 校正并复查左右动画；礼盒后置、倾斜道具与彩纸画面正确。场景截图是隔离画面，右门预览动画由该资源默认状态播放，不能用作正式主游戏开锁验收证据。
- 161 个受保护文件哈希未变，含两张 Excel、运行时配置、其它关卡、共享 Actor Prefab/脚本和共享 HUD。此次未改 Data/Sort。
- HUD 首轮隔离截图缺少全局 Canvas 缩放，出现上下裁切，单次渲染后的视口检查不能作为通过证据。按 `UIManagerCanvasScalerFit` 的 1920×1080 参考尺寸，在验证场景以 1600×900 重拍后，敲击键与跳跃键同排、位于其左侧且完整处于视口内；最终证据为 `hud-scaled.log / hud.png / hud-check.txt`。生产 HUD 只对齐密码键 RectTransform，未改全局 UI 缩放。
- `uncertain`：正式主游戏触控、全部角色皮肤、真实音频/存档/结算与 Android/iOS 仍未实测；相关服务沿用原隔离桩。回滚基线见本目录 `before/`，仅回退此次变更并移除新增彩纸 Prefab，保留后续人工编辑。

## 两颗星星落点外移（2026-09-17）

用户截图显示中盒打开图遮住星星内侧。仅将 `Level145Ctrl.OpenGift` 的星星横向偏移从 ±1.35 改为 ±2.2，其余代码和关卡 Prefab 保持此次修改前一致，收集碰撞体、星星大小/高度、存档 index 不变。

证据位于 `E:/WorkSpace/outputs/level145-stars-20260917/`：完整编译 0 错误/112 条既有警告；实际资源隔离运行截图 `stars-spaced.png` 确认两颗星星完整露出、与箱体两侧有空隙。首轮截图后的诊断错误地从星星根节点读取 SpriteRenderer，修正隔离探针为读取子节点；未因此改动生产组件。正式主游戏收集/存档与真机仍沿用上文未验证边界。本次不重跑无关玩法；回滚只需恢复横向偏移，代码原样备份为 `Level145Ctrl.cs.before`。

## 狗追击与碰撞死亡修复（2026-09-17）

用户反馈右盒狗只有叫的动作，触碰不死亡，并要求取消蝴蝶结。证据目录：`E:/WorkSpace/outputs/level145-dog-20260917/`。

- `formal-error-before.txt` 摘录正式 Editor.log 的 RevealDog 空引用。真实 ActorCtrl 在 Start 缓存 RB，旧代码 Instantiate 后同帧访问 RB 导致出盒协程中断，物理保持关闭、dogChasing 不会置为 true。将隔离项目换成字节一致的生产 ActorCtrl 和 ActorSpineCtrl 后，旧代码复现同一错误，见 `baseline-checks.txt / baseline.log / before-stuck.png`。
- Level145Ctrl 在激活狗后等待一帧，让 Start 初始化完成再访问 RB；暂停状态切换只修改已经进入追击状态的狗。私有 Level145Dog 两处 idle/run 蝴蝶结保持 inactive，DogEnemyCtrl 对应引用置空，防止动画切换重新打开。未改共享脚本及 066 资源。
- `fixed-checks.txt / fixed-final.log`：真实 ActorCtrl、ActorSpineCtrl、DogEnemyCtrl、PuppyEnemyCtrl、本关脚本和原物理设置实际运行。生成时暂停安全；出盒落地等待后以真实刚体追击；玩家移到另一侧会转向；角色跳跃回调触发真实狗跳跃；追击暂停冻结、恢复后继续；双方自然碰撞触发真实角色 Die、击飞速度、旋转和原击打粒子；完整销毁重建后再次正常出狗。检查期间未发生运行时异常。
- 已查看 `chasing-no-bowknot.png / collision-death.png`，确认奔跑时无蝴蝶结，碰撞角色出现死亡表情、旋转和撞击爆点。`dog-jumping.png` 留存跟跳截图。初次修复验证误断言狗的 Body 应非空；狗实际使用序列化 SpineCtrl，而玩家 Body 已正确初始化，仅修正探针断言后重跑，未为测试改动生产共享 Actor。
- `compile.log`：完整 Assembly-CSharp 编译 0 错误，112 条既有警告，145 无诊断。`static-validation.json`：两处蝴蝶结对象和控制器共三个 Prefab 对象块变化，其余狗组件参数不变、引用闭合；10 个保护文件保持，生产真实 Actor/Dog/Puppy 源码与验证副本一致。
- 验证限制：UI、音频、存档、BaseLevelCtrl 主流程及事件转发仍由桩隔离；探针调用本关跳跃回调，不代表正式 HUD 输入整链验收。日志保留 Shadow 脚本未拷入隔离项目的提示及退出时 TMP 资源提示，不宣称整个日志零错误。主游戏失败重开调度、皮肤、声音和移动端仍未实测。
- `before/` 保存两个生产修改文件的此前原样副本，`Level145DogProbe.cs` 留存最终探针。回滚仅还原本次代码/蝴蝶结差异，保留星星落点、其它表现和配置。

## 礼盒前景、红框木槌和狗等待（2026-09-18）

以用户本次两张截图及文字为准：礼盒在前，木槌飞向红框并上下浮动，狗落地面向主角持续叫，主角一动才追逐。生产修改仅 `Level145Ctrl.cs` 和 `Level_145.prefab`；证据目录为 `E:/WorkSpace/outputs/level145-revision-20260918/`。

- `compile.log / compile.rsp`：使用当前主工程 Bee 响应文件与 Unity 自带 Roslyn，最终完整 Assembly-CSharp 编译退出码 0，0 错误、112 条既有警告，145 无诊断。编译产物只写证据目录。
- `static-validation.json`：Prefab 仅控制器新增木槌参数和三个礼盒 SortingGroup，共四个对象块变化；对象 ID 与本地引用保持闭合。066、共享 Actor/Dog/Puppy、私有狗及 Data/Sort 等 10 项保护文件哈希不变。7 份真实运行脚本与隔离副本字节一致。
- 昨日 Temp 验证工程已不存在，本次在 outputs 重建同版本 Unity 隔离工程，复制真实 ActorCtrl、ActorSpineCtrl、DogEnemyCtrl、PuppyEnemyCtrl、本关控制器与角色子类，使用实际关卡、Spine、粒子和 Physics2DSettings。`Level145RevisionProbe.cs / fixed-checks.txt / runtime.log` 留存 39 项通过记录。
- 盒子/木槌：三盒排序都高于角色和木槌；Sprite 中心落在截图红框的视口范围；完整浮动周期上下总跨度约 0.24，横向不漂移；暂停冻结、恢复继续；角色在原地面高度接触能自动拾取并挂到 point_hit，拾取后浮动停止。
- 狗：落地后朝向角色并播放 bark 与原叫声表现粒子；主角静止超过原 1 秒时限仍在原地叫；点击角色晃动不追逐；暂停输入不触发；左右输入启动后切换 run、恢复物理并关闭叫的粒子，松手继续追逐；转向、跟跳、自然碰撞 Die/击飞/旋转及重建均通过；从静止直接跳跃也能立即开始追逐并带动狗跳跃，蝴蝶结保持隐藏。
- 已查看 `gift-in-front-closed.png / gift-in-front-held-hammer.png / hammer-in-red-frame.png / dog-barking-wait.png`，确认闭合与打开箱体遮挡角色及持槌、木槌中央偏右、狗面向左侧主角叫的表现。另留存追逐与死亡截图。首轮导入期间同步脚本引发域重载、中断探针，日志保存为 `runtime-import-interrupted.log`，不作完成证据；固定最终源码后重新启动验证，以上结论来自完整结束的 `runtime.log`。
- `uncertain`：隔离 HUD、音频、存档、事件派发和 BaseLevelCtrl 主流程仍为替身，跳跃测试直接调用本关既有事件回调；未验收正式 HUD 触摸、全部皮肤、声音、失败重开调度和 Android/iOS。日志仍包含未复制的 Shadow 等非本轮脚本及材质导入提示，不宣称日志零错误；本轮运行检查未出现异常。
- 回滚：按 `before/` 与两个 `.diff` 只还原本次参数/代码变化；保留去蝴蝶结、初始化等待、原星星外移、开箱表现及配置登记。相关本关玩法说明和风险评估同步更新，未新增全局事件或改写共享系统。

## 2026-09-20 音效与锤子图验证

证据目录：`E:/WorkSpace/outputs/level145-audio-icon-20260920/`。

- `compile.rsp`/`compile.log`：使用 Unity 6000.0.59f2 自带 Roslyn 和生产 Assembly-CSharp 引用编译，退出码 0，0 错误、113 条警告。
- `Level145AudioProbe.cs`/`fixed-checks.txt`/`runtime.log`：108 项隔离 Unity 检查通过。四种音效时机、重复点击防叠播、下放暂停恢复、第一次落地只响一次、三个盒子共享开盒声；狗等待 3 秒仍只一份循环，暂停/失焦/应用挂起停止与恢复、移动/跳跃追击停止、死亡/进门/禁用清理均覆盖。保留真实 Actor 初始化、木槌落点浮动/拾取和狗追击/自然碰撞死亡等原回归。
- 运行命令：同版本 Unity `-batchmode -projectPath E:/WorkSpace/outputs/level145-revision-20260918/unity-check -executeMethod Level145AudioProbe.Verify -logFile E:/WorkSpace/outputs/level145-audio-icon-20260920/runtime.log`。测试脚本与替身仅位于 outputs 隔离项目，生产 Assets 未增加测试或菜单。
- `hammer-ui.png`：真实 GamePlayPanel_145 的 HitButton 子树实际渲染；已经查看黄色锤子透明轮廓、居中和比例。测试环境不包含完整正式 HUD 业务组件，故该图只证明按钮资源和显示。
- `static-checks.txt`：34 项静态核对通过，包含12份保护文件哈希、PNG与共享源字节一致、UI Sprite 参数、唯一 GUID、HUD 只变三个对象块、5个音效标签及文件引用。`audio-resources.json` 保存解析后的原音频路径。
- 测试夹具初次编译因尝试直接赋值 ActorCtrl.IsDied 的私有 setter 失败，改为测试内反射设置后重跑通过；未为测试修改正式 ActorCtrl。运行环境仍有缺失业务组件及既有导入提示，未把这些视为正式主游戏验收。

`uncertain`：音频服务为调用记录替身，没有试听正式混音；主游戏完整结算/重开、实际输入服务、存档和真机/原生挂起未实测。完整代码编译、隔离状态验证和按钮截图不能替代这些验收。

### 同日后续：单次下放与星星声

- 用户要求下放音效只播一次、开盒换成获得星星声音。只修改 Level145Ctrl：LowerGifts 入口普通播放 findItems，移除下放循环管理；原开图爆点播放 getStar。狗叫等其它分支不变。
- 查证 Postcard_Star.cs 的真实收集声音为 getStar；SoundCollection.asset 对应 `Sfx/Postcard/getStar1.ogg` 和 `getStar2.ogg`。这里只调用音频接口，无收集和发奖调用。
- `E:/WorkSpace/outputs/level145-audio-once-20260920/compile.rsp` / `compile.log`：完整生产脚本重新编译退出0，0错误、113警告。逐段审阅与 before 的局部差异，确认无下放循环重播和开盒旧标签。
- 前述108项隔离检查是上一轮版本记录，其中下放循环/开箱标签检查不代表当前规则；本轮没有新增或重跑运行测试，实际声音听感/真机仍 `uncertain`。局部回滚只恢复本轮控制器差异。

### 同日狗叫原关节奏对照

证据目录 `E:/WorkSpace/outputs/level145-bark-cadence-20260920/`。

- 原参考：17关 `TryPlayWaitingBark` 逐字提取到测试对照类 `Original17BarkReference.cs`，只包装测试入口/标记来源，实际间隔与tag从 `Level_017.prefab` 读取为0.4秒与bark。原方法传 `randomPitch:true`、默认不loop。`reference.json`留存参数；ffprobe及Ogg采样数均确认片段为48000 Hz、5280采样，即0.11秒。原17关脚本默认2秒不代表Prefab中的实际配置。
- `baseline-checks.txt` / `baseline.log`：先用修改前145代码执行相同对照测试，在“等待原首次间隔、不立即循环”断言处如期失败，复现错误，没有把失败标为通过。
- `fixed-checks.txt` / `runtime.log`：修复后97项断言及运行结束检查通过。60/30/15 FPS下每声时间与原17函数对照相同；首次和后续间隔均按0.4秒阈值，由帧步长量化，60 FPS样本0.4～0.416667秒，15 FPS样本可到0.466666秒，与原函数相同。`cadence.csv`保存双侧逐声记录及loop=false/randomPitch=true。
- 暂停/恢复、失焦/回焦、应用挂起/恢复、死亡/进门门禁（恢复由夹具模拟）、移动追击、跳跃追击、禁用及禁用后回焦、实例销毁后重建均覆盖；中断停止当前声且不产生新声，恢复不立即补叫。三盒下放仍只播放一次、开盒仍使用getStar，实际狗等待/动画/物理状态保持。
- `compile.rsp` / `compile.log`：完整生产Assembly-CSharp编译退出0，0错误/113警告。`validation.json`：14份保护文件哈希不变，包括17/60/66、共享Actor/Dog/AudioManager、145场景/狗/HUD、声音文件/集合及刚修改的146脚本和Prefab。
- 同版本Unity执行 `Level145BarkCadenceProbe.Baseline` 与 `.Verify`，隔离项目继续位于 `E:/WorkSpace/outputs/level145-revision-20260918/unity-check`，探针和替身没有进入生产Assets。音频替身记录时间/参数，主游戏服务仍隔离；实际混音/设备播放和正式主线整链仍 `uncertain`，不能据此宣称已经试听或完成真机验收。

此前关于“循环播放存在”的108项记录不能证明节奏复用正确；本次以对原函数的逐声对照替代这部分结论。回滚只还原本目录 before 中此次145狗叫调度差异。
