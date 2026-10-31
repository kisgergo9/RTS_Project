using System;

/// <summary>
/// Zárt egész tartomány: [Min, Max].
/// </summary>
public readonly struct IntRange : IEquatable<IntRange>
{
    public int Min { get; }
    public int Max { get; }

    public IntRange(int min, int max)
    {
        if (max < min)
            throw new ArgumentOutOfRangeException(nameof(max),
                $"A tartomány maximuma ({max}) nem lehet kisebb a minimumánál ({min}).");

        Min = min;
        Max = max;
    }

    public bool Contains(int value)
    {
        return value >= Min && value <= Max;
    }

    public bool Equals(IntRange other)
    {
        return Min == other.Min && Max == other.Max;
    }

    public override bool Equals(object obj)
    {
        return obj is IntRange other && Equals(other);
    }

    public override int GetHashCode()
    {
        return (Min * 397) ^ Max;
    }

    public override string ToString()
    {
        return $"[{Min}; {Max}]";
    }
}
