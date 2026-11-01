using TMPro;
using UnityEngine;

public class BuildingInfoPanel
{
    private const float Width = 4f;
    private const float Height = 1.5f;
    private const float Border = 0.04f;
    private const float Margin = 0.2f;
    private const float PanelScale = 0.5f;

    private const float TitleY = 0.52f;
    private const float DividerY = 0.34f;
    private const float ProductionLabelY = 0.17f;
    private const float ProductionBarY = -0.02f;
    private const float UpgradeLabelY = -0.30f;
    private const float UpgradeBarY = -0.49f;

    private const float TitleFontSize = 2.2f;
    private const float RowFontSize = 1.8f;
    private const float TextHeight = 0.3f;
    private const float BarHeight = 0.08f;

    private const float PreviewSlot = 0.9f;
    private const float PreviewFrameSize = 0.8f;
    private const float PreviewSize = 0.62f;

    private static readonly Color FrameColor = new Color32(120, 120, 120, 200);
    private static readonly Color BackgroundColor = new Color32(0, 0, 0, 170);
    private static readonly Color BarBackColor = new Color32(60, 60, 60, 255);
    private static readonly Color ProductionBarColor = new Color32(120, 190, 90, 255);
    private static readonly Color UpgradeBarColor = new Color32(240, 190, 60, 255);
    private static readonly Color LabelColor = new Color32(215, 220, 205, 255);
    private static readonly Color IdleColor = new Color32(120, 128, 115, 255);

    private static Sprite whiteSprite;

    private class Row
    {
        public TMP_Text label;
        public TMP_Text time;
        public SpriteRenderer fill;
        public float barY;
    }

    private GameObject root;
    private TMP_Text levelText;
    private Row productionRow;
    private Row upgradeRow;
    private GameObject previewRoot;
    private SpriteRenderer previewRenderer;

    private float contentLeft;
    private float contentRight;
    private float rowWidth;

    public BuildingInfoPanel(Transform parent, int buildingIndex, int sortingLayerId, int sortingOrder)
    {
        root = new GameObject("InfoPanel");
        root.transform.SetParent(parent, false);

        // A szülő méretétől függetlenül ugyanakkora legyen az ablak.
        Vector3 parentScale = parent.lossyScale;
        root.transform.localScale = new Vector3(PanelScale / parentScale.x, PanelScale / parentScale.y, 1f);

        contentLeft = -Width / 2f + Margin;
        contentRight = Width / 2f - Margin - PreviewSlot;
        rowWidth = contentRight - contentLeft;

        CreateBox("Frame", 0f, 0f, Width, Height, FrameColor, sortingLayerId, sortingOrder);
        CreateBox("Background", 0f, 0f, Width - 2f * Border, Height - 2f * Border, BackgroundColor, sortingLayerId, sortingOrder + 1);
        CreateBox("Divider", 0f, DividerY, Width - 2f * Margin, Border / 2f, FrameColor, sortingLayerId, sortingOrder + 2);

        TMP_Text titleText = CreateText("Title", contentLeft, TitleY, rowWidth, TitleFontSize, false, FontStyles.Bold, sortingLayerId, sortingOrder + 4);
        titleText.text = BaseController.ResourceNames[buildingIndex].ToUpper();

        levelText = CreateText("Level", Width / 2f - Margin, TitleY, rowWidth, TitleFontSize, true, FontStyles.Bold, sortingLayerId, sortingOrder + 4);

        productionRow = CreateRow("Production", ProductionLabelY, ProductionBarY, ProductionBarColor, sortingLayerId, sortingOrder);
        upgradeRow = CreateRow("Upgrade", UpgradeLabelY, UpgradeBarY, UpgradeBarColor, sortingLayerId, sortingOrder);

        CreatePreview(sortingLayerId, sortingOrder);

        root.SetActive(false);
    }

    // Az ablak az épület teteje fölött, annak közepén áll; a távolság világegységben van megadva.
    public void SetPosition(Vector2 topCenter, float gap)
    {
        float parentScaleY = root.transform.parent.lossyScale.y;
        float distance = (gap + Height * PanelScale / 2f) / parentScaleY;

        root.transform.localPosition = new Vector3(topCenter.x, topCenter.y + distance, 0f);
    }

    public void SetVisible(bool visible)
    {
        root.SetActive(visible);
    }

    public void SetLevel(int level)
    {
        SetText(levelText, $"{level}. szint");
    }

    public void SetProduction(bool active, int secondsLeft, float progress)
    {
        SetRow(productionRow, active, "Termelés", "Termelés: szabad", secondsLeft, progress);
    }

    public void SetUpgrade(bool active, int targetLevel, int secondsLeft, float progress, Sprite targetSprite)
    {
        SetRow(upgradeRow, active, $"Fejlesztés: {targetLevel}. szint", "Fejlesztés: szabad", secondsLeft, progress);
        SetPreview(active ? targetSprite : null);
    }

    private void SetRow(Row row, bool active, string activeLabel, string idleLabel, int secondsLeft, float progress)
    {
        SetText(row.label, active ? activeLabel : idleLabel);
        SetText(row.time, active ? $"{secondsLeft} mp" : "");
        row.label.color = active ? LabelColor : IdleColor;

        float fillWidth = active ? rowWidth * Mathf.Clamp01(progress) : 0f;
        row.fill.transform.localScale = new Vector3(fillWidth, BarHeight, 1f);
        row.fill.transform.localPosition = new Vector3(contentLeft + fillWidth / 2f, row.barY, 0f);
    }

    private void SetPreview(Sprite sprite)
    {
        previewRoot.SetActive(sprite != null);

        if (sprite == null || previewRenderer.sprite == sprite)
        {
            return;
        }

        float scale = PreviewSize / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);

        previewRenderer.sprite = sprite;
        previewRenderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text.text != value)
        {
            text.text = value;
        }
    }

    private Row CreateRow(string name, float labelY, float barY, Color barColor, int sortingLayerId, int order)
    {
        Row row = new Row();
        row.barY = barY;
        row.label = CreateText(name + "Label", contentLeft, labelY, rowWidth, RowFontSize, false, FontStyles.Normal, sortingLayerId, order + 4);
        row.time = CreateText(name + "Time", contentRight, labelY, rowWidth, RowFontSize, true, FontStyles.Bold, sortingLayerId, order + 4);
        row.time.color = Color.white;

        CreateBox(name + "BarBack", contentLeft + rowWidth / 2f, barY, rowWidth, BarHeight, BarBackColor, sortingLayerId, order + 2);
        row.fill = CreateBox(name + "BarFill", contentLeft, barY, 0f, BarHeight, barColor, sortingLayerId, order + 3);

        return row;
    }

    private void CreatePreview(int sortingLayerId, int order)
    {
        float x = (contentRight + Width / 2f - Border) / 2f;
        float y = (DividerY - (Height / 2f - Border)) / 2f;

        previewRoot = new GameObject("UpgradePreview");
        previewRoot.transform.SetParent(root.transform, false);
        previewRoot.transform.localPosition = new Vector3(x, y, 0f);

        CreateBox("PreviewFrame", previewRoot.transform, 0f, 0f, PreviewFrameSize, PreviewFrameSize, FrameColor, sortingLayerId, order + 2);
        CreateBox("PreviewBackground", previewRoot.transform, 0f, 0f, PreviewFrameSize - 2f * Border, PreviewFrameSize - 2f * Border, BackgroundColor, sortingLayerId, order + 3);

        GameObject sprite = new GameObject("Sprite");
        sprite.transform.SetParent(previewRoot.transform, false);

        previewRenderer = sprite.AddComponent<SpriteRenderer>();
        previewRenderer.sortingLayerID = sortingLayerId;
        previewRenderer.sortingOrder = order + 4;

        previewRoot.SetActive(false);
    }

    private SpriteRenderer CreateBox(string name, float x, float y, float width, float height, Color color, int sortingLayerId, int order)
    {
        return CreateBox(name, root.transform, x, y, width, height, color, sortingLayerId, order);
    }

    private SpriteRenderer CreateBox(string name, Transform parent, float x, float y, float width, float height, Color color, int sortingLayerId, int order)
    {
        GameObject box = new GameObject(name);
        box.transform.SetParent(parent, false);
        box.transform.localPosition = new Vector3(x, y, 0f);
        box.transform.localScale = new Vector3(width, height, 1f);

        SpriteRenderer renderer = box.AddComponent<SpriteRenderer>();
        renderer.sprite = GetWhiteSprite();
        renderer.color = color;
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = order;

        return renderer;
    }

    private TMP_Text CreateText(string name, float x, float y, float width, float fontSize, bool alignRight, FontStyles style, int sortingLayerId, int order)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(root.transform, false);
        textObject.transform.localPosition = new Vector3(x, y, 0f);

        TextMeshPro text = textObject.AddComponent<TextMeshPro>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = LabelColor;
        text.alignment = alignRight ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
        text.rectTransform.pivot = new Vector2(alignRight ? 1f : 0f, 0.5f);
        text.rectTransform.sizeDelta = new Vector2(width, TextHeight);
        text.sortingLayerID = sortingLayerId;
        text.sortingOrder = order;

        return text;
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            whiteSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        return whiteSprite;
    }
}
