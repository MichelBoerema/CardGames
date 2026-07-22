using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;



public enum CardSuit
{
    Hearts,
    Diamonds,
    Clubs,
    Spades,
    Joker
}

public enum BusRound
{
    RedBlack = 0,        // 0 cards
    HigherLower = 1,     // 1 card
    InsideOutside = 2,  // 2 cards
    SuitGuess = 3       // 3 cards
}

enum BusRoundPhase
{
    Choosing,        // Players make guesses
    Revealing,       // Cards revealed + correct/wrong popups
    GivingPoints,    // Correct players select targets
    Summary          // Show round summary
}

public enum RedBlackChoice
{
    None,
    Red,
    Black
}

public enum HigherLowerChoice
{
    None,
    Higher,
    Lower
}

public enum InsideOutsideChoice
{
    None,
    Inside,
    Outside
}

public enum HasSuitChoice
{
    None,
    Yes,
    No
}

public struct BusPlayerChoices
{
    public RedBlackChoice redBlack;
    public HigherLowerChoice higherLower;
    public InsideOutsideChoice insideOutside;

    public CardSuit suit;
    public HasSuitChoice hasSuit;
}


public class BusGamemanager : NetworkBehaviour
{
    public static BusGamemanager Instance;

    public List<Player> players = new();
    private int currentPlayerIndex = 0;
    private int pendingPointRewards = 0;

    private BusRoundPhase currentPhase;
    private BusRound currentRound = BusRound.RedBlack;
    private Dictionary<ulong, BusPlayerChoices> playerChoices = new();

    private Dictionary<ulong, bool> roundCorrect = new();
    private Dictionary<ulong, int> pointsReceivedThisRound = new();
    private HashSet<ulong> playersWhoMustGivePoint = new();

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
        if (sceneName != "BusGame")
            return;

        StartGameServer();
    }

    public void RegisterPlayer(Player player)
    {
        players.Add(player);
    }
    private void StartGameServer()
    {
        deck = GenerateDeck();
        string deckLog = string.Join(", ", deck.Select(c => c.ToString()));
        Debug.Log($"Deck before sending to clients: {deckLog}");
        ShuffleDeck(deck);

        playerChoices.Clear();
        foreach (var player in players)
        {
            playerChoices[player.OwnerClientId] = new BusPlayerChoices();
        }

        StartRound(currentRound);
    }

    void AdvanceRound()
    {
        if (currentRound < BusRound.SuitGuess)
        {
            currentRound++;
            StartRound(currentRound);
        }
        else
        {
            Debug.Log("Rounds complete, Start bus!");
            // End game or start bus punishment phase
        }
    }

    void StartRound(BusRound round)
    {
        currentPhase = BusRoundPhase.Choosing;

        roundCorrect.Clear();
        pointsReceivedThisRound.Clear();
        playersWhoMustGivePoint.Clear();

        foreach (var player in players)
        {
            switch (round)
            {
                case BusRound.RedBlack:
                    ShowRedBlackChoiceClientRpc(player.OwnerClientId);
                    break;

                case BusRound.HigherLower:
                    ShowHigherLowerChoiceClientRpc(player.OwnerClientId);
                    break;

                case BusRound.InsideOutside:
                    ShowInsideOutsideChoiceClientRpc(player.OwnerClientId);
                    break;

                case BusRound.SuitGuess:
                    ShowSuitChoiceClientRpc(player.OwnerClientId);
                    break;
            }
        }
    }

    [ClientRpc]
    void ShowRedBlackChoiceClientRpc(
    ulong targetClientId,
    ClientRpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId)
            return;

        BusUIManager.Instance.ShowRedBlackButtons(choice =>
        {
            SubmitRedBlackChoiceServerRpc(choice);
        });
    }

    [ClientRpc]
    void ShowHigherLowerChoiceClientRpc(
    ulong targetClientId,
    ClientRpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId)
            return;

        BusUIManager.Instance.ShowHigherLowerButtons(choice =>
        {
            SubmitHigherLowerChoiceServerRpc(choice);
        });
    }

    [ClientRpc]
    void ShowInsideOutsideChoiceClientRpc(
    ulong targetClientId,
    ClientRpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId)
            return;

        BusUIManager.Instance.ShowInsideOutsideButtons(choice =>
        {
            SubmitInsideOutsideChoiceServerRpc(choice);
        });
    }

    [ClientRpc]
    void ShowSuitChoiceClientRpc(
    ulong targetClientId,
    ClientRpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId)
            return;

        BusUIManager.Instance.ShowSuitButtons(choice =>
        {
            SubmitSuitChoiceServerRpc(choice);
        });
    }

    [ServerRpc(RequireOwnership = false)]
    public void SubmitRedBlackChoiceServerRpc(
    RedBlackChoice choice,
    ServerRpcParams rpcParams = default)
    {
        ulong id = rpcParams.Receive.SenderClientId;

        var c = playerChoices[id];
        c.redBlack = choice;
        playerChoices[id] = c;

        if (AllPlayersHaveChoice(c => c.redBlack != RedBlackChoice.None))
        {
            ResolveCurrentRoundForAll();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void SubmitHigherLowerChoiceServerRpc(
    HigherLowerChoice choice,
    ServerRpcParams rpcParams = default)
    {
        ulong id = rpcParams.Receive.SenderClientId;

        var c = playerChoices[id];
        c.higherLower = choice;
        playerChoices[id] = c;

        if (AllPlayersHaveChoice(c => c.higherLower != HigherLowerChoice.None))
        {
            ResolveCurrentRoundForAll();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void SubmitInsideOutsideChoiceServerRpc(
    InsideOutsideChoice choice,
    ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        var c = playerChoices[senderId];
        c.insideOutside = choice;
        playerChoices[senderId] = c;

        if (AllPlayersHaveChoice(c => c.insideOutside != InsideOutsideChoice.None))
        {
            ResolveCurrentRoundForAll();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void SubmitSuitChoiceServerRpc(
    HasSuitChoice choice,
    ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        var c = playerChoices[senderId];
        c.hasSuit = choice;
        playerChoices[senderId] = c;

        if (AllPlayersHaveChoice(c => c.hasSuit != HasSuitChoice.None))
        {
            ResolveCurrentRoundForAll();
        }
    }
    bool AllPlayersHaveChoice(System.Func<BusPlayerChoices, bool> predicate)
    {
        foreach (var p in players)
        {
            if (!predicate(playerChoices[p.OwnerClientId]))
                return false;
        }
        return true;
    }

    bool ResolveBusRound(Player player)
    {
        if (deck.Count == 0)
        {
            Debug.LogError("Deck empty!");
            return false;
        }

        var hand = player.hand;
        var choices = playerChoices[player.OwnerClientId];

        bool correct = false;

        switch (currentRound)
        {
            case BusRound.RedBlack:
                {
                    PlayingCard card = deck[0];

                    if (!card.IsJoker)
                    {
                        correct =
                            (choices.redBlack == RedBlackChoice.Red && card.IsRed) ||
                            (choices.redBlack == RedBlackChoice.Black && card.IsBlack);
                    }
                    break;
                }

            case BusRound.HigherLower:
                {
                    PlayingCard card = deck[0];

                    if (!card.IsJoker)
                    {
                        int prev = (int)hand[0].Value;
                        int next = (int)card.Value;

                        correct =
                            (choices.higherLower == HigherLowerChoice.Higher && next > prev) ||
                            (choices.higherLower == HigherLowerChoice.Lower && next < prev);
                    }
                    break;
                }

            case BusRound.InsideOutside:
                {
                    PlayingCard card = deck[0];

                    if (!card.IsJoker)
                    {
                        int a = (int)hand[0].Value;
                        int b = (int)hand[1].Value;
                        int min = Mathf.Min(a, b);
                        int max = Mathf.Max(a, b);
                        int v = (int)card.Value;

                        bool isInside = v > min && v < max;

                        correct =
                            (choices.insideOutside == InsideOutsideChoice.Inside && isInside) ||
                            (choices.insideOutside == InsideOutsideChoice.Outside && !isInside);
                    }
                    break;
                }

            case BusRound.SuitGuess:
                {
                    bool handContainsSuit = hand.Any(
                        card => card.Suit == (CardSuit)choices.suit
                    );

                    correct =
                        (choices.hasSuit == HasSuitChoice.Yes && handContainsSuit) ||
                        (choices.hasSuit == HasSuitChoice.No && !handContainsSuit);
                    break;
                }
        }

        PlayingCard newCard = deck[0];
        deck.RemoveAt(0);
        player.AddCard(newCard);

        return correct;
    }

    void ResolveCurrentRoundForAll()
    {
        currentPhase = BusRoundPhase.Revealing;
        pendingPointRewards = 0;

        foreach (var player in players)
        {
            bool correct = ResolveBusRound(player);

            roundCorrect[player.OwnerClientId] = correct;

            ShowRoundResultClientRpc(
                player.OwnerClientId,
                correct
            );

            if (correct)
            {
                pendingPointRewards++;
                playersWhoMustGivePoint.Add(player.OwnerClientId);
            }
            else
            {
                player.AddPoints(1);
                pointsReceivedThisRound[player.OwnerClientId]++;
            }
        }

        if (pendingPointRewards > 0)
            currentPhase = BusRoundPhase.GivingPoints;
        else
            ShowRoundSummary();
    }

    [ClientRpc]
    void ShowRoundResultClientRpc(
    ulong targetClientId,
    bool correct)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId)
            return;

        BusUIManager.Instance.ShowResultPopup(correct);
    }

    public void ShowPlayerSelectForPlayer()
    {
        ShowPlayerSelectClientRpc();
    }

    [ClientRpc]
    void ShowPlayerSelectClientRpc()
    {
        ulong localId = NetworkManager.Singleton.LocalClientId;

        List<Player> selectablePlayers = players
            .Where(p => p.OwnerClientId != localId)
            .ToList();

        BusUIManager.Instance.ShowPlayerSelection(
            selectablePlayers,
            (selectedId) =>
            {
                SubmitPointTargetServerRpc(selectedId);
            });
    }

    [ClientRpc]
    void ShowPointReceivedClientRpc(
    FixedString32Bytes fromPlayerName,
    int totalPoints)
    {
        BusUIManager.Instance?.ShowPointReceivedPopup(
            fromPlayerName.ToString(),
            totalPoints
        );
    }

    [ServerRpc(RequireOwnership = false)]
    void SubmitPointTargetServerRpc(
    ulong targetClientId,
    ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        Player sender = players.Find(p => p.OwnerClientId == senderId);
        Player target = players.Find(p => p.OwnerClientId == targetClientId);

        if (sender.OwnerClientId != rpcParams.Receive.SenderClientId || target == null)
            return;


        target.AddPoints(1);
        pendingPointRewards--;


        ShowPointReceivedClientRpc(
            sender.PlayerName.Value, 
            target.points
        );

        pointsReceivedThisRound[targetClientId]++;
        playersWhoMustGivePoint.Remove(senderId);

        if (playersWhoMustGivePoint.Count == 0)
        {
            ShowRoundSummary();
        }
    }
    void ShowRoundSummary()
    {
        currentPhase = BusRoundPhase.Summary;

        foreach (var p in players)
        {
            ShowRoundSummaryClientRpc(
                p.OwnerClientId,
                pointsReceivedThisRound[p.OwnerClientId]
            );
        }

        Invoke(nameof(AdvanceRound), 2.5f);
    }

    [ClientRpc]
    void ShowRoundSummaryClientRpc(
    ulong targetClientId,
    int pointsThisRound)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId)
            return;

        BusUIManager.Instance.ShowRoundSummary(pointsThisRound);
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

        foreach (var suit in suits)
        {
            for (int value = 1; value <= 13; value++)
            {
                deck.Add(new PlayingCard((PlayingDeckCardValue)value,suit));
            }
        }

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
