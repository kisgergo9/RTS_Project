using UnityEngine;
using UnityEngine.InputSystem;

public sealed class RuntimeUnitSpawner : MonoBehaviour
{
    [Header("Spawn settings")]
    [SerializeField] private GameObject unitPrefab;
    [SerializeField] private Camera worldCamera;

    [Header("Input")]
    [SerializeField] private Key spawnKey = Key.E;

    private void Awake()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;

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

        Instantiate(
            unitPrefab,
            mouseWorldPosition,
            unitPrefab.transform.rotation);
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
            Destroy(spawnedObject);
            return false;
        }

        // A Start() regisztráció elõtt beállítjuk a célcsoportot,
        // hogy az új egység ne hozzon létre külön csoportot.
        if (spawnedMember.Group != null)
        {
            Debug.LogError(
                "Az új egység már egy másik csoporthoz regisztrálódott.",
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

        DirectionalSpriteRenderer[] directionalRenderers =
            spawnedMember.GetComponentsInChildren<DirectionalSpriteRenderer>(true);

        foreach (DirectionalSpriteRenderer directional in directionalRenderers)
            directional.SetAngle(Random.value * 123);

        return true;
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