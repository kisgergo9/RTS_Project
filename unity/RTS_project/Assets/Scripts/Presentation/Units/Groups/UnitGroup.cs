using System.Collections.Generic;
using UnityEngine;

public sealed class UnitGroup : MonoBehaviour
{
    private readonly List<UnitGroupMember> _members = new();

    // A csoport létrehozásakor rögzített cellaméret.
    private Vector2 _cellSize = Vector2.one;

    public IReadOnlyList<UnitGroupMember> Members => _members;

    public int MemberCount => _members.Count;

    public UnitType? GroupType =>
        _members.Count > 0 ? _members[0].Type : null;

    public bool CanAccept(UnitGroupMember member)
    {
        if (member == null)
            return false;

        if (_members.Count == 0)
            return true;

        return AreCompatible(_members[0].Type, member.Type);
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
        for (int slot = 1; slot < int.MaxValue; slot++)
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

    public void InitializeCellSize(UnitGroupMember member)
    {
        if (member == null)
            return;

        float maxWidth = 0f;
        float maxHeight = 0f;

        DirectionalSpriteRenderer[] renderers =
            member.GetComponentsInChildren<DirectionalSpriteRenderer>(true);

        foreach (DirectionalSpriteRenderer directional in renderers)
        {
            for (int i = 0; i < directional.ViewCount; i++)
            {
                Sprite sprite = directional.GetSpriteAtIndex(i);

                if (sprite == null)
                    continue;

                Vector3 scale = directional.transform.lossyScale;
                Vector2 size = sprite.bounds.size;

                float width = size.x * Mathf.Abs(scale.x);
                float height = size.y * Mathf.Abs(scale.y);

                maxWidth = Mathf.Max(maxWidth, width);
                maxHeight = Mathf.Max(maxHeight, height);
            }
        }

        if (maxWidth > 0f && maxHeight > 0f)
            _cellSize = new Vector2(maxWidth, maxHeight);
    }

    public Vector3 GetSlotWorldPosition(int slot)
    {
        UnitGroupMember leader = GetLeader();

        if (leader == null)
            return transform.position;

        Vector2 grid = GetSpiralOffset(slot);

        // A Python-mátrix sorindexe lefelé nõ,
        // ezért a világ Y-koordinátáját megfordítjuk.
        Vector3 offset = new Vector3(
            grid.y * _cellSize.x,
            -grid.x * _cellSize.y,
            0f);

        return leader.transform.position + offset;
    }

    private static Vector2 GetSpiralOffset(int targetSlot)
    {
        if (targetSlot < 1)
            return Vector2.zero;

        // A Python-algoritmus kezdõpontja a középpont.
        int row = 0;
        int column = 0;
        int currentSlot = 1;
        int stepSize = 1;

        int[,] directions =
        {
            { 0,  1 }, // jobbra
            { 1,  0 }, // le
            { 0, -1 }, // balra
            { -1, 0 }  // fel
        };

        while (currentSlot < targetSlot)
        {
            for (int direction = 0; direction < 4; direction++)
            {
                int dx = directions[direction, 0];
                int dy = directions[direction, 1];

                for (int step = 0; step < stepSize; step++)
                {
                    row += dx;
                    column += dy;
                    currentSlot++;

                    if (currentSlot == targetSlot)
                        return new Vector2(row, column);
                }

                // A Python-algoritmusban le és fel után nõ a lépésszám.
                if (direction == 1 || direction == 3)
                    stepSize++;
            }
        }

        return Vector2.zero;
    }
}