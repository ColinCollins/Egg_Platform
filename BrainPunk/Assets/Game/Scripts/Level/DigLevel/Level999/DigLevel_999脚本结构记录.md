# DigLevel_999 脚本结构记录

## 2026-10-12 第二轮TBC设定（当前有效）

TBC从本地合成排序999改为Luban真实数据40999/路径DigLevel_999；数据行lockType=2，由控制器追加在当前普通排序末尾，本地序号31。选关按最后普通关PassedLevels解锁，星组隐藏，未解锁展示既有TBC提示。运行时仍只读占位，不消费墨水/提示，不提交成功或普通通关星级；沿Draw保留start、手动Reset失败/插屏/重建、exit。传送门在主线请求受理成功后增加既有EnterTBCPortal(type=3)，失败不报。OneLink可按实际路径进入，TBC顺序关号随远端排序数量变化。

51项托管检查覆盖真实TBC配置、末尾排序、解锁前置查询、TBC不写成功/星级与start/Reset/exit；Prefab及生产传送门点击/插屏/横屏表现仍uncertain。回滚必须成对恢复表、排序控制器、路由与统计约定，保留已有视觉和本轮前基础控制器；详细证据和风险见Dig选关与云控功能及风险记录。以下固定排序999相关文字为第一轮历史基线。


## 需求、基线与参考

2026-10-12 用户要求 Dig 增加 TBC。此前 Dig 最后一个普通关的下一关返回首页，没有占位资源。本关参考只读 `DrawLevel/Level999/DLevel_999脚本结构记录.md` 和实际 DLevel_999 Prefab 的未完待续美术、相机及主线传送门；不改变已验收 Draw 末关。

## 最终结构与职责

```text
DigGameLevelCtrl
  普通关排序 → 最后关的下一关为 999
  运行时占位配置：40999 / DigLevel_999，不计普通总关数
DigPlayCtrl
  翻页遮满 → 加载 Prefabs/DigLevel/DigLevel_999.prefab
  DigLevel999Ctrl.InitializeRuntime → 揭幕后开放本地门禁
  Dig HUD：保留 Reset/Pause/Shop，隐藏墨水/广告/提示
DigLevel_999.prefab
  DigLevel999Ctrl : MonoBehaviour, IDigLevelRuntime, IDebuger
  原 Draw 末关静态美术/文本/相机/地面/传送门点击组件
  ClickItemEventHandle UnityEvent → DigLevel999Ctrl.EnterMainLevelByClick
```

- TBC 是新独立 Prefab/GUID，没有修改 Draw 末关，也没有克隆为仍运行 Draw 的关卡。
- 移除 DrawLevelCtrl、DrawLineFeature、旧 DLevel999Ctrl、主线 Actor、死亡区、线模板及 Actor 入门 Trigger/销毁动画；相机 Follow target 的旧 Actor 引用清空。其它嵌套静态美术按原资源复用。
- 传送门 UnityEvent 显式绑定根组件 `900000000000000003`，程序集类型为 `Game.Dig.DigLevel999Ctrl, Assembly-CSharp`。脚本不搜索 Prefab 对象；UI 指针命中时不触发世界点击。
- 关卡接口可暂停/关闭输入/重建；不产生 Resolved、InkChanged，不接收提示或加墨，TryCompleteByHint 永远返回 false。控制器创建 HUD 时播放已有 TBC 音效。

## 生命周期、数据与边界

末关胜利解锁 999 → 正常翻页时保留已结算旧 Dig 至遮满 → 销毁旧关/HUD → 加载新 TBC → 创建绑定 → 揭幕/开放门禁。Reset 使用既有全黑重建，暂停与退出沿用当前 Dig 宿主。点击门禁通过后调用现有 GameManager.TryEnterMainLevel，成功请求保持本实例一次性门禁；失败记录日志并撤该门禁。

`Levels` 只暴露普通关，999 不记普通 PassedLevels、最高星或首胜奖励。当前停在 TBC 的旧档遇到新增普通内容时，初始化恢复首个未通关目标。链接直达路径是 `deep_link_value=open_level/deep_link_sub1=dig/deep_link_sub2=DigLevel_999`；普通无效链接全通时回退本关。OneLink 来源在揭幕后确认，退出/失败清理；不新增正式统计或 TBC portal 打点。

相对 Draw 的必要差异：本关没有绘线、主线 Actor 自动入门和两秒入门演出，只有静态 TBC 与点击传送门；没有从复制品继续发送 Draw 的事件或 portal type。共享 API/共享关卡不迁移，不提取通用 TBC 基类。

## 验证与结构复盘

49 项资源静态检查覆盖 anchor 唯一、local fileID 闭合、旧 Draw/Actor/死亡区依赖移除、UnityEvent 类型和唯一 GUID；33 项托管断言覆盖末关推进、TBC 不写普通进度/星级、指定 TBC 链接及新增内容续关。Editor/Android 条件源码编译通过。

`uncertain`：本轮没有在主工程实例化完整 TBC 或实测传送门点击、相机画面、音效、弹窗遮罩、Android/iOS 触控/横屏。75 项隔离 Unity 检查针对 Dig 首关，不能用作 TBC 渲染验收。iOS 原生引用缺失仍待工程环境解决。

结构复盘：沿用模式控制器管理资源/转场/存档、关卡只持局部门禁的分工。TBC 的最小接口空操作是明确不可通关边界；暂不复用 Draw 的角色演出或抽象共享 TBC 流程。回滚与风险见同目录风险评估。
