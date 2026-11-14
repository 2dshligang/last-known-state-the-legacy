using UnityEngine;

namespace LKS.World
{
    [DisallowMultipleComponent]
    public sealed class AirlockPresentation : MonoBehaviour
    {
        [SerializeField] private Transform leaf;
        [SerializeField] private Transform closedPose;
        [SerializeField] private Transform openPose;
        [SerializeField] private StationLabel statusLabel;

        // An external caller supplies the authoritative door state.
        public void ApplyOpen(bool open)
        {
            leaf.localPosition = open ? openPose.localPosition : closedPose.localPosition;
            statusLabel.SetText(open ? "AIRLOCK  /  OPEN" : "AIRLOCK  /  CLOSED");
        }
    }
}
