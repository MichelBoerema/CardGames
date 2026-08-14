using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Temporary debug script - attach to DiceVisual to verify drag events are firing.
/// Remove this after confirming drag detection works.
/// </summary>
public class DiceThrowInputDebug : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public void OnBeginDrag(PointerEventData eventData)
    {
        Debug.Log("✓ OnBeginDrag FIRED - drag detection working!");
    }

    public void OnDrag(PointerEventData eventData)
    {
        Debug.Log($"✓ OnDrag FIRED - position: {eventData.position}");
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log("✓ OnEndDrag FIRED");
    }
}
