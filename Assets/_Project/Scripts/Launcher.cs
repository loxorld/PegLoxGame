using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Launcher : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D ballPrefab;
    [SerializeField] private Transform launchPoint;

    [Header("Orbs (legacy / optional)")]
    [SerializeField] private OrbData[] orbs;
    [SerializeField] private int selectedOrbIndex = 0;

    [Header("Managers")]
    [SerializeField] private OrbManager orbManager;

    [Header("Launch Settings")]
    [SerializeField] private float launchForce = 10f;
    [SerializeField] private float maxAimMagnitude = 3.0f;

    private Vector2 dragStart;
    private bool isDragging;

    [Header("Trajectory Preview")]
    [SerializeField] private LineRenderer trajectoryLine;
    [SerializeField] private LayerMask collisionMask;          // Pegs + Walls 
    [SerializeField] private int maxBounces = 1;               // 1 rebote visible 
    [SerializeField] private float maxDistancePerSegment = 30f; // largo máximo del primer tramo
    [SerializeField] private float previewBallRadiusFallback = 0.15f;
    [SerializeField] private float maxPreviewSpeed = 30f;      // cap para preview
    [SerializeField] private float bounceTailLength = 2.0f;    // “colita” del rebote

    private float ballRadiusWorld;

    private void Awake()
    {
        ballRadiusWorld = GetBallWorldRadius();
    }

    private void Update()
    {
        HandleOrbSelectionLegacy();

        // Cambiar orbe equipado (Editor)
        if (Keyboard.current != null && orbManager != null)
        {
            if (Keyboard.current.qKey.wasPressedThisFrame) orbManager.PrevOrb();
            if (Keyboard.current.eKey.wasPressedThisFrame) orbManager.NextOrb();
        }

        // Touch (Android)
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;

            if (touch.press.wasPressedThisFrame)
            {
                dragStart = ScreenToWorld(touch.position.ReadValue());
                isDragging = true;
                SetTrajectoryVisible(true);
            }

            if (touch.press.isPressed && isDragging)
            {
                Vector2 current = ScreenToWorld(touch.position.ReadValue());
                Vector2 direction = dragStart - current;
                UpdateTrajectoryPreview(direction);
            }

            if (touch.press.wasReleasedThisFrame && isDragging)
            {
                Vector2 dragEnd = ScreenToWorld(touch.position.ReadValue());
                Vector2 direction = dragStart - dragEnd;

                SetTrajectoryVisible(false);
                ClearTrajectory();

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
            SetTrajectoryVisible(true);
        }

        if (isDragging && Mouse.current.leftButton.isPressed)
        {
            Vector2 current = ScreenToWorld(Mouse.current.position.ReadValue());
            Vector2 direction = dragStart - current;
            UpdateTrajectoryPreview(direction);
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame && isDragging)
        {
            Vector2 dragEnd = ScreenToWorld(Mouse.current.position.ReadValue());
            Vector2 direction = dragStart - dragEnd;

            SetTrajectoryVisible(false);
            ClearTrajectory();

            LaunchBall(direction);
            isDragging = false;
        }
    }

    private void HandleOrbSelectionLegacy()
    {
        // Opcional: el modo real es OrbManager.CurrentOrb
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

        OrbData orb = (orbManager != null) ? orbManager.CurrentOrb : null;

        ShotManager.Instance?.OnShotStarted(orb);

        direction = Vector2.ClampMagnitude(direction, maxAimMagnitude);

        Rigidbody2D ballInstance = Instantiate(ballPrefab, launchPoint.position, Quaternion.identity);

        var ballController = ballInstance.GetComponent<BallController>();
        if (ballController != null)
            ballController.Init(orb);

        ballInstance.linearVelocity = direction * launchForce;
    }

    // ---------------- Trajectory Preview ----------------

    private void SetTrajectoryVisible(bool visible)
    {
        if (trajectoryLine != null)
            trajectoryLine.enabled = visible;
    }

    private void ClearTrajectory()
    {
        if (trajectoryLine != null)
            trajectoryLine.positionCount = 0;
    }

    private void UpdateTrajectoryPreview(Vector2 direction)
    {
        if (trajectoryLine == null || launchPoint == null) return;

        if (ShotManager.Instance != null && (ShotManager.Instance.IsGameOver || ShotManager.Instance.ShotInProgress))
        {
            ClearTrajectory();
            return;
        }

        direction = Vector2.ClampMagnitude(direction, maxAimMagnitude);

        if (direction.sqrMagnitude < 0.0001f)
        {
            ClearTrajectory();
            return;
        }

        // Velocidad (coherente con LaunchBall)
        Vector2 velocity = direction * launchForce;

        // Cap para preview
        if (velocity.magnitude > maxPreviewSpeed)
            velocity = velocity.normalized * maxPreviewSpeed;

        Vector2 pos = launchPoint.position;
        Vector2 dir = velocity.normalized;

        // tramo inicial proporcional a potencia
        float power01 = Mathf.InverseLerp(0f, maxPreviewSpeed, velocity.magnitude);
        float firstLen = Mathf.Lerp(3f, maxDistancePerSegment, power01);

        float radius = (ballRadiusWorld > 0f) ? ballRadiusWorld : previewBallRadiusFallback;

        RaycastHit2D hit = Physics2D.CircleCast(
            pos,
            radius,
            dir,
            firstLen,
            collisionMask
        );

        List<Vector3> points = new List<Vector3>(4);
        points.Add(pos);

        if (hit.collider == null)
        {
            points.Add(pos + dir * firstLen);
            ApplyLine(points);
            return;
        }

        Vector2 hitPoint = hit.point;
        points.Add(hitPoint);

        if (maxBounces > 0)
        {
            Vector2 bounceDir = Vector2.Reflect(dir, hit.normal).normalized;

            // arrancamos fuera del collider
            Vector2 bounceStart = hitPoint + hit.normal * (radius + 0.01f);
            points.Add(bounceStart + bounceDir * bounceTailLength);
        }

        ApplyLine(points);
    }

    private void ApplyLine(List<Vector3> points)
    {
        trajectoryLine.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
            trajectoryLine.SetPosition(i, points[i]);
    }

    private float GetBallWorldRadius()
    {
        if (ballPrefab == null) return previewBallRadiusFallback;

        var circle = ballPrefab.GetComponent<CircleCollider2D>();
        if (circle == null) return previewBallRadiusFallback;

        // Prefab suele tener escala uniforme
        float scale = ballPrefab.transform.localScale.x;
        return circle.radius * scale;
    }
}
