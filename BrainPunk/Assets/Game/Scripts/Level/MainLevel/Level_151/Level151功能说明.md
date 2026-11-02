# Level151 功能说明

更新时间：2026-09-17

## 需求与修改前基线

用户指定正式关卡 151，密码为 `1358`，场景已从 128 复制。修改前 `Level_151.prefab` 与 `Level_128.prefab` 仅根名称不同，仍挂 `Level128Ctrl`，使用设备当前时间作为答案。未发现既有 151 专项文档；仓库规则引用的 `specs/037-remote-resource-loading/plan.md` 当前缺失，本次依据用户要求和本地源码、资源实施。

## 实现范围

- 新增本关 `Level151Ctrl.cs`，沿用 128 的 HUD 延迟配置、暂停提示显示和成功后入口锁定逻辑，指定 `PasswordPopup_151_1358`。
- 新增 `Level151PasswordPopup.cs` 与 `Resources/PasswordPopup_151_1358.prefab`，以 128 私有弹窗为基线，仅将答案规则改为精确匹配 `1358`。保留原四行 `1～9 / * / 0 / #` 键盘、缩放、输入显示、错误反馈和成功会话保护；不再读取设备时间。
- 控件引用在本关 Inspector 显式序列化绑定，包括原数字键、显示文本、关闭键、背景、显示容器、键盘容器及错误反馈，避免新代码复制旧版按节点名称扫描的做法。基类的生成字段是 private，保留其原绑定但不从子类访问；第四行仍只在 UI 首次创建时按原模板生成。
- `Assets/Game/Prefabs/Level/Level_151.prefab` 只替换本关控制器绑定。沿用 Actor、四个拖拽方块、两颗收集星、相机、密码区域、门与场景摆放。
- 用户补充确认：密码藏在其它关卡，本关只固定密码，其它逻辑维持原样。因此保留复制底稿中的暂停页 `????`，不添加答案线索，不改关卡表或本地化。
- 128、96、共享 `PasswordPopup`、`Level6GamePanelFeature`、Actor、GF 与 Packages 不作修改。

## 调用与生命周期

关卡启用 → 延后一帧取得新 `GamePlayPanel_001` → 配置本关密码资源 → 角色进入原密码区域显示 Use → 四位输入匹配 1358 → 本关弹窗按原流程成功反馈并延迟一秒派发既有 `OnTiggerItemEvent(1)` → 门打开 → 角色进门走既有通关链。本关收到同一事件后锁定并隐藏 Use，防止再次打开密码页。

输入完成后的锁定、Open/Close 会话版本、成功后仅派发一次、错误反馈后清空、失焦与退后台清空未完成输入、关闭与 Reset 取消待执行成功任务均保留 128 的实现。密码校验之外没有新增玩家行为。

暂停时只在 `GameSettingPopup` 实际可见后展示原提示；恢复、重置或离开时隐藏。Reset/Replay 仍由现有 PlayCtrl 销毁重建关卡和 HUD，不增加本关重复重置、存档字段或全局事件。已对照《关卡打点生命周期约定》2.2～2.5 节，本次不改其事件或统计时序。

## 配置与验证

本轮未写入配置。只读检查发现现有 LevelData/LevelSort 与对应 JSON 尚未登记 `10151 / Level_151`；按用户限定范围保留现状，不能据此声称正式选关入口已接通。两份工作簿与两份 JSON 均与本轮开始时的文件逐字节一致。

- 使用 Unity 6000.0.59f2 自带 Roslyn 和当前 Bee `Assembly-CSharp.rsp`，补入本关两个脚本并将产物输出到 `Temp/Level151/`：退出码 0、0 error、112 条工程 warning，本关无诊断。未改工程构建配置或生成程序集目录。
- 在 Unity 自带 Mono 下加载上述实际编译产物，通过反射调用 `Level151PasswordPopup.IsPasswordValid`：全部 10,000 种四位数字和 11 种空值、长度、空白、符号、全角/其它文字数字输入共 10,011 项通过，唯一接受 `1358`。
- 场景 Prefab 247 个唯一定义、1,819 个本地引用，密码 Prefab 144 个唯一定义、929 个本地引用，均无重复或悬空引用；本关新增 GUID 在 Assets 中唯一，资源名唯一，18 个非空显式引用全部指向正确类型与原控件。
- 场景除控制器 GUID 外与任务前文件完全一致；密码 Prefab 除根名、脚本 GUID、显式绑定外与 128 完全一致。控制器归一化类名/资源名后与 128 相同，密码盘 16 个输入、反馈、关闭和成功方法体与原版一致，仅提交调用移除时间参数。
- `git diff` 确认 128、96、Level6 共享脚本和 Level128 场景无本次改动。验证入口与输出：`Temp/Level151/compile.rsp`、`compile.log`、`PasswordChecks.cs`、`password-checks.log`、`check_static.py`、`static-results.json`。

PlayMode、完整开门过关、重开、退出、存档打点及 Android/iOS 横屏尚未验证，标记 `uncertain`。

## 回滚

将本关 Prefab 控制器 GUID 恢复为任务前的 Level128Ctrl GUID `4a111e5e86624b22820191b6baf65e16`，删除本关新增脚本、密码资源和对应 meta。任务前 Prefab 备份位于 `Temp/Level151/Level_151.before.prefab`；配置未修改，不需要回滚。不要覆盖其它既有工作区改动。
