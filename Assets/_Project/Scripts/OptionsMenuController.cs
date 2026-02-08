using UnityEngine;
using UnityEngine.UI;

public class OptionsMenuController : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider dragSensitivitySlider;

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
        }
        if (sfxSlider != null)
        {
            sfxSlider.value = PlayerPrefs.GetFloat("SfxVolume", 0.8f);
            sfxSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }

        if (dragSensitivitySlider != null)
        {
            dragSensitivitySlider.value = PlayerPrefs.GetFloat(
                LauncherPreferences.DragSensitivityKey,
                LauncherPreferences.DefaultDragSensitivity
            );
            dragSensitivitySlider.onValueChanged.AddListener(OnDragSensitivityChanged);
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
    }

    private void OnSfxVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetSfxVolume(value);
    }

    private void OnDragSensitivityChanged(float value)
    {
        PlayerPrefs.SetFloat(LauncherPreferences.DragSensitivityKey, value);
        PlayerPrefs.Save();
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
}