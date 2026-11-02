#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Dig
{
    public sealed partial class RandomSandSpriteGenerator
    {
        #region Prefab 显式生成
        /// <summary>默认装饰材质的稳定标识；随 meta 保留，移动资源目录后仍能定位，仅显式 Generate 读取。</summary>
        private const string DefaultDecorationMaterialGuid = "8a17503625cd40cea308cfcda9dd412a";

        /// <summary>在当前 Prefab Mode 保存随机装饰布局；再次执行替换本组件旧输出，不改沙土地图。</summary>
        [Button("Generate", ButtonSizes.Large), DisableInPlayMode]
        public void Generate()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Generate 只能在编辑器非播放态执行。");
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || !stage.IsPartOfPrefabContents(gameObject))
                throw new InvalidOperationException("请在目标关卡 Prefab Mode 中生成沙土装饰。");
            if (!sandSprite || !sandSprite.sprite || !sandVisual || sandVisual.gameObject != sandSprite.gameObject)
                throw new InvalidOperationException("请显式绑定同一 Sand 的 SpriteRenderer 与 SceneSpriteBrushClear。");
            if (count < 0 || region.width <= 0 || region.height <= 0 || spriteScale <= 0 ||
                !float.IsFinite(region.x) || !float.IsFinite(region.y) || !float.IsFinite(region.width) ||
                !float.IsFinite(region.height) || !float.IsFinite(spriteScale))
                throw new InvalidOperationException("区域和 Sprite Scale 必须是有限正数，Count 必须非负。");
            Vector3 scale = new Vector3(spriteScale / transform.TransformVector(Vector3.right).magnitude,
                spriteScale / transform.TransformVector(Vector3.up).magnitude, 1f);
            if (!float.IsFinite(scale.x) || !float.IsFinite(scale.y))
                throw new InvalidOperationException("Generator 的世界横纵尺寸不能为零。");
            var choices = new List<Sprite>();
            foreach (Sprite sprite in sprites)
            {
                if (!sprite) continue;
                if (sprite.bounds.size.x * scale.x > region.width || sprite.bounds.size.y * scale.y > region.height)
                    throw new InvalidOperationException($"{sprite.name} 无法完整放入区域，请增大 Region 或减小 Sprite Scale。");
                choices.Add(sprite);
            }
            if (count > 0 && choices.Count == 0) throw new InvalidOperationException("Sprites 至少需要一张有效装饰图。");

            Material material = decorationMaterial;
            if (!material)
                material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(DefaultDecorationMaterialGuid));
            if (!material)
                throw new InvalidOperationException("未找到默认 DigSandDecoration 材质，请恢复材质及其 meta，或显式绑定 Decoration Material。");
            if (!material.shader || material.shader.name != "Dig/SandDecoration")
                throw new InvalidOperationException($"Decoration Material（{material.name}）必须使用 Dig/SandDecoration Shader。");

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("生成随机沙土装饰");
            Undo.RegisterCompleteObjectUndo(this, "生成随机沙土装饰");
            decorationMaterial = material;
            foreach (SpriteRenderer renderer in generatedSprites)
                if (renderer) Undo.DestroyObjectImmediate(renderer.gameObject);

            generatedSprites = new SpriteRenderer[count];
            var random = new System.Random();
            for (int i = 0; i < count; i++)
            {
                Sprite sprite = choices[random.Next(choices.Count)];
                Bounds bounds = sprite.bounds;
                float minX = region.xMin - bounds.min.x * scale.x, maxX = region.xMax - bounds.max.x * scale.x;
                float minY = region.yMin - bounds.min.y * scale.y, maxY = region.yMax - bounds.max.y * scale.y;
                var child = new GameObject("SandDecoration_" + i) { layer = gameObject.layer };
                Undo.RegisterCreatedObjectUndo(child, "生成随机沙土装饰");
                child.transform.SetParent(transform, false);
                child.transform.localPosition = new Vector3(Mathf.Lerp(minX, maxX, (float)random.NextDouble()),
                    Mathf.Lerp(minY, maxY, (float)random.NextDouble()), 0f);
                child.transform.localScale = scale;
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sharedMaterial = decorationMaterial;
                renderer.sortingLayerID = sandSprite.sortingLayerID;
                renderer.sortingOrder = Mathf.Clamp(sandSprite.sortingOrder + sortingOrderOffset, short.MinValue, short.MaxValue);
                generatedSprites[i] = renderer;
            }
            RefreshEditorPreview();
            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkSceneDirty(stage.scene);
            Undo.CollapseUndoOperations(undoGroup);
        }
        #endregion

        #region 无运行时数据的预览
        private MaterialPropertyBlock _previewBlock;

        partial void RefreshEditorPreview()
        {
            if (!sandSprite || !sandVisual || !decorationMaterial || generatedSprites.Length == 0) return;
            // 只缓存编辑器绘制参数；不创建玩法节点/材质实例，不触发 DB、单例或 runtime 初始化。
            _previewBlock ??= new MaterialPropertyBlock();
            _previewBlock.SetMatrix(SandWorldToLocalId, sandSprite.transform.worldToLocalMatrix);
            _previewBlock.SetVector(SandRectId, SandRect());
            _previewBlock.SetTexture(MaskTexId, sandVisual.InitialMask ? sandVisual.InitialMask : Texture2D.blackTexture);
            foreach (SpriteRenderer renderer in generatedSprites)
                if (renderer) renderer.SetPropertyBlock(_previewBlock);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 a = transform.TransformPoint(new Vector3(region.xMin, region.yMin)),
                b = transform.TransformPoint(new Vector3(region.xMax, region.yMin)),
                c = transform.TransformPoint(new Vector3(region.xMax, region.yMax)),
                d = transform.TransformPoint(new Vector3(region.xMin, region.yMax));
            Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
        }
        #endregion
    }
}
#endif
