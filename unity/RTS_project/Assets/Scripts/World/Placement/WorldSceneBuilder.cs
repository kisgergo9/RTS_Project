using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A generált WorldLayout alapján felépíti a jelenet objektumait:
/// bázisok, terepakadályok, dekorációk, talaj, kemény pályahatár és a kamera
/// határai.
/// Játékszabályt nem tartalmaz; a layoutot a host generálja, a kliensek
/// ugyanezt a layoutot építik fel.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldSceneBuilder : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField]
    private BaseController basePrefab;

    [Header("Ground")]
    [Tooltip("Csempézve kitölti a világot. Ha nincs megadva, nem készül talaj.")]
    [SerializeField]
    private Sprite groundSprite;

    [Tooltip("A Base prefab legalsó rétege -100-as sorrendű, ezért a talajnak ennél lejjebb kell lennie.")]
    [SerializeField]
    private int groundSortingOrder = -1000;

    [Header("Boundary")]
    [Tooltip("A pálya széle mentén elhelyezett, mozgást tiltó falak vastagsága.")]
    [SerializeField, Min(0.1f)]
    private float boundaryThickness = 10f;

    [Header("Camera")]
    [Tooltip("Ha nincs megadva, a fő kamera CameraController komponensét használja.")]
    [SerializeField]
    private CameraController cameraController;

    private readonly List<BaseController> _builtBases = new List<BaseController>();
    private Transform _root;

    public IReadOnlyList<BaseController> BuiltBases => _builtBases;

    /// <summary>
    /// Felépíti a világot. A bázisok inicializálva térnek vissza, a
    /// játékállapotba regisztrálásuk a hívó (GameBootstrapper) feladata.
    /// </summary>
    /// <param name="terrainSettings">
    /// A tereptárgyak grafikai változatai. Csak akkor kötelező, ha a layout
    /// akadályokat vagy dekorációkat tartalmaz.
    /// </param>
    /// <param name="localPlayerId">
    /// A helyi játékos azonosítója: az ő bázisai saját (zöld) zászlót kapnak,
    /// a többi játékosé ellenséges (piros) zászlót.
    /// </param>
    public IReadOnlyList<BaseController> Build(
        WorldLayout layout,
        PlayerRegistry players,
        WorldGenerationSettings terrainSettings = null,
        int localPlayerId = 0)
    {
        if (layout == null)
            throw new ArgumentNullException(nameof(layout));

        if (players == null)
            throw new ArgumentNullException(nameof(players));

        if (basePrefab == null)
            throw new InvalidOperationException(
                $"A WorldSceneBuilder-hez nincs Base prefab rendelve: {name}");

        Clear();

        _root = new GameObject("GeneratedWorld").transform;
        _root.SetParent(transform, false);

        CreateGround(layout.Bounds);
        CreateBoundary(layout.Bounds);
        CreateBases(layout, players, localPlayerId, terrainSettings != null ? terrainSettings.ToConfig() : null);
        CreateTerrainFeatures(layout, terrainSettings);
        ApplyCameraBounds(layout.Bounds);

        return _builtBases;
    }

    public void Clear()
    {
        _builtBases.Clear();

        if (_root != null)
        {
            Destroy(_root.gameObject);
            _root = null;
        }
    }

    private void CreateBases(
        WorldLayout layout,
        PlayerRegistry players,
        int localPlayerId,
        WorldGenerationConfig config)
    {
        Transform parent = new GameObject("Bases").transform;
        parent.SetParent(_root, false);

        foreach (BaseLayout baseLayout in layout.Bases)
        {
            BaseController baseController = Instantiate(
                basePrefab,
                baseLayout.Position,
                Quaternion.identity,
                parent);

            baseController.name = $"Base_{baseLayout.BaseId}";

            DisableTestInput(baseController);

            if (baseLayout.OwnerType != BaseOwnerType.Player)
                ShowTurrets(baseController, config != null ? config.GetYellowTurretCount(baseLayout.Strength) : 0);

            if (baseController.TryGetComponent(out BaseOwnerView ownerView))
                ownerView.SetLocalPlayer(localPlayerId);

            IResourceWallet wallet = null;

            if (baseLayout.OwnerType == BaseOwnerType.Player)
                wallet = players.GetPlayer(baseLayout.PlayerId);

            baseController.Initialize(
                baseLayout.BaseId,
                new BaseOwner(baseLayout.OwnerType, baseLayout.PlayerId),
                baseLayout.Position,
                wallet);

            _builtBases.Add(baseController);
        }
    }

    /// <summary>
    /// IDEIGLENES: a Base prefab tartalmazza a BaseTestInput tesztkomponenst,
    /// amely saját pénztárcát állítana be és minden bázison saját GUI-t
    /// rajzolna. Generált világban ezt kikapcsoljuk. Ha a tesztkomponens
    /// lekerül a prefabról, ez a metódus törölhető.
    /// </summary>
    private static void DisableTestInput(BaseController baseController)
    {
        if (baseController.TryGetComponent(out BaseTestInput testInput))
            testInput.enabled = false;
    }

    /// <summary>
    /// IDEIGLENES, amíg a Base prefab nem példányosítható turretszámmal: a
    /// sárga bázison a prefab 12 turretjéből (Turrets/Turret_1–Turret_12) az
    /// első count darabot kapcsolja be, a többit ki. A számozás a saroktól
    /// kifelé, felváltva tölti meg a két sort. A kezdőbázis nem változik.
    /// Ha a prefab maga kezeli a turretszámot, ez a metódus a hívásával együtt
    /// törölhető, és a számot a prefabnak kell átadni.
    /// </summary>
    private static void ShowTurrets(BaseController baseController, int count)
    {
        Transform turrets = baseController.transform.Find("Turrets");

        if (turrets == null)
            return;

        turrets.gameObject.SetActive(count > 0);

        foreach (Transform turret in turrets)
        {
            const string prefix = "Turret_";
            int number = 0;

            bool numbered = turret.name.StartsWith(prefix, StringComparison.Ordinal) &&
                            int.TryParse(turret.name.Substring(prefix.Length), out number);

            turret.gameObject.SetActive(numbered && number <= count);
        }
    }

    /// <summary>
    /// A sárga bázisok előtti védőegységek létrehozása a rögzített helyeken,
    /// a bázistól kifelé (lefelé) nézve, sárga tulajdonossal. Az egységek még
    /// nincsenek inicializálva: az azonosító kiosztása és a játékállapotba
    /// regisztrálás a hívó (GameBootstrapper) feladata.
    /// </summary>
    public IReadOnlyList<UnitGroupMember> SpawnDefenders(WorldLayout layout, WorldGenerationSettings settings)
    {
        if (layout == null)
            throw new ArgumentNullException(nameof(layout));

        List<UnitGroupMember> spawned = new List<UnitGroupMember>(layout.Defenders.Count);

        if (layout.Defenders.Count == 0)
            return spawned;

        if (settings == null)
            throw new InvalidOperationException(
                "A layout védőegységeket tartalmaz, de nincs WorldGenerationSettings megadva.");

        if (_root == null)
            throw new InvalidOperationException("A védőegységek előtt a világot fel kell építeni (Build).");

        Dictionary<string, UnitGroupMember> prefabs =
            new Dictionary<string, UnitGroupMember>(StringComparer.OrdinalIgnoreCase);

        foreach (DefenderUnitEntry entry in settings.DefenderUnits)
        {
            if (entry != null && entry.prefab != null)
                prefabs[entry.id] = entry.prefab;
        }

        Transform parent = new GameObject("Defenders").transform;
        parent.SetParent(_root, false);

        foreach (DefenderLayout defender in layout.Defenders)
        {
            if (!prefabs.TryGetValue(defender.UnitTypeId, out UnitGroupMember prefab))
                throw new InvalidOperationException(
                    $"Nincs prefab a védőegység-típushoz: {defender.UnitTypeId}");

            UnitGroupMember unit = Instantiate(
                prefab,
                new Vector3(defender.Position.x, defender.Position.y, prefab.transform.position.z),
                prefab.transform.rotation,
                parent);

            unit.name = $"Defender_{defender.BaseId}_{defender.SlotIndex}_{defender.UnitTypeId}";
            unit.SetOwner(BaseOwnerType.Yellow, 0);

            // A bázistól kifelé, a pálya „lefelé” iránya felé néz (0° = fel).
            if (unit.TryGetComponent(out DirectionalSpriteRenderer spriteRenderer))
                spriteRenderer.SetAngle(180f);

            spawned.Add(unit);
        }

        return spawned;
    }

    private void CreateTerrainFeatures(WorldLayout layout, WorldGenerationSettings terrainSettings)
    {
        if (layout.Obstacles.Count == 0 && layout.Decorations.Count == 0)
            return;

        if (terrainSettings == null)
            throw new InvalidOperationException(
                "A layout tereptárgyakat tartalmaz, de nincs WorldGenerationSettings megadva " +
                "a grafikájukhoz.");

        Dictionary<string, TerrainFeatureDefinition> definitions =
            new Dictionary<string, TerrainFeatureDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (TerrainFeatureDefinition definition in terrainSettings.ObstacleDefinitions)
            definitions[definition.Id] = definition;

        foreach (TerrainFeatureDefinition definition in terrainSettings.DecorationDefinitions)
            definitions[definition.Id] = definition;

        Transform obstacleParent = new GameObject("Obstacles").transform;
        obstacleParent.SetParent(_root, false);

        foreach (TerrainFeatureLayout obstacle in layout.Obstacles)
        {
            TerrainFeatureDefinition definition = GetDefinition(definitions, obstacle.TypeId);
            GameObject instance = CreateFeature(definition, obstacle, obstacleParent);

            // Ugyanaz az alapterület, amellyel a generátor és a járhatósági rács
            // számolt; a lépték a transformból érvényesül.
            PolygonCollider2D collider = instance.AddComponent<PolygonCollider2D>();
            collider.points = ToArray(definition.ToType().Footprint.Points);
            instance.AddComponent<MovementObstacle>();
        }

        Transform decorationParent = new GameObject("Decorations").transform;
        decorationParent.SetParent(_root, false);

        foreach (TerrainFeatureLayout decoration in layout.Decorations)
        {
            TerrainFeatureDefinition definition = GetDefinition(definitions, decoration.TypeId);
            CreateFeature(definition, decoration, decorationParent);
        }
    }

    private static GameObject CreateFeature(
        TerrainFeatureDefinition definition,
        TerrainFeatureLayout feature,
        Transform parent)
    {
        GameObject instance = new GameObject($"{feature.TypeId}_{feature.Variant}");
        instance.transform.SetParent(parent, false);
        instance.transform.position = feature.Position;
        instance.transform.localScale = new Vector3(feature.Scale, feature.Scale, 1f);

        SpriteRenderer spriteRenderer = instance.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = definition.GetSprite(feature.Variant);
        spriteRenderer.sortingOrder = definition.SortingOrder;

        return instance;
    }

    private static TerrainFeatureDefinition GetDefinition(
        Dictionary<string, TerrainFeatureDefinition> definitions,
        string typeId)
    {
        if (!definitions.TryGetValue(typeId, out TerrainFeatureDefinition definition))
            throw new InvalidOperationException(
                $"Ismeretlen tereptárgy-típus a layoutban: {typeId}");

        return definition;
    }

    private static Vector2[] ToArray(IReadOnlyList<Vector2> points)
    {
        Vector2[] array = new Vector2[points.Count];

        for (int i = 0; i < array.Length; i++)
            array[i] = points[i];

        return array;
    }

    private void CreateGround(WorldBounds bounds)
    {
        if (groundSprite == null)
            return;

        GameObject ground = new GameObject("Ground");
        ground.transform.SetParent(_root, false);

        SpriteRenderer spriteRenderer = ground.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = groundSprite;
        spriteRenderer.drawMode = SpriteDrawMode.Tiled;
        spriteRenderer.size = new Vector2(bounds.Size, bounds.Size);
        spriteRenderer.sortingOrder = groundSortingOrder;
    }

    /// <summary>
    /// Négy, a pályán kívül fekvő fal. A MovementObstacle jelölés miatt az
    /// egységek ütközéskezelése akadályként kezeli őket.
    /// </summary>
    private void CreateBoundary(WorldBounds bounds)
    {
        GameObject boundary = new GameObject("WorldBoundary");
        boundary.transform.SetParent(_root, false);
        boundary.AddComponent<MovementObstacle>();

        float half = bounds.HalfSize;
        float thickness = boundaryThickness;
        float length = bounds.Size + 2f * thickness;
        float offset = half + thickness * 0.5f;

        AddWall(boundary, new Vector2(0f, offset), new Vector2(length, thickness));
        AddWall(boundary, new Vector2(0f, -offset), new Vector2(length, thickness));
        AddWall(boundary, new Vector2(offset, 0f), new Vector2(thickness, length));
        AddWall(boundary, new Vector2(-offset, 0f), new Vector2(thickness, length));
    }

    private static void AddWall(GameObject owner, Vector2 offset, Vector2 size)
    {
        BoxCollider2D wall = owner.AddComponent<BoxCollider2D>();
        wall.offset = offset;
        wall.size = size;
    }

    private void ApplyCameraBounds(WorldBounds bounds)
    {
        CameraController target = cameraController;

        if (target == null && Camera.main != null)
            target = Camera.main.GetComponent<CameraController>();

        if (target == null)
        {
            Debug.LogWarning(
                "Nem található CameraController, a kamera határai nem frissültek.",
                this);
            return;
        }

        target.SetWorldBounds(bounds.Min, bounds.Max);
    }
}
