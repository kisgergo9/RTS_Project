using System;
using System.Collections.Generic;

public sealed class GameDataRegistry
{
    private readonly Dictionary<string, GameObjectDefinition> _gameObjects =
        new Dictionary<string, GameObjectDefinition>(
            StringComparer.OrdinalIgnoreCase);

    public int GameObjectCount => _gameObjects.Count;

    public IEnumerable<GameObjectDefinition> GameObjects =>
        _gameObjects.Values;

    public GameDataRegistry(IEnumerable<GameObjectDefinition> definitions)
    {
        if (definitions == null)
        {
            throw new ArgumentNullException(nameof(definitions));
        }

        foreach (GameObjectDefinition definition in definitions)
        {
            Register(definition);
        }
    }

    private void Register(GameObjectDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentException(
                "A GameDataRegistry nem tartalmazhat null definíciót.");
        }

        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            throw new ArgumentException(
                $"A következõ GameObjectDefinition nem rendelkezik érvényes ID-val: " +
                $"{definition.name}");
        }

        if (_gameObjects.ContainsKey(definition.Id))
        {
            throw new ArgumentException(
                $"Duplikált GameObjectDefinition ID: {definition.Id}");
        }

        _gameObjects.Add(definition.Id, definition);
    }

    public bool TryGetGameObject(
        string id,
        out GameObjectDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            definition = null;
            return false;
        }

        return _gameObjects.TryGetValue(id, out definition);
    }

    public GameObjectDefinition GetGameObject(string id)
    {
        if (TryGetGameObject(id, out GameObjectDefinition definition))
        {
            return definition;
        }

        throw new KeyNotFoundException(
            $"Nem található GameObjectDefinition ezzel az ID-val: {id}");
    }

    public bool ContainsGameObject(string id)
    {
        return !string.IsNullOrWhiteSpace(id) &&
               _gameObjects.ContainsKey(id);
    }
}