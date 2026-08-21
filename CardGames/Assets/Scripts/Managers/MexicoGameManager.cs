using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Runs a game of Mexico (https://en.wikipedia.org/wiki/Mexico_(game)).
/// Betting/pot is intentionally left out - this manager just decides who
/// loses each round (RoundLost event) and keeps looping into the next round.
/// Hook RoundLost up to whatever elimination/points system you want.
///
/// Flow per the rules:
///  1. Every player rolls one die; highest starts as "leader" (ties re-roll).
///  2. Leader rolls up to 3 times, stopping whenever they like. The number
///     of rolls they actually took becomes the max every other player may
///     take that round.
///  3. Players roll in turn order after the leader. Only their FINAL roll
///     counts, not their best.
///  4. If the leader rolls "Mexico" (21) on any of their rolls, the lead
///     passes immediately to the next player, who becomes the new leader
///     with a fresh 3 rolls. This can chain around the table.
///  5. Lowest-ranked roll that round loses. The loser leads the next round.
/// </summary>
public class MexicoGameManager : NetworkBehaviour
{
    public static MexicoGameManager Instance;

    public enum GamePhase
    {
        StartingRollOff,  // All players roll 1 die to determine leader
        GameInProgress    // Normal turn-based game flow
    }

    private const int MexicoRank = 1000; // 2-1 "Mexico", always the highest possible roll
    private const int DoubleRankBase = 900; // doubles rank 901 (double-1) .. 906 (double-6)

    [Header("Config")]
    [Range(1, 3)] public int maxRollsPerTurn = 3;
    [Tooltip("Pause between reveals purely for pacing/UI, in seconds. Set to 0 to disable.")]
    public float revealDelay = 1.0f;

    public GamePhase CurrentPhase { get; private set; } = GamePhase.StartingRollOff;

    private readonly List<Player> turnOrder = new List<Player>();
    private readonly Dictionary<Player, (int d1, int d2, int rank)> roundResults = new Dictionary<Player, (int d1, int d2, int rank)>();
    private readonly Dictionary<Player, int> startingRollResults = new Dictionary<Player, int>(); // Single die rolls for roll-off
    private readonly HashSet<ulong> playersReadyForGameStart = new HashSet<ulong>();

    private int leaderIndex;
    private int activeOffset;      // 0 = leader, 1 = next player, etc. (relative to leaderIndex)
    private int rollsTakenThisTurn;
    private int rollsAllowedThisRound;

    // Expose turnOrder for UI and other systems
    public IReadOnlyList<Player> TurnOrder => turnOrder;
    public Player CurrentActivePlayer => 
        turnOrder.Count == 0 ? null : turnOrder[(leaderIndex + activeOffset) % turnOrder.Count];

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // Server-side: set up the game when scene loads
        if (!IsServer) return;

        Debug.Log("[MexicoGameManager] Server start - initializing game");

        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogError("[MexicoGameManager] GameManager not found!");
            return;
        }

        // Set up turn order and resources
        List<Player> players = gameManager.SetupGame("Mexico");
        if (players != null && players.Count > 0)
        {
            StartMexicoGame(players);
        }
    }

    /// <summary>Server-only entry point after GameManager.SetupGame has completed.</summary>
    public void StartMexicoGame(List<Player> players)
    {
        if (!IsServer) 
        {
            Debug.LogWarning("MexicoGameManager.StartMexicoGame called on non-server!");
            return;
        }
        if (players == null || players.Count < 2)
        {
            Debug.LogError("MexicoGameManager: need at least 2 players to start.");
            return;
        }

        Debug.Log($"[MexicoGameManager] Starting game with {players.Count} players");

        turnOrder.Clear();
        turnOrder.AddRange(players);
        startingRollResults.Clear();

        CurrentPhase = GamePhase.StartingRollOff;

        // Notify all players that starting roll-off phase is active
        NotifyStartingRollPhaseClientRpc();
    }

    /// <summary>Tell all clients to enable dice input for starting roll</summary>
    [ClientRpc]
    private void NotifyStartingRollPhaseClientRpc()
    {
        Debug.Log("[MexicoGameManager ClientRpc] Starting roll-off phase received by client");
        if (MexicoUIManager.Instance != null)
        {
            MexicoUIManager.Instance.SetGamePhase(GamePhase.StartingRollOff);
            Debug.Log("[MexicoGameManager] SetGamePhase called on UI");
        }
        else
        {
            Debug.LogError("[MexicoGameManager] MexicoUIManager.Instance is NULL!");
        }
    }

    /// <summary>Called by any player client during starting roll-off phase</summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestStartingRollServerRpc(ServerRpcParams rpcParams = default)
    {
        if (CurrentPhase != GamePhase.StartingRollOff) return;

        Player requester = GetPlayerBySender(rpcParams.Receive.SenderClientId);
        if (requester == null || startingRollResults.ContainsKey(requester))
            return; // Already rolled

        int d1 = UnityEngine.Random.Range(1, 7);
        startingRollResults[requester] = d1;

        // Broadcast this roll to everyone
        BroadcastStartingRollClientRpc(requester.PlayerName.Value, d1);

        // Check if all players have rolled
        if (startingRollResults.Count >= turnOrder.Count)
        {
            StartCoroutine(DetermineLeaderFromStartingRolls());
        }
    }

    [ClientRpc]
    private void BroadcastStartingRollClientRpc(Unity.Collections.FixedString32Bytes playerName, int dieValue)
    {
        UIManager.Instance?.ShowNotification($"{playerName} rolled: {dieValue}", 1.5f);
        // Let the UI show the final face for the local player
        if (MexicoUIManager.Instance != null)
            MexicoUIManager.Instance.OnStartingRoll(playerName, dieValue);
    }

    private IEnumerator DetermineLeaderFromStartingRolls()
    {
        if (revealDelay > 0f)
            yield return new WaitForSeconds(revealDelay);

        int highest = 0;
        foreach (var kv in startingRollResults)
            if (kv.Value > highest) highest = kv.Value;

        List<Player> contenders = new List<Player>();
        foreach (var kv in startingRollResults)
        {
            if (kv.Value == highest)
                contenders.Add(kv.Key);
        }

        if (contenders.Count == 1)
        {
            playersReadyForGameStart.Clear();
            var names = new Unity.Collections.FixedString32Bytes[startingRollResults.Count];
            var values = new int[startingRollResults.Count];
            int idx = 0;
            foreach (var kv in startingRollResults)
            {
                names[idx] = kv.Key.PlayerName.Value;
                values[idx] = kv.Value;
                idx++;
            }

            NotifyStartingRollSummaryClientRpc(names, values);

            // Clear winner
            leaderIndex = turnOrder.IndexOf(contenders[0]);
        }
        else
        {
            // Tie: re-roll with tied players
            startingRollResults.Clear();
            var tiedNames = new Unity.Collections.FixedString32Bytes[contenders.Count];
            for (int i = 0; i < contenders.Count; i++)
                tiedNames[i] = contenders[i].PlayerName.Value;

            NotifyRollOffTieClientRpc(tiedNames);
            // Players will call RequestStartingRollServerRpc again
        }
    }

    [ClientRpc]
    private void NotifyRollOffTieClientRpc(Unity.Collections.FixedString32Bytes[] tiedPlayerNames)
    {
        string names = string.Join(", ", System.Array.ConvertAll(tiedPlayerNames, n => n.ToString()));
        UIManager.Instance?.ShowNotification($"Tie! {names} re-roll", 2f);
    }

    [ClientRpc]
    private void NotifyStartingRollSummaryClientRpc(Unity.Collections.FixedString32Bytes[] playerNames, int[] dieValues)
    {
        if (MexicoUIManager.Instance != null)
            MexicoUIManager.Instance.ShowStartingRollSummary(playerNames, dieValues);
    }

    [ClientRpc]
    private void UpdateReadyStateClientRpc(Unity.Collections.FixedString32Bytes playerName, bool isReady)
    {
        if (MexicoUIManager.Instance != null)
            MexicoUIManager.Instance.UpdateStartingRollReadyState(playerName, isReady);
    }

    [ServerRpc(RequireOwnership = false)]
    public void MarkPlayerReadyServerRpc(ServerRpcParams rpcParams = default)
    {
        if (CurrentPhase != GamePhase.StartingRollOff)
            return;

        Player requester = GetPlayerBySender(rpcParams.Receive.SenderClientId);
        if (requester == null)
            return;

        bool wasReady = !playersReadyForGameStart.Add(requester.OwnerClientId);
        UpdateReadyStateClientRpc(requester.PlayerName.Value, !wasReady);

        if (!wasReady && playersReadyForGameStart.Count >= turnOrder.Count)
        {
            StartCoroutine(BeginGameAfterReadyDelay());
        }
    }

    private IEnumerator BeginGameAfterReadyDelay()
    {
        yield return new WaitForSeconds(3f);

        if (CurrentPhase == GamePhase.StartingRollOff && playersReadyForGameStart.Count >= turnOrder.Count)
            TransitionToGamePhase();
    }

    private void TransitionToGamePhase()
    {
        CurrentPhase = GamePhase.GameInProgress;
        NotifyGameStartClientRpc(leaderIndex);
        BeginRound();
    }

    [ClientRpc]
    private void NotifyGameStartClientRpc(int leaderIdx)
    {
        if (MexicoUIManager.Instance != null)
        {
            MexicoUIManager.Instance.SetGamePhase(GamePhase.GameInProgress);
        }
        Debug.Log($"Game started! Leader index: {leaderIdx}");
    }

    // ---------- Round flow ----------

    private void BeginRound()
    {
        roundResults.Clear();
        activeOffset = 0;
        rollsTakenThisTurn = 0;
        rollsAllowedThisRound = maxRollsPerTurn;

        // Notify UI to clear round results and update leader
        if (MexicoUIManager.Instance != null)
        {
            MexicoUIManager.Instance.ClearRoundResultsPanel();
            Player leader = CurrentActivePlayer;
            if (leader != null)
                MexicoUIManager.Instance.UpdateLeaderIndicator(leader);
        }

        NotifyActivePlayer();
    }

    private void NotifyActivePlayer()
    {
        Player activePlayer = CurrentActivePlayer;
        foreach (var p in turnOrder)
            p.SetTurnClientRpc(p == activePlayer);

        int allowedRolls = activeOffset == 0 ? maxRollsPerTurn : rollsAllowedThisRound;
        bool isLeader = activeOffset == 0;
        NotifyActivePlayerClientRpc(activePlayer != null ? activePlayer.OwnerClientId : 0, allowedRolls, isLeader);

        // Update UI to show current player on server instance too
        if (MexicoUIManager.Instance != null)
            MexicoUIManager.Instance.UpdateCurrentPlayerIndicator(activePlayer);
        if (MexicoUIManager.Instance != null)
            MexicoUIManager.Instance.SetActiveTurn(activePlayer, isLeader, allowedRolls);
    }

    [ClientRpc]
    private void NotifyActivePlayerClientRpc(ulong activePlayerClientId, int allowedRolls, bool isLeader)
    {
        Player activePlayer = null;
        foreach (var p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (p.OwnerClientId == activePlayerClientId)
            {
                activePlayer = p;
                break;
            }
        }

        if (MexicoUIManager.Instance != null)
            MexicoUIManager.Instance.SetActiveTurn(activePlayer, isLeader, allowedRolls);
    }

    /// <summary>Called by the active player's client when they want to roll (2 dice).</summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestRollServerRpc(ServerRpcParams rpcParams = default)
    {
        if (CurrentPhase != GamePhase.GameInProgress) return;

        Player requester = GetPlayerBySender(rpcParams.Receive.SenderClientId);
        if (requester == null || requester != CurrentActivePlayer) return;

        bool isLeader = activeOffset == 0;
        int maxAllowed = isLeader ? maxRollsPerTurn : rollsAllowedThisRound;

        if (rollsTakenThisTurn >= maxAllowed) return; // no rolls left this turn

        // Roll 2 dice
        int d1 = UnityEngine.Random.Range(1, 7);
        int d2 = UnityEngine.Random.Range(1, 7);
        rollsTakenThisTurn++;

        int rank = ComputeRank(d1, d2);
        roundResults[requester] = (d1, d2, rank);

        bool isMexico = rank == MexicoRank;
        bool rollsExhausted = rollsTakenThisTurn >= maxAllowed;

        ShowRollResultClientRpc(requester.PlayerName.Value, d1, d2, isMexico, rollsExhausted || isLeader && isMexico);

        if (isLeader && isMexico)
        {
            // Leader rolled Mexico: lead passes on immediately, no more choice.
            AdvanceAfterLeaderMexico();
            return;
        }

        if (rollsExhausted)
        {
            AdvanceToNextPlayer();
        }
        // Otherwise the player still has rolls left and may call
        // RequestRollServerRpc again or StopRollingServerRpc to lock it in.
    }

    /// <summary>Called by the active player's client when they choose to stop rolling early.</summary>
    [ServerRpc(RequireOwnership = false)]
    public void StopRollingServerRpc(ServerRpcParams rpcParams = default)
    {
        Player requester = GetPlayerBySender(rpcParams.Receive.SenderClientId);
        if (requester == null || requester != CurrentActivePlayer) return;
        if (!roundResults.ContainsKey(requester)) return; // must roll at least once first

        if (activeOffset == 0)
        {
            // Leader chose to stop: the number of rolls they took becomes the
            // cap for everyone else this round.
            rollsAllowedThisRound = rollsTakenThisTurn;
            NotifyRollLimitClientRpc(rollsAllowedThisRound);
        }

        AdvanceToNextPlayer();
    }

    [ClientRpc]
    private void NotifyRollLimitClientRpc(int rollsAllowed)
    {
        if (MexicoUIManager.Instance != null)
            MexicoUIManager.Instance.UpdateRollsAllowed(rollsAllowed, false);
    }

    private void AdvanceAfterLeaderMexico()
    {
        // New leader is the next player in turn order, with a fresh set of rolls.
        leaderIndex = (leaderIndex + 1) % turnOrder.Count;
        activeOffset = 0;
        rollsTakenThisTurn = 0;
        rollsAllowedThisRound = maxRollsPerTurn;

        if (roundResults.Count >= turnOrder.Count)
        {
            // Everyone has now rolled (chain of Mexicos went all the way
            // around) - finalize with whatever results we have.
            FinalizeRound();
            return;
        }

        NotifyActivePlayer();
    }

    private void AdvanceToNextPlayer()
    {
        activeOffset++;
        rollsTakenThisTurn = 0;

        if (activeOffset >= turnOrder.Count)
        {
            FinalizeRound();
        }
        else
        {
            NotifyActivePlayer();
        }
    }

    private void FinalizeRound()
    {
        Player loser = null;
        int lowestRank = int.MaxValue;

        foreach (var kv in roundResults)
        {
            if (kv.Value.rank < lowestRank)
            {
                lowestRank = kv.Value.rank;
                loser = kv.Key;
            }
        }

        if (loser == null)
        {
            Debug.LogError("MexicoGameManager: round finished with no results, cannot determine a loser.");
            return;
        }

        var (d1, d2, _) = roundResults[loser];
        AnnounceRoundResultClientRpc(loser.PlayerName.Value, d1, d2);

        RoundLost?.Invoke(loser);
        NotifyRoundLostClientRpc(loser.PlayerName.Value);

        // Loser leads the next round, regardless of who rolled last.
        leaderIndex = turnOrder.IndexOf(loser);
        BeginRound();
    }

    /// <summary>Fired on the server whenever a round ends, with the losing player.</summary>
    public event Action<Player> RoundLost;

    // ---------- Scoring ----------

    /// <summary>
    /// Mexico scoring rank: higher = better.
    /// 1000        -> "Mexico" (2 and 1), the single highest roll.
    /// 901..906    -> doubles, ranked by pip value (double-6 highest, double-1 lowest of the doubles).
    /// 31..65      -> everything else: tens digit = higher die, ones digit = lower die.
    /// </summary>
    public static int ComputeRank(int dieA, int dieB)
    {
        if ((dieA == 2 && dieB == 1) || (dieA == 1 && dieB == 2))
            return MexicoRank;

        if (dieA == dieB)
            return DoubleRankBase + dieA;

        int hi = Mathf.Max(dieA, dieB);
        int lo = Mathf.Min(dieA, dieB);
        return hi * 10 + lo;
    }

    private Player GetPlayerBySender(ulong senderClientId)
    {
        foreach (var p in turnOrder)
            if (p.OwnerClientId == senderClientId) return p;
        return null;
    }

    // ---------- Client-facing notifications (wire these into your UI) ----------

    [ClientRpc]
    private void ShowRollResultClientRpc(Unity.Collections.FixedString32Bytes playerName, int d1, int d2, bool isMexico, bool wasFinalRollOfTurn)
    {
        // Notify UI manager of the roll result
        if (MexicoUIManager.Instance != null)
        {
            MexicoUIManager.Instance.OnPlayerRolled(playerName, d1, d2, isMexico, wasFinalRollOfTurn);
        }
    }

    [ClientRpc]
    private void AnnounceRoundResultClientRpc(Unity.Collections.FixedString32Bytes loserName, int d1, int d2)
    {
        // Announce round result to UI manager
        // MexicoUIManager will show loser popup via OnRoundLost event hook
        Debug.Log($"{loserName} lost with roll {d1}-{d2}");
    }

    [ClientRpc]
    private void NotifyRoundLostClientRpc(Unity.Collections.FixedString32Bytes loserName)
    {
        if (MexicoUIManager.Instance != null)
            MexicoUIManager.Instance.ShowLoserAnnouncementForName(loserName);
    }
}
