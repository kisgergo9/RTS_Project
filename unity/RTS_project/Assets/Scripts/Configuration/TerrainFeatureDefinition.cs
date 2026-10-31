using System;
using UnityEngine;

/// <summary>
/// Egy tereptárgy-típus (akadály vagy dekoráció) szerkeszthető leírása.
/// A grafikai változatok itt vannak; a generátor a ToType() által készített,
/// Unity-független TerrainFeatureType-pal dolgozik, és csak a változat
/// sorszámát választja ki.
/// </summary>
[CreateAssetMenu(
    fileName = "NewTerrainFeature",
    menuName = "Game/Configuration/Terrain Feature Definition")]
public sealed class TerrainFeatureDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    private string id;

    [SerializeField]
    private string displayName;

    [Header("Behaviour")]
    [Tooltip("Akadály: elzárja a mozgást. Dekoráció: nem befolyásolja a járhatóságot.")]
    [SerializeField]
    private bool blocksMovement = true;

    [Header("Visuals")]
    [Tooltip("A grafikai változatok; a generátor ezek közül választ.")]
    [SerializeField]
    private Sprite[] sprites = Array.Empty<Sprite>();

    [SerializeField]
    private int sortingOrder;

    [Header("Footprint (scale 1)")]
    [Tooltip("Ellipszis alakú alapterület fél-tengelyei. Csak akkor használt, ha a sokszög üres.")]
    [SerializeField]
    private Vector2 footprintHalfSize = new Vector2(1f, 1f);

    [Tooltip("Az alapterület sokszöge a középponthoz viszonyítva. Ha legalább 3 pontja van, " +
             "ez felülírja az ellipszist.")]
    [SerializeField]
    private Vector2[] footprintPolygon = Array.Empty<Vector2>();

    [Header("Variation")]
    [SerializeField, Min(0.01f)]
    private float minScale = 1f;

    [SerializeField, Min(0.01f)]
    private float maxScale = 1f;

    [Tooltip("Relatív gyakoriság az azonos kategóriájú típusok között.")]
    [SerializeField, Min(0.001f)]
    private float weight = 1f;

    [Tooltip("Akadálynál: a dekorációk egy része e tárgy köré csoportosul (pl. pálmaliget az oázis körül).")]
    [SerializeField]
    private bool attractsDecorations;

    public string Id => id;
    public string DisplayName => displayName;
    public bool BlocksMovement => blocksMovement;
    public int SortingOrder => sortingOrder;
    public int VariantCount => sprites != null ? sprites.Length : 0;

    public Sprite GetSprite(int variant)
    {
        if (sprites == null || variant < 0 || variant >= sprites.Length)
            throw new ArgumentOutOfRangeException(nameof(variant),
                $"Nincs {variant}. grafikai változat a(z) {id} tereptárgyhoz.");

        return sprites[variant];
    }

    public TerrainFeatureType ToType()
    {
        Footprint footprint = footprintPolygon != null && footprintPolygon.Length >= 3
            ? new Footprint(footprintPolygon)
            : Footprint.Ellipse(footprintHalfSize.x, footprintHalfSize.y);

        return new TerrainFeatureType(
            id,
            blocksMovement,
            footprint,
            minScale,
            maxScale,
            weight,
            VariantCount,
            attractsDecorations);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        id = id?.Trim();
        displayName = displayName?.Trim();

        if (maxScale < minScale)
            maxScale = minScale;
    }
#endif
}
