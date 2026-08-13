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

    [Header("Popups")]
    [Tooltip("Duration each roll notification appears")]
    public float rollNotificationDuration = 2f;
    [Tooltip("Duration loser announcement appears")]
    public float loserAnnouncementDuration = 3f;

    private MexicoGameManager mexicoManager;
    private Player localPlayer;
    private bool isSpectating = false;
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
        mexicoManager = MexicoGameManager.Instance;
        if (mexicoManager == null)
        {
            Debug.LogError("MexicoUIManager: MexicoGameManager not found!");
            return;
        }

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
        if (localPlayer != null && mexicoManager.TurnOrder != null)
        {
            isSpectating = !mexicoManager.TurnOrder.Any(p => p == localPlayer);
        }
        else
        {
            isSpectating = true;
        }

        // Disable dice throw if spectating
        if (diceThrowInput != null)
            diceThrowInput.SetInteractable(!isSpectating);

        // Hook to MexicoGameManager events
        mexicoManager.RoundLost += OnRoundLost;

        // Initialize UI
        UpdateCurrentPlayerIndicator();
        ClearRoundResultsPanel();
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
    /// This should be called from a hook to Player's turn changes.
    /// </summary>
    public void UpdateCurrentPlayerIndicator(Player currentPlayer = null)
    {
        if (currentPlayer == null && localPlayer != null)
        {
            currentPlayer = localPlayer;
        }

        if (currentPlayer == null)
        {
            if (currentPlayerText != null) currentPlayerText.text = "Waiting...";
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

        // Fade in
        yield return StartCoroutine(UIManager.Instance.FadeCanvasGroup(canvasGroup, 1f, 0.5f));
        yield return new WaitForSeconds(loserAnnouncementDuration - 1f);

        // Fade out
        yield return StartCoroutine(UIManager.Instance.FadeCanvasGroup(canvasGroup, 0f, 0.5f));

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
    /// Called by the DiceThrowInput when a throw gesture is detected.
    /// This should be wired in Start() via diceThrowInput.OnThrow += OnDiceThrown.
    /// </summary>
    private void OnDiceThrown()
    {
        if (isSpectating) return;
        if (mexicoManager == null) return;
        if (!localPlayer.IsMyTurn) return; // Safety check

        // Request roll from the server
        mexicoManager.RequestRollServerRpc();
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
