# DLevel_089 功能与风险评估

## 需求、原基线与修改范围

2026-09-15，用户要求延续上一关，没有特殊新机制。开始时DLevel_089已由用户复制并修改：区域变窄，平台与成功门布局变化；配置表/运行JSON没有20089入口，Tips仍引用88。保留用户这份地图及其GUID，不覆盖回088。

本次仅接入新关数据与独立标题键。Prefab改一行mTerm；I2新增89词条且文字沿用88；LevelData/LevelSort各新增一行，更新两份Draw JSON。本关脚本/生命周期、物理、入场/放球、倒计时、相机、门和地图参数不改。无新C#、Shader、纹理、运行时对象、存档字段、事件、远端配置或平台SDK变更。结构和显式绑定见同目录脚本结构记录。

## 数据与用户可见效果

- 本地新增ID20089、资源DLevel_089、排序64，接在088后；标题仍为反重力区域。20墨水、0/5/10阈值和15秒失败倒计时沿用原Prefab和上关数据。
- S_DrawLevel_Tips_Level_89与88的12种现有语言一致，保留其它空语言槽惯例；今后可以独立改089标题而不修改088。
- 反重力区域继续共享D088组件/材质，保留用户当前0.2描边、约32%透明度及3.495×3平铺大小。不重新采用无缝生成试验。

## 验证证据

目录：`E:/WorkSpace/outputs/dlevel89-20260915/`。修改前Prefab、I2、两份源表/JSON快照和469个受保护文件哈希均留存。

1. `edit_tables.mjs before/edit/final`：通过表格技能工具读写并检查目标范围，最终渲染LevelData-final.png和LevelSort-final.png已目视核对，新行格式沿用前行、无公式错误命中。
2. `preserve_tables.py`只合入表格工具编写的新行并检查源表其它内容；`workbook-validation.json`确认全部原值、类型、样式和无关XML保持。LevelData末尾20999只顺延一行，未删除。
3. 实际执行 `dotnet Luban/Luban/Luban.dll -t client -d json --conf Luban/Configs/luban.conf -x outputDataDir=E:/WorkSpace/outputs/dlevel89-20260915/luban-generated`，生成/校验成功。只将两份Draw JSON的新增记录接入主工程，不覆盖其它生成配置。
4. `verify.mjs` / `static-validation.json`：两份Draw JSON与Luban结果一致；仅新增20089及排序64，旧数据不变；469个旧关/共享文件哈希不变；089仅mTerm变化、GUID不变；135个唯一对象/256处本地引用无悬空，41种直接外部资源GUID可定位；I2仅新增89且与88内容逐字一致。
5. 源码/Prefab检查确认成功门事件指向本关OnTriggerSuccess，D002及倒计时引用完整。本次没有新增/修改C#，未重复运行程序集编译。

## 风险、性能与待验收项

- `uncertain`：未操作用户正在使用的Unity进入完整089，尚未验证实际入场、鼠标/触摸画线、提示线解法、成功/失败/重开/退出、存档和打点。静态引用正确不等于整关解法成立。
- 本次只增加本地排序。启用服务器level_sort时，实际玩家顺序仍由既有RemoteConfig决定；没有发布或更改远端排序。
- Android/iOS横屏实机、构建/热更资源包和60fps性能未测。本次无新增运行时组件或纹理，机制开销沿用当前088；不把“无新增开销”作为真机达标证据。
- 088与089共享区域Prefab、材质和脚本。后续独立改样式或行为时使用89局部覆盖/资产，避免影响前关。

## 回滚

只移除源表新增20089/排序64并重新生成对应Draw JSON；移除独立89词条并把本关mTerm恢复88。用户原有DLevel_089地图及GUID不得删除或用备份整图覆盖。新增本关文档可以保留历史记录。无Git提交、推送或远端发布。
