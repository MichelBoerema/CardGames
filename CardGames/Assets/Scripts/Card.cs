using UnityEngine;
using UnityEngine.UI;

public class Card : MonoBehaviour
{
    public CardValue cardValue;
    public bool IsSelected { get; private set; }

    private Button button;
    private Text buttonText;
    private Image background;

    void Awake()
    {
        button = GetComponent<Button>();
        buttonText = GetComponentInChildren<Text>();
        background = GetComponent<Image>();

        button.onClick.AddListener(ToggleSelected);
    }

    public void Setup(CardValue value)
    {
        cardValue = value;
        buttonText.text = GetCardShortName(cardValue);
        SetSelected(false);
    }

    void ToggleSelected()
    {
        SetSelected(!IsSelected);
        UIManager.Instance.OnCardSelectionChanged(this);
    }

    public void SetInteractable(bool interactable)
    {
        button.interactable = interactable;
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;

        // simpele visual feedback
        background.color = IsSelected ? Color.yellow : Color.white;
    }

    string GetCardShortName(CardValue value)
    {
        switch (value)
        {
            case CardValue.King: return "K";
            case CardValue.Queen: return "Q";
            case CardValue.Ace: return "A";
            case CardValue.Joker: return "J";
            default: return "?";
        }
    }
}
