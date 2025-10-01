#if UNITY_SERVER
using SQLite;
using UnityEngine;
using System.IO;

namespace Server.Database
{
    public class DatabaseService : MonoBehaviour
    {
        public static DatabaseService Instance { get; private set; }
        private SQLiteConnection _connection;
        
        public UserRepository Users { get; private set; }
        public RoomRepository Rooms { get; private set; }
        public MatchRepository Matches { get; private set; }

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

        public void Initialize()
        {
            string dbPath = Path.Combine(Application.persistentDataPath, "game.db");
            _connection = new SQLiteConnection(dbPath);
            Debug.Log($"[SERVER] Database connected: {dbPath}");
            
            CreateTables();
            InitRepositories();
        }

        void CreateTables()
        {
            // SQLite-net tự động tạo tables từ attributes
            _connection.CreateTable<User>();
            _connection.CreateTable<Room>();
            _connection.CreateTable<RoomPlayer>();
            _connection.CreateTable<Match>();
            _connection.CreateTable<MatchScore>();
            
            Debug.Log("[SERVER] Database tables created");
        }
        
        void InitRepositories()
        {
            Users = new UserRepository(_connection);
            Rooms = new RoomRepository(_connection);
            Matches = new MatchRepository(_connection);
        }

        void OnApplicationQuit()
        {
            _connection?.Close();
        }
        
        public SQLiteConnection GetConnection() => _connection;
    }
}
#endif