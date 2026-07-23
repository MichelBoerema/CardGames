using UnityEngine;
using UnityEngine.UI;

public class AudioSettingsMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button musicButton;
    [SerializeField] private Button ambienceButton;
    [SerializeField] private Button soundEffectsButton;

    [Header("Mute Overlays")]
    [SerializeField] private Image musicMutedOverlay;
    [SerializeField] private Image ambienceMutedOverlay;
    [SerializeField] private Image soundEffectsMutedOverlay;

    [Header("Toggle Images")]
    [SerializeField] private Image musicToggleImage;
    [SerializeField] private Image ambienceToggleImage;
    [SerializeField] private Image soundEffectsToggleImage;

    [SerializeField] private Sprite toggleOnSprite;
    [SerializeField] private Sprite toggleOffSprite;

    [Header("Menu Root")]
    [SerializeField] private GameObject menuRoot;

    void Start()
    {
        musicButton.onClick.AddListener(OnMusicClicked);
        ambienceButton.onClick.AddListener(OnAmbienceClicked);
        soundEffectsButton.onClick.AddListener(OnSoundEffectsClicked);

        if (menuRoot != null)
            menuRoot.SetActive(false);

        RefreshUI();
    }

    void OnDestroy()
    {
        musicButton.onClick.RemoveListener(OnMusicClicked);
        ambienceButton.onClick.RemoveListener(OnAmbienceClicked);
        soundEffectsButton.onClick.RemoveListener(OnSoundEffectsClicked);
    }

    // ---------------- OPEN / CLOSE ----------------

    public void OpenSettings()
    {
        if (menuRoot == null) return;

        menuRoot.SetActive(true);
        RefreshUI();
    }

    public void CloseSettings()
    {
        if (menuRoot == null) return;

        menuRoot.SetActive(false);
    }

    // ---------------- BUTTONS ----------------

    void OnMusicClicked()
    {
        if (BarAudioManager.Instance == null)
            return;

        BarAudioManager.Instance.ToggleMusic();
        RefreshUI();
    }

    void OnAmbienceClicked()
    {
        if (BarAudioManager.Instance == null)
            return;

        BarAudioManager.Instance.ToggleAmbience();
        RefreshUI();
    }

    void OnSoundEffectsClicked()
    {
        BarAudioManager.Instance.ToggleSoundEffects();
        RefreshUI();
    }

    void RefreshUI()
    {
        if (BarAudioManager.Instance == null)
            return;

        musicToggleImage.sprite = BarAudioManager.Instance.MusicEnabled
            ? toggleOnSprite
            : toggleOffSprite;
        musicMutedOverlay.gameObject.SetActive(!BarAudioManager.Instance.MusicEnabled);

        ambienceToggleImage.sprite = BarAudioManager.Instance.AmbienceEnabled
            ? toggleOnSprite
            : toggleOffSprite;
        ambienceMutedOverlay.gameObject.SetActive(!BarAudioManager.Instance.AmbienceEnabled);

        soundEffectsToggleImage.sprite = BarAudioManager.Instance.SoundEffectsEnabled
            ? toggleOnSprite
            : toggleOffSprite;
        soundEffectsMutedOverlay.gameObject.SetActive(!BarAudioManager.Instance.SoundEffectsEnabled);
    }
}