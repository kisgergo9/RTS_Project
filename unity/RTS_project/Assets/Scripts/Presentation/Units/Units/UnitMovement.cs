
using UnityEngine;

[RequireComponent(typeof(DirectionalSpriteRenderer))]
[RequireComponent(typeof(UnitCollisionHandler))]
public sealed class UnitMovement : MonoBehaviour
{
    private enum MovementState
    {
        Idle,
        Moving,
        TurningInPlace,
        GroupTurning
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

    [Tooltip("A csoportos fordulás ennyi fokos eltérésnél tekinthetõ késznek.")]
    [SerializeField, Range(0.1f, 10f)]
    private float groupTurnTolerance = 1f;

    private DirectionalSpriteRenderer _directionalRenderer;
    private UnitCollisionHandler _collisionHandler;

    private MovementState _state = MovementState.Idle;

    private Vector2 _destination;
    private float _currentSpeed;

    private float _groupMaxSpeed = float.PositiveInfinity;

    public float BaseMoveSpeed => moveSpeed;

    public float BaseTurnSpeed
    {
        get
        {
            DirectionalSpriteRenderer renderer =
                GetComponent<DirectionalSpriteRenderer>();

            return renderer != null ? renderer.BaseTurnSpeed : 0f;
        }
    }

    private float EffectiveMoveSpeed =>
        Mathf.Min(moveSpeed, _groupMaxSpeed);

    private float _groupTurnAngle;
    private bool _groupTurnComplete;

    public bool IsMoving => _state != MovementState.Idle;
    public bool IsTravelling => _state == MovementState.Moving;

    public Vector2 Destination => _destination;

    public float CurrentSpeed => _currentSpeed;

    public bool IsGroupTurnComplete => _groupTurnComplete;

    private void Awake()
    {
        _directionalRenderer =
            GetComponent<DirectionalSpriteRenderer>();

        _collisionHandler =
            GetComponent<UnitCollisionHandler>();

        _directionalRenderer.Initialize();
    }

    public void MoveTo(Vector2 destination)
    {
        _groupTurnComplete = false;
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
    /// Csoport által kezdeményezett fordulás.
    /// Az egység elõször lefékez, majd helyben a megadott
    /// irányba fordul. A befejezés az IsGroupTurnComplete
    /// tulajdonságon keresztül ellenõrizhetõ.
    /// </summary>
    public void BeginGroupTurn(float targetAngle)
    {
        _groupTurnAngle = Mathf.Repeat(targetAngle, 360f);
        _groupTurnComplete = false;
        _state = MovementState.GroupTurning;
    }

    public void SetMovementLimits(
    float maxMoveSpeed,
    float maxTurnSpeed)
    {
        _groupMaxSpeed = Mathf.Max(0f, maxMoveSpeed);

        if (_directionalRenderer == null)
        {
            _directionalRenderer =
                GetComponent<DirectionalSpriteRenderer>();
        }

        if (_directionalRenderer != null)
            _directionalRenderer.SetTurnSpeedLimit(maxTurnSpeed);
    }

    public void ResetMovementLimits()
    {
        _groupMaxSpeed = float.PositiveInfinity;

        if (_directionalRenderer == null)
        {
            _directionalRenderer =
                GetComponent<DirectionalSpriteRenderer>();
        }

        if (_directionalRenderer != null)
            _directionalRenderer.ResetTurnSpeedLimit();
    }

    public void StopMovement()
    {
        _state = MovementState.Idle;
        _currentSpeed = 0f;
        _groupTurnComplete = false;

        _directionalRenderer.SetAngle(
            _directionalRenderer.CurrentAngle);
    }

    private void Update()
    {
        if (_state == MovementState.Idle)
            return;

        if (_state == MovementState.GroupTurning)
        {
            UpdateGroupTurning();
            return;
        }

        Vector2 position = transform.position;
        Vector2 toDestination = _destination - position;

        float distance = toDestination.magnitude;

        if (distance <= arrivalDistance)
        {
            FinishMovement();
            return;
        }

        float targetAngle = CalculateAngle(toDestination);

        _directionalRenderer.SetTargetAngle(targetAngle);

        float angleDifference = Mathf.Abs(
            Mathf.DeltaAngle(
                _directionalRenderer.CurrentAngle,
                targetAngle));

        if (_state == MovementState.Moving &&
            angleDifference >= stopForTurnAngle)
        {
            _state = MovementState.TurningInPlace;
        }

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
            targetSpeed = 0f;
        }
        else
        {
            float turnSpeedFactor =
                CalculateTurnSpeedFactor(angleDifference);

            float stoppingDistance = Mathf.Max(
                0f,
                distance - arrivalDistance);

            float brakingSpeed = Mathf.Sqrt(
                2f * deceleration * stoppingDistance);

            targetSpeed = Mathf.Min(
                EffectiveMoveSpeed * turnSpeedFactor,
                brakingSpeed);
        }

        float speedChange = targetSpeed > _currentSpeed
            ? acceleration
            : deceleration;

        _currentSpeed = Mathf.MoveTowards(
            _currentSpeed,
            targetSpeed,
            speedChange * Time.deltaTime);

        MoveForward();
    }

    private void UpdateGroupTurning()
    {
        _directionalRenderer.SetTargetAngle(_groupTurnAngle);

        float angleDifference = Mathf.Abs(
            Mathf.DeltaAngle(
                _directionalRenderer.CurrentAngle,
                _groupTurnAngle));

        // A már mozgó egység elõször lefékez.
        _currentSpeed = Mathf.MoveTowards(
            _currentSpeed,
            0f,
            deceleration * Time.deltaTime);

        if (_currentSpeed > 0f)
            MoveForward();

        // Csak teljes megállás és befejezett fordulás
        // után küldünk kész állapotot a csapatnak.
        if (_currentSpeed <= 0.01f &&
            angleDifference <= groupTurnTolerance)
        {
            _currentSpeed = 0f;
            _state = MovementState.Idle;
            _groupTurnComplete = true;
        }
    }

    private void MoveForward()
    {
        float angleRadians =
            _directionalRenderer.CurrentAngle * Mathf.Deg2Rad;

        Vector2 forward = new Vector2(
            Mathf.Sin(angleRadians),
            Mathf.Cos(angleRadians));

        Vector2 position = transform.position;

        Vector2 nextPosition =
            position + forward * (_currentSpeed * Time.deltaTime);

        // Elõbb ütközést vizsgálunk, csak utána mozgatunk.
        if (!_collisionHandler.CanMoveTo(nextPosition))
        {
            if (_state == MovementState.GroupTurning)
            {
                // A csoportfordulás alatt sem maradhatunk
                // végtelenül várakozó állapotban.
                _currentSpeed = 0f;
                _state = MovementState.Idle;
                _groupTurnComplete = true;
            }
            else
            {
                StopMovement();
            }

            return;
        }

        transform.position = new Vector3(
            nextPosition.x,
            nextPosition.y,
            transform.position.z);
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
            angleDifference);

        return 1f - t;
    }

    private static float CalculateAngle(Vector2 direction)
    {
        float angle = Mathf.Atan2(
            direction.x,
            direction.y) * Mathf.Rad2Deg;

        return Mathf.Repeat(angle, 360f);
    }

    private void FinishMovement()
    {
        _state = MovementState.Idle;
        _currentSpeed = 0f;

        _directionalRenderer.SetAngle(
            _directionalRenderer.CurrentAngle);
    }
}
