using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Player : NetworkBehaviour
{
    public List<CardValue> hand = new List<CardValue>();

    public bool IsMyTurn { get; private set; }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"Player spawned | Server={IsServer} | Owner={IsOwner}");
    }

    public void SetTurn(bool isMyTurn)
    {
        IsMyTurn = isMyTurn;

        if (IsOwner && UIManager.Instance != null)
        {
            UIManager.Instance.SetPlayerTurn(isMyTurn);
        }
    }

    public void AddCard(CardValue card)
    {
        if (!IsServer) return;

        ReceiveCardClientRpc(card);
    }

    [ClientRpc]
    void ReceiveCardClientRpc(CardValue card)
    {
        hand.Add(card);

        if (IsOwner)
        {
            UIManager.Instance.AddCardToHand(card);
        }
    }

    public void ClearHand()
    {
        if (!IsServer) return;

        ClearHandClientRpc();
    }

    [ClientRpc]
    void ClearHandClientRpc()
    {
        hand.Clear();

        if (IsOwner && UIManager.Instance != null)
        {
            UIManager.Instance.ClearHandUI();
        }
    }

}
