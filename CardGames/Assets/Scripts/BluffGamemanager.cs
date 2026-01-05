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

        StartTurn();
        //StartCoroutine(StartTurnAfterIntro());
    }

    void StartTurn()
    {
        if (!IsServer) return;

        int playersWithCards = GetPlayersWithCardsCount();

        if (playersWithCards == 1)
        {
            int lastIndex = GetLastPlayerWithCardsIndex();
            if (lastIndex != -1)
            {
                currentPlayerIndex = lastIndex;
                ForceCallBluffClientRpc(players[lastIndex].OwnerClientId);
            }
            return;
        }

        int nextIndex = GetNextPlayerWithCards(currentPlayerIndex);
        if (nextIndex == -1)
            return;

        currentPlayerIndex = nextIndex;

        for (int i = 0; i < players.Count; i++)
        {
            bool isTurn =
                i == currentPlayerIndex &&
                players[i].IsAlive &&
                players[i].HasCardsInHand();

            players[i].SetTurnClientRpc(isTurn);
        }
    }

    int GetPlayersWithCardsCount()
    {
        int count = 0;

        foreach (var player in players)
        {
            if (player.IsAlive && player.HasCardsInHand())
                count++;
        }

        return count;
    }

    int GetLastPlayerWithCardsIndex()
    {
        int index = -1;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].IsAlive && players[i].HasCardsInHand())
            {
                if (index != -1)
                    return -1;

                index = i;
            }
        }

        return index;
    }

    int GetNextPlayerWithCards(int startIndex)
    {
        int index = startIndex;

        for (int i = 0; i < players.Count; i++)
        {
            index = (index + 1) % players.Count;

            if (players[index].IsAlive && players[index].HasCardsInHand())
                return index;
        }

        return -1;
    }

    void EndTurnServer()
    {
        StartTurn();
    }

    void AdvanceToNextAlivePlayer()
    {
        int nextIndex = GetNextAlivePlayerIndex(currentPlayerIndex);
        if (nextIndex == -1)
        {
            //EndGame();
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
        ShowPlayedCardsPileClientRpc(cards.Length);

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

        yield break;
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

        NetworkManager.SceneManager.LoadScene(
            "BluffGame",
            LoadSceneMode.Single
        );
    }

    public void GoBackToLobby()
    {
        if (!IsServer) return;

        NetworkManager.SceneManager.LoadScene(
            "Lobby",
            LoadSceneMode.Single
        );
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

    [ClientRpc]
    void ShowPlayedCardsPileClientRpc(int cardCount)
    {
        UIManager.Instance?.ShowPlayedCardsPile(cardCount);
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

    [ClientRpc]
    void ForceCallBluffClientRpc(ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId)
            return;

        UIManager.Instance?.ForceCallBluffOnly();
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

    [ServerRpc(RequireOwnership = false)]
    public void BluffAnimationFinishedServerRpc()
    {
        if (!isResolvingBluff)
            return;

        ContinueAfterBluffServer();
    }

    void ContinueAfterBluffServer()
    {
        if (!IsServer) return;

        if (GetAlivePlayerCount() <= 1)
        {
            EndGame();
            return;
        }

        ResetRoundStateOnly();
        isResolvingBluff = false;
        StartGameServer();
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

        Debug.Log("Player died during bluff resolution");

        if (isResolvingBluff)
            return;

        int aliveCount = GetAlivePlayerCount();

        if (aliveCount <= 1)
        {
            EndGame();
            return;
        }

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
        List<CardValue> deck = new List<CardValue>();

        int cardsPerPlayer = 5;
        int totalCardsNeeded = players.Count * cardsPerPlayer;

        // Reserve 1 Joker minimum
        int remainingCards = totalCardsNeeded - 1;

        // Make remaining cards divisible by 3 (K/Q/A)
        int baseSetSize = (remainingCards / 3) * 3;
        int perTypeCount = baseSetSize / 3;

        // Add equal Kings / Queens / Aces
        for (int i = 0; i < perTypeCount; i++)
        {
            deck.Add(CardValue.King);
            deck.Add(CardValue.Queen);
            deck.Add(CardValue.Ace);
        }

        // Add Jokers to fill the rest (at least 1)
        while (deck.Count < totalCardsNeeded)
        {
            deck.Add(CardValue.Joker);
        }

        return deck;
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