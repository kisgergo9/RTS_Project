
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class DirectionalSpriteRenderer : MonoBehaviour
{
    [Header("Directional sprites")]
    [SerializeField]
    private Sprite[] sprites;

    [Header("Direction mapping")]
    [SerializeField]
    private float referenceAngle;

    private SpriteRenderer _spriteRenderer;
    private DirectionalSpriteSet _spriteSet;

    public int ViewCount => _spriteSet?.ViewCount ?? 0;

    public void Initialize()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();

        _spriteSet = new DirectionalSpriteSet(
            sprites);

        SetAngle(referenceAngle);
    }

    public void SetAngle(float worldAngle)
    {
        if (_spriteSet == null || _spriteRenderer == null)
            return;

        _spriteRenderer.sprite =
            _spriteSet.GetSpriteForAngle(worldAngle);
    }

    public void SetView(int index)
    {
        if (_spriteSet == null || _spriteRenderer == null)
            return;

        _spriteRenderer.sprite =
            _spriteSet.GetSpriteAtIndex(index);
    }

    public Sprite GetSpriteAtIndex(int index)
    {
        if (_spriteSet == null)
            return null;

        return _spriteSet.GetSpriteAtIndex(index);
    }
}
