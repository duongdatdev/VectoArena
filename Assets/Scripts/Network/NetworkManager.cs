using UnityEngine;
using Colyseus;
using System.Threading.Tasks;
using System;

using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using VectoArena.Schema;
using UnityEngine.SceneManagement;

public class NetworkManager : MonoBehaviour
{
    //singleton instance
    public static NetworkManager Instance;
    
    //local Node.js server endpoint. Change this before pushing to production.
    private const string ServerURL = "ws://localhost:2567";
    private const string HttpURL = "http://localhost:2567";
    


    private Client client;
    private string authToken;

    [Header("Prefabs")]
    [SerializeField] private GameObject localPlayerPrefab;
    [SerializeField] private GameObject remotePlayerPrefab;
    [SerializeField] private GameObject playerPrefab; // fallback if separate prefabs are not assigned

    private Dictionary<string, GameObject> playerObjects = new Dictionary<string, GameObject>();
    private bool isSceneLoaded = false;

    public event Action OnGameStart;

    // using a generic object here for now. 
    // remember to swap this out with actual schema later.
    private Room<GameState> room;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded; 
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        isSceneLoaded = (scene.name == "GameplayScene");
        Debug.Log($"OnSceneLoaded: {scene.name}, mode={mode}, isSceneLoaded={isSceneLoaded}");
        if (isSceneLoaded)
        {
            Debug.Log($"GameplayScene loaded. room={(room != null ? "ready" : "null")}, players={(room != null ? room.State.players.Count.ToString() : "-")}");
            CheckAndSpawnInitialPlayers();
        }
    }

    void Start()
    {
        client = new Client(ServerURL);
    }

    public async Task<bool> Login(string username, string password)
    {
        using (var httpClient = new HttpClient())
        {
            var loginData = new { username = username, password = password };
            var json = JsonConvert.SerializeObject(loginData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.PostAsync($"{HttpURL}/auth/login", content);
                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<LoginResponse>(responseString);
                    authToken = result.token;
                    Debug.Log("Login successful! Token: " + authToken);
                    return true;
                }
                else
                {
                    Debug.LogError("Login failed: " + response.ReasonPhrase);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Login error: " + ex.Message);
                return false;
            }
        }
    }

    public async Task<bool> Register(string username, string password)
    {
        using (var httpClient = new HttpClient())
        {
            var registerData = new { username = username, password = password };
            var json = JsonConvert.SerializeObject(registerData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.PostAsync($"{HttpURL}/auth/register", content);
                if (response.IsSuccessStatusCode)
                {
                    Debug.Log("Registration successful!");
                    return true;
                }
                else
                {
                    Debug.LogError("Registration failed: " + response.ReasonPhrase);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Registration error: " + ex.Message);
                return false;
            }
        }
    }

    public async Task ConnectAndJoinBattle()
    {
        try
        {
            Debug.Log("Attempting to connect to the server...");
            //reset before connecting
            hasGameStarted = false;
            playerObjects.Clear();
            
            // Pass token to options if needed
            var options = new Dictionary<string, object> { { "accessToken", authToken } };
            
            room = await client.JoinOrCreate<GameState>("battle", options);
            Debug.Log("Connected to room! Session ID: " + room.SessionId);
            


            room.OnStateChange += (state, isFirstState) =>
            {
                if (state.matchState == "PLAYING")
                {
                    HandleGameStart();
                }
            };

            room.OnMessage<object>("GAME_START", (message) =>
            {
                Debug.Log("GAME_START message received");
                HandleGameStart();
            });

            var callbacks = Colyseus.Schema.Callbacks.Get(room);
            callbacks.OnAdd(state => state.players, (key, player) => OnPlayerJoin(key, player));
            callbacks.OnRemove(state => state.players, (key, player) => OnPlayerLeave(key, player));
        }
        catch (Exception ex)
        {
            Debug.LogError("Connection failed: " + ex.Message);
        }
    }

    private void OnPlayerJoin(string key, PlayerState player)
    {
        Debug.Log($"Player joined schema: {player.username}, key={key}, isSceneLoaded={isSceneLoaded}, roomSessionId={room?.SessionId}");
        if (isSceneLoaded)
        {
            SpawnPlayer(key, player);
        }
        else
        {
            Debug.Log("Player join arrived before GameplayScene loaded. Will spawn after scene load.");
        }
    }

    private void OnPlayerLeave(string key, PlayerState player)
    {
        if (playerObjects.ContainsKey(key))
        {
            Destroy(playerObjects[key]);
            playerObjects.Remove(key);
            Debug.Log("Player left and object destroyed: " + key);
        }
    }

    private void CheckAndSpawnInitialPlayers()
    {
        if (room == null)
        {
            Debug.LogWarning("CheckAndSpawnInitialPlayers called but room is null.");
            return;
        }

        Debug.Log($"CheckAndSpawnInitialPlayers: room ready, players={room.State.players.Count}, existingObjects={playerObjects.Count}");
        room.State.players.ForEach((key, player) =>
        {
            if (!playerObjects.ContainsKey(key) || playerObjects[key] == null)
            {
                if (playerObjects.ContainsKey(key))
                {
                    playerObjects.Remove(key);
                }
                SpawnPlayer(key, player);
            }
        });
    }

    private void SpawnPlayer(string key, PlayerState playerState)
    {
        if (playerObjects.ContainsKey(key) && playerObjects[key] != null) return;
        if (playerObjects.ContainsKey(key)) playerObjects.Remove(key);

        bool isLocalPlayer = (key == room.SessionId);
        Debug.Log($"SpawnPlayer called for {playerState.username} key={key} pos=({playerState.x}, {playerState.y}, {playerState.z}) rot={playerState.rotation} isLocal={isLocalPlayer}");

        GameObject pPrefab = isLocalPlayer ? localPlayerPrefab : remotePlayerPrefab;
        string prefabName = isLocalPlayer ? "localPlayerPrefab" : "remotePlayerPrefab";

        if (pPrefab == null)
        {
            pPrefab = playerPrefab;
            prefabName = "playerPrefab (fallback)";
        }

        if (pPrefab == null)
        {
            Debug.LogWarning("No player prefab assigned! Using emergency placeholder.");
            pPrefab = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(pPrefab.GetComponent<CapsuleCollider>());
            prefabName = "capsulePlaceholder";
        }

        Vector3 spawnPos = new Vector3(playerState.x, playerState.y, playerState.z);
        GameObject playerObj = Instantiate(pPrefab, spawnPos, Quaternion.Euler(0, playerState.rotation, 0));
        playerObj.name = "Player_" + playerState.username;
        Debug.Log($"Instantiated playerObj {playerObj.name} from {prefabName}, active={playerObj.activeSelf}, scene={playerObj.scene.name}");
        playerObjects.Add(key, playerObj);
        
        Debug.Log(playerObj.gameObject.name);

        // track and Sync
        var sync = playerObj.GetComponent<NetworkPlayerSync>();
        sync.Initialize(playerState, key, room);

        // setup Camera if it's the local player
        if (isLocalPlayer)
        {
            var cam = Camera.main.GetComponent<CameraFollow>();
            if (cam != null)
            {
                cam.target = playerObj.transform;
            }
        }
    }

    private bool hasGameStarted = false;

    private void HandleGameStart()
    {
        if (hasGameStarted) return;
        hasGameStarted = true;
        
        Debug.Log("Game Started! Triggering events.");
        OnGameStart?.Invoke();
    }
    
    public void CancelMatchmaking()
    {
        if (room != null)
        {
            _ = room.Leave();
            room = null;
            hasGameStarted = false;
            Debug.Log("Cancelled matchmaking.");
        }
    }

    void OnDestroy()
    {
        if (room != null)
        {
           _= room.Leave();
        }
    }

    [Serializable]
    public class LoginResponse
    {
        public string token;
    }
}