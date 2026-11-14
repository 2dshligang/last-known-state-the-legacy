using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Cursor = UnityEngine.Cursor;

namespace LKS.World
{
    [RequireComponent(typeof(FirstPersonMotor))]
    public sealed class StationFirstPerson : MonoBehaviour
    {
        private const string SensitivityKey = "lks.look.sensitivity";
        private const string InvertKey = "lks.look.invert-y";
        private readonly List<ReadingFocus> readers = new();
        private InputAction move, look, use, escape;
        private FirstPersonMotor motor;
        private GameplaySession pausedSession;
        private int queuedUseFrame = -1;
        private bool escapeRequested;
        private bool focused;
        private bool paused;
        private bool captureRequested;
        private bool hadCapture;
        private float? previousTimeScale;
        private int suppressUntil;
        private float sensitivity;
        private bool invert;

        // Presentation-only events. W06 resolves the target and sends the authoritative request.
        public event Action InteractPressed;
        public event Action RestartRequested;

        internal void RequestRestart()
        {
            if (RestartRequested == null) RestartAtSpawn();
            else RestartRequested.Invoke();
        }
        public bool IsPaused => paused;
        public bool HasReadingFocus => readers.Count != 0;
        public bool HasApplicationFocus => focused;
        public bool HasWorldFocus => focused && captureRequested && !paused && !HasReadingFocus;
        public bool CanInteract => HasWorldFocus && Cursor.lockState == CursorLockMode.Locked &&
            Time.frameCount > suppressUntil;

        public float Sensitivity
        {
            get => sensitivity;
            set
            {
                float next = Mathf.Clamp(float.IsFinite(value) ? value : .12f, .035f, .4f);
                if (sensitivity == next) return;
                sensitivity = next;
                PlayerPrefs.SetFloat(SensitivityKey, sensitivity);
            }
        }
        public bool InvertY
        {
            get => invert;
            set
            {
                if (invert == value) return;
                invert = value;
                PlayerPrefs.SetInt(InvertKey, value ? 1 : 0);
            }
        }

        [SerializeField] private Transform spawn;
        [SerializeField] private SessionBootstrap sessionBootstrap;

        private void Awake()
        {
            motor = GetComponent<FirstPersonMotor>();
            float savedSensitivity = PlayerPrefs.GetFloat(SensitivityKey, .12f);
            sensitivity = float.IsFinite(savedSensitivity) ? Mathf.Clamp(savedSensitivity, .035f, .4f) : .12f;
            invert = PlayerPrefs.GetInt(InvertKey, 0) != 0;
            move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            look = new InputAction("Look", InputActionType.PassThrough, "<Mouse>/delta");
            use = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            escape = new InputAction("Escape", InputActionType.Button, "<Keyboard>/escape");
            use.performed += OnUse;
            escape.performed += OnEscape;
        }

        private void OnEnable()
        {
            focused = Application.isFocused;
            captureRequested = true;
            RestartAtSpawn();
            move.Enable();
            look.Enable();
            use.Enable();
            escape.Enable();
        }

        private void Update()
        {
            if (escapeRequested)
            {
                escapeRequested = false;
                SetPaused(!paused);
            }
            UpdateCapture();
            if (!CanInteract) { queuedUseFrame = -1; return; }
            motor.Look(look.ReadValue<Vector2>(), sensitivity, invert);
            motor.Move(move.ReadValue<Vector2>(), Time.unscaledDeltaTime);
            // Dispatch once, after Escape and focus are processed for this frame.
            bool dispatch = queuedUseFrame >= 0 && queuedUseFrame >= Time.frameCount - 1;
            queuedUseFrame = -1;
            if (dispatch) InteractPressed?.Invoke();
        }

        private void LateUpdate() => ApplySessionPause();

        private void OnApplicationFocus(bool active)
        {
            focused = active;
            if (!active) captureRequested = false;
            UpdateCapture();
        }

        public void ResumePointer()
        {
            if (!focused || paused || HasReadingFocus) return;
            captureRequested = true;
            UpdateCapture();
        }

        public void SetPaused(bool value)
        {
            if (paused == value) return;
            paused = value;
            if (value)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0;
            }
            else RestoreTimeScale();
            if (value) captureRequested = false;
            else if (!HasReadingFocus) captureRequested = true;
            UpdateCapture();
            ApplySessionPause();
        }

        public IDisposable AcquireReadingFocus(Action onRevoked = null)
        {
            if (!isActiveAndEnabled) throw new InvalidOperationException("Player input is disabled.");
            var lease = new ReadingFocus(this, onRevoked);
            readers.Add(lease);
            UpdateCapture();
            ApplySessionPause();
            return lease;
        }

        public void RestartAtSpawn()
        {
            captureRequested = focused;
            ResetFocus();
            if (spawn != null)
            {
                var controller = GetComponent<CharacterController>();
                controller.enabled = false;
                transform.SetPositionAndRotation(spawn.position, spawn.rotation);
                controller.enabled = true;
            }
            motor.ResetMotion();
            UpdateCapture();
        }

        private void ResetFocus()
        {
            paused = false;
            RestoreTimeScale();
            foreach (var reader in readers.ToArray()) reader.Revoke();
            readers.Clear();
            escapeRequested = false;
            queuedUseFrame = -1;
            suppressUntil = Time.frameCount + 1;
            ApplySessionPause();
        }

        private void RestoreTimeScale()
        {
            if (!previousTimeScale.HasValue) return;
            Time.timeScale = previousTimeScale.Value;
            previousTimeScale = null;
        }

        private void Release(ReadingFocus lease)
        {
            if (!readers.Remove(lease)) return;
            UpdateCapture();
            ApplySessionPause();
        }

        private void ApplySessionPause()
        {
            var current = sessionBootstrap != null ? sessionBootstrap.Current : null;
            if (!ReferenceEquals(pausedSession, current)) pausedSession = null;
            if (current == null) return;
            bool shouldPause = isActiveAndEnabled && (paused || HasReadingFocus || !HasWorldFocus);
            if (shouldPause == ReferenceEquals(pausedSession, current)) return;
            current.SetPowerReadingPaused(shouldPause);
            pausedSession = shouldPause ? current : null;
        }

        private void UpdateCapture()
        {
            bool capture = isActiveAndEnabled && HasWorldFocus;
            if (capture != hadCapture)
            {
                hadCapture = capture;
                queuedUseFrame = -1;
                suppressUntil = Time.frameCount + 1;
                Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !capture;
            }
            // Unity or the OS can release the cursor independently of the requested state.
            else if (capture && Cursor.lockState != CursorLockMode.Locked)
            {
                captureRequested = false;
                UpdateCapture();
            }
        }

        private void OnUse(InputAction.CallbackContext context) => QueueInteraction();

        internal void QueueInteraction()
        {
            if (CanInteract) queuedUseFrame = Time.frameCount;
        }

        private void OnEscape(InputAction.CallbackContext context) => QueueEscape();

        internal void QueueEscape()
        {
            if (focused) escapeRequested = true;
        }

        private void OnDisable()
        {
            captureRequested = false;
            ResetFocus();
            UpdateCapture();
            move?.Disable();
            look?.Disable();
            use?.Disable();
            escape?.Disable();
            PlayerPrefs.Save();
        }

        private void OnDestroy()
        {
            move?.Dispose();
            look?.Dispose();
            use?.Dispose();
            escape?.Dispose();
        }

        private sealed class ReadingFocus : IDisposable
        {
            private StationFirstPerson owner;
            private Action revoked;

            internal ReadingFocus(StationFirstPerson owner, Action revoked)
            {
                this.owner = owner;
                this.revoked = revoked;
            }

            public void Dispose()
            {
                var previous = owner;
                owner = null;
                revoked = null;
                if (previous != null) previous.Release(this);
            }

            internal void Revoke()
            {
                var callback = revoked;
                owner = null;
                revoked = null;
                try { callback?.Invoke(); }
                catch (Exception error) { Debug.LogException(error); }
            }
        }
    }
}
