using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class Launcher
{
    private void Update()
    {
        if (orbManager == null || relicManager == null)
            ResolveReferences();

        HandleOrbSelectionLegacy();

        if (Keyboard.current != null && orbManager != null)
        {
            if (Keyboard.current.qKey.wasPressedThisFrame)
                orbManager.PrevOrb();

            if (Keyboard.current.eKey.wasPressedThisFrame)
                orbManager.NextOrb();
        }

        bool canAim = GameFlowManager.Instance == null || GameFlowManager.Instance.CanShoot;
        if (!canAim)
        {
            isDragging = false;
            isDragBeyondDeadzone = false;
            SetTrajectoryVisible(false);
            ClearTrajectory();
            return;
        }

        if (Touchscreen.current != null)
        {
            HandleTouchInput(Touchscreen.current.primaryTouch);
            return;
        }

        if (Mouse.current == null)
            return;

        HandleMouseInput(Mouse.current);
    }

    private void HandleTouchInput(TouchControl touch)
    {
        int touchId = touch.touchId.ReadValue();

        if (touch.press.wasPressedThisFrame)
        {
            if (IsPointerOverBlockingUI(touch.position.ReadValue(), touchId))
            {
                CancelDrag();
                return;
            }

            BeginDrag(touch.position.ReadValue());
        }

        if (touch.press.isPressed && isDragging)
            UpdateDragPreview(touch.position.ReadValue());

        if (touch.press.wasReleasedThisFrame && isDragging)
            ReleaseDrag(touch.position.ReadValue(), touchId);
    }

    private void HandleMouseInput(Mouse mouse)
    {
        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (IsPointerOverBlockingUI(mouse.position.ReadValue(), null))
            {
                CancelDrag();
                return;
            }

            BeginDrag(mouse.position.ReadValue());
        }

        if (isDragging && mouse.leftButton.isPressed)
            UpdateDragPreview(mouse.position.ReadValue());

        if (mouse.leftButton.wasReleasedThisFrame && isDragging)
            ReleaseDrag(mouse.position.ReadValue(), null);
    }

    private void BeginDrag(Vector2 screenPosition)
    {
        if (!EnsureCameraAvailable())
        {
            CancelDrag();
            return;
        }

        dragStartScreen = screenPosition;
        dragStartWorld = ScreenToWorld(screenPosition);

        isDragging = true;
        isDragBeyondDeadzone = false;
        SetTrajectoryVisible(false);
        ClearTrajectory();
    }

    private void UpdateDragPreview(Vector2 currentScreen)
    {
        if (!EnsureCameraAvailable())
        {
            CancelDrag();
            return;
        }

        if (IsWithinDeadzone(currentScreen))
        {
            if (isDragBeyondDeadzone)
            {
                isDragBeyondDeadzone = false;
                SetTrajectoryVisible(false);
                ClearTrajectory();
            }

            return;
        }

        if (!isDragBeyondDeadzone)
        {
            isDragBeyondDeadzone = true;
            SetTrajectoryVisible(true);
        }

        Vector2 currentWorld = ScreenToWorld(currentScreen);
        Vector2 directionWorld = dragStartWorld - currentWorld;
        UpdateTrajectoryPreview(directionWorld, currentScreen);
    }

    private void ReleaseDrag(Vector2 releaseScreen, int? pointerId)
    {
        if (!EnsureCameraAvailable())
        {
            CancelDrag();
            return;
        }

        if (IsCancelShot(releaseScreen, pointerId) || IsWithinDeadzone(releaseScreen))
        {
            CancelDrag();
            return;
        }

        Vector2 releaseWorld = ScreenToWorld(releaseScreen);
        Vector2 directionWorld = dragStartWorld - releaseWorld;

        SetTrajectoryVisible(false);
        ClearTrajectory();

        LaunchBall(directionWorld, releaseScreen);
        isDragging = false;
    }

    private bool IsPointerOverBlockingUI(Vector2 screenPosition, int? pointerId)
    {
        if (EventSystem.current == null)
            return false;

        if (pointerId.HasValue && !EventSystem.current.IsPointerOverGameObject(pointerId.Value))
            return false;

        if (!pointerId.HasValue && !EventSystem.current.IsPointerOverGameObject())
            return false;

        var eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject == null)
                continue;

            Selectable selectable = result.gameObject.GetComponentInParent<Selectable>();
            if (selectable != null && selectable.IsInteractable())
                return true;
        }

        return false;
    }

    private bool IsPointerOverAnyUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
            return false;

        var eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }

    private bool IsCancelShot(Vector2 screenPosition, int? pointerId)
    {
        if (cancelShotRect != null)
        {
            Camera uiCamera = ResolveUiCamera(cancelShotRect);
            if (RectTransformUtility.RectangleContainsScreenPoint(cancelShotRect, screenPosition, uiCamera))
                return true;
        }

        if (!cancelShotOnUI || EventSystem.current == null)
            return false;

        if (pointerId.HasValue)
        {
            if (!EventSystem.current.IsPointerOverGameObject(pointerId.Value))
                return false;
        }
        else
        {
            if (!EventSystem.current.IsPointerOverGameObject())
                return false;
        }

        return IsPointerOverAnyUI(screenPosition);
    }

    private static Camera ResolveUiCamera(RectTransform rect)
    {
        if (rect == null)
            return null;

        Canvas canvas = rect.GetComponentInParent<Canvas>();
        if (canvas == null)
            return null;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
    }

    private void HandleOrbSelectionLegacy()
    {
        if (Keyboard.current == null || orbs == null || orbs.Length == 0)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            selectedOrbIndex = 0;

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            selectedOrbIndex = 1;

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
            selectedOrbIndex = 2;

        selectedOrbIndex = Mathf.Clamp(selectedOrbIndex, 0, orbs.Length - 1);
    }

    private Vector2 ScreenToWorld(Vector2 screenPos)
    {
        if (!EnsureCameraAvailable())
            return Vector2.zero;

        return cachedCamera.ScreenToWorldPoint(screenPos);
    }

    private bool IsWithinDeadzone(Vector2 screenPosition)
    {
        if (deadzonePixels <= 0f)
            return false;

        return Vector2.Distance(dragStartScreen, screenPosition) < deadzonePixels;
    }

    private void CancelDrag()
    {
        if (!isDragging)
            return;

        isDragging = false;
        isDragBeyondDeadzone = false;
        SetTrajectoryVisible(false);
        ClearTrajectory();
    }
}
