using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

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

    [SerializeField] private Sprite cachedAvatarSprite;
    [SerializeField] public Sprite defaultAvatar;
    public NetworkVariable<FixedString4096Bytes> AvatarBase64 =
    new NetworkVariable<FixedString4096Bytes>(
        "",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );


    [Header("Punishment")]
    public int points;
    private int shotsUntilDeath;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            SendNameToServer();
            SendAvatarToServer();
        }

        AvatarBase64.OnValueChanged += OnAvatarChanged;

        if (!string.IsNullOrEmpty(AvatarBase64.Value.ToString()))
            OnAvatarChanged("", AvatarBase64.Value);
    }

    void SendAvatarToServer()
    {
        Texture2D avatar = LobbyAvatarController.LoadSavedAvatar();
        if (avatar == null)
            return;

        string base64 = System.Convert.ToBase64String(avatar.EncodeToPNG());
        SetAvatarServerRpc(base64);
    }

    [ServerRpc(RequireOwnership = false)]
    void SetAvatarServerRpc(string base64)
    {
        AvatarBase64.Value = base64;
    }
    public Sprite GetAvatarSprite()
    {
        return cachedAvatarSprite != null
            ? cachedAvatarSprite
            : defaultAvatar;
    }

    void OnAvatarChanged(FixedString4096Bytes oldValue, FixedString4096Bytes newValue)
    {
        if (string.IsNullOrEmpty(newValue.ToString()))
        {
            cachedAvatarSprite = defaultAvatar;
            return;
        }

        byte[] data = System.Convert.FromBase64String(newValue.ToString());

        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(data);

        cachedAvatarSprite = Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f)
        );
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
