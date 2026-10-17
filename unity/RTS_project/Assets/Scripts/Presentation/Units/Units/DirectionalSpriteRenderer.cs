
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class DirectionalSpriteRenderer : MonoBehaviour
{
    [Header("Directional sprites")]
    [SerializeField]
    private Sprite[] sprites;

    [Header("Initial direction")]
    [Tooltip("Az induló irány fokban. 0 = felfelé, 90 = jobbra.")]
    [SerializeField]
    private float referenceAngle;

    [Header("Rotation")]
    [Tooltip("Az elfordulás sebessége fok/másodpercben.")]
    [SerializeField, Min(0f)]
    private float turnSpeed = 180f;

    private SpriteRenderer _spriteRenderer;
    private DirectionalSpriteSet _spriteSet;
    private bool _initialized;

    public int ViewCount => _spriteSet?.ViewCount ?? 0;

    public float CurrentAngle { get; private set; }

    public float TargetAngle { get; private set; }

    public float TurnSpeed
    {
        get => turnSpeed;
        set => turnSpeed = Mathf.Max(0f, value);
    }
    public float BaseTurnSpeed => turnSpeed;

    // Alapértelmezésben nincs külsõ korlát.
    private float _turnSpeedLimit = float.PositiveInfinity;

    // A tényleges fordulási sebesség nem lépheti túl
    // sem az egység saját értékét, sem a csoport korlátját.
    private float EffectiveTurnSpeed =>
        Mathf.Min(turnSpeed, _turnSpeedLimit);

    public void SetTurnSpeedLimit(float maxTurnSpeed)
    {
        _turnSpeedLimit = Mathf.Max(0f, maxTurnSpeed);
    }

    public void ResetTurnSpeedLimit()
    {
        _turnSpeedLimit = float.PositiveInfinity;
    }


    public void Initialize()
    {
        if (_initialized)
            return;

        _spriteRenderer = GetComponent<SpriteRenderer>();

        _spriteSet = new DirectionalSpriteSet(sprites);

        _initialized = true;

        SetAngle(referenceAngle);
    }

    private void Update()
    {
        if (!_initialized)
            return;

        if (Mathf.Approximately(CurrentAngle, TargetAngle))
            return;

        CurrentAngle = Mathf.MoveTowardsAngle(
            CurrentAngle,
            TargetAngle,
            EffectiveTurnSpeed * Time.deltaTime
        );

        CurrentAngle = Mathf.Repeat(CurrentAngle, 360f);

        RenderCurrentAngle();
    }

    /// <summary>
    /// Azonnal beállítja az aktuális irányt.
    /// A célirányt is erre az értékre állítja.
    /// </summary>
    public void SetAngle(float angle)
    {
        if (!_initialized)
            Initialize();

        CurrentAngle = Mathf.Repeat(angle, 360f);
        TargetAngle = CurrentAngle;

        RenderCurrentAngle();
    }

    /// <summary>
    /// Beállítja a kívánt irányt.
    /// Az egység fokozatosan fordul el felé.
    /// </summary>
    public void SetTargetAngle(float angle)
    {
        if (!_initialized)
            Initialize();

        TargetAngle = Mathf.Repeat(angle, 360f);
    }

    /// <summary>
    /// Közvetlenül kiválaszt egy nézetet.
    /// Az aktuális irányt is a kiválasztott nézethez igazítja.
    /// </summary>
    public void SetView(int index)
    {
        if (!_initialized)
            Initialize();

        float angle = index * _spriteSet.AnglePerView;
        SetAngle(angle);
    }

    public Sprite GetSpriteAtIndex(int index)
    {
        if (!_initialized)
            Initialize();

        return _spriteSet.GetSpriteAtIndex(index);
    }

    private void RenderCurrentAngle()
    {
        _spriteRenderer.sprite =
            _spriteSet.GetSpriteForAngle(CurrentAngle);
    }
}
