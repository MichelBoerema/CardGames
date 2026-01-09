using UnityEngine;
using UnityEngine.UI;

public enum Suit
{
    Hearts,
    Diamonds,
    Clubs,
    Spades
}

public enum JokerColor
{
    Red,
    Black
}

public class Card : MonoBehaviour
{
    public CardValue cardValue;
    public Suit suit;
    public JokerColor jokerColor;
    public bool IsSelected { get; private set; }

    [Header("UI References")]
    [SerializeField] private Button button;
    [SerializeField] private Text buttonText;
    [SerializeField] private Image background;
    [SerializeField] private Image cardArt;

    [Header("Card Sprites per Suit")]
    public Sprite kingHeartsSprite;
    public Sprite kingDiamondsSprite;
    public Sprite kingClubsSprite;
    public Sprite kingSpadesSprite;

    public Sprite queenHeartsSprite;
    public Sprite queenDiamondsSprite;
    public Sprite queenClubsSprite;
    public Sprite queenSpadesSprite;

    public Sprite aceHeartsSprite;
    public Sprite aceDiamondsSprite;
    public Sprite aceClubsSprite;
    public Sprite aceSpadesSprite;

    [Header("Joker Sprites")]
    public Sprite jokerRedSprite;
    public Sprite jokerBlackSprite;

    void Awake()
    {
        button.onClick.AddListener(ToggleSelected);
    }

    public void Setup(CardValue value)
    {
        cardValue = value;

        // Randomize suit for King, Queen, Ace
        if (cardValue == CardValue.King || cardValue == CardValue.Queen || cardValue == CardValue.Ace)
        {
            suit = (Suit)Random.Range(0, 4); // 0-3 = Hearts, Diamonds, Clubs, Spades
        }
        else if (cardValue == CardValue.Joker)
        {
            jokerColor = (Random.Range(0, 2) == 0) ? JokerColor.Red : JokerColor.Black;
        }

        cardArt.sprite = GetCardSprite();
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
        background.color = selected ? Color.yellow : Color.white;
    }

    Sprite GetCardSprite()
    {
        switch (cardValue)
        {
            case CardValue.King:
                return suit switch
                {
                    Suit.Hearts => kingHeartsSprite,
                    Suit.Diamonds => kingDiamondsSprite,
                    Suit.Clubs => kingClubsSprite,
                    Suit.Spades => kingSpadesSprite,
                    _ => null
                };
            case CardValue.Queen:
                return suit switch
                {
                    Suit.Hearts => queenHeartsSprite,
                    Suit.Diamonds => queenDiamondsSprite,
                    Suit.Clubs => queenClubsSprite,
                    Suit.Spades => queenSpadesSprite,
                    _ => null
                };
            case CardValue.Ace:
                return suit switch
                {
                    Suit.Hearts => aceHeartsSprite,
                    Suit.Diamonds => aceDiamondsSprite,
                    Suit.Clubs => aceClubsSprite,
                    Suit.Spades => aceSpadesSprite,
                    _ => null
                };
            case CardValue.Joker:
                return jokerColor == JokerColor.Red ? jokerRedSprite : jokerBlackSprite;
            default:
                return null;
        }
    }

    public void HighlightCard(bool isCorrect)
    {
        background.color = isCorrect ? Color.green : Color.red;
    }
}
