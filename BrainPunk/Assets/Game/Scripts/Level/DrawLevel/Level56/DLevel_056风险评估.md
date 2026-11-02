# DLevel_056 平台物理风险评估

## 1. 需求与原行为基线

用户反馈：DLevel_056 画线后平台没有开启物理，不能产生左右平衡。

静态基线显示：`pingtai/platform_Stone` 已有 Rigidbody2D（质量 `10`、重力倍率 `1`）和连接 `Sp_l93_cl_01` 静态支点的 HingeJoint2D，但 Rigidbody2D 的 `m_BodyType` 为 `1`（Kinematic），运行时没有有效挂载的平台控制器在首笔画线后切换它。因此画线只能撞到一个不受力的 Kinematic 平台。

## 2. 本次变更与影响范围

- 修改：`Assets/Game/Prefabs/DrawLevel/DLevel_056.prefab`。
- 修复：在根节点有效挂载 `DLevel26PlatformCtrl`，并绑定单组齿轮、平台刚体和铰链。
- 未修改：共享 DrawLevelCtrl、DrawLineFeature、Physics2D Layer 矩阵、平台质量/阻尼/铰链几何、D054/D055 运行逻辑、D053 蜂群脚本。
- 数据/UI/存档/Analytics/SDK：无新增或迁移。

## 3. 风险项

1. **首笔回调顺序风险**：D053 Hive 与平台控制器都消费首笔事件；二者均为本地 Handler，若未来改动 Handler 扫描顺序，平台释放和蜂群生成的同帧顺序可能变化。当前两者互不写同一刚体。
2. **铰链引用风险**：绑定的是嵌套 `platform_Stone` 的本地新增 Rigidbody2D/HingeJoint2D；若 Unity 重新生成嵌套 Prefab Override，需重新检查三个 fileID。
3. **角色状态差异**：D056 Actor 当前是 Static 覆盖，本次没有仿照 D037 释放 Actor；若关卡设计要求角色也随平台受力，需要另行确认，不能由本修复隐式改变。
4. **蜂群残留待确认**：D056 仍激活 D053 Hive。若本关不是蜂群+平台组合，后续应在确认玩法后移除对应脚本、实例和引用；本次不做猜测性删除。

## 4. 验证证据与未验证项

- 静态检查：D056 `139` 个本地 anchor 无重复；新增组件引用的三个 fileID 均存在。
- 脚本 GUID 检查：D054/D055/D056 所有 `m_Script` GUID 均可映射到项目中的 `.cs.meta`。
- 格式检查：新增组件与引用行无尾随空格。`DLevel_056.prefab` 当前为未跟踪文件，普通 `git diff --check` 不覆盖完整资源；未清理 Unity YAML 原有的空值行。
- `uncertain`：Unity PlayMode 画线后的角度响应、平台与画线的碰撞手感、重试/暂停/成功时序、Android/iOS 横屏尚未执行。

## 5. 回滚路径

从 DLevel_056 Prefab 根节点的 `m_Component` 列表移除 `DLevel26PlatformCtrl` 引用即可回到“平台始终 Kinematic”的原基线；共享脚本和其它关卡无需回滚。
