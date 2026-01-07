using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Editor.UIGenerator
{
    [System.Serializable]
    public class ArtboardData
    {
        [JsonProperty("uuid")]
        public string Uuid { get; set; }

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("origin")]
        public string Origin { get; set; }

        [JsonProperty("output")]
        public string Output { get; set; }

        [JsonProperty("detail")]
        public string Detail { get; set; }

        [JsonProperty("platform")]
        public string Platform { get; set; }

        [JsonProperty("baseDpi")]
        public int BaseDpi { get; set; }

        [JsonProperty("bounds")]
        public BoundsData Bounds { get; set; }

        [JsonProperty("resolution")]
        public int Resolution { get; set; }

        [JsonProperty("layers")]
        public List<LayerData> Layers { get; set; }

        [JsonProperty("assets")]
        public Dictionary<string, object> Assets { get; set; }
    }

    [System.Serializable]
    public class BoundsData
    {
        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }

        [JsonProperty("width")]
        public float Width { get; set; }

        [JsonProperty("height")]
        public float Height { get; set; }
    }
}

