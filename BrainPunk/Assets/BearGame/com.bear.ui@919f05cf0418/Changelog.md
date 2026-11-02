# Changelog

## [0.0.1] - 2025.1.5
+ 初始版本
+ 实现 UI 管理器核心功能
+ 实现 UI 栈管理
+ 实现 UI 生命周期管理
+ 实现 UI 层级管理
+ 实现遮罩管理
+ 实现数据绑定机制
+ 实现缩放动画组件（MonoBehaviour 组件模式）
+ 预留预加载接口

## [0.0.2] - 2025.1.6
- 增加 Loader 注册handle
- 调整 OpenUI 接口

## [0.0.3] - 2025.1.6
- 接口调整
  
## [0.0.4] - 2025.1.20
- 增加完全释放接口
- 
## [0.0.5] - 2025.1.27
- 支持重复类型，不同名称界面生成

## [0.0.6] - 2025.1.30
- 增加 Scaler 设置

## [0.0.7] - 2025.2.26
- UIScaleAnimation 功能调整
- UIBaseView 优化
- 
## [0.0.8] - 2025.3.5
- 增加 stack 的防护，禁止 top 重复入栈，并报错提醒

## [0.0.9] - 2026.3.23
+ **Editor / UIGenerator**：从 JSON / PSD 导出（`document` + `layers`）生成 UI Prefab；支持 `isGroup`/`children`，子节点逆序以对齐 PS 叠放
+ 无 TMP 时走 `UnityEngine.UI.Text`（`TMP_PRESENT`）；`UIGeneratorSettings` 默认 `Assets/Resources/UIGeneratorSettings.asset`
+ `IUIGeneratorPostProcessor` 生成后处理；Image 尺寸与 Sprite 不一致时 Sliced；PSD 文本支持 `color`、`content`、`fontSize`、`opacity`
+ JSON 规范化（类 JS 字面量）；README 增加 UIGenerator 说明