using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public enum PopupType
{
    FullRoundStart,
    TableRankOnly,
    BluffSurvival
}


public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Hand UI")]
    public Transform handUIParent;
    public GameObject cardButtonPrefab;
    private const int MAX_SELECTED_CARDS = 3;

    [Header("Game UI")]
    public Text tableRankText;
    public Text lastClaims;
    public Button leaveGameButton;

    [Header("Leave Game Confirmation")]
    public GameObject leaveGameConfirmationPopup; // Panel with Yes/No buttons
    public Button leaveGameYesButton;
    public Button leaveGameNoButton;

    [Header("Next Player Info UI")]
    public GameObject nextPlayerInfoPanel;
    public Image nextPlayerAvatarImage;
    public Text nextPlayerNameText;
    public Text nextPlayerCardsLeftText;
    public Text nextPlayerPointsAmount;
    [SerializeField] private Sprite fallbackAvatar;

    [Header("Popup")]
    public GameObject infoPopup;
    public Text titleText;
    public Text descriptionText;
    public Transform cardSpawnParent;
    public GameObject uiCardPrefab;
    public bool isPopupLocked = false;

    [Header("Title Animation")]
    public Animator titleAnimator;

    [Header("Bluff Animation")]
    public Image punishedAvatarImage;
    public Text punishedPlayerNameText;
    public GameObject gunObject;
    public Animator gunAnimator;
    public AudioSource gunAudio;
    public AudioClip gunBangClip;
    public AudioClip gunClickClip;

    [Header("Pre-Round Intro")]
    public GameObject preRoundIntroPopup;   // panel with image
    public Animator preRoundIntroAnimator;
    public AudioSource preGunAudio;
    public AudioClip gunReloadClip;
    [SerializeField] private float preRoundIntroDuration = 3f;

    [Header("Table Rank Title")]
    [SerializeField] private GameObject originalTableRankText;
    public Text tableRankTitleText;
    public GameObject panelTableRank;
    public Animator tableRankTitleAnimator;

    [Header("Central Pile")]
    public Transform pileParent;
    public GameObject cardBacksidePrefab;

    private readonly List<GameObject> activePileCards = new();

    [Header("Points UI")]
    public Text pointsText;

    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public Text winnerText;
    public Button restartGameButton;
    public Button endServerButton;

    private List<Card> selectedCards = new List<Card>();

    public Button playCardsButton;
    public Button callBluffButton;

    private Player localPlayer;


    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    private void Start()
    {
        if (leaveGameButton != null)
            leaveGameButton.onClick.AddListener(OnLeaveGameButtonClicked);
    }
    public void SetLocalPlayer(Player player)
    {
        localPlayer = player;
    }

    public void AddCardToHand(CardValue cardValue)
    {
        GameObject cardGO = Instantiate(cardButtonPrefab, handUIParent);
        Card card = cardGO.GetComponent<Card>();
        card.Setup(cardValue);
    }

    public void SetPlayerTurn(bool isMyTurn)
    {
        playCardsButton.interactable = isMyTurn;
        callBluffButton.interactable = isMyTurn;
        playCardsButton.gameObject.SetActive(isMyTurn);
        callBluffButton.gameObject.SetActive(isMyTurn);

        SetHandInteractable(isMyTurn);
    }
    void SetHandInteractable(bool interactable)
    {
        foreach (Transform child in handUIParent)
        {
            Card card = child.GetComponent<Card>();
            if (card != null)
            {
                card.SetInteractable(interactable);
            }
        }
    }
    private void OnLeaveGameButtonClicked()
    {
        ShowLeaveGameConfirmation();
        Debug.Log("Leave Game button clicked");
    }

    private void ShowLeaveGameConfirmation()
    {
        if (leaveGameConfirmationPopup == null) return;

        leaveGameConfirmationPopup.SetActive(true);
        playCardsButton.interactable = false;
        callBluffButton.interactable = false;

        leaveGameYesButton.onClick.RemoveAllListeners();
        leaveGameNoButton.onClick.RemoveAllListeners();



        leaveGameYesButton.onClick.AddListener(() =>
        {
            // Call the actual leave game logic
            BluffGamemanager.Instance?.LeaveGame();

            leaveGameConfirmationPopup.SetActive(false);
        });

        leaveGameNoButton.onClick.AddListener(() =>
        {
            leaveGameConfirmationPopup.SetActive(false);
            playCardsButton.interactable = true;
            callBluffButton.interactable = true;
        });
    }

    public void PlaySelectedCards()
    {
        if (selectedCards.Count == 0)
            return;

        List<CardValue> playedValues = new List<CardValue>();

        foreach (Card card in selectedCards)
        {
            playedValues.Add(card.cardValue);
            Destroy(card.gameObject);
        }

        // Clear the selected cards list
        selectedCards.Clear();

        // Remove these cards from the local player's hand
        if (localPlayer != null)
        {
            foreach (CardValue cv in playedValues)
            {
                localPlayer.hand.Remove(cv);
            }
        }

        // Send played cards to server
        BluffGamemanager.Instance.PlayCardsServerRpc(playedValues.ToArray());
    }

    public void CallBluff()
    {
        BluffGamemanager.Instance.CallBluffServerRpc();
    }

    public void ForceCallBluffOnly()
    {
        playCardsButton.interactable = false;
        playCardsButton.gameObject.SetActive(false);

        callBluffButton.interactable = true;
        callBluffButton.gameObject.SetActive(true);

        SetHandInteractable(false);
    }

    public void OnRestartGameClicked()
    {
        Debug.Log("trying to restart");
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.Log("restart failed");
            return;
        }

        BluffGamemanager.Instance.RestartGameServer();
    }

    public void OnEndServerClicked()
    {
        Debug.Log("trying to end");

        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.Log("end failed");
            return;
        }

        BluffGamemanager.Instance.GoBackToLobby();
    }

    #region UpdateTableRank
    public void UpdateTableRank(TableRank rank)
    {
        tableRankText.text = GetRankName(rank) + " table";
    }

    string GetRankName(TableRank rank)
    {
        switch (rank)
        {
            case TableRank.King: return "King's";
            case TableRank.Queen: return "Queen's";
            case TableRank.Ace: return "Ace's";
            default: return "?";
        }
    }
    #endregion

    #region CardSelection
    public void OnCardSelectionChanged(Card card)
    {
        if (card.IsSelected)
        {
            if (selectedCards.Count >= MAX_SELECTED_CARDS)
            {
                card.SetSelected(false);
                return;
            }

            selectedCards.Add(card);
        }
        else
        {
            selectedCards.Remove(card);
        }

        UpdateCardInteractability();
    }

    void UpdateCardInteractability()
    {
        bool canSelectMore = selectedCards.Count < MAX_SELECTED_CARDS;

        foreach (Transform child in handUIParent)
        {
            Card card = child.GetComponent<Card>();
            if (card == null) continue;

            if (!card.IsSelected)
                card.SetInteractable(canSelectMore);
        }
    }

    public List<Card> GetSelectedCards()
    {
        return selectedCards;
    }

    public void ClearSelection()
    {
        foreach (var card in selectedCards)
        {
            card.SetSelected(false);
        }
        selectedCards.Clear();
    }
    public void ClearHandUI()
    {
        foreach (Transform child in handUIParent)
            Destroy(child.gameObject);

        ClearSelection();
        UpdateCardInteractability();
    }

    #endregion

    #region Popups
    public void HidePopup()
    {
        infoPopup.SetActive(false);
        panelTableRank.SetActive(false);
        ClearSpawnedCards();
    }

    void ClearSpawnedCards()
    {
        foreach (Transform child in cardSpawnParent)
            Destroy(child.gameObject);
    }

    public void ShowFullRoundIntro(TableRank rank, List<CardValue> deck)
    {
        StartCoroutine(FullRoundIntroRoutine(rank, deck));
    }

    IEnumerator FullRoundIntroRoutine(TableRank rank, List<CardValue> deck)
    {
        yield return PreRoundIntroRoutineInternal();

        ShowRoundStartPopup(deck);
        yield return new WaitForSeconds(4f);

        ShowTableRankPopup(rank);
        yield return new WaitForSeconds(2f);

        HidePopup();
    }

    IEnumerator PreRoundIntroRoutineInternal()
    {
        HidePopup();

        preRoundIntroPopup.SetActive(true);
        preGunAudio.PlayOneShot(gunReloadClip);

        if (preRoundIntroAnimator != null)
            preRoundIntroAnimator.Play(0, 0, 0f);

        yield return new WaitForSeconds(preRoundIntroDuration);

        preRoundIntroPopup.SetActive(false);
    }

    public void ShowRoundStartPopup(List<CardValue> deck)
    {
        infoPopup.SetActive(true);
        ClearSpawnedCards();

        titleText.text = "Round Started";

        int kings = deck.Count(c => c == CardValue.King);
        int queens = deck.Count(c => c == CardValue.Queen);
        int aces = deck.Count(c => c == CardValue.Ace);
        int jokers = deck.Count(c => c == CardValue.Joker);

        descriptionText.text =
            $"Deck Contains:\n" +
            $"• {kings}× King\n" +
            $"• {queens}× Queen\n" +
            $"• {aces}× Ace\n" +
            $"• {jokers}× Joker";

        StartCoroutine(HidePopupAfterDelay(4f));
    }

    public void ShowTableRankPopup(TableRank rank)
    {
        StartCoroutine(ShowTableRankWhenReady(rank));
    }

    IEnumerator ShowTableRankWhenReady(TableRank rank)
    {
        while (isPopupLocked)
            yield return null;

        originalTableRankText.SetActive(false);
        infoPopup.SetActive(false);
        ClearSpawnedCards();

        panelTableRank.SetActive(true);
        tableRankTitleText.text = $"{rank}'s table";

        if (tableRankTitleAnimator != null)
        {
            tableRankTitleAnimator.ResetTrigger("ZoomOut");
            tableRankTitleAnimator.SetTrigger("ZoomOut");
        }

        yield return new WaitForSeconds(1.5f);

        panelTableRank.SetActive(false);
        originalTableRankText.SetActive(true);
        HidePopup();
    }

    public void ShowPlayedCardsPile(int cardCount)
    {
        //ClearPlayedCardsPile();

        for (int i = 0; i < cardCount; i++)
        {
            GameObject card = Instantiate(cardBacksidePrefab, pileParent);

            RectTransform rt = card.GetComponent<RectTransform>();

            // EXACT same position
            rt.anchoredPosition = Vector2.zero;

            // Random rotation only
            rt.localRotation = Quaternion.Euler(
                0f,
                0f,
                Random.Range(-12f, 12f)
            );
            rt.localPosition = new Vector3(0f, 0f, -i * 0.01f);

            // Ensure correct draw order (last card on top)
            rt.SetAsLastSibling();

            activePileCards.Add(card);
        }
    }

    public void ClearPlayedCardsPile()
    {
        foreach (var card in activePileCards)
            Destroy(card);

        activePileCards.Clear();
    }

    public void ShowBluffRevealSequence(
     Player punishedPlayer,
     CardValue[] cards,
     TableRank rank,
     bool survived)
    {
        if (isPopupLocked)
            return;

        StartCoroutine(BluffRevealSequence(punishedPlayer, cards, rank, survived));
    }

    IEnumerator BluffRevealSequence(
    Player punishedPlayer,
    CardValue[] cards,
    TableRank rank,
    bool survived)
    {
        isPopupLocked = true;

        ShowBluffReveal(
            punishedPlayer.PlayerName.Value,
            cards,
            rank
        );

        yield return new WaitForSeconds(2.5f);

        ClearSpawnedCards();
        descriptionText.text = "";

        yield return StartCoroutine(
            ShowBluffSurvivalSequence(punishedPlayer, survived)
        );

        HidePopup();

        isPopupLocked = false;
    }

    IEnumerator ShowBluffSurvivalSequence(
    Player punishedPlayer,
    bool survived)
    {
        infoPopup.SetActive(true);
        ClearSpawnedCards();

        titleText.text = "";

        punishedAvatarImage.sprite = punishedPlayer.GetNetworkAvatar();
        punishedAvatarImage.gameObject.SetActive(true);

        // Player Name
        punishedPlayerNameText.text = punishedPlayer.PlayerName.Value.ToString();
        punishedPlayerNameText.gameObject.SetActive(true);

        gunObject.SetActive(true);

        yield return StartCoroutine(
            PlayGunSequenceWithResultText(punishedPlayer, survived)
        );

        gunObject.SetActive(false);
        punishedAvatarImage.gameObject.SetActive(false);
        punishedPlayerNameText.gameObject.SetActive(false);
    }

    IEnumerator PlayGunSequenceWithResultText(
    Player punishedPlayer,
    bool survived)
    {
        yield return new WaitForSeconds(0.3f);

        gunAnimator.SetTrigger("Aim");
        yield return new WaitForSeconds(2f);

        if (survived)
        {
            gunAnimator.SetTrigger("Click");
            gunAudio.PlayOneShot(gunClickClip);
        }
        else
        {
            gunAnimator.SetTrigger("Bang");
            gunAudio.PlayOneShot(gunBangClip);
        }

        yield return new WaitForSeconds(2f);

        ClearPlayedCardsPile();

        BluffGamemanager.Instance.BluffAnimationFinishedServerRpc();
    }

    public void ShowBluffReveal(
    FixedString32Bytes playerName,
    CardValue[] cards,
    TableRank rank)
    {
        infoPopup.SetActive(true);
        ClearSpawnedCards();

        titleText.text = $"{playerName} claimed {cards.Length}X {rank}";
        descriptionText.text = "";

        foreach (CardValue cardValue in cards)
        {
            GameObject cardGO = Instantiate(uiCardPrefab, cardSpawnParent);
            Card card = cardGO.GetComponent<Card>();

            card.Setup(cardValue);
            card.SetInteractable(false);

            bool isCorrect =
                cardValue == CardValue.Joker ||
                DoesCardMatchTableRank(cardValue, rank);

            card.HighlightCard(isCorrect);
        }
    }

    private bool DoesCardMatchTableRank(CardValue card, TableRank rank)
    {
        switch (rank)
        {
            case TableRank.King: return card == CardValue.King;
            case TableRank.Queen: return card == CardValue.Queen;
            case TableRank.Ace: return card == CardValue.Ace;
            default: return false;
        }
    }

    public void ShowGameOver(FixedString32Bytes winnerName, bool isHost)
    {
        gameOverPanel.SetActive(true);

        winnerText.text = $"WINNER:\n{winnerName}";

        // Only host can control the game flow
        restartGameButton.gameObject.SetActive(isHost);
        endServerButton.gameObject.SetActive(isHost);

        restartGameButton.interactable = isHost;
        endServerButton.interactable = isHost;
    }

    IEnumerator HidePopupAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HidePopup();
    }
    #endregion

    #region UI
    public void UpdatePointsUI(int newPoints, int maxPoints)
    {
        pointsText.text = $"{newPoints}/{maxPoints}";
    }

    public void UpdateLastClaims(FixedString32Bytes PlayerName, int amountClaimed, TableRank currentTableRank)
    {
        lastClaims.text = $"{PlayerName}\n claims \n{amountClaimed}X {currentTableRank}";
    }

    public void UpdateNextPlayerInfo(
    FixedString32Bytes playerName,
    int avatarId,
    int cardsLeft,
    int points
    )
    {
        nextPlayerInfoPanel.SetActive(true);

        nextPlayerNameText.text = playerName.ToString();
        nextPlayerCardsLeftText.text = cardsLeft.ToString();
        nextPlayerPointsAmount.text = $"{points}/6";

        Sprite avatar = AvatarDatabase.Instance?.GetAvatar(avatarId);
        nextPlayerAvatarImage.sprite = avatar != null
            ? avatar
            : fallbackAvatar;
    }

    #endregion
}
