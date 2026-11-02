using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Editor.UIGenerator
{
    /// <summary>
    /// Photoshop / 设计工具导出的 document + layers（含 isGroup / children）结构。
    /// </summary>
    [System.Serializable]
    public class PsdExportRoot
    {
        [JsonProperty("document")]
        public PsdDocumentInfo Document { get; set; }

        [JsonProperty("layers")]
        public List<PsdLayerNode> Layers { get; set; }
    }

    [System.Serializable]
    public class PsdDocumentInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("width")]
        public float Width { get; set; }

        [JsonProperty("height")]
        public float Height { get; set; }

        [JsonProperty("resolution")]
        public float Resolution { get; set; }

        [JsonProperty("mode")]
        public string Mode { get; set; }

        /// <summary>json+polyfill 等新导出脚本附带的元信息（可选）。</summary>
        [JsonProperty("exportMeta")]
        public PsdExportMeta ExportMeta { get; set; }
    }

    [System.Serializable]
    public class PsdExportMeta
    {
        [JsonProperty("format")]
        public string Format { get; set; }

        [JsonProperty("scriptVersion")]
        public string ScriptVersion { get; set; }
    }

    /// <summary>图层填充色 / 文本颜色（新格式为 r,g,b,hex）。</summary>
    [System.Serializable]
    public class PsdLayerColor
    {
        [JsonProperty("hex")]
        public string Hex { get; set; }

        [JsonProperty("r")]
        public int R { get; set; }

        [JsonProperty("g")]
        public int G { get; set; }

        [JsonProperty("b")]
        public int B { get; set; }
    }

    /// <summary>PSD 文本描边参数；宽度单位由 widthUnit 明确给出。</summary>
    [System.Serializable]
    public class PsdTextOutlineData
    {
        /// <summary>是否启用文本描边；关闭时生成器保持字体基础材质。</summary>
        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        /// <summary>描边宽度；当前 PSD 导出样例使用像素值。</summary>
        [JsonProperty("width")]
        public float Width { get; set; }

        /// <summary>描边宽度单位，例如 px。</summary>
        [JsonProperty("widthUnit")]
        public string WidthUnit { get; set; }

        /// <summary>描边颜色；优先使用其中的 hex。</summary>
        [JsonProperty("color")]
        public PsdLayerColor Color { get; set; }
    }

    [System.Serializable]
    public class PsdLayerNode
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("visible")]
        public bool Visible { get; set; }

        /// <summary>图层整体不透明度 0–100；null 表示 JSON 未提供该字段。</summary>
        [JsonProperty("opacity")]
        public float? Opacity { get; set; }

        /// <summary>设计坐标系中的图层旋转角度（度；新导出）；null 表示 JSON 未提供该字段。</summary>
        [JsonProperty("rotation")]
        public float? Rotation { get; set; }

        [JsonProperty("isGroup")]
        public bool IsGroup { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("bounds")]
        public PsdBounds Bounds { get; set; }

        [JsonProperty("children")]
        public List<PsdLayerNode> Children { get; set; }

        [JsonProperty("color")]
        public PsdLayerColor LayerColor { get; set; }

        /// <summary>文本图层实际文案（新导出）。</summary>
        [JsonProperty("content")]
        public string Content { get; set; }

        /// <summary>文本字号（点，与 PS 一致；新导出）。</summary>
        [JsonProperty("fontSize")]
        public float FontSize { get; set; }

        /// <summary>文本描边参数；仅文本图层使用。</summary>
        [JsonProperty("outline")]
        public PsdTextOutlineData Outline { get; set; }

        /// <summary>切图文件名或相对路径（新导出，与图层名解耦；多图层可共用同一 src）。</summary>
        [JsonProperty("src")]
        public string Src { get; set; }
    }

    [System.Serializable]
    public class PsdBounds
    {
        [JsonProperty("left")]
        public float Left { get; set; }

        [JsonProperty("top")]
        public float Top { get; set; }

        [JsonProperty("right")]
        public float Right { get; set; }

        [JsonProperty("bottom")]
        public float Bottom { get; set; }

        [JsonProperty("width")]
        public float Width { get; set; }

        [JsonProperty("height")]
        public float Height { get; set; }
    }
}
