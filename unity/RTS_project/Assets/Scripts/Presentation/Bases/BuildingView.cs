using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class BuildingView : MonoBehaviour
{
    [SerializeField] private Sprite[] levelSprites;

    private SpriteRenderer spriteRenderer;
    private int shownLevel = -1;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
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
    }
}
