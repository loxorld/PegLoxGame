using System;

public readonly struct MapNodeModalOption
{
    public string Label { get; }
    public Action OnSelect { get; }
    public bool IsEnabled { get; }

    public MapNodeModalOption(string label, Action onSelect, bool isEnabled = true)
    {
        Label = label;
        OnSelect = onSelect;
        IsEnabled = isEnabled;
    }
}