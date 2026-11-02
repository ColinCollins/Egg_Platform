# DLevel_014 纯物理投篮与进筐功能与风险评估

## 1. 原行为与根因

- D014 根节点误挂 `DLevel15Ctrl`，Alien、球和 Spine 引用全空；它既不能发射篮球，也不能判定进筐。
- 篮球开局为 Dynamic，依靠投球器实体碰撞暂时托住；第一笔完成没有任何本地响应。
- 两个篮沿虽为实体 BoxCollider2D，但只有普通碰撞，没有成功入口。
- 篮球没有 ActorCtrl，通用成功 Actor 校验与通用 FallArea 角色过滤都不能直接复用。

## 2. 新行为

```text
开局篮球 Kinematic 静止
  -> 玩家完成第一笔有效线
  -> 篮球 Dynamic + 单次右上 Impulse + 单次 Spin Impulse
  -> 后续不再脚本改速度
  -> 篮球与画线/投球器/篮沿纯 Physics2D 碰撞
  -> 球心从上向下依次穿过篮筐上/下平面
  -> 占用成功锁 + 在得分点播放彩纸烟花
  -> 篮球继续物理 0.35s，再渐隐 0.4s
  -> 正式提交胜利
```

- 如果篮球中心先进入本关 FallArea，则播放通用 `failed` 音效，等待 `0.2s` 后提交失败并走 Draw 重载。
- 后续笔画不重复发射；点按、取消、长度不足的线不发射。
- Reset 恢复球的 Prefab 初始位置，而不是按当前飞行位置继续。
- Pause 暂停篮球模拟、烟花、成功延迟与渐隐；恢复后保留暂停前的物理速度继续。

## 3. 纯物理边界

- 只在首笔完成时调用一次 `AddForce(..., ForceMode2D.Impulse)` 和可选 `AddTorque`。
- 飞行中没有 Tween、路径跟随、目标吸附、逐帧速度覆盖、进框拉拽或碰撞后脚本反射。
- 篮球使用本关局部 `0.85` 弹性、Maximum Combine，使它即使撞到未配置弹性的画线也能产生
  可见反弹；不修改共享画线 Prefab。
- Collision Detection 改为 Continuous，降低高速穿过窄篮沿或细线的概率，但不能替代 PlayMode
  对极端帧率和高速斜碰的验证。

## 4. 复用与影响范围

- D014 篮筐前/后景 Scale 与两个篮沿 Collider 几何同步当前 D013，整体高度保留 D014 现有布局。
- 用户对投球器的最新位置 `(-5.35,-1.15)`、旋转 `12.591°`、新 Sprite
  `Sp_hx_jstl_01` 及 Collider 变化全部保留。
- D013/D014 共用 `DrawGame_BasketSuccessFirework.prefab`；该 Prefab 使用常规成功门
  `ConfettiBlastRainbow` 的非循环、非自播、Burst、尺寸、受力和 SceneLayer 覆盖；根排序为
  Order `220`，位于球和篮筐前景之上。
- 不修改数据库、Luban、存档、通用成功门、D008、共享球/线、全局事件或平台系统。

## 5. 风险矩阵

| 风险 | 等级 | 当前控制 | 待验证 |
| --- | --- | --- | --- |
| 默认弹道不可解或直接进框 | 高 | 固定右上方向 `(1,0.75)`、Impulse `14`，目标是先飞过篮筐 | 至少 5 种画线形状与无画线基线 |
| 高速穿线/穿框 | 高 | Continuous、篮球直径约 1.065、实体篮沿 | 30/60 fps 与移动端 |
| 反弹过强或不足 | 高 | 本关材质 Bounce `0.85`、Maximum，不污染共享材质 | 实际线质量/角度组合 |
| 从侧面或下方误判 | 高 | 上/下双平面、向下速度、X 开口门禁 | 擦框、回弹、单帧跨越 |
| 成功后重复失败/超时 | 中 | `TryBeginSuccessPerformance` 先占用结果，失败提交被锁住 | 15 秒临界时刻 |
| 烟花循环、自播或层级错误 | 中 | 共享 wrapper 关闭 Loop/Play On Awake，SceneLayer SortingGroup Order 220 | D013/D014 实际画面 |
| FallArea 不触发 | 中 | D014 显式读取本关 BoxCollider bounds，不依赖 ActorCtrl | 球心进入边界时机 |
| Reset 残留球/粒子/成功锁 | 中 | 版本号、Kinematic 复位、Stop/Clear/Destroy、取消本地占用 | 连续快速重开 |
| 暂停后物理或演出偷跑 | 中 | Pause 关闭 Rigidbody2D 模拟、暂停粒子，等待/渐隐只累计可玩时间 | 接触瞬间暂停及恢复 |
| FallArea 延迟期间暂停 | 低 | 与通用 FallArea 一致使用普通 `WaitForSeconds(0.2)`；无 Actor 死亡动画 | 触发后立即暂停仍会重建 |

## 6. 验收清单

- [ ] 未画线时篮球保持静止；点按/无效短线不发射。
- [ ] 第一笔有效线完成后只发射一次，后续笔不补力。
- [ ] 无画线基线不会直接成功；玩家能用实体线碰撞把球导入篮筐。
- [ ] 飞行和所有反弹中没有脚本瞬移、吸附或逐帧速度跳变。
- [ ] 只碰框、侧入、下方反弹不过关；从上向下完整进框只结算一次。
- [ ] D013 与 D014 都在确认得分点播放同一轮烟花，失败/Reset 不残留。
- [ ] 成功后篮球不停在篮内，继续物理后渐隐；15 秒临界点不被失败覆盖。
- [ ] 篮球进入 FallArea 播放失败音效并在约 `0.2s` 后重开；手动 Reset/Replay 恢复初始球状态。
- [ ] Android/iOS 横屏的碰撞、粒子排序和性能正常。

## 7. 验证状态

- 独立 Unity Roslyn 全量编译：`0 error / 104 warnings`，warning 均来自工程既有代码。
- 目标 `git diff --check`：通过，只有仓库既有 LF/CRLF 提示。
- Unity PrefabImporter 已重新导入 D013、D014、共享烟花和篮球材质；D014 `107` 个、共享烟花
  `5` 个 YAML anchor 唯一且本地 fileID 无缺失。运行态真实弹道与完整 PlayMode 尚未形成可确认
  的自动化证据，统一标记 `uncertain`，不得把静态编译等同于手感验收。

## 8. 回滚路径

1. 只回滚投球手感时调整 `launchDirection`、`launchImpulse`、`spinImpulse` 或本关篮球材质，
   不在碰撞回调中强写反射速度。
2. 回滚篮筐同步时应成组恢复前/后景 Scale、位置与两个 BoxCollider2D 的 offset/size。
3. 完整回滚时移除 DLevel14Ctrl、本关篮球材质和 D014 的烟花/FallArea 局部链，并恢复旧控制器；
   不回退用户手调的投球器位置、旋转、Sprite 与 Collider 尺寸。
