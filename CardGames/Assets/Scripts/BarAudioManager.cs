using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BarAudioManager : MonoBehaviour
{
    public static BarAudioManager Instance;

    [Header("Ambience")]
    public AudioSource ambienceSource;
    public AudioClip ambienceClip;
    [Range(0f, 1f)]
    public float ambienceVolume = 1f;

    [Header("Music Playlist")]
    public AudioSource musicSource;
    public List<AudioClip> musicClips = new List<AudioClip>();
    [Range(0f, 1f)]
    public float musicVolume = 1f;

    [Header("Settings")]
    public bool shuffleMusic = false;
    public float delayBetweenTracks = 0f;

    int currentMusicIndex = 0;

    const string MusicVolumeKey = "MusicVolume";
    const string AmbienceVolumeKey = "AmbienceVolume";

    void Awake()
    {
        // Singleton guard
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
        LoadVolumes();
        StartAmbience();
        StartMusic();
    }

    void LoadVolumes()
    {
        musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, musicVolume);
        ambienceVolume = PlayerPrefs.GetFloat(AmbienceVolumeKey, ambienceVolume);
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = value;
        if (musicSource != null)
            musicSource.volume = value;

        PlayerPrefs.SetFloat(MusicVolumeKey, value);
    }

    public void SetAmbienceVolume(float value)
    {
        ambienceVolume = value;
        if (ambienceSource != null)
            ambienceSource.volume = value;

        PlayerPrefs.SetFloat(AmbienceVolumeKey, value);
    }

    // ---------------- AMBIENCE ----------------

    void StartAmbience()
    {
        if (ambienceSource == null || ambienceClip == null)
            return;

        ambienceSource.clip = ambienceClip;
        ambienceSource.loop = true;
        ambienceSource.volume = ambienceVolume;
        ambienceSource.Play();
    }

    // ---------------- MUSIC ----------------

    void StartMusic()
    {
        if (musicSource == null || musicClips.Count == 0)
            return;

        musicSource.volume = musicVolume;

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
