using UnityEngine;
using UnityEngine.UI;

public class OptionsMenuController : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

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
    }

    private void OnDestroy()
    {
       
        if (musicSlider != null)
            musicSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        if (sfxSlider != null)
            sfxSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
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
}