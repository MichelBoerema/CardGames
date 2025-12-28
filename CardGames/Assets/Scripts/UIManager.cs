using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Hand UI")]
    public Transform handUIParent;
    public GameObject cardButtonPrefab;

    [Header("Game UI")]
    public Text tableRankText;
    public Text lastClaims;

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

    public void AddCardToHand(CardValue cardValue)
    {
        GameObject cardGO = Instantiate(cardButtonPrefab, handUIParent);
        Card card = cardGO.GetComponent<Card>();
        card.Setup(cardValue);
    }

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

    public void SetLocalPlayer(Player player)
    {
        localPlayer = player;
    }

    public void SetPlayerTurn(bool isMyTurn)
    {
        playCardsButton.interactable = isMyTurn;
        callBluffButton.interactable = isMyTurn;

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

    public void UpdatePointsUI(int newPoints, int maxPoints)
    {
        pointsText.text = $"{newPoints}/{maxPoints}";
    }

    public void UpdateLastClaims(FixedString32Bytes PlayerName, int amountClaimed, TableRank currentTableRank)
    {
        lastClaims.text = $"{PlayerName} claimed {amountClaimed}X {currentTableRank}";
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

}
