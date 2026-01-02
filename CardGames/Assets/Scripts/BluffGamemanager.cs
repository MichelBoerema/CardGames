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
    // Singleton
    public static BluffGamemanager Instance;

    // Players & deck
    public List<Player> players = new();
    private List<CardValue> deck;

    // Game state
    public TableRank currentTableRank;
    public List<CardValue> laatsteGespeeldeKaarten = new();
    [SerializeField] private int currentPlayerIndex = 0;
    private int roundNumber = 0;
    private bool gameOver = false;

    // Bluff tracking
    private int lastPlayedPlayerIndex = -1;
    private int bluffCallerIndex = -1;

    // Bluff flow
    [SerializeField] private float bluffRevealDuration = 3f;
    [SerializeField] private float bluffTotalAnimationTime = 3f;
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
        if (gameOver)
            return;

        deck = GenerateDeck();
        ShuffleDeck(deck);
        DealCards();
        ChooseRandomTableRank();
        currentPlayerIndex = 0;

        if (roundNumber == 0)
        {
            ShowPreRoundIntroClientRpc(currentTableRank);
        }
        else
        {
            ShowRoundTableRankClientRpc(currentTableRank);
        }

        StartCoroutine(StartTurnAfterIntro());
    }
    IEnumerator StartTurnAfterIntro()
    {
        yield return new WaitForSeconds(3); // match animation duration
        StartTurn();
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

    void EndTurnServer()
    {
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

    [ServerRpc(RequireOwnership = false)]
    public void PlayCardsServerRpc(CardValue[] cards, ServerRpcParams rpcParams = default)
    {
        Player player = players[currentPlayerIndex];

        if (player.OwnerClientId != rpcParams.Receive.SenderClientId)
            return;

        lastPlayedPlayerIndex = currentPlayerIndex;
        laatsteGespeeldeKaarten.Clear();
        laatsteGespeeldeKaarten.AddRange(cards);

        UpdateLastClaimsClientRpc(
            player.PlayerName.Value,
            cards.Length,
            currentTableRank
        );

        EndTurnServer();
    }

    [ServerRpc(RequireOwnership = false)]
    public void CallBluffServerRpc(ServerRpcParams rpcParams = default)
    {
        if (isResolvingBluff)
            return;

        if (players[currentPlayerIndex].OwnerClientId != rpcParams.Receive.SenderClientId)
            return;

        bluffCallerIndex = currentPlayerIndex;
        isResolvingBluff = true;

        ShowBluffRevealClientRpc(
            players[lastPlayedPlayerIndex].PlayerName.Value,
            laatsteGespeeldeKaarten.ToArray(),
            currentTableRank
        );

        StartCoroutine(ResolveBluffSequence());
    }

    IEnumerator ResolveBluffSequence()
    {
        yield return new WaitForSeconds(bluffRevealDuration);

        int punishedIndex = DeterminePunishedPlayer();
        Player punishedPlayer = players[punishedIndex];

        punishedPlayer.PullTrigger(punishedPlayer.PlayerName.Value);
        bool survived = punishedPlayer.IsAlive;

        ShowBluffClientRpc(
            punishedPlayer.NetworkObject,
            laatsteGespeeldeKaarten.ToArray(),
            currentTableRank,
            survived
        );

        yield return new WaitForSeconds(bluffTotalAnimationTime);

        // NOW decide game state
        if (!survived && GetAlivePlayerCount() <= 1 && UIManager.Instance.isPopupLocked == false)
        {
            EndGame();
            yield break;
        }

        ResetRoundStateOnly();
        StartGameServer();

        isResolvingBluff = false;
    }

    public void EndGame()
    {
        if (gameOver)
            return;

        gameOver = true;

        Player winner = players.Find(p => p.IsAlive);
        EndGameClientRpc(winner != null ? winner.PlayerName.Value : "Nobody");
    }

    public void RestartGameServer()
    {
        if (!IsServer) return;

        foreach (Player player in players)
        {
            player.IsAlive = true;
            player.InitializeRoulette();
            player.ClearHand();
        }

        gameOver = false;
        roundNumber = 0;
        StartGameServer();
        HideGameOverClientRpc();
    }

    // ===== ROUND START =====
    [ClientRpc]
    void ShowPreRoundIntroClientRpc(TableRank rank)
    {
        UIManager.Instance?.ShowFullRoundIntro(rank);
    }

    [ClientRpc]
    void ShowRoundTableRankClientRpc(TableRank rank)
    {
        UIManager.Instance?.ShowTableRankPopup(rank);
    }

    [ClientRpc]
    void UpdateTableRankClientRpc(TableRank rank)
    {
        UIManager.Instance?.UpdateTableRank(rank);
    }

    // ===== PLAY CLAIM =====
    [ClientRpc]
    void UpdateLastClaimsClientRpc(
        FixedString32Bytes playerName,
        int amountClaimed,
        TableRank rank)
    {
        UIManager.Instance?.UpdateLastClaims(playerName, amountClaimed, rank);
    }

    // ===== BLUFF REVEAL =====
    [ClientRpc]
    void ShowBluffRevealClientRpc(
        FixedString32Bytes playerName,
        CardValue[] cards,
        TableRank rank)
    {
        UIManager.Instance?.ShowBluffReveal(playerName, cards, rank);
    }

    // ===== BLUFF RESULT =====
    [ClientRpc]
    void ShowBluffClientRpc(
        NetworkObjectReference playerRef,
        CardValue[] cards,
        TableRank rank,
        bool survived)
    {
        if (!playerRef.TryGet(out NetworkObject obj)) return;

        Player player = obj.GetComponent<Player>();
        UIManager.Instance.HidePopup();
        UIManager.Instance.ShowBluffRevealSequence(player, cards, rank, survived);
    }

    // ===== GAME OVER =====
    [ClientRpc]
    void EndGameClientRpc(FixedString32Bytes winnerName)
    {
        bool isHost = NetworkManager.Singleton.IsServer;
        UIManager.Instance?.ShowGameOver(winnerName, isHost);
    }

    [ClientRpc]
    void HideGameOverClientRpc()
    {
        UIManager.Instance?.gameOverPanel.SetActive(false);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestEndTurnServerRpc()
    {
        EndTurnServer();
    }

    void ChooseRandomTableRank()
    {
        TableRank[] ranks = { TableRank.King, TableRank.Queen, TableRank.Ace };
        currentTableRank = ranks[Random.Range(0, ranks.Length)];

        UpdateTableRankClientRpc(currentTableRank);
    }

    public void PlayCards(List<CardValue> cards)
    {
        if (!IsOwner) return;

        PlayCardsServerRpc(cards.ToArray());
    }

    int DeterminePunishedPlayer()
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

        return isBluff ? lastPlayedPlayerIndex : bluffCallerIndex;
    }
    void ResetRoundStateOnly()
    {
        laatsteGespeeldeKaarten.Clear();
        deck?.Clear();
        roundNumber++;

        lastPlayedPlayerIndex = -1;
        bluffCallerIndex = -1;

        UIManager.Instance.lastClaims.text = "Waiting For\nFirst Claim";

        foreach (Player player in players)
        {
            player.ClearHand();
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
}