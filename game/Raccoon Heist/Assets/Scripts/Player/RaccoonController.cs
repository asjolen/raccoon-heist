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
        [SerializeField] float crouchTransitionSpeed = 2.5f;

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
            else if (crouching && !Physics.Raycast(transform.position, Vector3.up, standHeight + 0.05f))
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

        // CharacterController doesn't push rigidbodies on its own — shove loot we bump into
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var rb = hit.rigidbody;
            if (rb == null || rb.isKinematic || hit.moveDirection.y < -0.3f) return;
            rb.AddForceAtPosition(hit.moveDirection * pushPower, hit.point, ForceMode.Impulse);
        }
    }
}
