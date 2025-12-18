using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance;

    [Header("UI")]
    public Button hostButton;
    public Button joinButton;
    public Button startGameButton;
    public InputField joinIPField;
    public Text playerListText;

    private List<ulong> connectedPlayers = new List<ulong>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        hostButton.onClick.AddListener(StartHost);
        joinButton.onClick.AddListener(JoinGame);
        startGameButton.onClick.AddListener(StartGame);
        startGameButton.interactable = false;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    void OnClientConnected(ulong clientId)
    {
        if (!connectedPlayers.Contains(clientId))
            connectedPlayers.Add(clientId);

        UpdatePlayerList();
        startGameButton.interactable = NetworkManager.Singleton.IsServer && connectedPlayers.Count > 0;
    }

    void OnClientDisconnected(ulong clientId)
    {
        connectedPlayers.Remove(clientId);
        UpdatePlayerList();
    }

    void UpdatePlayerList()
    {
        playerListText.text = "Players:\n";
        foreach (var id in connectedPlayers)
        {
            playerListText.text += $"Player {id}\n";
        }
    }

    public void StartHost()
    {
        var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        transport.ConnectionData.Port = (ushort)Random.Range(10000, 60000);
        NetworkManager.Singleton.StartHost();
    }

    public void JoinGame()
    {
        string ip = joinIPField.text;

        var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        transport.ConnectionData.Address = /*ip*/"127.0.0.1";
        transport.ConnectionData.Port = 7777;

        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("Host not ready yet!");
            return;
        }
        NetworkManager.Singleton.StartClient();
    }

    public void StartGame()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            // Switch to Game Scene for all clients
            NetworkManager.Singleton.SceneManager.LoadScene("GameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}
