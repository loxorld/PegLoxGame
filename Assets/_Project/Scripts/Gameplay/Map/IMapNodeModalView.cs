using System.Collections.Generic;

public interface IMapNodeModalView
{
    void ShowEvent(string title, string description, IReadOnlyList<MapNodeModalOption> options);
    void ShowShop(string title, string description, IReadOnlyList<MapNodeModalOption> options);
    void ShowGeneric(string title, string description, IReadOnlyList<MapNodeModalOption> options);
}