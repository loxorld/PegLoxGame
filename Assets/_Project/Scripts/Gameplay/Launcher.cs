using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Launcher : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D ballPrefab;
    [SerializeField] private Transform launchPoint;
    [SerializeField] private Camera cachedCamera;

    [Header("Orbs (legacy / optional)")]
    [SerializeField] private OrbData[] orbs;
    [SerializeField] private int selectedOrbIndex = 0;

    [Header("Managers")]
    [SerializeField] private OrbManager orbManager;
    [SerializeField] private RelicManager relicManager;

    [Header("Launch Settings")]
    [SerializeField] private float launchForce = 10f; // legacy (no se usa para velocidad, lo dejamos por compat)
    [SerializeField] private float maxAimMagnitude = 3.0f;

    [Header("Drag Tuning (screen space)")]
    [SerializeField, Min(10f)] private float maxDragPixels = 220f;
    [SerializeField] private bool useScreenSpaceDragForPower = true;

    [Header("Launch Speed Cap")]
    [SerializeField] private float maxLaunchSpeed = 18f;
    [SerializeField] private float minLaunchSpeed = 3f;

    [Header("Power Curve")]
    [SerializeField] private AnimationCurve powerCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool clampCurveOutput01 = true;

    private Vector2 dragStartWorld;
    private Vector2 dragStartScreen;
    private bool isDragging;
    private bool hasLoggedMissingCamera;

    [Header("Trajectory Preview")]
    [SerializeField] private LineRenderer trajectoryLine;
    [SerializeField] private LayerMask collisionMask;           // Pegs + Walls
    [SerializeField] private int maxBounces = 1;                // 1 rebote visible
    [SerializeField] private float maxDistancePerSegment = 30f; // largo máximo del primer tramo
    [SerializeField] private float previewBallRadiusFallback = 0.15f;
    [SerializeField] private float maxPreviewSpeed = 30f;       // recomendado = maxLaunchSpeed
    [SerializeField] private float bounceTailLength = 2.0f;

    private float ballRadiusWorld;

    private void Awake()
    {
        ResolveReferences();
        ballRadiusWorld = GetBallWorldRadius();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
    }
#endif

    private void Update()
    {

        if (orbManager == null || relicManager == null)
            ResolveReferences();
        HandleOrbSelectionLegacy();

        // Cambiar orbe equipado (Editor)
        if (Keyboard.current != null && orbManager != null)
        {
            if (Keyboard.current.qKey.wasPressedThisFrame) orbManager.PrevOrb();
            if (Keyboard.current.eKey.wasPressedThisFrame) orbManager.NextOrb();
        }

        bool canAim = (GameFlowManager.Instance == null) || GameFlowManager.Instance.CanShoot;
        if (!canAim)
        {
            isDragging = false;
            SetTrajectoryVisible(false);
            ClearTrajectory();
            return;
        }

        // Touch (Android)
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            int touchId = touch.touchId.ReadValue();

            if (touch.press.wasPressedThisFrame)
            {
                if (IsPointerOverBlockingUI(touch.position.ReadValue(), touchId))
                {
                    isDragging = false;
                    SetTrajectoryVisible(false);
                    ClearTrajectory();
                    return;
                }

                dragStartScreen = touch.position.ReadValue();
                dragStartWorld = ScreenToWorld(dragStartScreen);

                isDragging = true;
                SetTrajectoryVisible(true);
            }

            if (touch.press.isPressed && isDragging)
            {
                Vector2 currentScreen = touch.position.ReadValue();
                Vector2 currentWorld = ScreenToWorld(currentScreen);
                Vector2 directionWorld = dragStartWorld - currentWorld;

                UpdateTrajectoryPreview(directionWorld, currentScreen);
            }

            if (touch.press.wasReleasedThisFrame && isDragging)
            {
                Vector2 releaseScreen = touch.position.ReadValue();
                Vector2 releaseWorld = ScreenToWorld(releaseScreen);
                Vector2 directionWorld = dragStartWorld - releaseWorld;

                SetTrajectoryVisible(false);
                ClearTrajectory();

                LaunchBall(directionWorld, releaseScreen);
                isDragging = false;
            }

            return;
        }

        // Mouse (Editor)
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (IsPointerOverBlockingUI(Mouse.current.position.ReadValue(), null))
            {
                isDragging = false;
                SetTrajectoryVisible(false);
                ClearTrajectory();
                return;
            }

            dragStartScreen = Mouse.current.position.ReadValue();
            dragStartWorld = ScreenToWorld(dragStartScreen);

            isDragging = true;
            SetTrajectoryVisible(true);
        }

        if (isDragging && Mouse.current.leftButton.isPressed)
        {
            Vector2 currentScreen = Mouse.current.position.ReadValue();
            Vector2 currentWorld = ScreenToWorld(currentScreen);
            Vector2 directionWorld = dragStartWorld - currentWorld;

            UpdateTrajectoryPreview(directionWorld, currentScreen);
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame && isDragging)
        {
            Vector2 releaseScreen = Mouse.current.position.ReadValue();
            Vector2 releaseWorld = ScreenToWorld(releaseScreen);
            Vector2 directionWorld = dragStartWorld - releaseWorld;

            SetTrajectoryVisible(false);
            ClearTrajectory();

            LaunchBall(directionWorld, releaseScreen);
            isDragging = false;
        }
    }

    private bool IsPointerOverBlockingUI(Vector2 screenPosition, int? pointerId)
    {
        if (EventSystem.current == null) return false;
        if (pointerId.HasValue && !EventSystem.current.IsPointerOverGameObject(pointerId.Value)) return false;
        if (!pointerId.HasValue && !EventSystem.current.IsPointerOverGameObject()) return false;

        var eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results)
        {
            if (result.gameObject == null) continue;
            var selectable = result.gameObject.GetComponentInParent<Selectable>();
            if (selectable != null && selectable.IsInteractable())
            {
                return true;
            }
        }

        return false;
    }
    private void HandleOrbSelectionLegacy()
    {
        if (Keyboard.current == null || orbs == null || orbs.Length == 0) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) selectedOrbIndex = 0;
        if (Keyboard.current.digit2Key.wasPressedThisFrame) selectedOrbIndex = 1;
        if (Keyboard.current.digit3Key.wasPressedThisFrame) selectedOrbIndex = 2;

        selectedOrbIndex = Mathf.Clamp(selectedOrbIndex, 0, orbs.Length - 1);
    }

    private Vector2 ScreenToWorld(Vector2 screenPos)
    {
        if (cachedCamera == null)
        {
            if (!hasLoggedMissingCamera)
            {
                Debug.LogWarning($"{nameof(Launcher)}: No hay cámara asignada para convertir ScreenToWorld.");
                hasLoggedMissingCamera = true;
            }

            return Vector2.zero;
        }

        return cachedCamera.ScreenToWorldPoint(screenPos);
    }

    /// <summary>
    /// Convierte drag -> velocidad cappeada.
    /// Dirección (orientación) viene en world.
    /// Potencia puede venir en pixeles (estable en móvil).
    /// </summary>
    private Vector2 ComputeLaunchVelocity(Vector2 rawDirectionWorld, Vector2 currentScreenPos)
    {
        Vector2 dirWorld = Vector2.ClampMagnitude(rawDirectionWorld, maxAimMagnitude);
        if (dirWorld.sqrMagnitude < 0.0001f) return Vector2.zero;

        float drag01;
        if (useScreenSpaceDragForPower)
        {
            float pixels = Vector2.Distance(dragStartScreen, currentScreenPos);
            drag01 = Mathf.Clamp01(pixels / maxDragPixels);
        }
        else
        {
            drag01 = Mathf.InverseLerp(0f, maxAimMagnitude, dirWorld.magnitude);
        }

        float power01 = powerCurve != null ? powerCurve.Evaluate(drag01) : drag01;
        if (clampCurveOutput01) power01 = Mathf.Clamp01(power01);

        float speed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, power01);
        return dirWorld.normalized * speed;
    }

    private float ComputePower01(Vector2 rawDirectionWorld, Vector2 currentScreenPos)
    {
        Vector2 dirWorld = Vector2.ClampMagnitude(rawDirectionWorld, maxAimMagnitude);
        if (dirWorld.sqrMagnitude < 0.0001f) return 0f;

        float drag01;
        if (useScreenSpaceDragForPower)
        {
            float pixels = Vector2.Distance(dragStartScreen, currentScreenPos);
            drag01 = Mathf.Clamp01(pixels / maxDragPixels);
        }
        else
        {
            drag01 = Mathf.InverseLerp(0f, maxAimMagnitude, dirWorld.magnitude);
        }

        float power01 = powerCurve != null ? powerCurve.Evaluate(drag01) : drag01;
        if (clampCurveOutput01) power01 = Mathf.Clamp01(power01);

        return power01;
    }


    private void LaunchBall(Vector2 directionWorld, Vector2 releaseScreenPos)
    {
        if (GameFlowManager.Instance != null && !GameFlowManager.Instance.CanShoot)
            return;

        if (ShotManager.Instance != null && ShotManager.Instance.IsGameOver)
            return;

        if (ShotManager.Instance != null && ShotManager.Instance.ShotInProgress)
            return;

        ResolveReferences();
        OrbInstance orb = (orbManager != null) ? orbManager.CurrentOrb : null;

        ShotManager.Instance?.OnShotStarted(orb);

        Rigidbody2D ballInstance = Instantiate(ballPrefab, launchPoint.position, Quaternion.identity);

        var ballController = ballInstance.GetComponent<BallController>();
        if (ballController != null)
            ballController.Init(orb);

        Vector2 vel = ComputeLaunchVelocity(directionWorld, releaseScreenPos);
        ballInstance.linearVelocity = vel;
    }


    private void ResolveReferences()
    {
        if (orbManager == null)
            orbManager = OrbManager.Instance ?? FindObjectOfType<OrbManager>(true);

        if (relicManager == null)
            relicManager = RelicManager.Instance ?? FindObjectOfType<RelicManager>(true);

        if (cachedCamera == null)
            cachedCamera = Camera.main;
    }

    private int GetPreviewBounceBonus()
    {
        if (relicManager == null) return 0;

        int bonus = 0;
        var relics = relicManager.ActiveRelics;
        for (int i = 0; i < relics.Count; i++)
        {
            if (relics[i] is IPreviewBounceModifier preview)
                bonus += Mathf.Max(0, preview.BonusPreviewBounces);
        }

        return bonus;
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

    private void UpdateTrajectoryPreview(Vector2 directionWorld, Vector2 currentScreenPos)
    {
        if (trajectoryLine == null || launchPoint == null) return;

        if (ShotManager.Instance != null && (ShotManager.Instance.IsGameOver || ShotManager.Instance.ShotInProgress))
        {
            ClearTrajectory();
            return;
        }

        Vector2 velocity = ComputeLaunchVelocity(directionWorld, currentScreenPos);
        if (velocity.sqrMagnitude < 0.0001f)
        {
            ClearTrajectory();
            return;
        }

        // Cap opcional del preview (recomendado: maxPreviewSpeed = maxLaunchSpeed)
        if (velocity.magnitude > maxPreviewSpeed)
            velocity = velocity.normalized * maxPreviewSpeed;

        Vector2 pos = launchPoint.position;
        Vector2 dir = velocity.normalized;

        float power01 = ComputePower01(directionWorld, currentScreenPos);

        float minLen = 0.8f;
        float firstLen = Mathf.Lerp(minLen, maxDistancePerSegment, power01);


        float radius = (ballRadiusWorld > 0f) ? ballRadiusWorld : previewBallRadiusFallback;

 

        List<Vector3> points = new List<Vector3>(4) { pos };

        int previewBounces = Mathf.Max(0, maxBounces + GetPreviewBounceBonus());
        float minTail = 0.2f;
        float tailT = Mathf.SmoothStep(0f, 1f, power01);
        float tailLen = Mathf.Lerp(minTail, bounceTailLength, tailT);
        float segmentLen = firstLen;

        for (int bounceIndex = 0; bounceIndex <= previewBounces; bounceIndex++)
        {
            RaycastHit2D hit = Physics2D.CircleCast(
                pos,
                radius,
                dir,
                segmentLen,
                collisionMask
            );

            if (hit.collider == null)
            {
                points.Add(pos + dir * segmentLen);
                break;
            }

            Vector2 hitPoint = hit.point;
            points.Add(hitPoint);

            if (bounceIndex == previewBounces)
                break;

            dir = Vector2.Reflect(dir, hit.normal).normalized;
            pos = hitPoint + hit.normal * (radius + 0.01f);
            segmentLen = tailLen;
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

        float scale = ballPrefab.transform.localScale.x;
        return circle.radius * scale;
    }
}
