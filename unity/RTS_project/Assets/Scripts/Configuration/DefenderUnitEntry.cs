using System;
using UnityEngine;

/// <summary>
/// Egy sárga védőcsapatban szerepelhető egységtípus a WorldGenerationSettings-ben:
/// azonosító, prefab és nehézségi érték.
/// </summary>
[Serializable]
public sealed class DefenderUnitEntry
{
    [Tooltip("Egyedi azonosító, a generált elrendezés ezzel hivatkozik a típusra.")]
    public string id;

    [Tooltip("Az egység prefabja (UnitGroupMember komponenssel). APC nem lehet védőegység.")]
    public UnitGroupMember prefab;

    [Tooltip("Ennyit fogyaszt a csapat nehézségi keretéből.")]
    [Min(1)]
    public int difficultyCost = 5;

    public DefenderUnitType ToType()
    {
        return new DefenderUnitType(id, difficultyCost);
    }
}
