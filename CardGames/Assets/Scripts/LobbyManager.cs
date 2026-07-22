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
using Unity.Collections;


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

    [Header("Flow Buttons")]
    public Button createRoomButton;
    public Button joinRoomButton;
    public Button continueButton;
    public Button joinContinueButton;
    public Button backToRoomSelectionButton;
    public Button ClientBackToPlayerSetupButton;

    [Header("Hard Back Button")]
    public Button reloadLobbyButton;

    [Header("Player Setup UI")]
    public GameObject playerSetupButtonsRoot; // name + avatar + continue
    public GameObject joinCodeConfirmButton;  // button that calls JoinGame

    [Header("Scene Selection")]
    public Dropdown sceneDropdown;
    private NetworkVariable<FixedString32Bytes> selectedScene =
    new NetworkVariable<FixedString32Bytes>(
        "BluffGame",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

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

        StartCoroutine(WaitForServices());

        createRoomButton.onClick.AddListener(OnCreateRoomSelected);
        joinRoomButton.onClick.AddListener(OnJoinRoomSelected);
        continueButton.onClick.AddListener(StartHost);
        joinContinueButton.onClick.AddListener(OnJoinContinuePressed);
        backToRoomSelectionButton.onClick.AddListener(OnBackToRoomSelectionSelected);
        ClientBackToPlayerSetupButton.onClick.AddListener(OnClientBackToPlayerSetupSelected);
        reloadLobbyButton.onClick.AddListener(ReloadLobbyScene);
        joinCodeConfirmButton.GetComponent<Button>().onClick.AddListener(JoinGame);

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        // INITIAL UI STATE
        playerSetupRoot.SetActive(false);
        joinCodeInputRoot.SetActive(false);
        joinCodeConfirmButton.SetActive(false);
        joinContinueButton.gameObject.SetActive(false);
        playerListRoot.SetActive(false);
        reloadLobbyButton.gameObject.SetActive(false);
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

    void OnCreateRoomSelected()
    {
        createRoomButton.gameObject.SetActive(false);
        joinRoomButton.gameObject.SetActive(false);
        playerSetupRoot.SetActive(true);
        joinCodeInputRoot.SetActive(false);
        joinCodeConfirmButton.SetActive(false);

        continueButton.gameObject.SetActive(true);
        backToRoomSelectionButton.gameObject.SetActive(true);
    }

    void OnJoinRoomSelected()
    {
        createRoomButton.gameObject.SetActive(false);
        joinRoomButton.gameObject.SetActive(false);

        playerSetupRoot.SetActive(true);

        // Join step 1
        joinCodeInputRoot.SetActive(false);
        joinCodeConfirmButton.SetActive(false);

        continueButton.gameObject.SetActive(false);
        joinContinueButton.gameObject.SetActive(true);
        backToRoomSelectionButton.gameObject.SetActive(true);
    }

    void OnBackToRoomSelectionSelected()
    {
        createRoomButton.gameObject.SetActive(true);
        joinRoomButton.gameObject.SetActive(true);

        playerSetupRoot.SetActive(false);
        continueButton.gameObject.SetActive(false);
        joinContinueButton.gameObject.SetActive(false);

        backToRoomSelectionButton.gameObject.SetActive(false);
    }
    void OnClientBackToPlayerSetupSelected()
    {
        OnJoinRoomSelected();

        ClientBackToPlayerSetupButton.gameObject.SetActive(false);
    }

    public void ReloadLobbyScene()
    {
        Debug.Log("Reloading lobby scene");

        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void OnJoinContinuePressed()
    {
        // Join step 2
        joinCodeInputRoot.SetActive(true);
        joinCodeConfirmButton.SetActive(true);

        joinContinueButton.gameObject.SetActive(false);
        playerSetupRoot.SetActive(false);

        backToRoomSelectionButton.gameObject.SetActive(false);
        ClientBackToPlayerSetupButton.gameObject.SetActive(true);
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

            Image avatarImage = row.transform.Find("AvatarBorder").Find("AvatarRoot").Find("AvatarImage").GetComponent<Image>();
            Text nameText = row.transform.Find("PlayerNameText").GetComponent<Text>();

            nameText.text = player.PlayerName.Value.ToString();

            avatarImage.sprite = player.GetNetworkAvatar();
        }
    }

    public void RequestPlayerListRefresh()
    {
        StopAllCoroutines();
        StartCoroutine(DelayedPlayerListRefresh());
    }

    private IEnumerator DelayedPlayerListRefresh()
    {
        // Wait one frame to ensure NetworkObjects are ready
        yield return null;

        UpdatePlayerList();
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

        sceneDropdown.gameObject.SetActive(true);
        sceneDropdown.onValueChanged.AddListener(OnSceneDropdownChanged);

        // Initialize with current value
        OnSceneDropdownChanged(sceneDropdown.value);

        hostButton.interactable = false;
        joinButton.interactable = false;

        // UI state for host
        joinCodeInputRoot.SetActive(false);
        joinCodeDisplayRoot.SetActive(true);
        startGameButtonRoot.SetActive(true);
        hostGameButtonRoot.SetActive(false);
        clientGameButtonRoot.SetActive(false);

        joinCodeDisplay.text = $"{joinCode}";

        continueButton.gameObject.SetActive(false);
        backToRoomSelectionButton.gameObject.SetActive(false);
        reloadLobbyButton.gameObject.SetActive(true);

        UpdatePlayerList();
    }

    void OnSceneDropdownChanged(int index)
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        string sceneName = sceneDropdown.options[index].text;
        selectedScene.Value = sceneName;

        Debug.Log($"Selected scene: {sceneName}");
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
        sceneDropdown.gameObject.SetActive(false);

        joinCodeDisplay.text = $"{joinCode}";

        joinContinueButton.gameObject.SetActive(false);
        ClientBackToPlayerSetupButton.gameObject.SetActive(false);
        reloadLobbyButton.gameObject.SetActive(true);

        UpdatePlayerList();
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

        playerSetupRoot.SetActive(false);
        playerListRoot.SetActive(true);

        Debug.Log($"Saved player name: {chosenName}");
    }

    public void StartGame()
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        string sceneToLoad = selectedScene.Value.ToString();

        Debug.Log($"Loading scene: {sceneToLoad}");

        NetworkManager.Singleton.SceneManager.LoadScene(
            sceneToLoad,
            LoadSceneMode.Single
        );
    }
}
