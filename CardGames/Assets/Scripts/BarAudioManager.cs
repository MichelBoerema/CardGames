using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BarAudioManager : MonoBehaviour
{
    public static BarAudioManager Instance;

    [Header("Ambience")]
    public AudioSource ambienceSource;
    public AudioClip ambienceClip;

    [Header("Music Playlist")]
    public AudioSource musicSource;
    public List<AudioClip> musicClips = new List<AudioClip>();

    [Header("Settings")]
    public bool shuffleMusic = false;
    public float delayBetweenTracks = 0f;

    int currentMusicIndex = 0;

    const string MusicEnabledKey = "MusicEnabled";
    const string AmbienceEnabledKey = "AmbienceEnabled";
    const string SoundEffectsEnabledKey = "SoundEffectsEnabled";

    public bool MusicEnabled { get; private set; } = true;
    public bool AmbienceEnabled { get; private set; } = true;
    public bool SoundEffectsEnabled { get; private set; } = true;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        LoadSettings();

        StartAmbience();
        StartMusic();

        ApplyAudioSettings();
    }

    void LoadSettings()
    {
        MusicEnabled = PlayerPrefs.GetInt(MusicEnabledKey, 1) == 1;
        AmbienceEnabled = PlayerPrefs.GetInt(AmbienceEnabledKey, 1) == 1;
        SoundEffectsEnabled = PlayerPrefs.GetInt(SoundEffectsEnabledKey, 1) == 1;
    }

    void SaveSettings()
    {
        PlayerPrefs.SetInt(MusicEnabledKey, MusicEnabled ? 1 : 0);
        PlayerPrefs.SetInt(AmbienceEnabledKey, AmbienceEnabled ? 1 : 0);
        PlayerPrefs.SetInt(SoundEffectsEnabledKey, SoundEffectsEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void ToggleMusic()
    {
        MusicEnabled = !MusicEnabled;
        ApplyAudioSettings();
        SaveSettings();
    }

    public void ToggleAmbience()
    {
        AmbienceEnabled = !AmbienceEnabled;

        if (ambienceSource != null)
            ambienceSource.mute = !AmbienceEnabled;

        SaveSettings();
    }

    public void ToggleSoundEffects()
    {
        SoundEffectsEnabled = !SoundEffectsEnabled;
        SaveSettings();
    }

    void ApplyAudioSettings()
    {
        if (musicSource != null)
            musicSource.mute = !MusicEnabled;

        if (ambienceSource != null)
            ambienceSource.mute = !AmbienceEnabled;
    }

    // ---------------- AMBIENCE ----------------

    void StartAmbience()
    {
        if (ambienceSource == null || ambienceClip == null)
            return;

        ambienceSource.clip = ambienceClip;
        ambienceSource.loop = true;
        ambienceSource.Play();
    }

    // ---------------- MUSIC ----------------

    void StartMusic()
    {
        if (musicSource == null || musicClips.Count == 0)
            return;

        if (shuffleMusic)
            Shuffle(musicClips);

        StartCoroutine(MusicLoop());
    }

    IEnumerator MusicLoop()
    {
        while (true)
        {
            AudioClip clip = musicClips[currentMusicIndex];

            musicSource.clip = clip;
            musicSource.loop = false;
            musicSource.Play();

            yield return new WaitForSeconds(clip.length + delayBetweenTracks);

            currentMusicIndex++;

            if (currentMusicIndex >= musicClips.Count)
                currentMusicIndex = 0;
        }
    }

    // ---------------- UTILS ----------------

    void Shuffle(List<AudioClip> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int rand = Random.Range(0, i + 1);
            (list[i], list[rand]) = (list[rand], list[i]);
        }
    }
}