#if UNITY_SERVER
using UnityEngine;
using Server.Database;

public class ServerBootstrap : MonoBehaviour
{
    void Start()
    {
        Debug.Log("[SERVER] ========== SERVER STARTING ==========");
        
        // Create DatabaseService
        GameObject dbObject = new GameObject("DatabaseService");
        var dbService = dbObject.AddComponent<DatabaseService>();
        dbService.Initialize();
        
        Debug.Log("[SERVER] ========== SERVER READY ==========");
    }
}
#endif