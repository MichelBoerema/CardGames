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

    [Header("Name")]
    public InputField nameInputField;

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

        string savedName = PlayerPrefs.GetString("PlayerName", "");
        nameInputField.text = savedName;

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

        foreach (var player in FindObjectsOfType<Player>())
        {
            string name = player.PlayerName.Value.ToString();
            playerListText.text += $"{name}\n";
        }
    }

    public void StartHost()
    {
        ChooseName();

        var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        transport.ConnectionData.Port = 7777;
        NetworkManager.Singleton.StartHost();
    }

    public void JoinGame()
    {
        ChooseName();

        var transport = NetworkManager.Singleton
            .GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();

        transport.ConnectionData.Address = joinIPField.text.Trim();
        transport.ConnectionData.Port = 7777;

        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("Already connected or hosting");
            return;
        }

        NetworkManager.Singleton.StartClient();
    }


    public void ChooseName()
    {
        string chosenName = nameInputField.text.Trim();

        if (string.IsNullOrEmpty(chosenName))
            chosenName = $"Player {Random.Range(1000, 9999)}";

        if (chosenName.Length > 16)
            chosenName = chosenName.Substring(0, 16);

        PlayerPrefs.SetString("PlayerName", chosenName);
        PlayerPrefs.Save();

        Debug.Log($"Saved player name: {chosenName}");
    }

    public void StartGame()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            // Switch to Game Scene for all clients
            NetworkManager.Singleton.SceneManager.LoadScene("BluffGame", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}
