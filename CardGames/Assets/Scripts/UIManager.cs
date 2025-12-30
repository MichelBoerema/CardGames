using System.Collections;
using System.Collections.Generic;
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

    [Header("Game UI")]
    public Text tableRankText;
    public Text lastClaims;

    [Header("Popup")]
    public GameObject infoPopup;
    public Text titleText;
    public Text descriptionText;
    public Transform cardSpawnParent;
    public GameObject uiCardPrefab;


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

        selectedCards.Clear();

        BluffGamemanager.Instance.PlayCardsServerRpc(playedValues.ToArray());
    }
    public void CallBluff()
    {
        BluffGamemanager.Instance.CallBluffServerRpc();
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

        NetworkManager.Singleton.Shutdown();
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
            if (!selectedCards.Contains(card))
                selectedCards.Add(card);
        }
        else
        {
            selectedCards.Remove(card);
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
        {
            Destroy(child.gameObject);
        }

        ClearSelection();
    }
    #endregion

    #region Popups
    public void HidePopup()
    {
        infoPopup.SetActive(false);
        ClearSpawnedCards();
    }

    void ClearSpawnedCards()
    {
        foreach (Transform child in cardSpawnParent)
            Destroy(child.gameObject);
    }

    public void ShowRoundStartPopup(TableRank tableRank)
    {
        infoPopup.SetActive(true);
        ClearSpawnedCards();

        titleText.text = "Round Started";

        descriptionText.text =
            "Deck Contains:\n" +
            "• 6× King\n" +
            "• 6× Queen\n" +
            "• 6× Ace\n" +
            "• 2× Joker\n\n" +
            $"Table Rank: {tableRank}";

        StartCoroutine(HidePopupAfterDelay(4f));
    }
    public void ShowTableRankPopup(TableRank rank)
    {
        infoPopup.SetActive(true);
        ClearSpawnedCards();

        titleText.text = "Table Rank";
        descriptionText.text = $"This round is played as:\n\n{rank}";

        StartCoroutine(HidePopupAfterDelay(2.5f));
    }

    public void ShowBluffRevealSequence(
    FixedString32Bytes playerName,
    CardValue[] cards,
    TableRank rank,
    bool survived)
    {
        StartCoroutine(BluffRevealSequence(playerName, cards, rank, survived));
    }
    IEnumerator BluffRevealSequence(
    FixedString32Bytes playerName,
    CardValue[] cards,
    TableRank rank,
    bool survived)
    {
        ShowBluffReveal(playerName, cards, rank);

        yield return new WaitForSeconds(3f);

        ShowBluffSurvivalPopup(playerName, survived);

        yield return new WaitForSeconds(5f);

        HidePopup();
    }
    public void ShowBluffSurvivalPopup(
    FixedString32Bytes playerName,
    bool survived)
    {
        infoPopup.SetActive(true);
        ClearSpawnedCards();

        titleText.text = "Bluff Result";
        descriptionText.text = survived
            ? $"{playerName} survived!"
            : $"{playerName} died!";
    }

    public void ShowBluffReveal(
    FixedString32Bytes playerName,
    CardValue[] cards,
    TableRank rank)
    {
        infoPopup.SetActive(true);
        ClearSpawnedCards();

        titleText.text = "Bluff Revealed";
        descriptionText.text = $"{playerName} claimed {cards.Length} {rank}s";

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
    #endregion
}
