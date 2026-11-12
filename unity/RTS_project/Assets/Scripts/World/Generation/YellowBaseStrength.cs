using System;
using System.Collections.Generic;
using UnityEngine;

// A sárga bázisok erőssége (1–10): a kezdőbázisokhoz közeliek gyengébbek.
public static class YellowBaseStrength
{
    public const int Min = 1;
    public const int Max = 10;

    // Az erősségek a sárga bázisok sorrendjében.
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

    // A sorrend adott helyéhez tartozó erősség (első: 1, utolsó: 10).
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
