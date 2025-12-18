using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Player : NetworkBehaviour
{
    public List<CardValue> hand = new List<CardValue>();

    public bool IsMyTurn { get; private set; }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
            Debug.Log("Local player spawned for client: " + OwnerClientId);
        else
            Debug.Log("Remote player spawned for client: " + OwnerClientId);
    }


    public void SetTurn(bool isMyTurn)
    {
        IsMyTurn = isMyTurn;

        if (IsOwner)
        {
            UIManager.Instance.SetPlayerTurn(isMyTurn);
        }
    }

    public void AddCard(CardValue card)
    {
        hand.Add(card);

        if (IsOwner)
        {
            UIManager.Instance.AddCardToHand(card);
        }
    }
}
