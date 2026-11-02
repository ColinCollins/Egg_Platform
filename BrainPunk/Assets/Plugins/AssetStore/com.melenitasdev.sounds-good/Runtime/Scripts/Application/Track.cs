using System;
using UnityEngine;

namespace MelenitasDev.SoundsGood
{
    /// <summary>
    /// Serializable pseudo-enum for music tags.
    /// Concrete values live in Track_Generated.cs (Assets/SoundsGood/Data/Generated).
    /// </summary>
    [Serializable]
    public partial struct Track : IEquatable<Track>
    {
        [SerializeField] private string value;

        internal Track (string value) => this.value = value;

        public override string ToString () => value;
        public bool Equals (Track other) => value == other.value;
        public override int GetHashCode () => value?.GetHashCode() ?? 0;

        public static implicit operator string (Track s) => s.value;
    }
}