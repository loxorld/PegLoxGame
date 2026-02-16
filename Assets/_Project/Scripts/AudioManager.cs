using System;
using System.Collections.Generic;
using UnityEngine;

public enum AudioEventId
{
    UiClick,
    UiOpenPanel,
    UiClosePanel,
    LaunchBall,
    PegHit,
    EnemyHit,
    EnemyDefeated,
}


public class AudioManager : MonoBehaviour
{
    [Serializable]
    private class AudioEventEntry
    {
        public AudioEventId eventId;
        public AudioClip clip;
    }

    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [Tooltip("AudioSource for looping background music.")]
    [SerializeField] private AudioSource musicSource;

    [Tooltip("AudioSource for sound effects.")]
    [SerializeField] private AudioSource sfxSource;

    [Header("SFX Event Library")]
    [Tooltip("Mapea cada evento de audio a un AudioClip.")]
    [SerializeField] private AudioEventEntry[] sfxEventEntries;

    [Header("Music Clips")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip combatMusic;
    [SerializeField] private AudioClip shopMusic;

    private const string MusicVolumeKey = "MusicVolume";
    private const string SfxVolumeKey = "SfxVolume";
    private readonly Dictionary<AudioEventId, AudioClip> sfxByEvent = new Dictionary<AudioEventId, AudioClip>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[AudioManager] Duplicate instance detected. Keep only the BootScene prefab instance.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);


        float savedMusic = PlayerPrefs.GetFloat(MusicVolumeKey, 0.8f);
        float savedSfx = PlayerPrefs.GetFloat(SfxVolumeKey, 0.8f);
        SetMusicVolume(savedMusic);
        SetSfxVolume(savedSfx);
        RebuildSfxMap();

        if (musicSource == null || sfxSource == null)
            Debug.LogWarning("[AudioManager] Missing AudioSource references. Configure the BootScene AudioManager prefab.");
    }


    private void OnValidate()
    {
        RebuildSfxMap();
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

    public void PlaySfx(AudioEventId eventId)
    {
        if (!sfxByEvent.TryGetValue(eventId, out AudioClip clip) || clip == null)
        {
            if ((eventId == AudioEventId.UiOpenPanel || eventId == AudioEventId.UiClosePanel)
                && sfxByEvent.TryGetValue(AudioEventId.UiClick, out AudioClip fallbackClip)
                && fallbackClip != null)
            {
                PlaySfx(fallbackClip);
            }

            return;
        }

        PlaySfx(clip);
    }

    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (musicSource == null || clip == null)
            return;

        if (musicSource.clip == clip && musicSource.isPlaying)
            return;

        musicSource.loop = loop;
        musicSource.clip = clip;
        musicSource.Play();
    }

    public void PlayMenuMusic()
    {
        PlayMusic(menuMusic, true);
    }

    public void PlayCombatMusic()
    {
        PlayMusic(combatMusic, true);
    }

    public void PlayShopMusic()
    {
        if (shopMusic != null)
        {
            PlayMusic(shopMusic, true);
            return;
        }

        PlayCombatMusic();
    }

    private void RebuildSfxMap()
    {
        sfxByEvent.Clear();
        if (sfxEventEntries == null)
            return;

        for (int i = 0; i < sfxEventEntries.Length; i++)
        {
            AudioEventEntry entry = sfxEventEntries[i];
            if (entry == null)
                continue;

            sfxByEvent[entry.eventId] = entry.clip;
        }
    }
}