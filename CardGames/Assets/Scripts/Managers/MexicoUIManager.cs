using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mexico game-specific UI manager. Handles RoundOrder display, current player indicator,
/// persistent round results panel, dice throw input, and popups for roll results/loser.
/// 
/// Follows same singleton pattern as BusUIManager.
/// </summary>
public class MexicoUIManager : MonoBehaviour
{
    public static MexicoUIManager Instance;

    [Header("Current Player UI")]
    public Text currentPlayerText;
    public Image currentPlayerAvatarImage;
    [Tooltip("Shows 'Dealer' or 'Leader' label")]
    public Text leaderIndicatorText;

    [Header("Round Results Panel")]
    public GameObject roundResultsPanel;
    public Transform roundResultsContent; // Parent for roll entries
    public GameObject rollEntryPrefab;    // Prefab: avatar, name, dice, rank, label
    [Tooltip("Max height before scroll. 0 = no limit")]
    public float maxPanelHeight = 400f;

    [Header("Loser Display")]
    public GameObject loserDisplayPanel;
    public Image loserAvatarImage;
    public Text loserNameText;
    [Tooltip("Text that appears, e.g., 'You Lost!' or 'PlayerName Lost!'")]
    public Text loserStatusText;

    [Header("Dice Input")]
    public DiceThrowInput diceThrowInput;
    public CanvasGroup diceInputCanvasGroup; // For fade animations
    [Tooltip("Single die visual for starting roll-off")]
    public GameObject oneDiceVisual;
    [Tooltip("Two dice visual for normal game play")]
    public GameObject twoDiceVisual;

    [Header("Popups")]
    [Tooltip("Duration each roll notification appears")]
    public float rollNotificationDuration = 2f;
    [Tooltip("Duration loser announcement appears")]
    public float loserAnnouncementDuration = 3f;
    [Header("Controls")]
    public Button continueRollButton;

    private MexicoGameManager mexicoManager;
    private Player localPlayer;
    private bool isSpectating = false;
    private MexicoGameManager.GamePhase currentPhase = MexicoGameManager.GamePhase.StartingRollOff;
    private int localRollsTakenThisTurn = 0;
    private int maxRollsPerTurnLocal = 3;
    private readonly List<GameObject> currentRoundRolls = new List<GameObject>();
    private readonly List<Player> knownPlayers = new List<Player>();

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        Debug.Log("[MexicoUIManager] Start() called");
        
        mexicoManager = MexicoGameManager.Instance;
        if (mexicoManager == null)
        {
            Debug.LogError("MexicoUIManager: MexicoGameManager not found!");
            return;
        }

        Debug.Log($"[MexicoUIManager] MexicoGameManager found, Phase: {mexicoManager.CurrentPhase}");

        // Find local player
        Player[] allPlayers = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (var p in allPlayers)
        {
            if (p.IsOwner)
            {
                localPlayer = p;
                break;
            }
        }

        // Check if local player is in the game (spectator check)
        // NOTE: TurnOrder is populated by MexicoGameManager.StartMexicoGame(), which
        // happens AFTER this Start(). For Phase 5 testing, start interactable.
        // TODO: Wire this logic to fire after game actually starts.
        if (false && localPlayer != null && mexicoManager.TurnOrder != null)
        {
            isSpectating = !mexicoManager.TurnOrder.Any(p => p == localPlayer);
        }
        else
        {
            isSpectating = false; // Start as non-spectating; will be updated when game begins
        }

        // Enable dice input for starting roll-off phase (all players can throw)
        if (diceThrowInput != null)
            diceThrowInput.SetInteractable(!isSpectating);

        // Hook to MexicoGameManager events
        mexicoManager.RoundLost += OnRoundLost;

        // Initialize UI
        UpdateCurrentPlayerIndicator(); // null = "Waiting..."
        ClearRoundResultsPanel();
        
        // Wire dice throw event
        WireDiceThrowEvent();

        // Continue/stop rolling button
        if (continueRollButton != null)
        {
            continueRollButton.onClick.AddListener(OnContinuePressed);
            continueRollButton.gameObject.SetActive(false);
        }

        // Read max rolls from manager if available
        if (mexicoManager != null) maxRollsPerTurnLocal = mexicoManager.maxRollsPerTurn;
    }

    void OnDestroy()
    {
        if (mexicoManager != null)
            mexicoManager.RoundLost -= OnRoundLost;
    }

    /// <summary>
    /// Called by MexicoGameManager.ShowRollResultClientRpc equivalent.
    /// Adds a roll entry to the persistent results panel.
    /// Should be called from game manager via ClientRpc or direct method call.
    /// </summary>
    public void OnPlayerRolled(FixedString32Bytes playerName, int d1, int d2, bool isMexico, bool wasFinalRoll)
    {
        // Add entry to round results panel
        if (rollEntryPrefab != null && roundResultsContent != null)
        {
            GameObject rollEntry = Instantiate(rollEntryPrefab, roundResultsContent);
            currentRoundRolls.Add(rollEntry);

            // Populate roll entry UI
            SetupRollEntry(rollEntry, playerName, d1, d2, isMexico);
        }

        // Show notification popup
        string rollText = isMexico
            ? $"{playerName}: Mexico! (2-1)"
            : $"{playerName}: {d1}-{d2}";

        UIManager.Instance?.ShowNotification(rollText, rollNotificationDuration);

        // Auto-scroll to bottom if content height exceeds panel height
        if (maxPanelHeight > 0f)
        {
            StartCoroutine(ScrollToBotom());
        }

        // If this client is the player who rolled, show the final dice faces
        if (localPlayer != null && playerName.ToString() == localPlayer.PlayerName.Value)
        {
            // Track local rolls taken this turn (client-side)
            localRollsTakenThisTurn++;

            if (currentPhase == MexicoGameManager.GamePhase.StartingRollOff)
            {
                // Shouldn't normally reach here for starting roll (different RPC),
                // but handle defensively: show single die
                if (diceThrowInput != null)
                    diceThrowInput.SetDiceFace(d1);
            }
            else
            {
                if (diceThrowInput != null)
                    diceThrowInput.SetDiceFaces(d1, d2);
            }

            // Show or hide the continue button: only if in game phase and local player has rolled at least once
            if (currentPhase == MexicoGameManager.GamePhase.GameInProgress && continueRollButton != null)
            {
                bool canContinue = localRollsTakenThisTurn > 0 && localRollsTakenThisTurn < maxRollsPerTurnLocal;
                continueRollButton.gameObject.SetActive(canContinue);
            }
        }
    }

    /// <summary>
    /// Called when a starting roll (single die) is broadcast from server.
    /// Shows the final face on the local player's dice visual.
    /// </summary>
    public void OnStartingRoll(Unity.Collections.FixedString32Bytes playerName, int dieValue)
    {
        if (localPlayer == null) return;
        if (playerName.ToString() != localPlayer.PlayerName.Value) return;

        if (diceThrowInput != null)
            diceThrowInput.SetDiceFace(dieValue);
    }

    private void SetupRollEntry(GameObject rollEntry, FixedString32Bytes playerName, int d1, int d2, bool isMexico)
    {
        // Expected hierarchy: rollEntry has child components
        // Adjust these names to match your actual prefab structure
        
        Text nameText = rollEntry.transform.Find("PlayerName")?.GetComponent<Text>();
        if (nameText != null) nameText.text = playerName.ToString();

        Text diceText = rollEntry.transform.Find("DiceDisplay")?.GetComponent<Text>();
        if (diceText != null) diceText.text = $"{d1}-{d2}";

        int rank = MexicoGameManager.ComputeRank(d1, d2);
        Text rankText = rollEntry.transform.Find("Rank")?.GetComponent<Text>();
        if (rankText != null) rankText.text = GetRankLabel(rank);

        // Optionally set avatar
        Image avatarImage = rollEntry.transform.Find("AvatarImage")?.GetComponent<Image>();
        if (avatarImage != null)
        {
            // Find player by name and get avatar
            Player player = FindPlayerByName(playerName);
            if (player != null)
            {
                Sprite avatarSprite = AvatarDatabase.Instance?.GetAvatar(player.AvatarId.Value);
                if (avatarSprite != null)
                    avatarImage.sprite = avatarSprite;
            }
        }
    }

    private string GetRankLabel(int rank)
    {
        if (rank == 1000) return "Mexico!";
        if (rank >= 901 && rank <= 906) return $"Double-{rank - 900}";
        return $"{rank / 10}-{rank % 10}";
    }

    private Player FindPlayerByName(FixedString32Bytes name)
    {
        if (mexicoManager.TurnOrder == null) return null;

        foreach (var p in mexicoManager.TurnOrder)
        {
            if (p.PlayerName.Value == name)
                return p;
        }

        return null;
    }

    /// <summary>
    /// Call this from any script when you want to notify the UI about a roll result.
    /// Typically called from a ClientRpc wrapper or coroutine in MexicoGameManager.
    /// </summary>
    public void ClearRoundResultsPanel()
    {
        foreach (var roll in currentRoundRolls)
            Destroy(roll);
        currentRoundRolls.Clear();
    }

    private IEnumerator ScrollToBotom()
    {
        yield return null; // Wait one frame for layout rebuild
        
        ScrollRect scrollRect = roundResultsPanel?.GetComponent<ScrollRect>();
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 0f; // Scroll to bottom
    }

    /// <summary>
    /// Update UI to show whose turn it is. Called when Player.SetTurnClientRpc fires.
    /// </summary>
    public void UpdateCurrentPlayerIndicator(Player currentPlayer = null)
    {
        if (currentPlayer == null)
        {
            if (currentPlayerText != null) currentPlayerText.text = "Waiting...";
            if (currentPlayerAvatarImage != null) currentPlayerAvatarImage.sprite = null;
            // Not anyone's turn locally - hide continue button
            if (continueRollButton != null)
                continueRollButton.gameObject.SetActive(false);
            return;
        }

        bool isMyTurn = currentPlayer == localPlayer;
        string turnText = isMyTurn ? "YOUR TURN" : $"{currentPlayer.PlayerName.Value}'s Turn";
        
        if (currentPlayerText != null)
            currentPlayerText.text = turnText;

        if (currentPlayerAvatarImage != null)
        {
            Sprite avatarSprite = AvatarDatabase.Instance?.GetAvatar(currentPlayer.AvatarId.Value);
            if (avatarSprite != null)
                currentPlayerAvatarImage.sprite = avatarSprite;
        }

        // Reset local roll counter at start of new turn for local player
        if (isMyTurn)
        {
            localRollsTakenThisTurn = 0;
            // enable dice input for local player
            if (diceThrowInput != null)
                diceThrowInput.SetInteractable(true);
            if (continueRollButton != null)
                continueRollButton.gameObject.SetActive(false);
        }
        else
        {
            // not local player's turn
            if (diceThrowInput != null)
                diceThrowInput.SetInteractable(false);
            if (continueRollButton != null)
                continueRollButton.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Called when a new leader is established (e.g., loser of previous round).
    /// Shows visual indication of who the new leader is.
    /// </summary>
    public void UpdateLeaderIndicator(Player leader)
    {
        if (leaderIndicatorText != null && leader != null)
        {
            leaderIndicatorText.text = $"Dealer: {leader.PlayerName.Value}";
        }
    }

    /// <summary>
    /// Called when MexicoGameManager.RoundLost event fires.
    /// Shows loser announcement popup.
    /// </summary>
    private void OnRoundLost(Player loser)
    {
        if (loser == null) return;

        bool isLocalPlayerLoser = loser == localPlayer;
        string loserText = isLocalPlayerLoser ? "You Lost!" : $"{loser.PlayerName.Value} Lost!";

        // Update loser display
        if (loserNameText != null)
            loserNameText.text = loser.PlayerName.Value.ToString();
        if (loserStatusText != null)
            loserStatusText.text = loserText;
        if (loserAvatarImage != null)
        {
            Sprite avatarSprite = AvatarDatabase.Instance?.GetAvatar(loser.AvatarId.Value);
            if (avatarSprite != null)
                loserAvatarImage.sprite = avatarSprite;
        }

        // Show loser announcement with fade
        StartCoroutine(ShowLoserAnnouncement());
    }

    private IEnumerator ShowLoserAnnouncement()
    {
        if (loserDisplayPanel == null) yield break;

        CanvasGroup canvasGroup = loserDisplayPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = loserDisplayPanel.AddComponent<CanvasGroup>();

        loserDisplayPanel.SetActive(true);
        canvasGroup.alpha = 0f;

        // Fade in (if UIManager is available)
        if (UIManager.Instance != null)
        {
            yield return StartCoroutine(UIManager.Instance.FadeCanvasGroup(canvasGroup, 1f, 0.5f));
            yield return new WaitForSeconds(loserAnnouncementDuration - 1f);
            // Fade out
            yield return StartCoroutine(UIManager.Instance.FadeCanvasGroup(canvasGroup, 0f, 0.5f));
        }
        else
        {
            // Fallback: no UIManager, just show for duration then hide
            Debug.LogWarning("[MexicoUIManager] UIManager not found, showing loser panel without fade");
            canvasGroup.alpha = 1f;
            yield return new WaitForSeconds(loserAnnouncementDuration);
        }

        loserDisplayPanel.SetActive(false);
    }

    /// <summary>
    /// Disable dice input (e.g., during animations or when it's not player's turn).
    /// </summary>
    public void SetDiceInputInteractable(bool interactable)
    {
        if (diceThrowInput != null)
            diceThrowInput.SetInteractable(interactable && !isSpectating);
    }

    /// <summary>
    /// Called by MexicoGameManager to notify UI of game phase change.
    /// </summary>
    public void SetGamePhase(MexicoGameManager.GamePhase phase)
    {
        Debug.Log($"[MexicoUIManager] SetGamePhase called: {phase}");
        currentPhase = phase;

        if (phase == MexicoGameManager.GamePhase.StartingRollOff)
        {
            // Show 1 die for roll-off
            if (oneDiceVisual != null) oneDiceVisual.SetActive(true);
            if (twoDiceVisual != null) twoDiceVisual.SetActive(false);
            
            if (diceThrowInput != null)
            {
                diceThrowInput.SetDiceCount(1);
                diceThrowInput.SetInteractable(true);
                Debug.Log("[MexicoUIManager] Dice input enabled for starting roll (1 die)");
            }
            if (currentPlayerText != null)
                currentPlayerText.text = "Roll to determine leader";
        }
        else if (phase == MexicoGameManager.GamePhase.GameInProgress)
        {
            // Show 2 dice for normal game
            if (oneDiceVisual != null) oneDiceVisual.SetActive(false);
            if (twoDiceVisual != null) twoDiceVisual.SetActive(true);
            
            if (diceThrowInput != null)
            {
                diceThrowInput.SetDiceCount(2);
                Debug.Log("[MexicoUIManager] Game in progress phase (2 dice)");
            }
        }
    }

    /// <summary>
    /// Called by the DiceThrowInput when a throw gesture is detected.
    /// Routes to correct ServerRpc based on game phase.
    /// </summary>
    private void OnDiceThrown()
    {
        if (isSpectating) return;
        if (mexicoManager == null) return;

        if (currentPhase == MexicoGameManager.GamePhase.StartingRollOff)
        {
            // Starting roll-off: all players throw 1 die
            Debug.Log($"[MexicoUIManager] Throwing for starting roll-off");
            mexicoManager.RequestStartingRollServerRpc();
        }
        else if (currentPhase == MexicoGameManager.GamePhase.GameInProgress)
        {
            // Normal game: only active player throws 2 dice
            if (!localPlayer.IsMyTurn) 
            {
                Debug.Log($"[MexicoUIManager] Not your turn, ignoring throw");
                return;
            }
            Debug.Log($"[MexicoUIManager] Throwing 2 dice");
            mexicoManager.RequestRollServerRpc();
        }
    }

    /// <summary>
    /// Call this during game setup to wire up the dice throw event.
    /// Normally called from Start().
    /// </summary>
    public void WireDiceThrowEvent()
    {
        if (diceThrowInput != null)
        {
            diceThrowInput.OnThrow += OnDiceThrown;
        }
    }

    private void OnContinuePressed()
    {
        if (mexicoManager == null) return;
        Debug.Log("[MexicoUIManager] Continue pressed - stopping rolling for this player");
        // Call server RPC to stop rolling (server will validate requester is active player)
        mexicoManager.StopRollingServerRpc();

        // Disable continue locally until server responds
        if (continueRollButton != null)
            continueRollButton.gameObject.SetActive(false);
        if (diceThrowInput != null)
            diceThrowInput.SetInteractable(false);
    }

    /// <summary>
    /// Wire MexicoGameManager ClientRpc calls to local methods.
    /// Since you can't directly hook ClientRpcs, this should be called from 
    /// a game manager coroutine or wrapper that calls back to UI.
    /// 
    /// Alternatively, modify MexicoGameManager to call a static method on this class.
    /// </summary>
    public void WireMexicoManagerEvents()
    {
        // This is manually set up in MexicoGameManager by calling
        // MexicoUIManager methods after ClientRpcs are received.
        // See integration instructions below.
    }
}
