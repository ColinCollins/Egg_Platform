using System;
using UnityEngine;

namespace MelenitasDev.SoundsGood
{
    /// <summary>
    /// Serializable pseudo-enum for sound tags.
    /// Concrete values live in SFX_Generated.cs (Assets/SoundsGood/Data/Generated).
    /// </summary>
    [Serializable]
    public partial struct SFX : IEquatable<SFX>
    {
        [SerializeField] private string value;

        internal SFX (string value) => this.value = value;

        public override string ToString () => value;
        public bool Equals (SFX other) => value == other.value;
        public override int GetHashCode () => value?.GetHashCode() ?? 0;

        public static implicit operator string (SFX s) => s.value;
    }
}