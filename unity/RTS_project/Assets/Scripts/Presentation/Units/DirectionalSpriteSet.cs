
using System;
using UnityEngine;

public sealed class DirectionalSpriteSet
{
    private readonly Sprite[] _sprites;

    public int ViewCount => _sprites.Length;

    public float AnglePerView => 360f / ViewCount;

    public DirectionalSpriteSet(Sprite[] sprites)
    {
        if (sprites == null)
            throw new ArgumentNullException(nameof(sprites));

        if (sprites.Length == 0)
            throw new ArgumentException(
                "Legalább egy sprite szükséges.",
                nameof(sprites));

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null)
            {
                throw new ArgumentException(
                    $"A sprite tömb {i}. eleme null.",
                    nameof(sprites));
            }
        }

        _sprites = (Sprite[])sprites.Clone();
    }


    /// <summary>
    /// 0 fok = felfelé, 90 fok = jobbra,
    /// 180 fok = lefelé, 270 fok = balra.
    /// A legközelebbi nézetet adja vissza.
    /// A sprite-ok sorrendje 0 foktól indulva,
    /// növekvõ, óramutató járásával megegyezõ irányú.
    /// </summary>
    public Sprite GetSpriteForAngle(float worldAngle)
    {
        int index = Mathf.RoundToInt(worldAngle / AnglePerView) % ViewCount;

        if (index < 0)
            index += ViewCount;

        return _sprites[index];
    }

    public Sprite GetSpriteAtIndex(int index)
    {
        if (index < 0 || index >= ViewCount)
            throw new ArgumentOutOfRangeException(nameof(index));

        return _sprites[index];
    }
}
