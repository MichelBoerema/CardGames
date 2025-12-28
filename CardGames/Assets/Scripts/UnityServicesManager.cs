using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;

public class UnityServicesManager : MonoBehaviour
{
    public static UnityServicesManager Instance { get; private set; }
    public bool IsInitialized { get; private set; }

    private static Task initTask;

    async void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        await InitializeServicesOnce();
    }

    private async Task InitializeServicesOnce()
    {
        if (IsInitialized)
            return;

        if (initTask != null)
        {
            await initTask;
            return;
        }

        initTask = InitializeServicesInternal();
        await initTask;
    }

    private async Task InitializeServicesInternal()
    {
        await UnityServices.InitializeAsync();

        // SAFE authentication handling
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            try
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            catch (AuthenticationException)
            {
                // Another sign-in is already in progress wait for it
                await WaitForSignIn();
            }
        }

        IsInitialized = true;
        Debug.Log("Unity Services initialized safely");
    }

    private async Task WaitForSignIn()
    {
        while (!AuthenticationService.Instance.IsSignedIn)
            await Task.Delay(100);
    }
}
