using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class AvatarDatabase : NetworkBehaviour
{
    public static AvatarDatabase Instance { get; private set; }

    // Local list of sprites; synced using NetworkList of IDs if needed
    [SerializeField] private readonly List<Sprite> avatars = new List<Sprite>();

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

    /// <summary>
    /// Adds an avatar on the server and returns its ID
    /// </summary>
    public int AddAvatar(Sprite sprite)
    {
        if (!IsServer)
            throw new System.Exception("Only the server can add avatars");

        avatars.Add(sprite);
        return avatars.Count - 1;
    }

    /// <summary>
    /// Adds an avatar at a specific ID (for clients to mirror server data)
    /// </summary>
    public void AddAvatar(Sprite sprite, int id)
    {
        while (avatars.Count <= id)
            avatars.Add(null);

        avatars[id] = sprite;
    }

    /// <summary>
    /// Get an avatar by ID
    /// </summary>
    public Sprite GetAvatar(int id)
    {
        if (id < 0 || id >= avatars.Count)
            return null;

        return avatars[id];
    }
}
