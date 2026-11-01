using System;

/// <summary>
/// Seed-alapú, determinisztikus véletlenszám-generátor (PCG32, XSH-RR változat).
/// Ugyanabból a seedből minden gépen és .NET-verzión ugyanazt a sorozatot adja,
/// ezért a System.Random és a UnityEngine.Random helyett ezt kell használni
/// mindenhol, ahol a mentett seedből reprodukálható eredmény szükséges.
///
/// FIGYELEM: az algoritmus vagy a származtatott metódusok megváltoztatása
/// a korábban mentett seedekből más világot generál. Ilyenkor a
/// generátor verziószámát is növelni kell.
/// </summary>
public sealed class DeterministicRandom
{
    private const ulong Multiplier = 6364136223846793005UL;

    private ulong _state;
    private readonly ulong _increment;

    public int Seed { get; }

    public DeterministicRandom(int seed)
        : this(seed, CreateMixer(seed))
    {
    }

    private DeterministicRandom(int seed, ulong mixer)
        : this(seed, SplitMix64(ref mixer), SplitMix64(ref mixer))
    {
    }

    /// <summary>
    /// A PCG32 referencia-inicializálása (pcg32_srandom_r).
    /// </summary>
    private DeterministicRandom(int seed, ulong initialState, ulong sequence)
    {
        Seed = seed;

        _increment = (sequence << 1) | 1UL;
        _state = 0UL;
        NextUInt();
        _state = unchecked(_state + initialState);
        NextUInt();
    }

    /// <summary>
    /// Egyenletes eloszlású 32 bites előjel nélküli egész.
    /// </summary>
    public uint NextUInt()
    {
        unchecked
        {
            ulong oldState = _state;
            _state = oldState * Multiplier + _increment;

            uint xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
            int rotation = (int)(oldState >> 59);

            return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
        }
    }

    /// <summary>
    /// Egyenletes eloszlású egész a [0; maxExclusive) tartományban, torzítás nélkül.
    /// </summary>
    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive),
                "A felső határnak pozitívnak kell lennie.");

        uint bound = (uint)maxExclusive;
        uint threshold = unchecked(0u - bound) % bound;

        while (true)
        {
            uint value = NextUInt();

            if (value >= threshold)
                return (int)(value % bound);
        }
    }

    /// <summary>
    /// Egyenletes eloszlású egész a [minInclusive; maxExclusive) tartományban.
    /// </summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive),
                "A felső határnak nagyobbnak kell lennie az alsónál.");

        long range = (long)maxExclusive - minInclusive;

        if (range > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive),
                "A tartomány túl nagy.");

        return minInclusive + NextInt((int)range);
    }

    /// <summary>
    /// Egyenletes eloszlású valós szám a [0; 1) tartományban, 53 bit pontossággal.
    /// </summary>
    public double NextDouble()
    {
        ulong high = NextUInt() >> 5;
        ulong low = NextUInt() >> 6;

        return (high * 67108864.0 + low) * (1.0 / 9007199254740992.0);
    }

    /// <summary>
    /// Egyenletes eloszlású valós szám a [minInclusive; maxExclusive) tartományban.
    /// </summary>
    public float NextFloat(float minInclusive, float maxExclusive)
    {
        if (maxExclusive < minInclusive)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive),
                "A felső határ nem lehet kisebb az alsónál.");

        float value = (float)(minInclusive + (maxExclusive - (double)minInclusive) * NextDouble());

        // A float kerekítés miatt az eredmény elérhetné a felső határt.
        return value < maxExclusive ? value : minInclusive;
    }

    /// <summary>
    /// Új, véletlenszerű seed a host számára, ha a lobbyban nincs megadva.
    /// Nem determinisztikus; csak a generálás kezdetén használandó.
    /// </summary>
    public static int CreateRandomSeed()
    {
        return Guid.NewGuid().GetHashCode();
    }

    // A seed bitjeit SplitMix64-gyel szórjuk szét, hogy a közeli
    // seedek (pl. 1 és 2) se adjanak hasonló kezdő sorozatot.
    private static ulong CreateMixer(int seed)
    {
        return unchecked((ulong)(uint)seed);
    }

    private static ulong SplitMix64(ref ulong state)
    {
        unchecked
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
