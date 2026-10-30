using System;
using UnityEngine;


public class BaseController : MonoBehaviour
{
    public static readonly string[] ResourceNames =
{
        "Szén",
        "Acél",
        "Olaj",
        "Beton"
    };


    [SerializeField] private int baseId;
    [SerializeField] private BaseOwnerType startingOwnerType;
    [SerializeField] private int startingPlayerId;

    [SerializeField] private BaseSettings settings;
    [SerializeField] private BuildingView[] buildingViews;

    private BaseData data;
    private IResourceWallet wallet;

    public BaseData Data => data;

    public bool IsInitialized => data != null;

    // A GameBootstrapper hívja meg a játék inicializálásakor.
    public void InitializeFromSceneConfiguration(PlayerRegistry players)
    {
        if (players == null)
            throw new ArgumentNullException(nameof(players));

        BaseOwner owner = CreateStartingOwner();

        IResourceWallet assignedWallet = null;

        if (owner.Type == BaseOwnerType.Player)
        {
            PlayerState player = players.GetPlayer(owner.PlayerId);
            assignedWallet = player;
        }

        Initialize(
            baseId,
            owner,
            transform.position,
            assignedWallet
        );
    }

    // Világgenerálás esetén is ezt lehet majd használni.
    public void Initialize(int id,BaseOwner owner,Vector2 position,IResourceWallet resourceWallet)
    {
        if (IsInitialized)
        {
            throw new InvalidOperationException(
                $"A bázis már inicializálva van. BaseId: {data.BaseId}");
        }

        if (owner == null)
            throw new ArgumentNullException(nameof(owner));

        if (settings == null)
        {
            throw new InvalidOperationException(
                $"A bázishoz nincs BaseSettings hozzárendelve: {name}");
        }

        wallet = resourceWallet;

        transform.position = new Vector3(
            position.x,
            position.y,
            transform.position.z
        );

        data = new BaseData(id, owner, settings);

        UpdateBuildingViews();
    }

    private BaseOwner CreateStartingOwner()
    {
        int playerId = startingOwnerType == BaseOwnerType.Player
            ? startingPlayerId
            : 0;

        return new BaseOwner(startingOwnerType, playerId);
    }

    private void Update()
    {
        if (!IsInitialized)
            return;

        data.Tick(Time.deltaTime, wallet);
        UpdateBuildingViews();
    }

    private void OnValidate()
    {
        if (settings == null || settings.buildings == null)
        {
            return;
        }

        for (int i = 0; i < settings.buildings.Length; i++)
        {
            if (settings.buildings[i] != null)
            {
                settings.buildings[i].name = $"{BaseData.GetResource(i)}Building";
            }
        }
    }

    public void SetWallet(IResourceWallet newWallet)
    {
        wallet = newWallet;
    }

    public ActionInfo StartAction(int buildingIndex, BuildingAction action)
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException(
                "A bázis még nincs inicializálva.");
        }

        return data.StartAction(buildingIndex, action, wallet);
    }

    private void UpdateBuildingViews()
    {
        int count = Mathf.Min(buildingViews.Length, data.BuildingCount);

        for (int i = 0; i < count; i++)
        {
            if (buildingViews[i] != null)
            {
                buildingViews[i].SetLevel(data.GetLevel(i));
            }
        }
    }
}
