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

    public AudioSource cardAudioS;
    public AudioClip placeCardClip;

    [Header("Hand UI")]
    public Transform handUIParent;
    public GameObject cardButtonPrefab;
    private const int MAX_SELECTED_CARDS = 3;

    [Header("Game UI")]
    public Text tableRankText;
    public Text lastClaims;
    public Text playingPlayer;
    public Text myPlayerNameText;
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

    [Header("Cards Left")]
    [SerializeField] private Image cardsLeftImage;
    [SerializeField] private Sprite[] cardsLeftSprites;

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
    public GameObject punishedAvatarRoot;
    public Text punishedPlayerNameText;
    public Text punishedPlayerChamberText;
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
    public AudioClip deckShuffleClip;
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

    public void AddCardToHand(PlayingCard playingCard)
    {
        GameObject cardGO = Instantiate(cardButtonPrefab, handUIParent);
        Card card = cardGO.GetComponent<Card>();
        card.Setup(playingCard);
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

        List<PlayingCard> playedCards = new();

        foreach (Card card in selectedCards)
        {
            playedCards.Add(card.playingCard);
            Destroy(card.gameObject);
        }

        // Clear the selected cards list
        selectedCards.Clear();

        // Remove these cards from the local player's hand
        if (localPlayer != null)
        {
            foreach (PlayingCard card in playedCards)
            {
                localPlayer.hand.Remove(card);
            }
        }

        cardAudioS.PlayOneShot(placeCardClip);
        // Send played cards to server
        BluffGamemanager.Instance.PlayCardsServerRpc(playedCards.ToArray());
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
        gameOverPanel.SetActive(false);

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

    public void ShowFullRoundIntro(TableRank rank, List<PlayingCard> deck)
    {
        StartCoroutine(FullRoundIntroRoutine(rank, deck));
    }

    IEnumerator FullRoundIntroRoutine(TableRank rank, List<PlayingCard> deck)
    {
        yield return PreRoundIntroRoutineInternal();

        ShowRoundStartPopup(deck);
        cardAudioS.PlayOneShot(deckShuffleClip);
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

    public void ShowRoundStartPopup(List<PlayingCard> deck)
    {
        infoPopup.SetActive(true);
        ClearSpawnedCards();

        titleText.text = "Round Started";

        int kings = deck.Count(c => c.Value == PlayingDeckCardValue.King);
        int queens = deck.Count(c => c.Value == PlayingDeckCardValue.Queen);
        int aces = deck.Count(c => c.Value == PlayingDeckCardValue.Ace);
        int jokers = deck.Count(c => c.Value == PlayingDeckCardValue.Joker);

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
        nextPlayerInfoPanel.SetActive(false);
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
        Player lastPlayer,
    Player punishedPlayer,
    int chamberIndex,
    PlayingCard[] cards,
    TableRank rank,
    bool survived)
    {
        if (isPopupLocked)
            return;

        StartCoroutine(
            BluffRevealSequence(lastPlayer, punishedPlayer, chamberIndex, cards, rank, survived)
        );
    }

    IEnumerator BluffRevealSequence(
        Player lastPlayer,
    Player punishedPlayer,
    int chamberIndex,
    PlayingCard[] cards,
    TableRank rank,
    bool survived)
    {
        isPopupLocked = true;

        Debug.Log($"punishedPlayer is {punishedPlayer.PlayerName.Value}");

        ShowBluffReveal(
            lastPlayer.PlayerName.Value,
            punishedPlayer.PlayerName.Value,
            cards,
            rank
        );

        yield return new WaitForSeconds(2.5f);

        ClearSpawnedCards();
        descriptionText.text = "";

        yield return StartCoroutine(
            ShowBluffSurvivalSequence(punishedPlayer, chamberIndex, survived)
        );

        HidePopup();
        isPopupLocked = false;
    }

    IEnumerator ShowBluffSurvivalSequence(
    Player punishedPlayer,
    int chamberIndex,
    bool survived)
    {
        infoPopup.SetActive(true);
        ClearSpawnedCards();

        titleText.text = "";

        punishedAvatarImage.sprite = punishedPlayer.GetNetworkAvatar();
        punishedAvatarRoot.SetActive(true);

        punishedPlayerNameText.text = punishedPlayer.PlayerName.Value.ToString();
        punishedPlayerNameText.gameObject.SetActive(true);

        punishedPlayerChamberText.text = $"{chamberIndex - 1}/6";
        gunObject.SetActive(true);

        yield return StartCoroutine(
            PlayGunSequenceWithResultText(punishedPlayer, chamberIndex, survived)
        );

        gunObject.SetActive(false);
        punishedAvatarRoot.SetActive(false);
        punishedPlayerNameText.gameObject.SetActive(false);
    }

    IEnumerator PlayGunSequenceWithResultText(
    Player punishedPlayer,
    int chamberIndex,
    bool survived)
    {
        gunAnimator.SetTrigger("Idle");
        yield return new WaitForSeconds(0.3f);

        gunAnimator.SetTrigger("Aim");
        yield return new WaitForSeconds(2f);

        punishedPlayerChamberText.text = $"{chamberIndex}/6";
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
    FixedString32Bytes punishedPlayerName,
    PlayingCard[] cards,
    TableRank rank)
{
        Debug.Log($"ShowBluffReveal called for {playerName}");

        infoPopup.SetActive(true);
    ClearSpawnedCards();

    bool lied = false;

    foreach (PlayingCard playingCard in cards)
    {
        if (playingCard.Value != PlayingDeckCardValue.Joker &&
            !DoesCardMatchTableRank(playingCard, rank))
        {
            lied = true;
            break;
        }
    }

    titleText.text = lied
        ? $"{playerName} LIED!"
        : $"{playerName} told the truth!";

    descriptionText.text = $"{punishedPlayerName} gets SHOT!";

    foreach (PlayingCard playingCard in cards)
    {
        GameObject cardGO = Instantiate(uiCardPrefab, cardSpawnParent);
        Card card = cardGO.GetComponent<Card>();

        card.Setup(playingCard);
        card.SetInteractable(false);

        bool isCorrect =
            playingCard.Value == PlayingDeckCardValue.Joker ||
            DoesCardMatchTableRank(playingCard, rank);

        card.HighlightCard(isCorrect);
    }
}

    private bool DoesCardMatchTableRank(PlayingCard card, TableRank rank)
    {
        switch (rank)
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

    public void HideGameOver()
    {
        gameOverPanel.SetActive(false);
        restartGameButton.gameObject.SetActive(false);
        endServerButton.gameObject.SetActive(false);
    }
    IEnumerator HidePopupAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HidePopup();
    }
    #endregion

    #region UI
    public void SetMyPlayerName(string playerName)
    {
        myPlayerNameText.text = playerName;
    }

    public void UpdatePointsUI(int newPoints, int maxPoints)
    {
        pointsText.text = $"{newPoints}/{maxPoints}";
    }

    public void UpdateLastClaims(FixedString32Bytes PlayerName, int amountClaimed, TableRank currentTableRank)
    {
        lastClaims.text = $"{PlayerName}\n claims \n{amountClaimed}X {currentTableRank}";
    }

    public void UpdatePlayingPlayer(FixedString32Bytes PlayerName)
    {
        playingPlayer.text = $"{PlayerName}'s turn...";
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
        //nextPlayerCardsLeftText.text = cardsLeft.ToString();
        nextPlayerPointsAmount.text = $"{points}/6";

        Sprite avatar = AvatarDatabase.Instance?.GetAvatar(avatarId);
        nextPlayerAvatarImage.sprite = avatar != null
            ? avatar
            : fallbackAvatar;

        if (cardsLeft == 0)
        {
            cardsLeftImage.sprite = null;
        }
        else if (cardsLeft > 0 && cardsLeft < cardsLeftSprites.Length)
        {
            cardsLeftImage.sprite = cardsLeftSprites[cardsLeft - 1];
        }
    }

    #endregion
}
