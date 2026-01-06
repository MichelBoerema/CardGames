using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class Player : NetworkBehaviour
{
    public List<CardValue> hand = new List<CardValue>();

    [Header("Punishment")]
    public int points;
    private int shotsUntilDeath;

    public bool IsMyTurn { get; private set; }
    public bool IsAlive { get; set; } = true;


    public NetworkVariable<FixedString32Bytes> PlayerName =
        new NetworkVariable<FixedString32Bytes>(
            "Player",
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<int> AvatarId =
        new NetworkVariable<int>(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server
        );

    public Sprite defaultAvatar;

    private Texture2D ResizeTexture(Texture2D src, int width, int height)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height);
        Graphics.Blit(src, rt);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D newTex = new Texture2D(width, height);
        newTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        newTex.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        return newTex;
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            UploadPlayerName();
            StartCoroutine(UploadAvatarWhenReady());

            if (UIManager.Instance != null)
            {
                UIManager.Instance.SetLocalPlayer(this);
            }
        }

        base.OnNetworkSpawn();

        // Rebuild player list when a player appears
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.RequestPlayerListRefresh();
        }

        // Listen for name/avatar changes
        PlayerName.OnValueChanged += OnPlayerDataChanged;
        AvatarId.OnValueChanged += OnAvatarChanged;
    }

    private IEnumerator UploadAvatarWhenReady()
    {
        while (AvatarDatabase.Instance == null || !AvatarDatabase.Instance.IsSpawned)
            yield return null;

        Texture2D avatarTex = LobbyAvatarController.LoadSavedAvatar();
        if (avatarTex == null)
            yield break;

        Texture2D small = ResizeTexture(avatarTex, 32, 32);
        byte[] compressed = small.EncodeToJPG(10);

        UploadAvatarServerRpc(compressed);
    }

    [ServerRpc(RequireOwnership = false)]
    private void UploadAvatarServerRpc(byte[] compressedAvatar)
    {
        int id = AvatarDatabase.Instance.AddAvatar(compressedAvatar);
        AvatarId.Value = id;
    }

    private void UploadPlayerName()
    {
        string chosenName = PlayerPrefs.GetString("PlayerName", "Player");

        if (string.IsNullOrEmpty(chosenName))
            chosenName = "Player";

        if (chosenName.Length > 16)
            chosenName = chosenName.Substring(0, 16);

        SetPlayerNameServerRpc(chosenName);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetPlayerNameServerRpc(string name, ServerRpcParams rpcParams = default)
    {
        PlayerName.Value = name;
    }

    public Sprite GetNetworkAvatar()
    {
        Sprite avatar = AvatarDatabase.Instance?.GetAvatar(AvatarId.Value);
        return avatar != null ? avatar : defaultAvatar;
    }

    private void OnPlayerDataChanged(FixedString32Bytes oldVal, FixedString32Bytes newVal)
    {
        LobbyManager.Instance?.RequestPlayerListRefresh();
    }

    private void OnAvatarChanged(int oldVal, int newVal)
    {
        LobbyManager.Instance?.RequestPlayerListRefresh();
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

    public bool HasCardsInHand()
    {
        return hand.Count > 0;
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

    public int GetCardsInHandCount()
    {
        return hand.Count;
    }

    public void InitializeRoulette()
    {
        if (!IsServer) return;

        points = 0;
        shotsUntilDeath = UnityEngine.Random.Range(1, 7);

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
