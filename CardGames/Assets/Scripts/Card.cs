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

    [Header("Playing Cards")]
    [SerializeField] private Sprite[] cardSprites = new Sprite[52];

    [Header("Jokers")]
    [SerializeField] private Sprite jokerRedSprite;
    [SerializeField] private Sprite jokerBlackSprite;

    void Awake()
    {
        if(button != null)
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
        if (playingCard.IsJoker)
        {
            return jokerColor == JokerColor.Red
                ? jokerRedSprite
                : jokerBlackSprite;
        }

        int index = ((int)playingCard.Suit * 13)
                  + ((int)playingCard.Value - 1);

        return cardSprites[index];
    }

    public void HighlightCard(bool isCorrect)
    {
        background.color = isCorrect ? Color.green : Color.paleVioletRed;
    }
}
