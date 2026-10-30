using System;

public sealed class UnitIdAllocator
{
    private int _nextId = 1;

    public int Allocate()
    {
        if (_nextId == int.MaxValue)
            throw new InvalidOperationException(
                "Elfogytak a kiosztható egységazonosítók.");

        return _nextId++;
    }
}