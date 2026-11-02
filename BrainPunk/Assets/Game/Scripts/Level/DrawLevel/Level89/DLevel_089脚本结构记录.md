# DLevel_089 脚本结构记录

## 基本信息与参考基线

2026-09-15。关卡资源 DLevel_089，配置ID20089，本地排序64。用户说明复制自上一关、没有特殊新机制。工程开始时已存在用户修改后的089地图，因此保留它，不从088重新覆盖。

以D088当前反重力玩法和D002首笔释放结构为基线。D088的机关有隔离测试记录，但完整关卡解法/真机验收仍为uncertain，不将其描述为已完成整关验收。本关不新增空壳DLevel89Ctrl，不修改旧关或共享实现。

## 脚本树与职责

```text
DLevel_089
├─ DrawLevelCtrl                 既有状态、输入、成功/失败和重建接口
├─ DrawLineFeature               多笔画线，20墨水，星级阈值0/5/10
├─ DrawLevelCountdownCtrl        首笔后15秒失败倒计时
├─ DLevel2Ctrl                   猫夹球可见入场、首笔放球
├─ level2 / Alien / 球 / 平台 / 成功门 / 相机 / 提示线
└─ DLevel088_ReverseGravityZone  直接复用既有区域Prefab及实例覆盖
   ├─ DLevel88GravityZone        Trigger检测、反重力租约和运行时显示
   ├─ PolygonCollider2D / MeshFilter / MeshRenderer
   └─ Outline / LineRenderer    原素材Repeat描边
```

共享主控实现IDrawLevelRuntime；DLevel2Ctrl实现IDrawLevelVisibleIntroHandler和IDrawStrokeCompletedHandler。反重力组件复用DLevel88ZoneGeometry及其Editor，不复制类、不增加全局事件。关卡加载方创建整关，原标准重建/退出负责销毁；各组件沿原Disable/Destroy边界清理。

## 生命周期与关键调用

1. 原Draw加载链通过配置路径实例化089；主控沿既有Handler接口完成可见入场，再开放画线。
2. DLevel2Ctrl消费首笔完成事件，球脱离动画层级并转为Dynamic；倒计时同步开始。
3. 区域只在运行时创建自身Mesh；FixedUpdate按绑定Trigger做Overlap，按Rigidbody去重并获得重力租约，离开最后区域或停用时恢复。没有修改质量、速度或画线规则。
4. 成功门OnTrigger2DHandle的持久化事件指向同关DrawLevelCtrl.OnTriggerSuccess，沿原成功占用及表现流程结算。倒计时isSuccess=0，超时调用既有TryFailAndReset；出界、暂停、重开和胜利转场均沿原主控流程。
5. 本次没有新增生命周期入口、额外Reset清理、后台任务或编辑态实例创建。

## Prefab与显式绑定

- Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_089.prefab`，保留原GUID `d8d6e264730b78a4cacbecf271819600`。
- 根主控fileID900000000000000001；drawFeature绑定900000000000000002，成功门Spine绑定2984154252043883271，额外渲染组绑定6498767245324627167。倒计时和D002均绑定同一主控；夹子、球和相机沿现有序列化引用，不增加运行时搜索。
- 区域嵌套GUID `28708e2742eeaa04289e119786a027e3`，用户当前位置(3.59,0.05)、矩形6.3×12.3、描边0.2、颜色#98A0FA/Alpha0.31764707、平铺3.495×3保持。共享资产物理Draw层与Background排序保持。
- 内部继续使用用户指定Sp_hx_x_02原图Repeat，描边Sp_hx_x_01；不采用之前弃用的生成/跨边混合方案。
- 唯一Prefab增量为Tips的mTerm从88改成独立89键，保留当前编辑态日文文本、门/平台/提示线布局和所有其它属性。

## 数据、UI、资源与事件流

- LevelData.xlsx / draw_level第69行新增20089→DLevel_089；标题继承“反重力区域”，版本1.14.0、levelType0、lockType1、墨水20、阈值0/5/10均复制088；20999末尾行顺延至70。
- LevelSort.xlsx / drawLevelSort第69行新增64→20089，接在63→20088后。两份Draw JSON与本次Luban实际生成一致，只新增一行。
- I2新增S_DrawLevel_Tips_Level_89，复制88的12种现有语言；其它槽位和旧词条原样保留。不新增UI面板/事件，不改DB、存档、Analytics、SDK、RemoteConfig或远端顺序。
- 表格技能仅用于新行编写、导出和渲染校验；最终保留源表其它单元格值/类型/样式及无关XML。本地文档记录技能要求留存本关范围、风险和交接证据。

## 结构复盘、验证与回滚

相对D088仅有用户原地图差异及本次独立身份/数据接入，没有新机制，直接只读复用现有组件即可，不为命名新增脚本或提升共享抽象。后续若89需要独立材质/机关参数，先用本关实例覆盖或本关资产，不能直接改共享资源影响088。

135个唯一本地对象、256处非零本地引用闭合、41种外部GUID可定位，469个受保护文件哈希不变。源表、生成数据、I2和Prefab增量校验通过；具体证据、未覆盖运行时清单及回滚见同目录功能与风险评估。当前为本地配置接入完成，完整试玩仍待验证。
