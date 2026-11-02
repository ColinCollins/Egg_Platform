using Newtonsoft.Json;

namespace Game.Editor.UIGenerator
{
    [System.Serializable]
    public class LayerData
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("index")]
        public int Index { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }

        [JsonProperty("width")]
        public float Width { get; set; }

        [JsonProperty("height")]
        public float Height { get; set; }

        /// <summary>设计坐标系中的图层旋转角度（度）；null 表示 JSON 未提供，生成时会转换为 Unity UI 的 Z 轴角度。</summary>
        [JsonProperty("rotation")]
        public float? Rotation { get; set; }

        /// <summary>图层整体不透明度 0–100；null 表示 JSON 未提供，生成时保持组件原有 Alpha。</summary>
        [JsonProperty("opacity")]
        public float? Opacity { get; set; }

        [JsonProperty("textInfo")]
        public TextInfoData TextInfo { get; set; }

        /// <summary>切图文件名或相对路径（与图层名解耦）；优先用于 Sprite 检索。</summary>
        [JsonProperty("src")]
        public string Src { get; set; }

        public bool IsTextLayer => Type == "textLayer";
        public bool IsShapeLayer => Type == "shapeLayer";
        public bool IsImageLayer => Type == "layer";
    }

    [System.Serializable]
    public class TextInfoData
    {
        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("orientation")]
        public string Orientation { get; set; }

        [JsonProperty("bounds")]
        public BoundsData Bounds { get; set; }

        [JsonProperty("fontPostScriptName")]
        public string FontPostScriptName { get; set; }

        [JsonProperty("fontName")]
        public string FontName { get; set; }

        [JsonProperty("fontSize")]
        public float FontSize { get; set; }

        [JsonProperty("bold")]
        public bool Bold { get; set; }

        [JsonProperty("italic")]
        public bool Italic { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        /// <summary>不透明度 0–100；JSON 未提供时为 null，仅用 color 中的 alpha（若有）。</summary>
        [JsonProperty("opacity")]
        public float? Opacity { get; set; }

        /// <summary>文本描边参数；为空或未启用时使用字体基础材质。</summary>
        [JsonProperty("outline")]
        public TextOutlineData Outline { get; set; }
    }

    /// <summary>供 PSD 与旧 Artboard 入口共用的文本描边数据。</summary>
    [System.Serializable]
    public class TextOutlineData
    {
        /// <summary>是否启用描边。</summary>
        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        /// <summary>描边颜色，支持 #RRGGBB 或 #RRGGBBAA。</summary>
        [JsonProperty("color")]
        public string Color { get; set; }

        /// <summary>描边宽度；widthUnit=px 时会按 TMP Gradient Scale 换算。</summary>
        [JsonProperty("width")]
        public float Width { get; set; }

        /// <summary>描边宽度单位，例如 px；为空时按 TMP 归一化宽度处理。</summary>
        [JsonProperty("widthUnit")]
        public string WidthUnit { get; set; }
    }
}

