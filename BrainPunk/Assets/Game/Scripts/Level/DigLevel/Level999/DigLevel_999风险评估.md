# DigLevel_999 风险评估

## 2026-10-12 第二轮TBC设定（当前有效）

TBC从本地合成排序999改为Luban真实数据40999/路径DigLevel_999；数据行lockType=2，由控制器追加在当前普通排序末尾，本地序号31。选关按最后普通关PassedLevels解锁，星组隐藏，未解锁展示既有TBC提示。运行时仍只读占位，不消费墨水/提示，不提交成功或普通通关星级；沿Draw保留start、手动Reset失败/插屏/重建、exit。传送门在主线请求受理成功后增加既有EnterTBCPortal(type=3)，失败不报。OneLink可按实际路径进入，TBC顺序关号随远端排序数量变化。

51项托管检查覆盖真实TBC配置、末尾排序、解锁前置查询、TBC不写成功/星级与start/Reset/exit；Prefab及生产传送门点击/插屏/横屏表现仍uncertain。回滚必须成对恢复表、排序控制器、路由与统计约定，保留已有视觉和本轮前基础控制器；详细证据和风险见Dig选关与云控功能及风险记录。以下固定排序999相关文字为第一轮历史基线。


## 行为基线与影响范围

原 Dig 末关下一关返回首页；本次改为未完待续占位关。新增 DigLevel999Ctrl、DigLevel_999.prefab；影响 DigGameLevelCtrl 末关/旧档续关、DigPlayCtrl 加载/HUD/音效及新 Dig 链接分支。复用 ResourceManager、UIManager、EnterLevelLoading、现有主线入口与 TBC 音效；Packages/GuruSDK/GF 和已验收 Draw 末关只读。

## 风险与证据

| 风险 | 本轮处理及证据 | 未验证范围 |
|---|---|---|
| 复制资源残留 Draw/主线行为 | 静态检查移除 Draw 控制器/线条、Actor、死亡区及 Actor 入门组件，修正相机悬空 target | 主工程完整实例生命周期和渲染 |
| TBC 写入普通通关或星级 | 关卡不提交结果，排序层拒绝 RecordVictory(999)；托管断言覆盖 | 实际 DB 落盘往返 |
| 旧全通档留在首关/新内容仍留 TBC | 全通初始化进入 TBC；新增普通关时从 TBC 续入首未通关；托管断言覆盖 | 新版本真实玩家档迁移 |
| 翻页/暂停或 UI 点击穿透传送门 | 本地输入/暂停/一次性门禁，加 EventSystem 指针检测 | 实际弹窗遮罩和 Android/iOS 多触控 |
| 链接加载失败或晚到回调 | 复用统一 Dig 入口与版本取消；来源揭幕后确认，Deactivate 清理 | 真实 SDK 冷启动/已安装唤起/关内迟到 |
| 相机/材质/音效差异 | 保留 Draw 的静态资源，复用 TBC 音效；GUID 和本地 fileID 静态闭合 | 主工程 URP、横屏和资源包验证 |

本轮 Editor/Android 条件源码 0 错误，49 项静态资源及 33 项托管检查通过。75 项隔离首关 PlayMode 不覆盖本关，iOS 宏两处已有 Guru.UniKit.Apple 缺引用，完整 TBC 体验与移动端性能均 `uncertain`。

## 验收与回滚

人工执行：末关成功 → Next → TBC；首页全通旧档 → TBC；链接直达；Reset/Pause/返回首页；弹窗覆盖期间点击世界位置无跳转；关闭弹窗后传送门进入最小未通关主线；全通主线进入 Level_999；再次点击/退出后旧实例无跳转。检查普通通关数和最高星未因 TBC 增加。

回滚先移除 Dig 链接 TBC 入口，再撤占位排序和加载分支，使普通末关恢复返回首页；删除本轮新资源/脚本与 meta。保留普通 Dig 进度及星级字段，不改 Draw 末关/共享入口，不覆盖用户同时修改的资源。若仍使用墨水/提示，单独保留这些与 TBC 无关的能力。
