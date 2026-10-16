
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(DirectionalSpriteRenderer))]
public sealed class TurretVehiclePreview : MonoBehaviour
{
    [Header("Turret")]
    [SerializeField]
    private Transform turretPivot;

    [SerializeField]
    private DirectionalSpriteRenderer turretRenderer;

    [Header("Body rotation")]
    [SerializeField, Min(0f)]
    private float bodyRotationSpeed = 30f;

    [Header("Input")]
    [SerializeField]
    private Camera targetCamera;

    private DirectionalSpriteRenderer _bodyRenderer;
    private float _bodyTargetAngle;

    private void Awake()
    {
        _bodyRenderer = GetComponent<DirectionalSpriteRenderer>();
        _bodyRenderer.Initialize();

        if (turretPivot == null)
        {
            Debug.LogError("TurretPivot nincs beállítva.", this);
            enabled = false;
            return;
        }

        if (turretRenderer == null)
        {
            turretRenderer =
                turretPivot.GetComponent<DirectionalSpriteRenderer>();
        }

        if (turretRenderer == null)
        {
            Debug.LogError(
                "A TurretPivot objektumon nincs DirectionalSpriteRenderer.",
                this
            );

            enabled = false;
            return;
        }

        turretRenderer.Initialize();

        if (targetCamera == null)
            targetCamera = Camera.main;

        _bodyTargetAngle = _bodyRenderer.CurrentAngle;
    }

    private void Update()
    {
        RotateBody();
        AimTurretAtMouse();
    }

    private void RotateBody()
    {
        _bodyTargetAngle = Mathf.Repeat(
            _bodyTargetAngle + bodyRotationSpeed * Time.deltaTime,
            360f
        );

        _bodyRenderer.SetTargetAngle(_bodyTargetAngle);
    }

    private void AimTurretAtMouse()
    {
        if (targetCamera == null || Mouse.current == null)
            return;

        Vector2 mouseScreenPosition =
            Mouse.current.position.ReadValue();

        Vector3 mouseWorldPosition =
            targetCamera.ScreenToWorldPoint(
                new Vector3(
                    mouseScreenPosition.x,
                    mouseScreenPosition.y,
                    -targetCamera.transform.position.z
                )
            );

        Vector2 direction =
            (Vector2)(mouseWorldPosition - turretPivot.position);

        if (direction.sqrMagnitude < 0.0001f)
            return;

        // A világirány kiszámítása:
        // 0° = fel, 90° = jobbra.
        float worldAngle = Mathf.Atan2(
            direction.x,
            direction.y
        ) * Mathf.Rad2Deg;

        worldAngle = Mathf.Repeat(worldAngle, 360f);


        turretRenderer.SetTargetAngle(worldAngle);
    }
}
