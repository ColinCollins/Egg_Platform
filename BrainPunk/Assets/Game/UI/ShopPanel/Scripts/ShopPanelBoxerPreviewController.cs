using System;
using System.Collections.Generic;
using Bear.Logger;
using Config.Game;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ShopPanel Boxer 预览运行时控制器。
/// 只负责场景预览对象解析、RenderTexture、Spine 动作和部件 prefab 生命周期；
/// 不读取存档、不处理皮肤所有权、购买或事件派发。
/// </summary>
internal sealed class ShopPanelBoxerPreviewController : IDebuger, IDisposable
{
    private const string BackAnimationName = "preview_skin_(cap)";
    private const string BodyAnimationName = "preview_skin_(clothing)";
    private const string FullBodyAnimationName = "preview_skin_(full)";
    private const string FaceAnimationName = "preview_skin_(glasses)";
    private const string IdleAnimationName = "idle";
    private const int IgnoreRaycastLayer = 2;

    #region Runtime State

    private readonly Transform _uiRoot;
    private readonly string _sceneRootName;
    private readonly string _fallbackBaseSkinName;
    private readonly Dictionary<string, GameObject> _prefabCache = new();
    private readonly Dictionary<Transform, GameObject> _instances = new();

    private GameObject _sceneRoot;
    private Camera _camera;
    private RawImage _previewImage;
    private RenderTexture _renderTexture;
    private ActorSpineCtrl _actorSpine;
    private Transform _back;
    private Transform _face;
    private Transform _body;
    private int _applyVersion;
    private bool _hasLoggedMissingAnimation;
    private bool _hasLoggedMissingIdleAnimation;
    private bool _disposed;

    private static GameObject _cachedSceneRoot;

    #endregion

    #region Construction And Lifecycle

    /// <summary>
    /// 创建当前 ShopPanel 生命周期专用的 Boxer 预览控制器。
    /// UI 根节点只用于查找 Boxer_Rt 兜底；场景 Actor 引用仍从已加载场景根节点解析。
    /// </summary>
    internal ShopPanelBoxerPreviewController(
        Transform uiRoot,
        string sceneRootName,
        string fallbackBaseSkinName,
        RawImage previewImage,
        RenderTexture renderTexture)
    {
        _uiRoot = uiRoot;
        _sceneRootName = sceneRootName;
        _fallbackBaseSkinName = fallbackBaseSkinName;
        _previewImage = previewImage;
        _renderTexture = renderTexture;
    }

    /// <summary>
    /// 打开场景预览并清理上一次页面生命周期残留；调用方随后应传入当前有效皮肤值执行 Apply。
    /// </summary>
    internal void Open()
    {
        if (_disposed)
        {
            return;
        }

        _applyVersion++;
        SetPreviewActive(true, true);
        ClearAllParts();
        ClearRenderTexture();
    }

    /// <summary>
    /// 恢复当前 ShopPanel 生命周期内已保留的场景预览；不清理部件实例或 RenderTexture。
    /// </summary>
    internal void Resume()
    {
        if (_disposed)
        {
            return;
        }

        SetPreviewActive(true, false);
    }

    /// <summary>
    /// 暂时隐藏场景预览；用于 Skin 页签禁用，保留当前部件实例和未完成加载，等待同一 ShopPanel 内恢复。
    /// </summary>
    internal void Suspend()
    {
        if (_disposed)
        {
            return;
        }

        SetPreviewActive(false, false);
    }

    /// <summary>
    /// 关闭场景预览、使未完成异步加载失效并清理运行时部件实例；不会释放 prefab 缓存。
    /// </summary>
    internal void Close()
    {
        if (_disposed)
        {
            return;
        }

        _applyVersion++;
        ClearAllParts();
        SetPreviewActive(false, true);
    }

    /// <summary>
    /// 释放当前控制器。释放后不得再次用于打开或应用预览。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Close();
        _disposed = true;
        _prefabCache.Clear();
    }

    /// <summary>
    /// ShopPanel 关闭路径使用的静态保护：即使 SkinPart 已禁用，也隐藏场景 Skin 根节点。
    /// </summary>
    internal static void HideScenePreview(string rootName)
    {
        var root = ResolveSceneRoot(rootName, true);
        if (root != null)
        {
            root.SetActive(false);
        }
    }

    #endregion

    #region Appearance Application

    /// <summary>
    /// 应用调用方已经从当前存档与临时试穿状态合成的有效皮肤值。
    /// animationPart 为空时只恢复外观，不播放切换动作。
    /// </summary>
    internal void Apply(
        string backValue,
        string faceValue,
        string bodyValue,
        string fullBodyValue,
        SkinPart? animationPart)
    {
        if (_disposed)
        {
            return;
        }

        ResolveReferences();
        int applyVersion = ++_applyVersion;
        ApplyPartAsync(_back, backValue, SkinPart.Back, applyVersion).Forget();
        ApplyPartAsync(_face, faceValue, SkinPart.Face, applyVersion).Forget();
        ApplyPartAsync(_body, bodyValue, SkinPart.Body, applyVersion).Forget();

        string fullBodySkinName = SkinUtils.ResolveFullBodySkinName(fullBodyValue);
        ApplyFullBodySkin(string.IsNullOrEmpty(fullBodySkinName) ? _fallbackBaseSkinName : fullBodySkinName);
        if (animationPart.HasValue)
        {
            PlaySwitchAnimationIfNeeded(animationPart.Value);
        }
    }

    private void ApplyFullBodySkin(string fullBodySkinName)
    {
        if (_actorSpine == null)
        {
            ResolveReferences();
        }

        if (_actorSpine == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(fullBodySkinName) && _actorSpine.SetSkin(fullBodySkinName))
        {
            return;
        }

        if (!string.IsNullOrEmpty(_fallbackBaseSkinName) &&
            !string.Equals(fullBodySkinName, _fallbackBaseSkinName, StringComparison.Ordinal))
        {
            _actorSpine.SetSkin(_fallbackBaseSkinName);
        }
    }

    private void PlaySwitchAnimationIfNeeded(SkinPart skinPart)
    {
        if (_actorSpine == null)
        {
            ResolveReferences();
        }

        var animationState = _actorSpine != null ? _actorSpine.AnimationState : null;
        if (animationState == null)
        {
            return;
        }

        string animationName = GetAnimationName(skinPart);
        var currentEntry = animationState.GetCurrent(0);
        if (currentEntry != null &&
            currentEntry.Animation != null &&
            string.Equals(currentEntry.Animation.Name, animationName, StringComparison.Ordinal) &&
            (currentEntry.Loop || !currentEntry.IsComplete))
        {
            return;
        }

        var skeletonData = animationState.Data?.SkeletonData;
        if (skeletonData == null || skeletonData.FindAnimation(animationName) == null)
        {
            if (!_hasLoggedMissingAnimation)
            {
                _hasLoggedMissingAnimation = true;
                this.LogWarning($"[ShopPanelBoxerPreviewController] Boxer preview animation not found: {animationName}");
            }

            return;
        }

        var previewEntry = _actorSpine.PlayAnimation(animationName, false, false);
        if (previewEntry == null)
        {
            return;
        }

        if (skeletonData.FindAnimation(IdleAnimationName) == null)
        {
            if (!_hasLoggedMissingIdleAnimation)
            {
                _hasLoggedMissingIdleAnimation = true;
                this.LogWarning($"[ShopPanelBoxerPreviewController] Boxer idle animation not found: {IdleAnimationName}");
            }

            return;
        }

        animationState.AddAnimation(0, IdleAnimationName, true, 0f);
    }

    private static string GetAnimationName(SkinPart skinPart)
    {
        return skinPart switch
        {
            SkinPart.Back => BackAnimationName,
            SkinPart.Body => BodyAnimationName,
            SkinPart.FullBody => FullBodyAnimationName,
            SkinPart.Face => FaceAnimationName,
            _ => BackAnimationName
        };
    }

    #endregion

    #region Reference Resolution

    private void ResolveReferences()
    {
        ResolveSceneRootReference(false);
        var lookupRoot = _sceneRoot != null ? _sceneRoot.transform : _uiRoot;
        if (_sceneRoot != null)
        {
            ClearReferencesOutsideRoot(lookupRoot);
        }

        _actorSpine ??= lookupRoot != null ? lookupRoot.GetComponentInChildren<ActorSpineCtrl>(true) : null;
        if (_camera == null)
        {
            var cameraRoot = FindChildRecursive(lookupRoot, "SkinRt_Camera") ?? FindChildRecursive(lookupRoot, "Camera");
            _camera = cameraRoot != null ? cameraRoot.GetComponent<Camera>() : null;
            _camera ??= lookupRoot != null ? lookupRoot.GetComponentInChildren<Camera>(true) : null;
        }

        if (_previewImage == null)
        {
            var boxerRtRoot = FindChildRecursive(lookupRoot, "Boxer_Rt");
            _previewImage = boxerRtRoot != null ? boxerRtRoot.GetComponent<RawImage>() : null;
            _previewImage ??= FindRenderTextureRawImage(lookupRoot);
            _previewImage ??= FindRenderTextureRawImage(_uiRoot);
        }

        if (_renderTexture == null)
        {
            _renderTexture = _camera != null ? _camera.targetTexture : null;
            _renderTexture ??= _previewImage != null ? _previewImage.texture as RenderTexture : null;
        }

        if (_camera != null && _renderTexture != null && _camera.targetTexture != _renderTexture)
        {
            _camera.targetTexture = _renderTexture;
        }

        if (_previewImage != null && _renderTexture != null && _previewImage.texture != _renderTexture)
        {
            _previewImage.texture = _renderTexture;
        }

        ResolvePartReferences(lookupRoot);
    }

    private void ResolvePartReferences(Transform lookupRoot)
    {
        if (_sceneRoot != null)
        {
            if (!IsSelfOrChildOf(_back, lookupRoot)) _back = null;
            if (!IsSelfOrChildOf(_body, lookupRoot)) _body = null;
            if (!IsSelfOrChildOf(_face, lookupRoot)) _face = null;
        }

        _back ??= FindChildRecursive(lookupRoot, "back") ?? FindChildRecursive(lookupRoot, "Hat") ?? FindChildRecursive(lookupRoot, "Back");
        _body ??= FindChildRecursive(lookupRoot, "body") ?? FindChildRecursive(lookupRoot, "Shirt") ?? FindChildRecursive(lookupRoot, "Body");
        _face ??= FindChildRecursive(lookupRoot, "face") ?? FindChildRecursive(lookupRoot, "Face");
    }

    private void ClearReferencesOutsideRoot(Transform lookupRoot)
    {
        if (!IsSelfOrChildOf(_camera != null ? _camera.transform : null, lookupRoot)) _camera = null;
        if (!IsSelfOrChildOf(_actorSpine != null ? _actorSpine.transform : null, lookupRoot)) _actorSpine = null;
    }

    private static bool IsSelfOrChildOf(Transform target, Transform root)
    {
        if (target == null || root == null)
        {
            return false;
        }

        return target == root || target.IsChildOf(root);
    }

    private void ResolveSceneRootReference(bool includeInactive)
    {
        if (_sceneRoot != null)
        {
            _cachedSceneRoot = _sceneRoot;
            return;
        }

        _sceneRoot = ResolveSceneRoot(_sceneRootName, includeInactive);
    }

    private static GameObject ResolveSceneRoot(string rootName, bool includeInactive)
    {
        if (_cachedSceneRoot != null && _cachedSceneRoot.scene.IsValid())
        {
            return _cachedSceneRoot;
        }

        if (string.IsNullOrEmpty(rootName))
        {
            return null;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            var roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                var root = roots[rootIndex];
                if (root == null || !string.Equals(root.name, rootName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!includeInactive && !root.activeInHierarchy)
                {
                    continue;
                }

                _cachedSceneRoot = root;
                return root;
            }
        }

        return null;
    }

    private static RawImage FindRenderTextureRawImage(Transform lookupRoot)
    {
        if (lookupRoot == null)
        {
            return null;
        }

        var rawImages = lookupRoot.GetComponentsInChildren<RawImage>(true);
        for (int i = 0; i < rawImages.Length; i++)
        {
            if (rawImages[i] != null && rawImages[i].texture is RenderTexture)
            {
                return rawImages[i];
            }
        }

        return null;
    }

    #endregion

    #region Preview Lifecycle

    private void SetPreviewActive(bool active, bool clearRenderTexture)
    {
        ResolveSceneRootReference(true);
        if (_sceneRoot != null && _sceneRoot.activeSelf != active)
        {
            _sceneRoot.SetActive(active);
        }

        ResolveReferences();
        bool isRenderTextureReady = !active || EnsureRenderTextureCreated();
        if (_camera != null)
        {
            _camera.enabled = active && isRenderTextureReady;
        }

        if (clearRenderTexture && isRenderTextureReady)
        {
            ClearRenderTexture();
        }
    }

    private bool EnsureRenderTextureCreated()
    {
        if (_renderTexture == null)
        {
            this.LogWarning("[ShopPanelBoxerPreviewController] Boxer RenderTexture is missing.");
            return false;
        }

        if (_renderTexture.IsCreated() || _renderTexture.Create())
        {
            return true;
        }

        this.LogWarning(
            $"[ShopPanelBoxerPreviewController] Boxer RenderTexture create failed. " +
            $"graphicsDevice:{SystemInfo.graphicsDeviceType}, format:{_renderTexture.graphicsFormat}");
        return false;
    }

    private void ClearRenderTexture()
    {
        if (_renderTexture == null)
        {
            return;
        }

        var previous = RenderTexture.active;
        RenderTexture.active = _renderTexture;
        GL.Clear(false, true, Color.clear);
        RenderTexture.active = previous;
    }

    #endregion

    #region Part Loading

    private async UniTaskVoid ApplyPartAsync(Transform target, string savedValue, SkinPart part, int applyVersion)
    {
        if (target == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(savedValue))
        {
            ClearPart(target);
            return;
        }

        var skin = SkinUtils.ResolveSavedSkin(savedValue, part);
        if (skin == null)
        {
            ClearPart(target);
            this.LogWarning($"[ShopPanelBoxerPreviewController] Boxer part skin not found. part:{part}, saved:{savedValue}");
            return;
        }

        string prefabLocation = SkinUtils.ResolvePrefabLocation(skin.PrefabResource);
        if (string.IsNullOrEmpty(prefabLocation))
        {
            ClearPart(target);
            this.LogWarning($"[ShopPanelBoxerPreviewController] Boxer part PrefabResource is empty. part:{part}, skin:{skin.Id}");
            return;
        }

        ClearPart(target);
        SetPartVisible(target.gameObject, false);

        GameObject prefab = await LoadPartPrefabAsync(prefabLocation);
        if (_disposed || applyVersion != _applyVersion)
        {
            return;
        }

        if (prefab == null)
        {
            ClearPart(target);
            this.LogWarning($"[ShopPanelBoxerPreviewController] Boxer part prefab not found. part:{part}, skin:{skin.Id}, prefab:{prefabLocation}");
            return;
        }

        var instance = UnityEngine.Object.Instantiate(prefab, target);
        instance.name = prefab.name;
        SetLayerRecursively(instance.transform, IgnoreRaycastLayer);

        var instanceTransform = instance.transform;
        instanceTransform.localPosition = Vector3.zero;
        instanceTransform.localRotation = Quaternion.identity;
        instanceTransform.localScale = Vector3.one;

        if (instanceTransform is RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        _instances[target] = instance;
        SetPartVisible(target.gameObject, true);
    }

    private async UniTask<GameObject> LoadPartPrefabAsync(string location)
    {
        if (string.IsNullOrEmpty(location))
        {
            return null;
        }

        if (_prefabCache.TryGetValue(location, out var cachedPrefab))
        {
            return cachedPrefab;
        }

        if (!SkinUtils.CanLoadPrefabLocation(location))
        {
            return null;
        }

        try
        {
            var prefab = await SkinUtils.LoadPrefabAsync(location);
            if (prefab != null)
            {
                _prefabCache[location] = prefab;
            }

            return prefab;
        }
        catch (Exception ex)
        {
            this.LogWarning($"[ShopPanelBoxerPreviewController] Load boxer prefab failed. location:{location}, error:{ex.Message}");
            return null;
        }
    }

    #endregion

    #region Cleanup And Utilities

    private void ClearPart(Transform target)
    {
        if (target == null)
        {
            return;
        }

        if (_instances.TryGetValue(target, out var instance) &&
            instance != null &&
            instance.transform.parent != target)
        {
            instance.SetActive(false);
            UnityEngine.Object.Destroy(instance);
        }

        ClearPartChildren(target);
        _instances.Remove(target);
        SetPartVisible(target.gameObject, false);
    }

    private void ClearAllParts()
    {
        ClearPart(_back);
        ClearPart(_face);
        ClearPart(_body);

        foreach (var instance in _instances.Values)
        {
            if (instance != null)
            {
                instance.SetActive(false);
                UnityEngine.Object.Destroy(instance);
            }
        }

        _instances.Clear();
    }

    private static void ClearPartChildren(Transform target)
    {
        if (target == null)
        {
            return;
        }

        for (int i = target.childCount - 1; i >= 0; i--)
        {
            var child = target.GetChild(i);
            if (child == null)
            {
                continue;
            }

            child.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    private static void SetPartVisible(GameObject target, bool visible)
    {
        if (target != null && target.activeSelf != visible)
        {
            target.SetActive(visible);
            if (visible)
            {
                target.transform.DOKill();
                target.transform.localScale = Vector3.zero;
                target.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack);
            }
            else
            {
                target.transform.DOKill();
            }
        }
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null)
        {
            return;
        }

        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            var result = FindChildRecursive(child, childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    #endregion
}
