using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Orbit")]
    public float orbitSpeed = 150f;  // horizontal orbit around pyramid center
    public float tiltSpeed = 80f;   // vertical tilt (elevation angle)
    public float minTiltAngle = 5f;    // don't go below horizon
    public float maxTiltAngle = 85f;   // don't go straight down

    [Header("Zoom")]
    public float zoomSpeed = 5f;
    public float minZoom = 5f;    // closest distance to pivot
    public float maxZoom = 60f;   // farthest distance from pivot

    [Header("Pan")]
    public float panSpeed = 0.02f; // pan sensitivity

    // ── Pivot point ───────────────────────────────────────────
    // Camera always orbits and pans around this world point
    // Starts at pyramid center, shifts when panning
    private Vector3 pivotPoint = Vector3.zero;

    // ── Current orbit state ───────────────────────────────────
    private float currentOrbitAngle; // horizontal angle around Y axis
    private float currentTiltAngle;  // vertical elevation angle
    private float currentDistance;   // distance from pivot

    // ── Input tracking ────────────────────────────────────────
    private Vector2 lastMousePosition;

    void Start()
    {
        // Initialise orbit state from current camera transform
        // so whatever position you set in the editor is the starting state
        Vector3 offset = transform.position - pivotPoint;
        currentDistance = offset.magnitude;
        currentTiltAngle = Mathf.Asin(offset.normalized.y) * Mathf.Rad2Deg;
        currentOrbitAngle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;

        currentTiltAngle = Mathf.Clamp(currentTiltAngle, minTiltAngle, maxTiltAngle);
        currentDistance = Mathf.Clamp(currentDistance, minZoom, maxZoom);
    }

    void Update()
    {
        HandleOrbitAndTilt();
        HandlePan();
        HandleZoom();
        ApplyCameraTransform();
    }

    // ── Orbit (left mouse drag) ───────────────────────────────
    // Left drag: horizontal = orbit around Y axis
    //            vertical   = tilt up/down
    void HandleOrbitAndTilt()
    {
        if (!Mouse.current.leftButton.isPressed) return;

        Vector2 delta = Mouse.current.delta.ReadValue();

        currentOrbitAngle += delta.x * orbitSpeed * Time.deltaTime;
        currentTiltAngle -= delta.y * tiltSpeed * Time.deltaTime;
        currentTiltAngle = Mathf.Clamp(currentTiltAngle, minTiltAngle, maxTiltAngle);
    }

    // ── Pan (right mouse drag / middle mouse drag) ────────────
    // Shifts the pivot point in camera-local XY plane
    // Camera follows so the board appears to slide
    void HandlePan()
    {
        bool rightDrag = Mouse.current.rightButton.isPressed;
        bool middleDrag = Mouse.current.middleButton.isPressed;

        if (!rightDrag && !middleDrag) return;

        Vector2 delta = Mouse.current.delta.ReadValue();

        // Pan speed scales with distance so panning feels consistent at any zoom
        float scaledPanSpeed = panSpeed * currentDistance;

        // Move pivot in camera-local right and up directions
        Vector3 right = transform.right;
        Vector3 up = transform.up;

        // Constrain pan to XZ plane (no vertical pan) for a board game feel
        right.y = 0f; right.Normalize();
        up.y = 0f; up.Normalize();

        pivotPoint -= right * delta.x * scaledPanSpeed;
        pivotPoint -= up * delta.y * scaledPanSpeed;
    }

    // ── Zoom (scroll wheel) ───────────────────────────────────
    void HandleZoom()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;
        currentDistance -= scroll * zoomSpeed * Time.deltaTime * 10f;
        currentDistance = Mathf.Clamp(currentDistance, minZoom, maxZoom);
    }

    // ── Apply final camera position and rotation ──────────────
    // Recalculates camera position from spherical coordinates around pivot
    void ApplyCameraTransform()
    {
        float tiltRad = currentTiltAngle * Mathf.Deg2Rad;
        float orbitRad = currentOrbitAngle * Mathf.Deg2Rad;

        // Convert spherical to Cartesian offset from pivot
        Vector3 offset = new Vector3(
            currentDistance * Mathf.Cos(tiltRad) * Mathf.Sin(orbitRad),
            currentDistance * Mathf.Sin(tiltRad),
            currentDistance * Mathf.Cos(tiltRad) * Mathf.Cos(orbitRad)
        );

        transform.position = pivotPoint + offset;
        transform.LookAt(pivotPoint);
    }
}