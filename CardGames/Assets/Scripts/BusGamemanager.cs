using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

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

public enum CardSuit
{
    Hearts,
    Diamonds,
    Clubs,
    Spades,
    Joker
}

[System.Serializable]
public struct PlayingCard
{
    public PlayingDeckCardValue Value;
    public CardSuit Suit;

    public PlayingCard(PlayingDeckCardValue value, CardSuit suit)
    {
        Value = value;
        Suit = suit;
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


public class BusGamemanager : NetworkBehaviour
{
    public static BusGamemanager Instance;

    public List<BusPlayer> players = new();
    private int currentPlayerIndex = 0;

    [Header("Deck Settings")]
    [SerializeField] private int jokerCount = 2;

    private List<PlayingCard> deck = new List<PlayingCard>();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        players.Clear();

        // Find all Player objects that came from lobby
        foreach (var player in FindObjectsOfType<BusPlayer>())
        {
            RegisterPlayer(player);
        }

        Debug.Log($"Registered {players.Count} players in GameScene");

        NetworkManager.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
    }
    private void OnSceneLoaded(
    string sceneName,
    LoadSceneMode loadSceneMode,
    List<ulong> clientsCompleted,
    List<ulong> clientsTimedOut)
    {
        if (sceneName != "BusGame")
            return;

        StartGameServer();
    }

    public void RegisterPlayer(BusPlayer player)
    {
        players.Add(player);
    }
    private void StartGameServer()
    {
        deck = GenerateDeck();
        // DEBUG: Log deck contents
        string deckLog = string.Join(", ", deck.Select(c => c.ToString()));
        Debug.Log($"Deck before sending to clients: {deckLog}");
        ShuffleDeck(deck);

        StartTurn();
    }
    void StartTurn()
    {
        if (!IsServer) return;

        //int nextIndex = currentPlayerIndex + 1;
        //if (nextIndex == -1)
        //    return;

        //currentPlayerIndex = nextIndex;

        //for (int i = 0; i < players.Count; i++)
        //{
        //    bool isTurn =
        //        i == currentPlayerIndex;

        //    players[i].SetTurnOnBusGameClientRpc(isTurn);
        //}
    }

    List<PlayingCard> GenerateDeck()
    {
        deck.Clear();

        CardSuit[] suits =
        {
        CardSuit.Hearts,
        CardSuit.Diamonds,
        CardSuit.Clubs,
        CardSuit.Spades
    };

        // Standard cards
        foreach (var suit in suits)
        {
            for (int value = 1; value <= 13; value++)
            {
                deck.Add(new PlayingCard((PlayingDeckCardValue)value,suit));
            }
        }

        // Jokers
        for (int i = 0; i < jokerCount; i++)
        {
            deck.Add(new PlayingCard(
                PlayingDeckCardValue.Joker,
                CardSuit.Joker
            ));
        }

        Debug.Log($"Bus deck generated: {deck.Count} cards");
        return deck;
    }

    void ShuffleDeck(List<PlayingCard> deck)
    {
        for (int i = 0; i < deck.Count; i++)
        {
            int randomIndex = Random.Range(i, deck.Count);
            PlayingCard temp = deck[i];
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }
    }
}
