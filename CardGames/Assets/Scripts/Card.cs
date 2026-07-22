using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;


[System.Serializable]
public struct PlayingCard : INetworkSerializable
{
    public PlayingDeckCardValue Value;
    public CardSuit Suit;

    public PlayingCard(PlayingDeckCardValue value, CardSuit suit)
    {
        Value = value;
        Suit = suit;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref Value);
        serializer.SerializeValue(ref Suit);
    }
    public override string ToString()
    {
        if (Value == PlayingDeckCardValue.Joker)
            return "Joker";

        return $"{Value} of {Suit}";
    }

    public bool IsRed =>
    Suit == CardSuit.Hearts || Suit == CardSuit.Diamonds;

    public bool IsBlack =>
        Suit == CardSuit.Clubs || Suit == CardSuit.Spades;

    public bool IsJoker => Value == PlayingDeckCardValue.Joker;
}

public enum PlayingDeckCardValue
{
    Ace = 1,
    Two = 2,
    Three = 3,
    Four = 4,
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,
    Ten = 10,
    Jack = 11,
    Queen = 12,
    King = 13,
    Joker = 99
}

public enum JokerColor
{
    Red,
    Black
}

public class Card : MonoBehaviour
{
    public PlayingCard playingCard;
    public JokerColor jokerColor;
    public bool IsSelected { get; private set; }

    [Header("UI References")]
    [SerializeField] private Button button;
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

    public void Setup(PlayingCard card)
    {
        playingCard = card;

        if (playingCard.Value == PlayingDeckCardValue.Joker)
        {
            jokerColor = Random.Range(0, 2) == 0
                ? JokerColor.Red
                : JokerColor.Black;
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
        if (playingCard.Value == PlayingDeckCardValue.Joker)
            return jokerColor == JokerColor.Red ? jokerRedSprite : jokerBlackSprite;

        return (playingCard.Value, playingCard.Suit) switch
        {
            (PlayingDeckCardValue.King, CardSuit.Hearts) => kingHeartsSprite,
            (PlayingDeckCardValue.King, CardSuit.Diamonds) => kingDiamondsSprite,
            (PlayingDeckCardValue.King, CardSuit.Clubs) => kingClubsSprite,
            (PlayingDeckCardValue.King, CardSuit.Spades) => kingSpadesSprite,

            (PlayingDeckCardValue.Queen, CardSuit.Hearts) => queenHeartsSprite,
            (PlayingDeckCardValue.Queen, CardSuit.Diamonds) => queenDiamondsSprite,
            (PlayingDeckCardValue.Queen, CardSuit.Clubs) => queenClubsSprite,
            (PlayingDeckCardValue.Queen, CardSuit.Spades) => queenSpadesSprite,

            (PlayingDeckCardValue.Ace, CardSuit.Hearts) => aceHeartsSprite,
            (PlayingDeckCardValue.Ace, CardSuit.Diamonds) => aceDiamondsSprite,
            (PlayingDeckCardValue.Ace, CardSuit.Clubs) => aceClubsSprite,
            (PlayingDeckCardValue.Ace, CardSuit.Spades) => aceSpadesSprite,

            _ => null
        };
    }

    public void HighlightCard(bool isCorrect)
    {
        background.color = isCorrect ? Color.green : Color.paleVioletRed;
    }
}
