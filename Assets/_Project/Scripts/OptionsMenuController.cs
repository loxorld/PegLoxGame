using UnityEngine;
using UnityEngine.UI;

public class OptionsMenuController : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider dragSensitivitySlider;
    [SerializeField] private Image musicFillImage;
    [SerializeField] private Image sfxFillImage;
    [SerializeField] private Image dragSensitivityFillImage;

    [Header("Slider Fill Colors")]
    [SerializeField] private Color lowValueColor = new Color(0.94f, 0.36f, 0.36f, 1f);
    [SerializeField] private Color highValueColor = new Color(0.36f, 0.85f, 0.45f, 1f);

    [Header("Toggles")]
    [SerializeField] private Toggle dpiNormalizationToggle;

    [Header("Buttons")]
    [SerializeField] private Button mobilePresetButton;
    [SerializeField] private Button restoreDefaultsButton;

    private void Start()
    {

        if (musicSlider != null)
        {
            musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 0.8f);
            musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            UpdateSliderFillColor(musicSlider, musicFillImage);
        }
        if (sfxSlider != null)
        {
            sfxSlider.value = PlayerPrefs.GetFloat("SfxVolume", 0.8f);
            sfxSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            UpdateSliderFillColor(sfxSlider, sfxFillImage);
        }

        if (dragSensitivitySlider != null)
        {
            dragSensitivitySlider.value = PlayerPrefs.GetFloat(
                LauncherPreferences.DragSensitivityKey,
                LauncherPreferences.DefaultDragSensitivity
            );
            dragSensitivitySlider.onValueChanged.AddListener(OnDragSensitivityChanged);
            UpdateSliderFillColor(dragSensitivitySlider, dragSensitivityFillImage);
        }

        if (dpiNormalizationToggle != null)
        {
            bool useNormalization = PlayerPrefs.GetInt(
                LauncherPreferences.UseDpiNormalizationKey,
                LauncherPreferences.DefaultUseDpiNormalization ? 1 : 0
            ) == 1;
            dpiNormalizationToggle.isOn = useNormalization;
            dpiNormalizationToggle.onValueChanged.AddListener(OnDpiNormalizationChanged);
        }

        if (mobilePresetButton != null)
            mobilePresetButton.onClick.AddListener(ApplyMobilePreset);

        if (restoreDefaultsButton != null)
            restoreDefaultsButton.onClick.AddListener(RestoreDefaults);
    }

    private void OnDestroy()
    {

        if (musicSlider != null)
            musicSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        if (sfxSlider != null)
            sfxSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
        if (dragSensitivitySlider != null)
            dragSensitivitySlider.onValueChanged.RemoveListener(OnDragSensitivityChanged);
        if (dpiNormalizationToggle != null)
            dpiNormalizationToggle.onValueChanged.RemoveListener(OnDpiNormalizationChanged);
        if (mobilePresetButton != null)
            mobilePresetButton.onClick.RemoveListener(ApplyMobilePreset);
        if (restoreDefaultsButton != null)
            restoreDefaultsButton.onClick.RemoveListener(RestoreDefaults);
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMusicVolume(value);
        UpdateSliderFillColor(musicSlider, musicFillImage);
    }

    private void OnSfxVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetSfxVolume(value);
        UpdateSliderFillColor(sfxSlider, sfxFillImage);
    }

    private void OnDragSensitivityChanged(float value)
    {
        PlayerPrefs.SetFloat(LauncherPreferences.DragSensitivityKey, value);
        PlayerPrefs.Save();
        UpdateSliderFillColor(dragSensitivitySlider, dragSensitivityFillImage);
    }

    private void OnDpiNormalizationChanged(bool value)
    {
        PlayerPrefs.SetInt(LauncherPreferences.UseDpiNormalizationKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void ApplyMobilePreset()
    {
        SetDragSensitivity(LauncherPreferences.MobileDragSensitivity);
        SetDpiNormalization(LauncherPreferences.MobileUseDpiNormalization);
        PlayerPrefs.Save();
    }

    private void RestoreDefaults()
    {
        SetDragSensitivity(LauncherPreferences.DefaultDragSensitivity);
        SetDpiNormalization(LauncherPreferences.DefaultUseDpiNormalization);
        PlayerPrefs.Save();
    }

    private void SetDragSensitivity(float value)
    {
        PlayerPrefs.SetFloat(LauncherPreferences.DragSensitivityKey, value);
        if (dragSensitivitySlider != null)
            dragSensitivitySlider.value = value;
    }

    private void SetDpiNormalization(bool value)
    {
        PlayerPrefs.SetInt(LauncherPreferences.UseDpiNormalizationKey, value ? 1 : 0);
        if (dpiNormalizationToggle != null)
            dpiNormalizationToggle.isOn = value;
    }

    private void UpdateSliderFillColor(Slider slider, Image fillImage)
    {
        if (slider == null || fillImage == null)
            return;

        fillImage.color = Color.Lerp(lowValueColor, highValueColor, slider.normalizedValue);
    }
}