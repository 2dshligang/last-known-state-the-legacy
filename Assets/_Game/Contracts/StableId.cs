using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LKS
{
    [Serializable]
    public struct StableId : IEquatable<StableId>
    {
        private static readonly Regex Format = new Regex(@"\A[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*\z");

        // Public for Unity field serialization. Validate deserialized IDs before binding.
        public string value;

        public StableId(string value)
        {
            if (value == null || !Format.IsMatch(value))
                throw new ArgumentException("Invalid stable ID.", nameof(value));
            this.value = value;
        }

        public bool IsValid => value != null && Format.IsMatch(value);
        public bool Equals(StableId other) => StringComparer.Ordinal.Equals(value, other.value);
        public override bool Equals(object obj) => obj is StableId other && Equals(other);
        public override int GetHashCode() => value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        public override string ToString() => value ?? string.Empty;
        public static bool operator ==(StableId left, StableId right) => left.Equals(right);
        public static bool operator !=(StableId left, StableId right) => !left.Equals(right);

        public static void ValidateUnique(IEnumerable<StableId> ids)
        {
            var seen = new HashSet<StableId>();
            foreach (var id in ids)
            {
                if (!id.IsValid)
                    throw new ArgumentException($"Invalid stable ID '{id}'.", nameof(ids));
                if (!seen.Add(id))
                    throw new ArgumentException($"Duplicate stable ID '{id}'.", nameof(ids));
            }
        }
    }
}
