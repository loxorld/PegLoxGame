using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class Launcher : MonoBehaviour
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
#pragma warning disable CS0414
    [SerializeField] private float launchForce = 10f;
#pragma warning restore CS0414
    [SerializeField] private float maxAimMagnitude = 3.0f;
    [SerializeField] private LaunchSettings launchSettings = new LaunchSettings();

    [Header("Drag Tuning (screen space)")]
    [SerializeField] private bool useScreenSpaceDragForPower = true;
    [SerializeField, Min(0f), Tooltip("Deadzone en pixeles para ignorar arrastres mínimos.")]
    private float deadzonePixels = 8f;
    [SerializeField, Min(1f), Tooltip("DPI usado como referencia cuando la normalización está activa.")]
    private float referenceDpi = 160f;

    [Header("Launch Speed Cap")]
    [SerializeField] private bool clampCurveOutput01 = true;

    [Header("Cancel Shot")]
    [SerializeField, Tooltip("RectTransform opcional para cancelar el tiro al soltar dentro de su área.")]
    private RectTransform cancelShotRect;
    [SerializeField, Tooltip("Si está activo, cualquier UI bajo el puntero cancela el tiro al soltar.")]
    private bool cancelShotOnUI = true;

    private Vector2 dragStartWorld;
    private Vector2 dragStartScreen;
    private bool isDragging;
    private bool isDragBeyondDeadzone;
    private bool hasLoggedMissingCamera;

    [Header("Trajectory Preview")]
    [SerializeField] private LineRenderer trajectoryLine;
    [SerializeField] private LayerMask collisionMask;
    [SerializeField] private int maxBounces = 1;
    [SerializeField] private float maxDistancePerSegment = 30f;
    [SerializeField] private float previewBallRadiusFallback = 0.15f;
    [SerializeField] private float maxPreviewSpeed = 30f;
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
        ResolveReferences(suppressFallbackLogging: true);
        ConfigureTrajectoryLine();
    }
#endif

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

        OrbInstance orb = orbManager != null ? orbManager.CurrentOrb : null;

        ShotManager.Instance?.OnShotStarted(orb);

        Rigidbody2D ballInstance = Instantiate(ballPrefab, launchPoint.position, Quaternion.identity);

        BallController ballController = ballInstance.GetComponent<BallController>();
        if (ballController != null)
            ballController.Init(orb);

        Vector2 vel = ComputeLaunchVelocity(directionWorld, releaseScreenPos);
        ballInstance.linearVelocity = vel;

        AudioManager.Instance?.PlaySfx(AudioEventId.LaunchBall);
    }

    private void ResolveReferences(bool suppressFallbackLogging = false)
    {
        if (orbManager == null)
            orbManager = suppressFallbackLogging
                ? ResolveReferenceWithoutLogging(() => OrbManager.Instance)
                : ServiceRegistry.ResolveWithFallback(nameof(Launcher), nameof(orbManager), () => OrbManager.Instance ?? ServiceRegistry.LegacyFind<OrbManager>(true));

        if (relicManager == null)
            relicManager = suppressFallbackLogging
                ? ResolveReferenceWithoutLogging(() => RelicManager.Instance)
                : ServiceRegistry.ResolveWithFallback(nameof(Launcher), nameof(relicManager), () => RelicManager.Instance ?? ServiceRegistry.LegacyFind<RelicManager>(true));

        if (cachedCamera == null)
            cachedCamera = Camera.main;
    }

    private static T ResolveReferenceWithoutLogging<T>(System.Func<T> instanceResolver) where T : Component
    {
        if (ServiceRegistry.TryResolve(out T registered))
            return registered;

        T singleton = instanceResolver != null ? instanceResolver.Invoke() : null;
        if (singleton != null)
            return singleton;

        return Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
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
}
