using UnityEngine;

public class BaseController : MonoBehaviour
{
    [SerializeField] private int baseId;
    [SerializeField] private BaseOwnerType startingOwnerType;
    [SerializeField] private int startingPlayerId;

    [SerializeField] private BaseSettings settings;

    [SerializeField] private BuildingView[] buildingViews;

    private BaseData data;
    private IResourceWallet wallet;

    public BaseData Data
    {
        get { return data; }
    }

    private void Awake()
    {
        BaseOwner owner = new BaseOwner(startingOwnerType, startingPlayerId);
        data = new BaseData(baseId, owner, settings);
    }

    private void Update()
    {
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

    public void Initialize(int id, BaseOwner owner, Vector2 position)
    {
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        data = new BaseData(id, owner, settings);
    }

    public void SetWallet(IResourceWallet newWallet)
    {
        wallet = newWallet;
    }

    public ActionInfo StartAction(int buildingIndex, BuildingAction action)
    {
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
                buildingViews[i].SetHighlighted(i == data.ProducingIndex || i == data.UpgradingIndex);
            }
        }
    }
}
