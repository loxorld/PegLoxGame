using UnityEngine;

public partial class HUDController
{
    private InventoryOverlayUI inventoryOverlay;

    private void EnsureInventoryOverlay()
    {
        if (rootCanvas == null)
            return;

        if (inventoryOverlay == null)
        {
            Transform existing = FindDescendant(rootCanvas.transform, "InventoryOverlayRuntime");
            if (existing != null)
                inventoryOverlay = existing.GetComponent<InventoryOverlayUI>();

            if (inventoryOverlay == null)
            {
                var overlayObject = new GameObject("InventoryOverlayRuntime", typeof(RectTransform));
                RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
                overlayRect.SetParent(rootCanvas.transform, false);
                overlayRect.anchorMin = new Vector2(0.5f, 0.5f);
                overlayRect.anchorMax = new Vector2(0.5f, 0.5f);
                overlayRect.pivot = new Vector2(0.5f, 0.5f);
                overlayRect.anchoredPosition = Vector2.zero;
                overlayRect.sizeDelta = Vector2.zero;
                inventoryOverlay = overlayObject.AddComponent<InventoryOverlayUI>();
            }
        }

        inventoryOverlay.Bind(rootCanvas, combatHudRoot);
    }
}
