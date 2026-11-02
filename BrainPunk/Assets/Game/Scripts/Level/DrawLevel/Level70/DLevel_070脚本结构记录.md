# DLevel_070 脚本结构记录

## 1. 需求、基线与验收边界

2026-09-18：新增本关 Ctrl API，CollisionEnter 触发 Actor 播放非循环 eat，完整播放后进行胜利结算。

修改前没有 Level70 专用脚本或相应功能文档。最初检查时 DLevel4Ctrl 的释放列表为空；实际绑定前重新保存的基线已包含 fruit 与 weight 两个刚体，必须保留这份最新配置。基线副本保存在 `C:/Users/admin/AppData/Local/Temp/brainpunk-dlevel70-20260918/DLevel_070.before.prefab`。该副本没有本关 Ctrl、eat 回调或 fruit 的碰撞事件。

参考 DLevel7ActorCtrl 的局部 Actor 子类与 TrackEntry 完成回调、DLevel4Ctrl 的首笔刚体释放，以及 DrawLevelCtrl 的延迟成功契约。DLevel_007 结构记录明确没有完成 PlayMode/设备验收，因此参考基线的完整体验为 `uncertain`，不能称为已验收关卡。

本次已通过 Unity 编译、Prefab 绑定和真实 Spine 资源隔离检查；整关 PlayMode、Android/iOS 尚未验收。

## 2. 最终组件树与职责

```text
DLevel_070
├─ DrawLevelCtrl：共享初始化、状态门禁与结果提交
├─ DrawLineFeature：画线与线段所有权
├─ DelayAutoBeginDrawHandle：既有开放画线流程
├─ DrawLevelCountdownCtrl：既有倒计时
├─ DLevel4Ctrl：既有首笔释放，列表保留 fruit / weight
├─ DLevel70Ctrl：本关碰撞 API 与 eat 后成功提交
├─ Actor（嵌套 Actor prefab 的本关 override）
│  └─ DLevel70ActorCtrl : ActorCtrl：仅隔离 eat 及其回调
└─ fruit
   ├─ 原 SpriteRenderer / CircleCollider2D / Rigidbody2D
   ├─ 原 BaseItemExecutor / DestroyListener
   └─ LayerCollisionDetector：Actor 层 CollisionEnter → DLevel70Ctrl.OnCollisionEnter
```

DLevel70Ctrl 实现既有 `IDrawStrokeCompletedHandler`，只借其 Reset 通知取消本关动画；笔画回调为空，实际释放仍归 DLevel4Ctrl。新 Actor 子类保留共享移动能力、序列化字段和原 Spine 引用；仅 eat 期间暂停通用 OnUpdate/动画刷新，避免待机或接地逻辑覆盖 eat。没有修改共享 ActorCtrl、DrawLevelCtrl、DrawPlayCtrl、旧关脚本或基础 Actor prefab。

## 3. 生命周期与公开接口

1. 共享 DrawPlayCtrl 创建 prefab 并初始化 DrawLevelCtrl；本关不自行启动数据库、加载器或 UI。
2. 第一笔有效线条完成后，DLevel4Ctrl 把已绑定 fruit 与 weight 切换为 Dynamic；prefab 的 fruit 初态 Kinematic、Actor 初态 Static 保留。
3. fruit 的非 Trigger 碰撞体触碰 Actor 时，LayerCollisionDetector 按 Actor 层筛选，动态 Collision2D 参数传给 `DLevel70Ctrl.OnCollisionEnter(Collision2D)`；API 再校验对方刚体为已绑定 Actor.RB。
4. `PlayEatAndWin()` 也可以独立绑定无参数 Inspector 事件。编辑态、未初始化、暂停、对象禁用、Actor 死亡、重复触发或结果已占用时不启动。
5. `TryBeginSuccessPerformance()` 占用共享成功演出，关闭画线输入和界面切换；Actor `PlayEat(Action, Action)` 预检真实 eat 后以非循环方式播放。
6. 只有当前 TrackEntry 的 Complete 调用 `CompleteSuccessPerformance(nameof(DLevel70Ctrl))`，再进入共享 Resolved/DrawPlayCtrl 胜利链；不另派主线胜利事件、不直接写成功次数或打开面板。
7. 播放失败会解除演出占用。动画 Interrupt/End、对象禁用或 Reset 会解绑完成回调、取消本次成功占用；旧 Complete 无效。完成后的重复碰撞由共享结果门禁拒绝。

`DLevel70ActorCtrl.CancelEat()` 只取消本对象持有的 eat；如果外部已经换成新动画，不清除外部的新轨道。完整播放成功后保留 eat 末帧，不追加成功门或 Actor 渐隐演出。

## 4. 显式绑定与资源

| 宿主 | 字段或事件 | 最终绑定 |
| --- | --- | --- |
| DLevel70Ctrl | drawLevelCtrl | 根节点 DrawLevelCtrl |
| DLevel70Ctrl | actorCtrl | 本关 DLevel70ActorCtrl |
| Actor 子类 | 继承 spineCtrl、pushCollider 等 | 从当前 Actor 序列化配置迁移，保留引用 |
| Actor 内已有组件 | 原 actor 引用 | 重绑到新子类，避免移除旧组件后留空 |
| fruit | targetLayerMask | Actor，第 11 层，2048 |
| fruit | onEnter 持久动态事件 | 同关 Ctrl.OnCollisionEnter(Collision2D) |
| DLevel4Ctrl | boxRigidbodies | fruit / weight，保持基线顺序 |

实际资源为 `Assets/Game/ArtSrc/Spines/sp_box_025/Box_SkeletonData.asset`，包含 eat，时长 0.5333 秒。prefab GUID 保留 `53e530540b19827438e4d90d6c5ef1fc`。新增脚本不按名称或层级扫描运行时引用。

## 5. 数据、资源、UI、事件与局部边界

本关只使用序列化引用、现有 Collision2D UnityEvent、ActorSpineCtrl 与 DrawLevelCtrl 成功演出 API。标准 Resolved 由 DrawPlayCtrl 处理，其存档、打点、VictoryPanel 与下一关转场仍归共享流程；本关没有新增配置、存档字段、资源加载入口、全局事件或 HUD API。成功演出期间的倒计时/失败请求仍由共享结果门禁裁决，本次没有改倒计时脚本。

相对 Level7，本关不需要投掷释放帧、Coroutine 或二秒竞态等待；只有 eat 完成才能成功，缺少动画时取消而不采用直接胜利兜底。相对 Level66，仅复用“专用 Actor 子类”边界，不改共享 Actor。结构复盘决定：保持本关局部回调，暂不提取共享动画成功工具；完整场景回归不足。

## 6. 验证与缺口

- Unity 已编译并执行一次性绑定/隔离检查工具；完成后删除工具及其 meta，不保留通用辅助代码。
- 21 项隔离检查覆盖唯一 Actor 子类、显式引用、迁移配置、CollisionEnter 持久绑定、原刚体初态、首笔释放引用、编辑态 API 门禁，以及真实 Spine eat 的半程不完成、完成仅一次、动画替换取消、Reset 取消和旧 Complete 失效。
- 隔离检查使用真实 SkeletonData 与 AnimationState 时间推进，并通过反射设置待检查的私有回调状态；没有进入 PlayMode，不能证明真实物理事件、完整成功门禁或胜利面板。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo -v:q` 未能启动编译：NETSDK1004，缺少 `Temp/obj/Assembly-CSharp/project.assets.json`；不以此作为编译通过证据。
- 对修改前后 prefab 的差异复核：仅新增本关组件/事件、本关 Actor override 与迁移后的引用，原首笔两刚体列表、布局、图像、碰撞形状及 meta GUID 保留。
- CodeGraph 索引存在但 CLI/MCP 不可用，使用当前源码、Prefab YAML 和本地资源检查。引用的远程资源 feature plan 在当前路径不存在。未找到 Level70 专属旧文档，从共享契约建立本关记录。

`uncertain`：仍需整关 PlayMode 验证画线后两刚体下落、Actor 碰撞正确筛选、eat 可见完整播放、胜利只提交一次；覆盖 eat 期间 Reset/退出/暂停、倒计时到期、VictoryPanel Replay 与正常下一关转场，以及 Android/iOS 横屏表现。当前检查不证明实际 SDK、存档与分析事件结果。

## 7. 风险与回滚

详见同目录 `DLevel_070功能风险评估.md`。回滚时从当前 prefab 仅移除本关 Ctrl/Detector，恢复嵌套 ActorCtrl 和原引用；必须保留用户后来修改的内容。任务前副本只用于对比，不直接覆盖并行编辑后的 prefab。确认没有其它引用后删除 Level70 新脚本及其 meta，撤销本主题文档/中央复盘/当天记录；不清存档，不回滚共享系统。
