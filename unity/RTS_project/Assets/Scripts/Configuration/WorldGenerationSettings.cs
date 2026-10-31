using UnityEngine;

/// <summary>
/// A procedurális világgenerálás szerkeszthető beállításai.
/// Az alapértékek javaslatok, nem véglegesített játékszabályok.
/// A Base prefab mérete kb. 12 x 4,2 egység, a kijáratai legfeljebb kb.
/// 5,5 egységre, a tornyok felderítési területe 18 x 10 egységre terjed ki.
/// </summary>
[CreateAssetMenu(
    fileName = "WorldGenerationSettings",
    menuName = "Game/Configuration/World Generation Settings")]
public sealed class WorldGenerationSettings : ScriptableObject
{
    [Header("Players")]
    [SerializeField, Min(1)]
    private int minPlayerCount = 1;

    [SerializeField, Min(1)]
    private int maxPlayerCount = 10;

    [Header("Bases")]
    [Tooltip("Az összes bázis (játékos + sárga) maximális száma a legnagyobb világban.")]
    [SerializeField, Min(2)]
    private int maxBaseCount = 150;

    [Tooltip("Két bázis középpontja közötti minimális távolság.")]
    [SerializeField, Min(0.1f)]
    private float minBaseDistance = 24f;

    [Tooltip("A bázis körüli szabad terület sugara (megjelenési pontok, APC-foglalási terület). " +
             "A bázisok középpontja legalább ennyire van a pálya szélétől.")]
    [SerializeField, Min(0.1f)]
    private float baseClearRadius = 9f;

    [Tooltip("A sárga bázisok abszolút minimuma, játékosszámtól függetlenül.")]
    [SerializeField, Min(1)]
    private int minYellowBaseCount = 1;

    [Tooltip("Játékosonként legalább ennyi sárga bázis (felfelé kerekítve).")]
    [SerializeField, Min(0f)]
    private float yellowBasesPerPlayer = 1f;

    [Header("Obstacles (reserved space, not placed yet)")]
    [SerializeField, Min(0)]
    private int minObstacleCount = 0;

    [SerializeField, Min(0)]
    private int maxObstacleCount = 50;

    [Tooltip("Egy terepakadály helyigényének sugara.")]
    [SerializeField, Min(0.1f)]
    private float obstacleClearRadius = 8f;

    [Header("World Size")]
    [Tooltip("A négyzetes világ oldalhosszának abszolút minimuma.")]
    [SerializeField, Min(1)]
    private int absoluteMinWorldSize = 80;

    [Tooltip("A számított minimális és maximális világméretet ennek többszörösére kerekítjük felfelé.")]
    [SerializeField, Min(1)]
    private int worldSizeStep = 10;

    [Tooltip("A használható terület legfeljebb ekkora hányadát foglalják el a bázisok és akadályok. " +
             "Kisebb érték tágasabb világot és megbízhatóbb elhelyezést ad.")]
    [SerializeField, Range(0.05f, 0.5f)]
    private float targetFillRatio = 0.3f;

    [Header("Placement")]
    [SerializeField, Min(1)]
    private int maxPlacementAttemptsPerBase = 200;

    [SerializeField, Min(1)]
    private int maxGenerationAttempts = 10;

    public WorldGenerationConfig ToConfig()
    {
        return new WorldGenerationConfig(
            minPlayerCount,
            maxPlayerCount,
            maxBaseCount,
            minBaseDistance,
            baseClearRadius,
            minYellowBaseCount,
            yellowBasesPerPlayer,
            minObstacleCount,
            maxObstacleCount,
            obstacleClearRadius,
            absoluteMinWorldSize,
            worldSizeStep,
            targetFillRatio,
            maxPlacementAttemptsPerBase,
            maxGenerationAttempts);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (maxPlayerCount < minPlayerCount)
            maxPlayerCount = minPlayerCount;

        if (maxObstacleCount < minObstacleCount)
            maxObstacleCount = minObstacleCount;
    }
#endif
}
