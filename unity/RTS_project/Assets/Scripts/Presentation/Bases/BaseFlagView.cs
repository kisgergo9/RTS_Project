using UnityEngine;

public class BaseFlagView : MonoBehaviour
{
    [SerializeField] private Animator clothAnimator;

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
        ShowFlag(owner);
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
}
