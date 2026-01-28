// Assets/_Project/Scripts/Utils/CameraWorldRect.cs
using UnityEngine;

/// <summary>
/// Devuelve el rectángulo del mundo que está visible a través del viewport actual de la cámara.
/// Tiene en cuenta el rect de la cámara (cam.rect) y la conversión a coordenadas de mundo.
/// </summary>
public static class CameraWorldRect
{
    public static Rect GetVisibleWorldRect(Camera cam)
    {
        if (cam == null) return new Rect();
        // extremos del viewport (0–1) escalados al rect actual de la cámara
        float xMin = cam.rect.xMin;
        float yMin = cam.rect.yMin;
        float xMax = cam.rect.xMax;
        float yMax = cam.rect.yMax;

        // convertir a coordenadas de mundo
        Vector3 worldMin = cam.ViewportToWorldPoint(new Vector3(xMin, yMin, 0f));
        Vector3 worldMax = cam.ViewportToWorldPoint(new Vector3(xMax, yMax, 0f));
        return Rect.MinMaxRect(worldMin.x, worldMin.y, worldMax.x, worldMax.y);
    }
}
