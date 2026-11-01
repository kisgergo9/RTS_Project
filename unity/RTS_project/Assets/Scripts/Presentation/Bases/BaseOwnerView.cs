using TMPro;
using UnityEngine;

[RequireComponent(typeof(BaseController))]
public class BaseOwnerView : MonoBehaviour
{
    [SerializeField] private Animator clothAnimator;
    [SerializeField] private GameObject wall;
    [SerializeField] private TMP_Text ownerText;

    // A Cloth.controller állapotainak nevei.
    [SerializeField] private string ownFlagState = "FlagClothAnim";
    [SerializeField] private string enemyFlagState = "FlagClothEnemy";
    [SerializeField] private string yellowFlagState = "FlagClothYellow";

    // IDEIGLENES: a helyi játékos azonosítója (később a játék adja meg).
    [SerializeField] private int localPlayerId;

    private BaseController controller;
    private BaseOwner shownOwner;

    private void Awake()
    {
        controller = GetComponent<BaseController>();
    }

    private void Update()
    {
        BaseOwner owner = controller.Data.Owner;

        if (owner == shownOwner)
        {
            return;
        }

        shownOwner = owner;

        ShowFlag(owner);
        ShowWall(owner);
        ShowOwnerText(owner);
    }

    private void ShowFlag(BaseOwner owner)
    {
        string state = null;

        switch (owner.Type)
        {
            case BaseOwnerType.Player:
                state = owner.PlayerId == localPlayerId ? ownFlagState : enemyFlagState;
                break;

            case BaseOwnerType.Yellow:
                state = yellowFlagState;
                break;
        }

        // Gazdátlan bázison nincs zászló.
        clothAnimator.gameObject.SetActive(state != null);

        if (state != null)
        {
            clothAnimator.Play(state);
        }
    }

    // Gazdátlan bázison nincs fal.
    private void ShowWall(BaseOwner owner)
    {
        wall.SetActive(owner.Type != BaseOwnerType.None);
    }

    private void ShowOwnerText(BaseOwner owner)
    {
        switch (owner.Type)
        {
            case BaseOwnerType.Player:
                ownerText.text = $"Játékos {owner.PlayerId}";
                break;

            case BaseOwnerType.Yellow:
                ownerText.text = "Sárga";
                break;

            default:
                ownerText.text = "Gazdátlan";
                break;
        }
    }
}
