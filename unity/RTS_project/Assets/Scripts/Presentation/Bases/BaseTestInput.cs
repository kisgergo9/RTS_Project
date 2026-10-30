using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BaseController))]
public class BaseTestInput : MonoBehaviour
{
    [SerializeField] private int startingConcrete = 100000;

    private readonly Key[] productionKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
    private readonly Key[] upgradeKeys = { Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8 };

    private readonly string[] resourceNames = { "Szén", "Acél", "Olaj", "Beton" };

    private BaseController controller;
    private IResourceWallet wallet;
    private string lastMessage = "";

    private void Start()
    {
        controller = GetComponent<BaseController>();

        wallet = new SimpleWallet();
        wallet.Add(ResourceType.Concrete, startingConcrete);
        controller.SetWallet(wallet);
    }

    private void Update()
    {
        HandleInput();
    }

    private void HandleInput()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        for (int i = 0; i < productionKeys.Length; i++)
        {
            if (Keyboard.current[productionKeys[i]].wasPressedThisFrame)
            {
                StartAction(i, BuildingAction.Produce);
            }

            if (Keyboard.current[upgradeKeys[i]].wasPressedThisFrame)
            {
                StartAction(i, BuildingAction.Upgrade);
            }
        }
    }

    private void StartAction(int buildingIndex, BuildingAction action)
    {
        ActionInfo info = controller.StartAction(buildingIndex, action);
        string actionName = action == BuildingAction.Produce ? "termelés" : "fejlesztés";

        lastMessage = $"{resourceNames[buildingIndex]}, {actionName}: {GetInfoText(info)}";
    }

    private void OnGUI()
    {
        if (wallet == null)
        {
            return;
        }

        BaseData data = controller.Data;

        GUILayout.BeginArea(new Rect(10, 10, 380, 170), GUI.skin.box);

        for (int i = 0; i < data.BuildingCount; i++)
        {
            GUILayout.Label($"{resourceNames[i]}: {wallet.GetAmount(BaseData.GetResource(i))} | {GetBuildingText(data, i)}");
        }

        GUILayout.Label("1-4: termelés, 5-8: fejlesztés");
        GUILayout.Label(lastMessage);
        GUILayout.EndArea();
    }

    private string GetBuildingText(BaseData data, int buildingIndex)
    {
        string text = $"{data.GetLevel(buildingIndex)}. szint";

        if (buildingIndex == data.ProducingIndex)
        {
            text += $", termel: {Mathf.Ceil(data.ProductionTimeLeft)} mp";
        }

        if (buildingIndex == data.UpgradingIndex)
        {
            text += $", fejlődik: {Mathf.Ceil(data.UpgradeTimeLeft)} mp";
        }

        return text;
    }

    private string GetInfoText(ActionInfo info)
    {
        switch (info)
        {
            case ActionInfo.Started:
                return "elindult";

            case ActionInfo.ActionRunning:
                return "ilyen művelet már megy a bázison";

            case ActionInfo.MaxLevelReached:
                return "elérte a legmagasabb szintet";

            case ActionInfo.NotEnoughConcrete:
                return "nincs elég beton";

            default:
                return info.ToString();
        }
    }
}
