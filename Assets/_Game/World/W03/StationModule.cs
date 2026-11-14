using UnityEngine;

namespace LKS.World
{
    [DisallowMultipleComponent]
    public sealed class StationModule : MonoBehaviour
    {
        [SerializeField] private StableId moduleId;
        [SerializeField] private Transform inspectPoint;
        [SerializeField] private Transform snapStart;
        [SerializeField] private Transform snapEnd;
        [SerializeField] private GameObject[] authoredVisuals;
        [SerializeField] private GameObject[] fallbackVisuals;

        public StableId ModuleId => moduleId;
        public Transform InspectPoint => inspectPoint;
        public Transform SnapStart => snapStart;
        public Transform SnapEnd => snapEnd;
        public bool HasFallback => fallbackVisuals != null && fallbackVisuals.Length > 0;

        public bool TrySetFallbackVisual(bool useFallback)
        {
            if (!HasFallback) return false;
            foreach (var visual in authoredVisuals) visual.SetActive(!useFallback);
            foreach (var visual in fallbackVisuals) visual.SetActive(useFallback);
            return true;
        }
    }
}
