using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class BuildingView : MonoBehaviour
{
    [SerializeField] private Sprite[] levelSprites;

    [SerializeField] private Color outlineColor = new Color32(255, 255, 255, 130);
    [SerializeField] private float outlineScale = 1.06f;

    private SpriteRenderer spriteRenderer;
    private SpriteRenderer outlineRenderer;
    private int shownLevel = -1;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        CreateOutline();
    }

    public void SetLevel(int level)
    {
        if (level == shownLevel)
        {
            return;
        }

        if (level >= levelSprites.Length)
        {
            return;
        }

        shownLevel = level;
        spriteRenderer.sprite = levelSprites[level];
        outlineRenderer.sprite = levelSprites[level];
        CenterOutline();
    }

    public void SetHighlighted(bool highlighted)
    {
        outlineRenderer.enabled = highlighted;
    }

    private void CreateOutline()
    {
        Material material = new Material(Shader.Find("RTS/WhiteSilhouette"));
        material.color = outlineColor;

        GameObject outlineObject = new GameObject("Outline");
        outlineObject.transform.SetParent(transform, false);
        outlineObject.transform.localScale = new Vector3(outlineScale, outlineScale, 1f);

        outlineRenderer = outlineObject.AddComponent<SpriteRenderer>();
        outlineRenderer.material = material;
        outlineRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        outlineRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
        outlineRenderer.enabled = false;
    }

    // A nagyítás a sprite közepe körül történjen, ne a pivot körül.
    private void CenterOutline()
    {
        Vector3 center = outlineRenderer.sprite.bounds.center;
        outlineRenderer.transform.localPosition = -center * (outlineScale - 1f);
    }
}
