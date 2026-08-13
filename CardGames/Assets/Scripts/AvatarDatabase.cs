using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class AvatarDatabase : NetworkBehaviour
{
    public static AvatarDatabase Instance { get; private set; }

    // Server-authoritative storage
    private readonly Dictionary<int, byte[]> avatarData = new Dictionary<int, byte[]>();
    private readonly List<Sprite> avatars = new List<Sprite>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        // Send ALL existing avatars to the newly connected client
        foreach (var pair in avatarData)
        {
            SendAvatarToClientRpc(pair.Value, pair.Key,
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { clientId }
                    }
                });
        }
    }

    // SERVER ONLY
    public int AddAvatar(byte[] compressedAvatar)
    {
        if (!IsServer)
            throw new System.Exception("Only server can add avatars");

        Texture2D tex = new Texture2D(64, 64);
        tex.LoadImage(compressedAvatar);
        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f)
        );

        int id = avatars.Count;
        avatars.Add(sprite);
        avatarData[id] = compressedAvatar;

        // Broadcast to all current clients
        SendAvatarToClientRpc(compressedAvatar, id);

        return id;
    }

    [ClientRpc]
    private void SendAvatarToClientRpc(byte[] compressedAvatar, int id, ClientRpcParams rpcParams = default)
    {
        Texture2D tex = new Texture2D(64, 64);
        tex.LoadImage(compressedAvatar);

        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f)
        );

        while (avatars.Count <= id)
            avatars.Add(null);

        avatars[id] = sprite;
    }

    public Sprite GetAvatar(int id)
    {
        if (id < 0 || id >= avatars.Count)
            return null;

        return avatars[id];
    }
}
