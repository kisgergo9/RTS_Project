using UnityEngine;
using UnityEngine.InputSystem;

public sealed class RuntimeUnitSpawner : MonoBehaviour
{
    [Header("Spawn settings")]
    [SerializeField] private GameObject unitPrefab;
    [SerializeField] private Camera worldCamera;

    [Header("Input")]
    [SerializeField] private Key spawnKey = Key.E;

    private GameObjectRegistrar _registrar;

    private void Awake()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;

        _registrar = GameObjectRegistrar.Instance;

        if (_registrar == null)
        {
            Debug.LogError(
                "Nem található GameObjectRegistrar a jelenetben.",
                this);
        }

        if (unitPrefab == null)
        {
            Debug.LogError(
                "A RuntimeUnitSpawner prefabja nincs beállítva.",
                this);
        }
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null)
            return;

        if (worldCamera == null || unitPrefab == null)
            return;

        if (_registrar == null)
            return;

        if (!Keyboard.current[spawnKey].wasPressedThisFrame)
            return;

        SpawnUnit();
    }

    private void SpawnUnit()
    {
        Vector3 mouseWorldPosition = GetMouseWorldPosition();

        UnitGroup selectedGroup = GetSingleSelectedGroup();

        if (selectedGroup != null &&
            TrySpawnIntoGroup(selectedGroup))
        {
            return;
        }

        SpawnStandaloneUnit(mouseWorldPosition);
    }

    private void SpawnStandaloneUnit(Vector3 position)
    {
        GameObject spawnedObject = Instantiate(
            unitPrefab,
            position,
            unitPrefab.transform.rotation);

        UnitGroupMember spawnedMember =
            spawnedObject.GetComponentInChildren<UnitGroupMember>(true);

        if (spawnedMember == null)
        {
            Debug.LogError(
                "A létrehozott prefab nem tartalmaz UnitGroupMember komponenst.",
                spawnedObject);

            Destroy(spawnedObject);
            return;
        }

        if (!TryRegisterUnit(spawnedMember))
        {
            Destroy(spawnedObject);
            return;
        }

        SetRandomDirection(spawnedMember);
    }

    private bool TrySpawnIntoGroup(UnitGroup targetGroup)
    {
        if (targetGroup == null)
            return false;

        UnitGroupMember prefabMember =
            unitPrefab.GetComponentInChildren<UnitGroupMember>(true);

        if (prefabMember == null)
            return false;

        if (!targetGroup.CanAccept(prefabMember))
            return false;

        int slot = targetGroup.FindNextFreeSlot();

        if (slot < 1)
            return false;

        Vector3 spawnPosition =
            targetGroup.GetSlotWorldPosition(slot);

        GameObject spawnedObject = Instantiate(
            unitPrefab,
            spawnPosition,
            unitPrefab.transform.rotation);

        UnitGroupMember spawnedMember =
            spawnedObject.GetComponentInChildren<UnitGroupMember>(true);

        if (spawnedMember == null)
        {
            Debug.LogError(
                "A létrehozott prefab nem tartalmaz UnitGroupMember komponenst.",
                spawnedObject);

            Destroy(spawnedObject);
            return false;
        }

        // Az egység regisztrációja az AddMember elõtt történik.
        if (!TryRegisterUnit(spawnedMember))
        {
            Destroy(spawnedObject);
            return false;
        }

        if (spawnedMember.Group != null)
        {
            Debug.LogError(
                "Az új egység már egy másik csoporthoz tartozik.",
                spawnedMember);

            Destroy(spawnedObject);
            return false;
        }

        if (!targetGroup.AddMember(spawnedMember, slot))
        {
            Destroy(spawnedObject);
            return false;
        }

        spawnedObject.transform.SetParent(targetGroup.transform, true);

        SetRandomDirection(spawnedMember);

        return true;
    }

    private bool TryRegisterUnit(UnitGroupMember member)
    {
        if (member == null)
            return false;

        try
        {
            _registrar.RegisterUnit(member);
            return member.IsInitialized;
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, member);
            return false;
        }
    }

    private void SetRandomDirection(UnitGroupMember member)
    {
        DirectionalSpriteRenderer[] directionalRenderers =
            member.GetComponentsInChildren<DirectionalSpriteRenderer>(true);

        foreach (DirectionalSpriteRenderer directional in directionalRenderers)
            directional.SetAngle(Random.value * 123f);
    }

    private UnitGroup GetSingleSelectedGroup()
    {
        UnitGroup[] groups =
            FindObjectsByType<UnitGroup>(FindObjectsSortMode.None);

        UnitGroup selectedGroup = null;
        int selectedCount = 0;

        foreach (UnitGroup group in groups)
        {
            if (group == null || !group.IsSelected)
                continue;

            selectedGroup = group;
            selectedCount++;

            if (selectedCount > 1)
                return null;
        }

        return selectedCount == 1 ? selectedGroup : null;
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector2 mousePosition = Mouse.current.position.ReadValue();

        Ray ray = worldCamera.ScreenPointToRay(mousePosition);

        Plane worldPlane = new Plane(
            Vector3.forward,
            Vector3.zero);

        if (!worldPlane.Raycast(ray, out float distance))
            return Vector3.zero;

        Vector3 position = ray.GetPoint(distance);
        position.z = 0f;

        return position;
    }
}