
using UnityEngine;

[RequireComponent(typeof(DirectionalSpriteRenderer))]
public sealed class UnitMovement : MonoBehaviour
{
    private enum MovementState
    {
        Idle,
        Moving
    }

    [Header("Movement")]
    [SerializeField, Min(0f)]
    private float moveSpeed = 2f;

    [SerializeField, Min(0.01f)]
    private float arrivalDistance = 0.1f;

    [SerializeField, Min(0.01f)]
    private float acceleration = 2f;

    [SerializeField, Min(0.01f)]
    private float deceleration = 4f;

    [Header("Turning")]
    [Tooltip("Ekkora vagy kisebb irányeltérésnél használható a teljes sebesség.")]
    [SerializeField, Range(0f, 180f)]
    private float fullSpeedTurnAngle = 30f;

    [Tooltip("Ekkora irányeltérésnél a kívánt sebesség nulla.")]
    [SerializeField, Range(1f, 180f)]
    private float stopForTurnAngle = 150f;

    private DirectionalSpriteRenderer _directionalRenderer;

    private MovementState _state = MovementState.Idle;

    private Vector2 _destination;

    private float _currentSpeed;

    public bool IsMoving => _state != MovementState.Idle;

    public Vector2 Destination => _destination;

    public float CurrentSpeed => _currentSpeed;

    private void Awake()
    {
        _directionalRenderer =
            GetComponent<DirectionalSpriteRenderer>();

        _directionalRenderer.Initialize();
    }

    /// <summary>
    /// Új mozgási parancsot ad az egységnek.
    /// A korábbi sebesség megmarad, ezért az egység
    /// természetesen reagálhat az új célpontra.
    /// </summary>
    public void MoveTo(Vector2 destination)
    {
        _destination = destination;

        Vector2 position = transform.position;
        Vector2 direction = _destination - position;

        if (direction.sqrMagnitude
            <= arrivalDistance * arrivalDistance)
        {
            StopMovement();
            return;
        }

        _state = MovementState.Moving;
    }

    /// <summary>
    /// Azonnal megszakítja a mozgást.
    /// </summary>
    public void StopMovement()
    {
        _state = MovementState.Idle;
        _currentSpeed = 0f;

        _directionalRenderer.SetAngle(
            _directionalRenderer.CurrentAngle
        );
    }

    private void Update()
    {
        if (_state == MovementState.Idle)
            return;

        Vector2 position = transform.position;
        Vector2 toDestination = _destination - position;

        float distance = toDestination.magnitude;

        if (distance <= arrivalDistance)
        {
            FinishMovement();
            return;
        }

        // A célirány mindig az aktuális pozícióból számolódik.
        float targetAngle = CalculateAngle(toDestination);

        _directionalRenderer.SetTargetAngle(targetAngle);

        // Az aktuális irány és a célirány közötti eltérés.
        float angleDifference = Mathf.Abs(
            Mathf.DeltaAngle(
                _directionalRenderer.CurrentAngle,
                targetAngle
            )
        );

        // Nagy irányváltásnál csökkentjük a kívánt sebességet.
        // A forgás közben ez az érték folyamatosan változik.
        float turnSpeedFactor = CalculateTurnSpeedFactor(
            angleDifference
        );

        // A célpont közelében a féktávolság alapján lassítunk.
        float stoppingDistance = Mathf.Max(
            0f,
            distance - arrivalDistance
        );

        float brakingSpeed = Mathf.Sqrt(
            2f * deceleration * stoppingDistance
        );

        float targetSpeed = Mathf.Min(
            moveSpeed * turnSpeedFactor,
            brakingSpeed
        );

        // A tényleges sebesség nem ugrik azonnal a célértékre.
        float speedChange = targetSpeed > _currentSpeed
            ? acceleration
            : deceleration;

        _currentSpeed = Mathf.MoveTowards(
            _currentSpeed,
            targetSpeed,
            speedChange * Time.deltaTime
        );

        // 0 fok = felfelé, 90 fok = jobbra.
        // Mindig az aktuális irányba haladunk.
        float angleRadians =
            _directionalRenderer.CurrentAngle * Mathf.Deg2Rad;

        Vector2 forward = new Vector2(
            Mathf.Sin(angleRadians),
            Mathf.Cos(angleRadians)
        );

        float step = _currentSpeed * Time.deltaTime;

        // A lépés ne legyen nagyobb a még hátralévõ
        // távolságnál. Nem teleportálunk a célpontra.
        step = Mathf.Min(step, distance);

        Vector2 nextPosition = position + forward * step;

        transform.position = new Vector3(
            nextPosition.x,
            nextPosition.y,
            transform.position.z
        );

        // A következõ frissítés ellenõrzi a tényleges
        // távolságot, és lezárja a mozgást, ha szükséges.
    }

    private float CalculateTurnSpeedFactor(float angleDifference)
    {
        if (angleDifference >= stopForTurnAngle)
            return 0f;

        if (angleDifference <= fullSpeedTurnAngle)
            return 1f;

        float t = Mathf.InverseLerp(
            fullSpeedTurnAngle,
            stopForTurnAngle,
            angleDifference
        );

        return 1f - t;
    }

    private static float CalculateAngle(Vector2 direction)
    {
        float angle = Mathf.Atan2(
            direction.x,
            direction.y
        ) * Mathf.Rad2Deg;

        return Mathf.Repeat(angle, 360f);
    }

    private void FinishMovement()
    {
        _state = MovementState.Idle;
        _currentSpeed = 0f;

        _directionalRenderer.SetAngle(
            _directionalRenderer.CurrentAngle
        );
    }
}
