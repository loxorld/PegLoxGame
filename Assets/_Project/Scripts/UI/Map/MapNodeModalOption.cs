using System;

public readonly struct MapNodeModalOption
{
    public string Label { get; }
    public Action OnSelect { get; }

    public MapNodeModalOption(string label, Action onSelect)
    {
        Label = label;
        OnSelect = onSelect;
    }
}