using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;

public sealed class WalkabilityGridTests
{
    private static WalkabilityGrid Grid(float size = 20f, float cellSize = 1f)
    {
        return new WalkabilityGrid(new WorldBounds(size), cellSize);
    }

    /// <summary>
    /// Függőleges fal a megadott x oszlopban, opcionális réssel.
    /// </summary>
    private static void BuildWall(WalkabilityGrid grid, int column, int gapFrom = -1, int gapTo = -1)
    {
        for (int y = 0; y < grid.Height; y++)
        {
            if (y >= gapFrom && y <= gapTo)
                continue;

            grid.Block(column, y);
        }
    }

    // Méretek és koordináták

    [TestCase(20f, 1f, 20)]
    [TestCase(20f, 0.5f, 40)]
    [TestCase(530f, 1f, 530)]
    [TestCase(21f, 2f, 11)]
    public void Dimensions_CoverWholeWorld(float size, float cellSize, int expected)
    {
        WalkabilityGrid grid = Grid(size, cellSize);

        Assert.AreEqual(expected, grid.Width);
        Assert.AreEqual(expected, grid.Height);
    }

    [Test]
    public void Constructor_RejectsInvalidCellSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Grid(20f, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Grid(20f, 30f));
    }

    [Test]
    public void WorldToCell_AndBack_RoundTrips()
    {
        WalkabilityGrid grid = Grid(20f, 1f);

        Assert.AreEqual(new Vector2Int(0, 0), grid.WorldToCell(new Vector2(-10f, -10f)));
        Assert.AreEqual(new Vector2Int(10, 10), grid.WorldToCell(Vector2.zero));
        Assert.AreEqual(new Vector2(-9.5f, -9.5f), grid.CellToWorld(0, 0));
        Assert.AreEqual(new Vector2Int(13, 4), grid.WorldToCell(grid.CellToWorld(13, 4)));
    }

    [Test]
    public void EmptyGrid_IsFullyWalkableSingleRegion()
    {
        WalkabilityGrid grid = Grid();

        Assert.AreEqual(0, grid.CountBlocked());
        Assert.AreEqual(1, grid.RegionCount);
        Assert.IsTrue(grid.IsWalkable(Vector2.zero));
    }

    [Test]
    public void OutsideCells_AreNotWalkable()
    {
        WalkabilityGrid grid = Grid();

        Assert.IsFalse(grid.IsWalkable(-1, 0));
        Assert.IsFalse(grid.IsWalkable(0, grid.Height));
        Assert.IsFalse(grid.IsWalkable(new Vector2(50f, 0f)));
        Assert.AreEqual(WalkabilityGrid.NoRegion, grid.GetRegion(-1, -1));
    }

    // Alapterületek

    [Test]
    public void BlockFootprint_BlocksInsideOnly()
    {
        WalkabilityGrid grid = Grid(40f);
        grid.BlockFootprint(Footprint.Ellipse(5f, 2f), Vector2.zero);

        Assert.IsFalse(grid.IsWalkable(Vector2.zero));
        Assert.IsFalse(grid.IsWalkable(new Vector2(4f, 0.5f)));
        Assert.IsTrue(grid.IsWalkable(new Vector2(0f, 3f)));
        Assert.IsTrue(grid.IsWalkable(new Vector2(6f, 0f)));
    }

    [Test]
    public void BlockFootprint_RespectsPositionAndScale()
    {
        WalkabilityGrid grid = Grid(40f);
        grid.BlockFootprint(Footprint.Ellipse(2f, 2f), new Vector2(10f, 5f), 2f);

        Assert.IsFalse(grid.IsWalkable(new Vector2(13.5f, 5.5f)));
        Assert.IsTrue(grid.IsWalkable(new Vector2(15.5f, 5.5f)));
        Assert.IsTrue(grid.IsWalkable(Vector2.zero));
    }

    [Test]
    public void BlockFootprint_InflationExtendsBlockedArea()
    {
        WalkabilityGrid plain = Grid(40f);
        WalkabilityGrid inflated = Grid(40f);

        plain.BlockFootprint(Footprint.Ellipse(4f, 1f), Vector2.zero);
        inflated.BlockFootprint(Footprint.Ellipse(4f, 1f), Vector2.zero, 1f, 2f);

        Assert.IsTrue(plain.IsWalkable(new Vector2(0.5f, 2.5f)));
        Assert.IsFalse(inflated.IsWalkable(new Vector2(0.5f, 2.5f)));
        Assert.IsTrue(inflated.IsWalkable(new Vector2(0.5f, 3.5f)));
        Assert.Greater(inflated.CountBlocked(), plain.CountBlocked());
    }

    [Test]
    public void BlockFootprint_NearEdgeIsClipped()
    {
        WalkabilityGrid grid = Grid(20f);

        Assert.DoesNotThrow(() => grid.BlockFootprint(Footprint.Ellipse(5f, 5f), new Vector2(10f, 10f)));
        Assert.IsFalse(grid.IsWalkable(grid.Width - 1, grid.Height - 1));
    }

    [Test]
    public void BlockBorder_BlocksStripAlongEdges()
    {
        WalkabilityGrid grid = Grid(20f);
        grid.BlockBorder(2f);

        Assert.IsFalse(grid.IsWalkable(0, 10));
        Assert.IsFalse(grid.IsWalkable(1, 10));
        Assert.IsTrue(grid.IsWalkable(2, 10));
        Assert.IsFalse(grid.IsWalkable(10, grid.Height - 1));
        Assert.IsTrue(grid.IsWalkable(10, 10));
        Assert.AreEqual(1, grid.RegionCount);
    }

    // Régiók

    [Test]
    public void FullWall_SplitsIntoTwoRegions()
    {
        WalkabilityGrid grid = Grid();
        BuildWall(grid, 10);

        Assert.AreEqual(2, grid.RegionCount);
        Assert.AreNotEqual(grid.GetRegion(2, 2), grid.GetRegion(15, 2));
    }

    [Test]
    public void WallWithGap_StaysConnected()
    {
        WalkabilityGrid grid = Grid();
        BuildWall(grid, 10, 5, 5);

        Assert.AreEqual(1, grid.RegionCount);
        Assert.AreEqual(grid.GetRegion(2, 2), grid.GetRegion(15, 2));
    }

    [Test]
    public void DiagonalGap_DoesNotAllowCornerCutting()
    {
        // Lépcsőzetes fal: minden sorban egy tiltott cella, átlósan egymás mellett.
        // Átlósan „átcsúszni” csak sarokvágással lehetne, ami tiltott.
        WalkabilityGrid grid = Grid(10f);

        for (int i = 0; i < grid.Width; i++)
            grid.Block(i, i);

        Assert.AreEqual(2, grid.RegionCount);
        Assert.AreNotEqual(grid.GetRegion(0, 5), grid.GetRegion(5, 0));
    }

    [Test]
    public void Regions_UpdateAfterNewBlocking()
    {
        WalkabilityGrid grid = Grid();

        Assert.AreEqual(1, grid.RegionCount);

        BuildWall(grid, 10);

        Assert.AreEqual(2, grid.RegionCount);
    }

    [Test]
    public void AreConnected_ChecksAllCells()
    {
        WalkabilityGrid grid = Grid();
        BuildWall(grid, 10);

        Vector2Int left1 = new Vector2Int(2, 2);
        Vector2Int left2 = new Vector2Int(5, 18);
        Vector2Int right = new Vector2Int(15, 2);

        Assert.IsTrue(grid.AreConnected(new[] { left1, left2 }));
        Assert.IsFalse(grid.AreConnected(new[] { left1, right }));
        Assert.IsFalse(grid.AreConnected(new[] { left1, new Vector2Int(10, 3) }));
        Assert.IsTrue(grid.AreConnected(new List<Vector2Int>()));
    }

    [Test]
    public void TryFindNearestWalkableCell_StepsOutOfBlockedArea()
    {
        WalkabilityGrid grid = Grid(40f);
        grid.BlockFootprint(Footprint.Ellipse(5f, 2f), Vector2.zero);

        // Az ellipszis szélén belüli pont: a saját cellája tiltott.
        Vector2 nearEdge = new Vector2(4.8f, 0f);

        Assert.IsFalse(grid.IsWalkable(nearEdge));
        Assert.IsTrue(grid.TryFindNearestWalkableCell(nearEdge, 2f, out Vector2Int cell));
        Assert.IsTrue(grid.IsWalkable(cell.x, cell.y));
        Assert.LessOrEqual(Vector2.Distance(grid.CellToWorld(cell.x, cell.y), nearEdge), 2f);

        Assert.IsFalse(grid.TryFindNearestWalkableCell(Vector2.zero, 1f, out _));
    }

    [Test]
    public void Clone_IsIndependent()
    {
        WalkabilityGrid original = Grid();
        WalkabilityGrid copy = original.Clone();

        copy.Block(3, 3);

        Assert.IsTrue(original.IsWalkable(3, 3));
        Assert.IsFalse(copy.IsWalkable(3, 3));
    }

    // Teljesítmény

    [Test]
    public void LargestWorld_BuildsAndLabelsQuickly()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        WalkabilityGrid grid = new WalkabilityGrid(new WorldBounds(530f), 1f);
        grid.BlockBorder(2f);

        DeterministicRandom random = new DeterministicRandom(1);
        Footprint obstacle = Footprint.Ellipse(5.5f, 1.6f);

        for (int i = 0; i < 50; i++)
        {
            Vector2 position = new Vector2(random.NextFloat(-250f, 250f), random.NextFloat(-250f, 250f));
            grid.BlockFootprint(obstacle, position, 1.15f, 2f);
        }

        Footprint baseFootprint = Footprint.Ellipse(5.2f, 1.9f);

        for (int i = 0; i < 150; i++)
        {
            Vector2 position = new Vector2(random.NextFloat(-250f, 250f), random.NextFloat(-250f, 250f));
            grid.BlockFootprint(baseFootprint, position);
        }

        int regions = grid.RegionCount;
        stopwatch.Stop();

        Assert.GreaterOrEqual(regions, 1);
        Assert.Less(stopwatch.ElapsedMilliseconds, 500,
            $"A 530-as rács felépítése és régiókra bontása {stopwatch.ElapsedMilliseconds} ms volt.");
        UnityEngine.Debug.Log(
            $"WalkabilityGrid 530x530, 50 akadály, 150 bázis, régiók: {stopwatch.ElapsedMilliseconds} ms");
    }
}
