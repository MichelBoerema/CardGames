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

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        pointPopupPanel.SetActive(false);
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
        playerSelectRoot.SetActive(true);
        onPlayerSelected = onSelected;

        foreach (Transform child in playerButtonParent)
            Destroy(child.gameObject);

        foreach (var player in players)
        {
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
}
