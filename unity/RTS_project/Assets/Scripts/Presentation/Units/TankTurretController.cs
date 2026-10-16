using UnityEngine;

[DisallowMultipleComponent]
public sealed class TankTurretController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private DirectionalSpriteRenderer bodyRenderer;

    [SerializeField]
    private DirectionalSpriteRenderer turretRenderer;

    [SerializeField]
    private Transform turretPivot;

    [Header("Turret position")]
    [Tooltip("A torony rögzítési pontja kövesse a test irányát.")]
    [SerializeField]
    private bool rotateMountOffsetWithBody = true;

    private Transform _currentTarget;
    private Vector3 _initialLocalPosition;

    public bool HasTarget => _currentTarget != null;

    private void Awake()
    {
        if (bodyRenderer == null)
            bodyRenderer = GetComponent<DirectionalSpriteRenderer>();

        if (turretPivot == null)
        {
            Debug.LogError(
                "A TankTurretController TurretPivot referenciája hiányzik.",
                this
            );

            enabled = false;
            return;
        }

        if (turretRenderer == null)
        {
            turretRenderer =
                turretPivot.GetComponent<DirectionalSpriteRenderer>();
        }

        if (bodyRenderer == null || turretRenderer == null)
        {
            Debug.LogError(
                "A testhez és a toronyhoz is szükséges " +
                "DirectionalSpriteRenderer komponens.",
                this
            );

            enabled = false;
            return;
        }

        bodyRenderer.Initialize();
        turretRenderer.Initialize();

        _initialLocalPosition = turretPivot.localPosition;
    }

    private void LateUpdate()
    {
        UpdateTurretPosition();
        UpdateTurretDirection();
    }

    private void UpdateTurretPosition()
    {
        if (!rotateMountOffsetWithBody)
            return;

        // A test sprite-ja irányváltással fordul,
        // nem a Transform tényleges forgatásával.
        Vector2 offset = _initialLocalPosition;

        Vector2 rotatedOffset = Quaternion.Euler(
            0f,
            0f,
            -bodyRenderer.CurrentAngle
        ) * offset;

        turretPivot.localPosition = new Vector3(
            rotatedOffset.x,
            rotatedOffset.y,
            _initialLocalPosition.z
        );
    }

    private void UpdateTurretDirection()
    {
        if (_currentTarget == null)
        {
            // Nincs célzás: a torony a test irányát követi.
            turretRenderer.SetTargetAngle(
                bodyRenderer.CurrentAngle
            );

            return;
        }

        Vector2 direction =
            (Vector2)(_currentTarget.position - turretPivot.position);

        if (direction.sqrMagnitude < 0.0001f)
            return;

        // 0° = felfelé, 90° = jobbra.
        float worldAngle = Mathf.Atan2(
            direction.x,
            direction.y
        ) * Mathf.Rad2Deg;

        turretRenderer.SetTargetAngle(
            Mathf.Repeat(worldAngle, 360f)
        );
    }

    public void SetTarget(Transform target)
    {
        _currentTarget = target;
    }

    public void ClearTarget()
    {
        _currentTarget = null;
    }
}