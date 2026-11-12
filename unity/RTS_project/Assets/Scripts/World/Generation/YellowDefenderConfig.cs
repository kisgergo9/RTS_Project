using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Egy sárga védőcsapatban szerepelhető egységtípus és nehézségi értéke.
/// </summary>
public sealed class DefenderUnitType
{
    public string Id { get; }

    /// <summary>
    /// Az egység nehézségi értéke: ennyit „fogyaszt” a csapat nehézségi keretéből.
    /// </summary>
    public int DifficultyCost { get; }

    public DefenderUnitType(string id, int difficultyCost)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A védőegység-típus azonosítója nem lehet üres.", nameof(id));

        if (difficultyCost <= 0)
            throw new ArgumentOutOfRangeException(nameof(difficultyCost),
                $"A nehézségi értéknek pozitívnak kell lennie ({id}).");

        Id = id.Trim();
        DifficultyCost = difficultyCost;
    }
}

/// <summary>
/// A sárga bázisok előtti védőcsapatok Unity-független konfigurációja.
/// A WorldGenerationConfig opcionális része: ha nincs megadva, nem
/// generálódnak védőcsapatok.
///
/// Minden védett sárga bázis az erősségétől függő pontkeretet kap: 1-es
/// erősségnél MinBasePoints, 10-esnél MaxBasePoints, közte egyenletesen. A
/// keret véletlenszerűen oszlik meg legfeljebb annyi csapat között, ahány
/// csapathely (SquadAnchors) van; minden csapat legalább MinSquadPoints
/// pontot kap. Egy csapat egységeinek pontértékei összesen nem lépik túl a
/// csapat pontját. Egy csapat a játékban egy UnitGroup: a vezetője a
/// csapathelyen áll, a többiek az alakzat szerint köré.
/// </summary>
public sealed class YellowDefenderConfig
{
    private readonly DefenderUnitType[] _unitTypes;
    private readonly Vector2[] _squadAnchors;

    public IReadOnlyList<DefenderUnitType> UnitTypes => _unitTypes;

    /// <summary>
    /// Az 1-es, illetve a 10-es erősségű sárga bázis összes védőpontja.
    /// </summary>
    public int MinBasePoints { get; }
    public int MaxBasePoints { get; }

    /// <summary>
    /// Egy csapat legkisebb pontja (legalább a legolcsóbb egység ára, így
    /// nincs üres csapat).
    /// </summary>
    public int MinSquadPoints { get; }

    /// <summary>
    /// A sárga bázisok ekkora hányada kap védőcsapatokat. [0; 1]
    /// </summary>
    public float DefendedBaseFraction { get; }

    /// <summary>
    /// A csapatok helye (a csapat vezetőjéé) a bázis középpontjához
    /// viszonyítva. Egy bázisnak legfeljebb ennyi csapata van.
    /// </summary>
    public IReadOnlyList<Vector2> SquadAnchors => _squadAnchors;

    public int CheapestCost
    {
        get
        {
            int cheapest = int.MaxValue;

            foreach (DefenderUnitType type in _unitTypes)
                cheapest = Math.Min(cheapest, type.DifficultyCost);

            return cheapest;
        }
    }

    public YellowDefenderConfig(
        IEnumerable<DefenderUnitType> unitTypes,
        int minBasePoints,
        int maxBasePoints,
        int minSquadPoints,
        float defendedBaseFraction,
        IEnumerable<Vector2> squadAnchors)
    {
        if (unitTypes == null)
            throw new ArgumentNullException(nameof(unitTypes));

        if (squadAnchors == null)
            throw new ArgumentNullException(nameof(squadAnchors));

        _unitTypes = new List<DefenderUnitType>(unitTypes).ToArray();
        _squadAnchors = new List<Vector2>(squadAnchors).ToArray();

        if (_unitTypes.Length == 0)
            throw new ArgumentException("Legalább egy védőegység-típus szükséges.", nameof(unitTypes));

        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (DefenderUnitType type in _unitTypes)
        {
            if (type == null)
                throw new ArgumentException("A védőegység-típusok között null elem van.", nameof(unitTypes));

            if (!ids.Add(type.Id))
                throw new ArgumentException($"Duplikált védőegység-azonosító: {type.Id}", nameof(unitTypes));
        }

        if (_squadAnchors.Length == 0)
            throw new ArgumentException("Legalább egy csapathely szükséges.", nameof(squadAnchors));

        if (minSquadPoints <= 0)
            throw new ArgumentOutOfRangeException(nameof(minSquadPoints),
                "A csapat pontjának pozitívnak kell lennie.");

        if (minBasePoints < minSquadPoints)
            throw new ArgumentOutOfRangeException(nameof(minBasePoints),
                "A bázis legkisebb pontja nem lehet kisebb egy csapat legkisebb pontjánál.");

        if (maxBasePoints < minBasePoints)
            throw new ArgumentOutOfRangeException(nameof(maxBasePoints),
                "A bázis legnagyobb pontja nem lehet kisebb a legkisebbnél.");

        if (defendedBaseFraction < 0f || defendedBaseFraction > 1f)
            throw new ArgumentOutOfRangeException(nameof(defendedBaseFraction),
                "A védett bázisok aránya a [0; 1] tartományba kell essen.");

        MinBasePoints = minBasePoints;
        MaxBasePoints = maxBasePoints;
        MinSquadPoints = minSquadPoints;
        DefendedBaseFraction = defendedBaseFraction;

        if (CheapestCost > MinSquadPoints)
            throw new ArgumentException(
                $"A legolcsóbb védőegység ({CheapestCost}) drágább egy csapat legkisebb pontjánál " +
                $"({MinSquadPoints}), így lehetne üres csapat.");
    }

    /// <summary>
    /// Egy adott erősségű sárga bázis összes védőpontja: 1-es erősségnél
    /// MinBasePoints, 10-esnél MaxBasePoints, közte egyenletesen. Ismeretlen
    /// (0) erősségnél a legkisebb érték.
    /// </summary>
    public int GetBasePoints(int strength)
    {
        int clamped = Math.Max(YellowBaseStrength.Min, Math.Min(strength, YellowBaseStrength.Max));
        double progress = (clamped - YellowBaseStrength.Min) /
                          (double)(YellowBaseStrength.Max - YellowBaseStrength.Min);

        return MinBasePoints + (int)Math.Round(
            (MaxBasePoints - MinBasePoints) * progress,
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Ennyi pontból hány csapat lesz: ahány csapathely van, de mindegyik
    /// csapatnak legalább MinSquadPoints pont jut.
    /// </summary>
    public int GetSquadCount(int basePoints)
    {
        return Math.Min(_squadAnchors.Length, basePoints / MinSquadPoints);
    }
}
