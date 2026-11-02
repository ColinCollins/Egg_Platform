# 拼接规格

## JSON 结构

```json
{
  "template": "Assets/Game/Prefabs/WannerLevel/ILevel_025.prefab",
  "fixed_grounds": [
    {"x": -12.84, "y": 2.06, "width": 5.46, "height": 10}
  ],
  "levels": [
    {
      "name": "ILevel_026",
      "reference": "Assets/Game/Prefabs/WannerLevel/6-2.png",
      "actor": {"x": -8.07, "y": -1.795},
      "door": {"x": 8.005, "y": -1.588},
      "grounds": [
        {"x": -7.575, "y": -7.315, "width": 6.26, "height": 10}
      ]
    }
  ]
}
```

## 字段约束

- `template`：相对 BrainPunk 项目根目录的已验收 prefab。生成器只读取，不修改。
- `fixed_grounds`：本批每关共同保留的相机边界或公共碰撞块。
- `name`：必须匹配 `ILevel_\d{3}`。
- `reference`：源 PNG 的项目相对路径；其 `.meta` 必须存在并提供 GUID。
- `actor` / `door`：嵌套 prefab 根 Transform 的局部坐标。
- `grounds`：源图可见平台、墙体和顶边；尺寸与位置均为 Unity 世界单位。

生成结果的 Ground 数量为 `fixed_grounds + grounds`。验证报告必须同时给出总数与两类规格数量，避免把边界 Collider 漏报为“多余对象”。

## 测量口径

- 图片按完整 PNG 尺寸居中，默认 100 PPU。
- 像素矩形使用包含首尾像素的宽高；例如 `x=838..995` 的宽度为 `1.58`。
- 只关心顶面的深平台通常使用 `height = 10` 并向下延伸；独立墙或顶边按参考图实际厚度配置。
- Actor 的 Y 先以脚下顶边加模板脚底偏移为基线；Door 的 Y 先以平台顶边加模板门根偏移为基线，再由 Unity Scene 视图核对。
- 同色网页框架会进入候选矩形，必须结合 Camera 可视范围和相邻已完成关卡人工筛选。
