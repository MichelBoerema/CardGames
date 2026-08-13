using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    public List<Player> players = new List<Player>();
    private List<PlayingCard> deck;

    // Game state
    public TableRank currentTableRank;
    public List<PlayingCard> laatsteGespeeldeKaarten = new List<PlayingCard>();
    [SerializeField] private int currentPlayerIndex = 0;
    private int roundNumber = 0;
    private bool gameOver = false;

    // Bluff tracking
    private int lastPlayedPlayerIndex = -1;
    private int bluffCallerIndex = -1;
    private int punishedPlayerIndex = -1;

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

        foreach (Player player in players)
        {
            player.SetMyNameUIClientRpc();
        }
        deck = GenerateDeck();
        ShuffleDeck(deck);
        ChooseRandomTableRank();
        if (punishedPlayerIndex != -1)
        {
            currentPlayerIndex = 0;
        }

        if (roundNumber == 0)
        {
            ShowPreRoundIntroClientRpc(currentTableRank, deck.ToArray());
        }
        else
        {
            ShowRoundTableRankClientRpc(currentTableRank);
        }

        DealCards();
        StartTurn();
    }


    void StartTurn()
    {
        if (!IsServer) return;

        List<int> playersWithCards = new List<int>();

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].IsAlive && players[i].HasCardsInHand())
                playersWithCards.Add(i);
        }

        if (playersWithCards.Count == 1)
        {
            int lastIndex = playersWithCards[0];
            currentPlayerIndex = lastIndex;

            for (int i = 0; i < players.Count; i++)
            {
                players[i].SetTurnClientRpc(false);
            }

            ForceCallBluffClientRpc(players[lastIndex].OwnerClientId);
            return;
        }

        if (playersWithCards.Count == 0)
        {
            Debug.LogWarning("No players with cards left!");
            return;
        }

        int nextIndex = GetNextPlayerWithCards(currentPlayerIndex);
        if (nextIndex == -1)
            return;

        Player lastPlayer = players[currentPlayerIndex];

        currentPlayerIndex = nextIndex;

        UpdatePlayingPlayerClientRpc(
        players[currentPlayerIndex].PlayerName.Value
        );

        for (int i = 0; i < players.Count; i++)
        {
            bool isTurn =
                i == currentPlayerIndex &&
                players[i].IsAlive &&
                players[i].HasCardsInHand();

            players[i].SetTurnClientRpc(isTurn);
        }
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
    public void PlayCardsServerRpc(PlayingCard[] cards, ServerRpcParams rpcParams = default)
    {
        Player player = players[currentPlayerIndex];

        if (player.OwnerClientId != rpcParams.Receive.SenderClientId)
            return;

        // Remove cards from the server's authoritative hand
        foreach (var card in cards)
        {
            player.hand.Remove(card);
        }

        laatsteGespeeldeKaarten.Clear();
        laatsteGespeeldeKaarten.AddRange(cards);

        ShowLastPlayedPlayerInfoClientRpc(
        player.PlayerName.Value,
        player.AvatarId.Value,
        player.hand.Count,
        player.points
        );

        // Proceed with other gameplay logic
        lastPlayedPlayerIndex = currentPlayerIndex;
        laatsteGespeeldeKaarten.Clear();
        laatsteGespeeldeKaarten.AddRange(cards);

        UpdateLastClaimsClientRpc(player.PlayerName.Value, cards.Length, currentTableRank);
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
        punishedPlayerIndex = DeterminePunishedPlayer();
        Player punishedPlayer = players[punishedPlayerIndex];
        Player lastPlayer = players[lastPlayedPlayerIndex];

        punishedPlayer.PullTrigger(punishedPlayer.PlayerName.Value);
        bool survived = punishedPlayer.IsAlive;

        ShowBluffClientRpc(
            lastPlayer.NetworkObject,
            punishedPlayer.NetworkObject,
            punishedPlayer.points,
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

        gameOver = false;
        roundNumber = 0;
        isResolvingBluff = false;

        lastPlayedPlayerIndex = -1;
        bluffCallerIndex = -1;
        players.Reverse();
        currentPlayerIndex = 0;

        foreach (Player p in players)
        {
            p.ResetForNewGame();
        }

        ResetRoundStateOnly();
        HideGameOverClientRpc();

        StartGameServer();
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
    void ShowPreRoundIntroClientRpc(TableRank rank, PlayingCard[] deck)
    {
        UIManager.Instance?.ShowFullRoundIntro(rank, new List<PlayingCard>(deck));
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

    [ClientRpc]
    void ResetLastClaimsClientRpc()
    {
        UIManager.Instance.lastClaims.text = "Waiting For\nFirst Claim";
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
    void UpdatePlayingPlayerClientRpc(FixedString32Bytes playerName)
    {
        UIManager.Instance?.UpdatePlayingPlayer(playerName);
    }

    [ClientRpc]
    void ShowLastPlayedPlayerInfoClientRpc(
    FixedString32Bytes playerName,
    int avatarId,
    int cardsLeft,
    int points
)
    {
        if (UIManager.Instance == null)
            return;

        UIManager.Instance.UpdateNextPlayerInfo(playerName, avatarId, cardsLeft, points);
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
    PlayingCard[] cards,
    TableRank rank)
    {
        Debug.Log($"ShowBluffRevealClientRpc called for {playerName}");
        //UIManager.Instance?.ShowBluffReveal(playerName, cards, rank);
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
    NetworkObjectReference lastPlayerRef,
    NetworkObjectReference punishedPlayerRef,
    int chamberIndex,
    PlayingCard[] cards,
    TableRank rank,
    bool survived)
    {
        if (!punishedPlayerRef.TryGet(out NetworkObject Pp_obj)) return;
        if (!lastPlayerRef.TryGet(out NetworkObject Lp_obj)) return;

        Player punishedPlayer = Pp_obj.GetComponent<Player>();
        Player lastPlayer = Lp_obj.GetComponent<Player>();
        UIManager.Instance.HidePopup();
        UIManager.Instance.ShowBluffRevealSequence(
            lastPlayer,
            punishedPlayer,
            chamberIndex,
            cards,
            rank,
            survived
        );
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

        if (punishedPlayerIndex != -1)
        {
            // Set index to one before punished player
            currentPlayerIndex =
                (punishedPlayerIndex - 1 + players.Count) % players.Count;
        }

        punishedPlayerIndex = -1;

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
        UIManager.Instance?.HideGameOver();
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

    public void PlayCards(List<PlayingCard> cards)
    {
        if (!IsOwner) return;

        PlayCardsServerRpc(cards.ToArray());
    }

    int DeterminePunishedPlayer()
    {
        bool isBluff = false;

        foreach (PlayingCard card in laatsteGespeeldeKaarten)
        {
            if (card.Value == PlayingDeckCardValue.Joker)
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

        ResetLastClaimsClientRpc();

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

    public void LeaveGame()
    {
        Debug.Log("Leaving game");

        if (NetworkManager.Singleton == null)
        {
            SceneManager.LoadScene("Lobby");
            return;
        }

        // CLIENT leaves
        if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            RemoveLocalPlayerFromList();
            NetworkManager.Singleton.Shutdown();
            SceneManager.LoadScene("Lobby");
            return;
        }

        // HOST leaves (shuts down entire session)
        if (NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.Shutdown();
            SceneManager.LoadScene("Lobby");
        }
    }
    void RemoveLocalPlayerFromList()
    {
        ulong localClientId = NetworkManager.Singleton.LocalClientId;

        Player playerToRemove = players.Find(
            p => p.OwnerClientId == localClientId
        );

        if (playerToRemove != null)
        {
            players.Remove(playerToRemove);
            Debug.Log($"Removed player {playerToRemove.PlayerName.Value} from game");
        }
    }
    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    void OnClientDisconnected(ulong clientId)
    {
        Player player = players.Find(p => p.OwnerClientId == clientId);
        if (player != null)
        {
            players.Remove(player);
            Debug.Log($"Player {player.PlayerName.Value} disconnected and removed");
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

    bool DoesCardMatchTableRank(PlayingCard card)
    {
        switch (currentTableRank)
        {
            case TableRank.King:
                return card.Value == PlayingDeckCardValue.King;

            case TableRank.Queen:
                return card.Value == PlayingDeckCardValue.Queen;

            case TableRank.Ace:
                return card.Value == PlayingDeckCardValue.Ace;

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

                PlayingCard card = deck[0];
                deck.RemoveAt(0);

                players[i].AddCard(card);
            }
        }
    }

    List<PlayingCard> GenerateDeck()
    {
        List<PlayingCard> deck = new List<PlayingCard>();

        int cardsPerPlayer = 5;
        int totalCardsNeeded = players.Count * cardsPerPlayer;

        // Reserve at least 1 Joker
        int remainingCards = totalCardsNeeded - 1;

        // Make remaining cards divisible by 3 (King/Queen/Ace)
        int baseSetSize = (remainingCards / 3) * 3;
        int perTypeCount = baseSetSize / 3;

        CardSuit[] suits =
        {
        CardSuit.Hearts,
        CardSuit.Diamonds,
        CardSuit.Clubs,
        CardSuit.Spades
    };

        int suitIndex = 0;

        // Add equal Kings / Queens / Aces
        for (int i = 0; i < perTypeCount; i++)
        {
            deck.Add(new PlayingCard(PlayingDeckCardValue.King, suits[suitIndex++ % suits.Length]));
            deck.Add(new PlayingCard(PlayingDeckCardValue.Queen, suits[suitIndex++ % suits.Length]));
            deck.Add(new PlayingCard(PlayingDeckCardValue.Ace, suits[suitIndex++ % suits.Length]));
        }

        // Add Jokers to fill the rest (at least 1)
        while (deck.Count < totalCardsNeeded)
        {
            deck.Add(new PlayingCard(
                PlayingDeckCardValue.Joker,
                CardSuit.Joker));
        }

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