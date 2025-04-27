using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public sealed class CameraRTS : MonoBehaviour
{
    [Header("Edge-pan")]
    [SerializeField] float panSpeed      = 10f;  // faster
    [SerializeField] int   edgeSize      = 12;   // px

    [Header("Middle-drag")]
    [SerializeField] float dragFactor    = 1f;   // 1 == 1-в-1 движение

    [Header("Zoom")]
    [SerializeField] float zoomScale     = 0.15f;  // 15 % за тик
    [SerializeField] float zoomMin       = 3f;
    [SerializeField] float zoomMax       = 15f;

    Camera cam;
    Vector3 dragOrigin;
    bool    dragging;

    void Awake() => cam = GetComponent<Camera>();

    void Update()
    {
        HandleMiddleDrag();
        HandleEdgePan();
        HandleZoom();
    }

    /* -------- Middle-mouse drag -------- */
    void HandleMiddleDrag()
    {
        if (Mouse.current.middleButton.wasPressedThisFrame)
        {
            dragging   = true;
            dragOrigin = ScreenToGround(Mouse.current.position.ReadValue());
        }
        else if (Mouse.current.middleButton.wasReleasedThisFrame)
            dragging = false;

        if (!dragging) return;

        Vector3 now = ScreenToGround(Mouse.current.position.ReadValue());
        Vector3 diff = dragOrigin - now;
        transform.position += diff * dragFactor;
    }

    /* -------- Edge pan (disabled while drag) -------- */
    void HandleEdgePan()
    {
        if (dragging) return;                                // 3)

        Vector2 m = Mouse.current.position.ReadValue();
        if (m.x < 0 || m.x > Screen.width ||
            m.y < 0 || m.y > Screen.height) return;         // 2)

        Vector3 move = Vector3.zero;

        if (m.x < edgeSize)                    move.x -= 1;
        else if (m.x > Screen.width  - edgeSize) move.x += 1;

        if (m.y < edgeSize)                    move.y -= 1;
        else if (m.y > Screen.height - edgeSize) move.y += 1;

        if (move != Vector3.zero)
            transform.Translate(move.normalized * panSpeed * Time.deltaTime,
                                Space.World);
    }

    /* -------- Scroll zoom (responsive) -------- */
    void HandleZoom()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Approximately(scroll, 0f)) return;

        float factor = 1 - Mathf.Sign(scroll) * zoomScale;  // 1 ± 15 %
        float target = Mathf.Clamp(cam.orthographicSize * factor, zoomMin, zoomMax);
        cam.orthographicSize = target;                      // моментально 1)
    }

    /* -------- Helpers -------- */
    Vector3 ScreenToGround(Vector2 screen)
    {
        Vector3 v = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, cam.nearClipPlane));
        v.z = 0f;
        return v;
    }
}
