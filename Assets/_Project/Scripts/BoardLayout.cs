using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Board/Board Layout", fileName = "BoardLayout_")]
public class BoardLayout : ScriptableObject
{
    [Header("Grid Size")]
    public int rows = 8;
    public int cols = 7;

    [Header("Mask (rows*cols)")]
    [Tooltip("Si es null o tamaño incorrecto, se considera todo activo.")]
    public bool[] activeMask;

    public bool IsActive(int r, int c)
    {
        if (rows <= 0 || cols <= 0) return false;

        if (activeMask == null || activeMask.Length != rows * cols)
            return true;

        int idx = r * cols + c;
        return idx >= 0 && idx < activeMask.Length && activeMask[idx];
    }

    public void EnsureMaskSize(bool defaultValue = true)
    {
        int size = Mathf.Max(1, rows * cols);

        if (activeMask == null || activeMask.Length != size)
        {
            activeMask = new bool[size];
            for (int i = 0; i < activeMask.Length; i++)
                activeMask[i] = defaultValue;
        }
    }

    private void SetAll(bool value)
    {
        EnsureMaskSize(false);
        for (int i = 0; i < activeMask.Length; i++)
            activeMask[i] = value;
        MarkDirty();
    }

    private void MarkDirty()
    {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private void SetCell(int r, int c, bool value)
    {
        if (r < 0 || r >= rows || c < 0 || c >= cols) return;
        EnsureMaskSize(false);
        activeMask[r * cols + c] = value;
    }

    // -------------------- GENERADORES (ContextMenu) --------------------

    [ContextMenu("Mask/Generate FULL")]
    public void GenerateFull()
    {
        SetAll(true);
    }

    [ContextMenu("Mask/Generate EMPTY")]
    public void GenerateEmpty()
    {
        SetAll(false);
    }

    [ContextMenu("Mask/Generate CHECKER (Ajedrez)")]
    public void GenerateChecker()
    {
        SetAll(false);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                SetCell(r, c, ((r + c) % 2 == 0));

        MarkDirty();
    }

    [ContextMenu("Mask/Generate DIAMOND")]
    public void GenerateDiamond()
    {
        SetAll(false);

        // Centro de grilla (puede caer entre celdas si es par)
        float cr = (rows - 1) * 0.5f;
        float cc = (cols - 1) * 0.5f;

        // Radio Manhattan máximo para cubrir bien la grilla
        float maxDist = cr + cc;

        // Ajuste: cuanto más chico, más “apretado” el diamante.
        // 0.55 suele quedar bien para 8x7.
        float fill = 0.55f;

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                float d = Mathf.Abs(r - cr) + Mathf.Abs(c - cc);
                if (d <= maxDist * fill)
                    SetCell(r, c, true);
            }

        MarkDirty();
    }

    [ContextMenu("Mask/Generate ISLANDS (2 islas)")]
    public void GenerateTwoIslands()
    {
        SetAll(false);

        // Isla izquierda arriba
        FillRect(1, 1, rows / 2, cols / 2, true);

        // Isla derecha abajo
        FillRect(rows / 2, cols / 2, rows - 2, cols - 2, true);

        // Abrimos un par de agujeros para que no quede bloque sólido
        PunchHoles(3);

        MarkDirty();
    }

    [ContextMenu("Mask/Generate RING (Marco)")]
    public void GenerateRing()
    {
        SetAll(false);

        int top = 0;
        int left = 0;
        int bottom = rows - 1;
        int right = cols - 1;

        for (int c = left; c <= right; c++) { SetCell(top, c, true); SetCell(bottom, c, true); }
        for (int r = top; r <= bottom; r++) { SetCell(r, left, true); SetCell(r, right, true); }

        MarkDirty();
    }

    // -------------------- Helpers internos --------------------

    private void FillRect(int r0, int c0, int r1, int c1, bool value)
    {
        int rr0 = Mathf.Clamp(Mathf.Min(r0, r1), 0, rows - 1);
        int rr1 = Mathf.Clamp(Mathf.Max(r0, r1), 0, rows - 1);
        int cc0 = Mathf.Clamp(Mathf.Min(c0, c1), 0, cols - 1);
        int cc1 = Mathf.Clamp(Mathf.Max(c0, c1), 0, cols - 1);

        for (int r = rr0; r <= rr1; r++)
            for (int c = cc0; c <= cc1; c++)
                SetCell(r, c, value);
    }

    private void PunchHoles(int holes)
    {
        // Agujeros random simples (no deterministas)
        for (int i = 0; i < holes; i++)
        {
            int r = Random.Range(0, rows);
            int c = Random.Range(0, cols);
            SetCell(r, c, false);
        }
    }
}
