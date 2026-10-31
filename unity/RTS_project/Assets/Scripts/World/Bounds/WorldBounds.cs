using System;
using UnityEngine;

/// <summary>
/// A négyzetes játékvilág kemény határa. A világ középpontja az origó,
/// oldalhossza Size, így a határ a [-Size/2; Size/2] tartomány mindkét tengelyen.
/// A pálya széle kemény határ: sem az egységek, sem a kamera nem hagyhatják el.
/// </summary>
public readonly struct WorldBounds : IEquatable<WorldBounds>
{
    public float Size { get; }
    public float HalfSize => Size * 0.5f;
    public Vector2 Center => Vector2.zero;
    public Vector2 Min => new Vector2(-HalfSize, -HalfSize);
    public Vector2 Max => new Vector2(HalfSize, HalfSize);

    public WorldBounds(float size)
    {
        if (!(size > 0f) || float.IsInfinity(size))
            throw new ArgumentOutOfRangeException(nameof(size),
                $"A világ méretének pozitív, véges számnak kell lennie: {size}");

        Size = size;
    }

    /// <summary>
    /// Igaz, ha a pont a határon belül van, és minden oldaltól legalább
    /// margin távolságra esik. A határon lévő pont még a világhoz tartozik.
    /// </summary>
    public bool Contains(Vector2 point, float margin = 0f)
    {
        float limit = HalfSize - margin;

        return limit >= 0f &&
               point.x >= -limit && point.x <= limit &&
               point.y >= -limit && point.y <= limit;
    }

    /// <summary>
    /// A pontot a határon belülre, minden oldaltól legalább margin távolságra
    /// helyezi. Ha a margin nagyobb a világ felénél, a középpontot adja.
    /// </summary>
    public Vector2 Clamp(Vector2 point, float margin = 0f)
    {
        float limit = Mathf.Max(0f, HalfSize - margin);

        return new Vector2(
            Mathf.Clamp(point.x, -limit, limit),
            Mathf.Clamp(point.y, -limit, limit));
    }

    public bool Equals(WorldBounds other)
    {
        return Size.Equals(other.Size);
    }

    public override bool Equals(object obj)
    {
        return obj is WorldBounds other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Size.GetHashCode();
    }

    public override string ToString()
    {
        return $"WorldBounds(Size={Size}, Min={Min}, Max={Max})";
    }
}
