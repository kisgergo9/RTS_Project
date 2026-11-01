using System;
using NUnit.Framework;
using UnityEngine;

public sealed class WorldBoundsTests
{
    [Test]
    public void Bounds_AreCenteredOnOrigin()
    {
        WorldBounds bounds = new WorldBounds(200f);

        Assert.AreEqual(new Vector2(-100f, -100f), bounds.Min);
        Assert.AreEqual(new Vector2(100f, 100f), bounds.Max);
        Assert.AreEqual(Vector2.zero, bounds.Center);
        Assert.AreEqual(100f, bounds.HalfSize);
    }

    [TestCase(0f)]
    [TestCase(-10f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void Constructor_RejectsInvalidSize(float size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldBounds(size));
    }

    [Test]
    public void Contains_IncludesEdgeAndExcludesOutside()
    {
        WorldBounds bounds = new WorldBounds(100f);

        Assert.IsTrue(bounds.Contains(Vector2.zero));
        Assert.IsTrue(bounds.Contains(new Vector2(50f, -50f)));
        Assert.IsFalse(bounds.Contains(new Vector2(50.01f, 0f)));
        Assert.IsFalse(bounds.Contains(new Vector2(0f, -50.01f)));
    }

    [Test]
    public void Contains_WithMargin_RequiresDistanceFromEdge()
    {
        WorldBounds bounds = new WorldBounds(100f);

        Assert.IsTrue(bounds.Contains(new Vector2(40f, 40f), 10f));
        Assert.IsFalse(bounds.Contains(new Vector2(40.5f, 0f), 10f));
    }

    [Test]
    public void Contains_WithMarginLargerThanHalfSize_IsAlwaysFalse()
    {
        WorldBounds bounds = new WorldBounds(10f);

        Assert.IsFalse(bounds.Contains(Vector2.zero, 6f));
    }

    [Test]
    public void Clamp_MovesOutsidePointOntoEdge()
    {
        WorldBounds bounds = new WorldBounds(100f);

        Assert.AreEqual(new Vector2(50f, -50f), bounds.Clamp(new Vector2(80f, -200f)));
        Assert.AreEqual(new Vector2(10f, 20f), bounds.Clamp(new Vector2(10f, 20f)));
    }

    [Test]
    public void Clamp_WithMargin_KeepsDistanceFromEdge()
    {
        WorldBounds bounds = new WorldBounds(100f);

        Assert.AreEqual(new Vector2(40f, -40f), bounds.Clamp(new Vector2(80f, -200f), 10f));
    }

    [Test]
    public void Clamp_WithMarginLargerThanHalfSize_ReturnsCenter()
    {
        WorldBounds bounds = new WorldBounds(10f);

        Assert.AreEqual(Vector2.zero, bounds.Clamp(new Vector2(3f, -4f), 8f));
    }
}
