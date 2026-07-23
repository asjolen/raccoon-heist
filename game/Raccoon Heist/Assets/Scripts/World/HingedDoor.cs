using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RaccoonHeist.World
{
    // Lightweight prototype interaction for the shop entrance. It opens from either
    // side, while a separate street-side event lets the alarm identify a break-in.
    // This can become host-authoritative with FishNet.
    [DisallowMultipleComponent]
    public sealed class HingedDoor : MonoBehaviour
    {
        [Header("Interaction")]
        [SerializeField] float interactionDistance = 1.65f;
        [SerializeField] Key interactionKey = Key.E;
        [SerializeField] bool lockedFromStreet = false;
        [SerializeField] bool unlockOnInteriorUse = true;

        [Header("Hinge")]
        [SerializeField] float openAngle = 105f;
        [SerializeField] float degreesPerSecond = 220f;

        Quaternion closedRotation;
        bool isOpen;

        public bool IsOpen => isOpen;
        public bool IsLockedFromStreet => lockedFromStreet;
        public event Action Opened;
        public event Action OpenedFromStreet; // break-in signal; interior openings stay silent

        void Awake()
        {
            closedRotation = transform.localRotation;
        }

        void Update()
        {
            var target = closedRotation * Quaternion.Euler(0f, isOpen ? openAngle : 0f, 0f);
            transform.localRotation = Quaternion.RotateTowards(
                transform.localRotation, target, degreesPerSecond * Time.deltaTime);

            var keyboard = Keyboard.current;
            var viewer = Camera.main != null ? Camera.main.transform : null;
            if (keyboard == null || viewer == null || !keyboard[interactionKey].wasPressedThisFrame) return;
            if ((viewer.position - transform.position).sqrMagnitude > interactionDistance * interactionDistance) return;

            TryToggle(viewer.position);
        }

        public bool TryToggle(Vector3 viewerPosition)
        {
            bool viewerIsOnStreet = viewerPosition.z < transform.position.z - 0.05f;
            if (viewerIsOnStreet && lockedFromStreet) return false;
            if (!viewerIsOnStreet && unlockOnInteriorUse) lockedFromStreet = false;
            isOpen = !isOpen;
            if (isOpen)
            {
                Opened?.Invoke();
                if (viewerIsOnStreet) OpenedFromStreet?.Invoke();
            }
            return true;
        }

        public void UnlockFromInside() => lockedFromStreet = false;
    }
}
