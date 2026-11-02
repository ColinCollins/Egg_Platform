using System;
using System.Collections.Generic;
using System.Threading;
using Bear.EventSystem;
using Bear.Logger;
using Config.Game;
using Cysharp.Threading.Tasks;
using Game.Events;
using Spine.Unity;
using UnityEngine;

namespace Game.Level.Actor
{
    /// <summary>
    /// 声明当前关卡控制器负责的玩家 Actor，供皮肤绑定在不依赖具体关卡类型的前提下校验目标身份。
    /// 调用边界：实现方只能返回本关实际受控的玩家 Actor；克隆体、工具和敌人不得作为该属性结果。
    /// </summary>
    public interface IActorSkinBindingOwner
    {
        /// <summary>
        /// 当前所有者实际负责、允许应用玩家皮肤的 Actor。
        /// </summary>
        ActorCtrl Actor { get; }
    }

    public sealed class ActorSkinBinding : MonoBehaviour, IDebuger
    {
        private enum BindingMode
        {
            LevelPlayerOnly,
            ExplicitOptIn
        }

        [Header("Target")]
        [SerializeField] private ActorCtrl actor;
        [SerializeField] private ActorSpineCtrl actorSpine;
        [SerializeField] private BindingMode bindingMode = BindingMode.LevelPlayerOnly;

        [Header("Fallback")]
        [SerializeField] private string fallbackFullBodySkinName = "box_original";

        [Header("Part Mounts")]
        [SerializeField] private Transform backMount;
        [SerializeField] private Transform faceMount;
        [SerializeField] private Transform bodyMount;

        private readonly Dictionary<SkinPart, GameObject> _partInstances = new();
        private EventSubscriber _subscriber;
        private int _applyVersion;
        private bool _eligible;

        /// <summary>
        /// 当前保存的 Skin 选择和部件异步加载处理结束后触发；仅用于同一 Actor 外部表现刷新，订阅方不得在回调内写存档或再次触发 Skin 绑定。
        /// </summary>
        public event Action<ActorSkinBinding> CurrentSelectionApplied;

        private void Awake()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ResolveReferences();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ResolveReferences();
            _eligible = IsEligibleForBinding();
            BindEvents();

            if (_eligible)
            {
                ApplyCurrentSelection();
            }
        }

        private void Start()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            RefreshFromCurrentSelection();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            _applyVersion++;
            EventsUtils.ResetEvents(ref _subscriber);
            ClearAllOwnedParts();
        }

        private void OnDestroy()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            _applyVersion++;
            EventsUtils.ResetEvents(ref _subscriber);
            ClearAllOwnedParts();
        }

        public void RefreshFromCurrentSelection()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ResolveReferences();
            _eligible = IsEligibleForBinding();
            if (!_eligible)
            {
                return;
            }

            ApplyCurrentSelection();
        }

        private void BindEvents()
        {
            EventsUtils.ResetEvents(ref _subscriber);
            _subscriber.Subscribe<ActorSkinSelectionChangedEvent>(OnSkinSelectionChanged);
        }

        private void OnSkinSelectionChanged(ActorSkinSelectionChangedEvent _)
        {
            RefreshFromCurrentSelection();
        }

        private void ApplyCurrentSelection()
        {
            var gameData = DB.GameData;
            int applyVersion = ++_applyVersion;

            if (gameData == null)
            {
                this.LogWarning("[ActorSkinBinding] GameData is null. Applying fallback actor skin.");
                ApplyFullBodySkin(string.Empty);
                ClearAllOwnedParts();
                NotifyCurrentSelectionApplied(applyVersion);
                return;
            }

            ApplyCurrentSelectionAsync(
                    gameData.CurrentSkinId,
                    gameData.CurrentBackPart,
                    gameData.CurrentFacePart,
                    gameData.CurrentBodyPart,
                    applyVersion)
                .Forget();
        }

        private async UniTaskVoid ApplyCurrentSelectionAsync(
            string fullBodySkinId,
            string backPart,
            string facePart,
            string bodyPart,
            int applyVersion)
        {
            if (this == null || !isActiveAndEnabled || applyVersion != _applyVersion)
            {
                return;
            }

            ApplyFullBodySkin(fullBodySkinId);
            await UniTask.WhenAll(
                ApplyPartAsync(SkinPart.Back, backPart, applyVersion),
                ApplyPartAsync(SkinPart.Face, facePart, applyVersion),
                ApplyPartAsync(SkinPart.Body, bodyPart, applyVersion));
            NotifyCurrentSelectionApplied(applyVersion);
        }

        private void ApplyFullBodySkin(string savedSkinId)
        {
            if (actorSpine == null)
            {
                return;
            }

            string skinName = SkinUtils.ResolveFullBodySkinName(savedSkinId);
            if (!string.IsNullOrEmpty(skinName) && actorSpine.SetSkin(skinName))
            {
                return;
            }

            if (!string.IsNullOrEmpty(fallbackFullBodySkinName))
            {
                actorSpine.SetSkin(fallbackFullBodySkinName);
            }
        }

        private async UniTask ApplyPartAsync(SkinPart part, string savedValue, int applyVersion)
        {
            var mount = GetMount(part);
            if (mount == null)
            {
                ClearPart(part);
                return;
            }

            if (string.IsNullOrEmpty(savedValue))
            {
                ClearPart(part);
                return;
            }

            var skin = SkinUtils.ResolveSavedSkin(savedValue, part);
            if (skin == null)
            {
                ClearPart(part);
                this.LogWarning($"[ActorSkinBinding] Part skin not found. part:{part}, saved:{savedValue}");
                return;
            }

            string prefabLocation = SkinUtils.ResolvePrefabLocation(skin.PrefabResource);
            if (string.IsNullOrEmpty(prefabLocation))
            {
                ClearPart(part);
                this.LogWarning($"[ActorSkinBinding] Part PrefabResource is empty. part:{part}, skin:{skin.Id}");
                return;
            }

            ClearPart(part);

            GameObject prefab = null;
            try
            {
                if (!SkinUtils.CanLoadPrefabLocation(prefabLocation))
                {
                    this.LogWarning($"[ActorSkinBinding] Part prefab cannot load. part:{part}, prefab:{prefabLocation}");
                    return;
                }

                CancellationToken cancellationToken = this.GetCancellationTokenOnDestroy();
                prefab = await SkinUtils.LoadPrefabAsync(prefabLocation, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                this.LogWarning($"[ActorSkinBinding] Load part prefab failed. part:{part}, prefab:{prefabLocation}, error:{ex.Message}");
                return;
            }

            if (this == null || !isActiveAndEnabled || applyVersion != _applyVersion)
            {
                return;
            }

            if (prefab == null)
            {
                ClearPart(part);
                this.LogWarning($"[ActorSkinBinding] Loaded part prefab is null. part:{part}, prefab:{prefabLocation}");
                return;
            }

            var instance = Instantiate(prefab, mount);
            instance.name = prefab.name;
            var instanceTransform = instance.transform;
            instanceTransform.localPosition = Vector3.zero;
            instanceTransform.localRotation = Quaternion.identity;
            instanceTransform.localScale = Vector3.one;
            _partInstances[part] = instance;
        }

        private void NotifyCurrentSelectionApplied(int applyVersion)
        {
            if (this == null || !isActiveAndEnabled || applyVersion != _applyVersion)
            {
                return;
            }

            CurrentSelectionApplied?.Invoke(this);
        }

        private bool IsEligibleForBinding()
        {
            if (bindingMode == BindingMode.ExplicitOptIn)
            {
                return true;
            }

            if (actor == null)
            {
                return false;
            }

            var owner = actor.GetComponentInParent<IActorSkinBindingOwner>(true);
            return owner != null && owner.Actor == actor;
        }

        private void ResolveReferences()
        {
            actor ??= GetComponent<ActorCtrl>();
            actorSpine ??= actor != null ? actor.SpineCtrl : null;
            actorSpine ??= GetComponentInChildren<ActorSpineCtrl>(true);

            var lookupRoot = actor != null ? actor.transform : transform;
            backMount ??= FindPartMount(lookupRoot, "back") ?? FindPartMount(lookupRoot, "Back");
            faceMount ??= FindPartMount(lookupRoot, "face") ?? FindPartMount(lookupRoot, "Face");
            bodyMount ??= FindPartMount(lookupRoot, "body") ?? FindPartMount(lookupRoot, "Body");
        }

        private Transform GetMount(SkinPart part)
        {
            return part switch
            {
                SkinPart.Back => backMount,
                SkinPart.Face => faceMount,
                SkinPart.Body => bodyMount,
                _ => null
            };
        }

        private void ClearPart(SkinPart part)
        {
            if (_partInstances.TryGetValue(part, out var instance) && instance != null)
            {
                Destroy(instance);
            }

            _partInstances.Remove(part);
        }

        private void ClearAllOwnedParts()
        {
            foreach (var instance in _partInstances.Values)
            {
                if (instance != null)
                {
                    Destroy(instance);
                }
            }

            _partInstances.Clear();
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
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

        private static Transform FindPartMount(Transform root, string childName)
        {
            var firstMatch = FindChildRecursive(root, childName);
            var followerMatch = FindChildWithFollowerRecursive(root, childName);
            return followerMatch != null ? followerMatch : firstMatch;
        }

        private static Transform FindChildWithFollowerRecursive(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == childName &&
                    (child.GetComponent<BoneFollowerAutoBinder>() != null || child.GetComponent<BoneFollower>() != null))
                {
                    return child;
                }

                var result = FindChildWithFollowerRecursive(child, childName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}
