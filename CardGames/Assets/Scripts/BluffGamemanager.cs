using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;

public enum CardValue
{
    King,
    Queen,
    Ace,
    Joker
}

public enum TableRank
{
    King,
    Queen,
    Ace
}



public class BluffGamemanager : MonoBehaviour
{
    public List<Player> players = new List<Player>();

    private List<CardValue> deck;

    public static BluffGamemanager Instance;

    [Header("Game State")]
    public TableRank currentTableRank;
    public List<CardValue> laatsteGespeeldeKaarten = new List<CardValue>();
    private int currentPlayerIndex = 0;


    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        // Alle spelers in de scene automatisch vinden
        players.AddRange(FindObjectsOfType<Player>());

        deck = GenerateDeck(players.Count);
        ShuffleDeck(deck);
        DealCards();
        ChooseRandomTableRank();
        StartTurn();

    }

    void StartTurn()
    {
        for (int i = 0; i < players.Count; i++)
        {
            players[i].SetTurn(i == currentPlayerIndex);
        }

        Debug.Log($"Turn started for player {currentPlayerIndex}");
    }

    public void EndTurn()
    {
        currentPlayerIndex++;

        if (currentPlayerIndex >= players.Count)
            currentPlayerIndex = 0;

        StartTurn();
    }


    void ChooseRandomTableRank()
    {
        TableRank[] ranks = { TableRank.King, TableRank.Queen, TableRank.Ace };
        currentTableRank = ranks[Random.Range(0, ranks.Length)];

        Debug.Log("Current Table Rank: " + currentTableRank);

        UIManager.Instance.UpdateTableRank(currentTableRank);
    }


    public void PlayCards(List<CardValue> cards)
    {
        laatsteGespeeldeKaarten.Clear();
        laatsteGespeeldeKaarten.AddRange(cards);

        Debug.Log("Laatst gespeelde kaarten:");
        foreach (var card in laatsteGespeeldeKaarten)
        {
            Debug.Log(card);
        }

        EndTurn();
    }

    public void CallBluff()
    {
        if (laatsteGespeeldeKaarten.Count == 0)
        {
            Debug.Log("No cards have been played yet.");
            return;
        }

        bool isBluff = false;

        foreach (CardValue card in laatsteGespeeldeKaarten)
        {
            // Joker is altijd geldig
            if (card == CardValue.Joker)
                continue;

            // Komt kaart niet overeen met table rank  bluff
            if (!DoesCardMatchTableRank(card))
            {
                isBluff = true;
                break;
            }
        }

        if (isBluff)
        {
            Debug.Log("BLUFF CALLED! Player was lying.");
            // later: straf voor speler die loog
        }
        else
        {
            Debug.Log("NO BLUFF! Cards were honest.");
            // later: straf voor caller
        }
        EndTurn();
    }

    bool DoesCardMatchTableRank(CardValue card)
    {
        switch (currentTableRank)
        {
            case TableRank.King:
                return card == CardValue.King;
            case TableRank.Queen:
                return card == CardValue.Queen;
            case TableRank.Ace:
                return card == CardValue.Ace;
            default:
                return false;
        }
    }

    void DealCards()
    {
        int currentPlayerIndex = 0;

        while (deck.Count > 0)
        {
            CardValue card = deck[0];
            deck.RemoveAt(0);

            players[currentPlayerIndex].AddCard(card);

            currentPlayerIndex++;
            if (currentPlayerIndex >= players.Count)
            {
                currentPlayerIndex = 0;
            }
        }

        // Debug: check handen
        foreach (Player player in players)
        {
            Debug.Log($"Player {player.name} has {player.hand.Count} cards");
        }
    }

    List<CardValue> GenerateDeck(int players)
    {
        List<CardValue> newDeck = new List<CardValue>();
        int cardsPerType = players + 2;

        for (int i = 0; i < cardsPerType; i++)
        {
            newDeck.Add(CardValue.King);
            newDeck.Add(CardValue.Queen);
            newDeck.Add(CardValue.Ace);
        }

        newDeck.Add(CardValue.Joker);
        newDeck.Add(CardValue.Joker);

        return newDeck;
    }

    void ShuffleDeck(List<CardValue> deck)
    {
        for (int i = 0; i < deck.Count; i++)
        {
            int randomIndex = Random.Range(i, deck.Count);
            CardValue temp = deck[i];
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }
    }
}