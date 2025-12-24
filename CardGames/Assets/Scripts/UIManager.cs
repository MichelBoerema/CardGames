using System.Collections.Generic;
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

    [Header("Table Rank UI")]
    public Text tableRankText;

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
}
