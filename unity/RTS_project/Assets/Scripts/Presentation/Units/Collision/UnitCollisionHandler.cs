
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

            return false;
        }

        return true;
    }

    private static bool IsMovementObstacle(Collider2D collider)
    {
        if (collider == null)
            return false;

        return collider.GetComponentInParent<MovementObstacle>()
            != null;
    }
}
