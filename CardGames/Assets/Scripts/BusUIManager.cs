using UnityEngine;

public class BusUIManager : MonoBehaviour
{
    public static BusUIManager Instance;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
