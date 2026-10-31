
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class CameraController : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference panAction;
    [SerializeField] private InputActionReference pointerPositionAction;
    [SerializeField] private InputActionReference zoomAction;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 10f;

    [Header("Zoom")]
    [SerializeField, Min(0.01f)] private float minZoom = 3f;
    [SerializeField, Min(0.01f)] private float maxZoom = 15f;
    [SerializeField, Min(0f)] private float zoomSensitivity = 2f;

    [Header("World Bounds")]
    [SerializeField] private bool useWorldBounds = true;
    [SerializeField] private Vector2 worldMin = new(-50f, -50f);
    [SerializeField] private Vector2 worldMax = new(50f, 50f);

    private Camera _camera;
    private bool _isPanning;
    private Vector2 _previousPointerPosition;

    private void Awake()
    {
        _camera = GetComponent<Camera>();

        if (!_camera.orthographic)
        {
            Debug.LogError(
                "CameraController requires an orthographic camera.",
                this);

            enabled = false;
            return;
        }

        if (minZoom > maxZoom)
        {
            Debug.LogWarning(
                "minZoom was greater than maxZoom. The values will be swapped.",
                this);

            (minZoom, maxZoom) = (maxZoom, minZoom);
        }

        _camera.orthographicSize = Mathf.Clamp(
            _camera.orthographicSize,
            minZoom,
            maxZoom);
    }

    private void OnEnable()
    {
        EnableAction(moveAction);
        EnableAction(panAction);
        EnableAction(pointerPositionAction);
        EnableAction(zoomAction);
    }

    private void OnDisable()
    {
        DisableAction(moveAction);
        DisableAction(panAction);
        DisableAction(pointerPositionAction);
        DisableAction(zoomAction);

        _isPanning = false;
    }

    private void Update()
    {
        HandleKeyboardMovement();
        HandleMousePan();
        HandleZoom();

        ClampCameraPosition();
    }

    private void HandleKeyboardMovement()
    {
        if (moveAction == null)
            return;

        Vector2 input = moveAction.action.ReadValue<Vector2>();

        if (input.sqrMagnitude > 1f)
            input.Normalize();

        Vector3 movement = new Vector3(input.x, input.y, 0f);

        transform.position += movement * (moveSpeed * Time.deltaTime);
    }

    private void HandleMousePan()
    {
        if (panAction == null || pointerPositionAction == null)
            return;

        bool panPressed = panAction.action.IsPressed();
        Vector2 pointerPosition =
            pointerPositionAction.action.ReadValue<Vector2>();

        if (panPressed && !_isPanning)
        {
            _isPanning = true;
            _previousPointerPosition = pointerPosition;
            return;
        }

        if (!panPressed)
        {
            _isPanning = false;
            return;
        }

        Vector2 pointerDelta = pointerPosition - _previousPointerPosition;
        _previousPointerPosition = pointerPosition;

        // Convert screen pixels to world units.
        float worldUnitsPerPixel =
            (2f * _camera.orthographicSize) / Screen.height;

        Vector3 movement = new Vector3(
            -pointerDelta.x * worldUnitsPerPixel,
            -pointerDelta.y * worldUnitsPerPixel,
            0f);

        transform.position += movement;
    }

    private void HandleZoom()
    {
        if (zoomAction == null)
            return;

        Vector2 scroll = zoomAction.action.ReadValue<Vector2>();

        if (Mathf.Approximately(scroll.y, 0f))
            return;

        float newZoom = _camera.orthographicSize
            - scroll.y * zoomSensitivity * Time.deltaTime;

        _camera.orthographicSize = Mathf.Clamp(
            newZoom,
            minZoom,
            maxZoom);
    }

    private void ClampCameraPosition()
    {
        if (!useWorldBounds)
            return;

        Vector3 position = transform.position;

        float halfHeight = _camera.orthographicSize;
        float halfWidth = halfHeight * _camera.aspect;

        float minX = worldMin.x + halfWidth;
        float maxX = worldMax.x - halfWidth;
        float minY = worldMin.y + halfHeight;
        float maxY = worldMax.y - halfHeight;

        position.x = ClampAxis(position.x, minX, maxX, worldMin.x, worldMax.x);
        position.y = ClampAxis(position.y, minY, maxY, worldMin.y, worldMax.y);

        transform.position = position;
    }

    private static float ClampAxis(
        float value,
        float minimum,
        float maximum,
        float worldMinimum,
        float worldMaximum)
    {
        // If the visible area is larger than the world on this axis,
        // center the camera on that axis instead of producing invalid limits.
        if (minimum > maximum)
            return (worldMinimum + worldMaximum) * 0.5f;

        return Mathf.Clamp(value, minimum, maximum);
    }

    /// <summary>
    /// Sets the world-space bounds. Intended for use by the map generator.
    /// </summary>
    public void SetWorldBounds(Vector2 minimum, Vector2 maximum)
    {
        worldMin = Vector2.Min(minimum, maximum);
        worldMax = Vector2.Max(minimum, maximum);

        // Awake előtt (pl. egy másik komponens Awake-jéből hívva) még nincs
        // kamera; ilyenkor az Update végzi el a pozíció korlátozását.
        if (_camera != null)
            ClampCameraPosition();
    }

    private static void EnableAction(InputActionReference reference)
    {
        if (reference != null)
            reference.action.Enable();
    }

    private static void DisableAction(InputActionReference reference)
    {
        if (reference != null)
            reference.action.Disable();
    }

    private void OnValidate()
    {
        minZoom = Mathf.Max(0.01f, minZoom);
        maxZoom = Mathf.Max(0.01f, maxZoom);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        zoomSensitivity = Mathf.Max(0f, zoomSensitivity);

        if (minZoom > maxZoom)
            maxZoom = minZoom;
    }
}
