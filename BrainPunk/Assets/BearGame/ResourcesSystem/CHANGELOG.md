# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- `ResourceManager.CanLoad<T>(string path)` and `IResourceLoader.CanLoad<T>(string path)` for resource availability checks through the Bear resource-system boundary.
- `YooAssetLoader.CanLoad<T>()` uses package location validation internally; feature code should call `ResourceManager.CanLoad<T>()` instead of directly using YooAsset package APIs.
- `ResourcesLoader.CanLoad<T>()` supports the same ResourceManager query path for Resources-backed loading.

### Changed
- `ResourceManager` 现在按 `IsAvailable + CanLoad<T>(path)` 选择 Loader，并按“路径 + 类型”隔离资源缓存与共享异步任务。
- Bear 缓存会记录实际加载资源的 OwnerLoader；`Release(path)`、Loader 注销和 `ReleaseAll()` 会释放正确的加载器。
- 单个异步等待者取消不再取消共享底层加载；释放期间完成的旧加载结果不会重新写入缓存。
- YooAsset 同步与异步加载统一缓存 `AssetHandle`；本地 AB 加载失败不再清空 Package 缓存或自动重试。

### Fixed
- 修复 YooAsset 同步 `AssetHandle` 未保存、`Release` 实际无法释放的问题。
- 修复同路径不同资源类型可能误用同一缓存或异步任务的问题。
- 修复 `LoadAndInstantiateAsync<T>` 缺少目标组件时遗留实例，以及传入返回组件调用 `ReleaseInstance` 时未销毁完整根对象的问题。

## [0.0.1] - 2026-03-31

### Added
- 初始版本发布
- 资源管理器核心功能
  - 支持多加载器策略（优先级管理）
  - 线程安全的资源缓存
  - 防止重复加载机制
- Resources 加载策略（兜底方案）
- YooAsset 加载策略（可选扩展）
- UniTask 异步加载支持（可选）
- 资源预加载和批量预加载
- 资源释放管理

### Features
- **同步加载**: `Load<T>(string path)`
- **异步加载**: `LoadAsync<T>(string path, Action<float> onProgress, CancellationToken cancellationToken)`
- **加载并实例化**: `LoadAndInstantiateAsync<T>(string path, Transform parent)`
- **预加载**: `PreloadAsync<T>(string path)` 和 `PreloadBatchAsync<T>(IEnumerable<string> paths)`
- **资源释放**: `Release(string path)`, `ReleaseInstance(Object instance)`, `ReleaseAll()`
- **查询接口**: `IsLoaded(string path)`, `GetLoadedAssets()`

### Dependencies
- Unity 6000.0.59f2 或更高版本
- UniTask (可选): 用于异步加载功能
- YooAsset (可选): 用于资源包加载

### Notes
- 定义 `UNITASK_ENABLED` 或 `UNITASK` 启用 UniTask 支持
- 定义 `YOOASSET` 或 `YOOASSET_ENABLED` 启用 YooAsset 支持
