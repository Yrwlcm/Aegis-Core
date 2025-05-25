using UnityEngine;
using UnityEngine.InputSystem;

namespace AegisCore2D.GeneralScripts
{
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRTS : MonoBehaviour
    {
        [Header("Edge-pan")]
        [SerializeField] private float panSpeed = 10f;
        [SerializeField] private int edgeSize = 12; // px

        [Header("Middle-drag")]
        [SerializeField] private float dragFactor = 1f;

        [Header("Zoom")]
        [SerializeField] private float zoomScale = 0.15f;
        [SerializeField] private float zoomMin = 3f;
        [SerializeField] private float zoomMax = 15f;

        private Camera cam;
        private Vector3 dragOrigin;
        private bool dragging;

        private void Awake() => cam = GetComponent<Camera>();

        private void Update()
        {
            if (Time.timeScale == 0f)
            {
                // Если тащили камеру средней кнопкой, сбрасываем
                if (dragging) dragging = false; 
                return;
            }
            
            HandleMiddleDrag();
            HandleEdgePan();
            HandleZoom();
        }

        /* -------- Middle-mouse drag -------- */
        private void HandleMiddleDrag()
        {
            if (Mouse.current.middleButton.wasPressedThisFrame)
            {
                dragging = true;
                dragOrigin = ScreenToGround(Mouse.current.position.ReadValue());
            }
            else if (Mouse.current.middleButton.wasReleasedThisFrame)
            {
                dragging = false;
            }

            if (!dragging) return;

            var now = ScreenToGround(Mouse.current.position.ReadValue());
            var diff = dragOrigin - now;
            transform.position += diff * dragFactor;
        }

        /* -------- Edge pan (disabled while drag) -------- */
        private void HandleEdgePan()
        {
            if (dragging) return;

            var m = Mouse.current.position.ReadValue();
            if (m.x < 0 || m.x > Screen.width ||
                m.y < 0 || m.y > Screen.height) return;

            var move = Vector3.zero;

            if (m.x < edgeSize) move.x -= 1;
            else if (m.x > Screen.width - edgeSize) move.x += 1;

            if (m.y < edgeSize) move.y -= 1;
            else if (m.y > Screen.height - edgeSize) move.y += 1;

            if (move != Vector3.zero)
            {
                transform.Translate(move.normalized * panSpeed * Time.deltaTime, Space.World);
            }
        }

        /* -------- Scroll zoom (responsive) -------- */
        private void HandleZoom()
        {
            var scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Approximately(scroll, 0f)) return;

            var factor = 1 - Mathf.Sign(scroll) * zoomScale;
            var target = Mathf.Clamp(cam.orthographicSize * factor, zoomMin, zoomMax);
            cam.orthographicSize = target;
        }

        /* -------- Helpers -------- */
        private Vector3 ScreenToGround(Vector2 screen)
        {
            var v = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, cam.nearClipPlane));
            v.z = 0f;
            return v;
        }
    }
}