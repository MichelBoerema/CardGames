using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Generic "flick to throw" gesture detector. Attach to the RectTransform the
/// player drags (e.g. the pair of dice icons). Dragging it upward and
/// releasing with enough speed or distance raises OnThrow. Not tied to any
/// specific gamemode's rules - Mexico, Yahtzee, etc. can all reuse this.
/// </summary>
public class DiceThrowInput : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Throw Detection")]
    [Tooltip("Minimum upward distance (pixels) the pointer must travel to count as a throw.")]
    public float minUpwardDistance = 80f;
    [Tooltip("Minimum upward speed (pixels/second) at release to count as a throw.")]
    public float minUpwardSpeed = 300f;
    [Tooltip("How far the dice may be dragged before being clamped (pixels).")]
    public float dragClamp = 250f;

    [SerializeField] private RectTransform diceVisual;

    /// <summary>Raised when a valid upward throw gesture is completed.</summary>
    public event Action OnThrow;

    private Vector2 restPosition;
    private Vector2 dragStartPointer;
    private Vector2 lastPointerPosition;
    private float lastMoveTime;
    private float lastUpwardSpeed;
    private bool isInteractable = true;
    private bool isDragging;

    void Awake()
    {
        if (diceVisual == null) diceVisual = GetComponent<RectTransform>();
        restPosition = diceVisual.anchoredPosition;
    }

    /// <summary>Enable/disable dragging (e.g. only interactable on your turn).</summary>
    public void SetInteractable(bool interactable)
    {
        isInteractable = interactable;
        if (!interactable && !isDragging)
            ResetToRest();
    }

    public void ResetToRest()
    {
        if (diceVisual != null)
            diceVisual.anchoredPosition = restPosition;
    }

    public RectTransform DiceVisual => diceVisual;
    public Vector2 RestPosition => restPosition;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!isInteractable) return;

        isDragging = true;
        dragStartPointer = eventData.position;
        lastPointerPosition = eventData.position;
        lastMoveTime = Time.unscaledTime;
        lastUpwardSpeed = 0f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isInteractable || !isDragging) return;

        Vector2 delta = eventData.position - dragStartPointer;
        delta.x = Mathf.Clamp(delta.x, -dragClamp, dragClamp);
        delta.y = Mathf.Clamp(delta.y, -dragClamp * 0.3f, dragClamp); // discourage dragging downward

        diceVisual.anchoredPosition = restPosition + delta;

        float dt = Mathf.Max(Time.unscaledTime - lastMoveTime, 0.0001f);
        lastUpwardSpeed = (eventData.position.y - lastPointerPosition.y) / dt;

        lastPointerPosition = eventData.position;
        lastMoveTime = Time.unscaledTime;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isInteractable || !isDragging) return;
        isDragging = false;

        float totalUpwardDistance = eventData.position.y - dragStartPointer.y;
        bool passedDistance = totalUpwardDistance >= minUpwardDistance;
        bool passedSpeed = lastUpwardSpeed >= minUpwardSpeed;

        if (passedDistance || passedSpeed)
            OnThrow?.Invoke();
        else
            ResetToRest();
    }
}
