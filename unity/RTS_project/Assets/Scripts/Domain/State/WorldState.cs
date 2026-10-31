using System;
using System.Collections.Generic;

public sealed class WorldState
{
    private readonly Dictionary<int, BaseData> _bases = new();
    private readonly Dictionary<int, UnitState> _units = new();

    public int BaseCount => _bases.Count;
    public int UnitCount => _units.Count;

    public IReadOnlyCollection<BaseData> Bases => _bases.Values;
    public IReadOnlyCollection<UnitState> Units => _units.Values;

    // A generált világ pillanatképe (seed, beállítások, bázispozíciók) a
    // mentéshez és a kliensek szinkronizálásához. Kézzel összeállított,
    // nem generált jelenet esetén null.
    public WorldGenerationState Generation { get; private set; }

    public void SetGeneration(WorldGenerationState generation)
    {
        if (generation == null)
            throw new ArgumentNullException(nameof(generation));

        if (Generation != null)
            throw new InvalidOperationException(
                "A világ generálási állapota már be van állítva.");

        Generation = generation;
    }

    // Bázisok

    public void RegisterBase(BaseData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        if (_bases.ContainsKey(data.BaseId))
            throw new InvalidOperationException(
                $"Már létezik ilyen BaseId: {data.BaseId}");

        _bases.Add(data.BaseId, data);
    }

    public bool TryGetBase(int id, out BaseData data)
    {
        return _bases.TryGetValue(id, out data);
    }

    public BaseData GetBase(int id)
    {
        if (!_bases.TryGetValue(id, out BaseData data))
            throw new KeyNotFoundException(
                $"Nem található ilyen bázis: {id}");

        return data;
    }

    public bool UnregisterBase(int id)
    {
        return _bases.Remove(id);
    }

    public bool ContainsBase(int id)
    {
        return _bases.ContainsKey(id);
    }

    // Egységek

    public void RegisterUnit(UnitState unit)
    {
        if (unit == null)
            throw new ArgumentNullException(nameof(unit));

        if (_units.ContainsKey(unit.UnitId))
            throw new InvalidOperationException(
                $"Már létezik ilyen UnitId: {unit.UnitId}");

        _units.Add(unit.UnitId, unit);
    }

    public bool TryGetUnit(int id, out UnitState unit)
    {
        return _units.TryGetValue(id, out unit);
    }

    public UnitState GetUnit(int id)
    {
        if (!_units.TryGetValue(id, out UnitState unit))
            throw new KeyNotFoundException(
                $"Nem található ilyen egység: {id}");

        return unit;
    }

    public bool UnregisterUnit(int id)
    {
        return _units.Remove(id);
    }

    public bool ContainsUnit(int id)
    {
        return _units.ContainsKey(id);
    }
}