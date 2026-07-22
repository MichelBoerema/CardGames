using UnityEngine;
using UnityEngine.UI;

public class AudioSettingsMenu : MonoBehaviour
{
    [Header("UI")]
    public Slider musicSlider;
    public Slider ambienceSlider;

    [Header("Menu Root")]
    public GameObject menuRoot; // Panel that contains the settings UI

    void Start()
    {
        // Initialize sliders with current values
        if (BarAudioManager.Instance != null)
        {
            musicSlider.value = BarAudioManager.Instance.musicVolume;
            ambienceSlider.value = BarAudioManager.Instance.ambienceVolume;
        }

        // Hook up listeners
        musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        ambienceSlider.onValueChanged.AddListener(OnAmbienceVolumeChanged);

        // Start closed
        if (menuRoot != null)
            menuRoot.SetActive(false);
    }

    void OnDestroy()
    {
        musicSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        ambienceSlider.onValueChanged.RemoveListener(OnAmbienceVolumeChanged);
    }

    // ---------------- OPEN / CLOSE ----------------

    public void OpenSettings()
    {
        if (menuRoot == null) return;

        menuRoot.SetActive(true);

        // Refresh slider values in case audio changed elsewhere
        if (BarAudioManager.Instance != null)
        {
            musicSlider.value = BarAudioManager.Instance.musicVolume;
            ambienceSlider.value = BarAudioManager.Instance.ambienceVolume;
        }
    }

    public void CloseSettings()
    {
        if (menuRoot == null) return;

        menuRoot.SetActive(false);
    }

    // ---------------- VOLUME HANDLERS ----------------

    void OnMusicVolumeChanged(float value)
    {
        if (BarAudioManager.Instance != null)
            BarAudioManager.Instance.SetMusicVolume(value);
    }

    void OnAmbienceVolumeChanged(float value)
    {
        if (BarAudioManager.Instance != null)
            BarAudioManager.Instance.SetAmbienceVolume(value);
    }
}
