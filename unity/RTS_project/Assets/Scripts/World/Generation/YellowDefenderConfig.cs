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
/// Minden védett sárga bázis véletlen nehézségi keretet kap a
/// [MinSquadDifficulty; MaxSquadDifficulty] tartományból; a csapat
/// egységeinek nehézségi értékei összesen ezt nem léphetik túl. Az egységek
/// a bázishoz viszonyított, rögzített helyekre (Slots) kerülnek.
/// </summary>
public sealed class YellowDefenderConfig
{
    private readonly DefenderUnitType[] _unitTypes;
    private readonly Vector2[] _slots;

    public IReadOnlyList<DefenderUnitType> UnitTypes => _unitTypes;
    public int MinSquadDifficulty { get; }
    public int MaxSquadDifficulty { get; }

    /// <summary>
    /// A sárga bázisok ekkora hányada kap védőcsapatot. [0; 1]
    /// </summary>
    public float DefendedBaseFraction { get; }

    /// <summary>
    /// A védőegységek rögzített helyei a bázis középpontjához viszonyítva.
    /// Egy csapat legfeljebb ennyi egységből áll.
    /// </summary>
    public IReadOnlyList<Vector2> Slots => _slots;

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
        int minSquadDifficulty,
        int maxSquadDifficulty,
        float defendedBaseFraction,
        IEnumerable<Vector2> slots)
    {
        if (unitTypes == null)
            throw new ArgumentNullException(nameof(unitTypes));

        if (slots == null)
            throw new ArgumentNullException(nameof(slots));

        _unitTypes = new List<DefenderUnitType>(unitTypes).ToArray();
        _slots = new List<Vector2>(slots).ToArray();

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

        if (_slots.Length == 0)
            throw new ArgumentException("Legalább egy védőhely szükséges.", nameof(slots));

        if (minSquadDifficulty <= 0)
            throw new ArgumentOutOfRangeException(nameof(minSquadDifficulty),
                "A csapat nehézségének pozitívnak kell lennie.");

        if (maxSquadDifficulty < minSquadDifficulty)
            throw new ArgumentOutOfRangeException(nameof(maxSquadDifficulty),
                "A legnagyobb nehézség nem lehet kisebb a legkisebbnél.");

        if (defendedBaseFraction < 0f || defendedBaseFraction > 1f)
            throw new ArgumentOutOfRangeException(nameof(defendedBaseFraction),
                "A védett bázisok aránya a [0; 1] tartományba kell essen.");

        MinSquadDifficulty = minSquadDifficulty;
        MaxSquadDifficulty = maxSquadDifficulty;
        DefendedBaseFraction = defendedBaseFraction;

        if (CheapestCost > MinSquadDifficulty)
            throw new ArgumentException(
                $"A legolcsóbb védőegység ({CheapestCost}) drágább a legkisebb csapatnehézségnél " +
                $"({MinSquadDifficulty}), így lehetne üres csapat.");
    }
}
