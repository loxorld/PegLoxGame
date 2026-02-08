using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Launcher : MonoBehaviour
{
    [System.Serializable]
    public class LaunchSettings
    {
        [Min(10f)] public float maxDragPixels = 220f;
        public AnimationCurve powerCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public bool useDpiNormalization = false;
        public float maxLaunchSpeed = 18f;
        public float minLaunchSpeed = 3f;
    }

    [Header("References")]
    [SerializeField, Tooltip("Prefab con Rigidbody2D usado para disparar. Obligatorio.")]
    private Rigidbody2D ballPrefab;
    [SerializeField, Tooltip("Punto de salida del disparo. Obligatorio.")]
    private Transform launchPoint;
    [SerializeField, Tooltip("Cámara usada para convertir pantalla a mundo. Obligatoria para apuntar.")]
    private Camera cachedCamera;

    [Header("Orbs (legacy / optional)")]
    [SerializeField] private OrbData[] orbs;
    [SerializeField] private int selectedOrbIndex = 0;

    [Header("Managers")]
    [SerializeField] private OrbManager orbManager;
    [SerializeField] private RelicManager relicManager;

    [Header("Launch Settings")]
    [SerializeField] private float launchForce = 10f; // legacy (no se usa para velocidad, lo dejamos por compat)
    [SerializeField] private float maxAimMagnitude = 3.0f;
    [SerializeField] private LaunchSettings launchSettings = new LaunchSettings();

    [Header("Drag Tuning (screen space)")]
    [SerializeField] private bool useScreenSpaceDragForPower = true;
    [SerializeField, Min(1f), Tooltip("DPI usado como referencia cuando la normalización está activa.")]
    private float referenceDpi = 160f;

    [Header("Launch Speed Cap")]
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
    [SerializeField, Min(1)] private int previewSteps = 60;
    [SerializeField, Min(0.001f)] private float previewTimeStep = 0.03f;
    [SerializeField, Min(0)] private int previewCornerVertices = 4;
    [SerializeField, Min(0)] private int previewCapVertices = 4;
    [SerializeField, Tooltip("Gradiente evaluado con power01 para colorear el LineRenderer.")]
    private Gradient trajectoryPowerGradient;
    [SerializeField, Tooltip("Curva evaluada con power01 para escalar el ancho del LineRenderer.")]
    private AnimationCurve trajectoryWidthByPower;
    [SerializeField, Tooltip("Si está activo, se crean valores por defecto para el gradiente/curva cuando no están asignados.")]
    private bool autoConfigurePowerStyling = true;

    private float ballRadiusWorld;
    private readonly List<Vector3> trajectoryPoints = new List<Vector3>(128);
    private Gradient baseTrajectoryGradient;
    private float baseTrajectoryWidthMultiplier;
    private readonly Gradient powerPreviewGradient = new Gradient();
    private readonly GradientColorKey[] powerGradientColorKeys = new GradientColorKey[2];
    private readonly GradientAlphaKey[] powerGradientAlphaKeys = new GradientAlphaKey[2];
    private float effectiveMaxDragPixels;
    private float effectiveMaxLaunchSpeed;
    private float effectiveMinLaunchSpeed;
    private bool effectiveUseDpiNormalization;
    private AnimationCurve effectivePowerCurve;

    private void Awake()
    {
        ResolveReferences();
        ValidateRequiredReferences(nameof(Awake));
        ballRadiusWorld = GetBallWorldRadius();
        ConfigureTrajectoryLine();
        ApplyLaunchSettingsFromPrefs();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ValidateRequiredReferences(nameof(OnEnable));
        ConfigureTrajectoryLine();
        ApplyLaunchSettingsFromPrefs();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
        ConfigureTrajectoryLine();
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

                if (!EnsureCameraAvailable())
                {
                    CancelDrag();
                    return;
                }

                dragStartScreen = touch.position.ReadValue();
                dragStartWorld = ScreenToWorld(dragStartScreen);

                isDragging = true;
                SetTrajectoryVisible(true);
            }

            if (touch.press.isPressed && isDragging)
            {
                if (!EnsureCameraAvailable())
                {
                    CancelDrag();
                    return;
                }

                Vector2 currentScreen = touch.position.ReadValue();
                Vector2 currentWorld = ScreenToWorld(currentScreen);
                Vector2 directionWorld = dragStartWorld - currentWorld;

                UpdateTrajectoryPreview(directionWorld, currentScreen);
            }

            if (touch.press.wasReleasedThisFrame && isDragging)
            {
                if (!EnsureCameraAvailable())
                {
                    CancelDrag();
                    return;
                }

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

            if (!EnsureCameraAvailable())
            {
                CancelDrag();
                return;
            }

            dragStartScreen = Mouse.current.position.ReadValue();
            dragStartWorld = ScreenToWorld(dragStartScreen);

            isDragging = true;
            SetTrajectoryVisible(true);
        }

        if (isDragging && Mouse.current.leftButton.isPressed)
        {
            if (!EnsureCameraAvailable())
            {
                CancelDrag();
                return;
            }

            Vector2 currentScreen = Mouse.current.position.ReadValue();
            Vector2 currentWorld = ScreenToWorld(currentScreen);
            Vector2 directionWorld = dragStartWorld - currentWorld;

            UpdateTrajectoryPreview(directionWorld, currentScreen);
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame && isDragging)
        {
            if (!EnsureCameraAvailable())
            {
                CancelDrag();
                return;
            }

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
        if (!EnsureCameraAvailable())

            return Vector2.zero;
        

        return cachedCamera.ScreenToWorldPoint(screenPos);
    }

    /// <summary>
    /// Convierte drag -> velocidad cappeada.
    /// Dirección (orientación) viene en world.
    /// Potencia puede venir en pixeles (estable en móvil).
    /// </summary>
    private Vector2 ComputeLaunchVelocity(Vector2 rawDirectionWorld, Vector2 currentScreenPos)
    {
        if (!TryComputePowerData(rawDirectionWorld, currentScreenPos, out Vector2 dirWorld, out _, out float power01))
            return Vector2.zero;

        float speed = Mathf.Lerp(effectiveMinLaunchSpeed, effectiveMaxLaunchSpeed, power01);
        return dirWorld.normalized * speed;
    }

    private float ComputePower01(Vector2 rawDirectionWorld, Vector2 currentScreenPos)
    {
        return TryComputePowerData(rawDirectionWorld, currentScreenPos, out _, out _, out float power01)
            ? power01
            : 0f;
    }

    private bool TryComputePowerData(
        Vector2 rawDirectionWorld,
        Vector2 currentScreenPos,
        out Vector2 directionWorld,
        out float drag01,
        out float power01)
    {
        directionWorld = Vector2.ClampMagnitude(rawDirectionWorld, maxAimMagnitude);
        if (directionWorld.sqrMagnitude < 0.0001f)
        {
            drag01 = 0f;
            power01 = 0f;
            return false;
        }

        if (useScreenSpaceDragForPower)
        {
            float pixels = Vector2.Distance(dragStartScreen, currentScreenPos);
            if (effectiveUseDpiNormalization)
            {
                float dpi = Screen.dpi;
                if (dpi > 0f && !float.IsNaN(dpi) && !float.IsInfinity(dpi))
                {
                    pixels *= referenceDpi / dpi;
                }
            }
            drag01 = Mathf.Clamp01(pixels / effectiveMaxDragPixels);
        }
        else
        {
            drag01 = Mathf.InverseLerp(0f, maxAimMagnitude, directionWorld.magnitude);
        }

        power01 = effectivePowerCurve != null ? effectivePowerCurve.Evaluate(drag01) : drag01;
        if (clampCurveOutput01) power01 = Mathf.Clamp01(power01);

        return true;
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
        ValidateRequiredReferences(nameof(LaunchBall));
        if (ballPrefab == null || launchPoint == null)
            return;

        if (!EnsureCameraAvailable())
            return;
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

    private void ApplyLaunchSettingsFromPrefs()
    {
        float sensitivity = PlayerPrefs.GetFloat(LauncherPreferences.DragSensitivityKey, LauncherPreferences.DefaultDragSensitivity);
        sensitivity = Mathf.Max(0.1f, sensitivity);
        effectiveMaxDragPixels = Mathf.Max(1f, launchSettings.maxDragPixels / sensitivity);
        int dpiNormalization = PlayerPrefs.GetInt(
            LauncherPreferences.UseDpiNormalizationKey,
            launchSettings.useDpiNormalization ? 1 : 0
        );
        effectiveUseDpiNormalization = dpiNormalization == 1;
        effectiveMaxLaunchSpeed = Mathf.Max(0f, launchSettings.maxLaunchSpeed);
        effectiveMinLaunchSpeed = Mathf.Clamp(launchSettings.minLaunchSpeed, 0f, effectiveMaxLaunchSpeed);
        effectivePowerCurve = launchSettings.powerCurve;
    }

    private void ValidateRequiredReferences(string context)
    {
        if (ballPrefab == null)
            Debug.LogWarning($"{nameof(Launcher)} ({gameObject.name}): Falta ballPrefab en {context}.");

        if (launchPoint == null)
            Debug.LogWarning($"{nameof(Launcher)} ({gameObject.name}): Falta launchPoint en {context}.");

        EnsureCameraAvailable();
    }

    private bool EnsureCameraAvailable()
    {
        if (cachedCamera != null)
            return true;

        if (!hasLoggedMissingCamera)
        {
            Debug.LogWarning($"{nameof(Launcher)} ({gameObject.name}): No hay cámara asignada para apuntar.");
            hasLoggedMissingCamera = true;
        }

        return false;
    }

    private void CancelDrag()
    {
        if (!isDragging)
            return;

        isDragging = false;
        SetTrajectoryVisible(false);
        ClearTrajectory();
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
        float radius = (ballRadiusWorld > 0f) ? ballRadiusWorld : previewBallRadiusFallback;

        trajectoryPoints.Clear();
        trajectoryPoints.Add(pos);

        float power01 = ComputePower01(directionWorld, currentScreenPos);
        ApplyTrajectoryPowerStyling(power01);
        float minLen = 0.8f;
        float maxPreviewDistance = Mathf.Lerp(minLen, maxDistancePerSegment, power01);

        int previewBounces = Mathf.Max(0, maxBounces + GetPreviewBounceBonus());
        int bounces = 0;
        int steps = Mathf.Max(1, previewSteps);
        int minSteps = Mathf.Max(1, Mathf.FloorToInt(steps * 0.25f));
        steps = Mathf.RoundToInt(Mathf.Lerp(minSteps, steps, power01));
        float timeStep = Mathf.Max(0.001f, previewTimeStep);
        float gravityScale = ballPrefab != null ? ballPrefab.gravityScale : 1f;
        float linearDrag = ballPrefab != null ? ballPrefab.linearDamping : 0f;
        Vector2 gravity = Physics2D.gravity * gravityScale;
        float remainingDistance = maxPreviewDistance;

        for (int step = 0; step < steps; step++)
        {
            velocity += gravity * timeStep;
            if (linearDrag > 0f)
                velocity *= 1f / (1f + linearDrag * timeStep);

            Vector2 displacement = velocity * timeStep;
            float distance = displacement.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                trajectoryPoints.Add(pos);
                continue;
            }

            if (distance > remainingDistance)
            {
                displacement = displacement.normalized * remainingDistance;
                distance = remainingDistance;
            }

            Vector2 direction = displacement / distance;
            RaycastHit2D hit = Physics2D.CircleCast(
                pos,
                radius,
                direction,
                distance,
                collisionMask
            );

            if (hit.collider == null)
            {
                pos += displacement;
                trajectoryPoints.Add(pos);
                remainingDistance -= distance;
                if (remainingDistance <= Mathf.Epsilon)
                    break;
                continue;
            }

            trajectoryPoints.Add(hit.point);

            if (bounces >= previewBounces)
                break;

            bounces++;
            velocity = Vector2.Reflect(velocity, hit.normal);
            pos = hit.point + hit.normal * (radius + 0.01f);
            remainingDistance = bounceTailLength;
        }

        ApplyLine(trajectoryPoints);
    }

    private void ApplyTrajectoryPowerStyling(float power01)
    {
        if (trajectoryLine == null) return;

        if (trajectoryPowerGradient != null)
        {
            Color powerColor = trajectoryPowerGradient.Evaluate(power01);
            ApplySolidGradient(powerColor);
            trajectoryLine.colorGradient = powerPreviewGradient;
        }
        else if (baseTrajectoryGradient != null)
        {
            trajectoryLine.colorGradient = baseTrajectoryGradient;
        }

        if (trajectoryWidthByPower != null)
        {
            float widthMultiplier = trajectoryWidthByPower.Evaluate(power01);
            trajectoryLine.widthMultiplier = baseTrajectoryWidthMultiplier * widthMultiplier;
        }
        else if (baseTrajectoryWidthMultiplier > 0f)
        {
            trajectoryLine.widthMultiplier = baseTrajectoryWidthMultiplier;
        }
    }

    private void ApplySolidGradient(Color color)
    {
        powerGradientColorKeys[0] = new GradientColorKey(color, 0f);
        powerGradientColorKeys[1] = new GradientColorKey(color, 1f);
        powerGradientAlphaKeys[0] = new GradientAlphaKey(color.a, 0f);
        powerGradientAlphaKeys[1] = new GradientAlphaKey(color.a, 1f);
        powerPreviewGradient.SetKeys(powerGradientColorKeys, powerGradientAlphaKeys);
    }

    private void ApplyLine(List<Vector3> points)
    {
        trajectoryLine.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
            trajectoryLine.SetPosition(i, points[i]);
    }

    private void ConfigureTrajectoryLine()
    {
        if (trajectoryLine == null) return;

        trajectoryLine.numCornerVertices = previewCornerVertices;
        trajectoryLine.numCapVertices = previewCapVertices;
        baseTrajectoryGradient = trajectoryLine.colorGradient;
        baseTrajectoryWidthMultiplier = trajectoryLine.widthMultiplier;
        EnsurePowerStylingDefaults();
    }

    private void EnsurePowerStylingDefaults()
    {
        if (!autoConfigurePowerStyling) return;

        if (trajectoryLine != null && trajectoryPowerGradient == null)
        {
            Gradient defaultGradient = new Gradient();
            Color baseColor = trajectoryLine.startColor;
            Color lowColor = new Color(baseColor.r * 0.5f, baseColor.g * 0.5f, baseColor.b * 0.5f, baseColor.a * 0.75f);
            GradientColorKey[] colorKeys =
            {
                new GradientColorKey(lowColor, 0f),
                new GradientColorKey(baseColor, 1f)
            };
            GradientAlphaKey[] alphaKeys =
            {
                new GradientAlphaKey(lowColor.a, 0f),
                new GradientAlphaKey(baseColor.a, 1f)
            };
            defaultGradient.SetKeys(colorKeys, alphaKeys);
            trajectoryPowerGradient = defaultGradient;
        }

        if (trajectoryWidthByPower == null)
        {
            trajectoryWidthByPower = new AnimationCurve(
                new Keyframe(0f, 0.8f),
                new Keyframe(1f, 1.2f)
            );
        }
    }

    private float GetBallWorldRadius()
    {
        if (ballPrefab == null) return previewBallRadiusFallback;

        var circle = ballPrefab.GetComponent<CircleCollider2D>();
        if (circle != null)
        {
            Vector3 lossyScale = ballPrefab.transform.lossyScale;
            float effectiveScale = Mathf.Max(lossyScale.x, lossyScale.y);
            return circle.radius * effectiveScale;
        }

        var collider = ballPrefab.GetComponent<Collider2D>();
        if (collider != null)
        {
            Debug.LogWarning(
                $"Launcher: Ball prefab '{ballPrefab.name}' uses {collider.GetType().Name}. " +
                "Using fallback radius to avoid incorrect trajectory preview."
            );
        }

        return previewBallRadiusFallback;
    }
}
