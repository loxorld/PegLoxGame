using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Board/Board Layout", fileName = "BoardLayout_")]
public class BoardLayout : ScriptableObject
{
    public enum LayoutPattern
    {
        Mask = 0,
        Full = 1,
        Checker = 2,
        Diamond = 3,
        TwoIslands = 4,
        Ring = 5,
        Hourglass = 6
    }

    [Header("Grid Size")]
    [Min(1)] public int rows = 8;
    [Min(1)] public int cols = 7;

    [Header("Layout Strategy")]
    public LayoutPattern pattern = LayoutPattern.Mask;
    [Range(0.2f, 1f)] public float fill = 0.55f;

    [Header("Mask (rows*cols)")]
    [Tooltip("Solo se usa cuando pattern = Mask. Si es null o tamaño incorrecto, se considera todo activo.")]
    public bool[] activeMask;

    public bool IsActive(int r, int c)
    {
        if (rows <= 0 || cols <= 0)
            return false;

        if (r < 0 || r >= rows || c < 0 || c >= cols)
            return false;

        switch (pattern)
        {
            case LayoutPattern.Full:
                return true;
            case LayoutPattern.Checker:
                return ((r + c) % 2) == 0;
            case LayoutPattern.Diamond:
                return IsDiamond(r, c);
            case LayoutPattern.TwoIslands:
                return IsTwoIslands(r, c);
            case LayoutPattern.Ring:
                return r == 0 || r == rows - 1 || c == 0 || c == cols - 1;
            case LayoutPattern.Hourglass:
                return IsHourglass(r, c);
            default:
                return IsMaskCellActive(r, c);
        }
    }

    private bool IsMaskCellActive(int r, int c)
    {
        if (activeMask == null || activeMask.Length != rows * cols)
            return true;

        int idx = r * cols + c;
        return activeMask[idx];
    }

    private bool IsDiamond(int r, int c)
    {
        float centerRow = (rows - 1) * 0.5f;
        float centerCol = (cols - 1) * 0.5f;
        float maxDistance = centerRow + centerCol;
        float distance = Mathf.Abs(r - centerRow) + Mathf.Abs(c - centerCol);
        return distance <= maxDistance * fill;
    }

    private bool IsTwoIslands(int r, int c)
    {
        int rowSplit = Mathf.Max(1, rows / 2);
        int colSplit = Mathf.Max(1, cols / 2);

        bool upperLeft = r >= 1 && r < rowSplit && c >= 1 && c < colSplit;
        bool lowerRight = r >= rowSplit && r < rows - 1 && c >= colSplit && c < cols - 1;

        return upperLeft || lowerRight;
    }

    private bool IsHourglass(int r, int c)
    {
        float halfRows = rows * 0.5f;
        float halfCols = cols * 0.5f;
        float rowFromCenter = Mathf.Abs(r + 0.5f - halfRows) / Mathf.Max(1f, halfRows);
        float minColNormalized = rowFromCenter * 0.6f;

        float colNormalized = Mathf.Abs(c + 0.5f - halfCols) / Mathf.Max(1f, halfCols);
        return colNormalized >= minColNormalized;
    }

    public void EnsureMaskSize(bool defaultValue = true)
    {
        int size = Mathf.Max(1, rows * cols);
        if (activeMask != null && activeMask.Length == size)
            return;

        activeMask = new bool[size];
        for (int i = 0; i < activeMask.Length; i++)
            activeMask[i] = defaultValue;
    }
}