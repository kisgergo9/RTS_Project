using TMPro;
using UnityEngine;

public class BuildingInfoView : MonoBehaviour
{
    [SerializeField] private TMP_Text ownerText;

    private BaseController controller;
    private BaseOwner shownOwner;

    private void Awake()
    {
        controller = GetComponentInParent<BaseController>();
    }

    private void Update()
    {
        BaseOwner owner = controller.Data.Owner;

        if (owner == shownOwner)
        {
            return;
        }

        shownOwner = owner;
        ownerText.text = GetOwnerText(owner);
    }

    private string GetOwnerText(BaseOwner owner)
    {
        switch (owner.Type)
        {
            case BaseOwnerType.Player:
                return $"Játékos {owner.PlayerId}";

            case BaseOwnerType.Yellow:
                return "Sárga";

            default:
                return "Gazdátlan";
        }
    }
}
