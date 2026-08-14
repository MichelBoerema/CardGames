using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Top-level, per-match manager. It doesn't know the rules of any specific
/// gamemode - it only answers "is this game played with dice or cards?",
/// hands out the right resources, and builds the turn order.
/// Individual gamemodes (e.g. MexicoGameManager) read from this once and
/// then run their own flow.
/// </summary>
public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    public enum GameCategory
    {
        Dice,
        Cards
    }

    [Serializable]
    public struct GameModeConfig
    {
        [Tooltip("Must match the scene name used in LobbyManager.SelectGamemode / NetworkManager.SceneManager")]
        public string sceneName;
        public GameCategory category;

        [Tooltip("Dice: how many dice each player gets. Cards: how many cards each player is dealt.")]
        public int resourceAmount;

        [Tooltip("Cards only: include the two jokers in the deck.")]
        public bool includeJokers;
    }

    [Header("Gamemode -> Resource mapping")]
    public List<GameModeConfig> gameModeConfigs = new List<GameModeConfig>()
    {
        new GameModeConfig { sceneName = "MEXICO", category = GameCategory.Dice,  resourceAmount = 2, includeJokers = false },
        new GameModeConfig { sceneName = "BluffGame",  category = GameCategory.Cards, resourceAmount = 5, includeJokers = false },
        new GameModeConfig { sceneName = "BusGame",    category = GameCategory.Cards, resourceAmount = 0, includeJokers = false },
    };

    [Header("Turn Order")]
    [Tooltip("If true, players keep the order they appear in (e.g. join order). If false, order is shuffled at game start.")]
    public bool useOriginalPlayerOrder = true;

    public GameCategory CurrentCategory { get; private set; }
    public int CurrentResourceAmount { get; private set; }

    private readonly List<Player> turnOrder = new List<Player>();
    private int currentTurnIndex;

    public IReadOnlyList<Player> TurnOrder => turnOrder;
    public Player CurrentTurnPlayer => turnOrder.Count == 0 ? null : turnOrder[currentTurnIndex];

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Call this once the gameplay scene has loaded (server only). Looks up the
    /// config for the given scene, distributes resources, builds turn order,
    /// and returns the resulting order so the specific gamemode manager can start.
    /// </summary>
    public List<Player> SetupGame(string sceneName)
    {
        if (!IsServer)
        {
            Debug.LogWarning("GameManager.SetupGame called on a non-server instance. Ignoring.");
            return null;
        }

        GameModeConfig config = default;
        bool found = false;
        foreach (var c in gameModeConfigs)
        {
            if (c.sceneName == sceneName)
            {
                config = c;
                found = true;
                break;
            }
        }

        if (!found)
        {
            Debug.LogError($"GameManager: No GameModeConfig found for scene '{sceneName}'.");
            return null;
        }

        CurrentCategory = config.category;
        CurrentResourceAmount = config.resourceAmount;

        BuildTurnOrder();

        if (CurrentCategory == GameCategory.Cards)
        {
            DealCards(config.resourceAmount, config.includeJokers);
        }
        // Dice games don't need physical distribution - CurrentResourceAmount tells
        // the specific dice-game manager how many dice each player rolls with.

        return turnOrder;
    }

    private void BuildTurnOrder()
    {
        turnOrder.Clear();
        turnOrder.AddRange(FindObjectsOfType<Player>());

        if (!useOriginalPlayerOrder)
        {
            ShuffleInPlace(turnOrder);
        }

        currentTurnIndex = 0;
    }

    private static void ShuffleInPlace(List<Player> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void DealCards(int cardsPerPlayer, bool includeJokers)
    {
        List<PlayingCard> deck = BuildDeck(includeJokers);
        ShuffleDeck(deck);

        int deckIndex = 0;
        foreach (var player in turnOrder)
        {
            for (int i = 0; i < cardsPerPlayer && deckIndex < deck.Count; i++, deckIndex++)
            {
                player.AddCard(deck[deckIndex]);
            }
        }
    }

    public static List<PlayingCard> BuildDeck(bool includeJokers)
    {
        var deck = new List<PlayingCard>(includeJokers ? 54 : 52);

        foreach (CardSuit suit in Enum.GetValues(typeof(CardSuit)))
        {
            for (int value = (int)PlayingDeckCardValue.Ace; value <= (int)PlayingDeckCardValue.King; value++)
            {
                deck.Add(new PlayingCard((PlayingDeckCardValue)value, suit));
            }
        }

        if (includeJokers)
        {
            deck.Add(new PlayingCard(PlayingDeckCardValue.Joker, CardSuit.Hearts));
            deck.Add(new PlayingCard(PlayingDeckCardValue.Joker, CardSuit.Spades));
        }

        return deck;
    }

    public static void ShuffleDeck(List<PlayingCard> deck)
    {
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
    }

    /// <summary>
    /// Generic turn advance helper for gamemodes that just want a simple
    /// round-robin. Games with their own turn logic (like Mexico's leader/
    /// pass-lead flow) can ignore this and manage their own index instead.
    /// </summary>
    public void AdvanceTurn()
    {
        if (turnOrder.Count == 0) return;

        currentTurnIndex = (currentTurnIndex + 1) % turnOrder.Count;
        NotifyTurn();
    }

    public void SetTurnIndex(int index)
    {
        if (turnOrder.Count == 0) return;
        currentTurnIndex = ((index % turnOrder.Count) + turnOrder.Count) % turnOrder.Count;
        NotifyTurn();
    }

    private void NotifyTurn()
    {
        foreach (var p in turnOrder)
        {
            p.SetTurnClientRpc(p == CurrentTurnPlayer);
        }
    }
}
