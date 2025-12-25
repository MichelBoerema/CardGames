using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Player : NetworkBehaviour
{
    public List<CardValue> hand = new List<CardValue>();

    public bool IsMyTurn { get; private set; }


    [Header("Punishment")]
    public int points;
    private int shotsUntilDeath;

    public override void OnNetworkSpawn()
    {
        Debug.Log($"Player spawned | Server={IsServer} | Owner={IsOwner}");
    }

    [ClientRpc]
    public void SetTurnClientRpc(bool isMyTurn)
    {
        IsMyTurn = isMyTurn;

        Debug.Log($"SetTurn | Player={OwnerClientId} | IsOwner={IsOwner} | MyTurn={isMyTurn}");

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

    public void InitializeRoulette()
    {
        if (!IsServer) return;

        points = 0;
        shotsUntilDeath = Random.Range(1, 7); // 1 to 6 inclusive

        Debug.Log($"Initial chamber: {shotsUntilDeath} safe shots");

        UpdatePointsClientRpc(points,6);
    }

    public void PullTrigger()
    {
        if (!IsServer) return;

        shotsUntilDeath--;
        points++;

        Debug.Log($"Click... {shotsUntilDeath} pulls remaining");
        UpdatePointsClientRpc(points, 6);

        if (shotsUntilDeath <= 0)
        {
            //TakeDamage(1);
            OnPlayerDied(); 
            Debug.Log($"New chamber rolled: {shotsUntilDeath} safe shots");
        }
    }

    void OnPlayerDied()
    {
        Debug.Log($"Player {OwnerClientId} has died!");
        // later: eliminate from turns, spectate mode, etc.
    }

    [ClientRpc]
    void UpdatePointsClientRpc(int newPoints, int maxPoints)
    {
        if (IsOwner && UIManager.Instance != null)
        {
            UIManager.Instance.UpdatePointsUI(newPoints, maxPoints);
        }
    }
}
