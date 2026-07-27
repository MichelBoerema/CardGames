using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BusUIManager : MonoBehaviour
{
    public static BusUIManager Instance;

    [Header("Player Select UI")]
    [SerializeField] private GameObject playerSelectRoot;
    [SerializeField] private Transform playerButtonParent;
    [SerializeField] private GameObject playerButtonPrefab;

    private Action<ulong> onPlayerSelected;

    [Header("Current Turn")]
    [SerializeField] private GameObject turnPanel;
    [SerializeField] private Text turnText;

    [Header("Player Hand")]
    [SerializeField] private Transform handParent;
    [SerializeField] private Transform busHandParent;
    [SerializeField] private GameObject handCardPrefab;
    [SerializeField] private GameObject busHandCardPrefab;

    private GameObject currentHandPrefab;

    [Header("Point Popup")]
    [SerializeField] private GameObject pointPopupPanel;
    [SerializeField] private Text pointPopupText;
    [SerializeField] private float popupDuration = 2.5f;
    private Coroutine popupRoutine;

    [Header("Result Popup")]
    [SerializeField] private GameObject resultPopup;
    [SerializeField] private Text resultText;

    [Header("Round Summary")]
    [SerializeField] private GameObject summaryPopup;
    [SerializeField] private Text summaryText;

    [Header("Round 1 UI")]
    [SerializeField] private GameObject redBlackRoot;
    [SerializeField] private Button redButton;
    [SerializeField] private Button blackButton;

    [Header("Round 2 UI")]
    [SerializeField] private GameObject higherLowerRoot;
    [SerializeField] private Button higherButton;
    [SerializeField] private Button lowerButton;

    [Header("Round 3 UI")]
    [SerializeField] private GameObject insideOutsideRoot;
    [SerializeField] private Button insideButton;
    [SerializeField] private Button outsideButton;

    [Header("Round 4 UI")]
    [SerializeField] private GameObject suitRoot;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    [Header("Bus UI")]
    [SerializeField] private GameObject busRoot;

    [SerializeField] private Transform busCardParent;
    [SerializeField] private GameObject busCardPrefab;

    [SerializeField] private Button skipBusButton;
    private readonly List<GameObject> busBacks = new();
    private readonly List<Card> busCards = new();

    [SerializeField] private GameObject cardBackPrefab;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        currentHandPrefab = handCardPrefab;

        pointPopupPanel.SetActive(false);
    }

    public void ShowCurrentTurn(string playerName)
    {
        turnPanel.SetActive(true);
        turnText.text = $"{playerName}'s Turn";
    }
    public void AddCardToHand(PlayingCard card)
    {
        GameObject cardObj = Instantiate(currentHandPrefab, handParent);

        Card ui = cardObj.GetComponent<Card>();
        ui.Setup(card);
    }
    public void HideCurrentTurn()
    {
        turnPanel.SetActive(false);
    }

    public void ShowResultPopup(bool correct)
    {
        if (popupRoutine != null)
            StopCoroutine(popupRoutine);

        resultPopup.SetActive(true);

        if (correct)
        {
            resultText.text = "Correct! \n Give 1 point to another player.";
            resultText.color = Color.green;
        }
        else
        {
            resultText.text = "Wrong! \n You received 1 point.";
            resultText.color = Color.red;
        }

        popupRoutine = StartCoroutine(HideResultPopup());
    }

    IEnumerator HideResultPopup()
    {
        yield return new WaitForSeconds(2f);
        resultPopup.SetActive(false);
    }
    public void ShowRoundSummary(int pointsThisRound)
    {
        summaryPopup.SetActive(true);

        if (pointsThisRound == 0)
        {
            summaryText.text = "Round Summary\n\nYou received no points this round";
        }
        else
        {
            summaryText.text =
                $"Round Summary\n\nYou received {pointsThisRound} point{(pointsThisRound > 1 ? "s" : "")} this round";
        }

        StartCoroutine(HideSummaryPopup());
    }

    IEnumerator HideSummaryPopup()
    {
        yield return new WaitForSeconds(2.5f);
        summaryPopup.SetActive(false);
    }
    public void ShowPointReceivedPopup(string fromPlayerName, int totalPoints)
    {
        if (popupRoutine != null)
            StopCoroutine(popupRoutine);

        popupRoutine = StartCoroutine(
            PointPopupRoutine(fromPlayerName, totalPoints)
        );
    }

    private IEnumerator PointPopupRoutine(string from, int total)
    {
        pointPopupText.text =
            $"You received <b>1 point</b> from <b>{from}</b>\n" +
            $"Total points: <b>{total}</b>";

        pointPopupPanel.SetActive(true);

        yield return new WaitForSeconds(popupDuration);

        pointPopupPanel.SetActive(false);
    }

    public void ShowPlayerSelection(
        List<Player> players,
        Action<ulong> onSelected)
    {
        Debug.Log("Opening player select UI");
        Debug.Log($"Selectable players: {players.Count}");

        playerSelectRoot.SetActive(true);
        onPlayerSelected = onSelected;

        foreach (Transform child in playerButtonParent)
            Destroy(child.gameObject);

        foreach (var player in players)
        {
            Debug.Log($"Adding button for {player.PlayerName.Value}");

            GameObject btnObj = Instantiate(playerButtonPrefab, playerButtonParent);
            Button button = btnObj.GetComponent<Button>();
            Text text = btnObj.GetComponentInChildren<Text>();

            ulong targetId = player.OwnerClientId;
            text.text = player.PlayerName.Value.ToString();

            button.onClick.AddListener(() =>
            {
                playerSelectRoot.SetActive(false);
                onPlayerSelected?.Invoke(targetId);
            });
        }
    }

    public void ShowRedBlackButtons(Action<RedBlackChoice> onChoice)
    {
        redBlackRoot.SetActive(true);

        redButton.onClick.RemoveAllListeners();
        blackButton.onClick.RemoveAllListeners();

        redButton.onClick.AddListener(() =>
        {
            redBlackRoot.SetActive(false);
            onChoice(RedBlackChoice.Red);
        });

        blackButton.onClick.AddListener(() =>
        {
            redBlackRoot.SetActive(false);
            onChoice(RedBlackChoice.Black);
        });
    }

    public void ShowHigherLowerButtons(Action<HigherLowerChoice> onChoice)
    {
        higherLowerRoot.SetActive(true);

        higherButton.onClick.RemoveAllListeners();
        lowerButton.onClick.RemoveAllListeners();

        higherButton.onClick.AddListener(() =>
        {
            higherLowerRoot.SetActive(false);
            onChoice(HigherLowerChoice.Higher);
        });

        lowerButton.onClick.AddListener(() =>
        {
            higherLowerRoot.SetActive(false);
            onChoice(HigherLowerChoice.Lower);
        });
    }
    public void ShowInsideOutsideButtons(Action<InsideOutsideChoice> onChoice)
    {
        insideOutsideRoot.SetActive(true);

        insideButton.onClick.RemoveAllListeners();
        outsideButton.onClick.RemoveAllListeners();

        insideButton.onClick.AddListener(() =>
        {
            insideOutsideRoot.SetActive(false);
            onChoice(InsideOutsideChoice.Inside);
        });

        outsideButton.onClick.AddListener(() =>
        {
            insideOutsideRoot.SetActive(false);
            onChoice(InsideOutsideChoice.Outside);
        });
    }
    public void ShowSuitButtons(Action<HasSuitChoice> onChoice)
    {
        suitRoot.SetActive(true);

        yesButton.onClick.RemoveAllListeners();
        noButton.onClick.RemoveAllListeners();

        yesButton.onClick.AddListener(() =>
        {
            suitRoot.SetActive(false);
            onChoice(HasSuitChoice.Yes);
        });

        noButton.onClick.AddListener(() =>
        {
            suitRoot.SetActive(false);
            onChoice(HasSuitChoice.No);
        });
    }

    public void CreateBus(List<BusRow> rows)
    {
        busRoot.SetActive(true);

        foreach (Transform row in busCardParent)
        {
            foreach (Transform child in row)
                Destroy(child.gameObject);
        }

        busCards.Clear();

        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            Transform rowTransform = busCardParent.GetChild(rowIndex);

            foreach (PlayingCard card in rows[rowIndex].cards)
            {
                // Create the visible card back
                GameObject back = Instantiate(cardBackPrefab, rowTransform);
                busBacks.Add(back);

                // Create the real card
                GameObject front = Instantiate(busCardPrefab, rowTransform);

                Card ui = front.GetComponent<Card>();
                ui.Setup(card);

                front.SetActive(false);

                busCards.Add(ui);
            }
        }
    }

    public void RevealBusCard(int row, int index)
    {
        int flatIndex = 0;

        for (int i = 0; i < row; i++)
            flatIndex += 4 - i;

        flatIndex += index;

        StartCoroutine(FlipCard(flatIndex));
    }

    IEnumerator FlipCard(int index)
    {
        GameObject back = busBacks[index];
        Transform t = back.transform;

        // Shrink the back
        while (t.localScale.x > 0)
        {
            t.localScale -= new Vector3(Time.deltaTime * 6f, 0f, 0f);
            yield return null;
        }

        Destroy(back);

        GameObject front = busCards[index].gameObject;
        front.SetActive(true);

        t = front.transform;
        t.localScale = new Vector3(0f, 1f, 1f);

        while (t.localScale.x < 1f)
        {
            t.localScale += new Vector3(Time.deltaTime * 6f, 0f, 0f);
            yield return null;
        }

        t.localScale = Vector3.one;
    }

    public void ShowBusPlayChoice(
    List<PlayingCard> hand,
    PlayingCard busCard)
    {
        // TODO
    }
}
