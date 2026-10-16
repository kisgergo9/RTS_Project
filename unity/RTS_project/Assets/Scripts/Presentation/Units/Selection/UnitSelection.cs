using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(DirectionalSpriteRenderer))]
public sealed class UnitSelection : MonoBehaviour
{
    private SpriteRenderer _spriteRenderer;
    private LineRenderer _selectionOutline;

    public bool IsSelected { get; private set; }

    [SerializeField, Min(0f)]
    private float colliderPadding = 0.05f;

    private BoxCollider2D _boxCollider;
    private DirectionalSpriteRenderer _directionalRenderer;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _boxCollider = GetComponent<BoxCollider2D>();
        _directionalRenderer = GetComponent<DirectionalSpriteRenderer>();
        _directionalRenderer.Initialize();
        InitializeCollider();

        GameObject outlineObject = new GameObject("SelectionOutline");
        outlineObject.transform.SetParent(transform, false);

        _selectionOutline = outlineObject.AddComponent<LineRenderer>();
        _selectionOutline.useWorldSpace = true;
        _selectionOutline.loop = true;
        _selectionOutline.positionCount = 4;
        _selectionOutline.startWidth = 0.015f;
        _selectionOutline.endWidth = 0.015f;
        _selectionOutline.startColor = Color.white;
        _selectionOutline.endColor = Color.white;
        _selectionOutline.material =
            new Material(Shader.Find("Sprites/Default"));

        _selectionOutline.sortingLayerID =
            _spriteRenderer.sortingLayerID;

        _selectionOutline.sortingOrder =
            _spriteRenderer.sortingOrder + 1;

        _selectionOutline.enabled = false;
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;

        if (_selectionOutline != null)
            _selectionOutline.enabled = selected;

        if (selected)
            UpdateOutline();
    }

    private void LateUpdate()
    {
        if (IsSelected)
            UpdateOutline();
    }

    private void UpdateOutline()
    {
        if (_spriteRenderer == null || _selectionOutline == null)
            return;

        Bounds bounds = _spriteRenderer.bounds;

        float padding = 0.08f;
        float minX = bounds.min.x - padding;
        float maxX = bounds.max.x + padding;
        float minY = bounds.min.y - padding;
        float maxY = bounds.max.y + padding;

        // A keret kissé a sprite elé kerüljön.
        float z = bounds.min.z - 0.01f;

        _selectionOutline.SetPosition(0, new Vector3(minX, minY, z));
        _selectionOutline.SetPosition(1, new Vector3(minX, maxY, z));
        _selectionOutline.SetPosition(2, new Vector3(maxX, maxY, z));
        _selectionOutline.SetPosition(3, new Vector3(maxX, minY, z));
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

            totalSize += (Vector2)sprite.bounds.size;
        }

        Vector2 averageSize = totalSize / spriteCount;

        _boxCollider.size =
            averageSize + Vector2.one * colliderPadding * 2f;

        _boxCollider.offset = Vector2.zero;
        _boxCollider.enabled = true;
    }
}