using UnityEngine;

namespace LKS.World
{
    [DisallowMultipleComponent]
    public sealed class StationInteractionAnchor : MonoBehaviour
    {
        [SerializeField] private StableId targetId;
        [SerializeField] private Transform approach;

        public StableId TargetId => targetId;
        public Transform Approach => approach;
    }
}
