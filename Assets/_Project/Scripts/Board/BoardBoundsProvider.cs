using UnityEngine;

public class BoardBoundsProvider : MonoBehaviour
{
    private Rect playableBounds;
    private bool hasBounds;

    public void SetBounds(Rect bounds)
    {
        playableBounds = bounds;
        hasBounds = true;
    }

    public bool TryGetBounds(out Rect bounds)
    {
        bounds = playableBounds;
        return hasBounds;
    }
}