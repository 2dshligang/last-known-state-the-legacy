using System;
using UnityEngine;

namespace LKS.World
{
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class StationGenerationManifest : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public StableId id;
            public GameObject node;
        }

        [SerializeField] private StationLayout layout;
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public StationLayout Layout => layout;
        public System.Collections.Generic.IReadOnlyList<Entry> Entries => entries;
    }
}
