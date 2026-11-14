using UnityEngine;

namespace LKS.World
{
    [DisallowMultipleComponent]
    public sealed class StationExteriorGate : MonoBehaviour
    {
        public static readonly StableId Id = new StableId("door.exterior-airlock");
        [SerializeField] private AirlockPresentation door;
        private bool? open;

        public bool IsOpen => open == true;

        internal bool IsFacing(Transform view)
        {
            Vector3 center = transform.TransformPoint(new Vector3(-.35f, 1.6f, -.4f));
            Vector3 offset = center - view.position;
            return offset.sqrMagnitude <= StationInteraction.Range * StationInteraction.Range &&
                Vector3.Dot(view.forward, offset.normalized) > .91f &&
                !Physics.Linecast(view.position, center, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
        }

        public void Present(bool essentialPower, Vector3 visitorPosition)
        {
            // This mechanical return remains usable on the exterior side without power.
            float outwardDistance = transform.InverseTransformPoint(visitorPosition).z;
            bool keepClear = open == true && Mathf.Abs(outwardDistance) < .75f;
            bool next = essentialPower || outwardDistance >= 0 || keepClear;
            if (open == next) return;
            open = next;
            door.ApplyOpen(next);
        }
    }
}
