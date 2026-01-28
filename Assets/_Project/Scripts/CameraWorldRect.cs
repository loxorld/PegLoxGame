using UnityEngine;

public static class CameraWorldRect
{
    /// Devuelve el rect visible en mundo respetando camera.rect (viewport recortado).
    public static Rect GetVisibleWorldRect(Camera cam, float z = 0f)
    {
        if (cam == null) cam = Camera.main;

        // camera.rect está en coordenadas normalizadas de viewport (0..1) pero recortado
        var r = cam.rect;

        Vector3 bl = cam.ViewportToWorldPoint(new Vector3(r.xMin, r.yMin, cam.nearClipPlane));
        Vector3 tr = cam.ViewportToWorldPoint(new Vector3(r.xMax, r.yMax, cam.nearClipPlane));

        float xMin = Mathf.Min(bl.x, tr.x);
        float xMax = Mathf.Max(bl.x, tr.x);
        float yMin = Mathf.Min(bl.y, tr.y);
        float yMax = Mathf.Max(bl.y, tr.y);

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }
}
