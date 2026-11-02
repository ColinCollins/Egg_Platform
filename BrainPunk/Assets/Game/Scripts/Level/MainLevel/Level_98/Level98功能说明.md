# Level098「跳过4个坑，实际上有5个坑」

## 2026-08-18 黄色范围脚本精简

- 背景：合并重复生命周期清理，保持石像、坑、门、绳、音效和暂停/重开行为不变。
- 影响文件：仅 `Level98Ctrl.cs`。`StopDoorLiftSound()` 与事件解绑集中到私有 `CleanupRuntimeState()`；`isPaused=true` 仍只在停用时设置。
- 前后差异：清理调用顺序和次数不变，没有修改 Tween、碰撞、关卡参数、资源、UI、配置、打点或成功失败链。
- 验证：`Assembly-CSharp.csproj` 编译通过（0 错误），目标 diff 检查通过。
- 回滚：把共享清理体分别展开回两个生命周期回调。

## 需求与基线

本关沿用 `Level_098.prefab` 现有的六段地面、五个坑、`Actor`、`door_0`、`shengzi` 和 `Sp_l89_02` 岩石雕像。第一段地面与初始画面美术保持原位，五个坑的实际空隙分别压缩到原宽度约 `0.8`，后续地面、第五坑石像、门、绳和 FallArea 随累计位移同步调整。纸张背景沿用共享 `CommonBg`，仅在本关增加第二个 X=`50` 的实例补齐横向画面，不修改共享背景预制体。

## 运行行为

- `Level98Ctrl` 是挂在 `Level_098` 根节点上的本关私有控制器，不修改 `BaseLevelCtrl`、Actor、共享门预制体或共享镜头脚本。
- 出口门进入触发器开局封印。角色在第五坑下方与石像碰撞体的顶面接触时，进入正确路线；等待 `statueRiseDelay` 后，脚本使用右侧地面碰撞体顶面实时计算石像目标高度，并用 Kinematic `Rigidbody2D.MovePosition` 平滑上升。承托期间同步角色竖直速度，避免角色被平台落下；玩家再次跳跃时会暂时解除承托同步。
- 石像升到目标顶面后恢复门触发器，角色可以沿齐平地面走到门内过关。
- 如果角色在石像路线尚未建立前站到第五坑右侧地面，视为跳过全部五个坑。脚本立即封印门、隐藏石像，并将门与 `shengzi` 用 `1.0s` 一起向相机顶边上拉，最终只保留 `doorVisibleHeightAtTop` 的门体可见高度，因此不能触发过关。
- 镜头只在本关黄框 `cameraFollowArea` 的 X 范围内跟随。角色到达视口约 `2/3` 后开始跟随，随后直接以角色世界 X 为平滑目标，使角色收敛到画面中线；超出黄框时目标钳制在区域边界。角色的 Y（包括掉到画面下方）永远不会改变相机 Y，Z 始终保持初始值。
- 石像确认存在有效上升距离时播放一次 Level089 的 `l89StonePillarDrop`；失败路线中门和绳实际上拉期间循环播放 Level051 使用的 `findItems`。循环音会在上拉结束、暂停、重置、Disable 和 Destroy 时停止，暂停恢复且仍在上拉阶段时重新开始。

## 绑定与可调参数

`Level_098.prefab` 根组件已绑定现有 Actor、GameCamera、`Ground_long (1)`、`Sp_l89_02`、`door_0` 和 `shengzi`。黄框为 `center=(29.66058,-0.25)`、`size=(51.32116,14)`；主要策划参数是 `statueRiseDelay`、`statueRiseDuration`、`statueTopOffset`、`cameraFollowAreaCenter/Size`、`doorLiftDuration` 与 `doorVisibleHeightAtTop`。石像终点不使用绝对 Y，地面或石像移动后会重新按碰撞体顶面计算。

## 验证记录

- Unity 6000.0.59f2 自带 Roslyn 隔离编译：退出码 0、0 error、104 条工程既有 warning，`Level98Ctrl.cs` 无诊断；编译输出写入系统临时目录，现有 `Library` DLL 的 SHA-256 前后相同。
- Prefab YAML 静态检查：167 个定义、0 个重复 fileID、0 个缺失的纯本地引用；新增 `CommonBg (1)` 的源 GUID、实例和根 Transform 引用均可解析。
- 布局静态复算确认五段空隙均为原值的 `0.8`；两段宽 FallArea 同步修改中心与 X 缩放，避免压缩后覆盖第五坑正确路线。两个音效 tag 均已在现有 `SoundCollection.asset` 登记，未复制或新增音频资源。
- Luban 隔离输出与当前运行时配置做结构化比对后，`tbleveldata.json` 仅新增 `10098 / Level_098`，`tblevelsort.json` 仅新增末尾排序项 `{id:96,path:"10098"}`；既有配置条目保持不变。
- 未操作 Unity 界面，尚未完成前台 PlayMode 的两条路线、Reset/Replay、不同横屏比例和 Android/iOS 触摸验收。

## 回滚

若只撤回本轮调整，恢复六段地面及其后续对象/FallArea 的旧坐标，移除本关 `CommonBg (1)`，并恢复旧镜头锚点、`1.2s` 门上拉与无音效调用即可；共享背景和音频资源不需要回退。若完整撤回 Level098 交付，还需移除根节点 `Level98Ctrl` 及其脚本/meta，并同步回退 `tbleveldata.json` 的 `10098` 项和 `tblevelsort.json` 的 `{id:96,path:"10098"}`。
