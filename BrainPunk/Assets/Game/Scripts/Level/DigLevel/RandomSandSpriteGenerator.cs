using System;
using Game.Scripts.Effects;
using UnityEngine;

namespace Game.Dig
{
    /// <summary>在编辑器生成并保存沙土装饰；运行时共用 Sand 遮罩逐像素镂空，不参与碰撞或耗墨。</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed partial class RandomSandSpriteGenerator : MonoBehaviour
    {
        #region 美术配置与显式绑定
        /// <summary>提供世界坐标到遮罩 UV 的映射及排序基准；在目标 Prefab 显式绑定。</summary>
        [SerializeField] private SpriteRenderer sandSprite;
        /// <summary>同一 Sand 的擦除组件；只读取其遮罩并订阅局部更新，不调用地图或运行时初始化。</summary>
        [SerializeField] private SceneSpriteBrushClear sandVisual;
#if UNITY_EDITOR
        /// <summary>随机选取的装饰图；Generate 忽略空项，所有有效图必须能完整放进区域。</summary>
        [SerializeField] private Sprite[] sprites = Array.Empty<Sprite>();
        /// <summary>Generator 本地矩形范围；整个 Sprite 的边界都限制在其中，Scene 选中时显示框线。</summary>
        [SerializeField] private Rect region = new Rect(-0.5f, -0.5f, 1f, 1f);
        /// <summary>保存到 Prefab 的生成数量；只在显式 Generate 时生效，允许零个。</summary>
        [SerializeField, Min(0)] private int count = 30;
        /// <summary>Sprite 原始 PPU 尺寸的世界缩放；生成时抵消父节点非等比缩放，不增加随机拉伸。</summary>
        [SerializeField, Min(0.001f)] private float spriteScale = 1f;
        /// <summary>装饰相对 Sand 的排序增量；默认放在沙土上方，需低于角色的排序。</summary>
        [SerializeField] private int sortingOrderOffset = 1;
#endif
        /// <summary>使用 Dig/SandDecoration Shader 的模板材质；Generate 可绑定随脚本提供的本地材质。</summary>
        [SerializeField] private Material decorationMaterial;
        /// <summary>仅由 Generate 保存的输出引用；再次生成只清理这些对象，不按名称或层级扫描。</summary>
        [SerializeField, HideInInspector] private SpriteRenderer[] generatedSprites = Array.Empty<SpriteRenderer>();
        #endregion

        #region 运行时遮罩同步
        private Material _runtimeMaterial;
        private SceneSpriteBrushClear _boundVisual;
        private static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");
        private static readonly int SandRectId = Shader.PropertyToID("_SandRect");
        private static readonly int SandWorldToLocalId = Shader.PropertyToID("_SandWorldToLocal");

        private void OnEnable()
        {
            if (!Application.IsPlaying(gameObject))
            {
                RefreshEditorPreview();
                return;
            }
            if (generatedSprites.Length == 0) return;
            if (!sandSprite || !sandVisual || !decorationMaterial)
                throw new InvalidOperationException("随机沙土装饰未绑定 Sand Sprite、Sand Visual 或 Decoration Material。");

            _runtimeMaterial = new Material(decorationMaterial) { name = "随机沙土装饰（运行时）" };
            _runtimeMaterial.SetMatrix(SandWorldToLocalId, sandSprite.transform.worldToLocalMatrix);
            _runtimeMaterial.SetVector(SandRectId, SandRect());
            UpdateMask(sandVisual.MaskTexture);
            foreach (SpriteRenderer renderer in generatedSprites)
            {
                if (!renderer) continue;
                renderer.SetPropertyBlock(null);
                renderer.sharedMaterial = _runtimeMaterial;
                renderer.enabled = true;
            }
            _boundVisual = sandVisual;
            _boundVisual.MaskTextureChanged += UpdateMask;
        }

        private Vector4 SandRect()
        {
            Bounds bounds = sandSprite.localBounds;
            return new Vector4(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
        }

        private void UpdateMask(RenderTexture texture)
        {
            if (!_runtimeMaterial) return;
            Texture mask = texture ? texture : sandVisual.InitialMask;
            _runtimeMaterial.SetTexture(MaskTexId, mask ? mask : Texture2D.blackTexture);
        }

        private void OnDisable()
        {
            if (!Application.isPlaying && !_runtimeMaterial) return;
            ReleaseRuntime();
        }

        private void OnDestroy()
        {
            if (!Application.isPlaying && !_runtimeMaterial) return;
            ReleaseRuntime();
        }

        private void ReleaseRuntime()
        {
            if (_boundVisual) _boundVisual.MaskTextureChanged -= UpdateMask;
            _boundVisual = null;
            if (!_runtimeMaterial) return;
            foreach (SpriteRenderer renderer in generatedSprites)
            {
                if (!renderer || renderer.sharedMaterial != _runtimeMaterial) continue;
                renderer.sharedMaterial = decorationMaterial;
                renderer.enabled = false;
            }
            if (Application.isPlaying) Destroy(_runtimeMaterial);
            else DestroyImmediate(_runtimeMaterial);
            _runtimeMaterial = null;
        }
        #endregion

        #region 本地编辑器预览
        partial void RefreshEditorPreview();

#if UNITY_EDITOR
        private void Update()
        {
            if (!Application.IsPlaying(gameObject)) RefreshEditorPreview();
        }

        private void OnValidate()
        {
            if (!Application.IsPlaying(gameObject)) RefreshEditorPreview();
        }
#endif
        #endregion
    }
}
