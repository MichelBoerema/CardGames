using UnityEngine;
using UnityEngine.UI;

public class Card : MonoBehaviour
{
    public CardValue cardValue;
    public bool IsSelected { get; private set; }

    [Header("UI References")]
    [SerializeField] private Button button;
    [SerializeField] private Text buttonText;
    [SerializeField] private Image background;
    [SerializeField] private Image cardArt;   // <-- NEW

    [Header("Card Sprites")]
    public Sprite kingSprite;
    public Sprite queenSprite;
    public Sprite aceSprite;
    public Sprite jokerSprite;

    void Awake()
    {
        button.onClick.AddListener(ToggleSelected);
    }

    public void Setup(CardValue value)
    {
        cardValue = value;

        // Optional: keep text for debugging
        buttonText.text = GetCardShortName(cardValue);

        cardArt.sprite = GetCardSprite(cardValue);
        cardArt.enabled = true;

        SetSelected(false);
    }

    void ToggleSelected()
    {
        SetSelected(!IsSelected);
        UIManager.Instance.OnCardSelectionChanged(this);
    }

    public void SetInteractable(bool interactable)
    {
        button.interactable = interactable;
        background.color = interactable ? Color.white : new Color(1f, 1f, 1f, 0.5f);
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;

        // visual feedback
        background.color = IsSelected ? Color.yellow : Color.white;
    }

    Sprite GetCardSprite(CardValue value)
    {
        switch (value)
        {
            case CardValue.King: return kingSprite;
            case CardValue.Queen: return queenSprite;
            case CardValue.Ace: return aceSprite;
            case CardValue.Joker: return jokerSprite;
            default: return null;
        }
    }

    string GetCardShortName(CardValue value)
    {
        switch (value)
        {
            case CardValue.King: return "K";
            case CardValue.Queen: return "Q";
            case CardValue.Ace: return "A";
            case CardValue.Joker: return "J";
            default: return "?";
        }
    }

    public void HighlightCard(bool isCorrect)
    {
        // Green for correct, red for incorrect
        if (isCorrect)
        {
            background.color = Color.green;
        }
        else
        {
            background.color = Color.red;
        }
    }
}
