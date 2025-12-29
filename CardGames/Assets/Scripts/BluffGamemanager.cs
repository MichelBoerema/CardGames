using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    private int roundNumber = 0;

    [Header("Bluff Tracking")]
    private int lastPlayedPlayerIndex = -1;
    private int bluffCallerIndex = -1;

    [Header("Bluff Cooldown")]
    [SerializeField] private float bluffRevealDuration = 3f;
    private bool isResolvingBluff = false;


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

        foreach (Player player in players)
        {
            player.InitializeRoulette();
        }
    }

    public void RegisterPlayer(Player player)
    {
        players.Add(player);
    }
    private void StartGameServer()
    {
        deck = GenerateDeck();
        ShuffleDeck(deck);
        DealCards();
        ChooseRandomTableRank();
        currentPlayerIndex = 0;

        if(roundNumber == 0)
            ShowRoundStartClientRpc(currentTableRank);
        else
            showRoundTableRankClientRpc(currentTableRank);


        StartTurn();
    }

    [ClientRpc]
    void ShowRoundStartClientRpc(TableRank rank)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowRoundStartPopup(rank);
        }
    }
    [ClientRpc]
    void showRoundTableRankClientRpc(TableRank rank)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowTableRankPopup(rank);
        }
    }

    void StartTurn()
    {
        if (!IsServer) return;

        for (int i = 0; i < players.Count; i++)
        {
            bool isTurn = (i == currentPlayerIndex && players[i].IsAlive);
            players[i].SetTurnClientRpc(isTurn);
        }
    }


    [ServerRpc(RequireOwnership = false)]
    public void RequestEndTurnServerRpc()
    {
        EndTurnServer();
    }


    void EndTurnServer()
    {
        if (!IsServer) return;

        AdvanceToNextAlivePlayer();
    }

    void AdvanceToNextAlivePlayer()
    {
        int nextIndex = GetNextAlivePlayerIndex(currentPlayerIndex);

        if (nextIndex == -1)
        {
            EndGame();
            return;
        }

        currentPlayerIndex = nextIndex;
        StartTurn();
    }

    void ChooseRandomTableRank()
    {
        TableRank[] ranks = { TableRank.King, TableRank.Queen, TableRank.Ace };
        currentTableRank = ranks[Random.Range(0, ranks.Length)];

        UpdateTableRankClientRpc(currentTableRank);
    }

    [ClientRpc]
    void UpdateTableRankClientRpc(TableRank rank)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateTableRank(rank);
        }
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

        lastPlayedPlayerIndex = currentPlayerIndex;

        laatsteGespeeldeKaarten.Clear();
        laatsteGespeeldeKaarten.AddRange(cards);

        UpdateLastClaimsClientRpc(player.PlayerName.Value, laatsteGespeeldeKaarten.Count, currentTableRank);

        EndTurnServer();
    }

    [ClientRpc]
    void UpdateLastClaimsClientRpc(FixedString32Bytes PlayerName, int amountClaimed, TableRank rank)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateLastClaims(PlayerName, amountClaimed, rank);
        }
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

        int punishedPlayerIndex;

        if (isBluff)
        {
            Debug.Log("BLUFF CALLED! Player was lying.");
            punishedPlayerIndex = lastPlayedPlayerIndex;
        }
        else
        {
            Debug.Log("NO BLUFF! Caller was wrong.");
            punishedPlayerIndex = bluffCallerIndex;
        }

        ApplyRoulettePunishment(punishedPlayerIndex);
        ResetGameState();
    }

    void ApplyRoulettePunishment(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= players.Count)
            return;

        Player punishedPlayer = players[playerIndex];

        Debug.Log($"Player {punishedPlayer.PlayerName.Value} pulls the trigger");

        punishedPlayer.PullTrigger(punishedPlayer.PlayerName.Value);
    }

    [ServerRpc(RequireOwnership = false)]
    public void CallBluffServerRpc(ServerRpcParams rpcParams = default)
    {
        if (isResolvingBluff)
            return;

        ulong sender = rpcParams.Receive.SenderClientId;

        if (players[currentPlayerIndex].OwnerClientId != sender)
            return;

        bluffCallerIndex = currentPlayerIndex;
        isResolvingBluff = true;

        // ONLY reveal here
        ShowBluffRevealClientRpc(
            players[lastPlayedPlayerIndex].PlayerName.Value,
            laatsteGespeeldeKaarten.ToArray(),
            currentTableRank
        );

        StartCoroutine(ResolveBluffAfterDelay());
    }
    IEnumerator ResolveBluffAfterDelay()
    {
        yield return new WaitForSeconds(bluffRevealDuration);

        int punishedIndex = ResolveBluffInternal();
        bool survived = players[punishedIndex].IsAlive;

        ShowBluffSurvivalClientRpc(
            players[punishedIndex].PlayerName.Value,
            survived
        );

        yield return new WaitForSeconds(2.5f);

        EndTurnServer();
        isResolvingBluff = false;
    }
    int ResolveBluffInternal()
    {
        bool isBluff = false;

        foreach (CardValue card in laatsteGespeeldeKaarten)
        {
            if (card == CardValue.Joker)
                continue;

            if (!DoesCardMatchTableRank(card))
            {
                isBluff = true;
                break;
            }
        }

        int punishedPlayerIndex =
            isBluff ? lastPlayedPlayerIndex : bluffCallerIndex;

        ApplyRoulettePunishment(punishedPlayerIndex);
        ResetGameState();

        return punishedPlayerIndex;
    }
    [ClientRpc]
    void ShowBluffSurvivalClientRpc(
    FixedString32Bytes playerName,
    bool survived)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowBluffSurvivalPopup(playerName, survived);
        }
    }
    [ClientRpc]
    void ShowBluffRevealClientRpc(
        FixedString32Bytes playerName,
        CardValue[] cards,
        TableRank rank)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowBluffReveal(
                playerName,
                cards,
                rank
            );
        }
    }

    public void OnPlayerDied(Player player)
    {
        if (!IsServer) return;

        int aliveCount = GetAlivePlayerCount();

        Debug.Log($"Alive players remaining: {aliveCount}");

        if (aliveCount <= 1)
        {
            EndGame();
            return;
        }

        // If the dead player was the current turn holder, move on
        if (players[currentPlayerIndex] == player)
        {
            AdvanceToNextAlivePlayer();
        }
    }

    int GetAlivePlayerCount()
    {
        int count = 0;
        foreach (var player in players)
        {
            if (player.IsAlive)
                count++;
        }
        return count;
    }

    int GetNextAlivePlayerIndex(int startIndex)
    {
        int index = startIndex;

        for (int i = 0; i < players.Count; i++)
        {
            index = (index + 1) % players.Count;

            if (players[index].IsAlive)
                return index;
        }

        return -1; 
    }

    void EndGame()
    {
        Player winner = null;

        foreach (var player in players)
        {
            if (player.IsAlive)
            {
                winner = player;
                break;
            }
        }

        Debug.Log($"GAME OVER! Winner: {winner?.PlayerName.Value}");

        EndGameClientRpc(winner != null ? winner.PlayerName.Value : "Nobody");
    }


    [ClientRpc]
    void EndGameClientRpc(FixedString32Bytes winnerName)
    {
        bool isHost = NetworkManager.Singleton.IsServer;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowGameOver(winnerName, isHost);
        }
    }

    public void RestartGameServer()
    {
        if (!IsServer) return;

        Debug.Log("Restarting game...");

        // Reset players
        foreach (Player player in players)
        {
            player.IsAlive = true;
            player.InitializeRoulette();
            player.ClearHand();
        }

        lastPlayedPlayerIndex = -1;
        bluffCallerIndex = -1;
        roundNumber = 0;
        laatsteGespeeldeKaarten.Clear();

        StartGameServer();
        HideGameOverClientRpc();
    }

    [ClientRpc]
    void HideGameOverClientRpc()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.gameOverPanel.SetActive(false);
        }
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
                if (!players[i].IsAlive)
                    continue;

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

    void ResetGameState()
    {
        if (!IsServer) return;

        laatsteGespeeldeKaarten.Clear();
        deck?.Clear();
        roundNumber++;

        foreach (Player player in players)
        {
            player.ClearHand();
        }
        StartGameServer();
    }
}