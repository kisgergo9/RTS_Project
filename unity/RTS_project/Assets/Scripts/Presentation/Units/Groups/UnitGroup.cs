using System.Collections.Generic;
using UnityEngine;

public sealed class UnitGroup : MonoBehaviour
{
    private readonly List<UnitGroupMember> _members = new();

    // A formáció minden egységtípusnál azonos cellaméretet használ.
    // A megadott értékek a sprite pixeles méretét jelentik.
    private const float ReferenceSpriteWidth = 60f;
    private const float ReferenceSpriteHeight = 40f;

    // A világkoordinátákra váltáshoz használt Pixels Per Unit érték.
    private const float ReferencePixelsPerUnit = 100f;

    private static readonly Vector2 CellSize = new Vector2(
        ReferenceSpriteWidth / ReferencePixelsPerUnit,
        ReferenceSpriteHeight / ReferencePixelsPerUnit);

    // A rombusz hegyesszögének fele.
    private const float FormationHalfAngle = 18.62f;

    private static readonly float FormationSin =
        Mathf.Sin(FormationHalfAngle * Mathf.Deg2Rad);

    private static readonly float FormationCos =
        Mathf.Cos(FormationHalfAngle * Mathf.Deg2Rad);

    // A szomszédos formációs helyek közötti távolság.
    private static readonly float FormationStep =
        Mathf.Max(CellSize.x, CellSize.y);

    public IReadOnlyList<UnitGroupMember> Members => _members;

    public int MemberCount => _members.Count;

    public UnitType? GroupType =>
        _members.Count > 0 ? _members[0].Type : null;

    private const int FormationSize = 7;
    private const int MaxMembers = FormationSize * FormationSize;

    [Header("Group selection")]

    [SerializeField, Min(0.001f)]
    private float selectionBaseHalfSize = 0.4f;

    // A kulcs a csapattagok száma, az érték a méretszorzó.
    private static readonly SortedDictionary<int, float> SelectionScaleRules =
        new SortedDictionary<int, float>
        {
        { 1, 1.0f },
        { 2, 1.8f },
        { 4, 2.55f },
        { 6, 2.9f },
        { 7, 3.1f },
        { 10, 4.3f },
        { 15, 4.4f },
        { 20, 4.5f },
        { 26, 6f },
        };

    [SerializeField, Min(0.001f)]
    private float selectionLineWidth = 0.025f;

    private LineRenderer _selectionOutline;
    private bool _isSelected;

    public bool IsSelected => _isSelected;

    private static readonly int[,] FormationOrder =
    {
    { 48, 45, 37, 29, 33, 40, 47 },
    { 43, 22, 14, 10, 19, 23, 38 },
    { 35, 18,  6,  2,  7, 15, 31 },
    { 26, 13,  5,  1,  3, 11, 27 },
    { 32, 21,  9,  4,  8, 20, 34 },
    { 41, 25, 17, 12, 16, 24, 42 },
    { 49, 39, 30, 28, 36, 44, 46 }
};

    public bool IsFull => _members.Count >= MaxMembers;

    private float GetSelectionScale()
    {
        float scale = 1f;

        foreach (KeyValuePair<int, float> rule in SelectionScaleRules)
        {
            if (MemberCount < rule.Key)
                break;

            scale = rule.Value;
        }

        return scale;
    }

    public bool CanAccept(UnitGroupMember member)
    {
        if (member == null || IsFull)
            return false;

        if (_members.Count == 0)
            return true;

        return AreCompatible(_members[0].Type, member.Type);
    }

    private void Awake()
    {
        InitializeSelectionOutline();
    }

    private void InitializeSelectionOutline()
    {
        GameObject outlineObject = new GameObject("GroupSelectionOutline");
        outlineObject.transform.SetParent(transform, false);

        _selectionOutline = outlineObject.AddComponent<LineRenderer>();
        _selectionOutline.useWorldSpace = true;
        _selectionOutline.loop = true;
        _selectionOutline.positionCount = 4;
        _selectionOutline.startWidth = selectionLineWidth;
        _selectionOutline.endWidth = selectionLineWidth;
        _selectionOutline.startColor = Color.white;
        _selectionOutline.endColor = Color.white;
        _selectionOutline.material =
            new Material(Shader.Find("Sprites/Default"));

        _selectionOutline.sortingOrder = 100;
        _selectionOutline.enabled = false;
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;

        if (_selectionOutline != null)
            _selectionOutline.enabled = selected;

        if (selected)
            UpdateSelectionOutline();
    }

    private void LateUpdate()
    {
        if (_isSelected)
            UpdateSelectionOutline();
    }

    private void UpdateSelectionOutline()
    {
        if (_selectionOutline == null)
            return;

        if (MemberCount == 0)
        {
            _selectionOutline.enabled = false;
            return;
        }

        Vector3 center = Vector3.zero;
        int validMemberCount = 0;

        foreach (UnitGroupMember member in _members)
        {
            if (member == null)
                continue;

            center += member.transform.position;
            validMemberCount++;
        }

        if (validMemberCount == 0)
        {
            _selectionOutline.enabled = false;
            return;
        }

        // A csapat geometriai középpontja.
        center /= validMemberCount;

        // A korábbi, létszámfüggõ növekedési szabály megtartása.
        float halfSize = selectionBaseHalfSize * GetSelectionScale();

        // A kijelölési rombusz mérete.
        float spacing = halfSize;
        float angleRadians = 40f * Mathf.Deg2Rad;

        // A két rácstengely eltolásvektorai.
        Vector2 columnOffset = new Vector2(spacing, 0f);

        Vector2 rowOffset = new Vector2(
            spacing * Mathf.Cos(angleRadians),
            -spacing * Mathf.Sin(angleRadians));

        // A formációval azonos, 20°-os elforgatás.
        Quaternion rotation = Quaternion.Euler(0f, 0f, 20f);

        Vector2 column = rotation * columnOffset;
        Vector2 row = rotation * rowOffset;

        // A négy sarok a két tengelyvektor összegébõl és
        // különbségébõl áll elõ.
        Vector3[] corners =
        {
            new Vector3(column.x + row.x, column.y + row.y, 0f),
            new Vector3(column.x - row.x, column.y - row.y, 0f),
            new Vector3(-column.x - row.x, -column.y - row.y, 0f),
            new Vector3(-column.x + row.x, -column.y + row.y, 0f)
        };

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 corner = corners[i];

            _selectionOutline.SetPosition(
                i,
                new Vector3(
                    center.x + corner.x,
                    center.y + corner.y,
                    0));
        }
    }

    private static float GetMemberSpriteRadius(UnitGroupMember member)
    {
        SpriteRenderer[] renderers =
            member.GetComponentsInChildren<SpriteRenderer>(true);

        float radius = 0f;

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || renderer.sprite == null)
                continue;

            Vector3 extents = renderer.bounds.extents;

            radius = Mathf.Max(
                radius,
                Mathf.Sqrt(extents.x * extents.x + extents.y * extents.y));
        }

        return radius;
    }

    public static bool AreCompatible(UnitType a, UnitType b)
    {
        // Jelenleg csak azonos típusok alkothatnak csoportot.
        // A késõbbi bõvítéshez ezt a metódust kell módosítani.
        return a == b;
    }

    public bool AddMember(UnitGroupMember member, int slot)
    {
        if (member == null || _members.Contains(member))
            return false;

        if (!CanAccept(member))
            return false;

        if (IsSlotOccupied(slot))
            return false;

        _members.Add(member);
        member.SetGroup(this, slot);

        return true;
    }

    public bool RemoveMember(UnitGroupMember member)
    {
        if (member == null || !_members.Remove(member))
            return false;

        member.SetGroup(null);
        return true;
    }

    public bool IsSlotOccupied(int slot)
    {
        foreach (UnitGroupMember member in _members)
        {
            if (member != null && member.FormationSlot == slot)
                return true;
        }

        return false;
    }

    public int FindNextFreeSlot()
    {
        for (int slot = 1; slot <= MaxMembers; slot++)
        {
            if (!IsSlotOccupied(slot))
                return slot;
        }

        return -1;
    }

    public UnitGroupMember GetNewestMember()
    {
        for (int i = _members.Count - 1; i >= 0; i--)
        {
            if (_members[i] != null)
                return _members[i];
        }

        return null;
    }

    public UnitGroupMember GetLeader()
    {
        foreach (UnitGroupMember member in _members)
        {
            if (member != null && member.FormationSlot == 1)
                return member;
        }

        return _members.Count > 0 ? _members[0] : null;
    }


    public Vector3 GetSlotWorldPosition(int slot)
    {
        UnitGroupMember leader = GetLeader();

        if (leader == null)
            return transform.position;

        Vector2 grid = GetSpiralOffset(slot);

        // A két rácstengely közötti szög 40°.
        float spacing = Mathf.Max(CellSize.x, CellSize.y);
        float angleRadians = 40f * Mathf.Deg2Rad;

        // Oszlopirány.
        Vector2 columnOffset = new Vector2(
            grid.y * spacing,
            0f);

        // Sorirány: 40°-os szöget zár be az oszloptengellyel.
        Vector2 rowOffset = new Vector2(
            grid.x * spacing * Mathf.Cos(angleRadians),
            -grid.x * spacing * Mathf.Sin(angleRadians));

        // Elõször kiszámítjuk a rombusz rácspontját.
        Vector2 offset = columnOffset + rowOffset;

        // Ezután elforgatjuk, hogy a rövidebbik átló függõleges legyen.
        offset = Quaternion.Euler(0f, 0f, 20f) * offset;

        return leader.transform.position +
               new Vector3(offset.x, offset.y, 0f);
    }


    private static Vector2 GetSpiralOffset(int targetSlot)
    {
        if (targetSlot < 1 || targetSlot > 49)
        {
            Debug.LogError(
                $"A formációs hely jelenleg csak 1 és 49 között támogatott: {targetSlot}");

            return Vector2.zero;
        }

        for (int row = 0; row < 7; row++)
        {
            for (int column = 0; column < 7; column++)
            {
                if (FormationOrder[row, column] == targetSlot)
                {
                    // A mátrix középpontja a (3, 3) index.
                    return new Vector2(
                        row - 3,
                        column - 3
                    );
                }
            }
        }

        return Vector2.zero;
    }
}