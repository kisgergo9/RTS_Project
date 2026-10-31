using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A pálya járhatósági rácsa: négyzetes cellák, cellánként járható vagy tiltott.
/// A világgenerátor ezzel ellenőrzi az elérhetőséget, az útvonaltervezés
/// (A*) pedig erre a rácsra épülhet, így nem jön létre párhuzamos rendszer.
///
/// Cellák: az (x, y) cella a [Min + x * CellSize; Min + (x + 1) * CellSize)
/// tartományt fedi mindkét tengelyen, ahol Min a pálya bal alsó sarka.
/// Egy cella akkor tiltott, ha a középpontja egy tiltott területre esik.
///
/// Összefüggőség: 8-szomszédság, de átlós lépés csak akkor megengedett, ha
/// mindkét érintett egyenes szomszéd is járható (nincs „sarokvágás”).
/// Csak járhatósági adatot tartalmaz; tulajdonjogról vagy harcról nem dönt.
/// </summary>
public sealed class WalkabilityGrid
{
    public const int NoRegion = -1;

    private static readonly int[] NeighborX = { 1, -1, 0, 0, 1, 1, -1, -1 };
    private static readonly int[] NeighborY = { 0, 0, 1, -1, 1, -1, 1, -1 };

    private readonly bool[] _blocked;
    private int[] _regions;
    private int _regionCount;

    public WorldBounds Bounds { get; }
    public float CellSize { get; }
    public int Width { get; }
    public int Height { get; }
    public int CellCount => _blocked.Length;

    public WalkabilityGrid(WorldBounds bounds, float cellSize)
    {
        if (!(cellSize > 0f) || cellSize > bounds.Size)
            throw new ArgumentOutOfRangeException(nameof(cellSize),
                "A cellaméretnek pozitívnak és legfeljebb a pálya méretének kell lennie.");

        Bounds = bounds;
        CellSize = cellSize;
        Width = Mathf.CeilToInt(bounds.Size / cellSize - 1e-4f);
        Height = Width;
        _blocked = new bool[Width * Height];
    }

    private WalkabilityGrid(WalkabilityGrid source)
    {
        Bounds = source.Bounds;
        CellSize = source.CellSize;
        Width = source.Width;
        Height = source.Height;
        _blocked = (bool[])source._blocked.Clone();
    }

    /// <summary>
    /// Független másolat (pl. egy akadály-elhelyezési kísérlethez).
    /// </summary>
    public WalkabilityGrid Clone()
    {
        return new WalkabilityGrid(this);
    }

    // Koordináták

    public bool IsInside(int x, int y)
    {
        return x >= 0 && y >= 0 && x < Width && y < Height;
    }

    public Vector2Int WorldToCell(Vector2 position)
    {
        Vector2 min = Bounds.Min;

        return new Vector2Int(
            Mathf.FloorToInt((position.x - min.x) / CellSize),
            Mathf.FloorToInt((position.y - min.y) / CellSize));
    }

    public Vector2 CellToWorld(int x, int y)
    {
        Vector2 min = Bounds.Min;

        return new Vector2(
            min.x + (x + 0.5f) * CellSize,
            min.y + (y + 0.5f) * CellSize);
    }

    // Járhatóság

    public bool IsWalkable(int x, int y)
    {
        return IsInside(x, y) && !_blocked[y * Width + x];
    }

    public bool IsWalkable(Vector2 position)
    {
        Vector2Int cell = WorldToCell(position);

        return IsWalkable(cell.x, cell.y);
    }

    public int CountBlocked()
    {
        int count = 0;

        foreach (bool blocked in _blocked)
        {
            if (blocked)
                count++;
        }

        return count;
    }

    public void Block(int x, int y)
    {
        if (!IsInside(x, y))
            return;

        _blocked[y * Width + x] = true;
        _regions = null;
    }

    /// <summary>
    /// Tiltottá jelöli azokat a cellákat, amelyek középpontja a megadott helyre
    /// és léptékre transzformált alapterületen belül van, vagy annak határától
    /// legfeljebb inflation távolságra esik.
    /// </summary>
    public void BlockFootprint(Footprint footprint, Vector2 position, float scale = 1f, float inflation = 0f)
    {
        if (footprint == null)
            throw new ArgumentNullException(nameof(footprint));

        if (inflation < 0f)
            throw new ArgumentOutOfRangeException(nameof(inflation));

        Footprint scaled = Mathf.Approximately(scale, 1f) ? footprint : footprint.Scaled(scale);
        Vector2 reach = scaled.HalfExtents + new Vector2(inflation, inflation);

        Vector2Int from = WorldToCell(position - reach);
        Vector2Int to = WorldToCell(position + reach);

        int minX = Mathf.Max(0, from.x);
        int minY = Mathf.Max(0, from.y);
        int maxX = Mathf.Min(Width - 1, to.x);
        int maxY = Mathf.Min(Height - 1, to.y);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 local = CellToWorld(x, y) - position;

                if (scaled.Contains(local) ||
                    (inflation > 0f && scaled.DistanceToEdge(local) <= inflation))
                {
                    _blocked[y * Width + x] = true;
                }
            }
        }

        _regions = null;
    }

    /// <summary>
    /// Tiltottá jelöli a pálya széle mentén azokat a cellákat, amelyek
    /// középpontja a széltől legfeljebb thickness távolságra van (vagy kívül esik).
    /// </summary>
    public void BlockBorder(float thickness)
    {
        if (thickness < 0f)
            throw new ArgumentOutOfRangeException(nameof(thickness));

        float limit = Bounds.HalfSize - thickness;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                Vector2 center = CellToWorld(x, y);

                if (Mathf.Abs(center.x) > limit || Mathf.Abs(center.y) > limit)
                    _blocked[y * Width + x] = true;
            }
        }

        _regions = null;
    }

    // Összefüggőség

    /// <summary>
    /// Az összefüggő járható régiók száma.
    /// </summary>
    public int RegionCount
    {
        get
        {
            EnsureRegions();
            return _regionCount;
        }
    }

    /// <summary>
    /// A cella régiójának azonosítója (0-tól), tiltott vagy pályán kívüli
    /// cellára NoRegion.
    /// </summary>
    public int GetRegion(int x, int y)
    {
        if (!IsWalkable(x, y))
            return NoRegion;

        EnsureRegions();
        return _regions[y * Width + x];
    }

    public int GetRegion(Vector2 position)
    {
        Vector2Int cell = WorldToCell(position);

        return GetRegion(cell.x, cell.y);
    }

    /// <summary>
    /// A ponthoz legközelebbi járható cella, legfeljebb maxDistance távolságon
    /// belül. Hasznos, ha egy kijárat közvetlenül egy tiltott terület mellett
    /// van, és a saját cellájának középpontja még a tiltott területre esik.
    /// </summary>
    public bool TryFindNearestWalkableCell(Vector2 position, float maxDistance, out Vector2Int cell)
    {
        Vector2Int origin = WorldToCell(position);

        if (IsWalkable(origin.x, origin.y))
        {
            cell = origin;
            return true;
        }

        int radius = Mathf.CeilToInt(maxDistance / CellSize);
        float bestDistanceSqr = maxDistance * maxDistance;
        bool found = false;
        cell = origin;

        for (int y = origin.y - radius; y <= origin.y + radius; y++)
        {
            for (int x = origin.x - radius; x <= origin.x + radius; x++)
            {
                if (!IsWalkable(x, y))
                    continue;

                float distanceSqr = (CellToWorld(x, y) - position).sqrMagnitude;

                // Szigorú összehasonlítás sorfolytonos bejárással: azonos
                // távolságnál mindig a legkisebb (y, x) indexű cella marad,
                // így az eredmény determinisztikus.
                if (distanceSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    cell = new Vector2Int(x, y);
                    found = true;
                }
            }
        }

        return found;
    }

    /// <summary>
    /// Igaz, ha az összes megadott cella járható és ugyanabba a régióba tartozik.
    /// Üres lista esetén igaz.
    /// </summary>
    public bool AreConnected(IEnumerable<Vector2Int> cells)
    {
        if (cells == null)
            throw new ArgumentNullException(nameof(cells));

        int region = NoRegion;

        foreach (Vector2Int cell in cells)
        {
            int current = GetRegion(cell.x, cell.y);

            if (current == NoRegion)
                return false;

            if (region == NoRegion)
                region = current;
            else if (current != region)
                return false;
        }

        return true;
    }

    private void EnsureRegions()
    {
        if (_regions != null)
            return;

        int[] regions = new int[_blocked.Length];

        for (int i = 0; i < regions.Length; i++)
            regions[i] = NoRegion;

        int[] queue = new int[_blocked.Length];
        int regionCount = 0;

        for (int start = 0; start < _blocked.Length; start++)
        {
            if (_blocked[start] || regions[start] != NoRegion)
                continue;

            int head = 0;
            int tail = 0;
            queue[tail++] = start;
            regions[start] = regionCount;

            while (head < tail)
            {
                int index = queue[head++];
                int x = index % Width;
                int y = index / Width;

                for (int n = 0; n < NeighborX.Length; n++)
                {
                    int nx = x + NeighborX[n];
                    int ny = y + NeighborY[n];

                    if (!IsWalkable(nx, ny))
                        continue;

                    // Átlós lépés csak sarokvágás nélkül.
                    if (n >= 4 && (!IsWalkable(nx, y) || !IsWalkable(x, ny)))
                        continue;

                    int next = ny * Width + nx;

                    if (regions[next] != NoRegion)
                        continue;

                    regions[next] = regionCount;
                    queue[tail++] = next;
                }
            }

            regionCount++;
        }

        _regions = regions;
        _regionCount = regionCount;
    }
}
