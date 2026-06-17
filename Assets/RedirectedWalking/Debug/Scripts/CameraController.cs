using UnityEngine;

namespace RDW
{

    /// <summary>
    /// Unity Scene View style runtime camera.
    /// 
    /// Controls:
    /// RMB Hold      = Look around
    /// WASD          = Move while RMB held
    /// Q / E         = Down / Up while RMB held
    /// Shift         = Faster move
    /// Mouse Wheel   = Move forward/backward zoom
    /// MMB Drag      = Pan camera
    /// 
    /// UI / OnGUI remains clickable because cursor is only locked while RMB is held.
    /// </summary>
    
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [Header("Key Bindings")]
        [Tooltip("The keyboard key to control forward movement")]
        public KeyCode forwardKey = KeyCode.W;
        [Tooltip("The keyboard key to control backward movement")]
        public KeyCode backwardKey = KeyCode.S;
        [Tooltip("The keyboard key to control leftward movement")]
        public KeyCode leftwardKey = KeyCode.A;
        [Tooltip("The keyboard key to control rightward movement")]
        public KeyCode rightwardKey = KeyCode.D;
        [Tooltip("The keyboard key to control upward movement")]
        public KeyCode upwardKey = KeyCode.Space;
        [Tooltip("The keyboard key to control downward movement")]
        public KeyCode downwardKey = KeyCode.LeftControl;
        [Tooltip("The keyboard key, when held, will multiply camera movement speed")]
        public KeyCode moveMultKey = KeyCode.LeftShift;

        [Header("Movement")]
        [Tooltip("How fast does the camera translate?")]
        public float moveSpeed = 8f;
        [Tooltip("Upon holding down the movement multiplier key, how fast should we multiply movement by?")]
        public float moveMultiplier = 3f;
        [Tooltip("When panning, how fast do we pan?")]
        public float panSpeed = 0.02f;
        [Tooltip("When zooming, how fast do we zoom?")]
        public float zoomSpeed = 5f;

        [Header("Look")]
        public float mouseSensitivity = 2f;
        public float maxPitch = 89f;
        private float yaw;
        private float pitch;

        // Hidden States
        // - Looking Mode
        private bool looking = false;
        // - Agent mode
        //private Transform mountedRoot = null;   // The Agent transform
        //private Transform mountedRef = null;    // The camera mount transform
        private Quaternion localLookRotation = Quaternion.identity;
        // - Restore state
        private Vector3 savedPosition;
        private Quaternion savedRotation;
        
        // Initialize some variables
        private void Start() {
            Vector3 euler = transform.eulerAngles;
            yaw = euler.y;
            pitch = euler.x;
        }

        // During each frame we:
        private void Update() {
            HandleLookMode();   // Check if we are in Look mode
            HandleZoom();       // Move the camera forward or backward
            HandlePan();        // Pan the camera via MMB
            
            // Based on `HandleLookMode()`, if we're looking, we rotate/move the camera via mouse and WASD
            if (looking) {
                HandleMouseLook();
                HandleMovement();
            }
        }

        // We check the RMB to see if we should be in Look mode or not.
        private void HandleLookMode() {
            // RMB pressed
            if (Input.GetMouseButtonDown(1)) {
                looking = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            // RMB released
            if (Input.GetMouseButtonUp(1)) {
                looking = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        // Handle the scrollwheel to move the camera closer or further (hence the zoom)
        private void HandleZoom() {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f) {
                transform.position += transform.forward * scroll * zoomSpeed * Time.deltaTime * 50f;
            }
        }

        // Handle panning via middle mouse (i.e. holding the scroll as a button)
        private void HandlePan() {
            // MMB drag
            if (Input.GetMouseButton(2)) {
                float mx = -Input.GetAxisRaw("Mouse X") * panSpeed;
                float my = -Input.GetAxisRaw("Mouse Y") * panSpeed;
                Vector3 move =
                    transform.right * mx +
                    transform.up * my;
                transform.position += move;
            }
        }

        // Rotate the camera based on the translation of the mouse
        private void HandleMouseLook() {
            // Get the translation of the mouse in 2D
            float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;
            // Calculate the rotational pitch and yaw caused by the mouse
            yaw += mouseX;
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
            // Rotate the mouse accordingly
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        // Handle the translational movement of the mouse
        private void HandleMovement() {
            // Set movement speed, multiply if necessary
            float speed = moveSpeed;
            if (Input.GetKey(moveMultKey)) {
                speed *= moveMultiplier;
            }

            // Identify movement
            Vector3 move = Vector3.zero;
            if (Input.GetKey(forwardKey)) move += transform.forward;
            if (Input.GetKey(backwardKey)) move -= transform.forward;
            if (Input.GetKey(leftwardKey)) move -= transform.right;
            if (Input.GetKey(rightwardKey)) move += transform.right;
            if (Input.GetKey(upwardKey)) move += Vector3.up;
            if (Input.GetKey(downwardKey)) move -= Vector3.up;
            // Normalize if needed
            if (move.sqrMagnitude > 1f)
                move.Normalize();

            // Move this according to time delta.
            transform.position += move * speed * Time.deltaTime;
        }

    }
}

