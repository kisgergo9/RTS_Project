
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class UnitCollisionHandler : MonoBehaviour
{
    [Header("Movement Collision")]
    [SerializeField]
    private BoxCollider2D movementCollider;

    [Header("Obstacle Detection")]
    [SerializeField]
    private LayerMask obstacleLayers = ~0;

    [SerializeField, Min(0f)]
    private float separationTolerance = 0.001f;

    private readonly HashSet<UnitGroup> _passThroughGroups = new();

    private UnitGroup _lastBlockingGroup;
    private UnitGroupMember _lastBlockingMember;

    public UnitGroup LastBlockingGroup => _lastBlockingGroup;
    public UnitGroupMember LastBlockingMember => _lastBlockingMember;

    public void SetPassThroughGroups(params UnitGroup[] groups)
    {
        _passThroughGroups.Clear();

        if (groups == null)
            return;

        foreach (UnitGroup group in groups)
        {
            if (group != null)
                _passThroughGroups.Add(group);
        }
    }

    public bool CanMoveTo(Vector2 nextPosition)
    {
        if (movementCollider == null ||
            !movementCollider.enabled)
        {
            Debug.LogError(
                $"A MovementCollider nincs beállítva: {name}",
                this);

            return false;
        }

        _lastBlockingGroup = null;
        _lastBlockingMember = null;

        Vector2 delta =
            nextPosition - (Vector2)transform.position;

        float distance = delta.magnitude;

        if (distance <= 0.000001f)
            return true;

        Vector2 direction = delta / distance;

        Bounds bounds = movementCollider.bounds;

        // Az egység tényleges colliderének alakját
        // használjuk az akadállyal való távolság méréséhez.
        Collider2D[] nearbyColliders =
            Physics2D.OverlapBoxAll(
                bounds.center,
                bounds.size,
                0f,
                obstacleLayers);

        bool currentlyOverlappingObstacle = false;

        foreach (Collider2D other in nearbyColliders)
        {
            if (!IsMovementObstacle(other))
                continue;

            ColliderDistance2D separation =
                movementCollider.Distance(other);

            if (!separation.isOverlapped)
                continue;

            currentlyOverlappingObstacle = true;

            // A normál az egység colliderétõl
            // az akadály felé mutat.
            // Negatív skalárszorzat: kifelé haladunk.
            float movementAgainstNormal =
                Vector2.Dot(direction, separation.normal);

            if (movementAgainstNormal > separationTolerance)
            {
                // Legalább az egyik átfedõ akadály felé
                // haladnánk, ezért blokkoljuk a mozgást.
                RecordBlockingCollider(other);
                return false;
            }
        }

        if (currentlyOverlappingObstacle)
        {
            // Már fennálló átfedés esetén csak olyan
            // mozgást engedünk, amely nem halad bele
            // jobban egyik átfedõ akadályba sem.
            return true;
        }

        // Ha nincs fennálló átfedés, a teljes mozgási
        // szakaszt vizsgáljuk, nem csak a végpontot.
        RaycastHit2D[] hits = Physics2D.BoxCastAll(
            bounds.center,
            bounds.size,
            0f,
            direction,
            distance,
            obstacleLayers);

        foreach (RaycastHit2D hit in hits)
        {
            Collider2D other = hit.collider;

            if (!IsMovementObstacle(other))
                continue;

            // A már érintett felülettõl kifelé vagy
            // érintõlegesen haladás ne legyen blokkolva.
            if (hit.distance <= separationTolerance &&
                Vector2.Dot(direction, hit.normal) >= -separationTolerance)
            {
                continue;
            }

            RecordBlockingCollider(other);
            return false;
        }

        return true;
    }

    private void RecordBlockingCollider(Collider2D collider)
    {
        if (collider == null)
            return;

        _lastBlockingMember =
            collider.GetComponentInParent<UnitGroupMember>();

        _lastBlockingGroup = _lastBlockingMember != null
            ? _lastBlockingMember.Group
            : null;
    }

    public void NotifyMovementBlocked()
    {
        UnitGroupMember groupMember =
            GetComponent<UnitGroupMember>();

        if (groupMember == null || groupMember.Group == null)
            return;

        groupMember.Group.HandleMemberCollision(
            groupMember,
            _lastBlockingGroup,
            _lastBlockingMember);
    }

    private bool IsMovementObstacle(Collider2D collider)
    {
        if (collider == null)
            return false;

        // A terepakadályokat továbbra is a meglévõ
        // MovementObstacle komponens alapján azonosítjuk.
        if (collider.GetComponentInParent<MovementObstacle>() != null)
            return true;

        // A saját egységhez tartozó collidereket kihagyjuk.
        if (collider.transform == transform ||
            collider.transform.IsChildOf(transform))
        {
            return false;
        }

        // Megkeressük, tartozik-e a collider egy másik
        // egységhez.
        UnitGroupMember otherMember =
            collider.GetComponentInParent<UnitGroupMember>();

        if (otherMember == null)
            return false;

        // A csatlakozás során az eredeti és a célcsoport
        // egységein is át lehet haladni.
        if (otherMember.Group != null && _passThroughGroups.Contains(otherMember.Group))
        {
            return false;
        }

        // Saját csoporttag ne számítson külsõ akadálynak.
        UnitGroupMember ownMember =
            GetComponent<UnitGroupMember>();

        if (ownMember != null &&
            ownMember.Group != null &&
            ownMember.Group == otherMember.Group)
        {
            return false;
        }

        // A másik csoport tagja akadály,
        // függetlenül attól, hogy a csoport mozog-e.
        return otherMember.Group != null;
    }

    public bool CanOccupyPosition(Vector2 position)
{
    BoxCollider2D boxCollider =
        GetComponentInChildren<BoxCollider2D>();

    if (boxCollider == null || !boxCollider.enabled)
        return true;

    Vector2 currentPosition = transform.position;

    // A collider középpontját az egység tervezett
    // pozíciójához igazítjuk.
    Vector2 candidateCenter =
        (Vector2)boxCollider.bounds.center
        + (position - currentPosition);

    Vector2 scale = boxCollider.transform.lossyScale;

    Vector2 size = new Vector2(
        boxCollider.size.x * Mathf.Abs(scale.x),
        boxCollider.size.y * Mathf.Abs(scale.y));

    float angle = boxCollider.transform.eulerAngles.z;

    Collider2D[] overlaps = Physics2D.OverlapBoxAll(
        candidateCenter,
        size,
        angle);

    foreach (Collider2D overlap in overlaps)
    {
        if (overlap == null || overlap == boxCollider)
            continue;

        // Saját egységhez tartozó collidereket figyelmen
        // kívül hagyjuk.
        if (overlap.transform == transform ||
            overlap.transform.IsChildOf(transform))
        {
            continue;
        }

        // Csak a megjelölt tereptárgyak akadályok.
        if (overlap.GetComponentInParent<MovementObstacle>() != null)
            return false;
    }

    return true;
}
}
