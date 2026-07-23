using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RaccoonHeist.Player
{
    // First-person raccoon movement. Local-only for now — gets ported onto a
    // FishNet NetworkBehaviour before any gameplay features are built on top.
    [RequireComponent(typeof(CharacterController))]
    public class RaccoonController : MonoBehaviour
    {
        [Header("Movement (m/s)")]
        [SerializeField] float sneakSpeed = 1.2f;
        [SerializeField] float scurrySpeed = 3.5f;
        [SerializeField] float crouchSpeed = 0.7f;
        [SerializeField] float jumpHeight = 1.0f;
        [SerializeField] float gravity = 20f;

        [Header("Crouch")]
        [SerializeField] float standHeight = 0.5f;
        [SerializeField] float crouchHeight = 0.3f;
        [SerializeField] float standRadius = 0.2f;
        [SerializeField] float crouchRadius = 0.14f;
        [SerializeField] float crouchTransitionSpeed = 2.5f;

        [Header("Breakable entries")]
        [SerializeField] Key breakInteractionKey = Key.E;
        [SerializeField] LayerMask breakInteractionMask = ~0;

        [Header("Breakable vents")]
        [SerializeField] float ventInteractionDistance = 1.35f;
        [SerializeField] float ventBreakImpulse = 1.8f;
        [SerializeField] float ventBreakTorque = 1.2f;
        [SerializeField] float ventBreakNoiseRadius = 9f;
        [SerializeField] float ventDebrisLifetime = 12f;

        [Header("Breakable glass")]
        [SerializeField] float glassInteractionDistance = 1.65f;
        [SerializeField] float glassBreakImpulse = 1.35f;
        [SerializeField] float glassBreakTorque = 1f;
        [SerializeField] float glassBreakNoiseRadius = 12f;
        [SerializeField] float glassDebrisLifetime = 10f;

        [Header("Look")]
        [SerializeField] float lookSensitivity = 0.12f;
        [SerializeField] float maxPitch = 85f;

        [Header("Physics")]
        [SerializeField] float pushPower = 0.4f;

        CharacterController controller;
        Transform eyes;
        Transform body;
        float pitch;
        float verticalVelocity;
        bool crouching;
        readonly Collider[] standClearanceHits = new Collider[16];

        // The future host-authoritative Harold hearing system can subscribe to this
        // without coupling the temporary local controller to an AI implementation.
        public static event Action<Vector3, float> LoudNoiseEmitted;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            eyes = GetComponentInChildren<Camera>().transform;
            body = transform.Find("Body");
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null) return;

            if (mouse != null)
            {
                Vector2 look = mouse.delta.ReadValue() * lookSensitivity;
                transform.Rotate(0f, look.x, 0f);
                pitch = Mathf.Clamp(pitch - look.y, -maxPitch, maxPitch);
                eyes.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            Vector2 input = Vector2.zero;
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            Vector3 move = (transform.right * input.x + transform.forward * input.y).normalized;

            // Crouch: hold Ctrl or C to squeeze into 0.3 m gaps. Standing back up is
            // blocked while something solid is overhead (e.g. under the cot).
            bool wantCrouch = keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed;
            if (wantCrouch)
                crouching = true;
            else if (crouching && CanStand())
                crouching = false;
            float targetHeight = crouching ? crouchHeight : standHeight;
            if (!Mathf.Approximately(controller.height, targetHeight))
            {
                controller.height = Mathf.MoveTowards(controller.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);
                controller.center = Vector3.up * (controller.height / 2f);
                eyes.localPosition = new Vector3(0f, controller.height * 0.6f, 0.08f);
                if (body != null)
                {
                    body.localScale = new Vector3(0.35f, controller.height / 2f, 0.35f);
                    body.localPosition = new Vector3(0f, controller.height / 2f, 0f);
                }
            }
            float targetRadius = crouching ? crouchRadius : standRadius;
            controller.radius = Mathf.MoveTowards(controller.radius, targetRadius, crouchTransitionSpeed * Time.deltaTime);

            if (keyboard[breakInteractionKey].wasPressedThisFrame)
                TryBreakEntry();

            // Sneak by default; hold Shift to scurry (scurrying will be LOUD later)
            float speed = crouching ? crouchSpeed : (keyboard.leftShiftKey.isPressed ? scurrySpeed : sneakSpeed);

            if (controller.isGrounded)
            {
                verticalVelocity = -1f;
                if (!crouching && keyboard.spaceKey.wasPressedThisFrame)
                    verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
            }
            else
            {
                verticalVelocity -= gravity * Time.deltaTime;
            }

            controller.Move((move * speed + Vector3.up * verticalVelocity) * Time.deltaTime);

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (Cursor.lockState != CursorLockMode.Locked && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void TryBreakEntry()
        {
            float interactionDistance = Mathf.Max(ventInteractionDistance, glassInteractionDistance);
            if (!Physics.Raycast(eyes.position, eyes.forward, out var hit, interactionDistance,
                    breakInteractionMask, QueryTriggerInteraction.Ignore))
                return;

            Transform breakable = hit.transform;
            while (breakable != null
                   && !breakable.name.StartsWith("BreakableVentCover_", StringComparison.Ordinal)
                   && !breakable.name.StartsWith("BreakableWindow_", StringComparison.Ordinal))
                breakable = breakable.parent;
            if (breakable == null) return;

            if (breakable.name.StartsWith("BreakableVentCover_", StringComparison.Ordinal))
            {
                if (hit.distance <= ventInteractionDistance) BreakVent(breakable);
                return;
            }

            if (hit.distance <= glassInteractionDistance) BreakWindow(breakable);
        }

        void BreakVent(Transform cover)
        {

            // Renaming first makes a double key press harmless even before the grate
            // has physically cleared the opening.
            cover.name = "Broken_" + cover.name;
            Vector3 noisePosition = cover.position;
            var source = cover.GetComponent<AudioSource>();
            if (source != null) source.Play();

            // Open the route immediately. The old single rigid grate could wedge
            // invisibly into the duct; authored slats now burst toward the player so
            // the break is unmistakable and cannot leave a blocking collider behind.
            foreach (var blockingCollider in cover.GetComponents<Collider>())
                blockingCollider.enabled = false;
            var intactVisual = cover.Find("POLYGON_BreakawayGrate");
            if (intactVisual != null) intactVisual.gameObject.SetActive(false);

            Vector3 towardPlayer = transform.position - cover.position;
            towardPlayer.y = 0f;
            if (towardPlayer.sqrMagnitude < 0.01f) towardPlayer = -eyes.forward;
            towardPlayer.Normalize();

            var fragments = cover.Find("VentBreakFragments");
            int fragmentCount = fragments != null ? fragments.childCount : 0;
            for (int i = fragmentCount - 1; i >= 0; i--)
            {
                var fragment = fragments.GetChild(i);
                fragment.SetParent(null, true);
                fragment.gameObject.SetActive(true);
                var fragmentBody = fragment.gameObject.AddComponent<Rigidbody>();
                fragmentBody.mass = 0.08f;
                fragmentBody.linearDamping = 0.06f;
                fragmentBody.angularDamping = 0.08f;
                fragmentBody.collisionDetectionMode = CollisionDetectionMode.Continuous;
                float sideways = (i - (fragmentCount - 1) * 0.5f) * 0.075f;
                Vector3 burst = (towardPlayer + transform.right * sideways + Vector3.up * (0.18f + i * 0.025f)).normalized;
                fragmentBody.AddForce(burst * ventBreakImpulse, ForceMode.Impulse);
                fragmentBody.AddTorque((transform.right * (i % 2 == 0 ? 1f : -1f) + Vector3.up * 0.35f).normalized
                    * ventBreakTorque, ForceMode.Impulse);
                Destroy(fragment.gameObject, ventDebrisLifetime);
            }

            LoudNoiseEmitted?.Invoke(noisePosition, ventBreakNoiseRadius);
            float audioLifetime = source != null && source.clip != null ? source.clip.length + 0.2f : 1f;
            Destroy(cover.gameObject, audioLifetime);
        }

        void BreakWindow(Transform pane)
        {
            pane.name = "Broken_" + pane.name;
            Vector3 noisePosition = pane.position;
            var source = pane.GetComponent<AudioSource>();
            if (source != null) source.Play();

            foreach (var blockingCollider in pane.GetComponents<Collider>())
                blockingCollider.enabled = false;
            var intactVisual = pane.Find("IntactGlass");
            if (intactVisual != null) intactVisual.gameObject.SetActive(false);

            Vector3 towardPlayer = transform.position - pane.position;
            towardPlayer.y = 0f;
            if (towardPlayer.sqrMagnitude < 0.01f) towardPlayer = -eyes.forward;
            towardPlayer.Normalize();

            var fragments = pane.Find("GlassBreakFragments");
            int fragmentCount = fragments != null ? fragments.childCount : 0;
            for (int i = fragmentCount - 1; i >= 0; i--)
            {
                var fragment = fragments.GetChild(i);
                fragment.SetParent(null, true);
                fragment.gameObject.SetActive(true);
                var fragmentBody = fragment.gameObject.AddComponent<Rigidbody>();
                fragmentBody.mass = 0.025f;
                fragmentBody.linearDamping = 0.045f;
                fragmentBody.angularDamping = 0.04f;
                fragmentBody.collisionDetectionMode = CollisionDetectionMode.Continuous;

                float signedOffset = i - (fragmentCount - 1) * 0.5f;
                float sideways = Mathf.Sin(i * 2.17f) * 0.42f + signedOffset * 0.012f;
                float lift = 0.16f + Mathf.Repeat(i * 0.137f, 0.34f);
                Vector3 burst = (towardPlayer + transform.right * sideways + Vector3.up * lift).normalized;
                float forceVariation = 0.78f + Mathf.Repeat(i * 0.193f, 0.38f);
                fragmentBody.AddForce(burst * glassBreakImpulse * forceVariation, ForceMode.Impulse);
                fragmentBody.AddTorque(
                    (transform.right * (i % 2 == 0 ? 1f : -1f)
                     + transform.forward * Mathf.Sin(i * 1.31f)
                     + Vector3.up * 0.45f).normalized * glassBreakTorque,
                    ForceMode.Impulse);
                Destroy(fragment.gameObject, glassDebrisLifetime);
            }

            LoudNoiseEmitted?.Invoke(noisePosition, glassBreakNoiseRadius);
            float audioLifetime = source != null && source.clip != null ? source.clip.length + 0.2f : 1f;
            Destroy(pane.gameObject, audioLifetime);
        }

        bool CanStand()
        {
            // Test the complete standing capsule, not a single vertical ray. This
            // avoids false crouch locks at duct lips while still preventing the
            // raccoon from expanding through a genuinely low ceiling.
            float testRadius = Mathf.Max(0.01f, standRadius - 0.01f);
            Vector3 lowerSphere = transform.position + Vector3.up * standRadius;
            Vector3 upperSphere = transform.position + Vector3.up * (standHeight - standRadius);
            int hitCount = Physics.OverlapCapsuleNonAlloc(lowerSphere, upperSphere, testRadius,
                standClearanceHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                var hit = standClearanceHits[i];
                if (hit == null || hit == controller || hit.transform.IsChildOf(transform)) continue;
                return false;
            }
            return true;
        }

        // CharacterController doesn't push rigidbodies on its own — shove loot we bump into
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var rb = hit.rigidbody;
            if (rb == null || rb.isKinematic || hit.moveDirection.y < -0.3f) return;
            rb.AddForceAtPosition(hit.moveDirection * pushPower, hit.point, ForceMode.Impulse);
        }
    }
}
