using System;
using NUnit.Framework;

public sealed class DeterministicRandomTests
{
    [Test]
    public void SameSeed_ProducesSameSequence()
    {
        DeterministicRandom a = new DeterministicRandom(987654);
        DeterministicRandom b = new DeterministicRandom(987654);

        for (int i = 0; i < 1000; i++)
            Assert.AreEqual(a.NextUInt(), b.NextUInt(), $"index={i}");
    }

    [Test]
    public void DifferentSeeds_ProduceDifferentSequences()
    {
        DeterministicRandom a = new DeterministicRandom(1);
        DeterministicRandom b = new DeterministicRandom(2);

        int equal = 0;

        for (int i = 0; i < 100; i++)
        {
            if (a.NextUInt() == b.NextUInt())
                equal++;
        }

        Assert.Less(equal, 3);
    }

    /// <summary>
    /// Regressziós teszt: ha ez elbukik, a mentett seedekből már más világ
    /// generálódna. Az algoritmus módosítása csak tudatosan, a generátor
    /// verziószámának növelésével együtt történhet.
    /// </summary>
    [Test]
    public void KnownSeed_ProducesKnownSequence()
    {
        DeterministicRandom random = new DeterministicRandom(12345);

        uint[] expected = { 1825437329u, 1832904605u, 993331261u, 1740132745u, 3315639865u };

        foreach (uint value in expected)
            Assert.AreEqual(value, random.NextUInt());
    }

    [Test]
    public void KnownSeed_ProducesKnownBoundedIntegers()
    {
        DeterministicRandom random = new DeterministicRandom(12345);

        int[] expected = { 29, 5, 61, 45, 65 };

        foreach (int value in expected)
            Assert.AreEqual(value, random.NextInt(100));
    }

    [Test]
    public void Seed_IsPreserved()
    {
        Assert.AreEqual(-42, new DeterministicRandom(-42).Seed);
    }

    [Test]
    public void NextInt_StaysInRangeAndCoversIt()
    {
        DeterministicRandom random = new DeterministicRandom(7);
        bool[] seen = new bool[10];

        for (int i = 0; i < 10000; i++)
        {
            int value = random.NextInt(10);

            Assert.That(value, Is.InRange(0, 9));
            seen[value] = true;
        }

        Assert.That(seen, Has.All.True);
    }

    [Test]
    public void NextInt_WithMinimum_StaysInRange()
    {
        DeterministicRandom random = new DeterministicRandom(7);

        for (int i = 0; i < 10000; i++)
            Assert.That(random.NextInt(-5, 5), Is.InRange(-5, 4));
    }

    [Test]
    public void NextInt_IsRoughlyUniform()
    {
        DeterministicRandom random = new DeterministicRandom(99);
        int[] counts = new int[4];
        const int samples = 40000;

        for (int i = 0; i < samples; i++)
            counts[random.NextInt(4)]++;

        foreach (int count in counts)
            Assert.That(count, Is.InRange(samples / 4 - 500, samples / 4 + 500));
    }

    [Test]
    public void NextDouble_StaysInUnitInterval()
    {
        DeterministicRandom random = new DeterministicRandom(3);

        for (int i = 0; i < 10000; i++)
        {
            double value = random.NextDouble();

            Assert.GreaterOrEqual(value, 0.0);
            Assert.Less(value, 1.0);
        }
    }

    [Test]
    public void NextFloat_StaysInRange()
    {
        DeterministicRandom random = new DeterministicRandom(3);

        for (int i = 0; i < 10000; i++)
        {
            float value = random.NextFloat(-2.5f, 7.5f);

            Assert.GreaterOrEqual(value, -2.5f);
            Assert.Less(value, 7.5f);
        }
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void NextInt_NonPositiveBound_Throws(int bound)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeterministicRandom(1).NextInt(bound));
    }

    [Test]
    public void NextInt_EmptyRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeterministicRandom(1).NextInt(5, 5));
    }
}
