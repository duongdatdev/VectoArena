using UnityEngine;
using Colyseus;
using System.Threading.Tasks;
using System;

using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using VectoArena.Schema;
using UnityEngine.SceneManagement;

public class NetworkManager : MonoBehaviour
{
    //singleton instance
    public static NetworkManager Instance;
    
    // Server endpoints loaded from config
    private string ServerURL => ConfigManager.Config.serverUrl;
    private string HttpURL => ConfigManager.Config.httpUrl;
    


    private Client client;
    private string authToken;

    [Header("Prefabs")]
    [SerializeField] private GameObject localPlayerPrefab;
    [SerializeField] private GameObject remotePlayerPrefab;
    [SerializeField] private GameObject playerPrefab; // fallback if separate prefabs are not assigned

    [Header("Item / Weapon Config")]
    public WeaponDatabase weaponDatabase;
    public GameObject itemMedicalKitPrefab;

    private Dictionary<string, GameObject> playerObjects = new Dictionary<string, GameObject>();
    private Dictionary<string, GameObject> itemObjects = new Dictionary<string, GameObject>();
    private Dictionary<string, ItemWeaponConfig> itemWeaponConfigs = new Dictionary<string, ItemWeaponConfig>();
    private bool isSceneLoaded = false;

    public event Action OnGameStart;
    public event Action<KillFeedMessage> OnKillFeedReceived;
    public event Action OnGameOver;
    public event Action<MatchResultMessage> OnMatchResultReceived;

    // using a generic object here for now. 
    // remember to swap this out with actual schema later.
    private Room<GameState> room;
    private bool isConnectingToBattle = false;
    private Task leaveRoomTask;
    private bool cancelMatchmakingRequested = false;

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
            CheckAndSpawnInitialItems();
            UpdateZoneState();
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
                    await PlayerInventory.LoadFromServer();
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

    public async Task<PlayerProfileResponse> LoadPlayerProfile()
    {
        return await SendPlayerRequest(HttpMethod.Get, "/player/profile", null);
    }

    public async Task<PlayerProfileResponse> BuyPlayerSkin(string skinId)
    {
        return await SendPlayerRequest(HttpMethod.Post, "/player/buy-skin", new SkinRequest { skinId = skinId });
    }

    public async Task<PlayerProfileResponse> EquipPlayerSkin(string skinId)
    {
        return await SendPlayerRequest(HttpMethod.Post, "/player/equip-skin", new SkinRequest { skinId = skinId });
    }

    public async Task<bool> LinkWallet(string walletAddress)
    {
        try
        {
            var response = await SendPlayerRequestRaw(HttpMethod.Post, "/web3/link-wallet", new { walletAddress });
            return response != null;
        }
        catch (Exception ex)
        {
            Debug.LogError("Link Wallet failed: " + ex.Message);
            return false;
        }
    }

    public async Task<bool> VerifyDeposit(string txHash)
    {
        try
        {
            var response = await SendPlayerRequestRaw(HttpMethod.Post, "/web3/deposit", new { txHash });
            if (response != null)
            {
                await PlayerInventory.LoadFromServer();
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError("Verify Deposit failed: " + ex.Message);
            return false;
        }
    }

    private async Task<string> SendPlayerRequestRaw(HttpMethod method, string path, object body)
    {
        if (string.IsNullOrEmpty(authToken))
        {
            throw new InvalidOperationException("Player is not authenticated.");
        }

        using (var httpClient = new HttpClient())
        using (var request = new HttpRequestMessage(method, $"{HttpURL}{path}"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            if (body != null)
            {
                string json = JsonConvert.SerializeObject(body);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            var response = await httpClient.SendAsync(request);
            string responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                PlayerApiError error = JsonConvert.DeserializeObject<PlayerApiError>(responseString);
                throw new InvalidOperationException(error?.error ?? response.ReasonPhrase);
            }

            return responseString;
        }
    }

    private async Task<PlayerProfileResponse> SendPlayerRequest(HttpMethod method, string path, object body)
    {
        if (string.IsNullOrEmpty(authToken))
        {
            throw new InvalidOperationException("Player is not authenticated.");
        }

        using (var httpClient = new HttpClient())
        using (var request = new HttpRequestMessage(method, $"{HttpURL}{path}"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            if (body != null)
            {
                string json = JsonConvert.SerializeObject(body);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            var response = await httpClient.SendAsync(request);
            string responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                PlayerApiError error = JsonConvert.DeserializeObject<PlayerApiError>(responseString);
                throw new InvalidOperationException(error?.error ?? response.ReasonPhrase);
            }

            return JsonConvert.DeserializeObject<PlayerProfileResponse>(responseString);
        }
    }

    public async Task ConnectAndJoinBattle()
    {
        while (isConnectingToBattle)
        {
            await Task.Delay(50);
        }

        try
        {
            isConnectingToBattle = true;
            cancelMatchmakingRequested = false;
            if (leaveRoomTask != null)
            {
                await leaveRoomTask;
                leaveRoomTask = null;
            }

            if (room != null)
            {
                await LeaveCurrentRoom();
            }

            Debug.Log("Attempting to connect to the server...");
            //reset before connecting
            hasGameStarted = false;
            playerObjects.Clear();
            itemObjects.Clear();
            itemWeaponConfigs.Clear();
            
            // Pass token to options if needed
            var options = new Dictionary<string, object>
            {
                { "accessToken", authToken }
            };
            
            var joinedRoom = await client.JoinOrCreate<GameState>("battle", options);
            if (cancelMatchmakingRequested)
            {
                await joinedRoom.Leave();
                Debug.Log("Matchmaking was cancelled before join completed.");
                return;
            }

            room = joinedRoom;
            Debug.Log("Connected to room! Session ID: " + joinedRoom.SessionId);
            


            room.OnStateChange += (state, isFirstState) =>
            {
                UpdateZoneState();
                if (state.matchState == "PLAYING")
                {
                    HandleGameStart();
                }
            };

            room.OnMessage<object>("GAME_START", (message) =>
            {
                Debug.Log("GAME_START message received");
                UpdateZoneState();
                HandleGameStart();
            });

            room.OnMessage<object>("GAME_OVER", (message) =>
            {
                Debug.Log("GAME_OVER message received");
                OnGameOver?.Invoke();
            });

            room.OnMessage<MatchResultMessage>("match_result", (message) =>
            {
                OnMatchResultReceived?.Invoke(message);
            });

            room.OnMessage<KillFeedMessage>("kill_feed", (message) =>
            {
                OnKillFeedReceived?.Invoke(message);
            });

            room.OnMessage<ShootMessage>("shoot", (message) =>
            {
                if (playerObjects.TryGetValue(message.clientId, out GameObject playerObj))
                {
                    var pc = playerObj.GetComponent<PlayerController>();
                    if (pc != null)
                    {
                        Vector3 pos = new Vector3(message.x, message.y, message.z);
                        Quaternion rot = Quaternion.Euler(message.rx, message.ry, message.rz);
                        pc.PerformShoot(pos, rot);
                    }
                }
            });

            room.OnMessage<MeleeAttackMessage>("melee_attack", (message) =>
            {
                if (message == null || string.IsNullOrEmpty(message.attackerId)) return;
                if (message.attackerId == room.SessionId) return;

                if (playerObjects.TryGetValue(message.attackerId, out GameObject playerObj))
                {
                    VectoAudioManager.PlayMelee(playerObj.transform.position, false);
                    var pc = playerObj.GetComponent<PlayerController>();
                    if (pc != null)
                    {
                        pc.TriggerAttackAnimation();
                    }
                }
            });

            room.OnMessage<ItemPickedMessage>("item_picked", (message) =>
            {
                OnItemPicked(message);
            });

            var callbacks = Colyseus.Schema.Callbacks.Get(joinedRoom);
            callbacks.OnAdd(state => state.players, (key, player) => OnPlayerJoin(key, player));
            callbacks.OnRemove(state => state.players, (key, player) => OnPlayerLeave(key, player));
            
            callbacks.OnAdd(state => state.items, (key, item) => OnItemAdd(key, item));
            callbacks.OnRemove(state => state.items, (key, item) => OnItemRemove(key, item));
        }
        catch (Exception ex)
        {
            Debug.LogError("Connection failed: " + ex.Message);
        }
        finally
        {
            isConnectingToBattle = false;
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

    private void OnItemAdd(string key, ItemState item)
    {
        if (!isSceneLoaded)
        {
            Debug.Log($"Item add arrived before GameplayScene loaded. key={key}, type={item.type}");
            return;
        }

        if (itemObjects.ContainsKey(key)) return;

        GameObject prefabToSpawn = null;
        if (item.type == "MedicalKit")
        {
            prefabToSpawn = itemMedicalKitPrefab;
        }
        else if (weaponDatabase != null)
        {
            prefabToSpawn = weaponDatabase.GetFloatingItemPrefab(item.type);
        }

        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"Item prefab for type {item.type} is not assigned in WeaponDatabase or NetworkManager!");
            return;
        }

        Vector3 spawnPos = new Vector3(item.x, item.y, item.z);
        GameObject spawnedItem = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
        spawnedItem.name = $"Item_{item.type}_{key}";

        WeaponPickup pickup = spawnedItem.GetComponent<WeaponPickup>();
        if (pickup != null)
        {
            pickup.Initialize(key);
            itemWeaponConfigs[key] = new ItemWeaponConfig
            {
                weaponModelPrefab = pickup.weaponModelPrefab,
                bulletPrefab = pickup.bulletPrefab,
                fireRate = pickup.fireRate,
                maxAmmo = pickup.maxAmmo
            };

            var callbacks = Colyseus.Schema.Callbacks.Get(room);
            callbacks.Listen(item, current => current.pickupBy, (_, __) =>
            {
                UpdateItemPickupProgressVisual(key, item);
            });
            callbacks.Listen(item, current => current.pickupProgress, (_, __) =>
            {
                UpdateItemPickupProgressVisual(key, item);
            });
        }

        itemObjects.Add(key, spawnedItem);
        UpdateItemPickupProgressVisual(key, item);
        Debug.Log($"Spawned item {item.type} at {spawnPos}");
    }

    private void OnItemRemove(string key, ItemState item)
    {
        if (itemObjects.ContainsKey(key))
        {
            Destroy(itemObjects[key]);
            itemObjects.Remove(key);
            Debug.Log($"Item removed and destroyed: {key}");
        }
    }

    private void OnItemPicked(ItemPickedMessage message)
    {
        if (message == null || string.IsNullOrEmpty(message.playerId))
        {
            return;
        }

        if (!playerObjects.TryGetValue(message.playerId, out GameObject playerObj) || playerObj == null)
        {
            return;
        }

        PlayerController playerController = playerObj.GetComponent<PlayerController>();
        if (playerController == null)
        {
            return;
        }

        if (message.itemType == "MedicalKit")
        {
            VectoAudioManager.PlayPickup(message.itemType, playerObj.transform.position, message.playerId == room?.SessionId);
            if (!string.IsNullOrEmpty(message.itemId))
            {
                itemWeaponConfigs.Remove(message.itemId);
            }
            return;
        }

        if (!TryGetItemWeaponConfig(message.itemId, message.itemType, out ItemWeaponConfig config))
        {
            Debug.LogWarning($"Missing weapon config for picked item {message.itemId} ({message.itemType}).");
            return;
        }

        if (message.fireRate > 0f && message.maxAmmo > 0)
        {
            playerController.EquipWeapon(config.weaponModelPrefab, config.bulletPrefab, message.fireRate, message.maxAmmo);
        }
        else
        {
            playerController.EquipWeapon(config.weaponModelPrefab, config.bulletPrefab, config.fireRate, config.maxAmmo);
        }
        VectoAudioManager.PlayPickup(message.itemType, playerObj.transform.position, message.playerId == room?.SessionId);
        if (!string.IsNullOrEmpty(message.itemId))
        {
            itemWeaponConfigs.Remove(message.itemId);
        }
    }

    private bool TryGetItemWeaponConfig(string itemId, string itemType, out ItemWeaponConfig config)
    {
        if (!string.IsNullOrEmpty(itemId) && itemWeaponConfigs.TryGetValue(itemId, out config))
        {
            return true;
        }

        if (weaponDatabase != null)
        {
            WeaponData data = weaponDatabase.GetWeaponData(itemType);
            if (data != null)
            {
                config = new ItemWeaponConfig
                {
                    weaponModelPrefab = data.weaponModelPrefab,
                    bulletPrefab = data.bulletPrefab,
                    fireRate = data.fireRate,
                    maxAmmo = data.maxAmmo
                };
                return true;
            }
        }

        GameObject sourcePrefab = weaponDatabase != null ? weaponDatabase.GetFloatingItemPrefab(itemType) : null;
        if (sourcePrefab != null)
        {
            WeaponPickup pickup = sourcePrefab.GetComponent<WeaponPickup>();
            if (pickup != null)
            {
                config = new ItemWeaponConfig
                {
                    weaponModelPrefab = pickup.weaponModelPrefab,
                    bulletPrefab = pickup.bulletPrefab,
                    fireRate = pickup.fireRate,
                    maxAmmo = pickup.maxAmmo
                };
                return true;
            }
        }

        config = null;
        return false;
    }

    private void UpdateItemPickupProgressVisual(string itemId, ItemState itemState)
    {
        if (itemState == null)
        {
            return;
        }

        if (!itemObjects.TryGetValue(itemId, out GameObject itemObject) || itemObject == null)
        {
            return;
        }

        WeaponPickup pickup = itemObject.GetComponent<WeaponPickup>();
        if (pickup == null)
        {
            return;
        }

        bool isActive = !string.IsNullOrEmpty(itemState.pickupBy) && itemState.pickupProgress > 0f;
        pickup.SetSyncedPickupProgress(itemState.pickupProgress, isActive);
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

    private void CheckAndSpawnInitialItems()
    {
        if (room == null)
        {
            Debug.LogWarning("CheckAndSpawnInitialItems called but room is null.");
            return;
        }

        Debug.Log($"CheckAndSpawnInitialItems: room ready, items={room.State.items.Count}, existingObjects={itemObjects.Count}");
        room.State.items.ForEach((key, item) =>
        {
            if (!itemObjects.ContainsKey(key) || itemObjects[key] == null)
            {
                if (itemObjects.ContainsKey(key))
                {
                    itemObjects.Remove(key);
                }
                OnItemAdd(key, item);
            }
        });
    }

    private void UpdateZoneState()
    {
        if (room == null || room.State == null || room.State.zone == null) return;

        var zoneController = FindAnyObjectByType<ZoneController>();
        if (zoneController == null) return;

        zoneController.SetServerAuthoritative(true);
        zoneController.ApplyState(room.State.zone);
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
        string skinId = string.IsNullOrEmpty(playerState.skinId) ? PlayerInventory.DefaultSkinId : playerState.skinId;
        Animator skinAnimator = PlayerSkinApplier.ApplySkin(playerObj, skinId);
        Debug.Log($"Instantiated playerObj {playerObj.name} from {prefabName}, active={playerObj.activeSelf}, scene={playerObj.scene.name}");
        playerObjects.Add(key, playerObj);
        
        Debug.Log(playerObj.gameObject.name);

        // track and Sync
        var sync = playerObj.GetComponent<NetworkPlayerSync>();
        sync.Initialize(playerState, key, room);
        sync.RefreshAnimator(skinAnimator);

        // setup Camera if it's the local player
        if (isLocalPlayer)
        {
            var cam = Camera.main.GetComponent<CameraFollow>();
            if (cam != null)
            {
                cam.target = playerObj.transform;
            }

            VectoAudioManager.FollowLocalPlayer(playerObj.transform);
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
    
    public Task CancelMatchmaking()
    {
        cancelMatchmakingRequested = true;
        if (leaveRoomTask == null)
        {
            leaveRoomTask = LeaveCurrentRoom();
        }

        return leaveRoomTask;
    }

    private async Task LeaveCurrentRoom()
    {
        var currentRoom = room;
        if (currentRoom != null)
        {
            room = null;
            hasGameStarted = false;
            try
            {
                await currentRoom.Leave();
                Debug.Log("Cancelled matchmaking.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Failed to leave matchmaking room: " + ex.Message);
            }
        }

        if (leaveRoomTask != null && leaveRoomTask.IsCompleted)
        {
            leaveRoomTask = null;
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

    [Serializable]
    private class SkinRequest
    {
        public string skinId;
    }

    [Serializable]
    private class PlayerApiError
    {
        public string error;
    }

    [Serializable]
    public class PlayerProfileResponse
    {
        public string username;
        public int vecBalance;
        public int coinBalance;
        public int level;
        public int xp;
        public int xpToNextLevel;
        public float xpProgress;
        public int levelsGained;
        public string equippedPlayerSkin;
        public string[] ownedSkins;
        public ShopSkinResponse[] shopSkins;
    }

    [Serializable]
    public class MatchResultMessage
    {
        public int placement;
        public int kills;
        public int xpEarned;
        public int level;
        public int xp;
        public int xpToNextLevel;
        public float xpProgress;
        public int levelsGained;
        public bool isWinner;
    }

    [Serializable]
    public class ShopSkinResponse
    {
        public string id;
        public string displayName;
        public int price;
        public bool owned;
        public bool equipped;
    }

    [Serializable]
    public class ShootMessage
    {
        public string clientId;
        public float x;
        public float y;
        public float z;
        public float rx;
        public float ry;
        public float rz;
    }

    [Serializable]
    public class ItemPickedMessage
    {
        public string playerId;
        public string itemId;
        public string itemType;
        public float fireRate;
        public int maxAmmo;
    }

    [Serializable]
    public class MeleeAttackMessage
    {
        public string attackerId;
        public string targetId;
    }

    [Serializable]
    public class KillFeedMessage
    {
        public string killerName;
        public string victimName;
        public string weapon;
    }

    private class ItemWeaponConfig
    {
        public GameObject weaponModelPrefab;
        public GameObject bulletPrefab;
        public float fireRate;
        public int maxAmmo;
    }
}
