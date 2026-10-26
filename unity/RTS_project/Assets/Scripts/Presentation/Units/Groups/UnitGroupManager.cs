using UnityEngine;

public sealed class UnitGroupManager : MonoBehaviour
{
    public static UnitGroupManager Instance { get; private set; }

    private int _nextGroupId = 1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "A jelenetben csak egy UnitGroupManager lehet.",
                this);

            enabled = false;
            return;
        }

        Instance = this;
    }

    public UnitGroup CreateSingletonGroup(UnitGroupMember member)
    {
        if (member == null)
            return null;

        if (member.Group != null)
            return member.Group;

        GameObject groupObject =
            new GameObject($"Group_{_nextGroupId++}");

        groupObject.transform.position = member.transform.position;
        groupObject.transform.SetParent(transform, true);

        UnitGroup group = groupObject.AddComponent<UnitGroup>();

        if (!group.AddMember(member, 1))
        {
            Destroy(groupObject);
            return null;
        }

        member.transform.SetParent(group.transform, true);

        return group;
    }

    public bool BeginJoinNewestMember(UnitGroup source, UnitGroup target)
    {
        if (source == null || target == null || source == target)
            return false;

        if (target.IsMoving)
            return false;

        UnitGroupMember member = source.GetNewestMember();

        if (member == null)
            return false;

        // Csak kezdeti ellenõrzés. Helyfoglalás nem történik.
        if (!target.TryGetJoinSlot(member, out _, out _))
            return false;

        int originalSlot = member.FormationSlot;
        UnitGroup originalGroup = source;

        GameObject joiningObject =
            new GameObject($"Group_{_nextGroupId++}");

        joiningObject.transform.position = member.transform.position;
        joiningObject.transform.SetParent(transform, true);

        UnitGroup joiningGroup =
            joiningObject.AddComponent<UnitGroup>();

        member.transform.SetParent(joiningGroup.transform, true);
        source.RemoveMember(member);

        if (!joiningGroup.AddMember(member, 1))
        {
            member.transform.SetParent(originalGroup.transform, true);
            originalGroup.AddMember(member, originalSlot);

            Destroy(joiningObject);
            return false;
        }

        // Sikeres leválasztás után engedélyezzük az áthaladást
        // a célcsoport és az eredeti csoport tagjain.
        UnitCollisionHandler collisionHandler =
            member.GetComponent<UnitCollisionHandler>();

        if (collisionHandler != null)
        {
            collisionHandler.SetPassThroughGroups(
                originalGroup,
                target);
        }

        UnitGroupJoinRequest request =
            joiningObject.AddComponent<UnitGroupJoinRequest>();

        request.Initialize(target);

        if (originalGroup.MemberCount == 0)
            Destroy(originalGroup.gameObject);

        return true;
    }

    public bool TryCompleteJoin(UnitGroup joiningGroup, UnitGroup target, UnitGroupMember member, float positionTolerance)
    {
        if (joiningGroup == null ||
            target == null ||
            member == null ||
            joiningGroup == target)
        {
            return false;
        }

        // A végsõ ellenõrzéskor ismét lekérjük az aktuális slotot.
        if (!target.TryGetJoinSlot(
                member,
                out int slot,
                out Vector3 currentSlotPosition))
        {
            return false;
        }

        Vector2 currentPosition = member.transform.position;
        Vector2 slotPosition = currentSlotPosition;

        if ((currentPosition - slotPosition).sqrMagnitude >
            positionTolerance * positionTolerance)
        {
            return false;
        }

        int originalSlot = member.FormationSlot;

        joiningGroup.RemoveMember(member);
        member.transform.SetParent(target.transform, true);

        // A világpozíció és a forgatás megõrzésével próbáljuk felvenni.
        if (!target.AddMember(member, slot))
        {
            member.transform.SetParent(joiningGroup.transform, true);
            joiningGroup.AddMember(member, originalSlot);
            return false;
        }

        UnitCollisionHandler collisionHandler = member.GetComponent<UnitCollisionHandler>();

        if (collisionHandler != null)
            collisionHandler.SetPassThroughGroups();

        // Nem állítjuk át sem a pozíciót, sem a forgatást.
        Destroy(joiningGroup.gameObject);

        return true;
    }

    // nem használt véglegesítéskor törölhetõ ha nem kerül használatba de teszteléshez még jól jöhet.
    public bool MergeNewestMember(UnitGroup source, UnitGroup target)
    {
        if (source == null || target == null || source == target)
            return false;

        UnitGroupMember member = source.GetNewestMember();

        if (member == null)
            return false;

        if (!target.CanAccept(member))
        {
            Debug.LogWarning(
                $"Nem kompatibilis csoportok: {source.name} és {target.name}.",
                member);

            return false;
        }

        int targetSlot = target.FindNextFreeSlot();
        int originalSlot = member.FormationSlot;
        if (targetSlot < 1)
            return false;

        // Elõbb kiszámítjuk a célpozíciót, amíg a célcsoport
        // vezetõje és a formáció még változatlan.
        Vector3 targetPosition = target.GetSlotWorldPosition(targetSlot);

        // Kiválasztjuk a forráscsoport legújabb tagját.
        source.RemoveMember(member);

        member.transform.SetParent(target.transform, true);

        if (!target.AddMember(member, targetSlot))
        {
            // Hiba esetén megpróbáljuk visszaállítani a forráscsoportot.
            member.transform.SetParent(source.transform, true);
            source.AddMember(member, originalSlot);
            return false;
        }

        member.transform.position = targetPosition;

        // A testen és a toronyon is azonnal beállítjuk a 90 fokos irányt.
        DirectionalSpriteRenderer[] renderers =
            member.GetComponentsInChildren<DirectionalSpriteRenderer>(true);

        foreach (DirectionalSpriteRenderer directional in renderers)
            directional.SetAngle(90f);

        if (source.MemberCount == 0)
            Destroy(source.gameObject);

        return true;
    }

    public void UnregisterMember(UnitGroupMember member)
    {
        if (member == null || member.Group == null)
            return;

        UnitGroup group = member.Group;
        group.RemoveMember(member);

        if (group.MemberCount == 0)
            Destroy(group.gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}