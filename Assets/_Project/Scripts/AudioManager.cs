using UnityEngine;


public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [Tooltip("AudioSource for looping background music.")]
    [SerializeField] private AudioSource musicSource;

    [Tooltip("AudioSource for sound effects.")]
    [SerializeField] private AudioSource sfxSource;

    private const string MusicVolumeKey = "MusicVolume";
    private const string SfxVolumeKey = "SfxVolume";

    private void Awake()
    {

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);


        float savedMusic = PlayerPrefs.GetFloat(MusicVolumeKey, 0.8f);
        float savedSfx = PlayerPrefs.GetFloat(SfxVolumeKey, 0.8f);
        SetMusicVolume(savedMusic);
        SetSfxVolume(savedSfx);
    }

    public void SetMusicVolume(float value)
    {
        if (musicSource != null)
            musicSource.volume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
    }


    public void SetSfxVolume(float value)
    {
        if (sfxSource != null)
            sfxSource.volume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value));
    }


    public void PlaySfx(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
            sfxSource.PlayOneShot(clip);
    }
}