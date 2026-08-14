using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

    [Header("Dice Configuration")]
    [Tooltip("Number of dice to throw (1 for starting roll-off, 2 for normal game)")]
    public int diceCount = 1;

    [SerializeField] private RectTransform diceVisual;
    [Header("Visuals")]
    [Tooltip("Assign 6 sprites for faces 1..6 in order")]
    public Sprite[] dieFaceSprites = new Sprite[6];
    public Image dieImage1;
    public Image dieImage2;

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

    /// <summary>Set single die face (1..6)</summary>
    public void SetDiceFace(int face)
    {
        if (dieImage1 == null || dieFaceSprites == null) return;
        int idx = Mathf.Clamp(face - 1, 0, Mathf.Max(0, dieFaceSprites.Length - 1));
        if (idx >= 0 && idx < dieFaceSprites.Length && dieFaceSprites[idx] != null)
            dieImage1.sprite = dieFaceSprites[idx];
    }

    /// <summary>Set two dice faces (1..6 each)</summary>
    public void SetDiceFaces(int faceA, int faceB)
    {
        if (dieImage1 == null || dieImage2 == null || dieFaceSprites == null) return;
        int a = Mathf.Clamp(faceA - 1, 0, Mathf.Max(0, dieFaceSprites.Length - 1));
        int b = Mathf.Clamp(faceB - 1, 0, Mathf.Max(0, dieFaceSprites.Length - 1));
        if (a >= 0 && a < dieFaceSprites.Length && dieFaceSprites[a] != null)
            dieImage1.sprite = dieFaceSprites[a];
        if (b >= 0 && b < dieFaceSprites.Length && dieFaceSprites[b] != null)
            dieImage2.sprite = dieFaceSprites[b];
    }

    /// <summary>Set how many dice to throw (1 or 2)</summary>
    public void SetDiceCount(int count)
    {
        diceCount = Mathf.Max(1, count);
    }

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

        Vector2 newPos = restPosition + delta;

        // Clamp within parent bounds
        RectTransform parent = diceVisual.parent as RectTransform;
        if (parent != null)
        {
            Rect parentRect = parent.rect;
            float halfWidth = diceVisual.rect.width / 2f;
            float halfHeight = diceVisual.rect.height / 2f;

            newPos.x = Mathf.Clamp(newPos.x, -parentRect.width / 2f + halfWidth, parentRect.width / 2f - halfWidth);
            newPos.y = Mathf.Clamp(newPos.y, -parentRect.height / 2f + halfHeight, parentRect.height / 2f - halfHeight);
        }

        diceVisual.anchoredPosition = newPos;

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
        {
            // Show immediate visual feedback locally while server processes roll
            if (diceCount == 1)
            {
                int r = UnityEngine.Random.Range(1, 7);
                SetDiceFace(r);
            }
            else
            {
                int r1 = UnityEngine.Random.Range(1, 7);
                int r2 = UnityEngine.Random.Range(1, 7);
                SetDiceFaces(r1, r2);
            }

            OnThrow?.Invoke();
        }
        else
            ResetToRest();
    }
}
