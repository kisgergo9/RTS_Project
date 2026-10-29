using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class BuildingView : MonoBehaviour
{
    private static readonly Color OutlineColor = new Color32(255, 255, 255, 130);
    private const float OutlineScale = 1.06f;
    private const float PanelGap = 0.1f;

    [SerializeField] private Sprite[] levelSprites;

    private SpriteRenderer spriteRenderer;
    private SpriteRenderer outlineRenderer;
    private BuildingInfoPanel infoPanel;
    private int shownLevel = -1;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        CreateOutline();
    }

    public void Show(BaseData data, int buildingIndex)
    {
        int level = data.GetLevel(buildingIndex);
        SetLevel(level);

        if (infoPanel == null)
        {
            infoPanel = new BuildingInfoPanel(transform, buildingIndex, spriteRenderer.sortingLayerID, spriteRenderer.sortingOrder + 100);
        }

        bool producing = buildingIndex == data.ProducingIndex;
        bool upgrading = buildingIndex == data.UpgradingIndex;

        outlineRenderer.enabled = producing || upgrading;
        infoPanel.SetVisible(producing || upgrading);

        if (!producing && !upgrading)
        {
            return;
        }

        int targetLevel = level + 1;

        Bounds bounds = spriteRenderer.sprite.bounds;
        infoPanel.SetPosition(new Vector2(bounds.center.x, bounds.max.y), PanelGap);

        infoPanel.SetLevel(level);
        infoPanel.SetProduction(producing, Mathf.CeilToInt(data.ProductionTimeLeft), data.ProductionProgress);
        infoPanel.SetUpgrade(upgrading, targetLevel, Mathf.CeilToInt(data.UpgradeTimeLeft), data.UpgradeProgress, GetSprite(targetLevel));
    }

    private Sprite GetSprite(int level)
    {
        if (level < 0 || level >= levelSprites.Length)
        {
            return null;
        }

        return levelSprites[level];
    }

    private void SetLevel(int level)
    {
        if (level == shownLevel || level >= levelSprites.Length)
        {
            return;
        }

        shownLevel = level;
        spriteRenderer.sprite = levelSprites[level];
        outlineRenderer.sprite = levelSprites[level];

        // A nagyítás a sprite közepe körül történjen, ne a pivot körül.
        outlineRenderer.transform.localPosition = -spriteRenderer.sprite.bounds.center * (OutlineScale - 1f);
    }

    private void CreateOutline()
    {
        Material material = new Material(Shader.Find("RTS/WhiteSilhouette"));
        material.color = OutlineColor;

        GameObject outlineObject = new GameObject("Outline");
        outlineObject.transform.SetParent(transform, false);
        outlineObject.transform.localScale = new Vector3(OutlineScale, OutlineScale, 1f);

        outlineRenderer = outlineObject.AddComponent<SpriteRenderer>();
        outlineRenderer.material = material;
        outlineRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        outlineRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
        outlineRenderer.enabled = false;
    }
}
