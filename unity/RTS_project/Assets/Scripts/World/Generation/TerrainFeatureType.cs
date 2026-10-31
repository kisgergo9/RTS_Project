using System;

/// <summary>
/// Egy tereptárgy-típus (pl. hegy, szakadék, oázis, pálmafa) Unity-független
/// leírása. Az akadályok elzárják a mozgást, a dekorációk nem.
/// A grafikai változatok (sprite-ok) a TerrainFeatureDefinition-ben vannak;
/// a generátor csak a változat sorszámát választja ki.
/// </summary>
public sealed class TerrainFeatureType
{
    public string Id { get; }
    public bool BlocksMovement { get; }

    /// <summary>
    /// Az alapterület 1-es léptéknél.
    /// </summary>
    public Footprint Footprint { get; }

    public float MinScale { get; }
    public float MaxScale { get; }

    /// <summary>
    /// A típus relatív gyakorisága a többi azonos kategóriájú típushoz képest.
    /// </summary>
    public float Weight { get; }

    /// <summary>
    /// A grafikai változatok száma (legalább 1).
    /// </summary>
    public int VariantCount { get; }

    /// <summary>
    /// Igaz, ha a dekorációk egy része e tereptárgy köré csoportosul
    /// (pl. pálmaliget az oázis körül).
    /// </summary>
    public bool AttractsDecorations { get; }

    /// <summary>
    /// A legnagyobb léptékű példány középponttól legtávolabbi pontja.
    /// </summary>
    public float MaxBoundingRadius => Footprint.BoundingRadius * MaxScale;

    public TerrainFeatureType(
        string id,
        bool blocksMovement,
        Footprint footprint,
        float minScale,
        float maxScale,
        float weight,
        int variantCount,
        bool attractsDecorations = false)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A tereptárgy-típus azonosítója nem lehet üres.", nameof(id));

        if (minScale <= 0f)
            throw new ArgumentOutOfRangeException(nameof(minScale),
                $"A legkisebb léptéknek pozitívnak kell lennie ({id}).");

        if (maxScale < minScale)
            throw new ArgumentOutOfRangeException(nameof(maxScale),
                $"A legnagyobb lépték nem lehet kisebb a legkisebbnél ({id}).");

        if (weight <= 0f)
            throw new ArgumentOutOfRangeException(nameof(weight),
                $"A gyakoriság súlyának pozitívnak kell lennie ({id}).");

        if (variantCount < 1)
            throw new ArgumentOutOfRangeException(nameof(variantCount),
                $"Legalább egy grafikai változat szükséges ({id}).");

        Id = id.Trim();
        BlocksMovement = blocksMovement;
        Footprint = footprint ?? throw new ArgumentNullException(nameof(footprint));
        MinScale = minScale;
        MaxScale = maxScale;
        Weight = weight;
        VariantCount = variantCount;
        AttractsDecorations = attractsDecorations;
    }
}
