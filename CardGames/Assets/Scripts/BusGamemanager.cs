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
public class BusRow
{
    public List<PlayingCard> cards = new();
    public int[] pointValues;
}

public class BusGamemanager : NetworkBehaviour
{
    public static BusGamemanager Instance;

    public List<Player> players = new();
    private int currentPlayerIndex = 0;

    private BusRoundPhase currentPhase;
    private BusRound currentRound = BusRound.RedBlack;
    private BusPlayerChoices currentChoice;

    private Dictionary<ulong, int> pointsReceivedThisRound = new();

    int[] busPointValues =
    {
    1,1,1,2,
    2,2,4,
    4,8,
    16
    };
    private List<BusRow> busRows = new();

    private int currentRow = 0;
    private int currentCard = 0;

    private PlayingCard currentBusCard;
    private int currentBusCardPoints;

    private bool busActive = false;

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

        currentPlayerIndex = 0;
        currentRound = BusRound.RedBlack;

        foreach (Player player in players)
        {
            pointsReceivedThisRound[player.OwnerClientId] = 0;
        }

        StartTurn();
    }

    void StartTurn()
    {
        currentChoice = new BusPlayerChoices();

        Player currentPlayer = players[currentPlayerIndex];

        ShowCurrentTurnClientRpc(currentPlayer.PlayerName.Value);

        switch (currentRound)
        {
            case BusRound.RedBlack:
                ShowRedBlackChoiceClientRpc(currentPlayer.OwnerClientId);
                break;

            case BusRound.HigherLower:
                ShowHigherLowerChoiceClientRpc(currentPlayer.OwnerClientId);
                break;

            case BusRound.InsideOutside:
                ShowInsideOutsideChoiceClientRpc(currentPlayer.OwnerClientId);
                break;

            case BusRound.SuitGuess:
                ShowSuitChoiceClientRpc(currentPlayer.OwnerClientId);
                break;
        }
    }

    void NextTurn()
    {
        currentPlayerIndex++;

        if (currentPlayerIndex >= players.Count)
        {
            currentPlayerIndex = 0;
            AdvanceRound();
            return;
        }

        StartTurn();
    }
    void AdvanceRound()
    {
        currentChoice = new BusPlayerChoices();

        if (currentRound < BusRound.SuitGuess)
        {
            currentRound++;
            StartTurn();
            return;
        }

        StartBus();
    }

    private void ResolveCurrentPlayerTurn(ulong playerId)
    {
        Player player = players[currentPlayerIndex];

        // Extra safety check
        if (player.OwnerClientId != playerId)
            return;

        bool correct = ResolveBusRound(player);

        Debug.Log($"Correct = {correct}");

        //RevealCardClientRpc(targetPlayerId, newCard);
        ShowRoundResultClientRpc(player.OwnerClientId, correct);

        if (correct)
        {
            Debug.Log($"Showing player select for {player.OwnerClientId}");
            ShowPlayerSelectClientRpc(player.OwnerClientId);
        }
        else
        {
            // Player punishes themselves.
            player.AddPoints(1);
            pointsReceivedThisRound[player.OwnerClientId]++;

            NextTurn();
        }
    }

    void StartBus()
    {
        busActive = true;

        busRows.Clear();

        int[][] values =
        {
        new []{1,1,1,2},
        new []{2,2,4},
        new []{4,8},
        new []{16}
    };

        foreach (var rowValues in values)
        {
            BusRow row = new BusRow();

            row.pointValues = rowValues;

            for (int i = 0; i < rowValues.Length; i++)
            {
                row.cards.Add(deck[0]);
                deck.RemoveAt(0);
            }

            busRows.Add(row);
        }
        CreateBusClientRpc();

        currentRow = 0;
        currentCard = 0;

        RevealNextBusCard();
    }

    void RevealNextBusCard()
    {
        BusRow row = busRows[currentRow];

        currentBusCard = row.cards[currentCard];
        currentBusCardPoints = row.pointValues[currentCard];

        ShowBusCardClientRpc(currentBusCard,
                             currentRow,
                             currentCard);

        currentPlayerIndex = 0;

        StartBusPlayerTurn();
    }

    void StartBusPlayerTurn()
    {
        Player player = players[currentPlayerIndex];

        ShowCurrentTurnClientRpc(player.PlayerName.Value);

        ShowBusPlayChoiceClientRpc(player.OwnerClientId);
    }

    void SkipBusTurn()
    {
        NextBusPlayer();
    }

    void NextBusPlayer()
    {
        currentPlayerIndex++;

        if (currentPlayerIndex >= players.Count)
        {
            currentCard++;

            BusRow row = busRows[currentRow];

            if (currentCard >= row.cards.Count)
            {
                currentRow++;
                currentCard = 0;

                if (currentRow >= busRows.Count)
                {
                    //EndBus();
                    return;
                }
            }

            RevealNextBusCard();
            return;
        }

        StartBusPlayerTurn();
    }

    bool CanPlay(PlayingCard handCard, PlayingCard busCard)
    {
        return
            handCard.Value == busCard.Value &&
            handCard.Suit != busCard.Suit;
    }

    void AddPointReceived(ulong id)
    {
        if (!pointsReceivedThisRound.ContainsKey(id))
            pointsReceivedThisRound[id] = 0;

        pointsReceivedThisRound[id]++;
    }

    [ServerRpc(RequireOwnership = false)]
    public void SubmitRedBlackChoiceServerRpc(
    RedBlackChoice choice,
    ServerRpcParams rpcParams = default)
    {
        ulong id = rpcParams.Receive.SenderClientId;

        currentChoice.redBlack = choice;
        ResolveCurrentPlayerTurn(id);
    }
    [ServerRpc(RequireOwnership = false)]
    void SubmitHigherLowerChoiceServerRpc(
    HigherLowerChoice choice,
    ServerRpcParams rpcParams = default)
    {
        ulong id = rpcParams.Receive.SenderClientId;

        currentChoice.higherLower = choice;
        ResolveCurrentPlayerTurn(id);
    }
    [ServerRpc(RequireOwnership = false)]
    void SubmitInsideOutsideChoiceServerRpc(
    InsideOutsideChoice choice,
    ServerRpcParams rpcParams = default)
    {
        ulong id = rpcParams.Receive.SenderClientId;

        currentChoice.insideOutside = choice;
        ResolveCurrentPlayerTurn(id);
    }
    [ServerRpc(RequireOwnership = false)]
    void SubmitSuitChoiceServerRpc(
    HasSuitChoice choice,
    ServerRpcParams rpcParams = default)
    {
        ulong id = rpcParams.Receive.SenderClientId;

        currentChoice.hasSuit = choice;
        ResolveCurrentPlayerTurn(id);
    }

    [ClientRpc]
    void ShowBusCardClientRpc(PlayingCard card, int row, int index)
    {
        BusUIManager.Instance.RevealBusCard(row, index);
    }

    [ClientRpc]
    void ShowBusPlayChoiceClientRpc(ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId)
            return;

        Player me = FindObjectsOfType<Player>()
            .First(p => p.IsOwner);

        BusUIManager.Instance.ShowBusPlayChoice(
            me.hand,
            currentBusCard);
    }

    [ClientRpc]
    void CreateBusClientRpc()
    {
        BusUIManager.Instance.CreateBus(busRows);
    }

    [ClientRpc]
    void ShowCurrentTurnClientRpc(FixedString32Bytes playerName)
    {
        BusUIManager.Instance.ShowCurrentTurn(playerName.ToString());
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

    bool ResolveBusRound(Player player)
    {
        if (deck.Count == 0)
        {
            Debug.LogError("Deck empty!");
            return false;
        }

        var hand = player.hand;

        bool correct = false;

        PlayingCard drawnCard = deck[0];
        deck.RemoveAt(0);

        switch (currentRound)
        {
            case BusRound.RedBlack:
                {
                    if (!drawnCard.IsJoker)
                    {
                        correct =
                            (currentChoice.redBlack == RedBlackChoice.Red && drawnCard.IsRed) ||
                            (currentChoice.redBlack == RedBlackChoice.Black && drawnCard.IsBlack);
                    }
                    break;
                }

            case BusRound.HigherLower:
                {
                    if (!drawnCard.IsJoker)
                    {
                        int prev = (int)hand[0].Value;
                        int next = (int)drawnCard.Value;

                        correct =
                            (currentChoice.higherLower == HigherLowerChoice.Higher && next > prev) ||
                            (currentChoice.higherLower == HigherLowerChoice.Lower && next < prev);
                    }
                    break;
                }

            case BusRound.InsideOutside:
                {
                    if (!drawnCard.IsJoker)
                    {
                        int a = (int)hand[0].Value;
                        int b = (int)hand[1].Value;
                        int min = Mathf.Min(a, b);
                        int max = Mathf.Max(a, b);
                        int v = (int)drawnCard.Value;

                        bool isInside = v > min && v < max;

                        correct =
                            (currentChoice.insideOutside == InsideOutsideChoice.Inside && isInside) ||
                            (currentChoice.insideOutside == InsideOutsideChoice.Outside && !isInside);
                    }
                    break;
                }

            case BusRound.SuitGuess:
                {
                    bool handContainsSuit = hand.Any(
                        card => card.Suit == (CardSuit)currentChoice.suit
                    );

                    correct =
                        (currentChoice.hasSuit == HasSuitChoice.Yes && handContainsSuit) ||
                        (currentChoice.hasSuit == HasSuitChoice.No && !handContainsSuit);
                    break;
                }
        }

        player.AddCard(drawnCard);

        return correct;
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
        ShowPlayerSelectClientRpc(players[currentPlayerIndex].OwnerClientId);
    }

    [ClientRpc]
    void ShowPlayerSelectClientRpc(ulong targetClientId)
    {
        Debug.Log($"ClientRPC received on {NetworkManager.Singleton.LocalClientId}, target = {targetClientId}");
        if (NetworkManager.Singleton.LocalClientId != targetClientId)
            return;

        ulong localId = NetworkManager.Singleton.LocalClientId;

        Player[] allPlayers = FindObjectsOfType<Player>();

        List<Player> selectablePlayers = allPlayers
            .Where(p => p.OwnerClientId != localId)
            .ToList();

        BusUIManager.Instance.ShowPlayerSelection(
            selectablePlayers,
            selectedId =>
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

        ShowPointReceivedClientRpc(
            sender.PlayerName.Value,
            target.points
        );

        pointsReceivedThisRound[targetClientId]++;

        NextTurn();
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
