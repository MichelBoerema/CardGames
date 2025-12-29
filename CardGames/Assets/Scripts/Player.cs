using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class Player : NetworkBehaviour
{
    public List<CardValue> hand = new List<CardValue>();

    public bool IsMyTurn { get; private set; }
    public bool IsAlive { get; set; } = true;


    public NetworkVariable<FixedString32Bytes> PlayerName =
    new NetworkVariable<FixedString32Bytes>(
        "Player",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Punishment")]
    public int points;
    private int shotsUntilDeath;

    public override void OnNetworkSpawn()
    {
        Debug.Log($"Player spawned | Server={IsServer} | Owner={IsOwner}");
        if (IsOwner)
        {
            SendNameToServer();
        }
    }

    [ClientRpc]
    public void SetTurnClientRpc(bool isMyTurn)
    {
        IsMyTurn = isMyTurn;

        if (IsOwner && UIManager.Instance != null)
        {
            UIManager.Instance.SetPlayerTurn(isMyTurn);
        }
    }

    void SendNameToServer()
    {
        string savedName = PlayerPrefs.GetString("PlayerName", "Player");
        SetPlayerNameServerRpc(savedName);
    }

    [ServerRpc(RequireOwnership = false)]
    void SetPlayerNameServerRpc(string name, ServerRpcParams rpcParams = default)
    {
        PlayerName.Value = name;
        Debug.Log($"Server set name for client {rpcParams.Receive.SenderClientId}: {name}");
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

    public void PullTrigger(FixedString32Bytes PlayerName)
    {
        if (!IsServer) return;

        shotsUntilDeath--;
        points++;

        Debug.Log($"Click... {shotsUntilDeath} pulls remaining");
        UpdatePointsClientRpc(points, 6);

        if (shotsUntilDeath <= 0)
        {
            OnPlayerDied(); 
        }

        UIManager.Instance.ShowBluffSurvivalPopup(PlayerName, IsAlive);
    }

    void OnPlayerDied()
    {
        if (!IsServer) return;

        IsAlive = false;

        Debug.Log($"Player {PlayerName.Value} has died!");

        BluffGamemanager.Instance.OnPlayerDied(this);
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
