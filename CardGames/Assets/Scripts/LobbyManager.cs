using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using System.Collections;


public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance;

    [Header("UI")]
    public Button hostButton;
    public Button joinButton;
    public Button startGameButton;
    public InputField joinCodeField;
    public Text joinCodeDisplay;
    public GameObject playerSetupRoot;

    [Header("Player List UI")]
    public Transform playerListParent;
    public GameObject playerListRoot;
    public GameObject playerRowPrefab;

    private List<ulong> connectedPlayers = new List<ulong>();

    [Header("Name")]
    public InputField nameInputField;

    [Header("Join Code UI")]
    public GameObject joinCodeInputRoot;   // parent of InputField
    public GameObject joinCodeDisplayRoot; // parent of Text
    public GameObject startGameButtonRoot;
    public GameObject hostGameButtonRoot;
    public GameObject clientGameButtonRoot;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        hostButton.interactable = false;
        joinButton.interactable = false;
        startGameButton.interactable = false;
        startGameButtonRoot.SetActive(false);

        StartCoroutine(WaitForServices());

        hostButton.onClick.AddListener(StartHost);
        joinButton.onClick.AddListener(JoinGame);
        startGameButton.onClick.AddListener(StartGame);

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        joinCodeInputRoot.SetActive(true);
        joinCodeDisplayRoot.SetActive(false);
    }
    IEnumerator WaitForServices()
    {
        while (UnityServicesManager.Instance == null ||
               !UnityServicesManager.Instance.IsInitialized)
        {
            yield return null;
        }

        hostButton.interactable = true;
        joinButton.interactable = true;

        Debug.Log("Lobby ready on device");
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    void OnClientConnected(ulong clientId)
    {
        if (!connectedPlayers.Contains(clientId))
            connectedPlayers.Add(clientId);

        
        startGameButton.interactable = NetworkManager.Singleton.IsServer && connectedPlayers.Count > 0;

        startGameButton.interactable =
        NetworkManager.Singleton.IsServer &&
        NetworkManager.Singleton.ConnectedClients.Count > 0;

        UpdatePlayerList();
    }

    void OnClientDisconnected(ulong clientId)
    {
        connectedPlayers.Remove(clientId);
        UpdatePlayerList();
    }

    void UpdatePlayerList()
    {
        foreach (Transform child in playerListParent)
            Destroy(child.gameObject);

        foreach (var player in FindObjectsOfType<Player>())
        {
            GameObject row = Instantiate(playerRowPrefab, playerListParent);

            Image avatarImage = row.transform.Find("AvatarImage").GetComponent<Image>();
            Text nameText = row.transform.Find("PlayerNameText").GetComponent<Text>();

            nameText.text = player.PlayerName.Value.ToString();

            avatarImage.sprite = player.GetNetworkAvatar();
        }
    }


    public async void StartHost()
    {
        if (!UnityServicesManager.Instance.IsInitialized)
        {
            Debug.LogError("Services not initialized yet!");
            return;
        }

        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(4);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));

        NetworkManager.Singleton.StartHost();

        ChooseName();

        hostButton.interactable = false;
        joinButton.interactable = false;

        // UI state for host
        joinCodeInputRoot.SetActive(false);
        joinCodeDisplayRoot.SetActive(true);
        startGameButtonRoot.SetActive(true);
        hostGameButtonRoot.SetActive(false);
        clientGameButtonRoot.SetActive(false);

        joinCodeDisplay.text = $"{joinCode}";
    }

    public async void JoinGame()
    {
        if (!UnityServicesManager.Instance.IsInitialized)
        {
            Debug.LogError("Services not initialized yet!");
            return;
        }

        string joinCode = joinCodeField.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(joinCode))
        {
            Debug.LogError("Join code empty");
            return;
        }

        JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));

        NetworkManager.Singleton.StartClient();

        ChooseName();

        hostButton.interactable = false;
        joinButton.interactable = false;

        // UI state for client
        joinCodeInputRoot.SetActive(false);
        joinCodeDisplayRoot.SetActive(true);
        hostGameButtonRoot.SetActive(false);
        clientGameButtonRoot.SetActive(false);

        joinCodeDisplay.text = $"{joinCode}";

    }

    public void ChooseName()
    {
        string chosenName = nameInputField.text.Trim();

        if (string.IsNullOrEmpty(chosenName))
            chosenName = $"Player {Random.Range(1000, 9999)}";

        if (chosenName.Length > 16)
            chosenName = chosenName.Substring(0, 16);

        //PlayerPrefs.SetString("PlayerName", chosenName);
        //PlayerPrefs.Save();

        playerSetupRoot.SetActive(false);
        playerListRoot.SetActive(true);

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
