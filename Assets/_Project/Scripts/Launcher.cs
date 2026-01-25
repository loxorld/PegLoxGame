using UnityEngine;
using UnityEngine.InputSystem;

public class Launcher : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D ballPrefab;
    [SerializeField] private Transform launchPoint;

    [Header("Orbs")]
    [SerializeField] private OrbData[] orbs;
    [SerializeField] private int selectedOrbIndex = 0;
    [SerializeField] private OrbManager orbManager;

    [Header("Launch Settings")]
    [SerializeField] private float launchForce = 10f;

    private Vector2 dragStart;
    private bool isDragging;


    void Update()
    {
        HandleOrbSelection();

        // Touch (Android)
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;

            if (touch.press.wasPressedThisFrame)
            {
                dragStart = ScreenToWorld(touch.position.ReadValue());
                isDragging = true;
            }

            if (touch.press.wasReleasedThisFrame && isDragging)
            {
                Vector2 dragEnd = ScreenToWorld(touch.position.ReadValue());
                Vector2 direction = dragStart - dragEnd;

                LaunchBall(direction);
                isDragging = false;
            }

            return;
        }

        // Mouse (Editor)
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            dragStart = ScreenToWorld(Mouse.current.position.ReadValue());
            isDragging = true;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame && isDragging)
        {
            Vector2 dragEnd = ScreenToWorld(Mouse.current.position.ReadValue());
            Vector2 direction = dragStart - dragEnd;

            LaunchBall(direction);
            isDragging = false;
        }
    }

    private void HandleOrbSelection()
    {
        if (Keyboard.current == null || orbs == null || orbs.Length == 0) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) selectedOrbIndex = 0;
        if (Keyboard.current.digit2Key.wasPressedThisFrame) selectedOrbIndex = 1;
        if (Keyboard.current.digit3Key.wasPressedThisFrame) selectedOrbIndex = 2;

        selectedOrbIndex = Mathf.Clamp(selectedOrbIndex, 0, orbs.Length - 1);
    }

    private Vector2 ScreenToWorld(Vector2 screenPos)
    {
        return Camera.main.ScreenToWorldPoint(screenPos);
    }

    private void LaunchBall(Vector2 direction)
    {
        if (ShotManager.Instance != null && ShotManager.Instance.IsGameOver)
            return;

        if (ShotManager.Instance != null && ShotManager.Instance.ShotInProgress)
            return;

        PegManager.Instance.ResetAllPegs();

        // Orbe actual del run (modo B)
        OrbData orb = (orbManager != null) ? orbManager.CurrentOrb : null;

        // Iniciar tiro con contexto
        ShotManager.Instance?.OnShotStarted(orb);

        direction = Vector2.ClampMagnitude(direction, 3.0f);

        Rigidbody2D ballInstance = Instantiate(ballPrefab, launchPoint.position, Quaternion.identity);

        // Inicializar bola con orbe seleccionado
        var ballController = ballInstance.GetComponent<BallController>();
        if (ballController != null)
            ballController.Init(orb);

        
        ballInstance.linearVelocity = direction * launchForce;
    }



    private OrbData GetSelectedOrb()
    {
        if (orbs == null || orbs.Length == 0) return null;
        selectedOrbIndex = Mathf.Clamp(selectedOrbIndex, 0, orbs.Length - 1);
        return orbs[selectedOrbIndex];
    }
}
