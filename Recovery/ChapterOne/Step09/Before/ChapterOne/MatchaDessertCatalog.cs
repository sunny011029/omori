using System;
using UnityEngine;

[Serializable]
public sealed class MatchaDessertDefinition
{
    public string id;
    public string displayName;
    public Sprite sprite;
    public bool availableAtStart;
    public string storyCharacterId;
    public string storyCharacterName;
}

// Collection definitions only; crafting and story completion unlocks are a later step.
public sealed class MatchaDessertCatalog : ScriptableObject
{
    public MatchaDessertDefinition[] desserts;
}
