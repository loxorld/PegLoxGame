using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Anima capas de fondo del menú principal con desplazamiento horizontal y oscilación vertical sutil.
/// </summary>
public class MainMenuAmbientAnimator : MonoBehaviour
{
    [Serializable]
    public class AmbientLayer
    {
        [Tooltip("Transform de la capa a animar (RectTransform o Transform normal).")]
        public Transform layerTransform;

        [Tooltip("Velocidad horizontal base en unidades UI/segundo. Valor recomendado: 5-20.")]
        public float speedX = 10f;

        [Tooltip("Amplitud vertical de oscilación en unidades UI.")]
        public float amplitudeY = 8f;

        [Tooltip("Frecuencia de oscilación vertical (Hz).")]
        public float frequencyY = 0.08f;

        [Tooltip("Factor de parallax para variar la sensación de profundidad (0.2-1.5).")]
        public float parallaxFactor = 1f;

        [Tooltip("Rango horizontal total de oscilación en unidades UI.")]
        public float loopDistance = 220f;

        [NonSerialized] public Vector3 baseLocalPosition;
        [NonSerialized] public float horizontalOffset;
        [NonSerialized] public float horizontalPhase;
        [NonSerialized] public float verticalPhase;
    }

    [Header("Layers")]
    [SerializeField] private List<AmbientLayer> layers = new();

    [Header("Global Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float globalIntensity = 0.35f;

    [SerializeField] private bool useUnscaledTime = true;

    [Tooltip("Si es true, inicializa la base en OnEnable para reanudar al reactivar el objeto.")]
    [SerializeField] private bool recacheOnEnable = false;

    private float elapsedTime;

    private void Awake()
    {
        CacheLayerData(resetOffsets: true);
    }

    private void OnEnable()
    {
        if (recacheOnEnable)
        {
            CacheLayerData(resetOffsets: false);
        }
    }

    private void Update()
    {
        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        elapsedTime += deltaTime;
        float intensity = Mathf.Clamp01(globalIntensity);

        for (int i = 0; i < layers.Count; i++)
        {
            AmbientLayer layer = layers[i];
            if (layer?.layerTransform == null)
            {
                continue;
            }

            float parallax = Mathf.Max(0f, layer.parallaxFactor);
            float speed = Mathf.Abs(layer.speedX * parallax * intensity);
            float oscillationRange = Mathf.Max(1f, layer.loopDistance);
            float oscillationRadius = oscillationRange * 0.5f;
            float horizontalOmega = (speed / oscillationRadius) * Mathf.PI;
            layer.horizontalOffset = Mathf.Sin((elapsedTime * horizontalOmega) + layer.horizontalPhase) * oscillationRadius;

            float yOffset = Mathf.Sin((elapsedTime * Mathf.PI * 2f * layer.frequencyY) + layer.verticalPhase) * layer.amplitudeY * intensity;

            Vector3 animatedPosition = layer.baseLocalPosition;
            animatedPosition.x += layer.horizontalOffset;
            animatedPosition.y += yOffset;
            layer.layerTransform.localPosition = animatedPosition;
        }
    }

    private void CacheLayerData(bool resetOffsets)
    {
        elapsedTime = 0f;

        for (int i = 0; i < layers.Count; i++)
        {
            AmbientLayer layer = layers[i];
            if (layer == null || layer.layerTransform == null)
            {
                continue;
            }

            layer.baseLocalPosition = layer.layerTransform.localPosition;

            if (resetOffsets)
            {
                layer.horizontalOffset = 0f;
            }

            layer.horizontalPhase = i * 0.6f;
            layer.verticalPhase = i * 0.9f;
        }
    }
}