using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(DirectionalSpriteRenderer))]
public sealed class UnitSelection : MonoBehaviour
{
    [SerializeField, Min(0f)]
    private float colliderPadding = 0.05f;

    private BoxCollider2D _boxCollider;
    private DirectionalSpriteRenderer _directionalRenderer;

    private void Awake()
    {
        _boxCollider = GetComponent<BoxCollider2D>();
        _directionalRenderer = GetComponent<DirectionalSpriteRenderer>();

        _directionalRenderer.Initialize();
        InitializeCollider();
    }

    private void InitializeCollider()
    {
        int spriteCount = _directionalRenderer.ViewCount;

        if (spriteCount <= 0)
        {
            Debug.LogError(
                "A kijelölési collider nem inicializálható: nincs beállított sprite.",
                this);

            _boxCollider.enabled = false;
            return;
        }

        Vector2 totalSize = Vector2.zero;

        for (int i = 0; i < spriteCount; i++)
        {
            Sprite sprite = _directionalRenderer.GetSpriteAtIndex(i);

            if (sprite == null)
                continue;

            totalSize += (Vector2)sprite.bounds.size;
        }

        Vector2 averageSize = totalSize / spriteCount;

        _boxCollider.size =
            averageSize + Vector2.one * colliderPadding * 2f;

        _boxCollider.offset = Vector2.zero;
        _boxCollider.enabled = true;
    }
}