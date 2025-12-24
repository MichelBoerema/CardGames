using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
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



public class BluffGamemanager : NetworkBehaviour
{
    public List<Player> players = new List<Player>();

    private List<CardValue> deck;

    public static BluffGamemanager Instance;

    [Header("Game State")]
    public TableRank currentTableRank;
    public List<CardValue> laatsteGespeeldeKaarten = new List<CardValue>();
    [SerializeField] private int currentPlayerIndex = 0;

    private void Start()
    {
        if (Instance == null)
            Instance = this;
    }
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        players.Clear();
        currentPlayerIndex = 0;

        // Find all Player objects that came from lobby
        foreach (var player in FindObjectsOfType<Player>())
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
        if (sceneName != "BluffGame")
            return;

        StartGameServer();
    }

    public void RegisterPlayer(Player player)
    {
        players.Add(player);
    }
    private void StartGameServer()
    {
        //Debug.Log("starting game server");
        deck = GenerateDeck();
        ShuffleDeck(deck);
        DealCards();
        ChooseRandomTableRank();
        currentPlayerIndex = 0;
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

    [ServerRpc(RequireOwnership = false)]
    public void RequestEndTurnServerRpc()
    {
        EndTurnServer();
    }


    void EndTurnServer()
    {
        if (!IsServer) return;

        currentPlayerIndex++;

        if (currentPlayerIndex >= players.Count)
            currentPlayerIndex = 0;

        StartTurn();
    }


    void ChooseRandomTableRank()
    {
        TableRank[] ranks = { TableRank.King, TableRank.Queen, TableRank.Ace };
        currentTableRank = ranks[Random.Range(0, ranks.Length)];

        UIManager.Instance.UpdateTableRank(currentTableRank);
    }


    public void PlayCards(List<CardValue> cards)
    {
        if (!IsOwner) return;

        PlayCardsServerRpc(cards.ToArray());
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayCardsServerRpc(CardValue[] cards, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        Player player = players[currentPlayerIndex];

        if (player.OwnerClientId != senderClientId)
        {
            Debug.LogWarning("Player tried to play out of turn!");
            return;
        }

        Debug.Log($"IsServer={IsServer} | IsClient={IsClient}");
        Debug.Log($"[SERVER] Player {senderClientId} played {cards.Length} cards");

        laatsteGespeeldeKaarten.Clear();
        laatsteGespeeldeKaarten.AddRange(cards);

        EndTurnServer();
    }

    public void ResolveBluff()
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
        RequestEndTurnServerRpc();
    }
    [ServerRpc(RequireOwnership = false)]
    public void CallBluffServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;

        if (players[currentPlayerIndex].OwnerClientId != sender)
            return;

        ResolveBluff();
        EndTurnServer();
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
        const int CARDS_PER_PLAYER = 5;

        for (int round = 0; round < CARDS_PER_PLAYER; round++)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (deck.Count == 0)
                {
                    Debug.LogWarning("Deck ran out of cards early!");
                    return;
                }

                CardValue card = deck[0];
                deck.RemoveAt(0);

                players[i].AddCard(card);
            }
        }
    }

    List<CardValue> GenerateDeck()
    {
        List<CardValue> newDeck = new List<CardValue>();
        int cardsPerType = 6;

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