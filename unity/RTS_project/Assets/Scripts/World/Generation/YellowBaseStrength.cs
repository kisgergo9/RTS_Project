using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A sárga bázisok erősségének (1–10) kiosztása.
///
/// Az erősségek egyenletesen oszlanak el: a leggyengébb bázis 1-es, a
/// legerősebb 10-es, a többi arányosan közte (10-nél kevesebb vagy több sárga
/// bázis esetén is). Hogy a játék elején ne legyen erős bázis közvetlenül egy
/// játékos mellett, a sorrendet a legközelebbi kezdőbázistól mért távolság
/// adja: a közeliek gyengébbek, a távoliak erősebbek. A sorrendet véletlen
/// zaj keveri (Jitter: a sorrendben elfoglalt relatív hely legfeljebb ennyivel
/// tolódhat el), így a kiosztás nem teljesen kiszámítható.
///
/// Garancia: 0,2-es zaj mellett a kezdőbázisokhoz legközelebbi sárga bázis
/// erőssége legfeljebb 5.
/// </summary>
public static class YellowBaseStrength
{
    public const int Min = 1;
    public const int Max = 10;

    /// <summary>
    /// Az erősségek a sárga bázisok sorrendjében. A véletlenszámok sorrendje
    /// rögzített, így ugyanabból a bemenetből mindig ugyanaz az eredmény.
    /// </summary>
    public static int[] Assign(
        IReadOnlyList<Vector2> yellowPositions,
        IReadOnlyList<Vector2> playerPositions,
        float jitter,
        DeterministicRandom random)
    {
        if (yellowPositions == null)
            throw new ArgumentNullException(nameof(yellowPositions));

        if (playerPositions == null)
            throw new ArgumentNullException(nameof(playerPositions));

        if (random == null)
            throw new ArgumentNullException(nameof(random));

        if (jitter < 0f || jitter > 1f)
            throw new ArgumentOutOfRangeException(nameof(jitter), "A zajnak a [0; 1] tartományba kell esnie.");

        int count = yellowPositions.Count;
        int[] strengths = new int[count];

        if (count == 0)
            return strengths;

        double[] distances = new double[count];

        for (int i = 0; i < count; i++)
            distances[i] = DistanceToNearest(yellowPositions[i], playerPositions);

        int[] byDistance = SortedIndices(count, (a, b) => distances[a].CompareTo(distances[b]));
        double[] keys = new double[count];

        for (int rank = 0; rank < count; rank++)
            keys[byDistance[rank]] = RelativeRank(rank, count);

        for (int i = 0; i < count; i++)
            keys[i] += (random.NextDouble() * 2.0 - 1.0) * jitter;

        int[] byKey = SortedIndices(count, (a, b) => keys[a].CompareTo(keys[b]));

        for (int position = 0; position < count; position++)
            strengths[byKey[position]] = StrengthAt(position, count);

        return strengths;
    }

    /// <summary>
    /// A sorrend adott helyéhez tartozó erősség: az első 1-es, az utolsó
    /// 10-es, a többi egyenletesen közte. Egyetlen sárga bázis 1-es.
    /// </summary>
    public static int StrengthAt(int position, int count)
    {
        if (count <= 1)
            return Min;

        return Min + (int)Math.Round(RelativeRank(position, count) * (Max - Min), MidpointRounding.AwayFromZero);
    }

    private static double RelativeRank(int rank, int count)
    {
        return count <= 1 ? 0.0 : rank / (double)(count - 1);
    }

    private static double DistanceToNearest(Vector2 position, IReadOnlyList<Vector2> others)
    {
        double best = double.MaxValue;

        foreach (Vector2 other in others)
            best = Math.Min(best, Vector2.Distance(position, other));

        return others.Count == 0 ? 0.0 : best;
    }

    // Stabil rendezés: azonos érték esetén az eredeti sorrend dönt.
    private static int[] SortedIndices(int count, Comparison<int> compare)
    {
        int[] indices = new int[count];

        for (int i = 0; i < count; i++)
            indices[i] = i;

        Array.Sort(indices, (a, b) =>
        {
            int result = compare(a, b);
            return result != 0 ? result : a.CompareTo(b);
        });

        return indices;
    }
}
