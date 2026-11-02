using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

namespace Game.Editor.SpriteAtlasTools
{
    [CreateAssetMenu(
        fileName = "SpriteAtlasPackerConfig",
        menuName = "Game/SpriteAtlas/SpriteAtlas Packer Config")]
    public class SpriteAtlasPackerConfig : ScriptableObject
    {
        [Header("Atlas 目标")]
        public SpriteAtlas atlas;

        [Header("要打进 Atlas 的目录（拖 Folder）")]
        public List<Object> spriteFolders = new List<Object>();

        [Header("打包参数")]
        public bool includeInBuild = true;
        public int maxTextureSize = 2048;
        public bool tightPacking = true;
        public bool rotation = false;

        [Header("过滤（像素）")]
        public bool enableSizeFilter = false;
        public int maxSpriteWidth = 512;
        public int maxSpriteHeight = 512;
    }
}

