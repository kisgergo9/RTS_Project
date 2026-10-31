using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Egy objektum alapterülete (helyigénye) a saját középpontjához viszonyított,
/// 1-es léptékű sokszögként. A világgenerátor és a járhatósági rács ebből
/// számolja, mely területet foglal el egy bázis, akadály vagy dekoráció.
/// Megváltoztathatatlan.
/// </summary>
public sealed class Footprint
{
    private const float MinArea = 1e-4f;

    private readonly Vector2[] _points;

    public IReadOnlyList<Vector2> Points => _points;

    /// <summary>
    /// A sokszög tengelyirányú fél-kiterjedése (a középponttól mért legnagyobb
    /// |x| és |y|).
    /// </summary>
    public Vector2 HalfExtents { get; }

    /// <summary>
    /// A középponttól legtávolabbi csúcs távolsága.
    /// </summary>
    public float BoundingRadius { get; }

    public Footprint(IEnumerable<Vector2> points)
    {
        if (points == null)
            throw new ArgumentNullException(nameof(points));

        _points = new List<Vector2>(points).ToArray();

        if (_points.Length < 3)
            throw new ArgumentException(
                "Az alapterülethez legalább 3 pont szükséges.", nameof(points));

        if (Mathf.Abs(SignedArea(_points)) < MinArea)
            throw new ArgumentException(
                "Az alapterület sokszöge elfajuló (nulla területű).", nameof(points));

        float maxX = 0f;
        float maxY = 0f;
        float maxRadius = 0f;

        foreach (Vector2 point in _points)
        {
            maxX = Mathf.Max(maxX, Mathf.Abs(point.x));
            maxY = Mathf.Max(maxY, Mathf.Abs(point.y));
            maxRadius = Mathf.Max(maxRadius, point.magnitude);
        }

        HalfExtents = new Vector2(maxX, maxY);
        BoundingRadius = maxRadius;
    }

    /// <summary>
    /// Ellipszis alakú alapterület, a megadott fél-tengelyekkel.
    /// </summary>
    public static Footprint Ellipse(float halfWidth, float halfHeight, int segments = 16)
    {
        if (halfWidth <= 0f || halfHeight <= 0f)
            throw new ArgumentOutOfRangeException(nameof(halfWidth),
                "Az ellipszis fél-tengelyeinek pozitívnak kell lenniük.");

        if (segments < 3)
            throw new ArgumentOutOfRangeException(nameof(segments));

        Vector2[] points = new Vector2[segments];

        for (int i = 0; i < segments; i++)
        {
            double angle = 2.0 * Math.PI * i / segments;

            points[i] = new Vector2(
                (float)(halfWidth * Math.Cos(angle)),
                (float)(halfHeight * Math.Sin(angle)));
        }

        return new Footprint(points);
    }

    public Footprint Scaled(float scale)
    {
        if (scale <= 0f)
            throw new ArgumentOutOfRangeException(nameof(scale),
                "A léptéknek pozitívnak kell lennie.");

        Vector2[] points = new Vector2[_points.Length];

        for (int i = 0; i < points.Length; i++)
            points[i] = _points[i] * scale;

        return new Footprint(points);
    }

    /// <summary>
    /// Igaz, ha a (középponthoz viszonyított) pont a sokszögön belül van.
    /// </summary>
    public bool Contains(Vector2 localPoint)
    {
        bool inside = false;

        for (int i = 0, j = _points.Length - 1; i < _points.Length; j = i++)
        {
            Vector2 a = _points[i];
            Vector2 b = _points[j];

            if ((a.y > localPoint.y) != (b.y > localPoint.y) &&
                localPoint.x < (b.x - a.x) * (localPoint.y - a.y) / (b.y - a.y) + a.x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>
    /// A (középponthoz viszonyított) pont legkisebb távolsága a sokszög
    /// határától. Belső pontra is a határtól mért távolságot adja.
    /// </summary>
    public float DistanceToEdge(Vector2 localPoint)
    {
        float best = float.MaxValue;

        for (int i = 0, j = _points.Length - 1; i < _points.Length; j = i++)
            best = Mathf.Min(best, DistanceToSegment(localPoint, _points[j], _points[i]));

        return best;
    }

    /// <summary>
    /// Két elhelyezett alapterület legkisebb távolsága. Átfedés vagy érintés
    /// esetén 0.
    /// </summary>
    public static float Distance(Footprint a, Vector2 positionA, Footprint b, Vector2 positionB)
    {
        if (a == null)
            throw new ArgumentNullException(nameof(a));

        if (b == null)
            throw new ArgumentNullException(nameof(b));

        Vector2 offset = positionB - positionA;
        float best = float.MaxValue;

        // A csúcsainak távolsága B-től (B saját koordinátáiban), és fordítva.
        foreach (Vector2 point in a._points)
        {
            Vector2 local = point - offset;

            if (b.Contains(local))
                return 0f;

            best = Mathf.Min(best, b.DistanceToEdge(local));
        }

        foreach (Vector2 point in b._points)
        {
            Vector2 local = point + offset;

            if (a.Contains(local))
                return 0f;

            best = Mathf.Min(best, a.DistanceToEdge(local));
        }

        // Csúcs nélküli metszés (két él keresztezi egymást).
        for (int i = 0, j = a._points.Length - 1; i < a._points.Length; j = i++)
        {
            for (int k = 0, l = b._points.Length - 1; k < b._points.Length; l = k++)
            {
                if (SegmentsIntersect(a._points[j], a._points[i], b._points[l] + offset, b._points[k] + offset))
                    return 0f;
            }
        }

        return best;
    }

    private static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        float d1 = Cross(q2 - q1, p1 - q1);
        float d2 = Cross(q2 - q1, p2 - q1);
        float d3 = Cross(p2 - p1, q1 - p1);
        float d4 = Cross(p2 - p1, q2 - p1);

        return ((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) &&
               ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f));
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSqr = ab.sqrMagnitude;

        if (lengthSqr <= 0f)
            return Vector2.Distance(point, a);

        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSqr);

        return Vector2.Distance(point, a + ab * t);
    }

    private static float SignedArea(Vector2[] points)
    {
        float area = 0f;

        for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            area += points[j].x * points[i].y - points[i].x * points[j].y;

        return area * 0.5f;
    }
}
