using System;
using UnityEngine;

namespace MelenitasDev.SoundsGood
{
    /// <summary>
    /// Serializable pseudo-enum for output tags.
    /// Concrete values live in Output_Generated.cs (Assets/SoundsGood/Data/Generated).
    /// </summary>
    [Serializable]
    public partial struct Output : IEquatable<Output>
    {
        [SerializeField] private string value;

        internal Output (string value) => this.value = value;

        public override string ToString()        => value;
        public bool           Equals(Output other)  => value == other.value;
        public override int   GetHashCode()      => value?.GetHashCode() ?? 0;

        public static implicit operator string (Output s) => s.value;
    }
}
