using UnityEngine;

[CreateAssetMenu(
    fileName = "NewMovementDefinition",
    menuName = "Game/Configuration/Movement Definition")]
public sealed class MovementDefinition : ScriptableObject
{
    [Header("Movement")]
    [Min(0.01f)]
    [SerializeField]
    private float moveSpeed = 1f;

    [Header("Rotation")]
    [Min(0.01f)]
    [SerializeField]
    private float turnSpeed = 180f;

    public float MoveSpeed => moveSpeed;
    public float TurnSpeed => turnSpeed;

#if UNITY_EDITOR
    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0.01f, moveSpeed);
        turnSpeed = Mathf.Max(0.01f, turnSpeed);
    }
#endif
}