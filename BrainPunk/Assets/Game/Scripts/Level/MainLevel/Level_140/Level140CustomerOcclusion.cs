using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

/// <summary>只给本关膨胀退场的角色实例分配裁切材质；顾客及三份通关的玩家共用，不修改共享 Box 材质。</summary>
public sealed class Level140CustomerOcclusion : MonoBehaviour
{
    private readonly Dictionary<Material, Material> replacements = new Dictionary<Material, Material>();
    private readonly Dictionary<SpriteRenderer, Material> clothingOriginals = new Dictionary<SpriteRenderer, Material>();
    private SkeletonAnimation skeleton;

    /// <summary>为指定角色及可选顾客衣服安装低于台面世界Y的独立裁切材质；每个组件只允许应用一次，销毁时恢复并回收。主角不传衣服时保持原逻辑。</summary>
    public void Apply(SkeletonAnimation target, Shader shader, float surfaceY, SpriteRenderer[] clothing = null)
    {
        if (skeleton != null) return;
        skeleton = target;
        foreach (var atlas in target.skeletonDataAsset.atlasAssets)
        {
            foreach (var source in atlas.Materials)
            {
                if (source == null || replacements.ContainsKey(source)) continue;
                var material = new Material(source) { name = "Level140_ClippedBox", shader = shader };
                if (source.IsKeywordEnabled("_STRAIGHT_ALPHA_INPUT")) material.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
                else material.DisableKeyword("_STRAIGHT_ALPHA_INPUT");
                material.SetFloat("_Level140ClipY", surfaceY);
                replacements.Add(source, material);
                target.CustomMaterialOverride[source] = material;
            }
        }
        if (clothing == null) return;
        foreach (var renderer in clothing)
        {
            var source = renderer.sharedMaterial;
            if (!replacements.TryGetValue(source, out var material))
            {
                material = new Material(source) { name = "Level140_ClippedClothes", shader = shader };
                // 普通Sprite纹理不是Spine预乘透明图，采样后需要先乘alpha。
                material.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
                material.SetFloat("_Level140ClipY", surfaceY);
                replacements.Add(source, material);
            }
            clothingOriginals.Add(renderer, source);
            renderer.sharedMaterial = material;
        }
    }

    private void OnDestroy()
    {
        foreach (var pair in clothingOriginals)
            if (pair.Key != null) pair.Key.sharedMaterial = pair.Value;
        clothingOriginals.Clear();
        foreach (var pair in replacements)
        {
            if (skeleton != null && skeleton.CustomMaterialOverride.TryGetValue(pair.Key, out var current) && current == pair.Value)
                skeleton.CustomMaterialOverride.Remove(pair.Key);
            Destroy(pair.Value);
        }
        replacements.Clear();
    }
}
