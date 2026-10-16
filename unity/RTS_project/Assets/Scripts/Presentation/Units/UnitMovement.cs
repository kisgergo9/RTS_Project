using UnityEngine;

[RequireComponent(typeof(DirectionalSpriteRenderer))]
public sealed class UnitMovement : MonoBehaviour
{
    private enum MovementState
    {
        Idle,
        Moving,
        TurningInPlace
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

    [Tooltip("Ekkora irányeltérésnél az egység megkezdi a megállást.")]
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

        // A cél irányát mindig az aktuális pozícióból számoljuk.
        float targetAngle = CalculateAngle(toDestination);

        _directionalRenderer.SetTargetAngle(targetAngle);

        float angleDifference = Mathf.Abs(
            Mathf.DeltaAngle(
                _directionalRenderer.CurrentAngle,
                targetAngle
            )
        );

        // Nagy irányeltérésnél átváltunk helyben fordulásra.
        if (_state == MovementState.Moving &&
            angleDifference >= stopForTurnAngle)
        {
            _state = MovementState.TurningInPlace;
        }

        // Csak teljes megállás után indulhat újra,
        // ha már megfelelõen a cél felé néz.
        if (_state == MovementState.TurningInPlace &&
            _currentSpeed <= 0.01f &&
            angleDifference <= fullSpeedTurnAngle)
        {
            _currentSpeed = 0f;
            _state = MovementState.Moving;
        }

        float targetSpeed;

        if (_state == MovementState.TurningInPlace)
        {
            // Elõször lefékezünk, utána helyben fordulunk.
            targetSpeed = 0f;
        }
        else
        {
            float turnSpeedFactor =
                CalculateTurnSpeedFactor(angleDifference);

            float stoppingDistance = Mathf.Max(
                0f,
                distance - arrivalDistance
            );

            float brakingSpeed = Mathf.Sqrt(
                2f * deceleration * stoppingDistance
            );

            targetSpeed = Mathf.Min(
                moveSpeed * turnSpeedFactor,
                brakingSpeed
            );
        }

        float speedChange = targetSpeed > _currentSpeed
            ? acceleration
            : deceleration;

        _currentSpeed = Mathf.MoveTowards(
            _currentSpeed,
            targetSpeed,
            speedChange * Time.deltaTime
        );

        // A sprite iránya szerint haladunk.
        // A TurningInPlace állapotban is lefékezünk,
        // ezért a sebesség csak fokozatosan csökken nullára.
        float angleRadians =
            _directionalRenderer.CurrentAngle * Mathf.Deg2Rad;

        Vector2 forward = new Vector2(
            Mathf.Sin(angleRadians),
            Mathf.Cos(angleRadians)
        );

        float step = _currentSpeed * Time.deltaTime;
        step = Mathf.Min(step, distance);

        Vector2 nextPosition = position + forward * step;

        transform.position = new Vector3(
            nextPosition.x,
            nextPosition.y,
            transform.position.z
        );
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