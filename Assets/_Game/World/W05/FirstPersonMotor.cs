using UnityEngine;

namespace LKS.World
{
    [RequireComponent(typeof(CharacterController))]
    internal sealed class FirstPersonMotor : MonoBehaviour
    {
        [SerializeField] private Transform eye;
        private CharacterController body;
        private float pitch;
        private float verticalSpeed;

        internal void ResetMotion()
        {
            body = GetComponent<CharacterController>();
            verticalSpeed = 0;
            pitch = 0;
            eye.localRotation = Quaternion.identity;
        }

        internal void Look(Vector2 delta, float sensitivity, bool invert)
        {
            transform.Rotate(0, delta.x * sensitivity, 0, Space.Self);
            pitch = Mathf.Clamp(pitch + delta.y * sensitivity * (invert ? 1 : -1), -85, 85);
            eye.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        internal void Move(Vector2 input, float elapsed)
        {
            var local = Vector2.ClampMagnitude(input, 1);
            Vector3 walking = (transform.right * local.x + transform.forward * local.y) * 3.4f;
            // Bound both the distance per collision step and the catch-up after a stall.
            float remaining = Mathf.Min(elapsed, .25f);
            while (remaining > .0001f)
            {
                float step = Mathf.Min(remaining, 1f / 60);
                verticalSpeed = body.isGrounded ? -1.5f : Mathf.Max(-18, verticalSpeed - 16 * step);
                var flags = body.Move((walking + Vector3.up * verticalSpeed) * step);
                if ((flags & CollisionFlags.Below) != 0) verticalSpeed = -1.5f;
                remaining -= step;
            }
        }
    }
}
