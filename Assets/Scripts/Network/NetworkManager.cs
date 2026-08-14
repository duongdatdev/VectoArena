using UnityEngine;
using Colyseus;
using System.Threading.Tasks;
using System;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using VectoArena.Schema;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.Networking;

public class NetworkManager : MonoBehaviour
{
    //singleton instance
    public static NetworkManager Instance;
    
    // Server endpoints loaded from config
    private string ServerURL => ConfigManager.Config.serverUrl;
    private string HttpURL => ConfigManager.Config.httpUrl;
    


    private Client client;
    private string authToken;
    public string LastErrorMessage { get; private set; }

    private sealed class HttpResult
    {
        public long StatusCode;
        public string Body;
        public string Error;

        public bool IsSuccessStatusCode => StatusCode >= 200 && StatusCode < 300;
    }

#if !UNITY_WEBGL || UNITY_EDITOR
    private static readonly HttpClient SharedHttpClient = CreateSharedHttpClient();

    private static HttpClient CreateSharedHttpClient()
    {
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return httpClient;
    }
#endif

    private static async Task<HttpResult> SendHttpRequestAsync(
        HttpMethod method,
        string url,
        string json = null,
        string bearerToken = null)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        using (var request = new UnityWebRequest(url, method.Method))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 30;
            request.SetRequestHeader("Accept", "application/json");

            if (json != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json");
            }

            if (!string.IsNullOrEmpty(bearerToken))
            {
                request.SetRequestHeader("Authorization", "Bearer " + bearerToken);
            }

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            if (!operation.isDone)
            {
                var completionSource = new TaskCompletionSource<bool>();
                operation.completed += _ => completionSource.TrySetResult(true);
                await completionSource.Task;
            }

            return new HttpResult
            {
                StatusCode = request.responseCode,
                Body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty,
                Error = request.error
            };
        }
#else
        using (var request = new HttpRequestMessage(method, url))
        {
            if (!string.IsNullOrEmpty(bearerToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            }

            if (json != null)
            {
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            using (HttpResponseMessage response = await SharedHttpClient.SendAsync(request))
            {
                return new HttpResult
                {
                    StatusCode = (long)response.StatusCode,
                    Body = await response.Content.ReadAsStringAsync(),
                    Error = response.ReasonPhrase
                };
            }
        }
#endif
    }

    [Header("Prefabs")]
    [SerializeField] private GameObject localPlayerPrefab;
    [SerializeField] private GameObject remotePlayerPrefab;
    [SerializeField] private GameObject playerPrefab; // fallback if separate prefabs are not assigned

    [Header("Item / Weapon Config")]
    public WeaponDatabase weaponDatabase;
    public GameObject itemMedicalKitPrefab;
    public GameObject itemVecPrefab;

    private Dictionary<string, GameObject> playerObjects = new Dictionary<string, GameObject>();
    private Dictionary<string, GameObject> itemObjects = new Dictionary<string, GameObject>();

    // Public accessor so combat feedback (damage numbers) can locate a player by sessionId.
    public bool TryGetPlayerObject(string sessionId, out GameObject playerObject)
    {
        if (!string.IsNullOrEmpty(sessionId))
        {
            return playerObjects.TryGetValue(sessionId, out playerObject);
        }

        playerObject = null;
        return false;
    }

    private Dictionary<string, ItemWeaponConfig> itemWeaponConfigs = new Dictionary<string, ItemWeaponConfig>();
    private Dictionary<string, Action> playerSchemaUnsubs = new Dictionary<string, Action>();
    private bool isSceneLoaded = false;

    public event Action OnGameStart;
    public event Action<string> OnConnectionFailed;
    public event Action<KillFeedMessage> OnKillFeedReceived;
    public event Action OnGameOver;
    public event Action<MatchResultMessage> OnMatchResultReceived;

    public bool IsGameplayInputBlocked { get; private set; }

    // Fired on every client when any player takes damage (for floating damage numbers).
    public static event Action<DamageTakenMessage> OnDamageTaken;

    // using a generic object here for now. 
    // remember to swap this out with actual schema later.
    private Room<GameState> room;
    private bool isConnectingToBattle = false;
    private Task leaveRoomTask;
    private bool cancelMatchmakingRequested = false;
    private bool returningToMenuAfterDisconnect = false;
    private VisualElement connectionOverlay;
    private Label connectionStatusLabel;
    private readonly ConcurrentQueue<RoomConnectionEvent> roomConnectionEvents = new ConcurrentQueue<RoomConnectionEvent>();

    private enum RoomConnectionEventType
    {
        Dropped,
        Reconnected,
        Failed
    }

    private sealed class RoomConnectionEvent
    {
        public Room<GameState> SourceRoom;
        public RoomConnectionEventType Type;
        public int Code;
    }

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
        BindConnectionOverlay();
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

    private void Update()
    {
        while (roomConnectionEvents.TryDequeue(out RoomConnectionEvent connectionEvent))
        {
            HandleRoomConnectionEvent(connectionEvent);
        }
    }

    public async Task<bool> Login(string username, string password)
    {
        var loginData = new { username = username, password = password };
        var json = JsonConvert.SerializeObject(loginData);

        try
        {
            LastErrorMessage = null;
            HttpResult response = await SendHttpRequestAsync(
                HttpMethod.Post,
                $"{HttpURL}/auth/login",
                json);
            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<LoginResponse>(response.Body);
                authToken = result.token;
                await PlayerInventory.LoadFromServer();
                // Debug.Log("Login successful! Token: " + authToken);
                return true;
            }

            LastErrorMessage = ExtractApiError(
                response.Body,
                response.Error,
                (int)response.StatusCode);
            Debug.LogError("Login failed: " + LastErrorMessage);
            return false;
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            Debug.LogError("Login error: " + ex.Message);
            return false;
        }
    }

    public async Task<bool> Register(string username, string password)
    {
        var registerData = new { username = username, password = password };
        var json = JsonConvert.SerializeObject(registerData);

        try
        {
            HttpResult response = await SendHttpRequestAsync(
                HttpMethod.Post,
                $"{HttpURL}/auth/register",
                json);
            if (response.IsSuccessStatusCode)
            {
                Debug.Log("Registration successful!");
                return true;
            }

            Debug.LogError("Registration failed: " + response.Error);
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError("Registration error: " + ex.Message);
            return false;
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

    public async Task<TransactionHistoryResponse> LoadTransactions(string currencyType = "VEC", string type = null, int limit = 20, int offset = 0)
    {
        var queryParts = new List<string>();

        if (!string.IsNullOrEmpty(currencyType))
        {
            queryParts.Add("currencyType=" + Uri.EscapeDataString(currencyType));
        }

        if (!string.IsNullOrEmpty(type))
        {
            queryParts.Add("type=" + Uri.EscapeDataString(type));
        }

        queryParts.Add("limit=" + Mathf.Clamp(limit, 1, 100));
        queryParts.Add("offset=" + Mathf.Max(0, offset));

        string path = "/player/transactions?" + string.Join("&", queryParts);
        string response = await SendPlayerRequestRaw(HttpMethod.Get, path, null);
        return JsonConvert.DeserializeObject<TransactionHistoryResponse>(response);
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

    public async Task<WalletNonceResponse> GetWalletNonceAsync()
    {
        string response = await SendPlayerRequestRaw(HttpMethod.Get, "/wallet/nonce", null);
        return JsonConvert.DeserializeObject<WalletNonceResponse>(response);
    }

    public async Task<WalletVerifyResponse> VerifyWalletAsync(string address, string signature, string nonce)
    {
        string response = await SendPlayerRequestRaw(HttpMethod.Post, "/wallet/verify", new { address, signature, nonce });
        return JsonConvert.DeserializeObject<WalletVerifyResponse>(response);
    }

    public async Task<NftSyncResponse> SyncNftOwnershipAsync()
    {
        string response = await SendPlayerRequestRaw(HttpMethod.Post, "/nft/sync", new { });
        return JsonConvert.DeserializeObject<NftSyncResponse>(response);
    }

    public async Task<NftPurchaseConfirmResponse> ConfirmNftPurchaseAsync(string skinId, string txHash)
    {
        string response = await SendPlayerRequestRaw(HttpMethod.Post, "/nft/purchase/confirm", new { skinId, txHash });
        NftPurchaseConfirmResponse confirmation = JsonConvert.DeserializeObject<NftPurchaseConfirmResponse>(response);
        if (confirmation == null || confirmation.nftSkin == null || !confirmation.nftSkin.owned)
        {
            throw new InvalidOperationException("NFT purchase is pending backend confirmation.");
        }

        return confirmation;
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

        string json = body != null ? JsonConvert.SerializeObject(body) : null;
        HttpResult response = await SendHttpRequestAsync(
            method,
            $"{HttpURL}{path}",
            json,
            authToken);

        if (!response.IsSuccessStatusCode)
        {
            PlayerApiError error = JsonConvert.DeserializeObject<PlayerApiError>(response.Body);
            throw new InvalidOperationException(BuildApiErrorMessage(error, response.Error));
        }

        return response.Body;
    }

    private async Task<PlayerProfileResponse> SendPlayerRequest(HttpMethod method, string path, object body)
    {
        string responseString = await SendPlayerRequestRaw(method, path, body);
        return JsonConvert.DeserializeObject<PlayerProfileResponse>(responseString);
    }

    public Task ConnectAndJoinBattle()
    {
        return ConnectAndJoinBattle(false);
    }

    public Task ConnectAndJoinAirdrop()
    {
        return ConnectAndJoinBattle(true);
    }

    private async Task ConnectAndJoinBattle(bool playToAirdrop)
    {
        while (isConnectingToBattle)
        {
            await Task.Delay(50);
        }

        try
        {
            isConnectingToBattle = true;
            LastErrorMessage = null;
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
            
            var roomName = playToAirdrop ? "airdrop" : "battle";
            var joinedRoom = await client.JoinOrCreate<GameState>(roomName, options);
            if (cancelMatchmakingRequested)
            {
                await joinedRoom.Leave();
                Debug.Log("Matchmaking was cancelled before join completed.");
                return;
            }

            room = joinedRoom;
            ConfigureRoomReconnection(joinedRoom);
            Debug.Log("Connected to room! Session ID: " + joinedRoom.SessionId);
            


            joinedRoom.OnStateChange += (state, isFirstState) =>
            {
                if (!IsCurrentRoom(joinedRoom)) return;

                UpdateZoneState();
                if (state.matchState == "PLAYING")
                {
                    HandleGameStart();
                }
            };

            joinedRoom.OnMessage<object>("GAME_START", (message) =>
            {
                if (!IsCurrentRoom(joinedRoom)) return;

                Debug.Log("GAME_START message received");
                UpdateZoneState();
                HandleGameStart();
            });

            joinedRoom.OnMessage<object>("GAME_OVER", (message) =>
            {
                if (!IsCurrentRoom(joinedRoom)) return;

                Debug.Log("GAME_OVER message received");
                OnGameOver?.Invoke();
            });

            joinedRoom.OnMessage<MatchResultMessage>("match_result", (message) =>
            {
                if (!IsCurrentRoom(joinedRoom)) return;
                OnMatchResultReceived?.Invoke(message);
            });

            joinedRoom.OnMessage<KillFeedMessage>("kill_feed", (message) =>
            {
                if (!IsCurrentRoom(joinedRoom)) return;
                OnKillFeedReceived?.Invoke(message);
            });

            joinedRoom.OnMessage<ShootMessage>("shoot", (message) =>
            {
                if (!IsCurrentRoom(joinedRoom)) return;

                if (playerObjects.TryGetValue(message.clientId, out GameObject playerObj))
                {
                    var pc = playerObj.GetComponent<PlayerController>();
                    if (pc != null)
                    {
                        Vector3 pos = new Vector3(message.x, message.y, message.z);
                        Quaternion rot = Quaternion.Euler(message.rx, message.ry, message.rz);
                        pc.PerformShoot(pos, rot);
                    }

                    // Remote PlayerController instances are intentionally disabled, so their
                    // Start method never caches an Animator. NetworkPlayerSync owns the live
                    // remote Animator and must trigger the replicated attack animation.
                    playerObj.GetComponent<NetworkPlayerSync>()?.TriggerAttackAnimation();
                }
            });

            joinedRoom.OnMessage<MeleeAttackMessage>("melee_attack", (message) =>
            {
                if (!IsCurrentRoom(joinedRoom)) return;
                if (message == null || string.IsNullOrEmpty(message.attackerId)) return;
                if (message.attackerId == joinedRoom.SessionId) return;

                if (playerObjects.TryGetValue(message.attackerId, out GameObject playerObj))
                {
                    VectoAudioManager.PlayMelee(playerObj.transform.position, false);
                    playerObj.GetComponent<NetworkPlayerSync>()?.TriggerAttackAnimation();
                }
            });

            joinedRoom.OnMessage<ItemPickedMessage>("item_picked", (message) =>
            {
                if (!IsCurrentRoom(joinedRoom)) return;
                OnItemPicked(message);
            });

            joinedRoom.OnMessage<DamageTakenMessage>("damage_taken", (message) =>
            {
                if (!IsCurrentRoom(joinedRoom)) return;
                if (message == null) return;

                // Play the victim's built-in "hit" reaction animation (top-down feedback).
                if (playerObjects.TryGetValue(message.victimId, out GameObject victimObj) && victimObj != null)
                {
                    var pc = victimObj.GetComponent<PlayerController>();
                    if (pc != null)
                    {
                        pc.TriggerHitAnimation();
                    }
                }

                OnDamageTaken?.Invoke(message);
            });

            var callbacks = Colyseus.Schema.Callbacks.Get(joinedRoom);
            callbacks.OnAdd(state => state.players, (key, player) =>
            {
                if (IsCurrentRoom(joinedRoom)) OnPlayerJoin(key, player);
            });
            callbacks.OnRemove(state => state.players, (key, player) =>
            {
                if (IsCurrentRoom(joinedRoom)) OnPlayerLeave(key, player);
            });
            
            callbacks.OnAdd(state => state.items, (key, item) =>
            {
                if (IsCurrentRoom(joinedRoom)) OnItemAdd(key, item);
            });
            callbacks.OnRemove(state => state.items, (key, item) =>
            {
                if (IsCurrentRoom(joinedRoom)) OnItemRemove(key, item);
            });
        }
        catch (Exception ex)
        {
            LastErrorMessage = NormalizeConnectionError(ex.Message);
            Debug.LogError("Connection failed: " + LastErrorMessage);
            OnConnectionFailed?.Invoke(LastErrorMessage);
        }
        finally
        {
            isConnectingToBattle = false;
        }
    }

    private bool IsCurrentRoom(Room<GameState> sourceRoom)
    {
        return sourceRoom != null && ReferenceEquals(sourceRoom, room) && !cancelMatchmakingRequested;
    }

    private void ConfigureRoomReconnection(Room<GameState> joinedRoom)
    {
        joinedRoom.Reconnection.Enabled = true;
        joinedRoom.Reconnection.MinUptime = 0;
        joinedRoom.Reconnection.MaxRetries = 10;
        joinedRoom.Reconnection.MinDelay = 250;
        joinedRoom.Reconnection.Delay = 250;
        joinedRoom.Reconnection.MaxDelay = 2000;
        joinedRoom.Reconnection.MaxEnqueuedMessages = 0;

        joinedRoom.OnDrop += code => EnqueueRoomConnectionEvent(joinedRoom, RoomConnectionEventType.Dropped, code);
        joinedRoom.OnReconnect += () => EnqueueRoomConnectionEvent(joinedRoom, RoomConnectionEventType.Reconnected, 0);
        joinedRoom.OnLeave += code => EnqueueRoomConnectionEvent(joinedRoom, RoomConnectionEventType.Failed, code);
    }

    private void EnqueueRoomConnectionEvent(Room<GameState> sourceRoom, RoomConnectionEventType type, int code)
    {
        roomConnectionEvents.Enqueue(new RoomConnectionEvent
        {
            SourceRoom = sourceRoom,
            Type = type,
            Code = code
        });
    }

    private void HandleRoomConnectionEvent(RoomConnectionEvent connectionEvent)
    {
        if (connectionEvent.SourceRoom != room)
        {
            return;
        }

        switch (connectionEvent.Type)
        {
            case RoomConnectionEventType.Dropped:
                Debug.LogWarning($"[NetworkManager] Connection dropped (code {connectionEvent.Code}); reconnecting...");
                SetGameplayInputBlocked(true, "ĐANG KẾT NỐI LẠI...");
                break;

            case RoomConnectionEventType.Reconnected:
                Debug.Log("[NetworkManager] Reconnected to the active room.");
                SetGameplayInputBlocked(false, null);
                break;

            case RoomConnectionEventType.Failed:
                LastErrorMessage = "Mất kết nối tới trận đấu.";
                Debug.LogError($"[NetworkManager] Unable to reconnect to room (code {connectionEvent.Code}).");
                room = null;
                hasGameStarted = false;
                SetGameplayInputBlocked(true, "MẤT KẾT NỐI - ĐANG VỀ SẢNH...");
                OnConnectionFailed?.Invoke(LastErrorMessage);
                _ = ReturnToMenuAfterDisconnect();
                break;
        }
    }

    private async Task ReturnToMenuAfterDisconnect()
    {
        if (returningToMenuAfterDisconnect)
        {
            return;
        }

        returningToMenuAfterDisconnect = true;
        await Task.Delay(1500);

        if (SceneManager.GetActiveScene().name == "GameplayScene")
        {
            SceneManager.LoadScene("MainScene");
        }

        SetGameplayInputBlocked(false, null);
        returningToMenuAfterDisconnect = false;
    }

    private void BindConnectionOverlay()
    {
        connectionOverlay = null;
        connectionStatusLabel = null;

        UIDocument[] documents = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
        foreach (UIDocument document in documents)
        {
            if (document.gameObject.name != "GameplayUI" || document.rootVisualElement == null)
            {
                continue;
            }

            connectionOverlay = document.rootVisualElement.Q<VisualElement>("ConnectionOverlay");
            connectionStatusLabel = document.rootVisualElement.Q<Label>("ConnectionStatusLabel");
            break;
        }

        if (connectionOverlay != null)
        {
            connectionOverlay.style.display = IsGameplayInputBlocked ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void SetGameplayInputBlocked(bool blocked, string status)
    {
        IsGameplayInputBlocked = blocked;

        if (connectionStatusLabel != null && !string.IsNullOrWhiteSpace(status))
        {
            connectionStatusLabel.text = status;
        }

        if (connectionOverlay != null)
        {
            connectionOverlay.style.display = blocked ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void OnPlayerJoin(string key, PlayerState player)
    {
        Debug.Log($"Player joined schema: {player.username}, key={key}, isSceneLoaded={isSceneLoaded}, roomSessionId={room?.SessionId}");

        var callbacks = Colyseus.Schema.Callbacks.Get(room);
        Action unsubscribe = callbacks.Listen(player, current => current.isDead, (_, __) =>
        {
            if (player.isDead)
            {
                RemovePlayerObject(key, "Player died and object destroyed");
            }
        });

        if (playerSchemaUnsubs.TryGetValue(key, out var previous))
        {
            try { previous?.Invoke(); } catch (Exception ex) { Debug.LogWarning($"[NetworkManager] Prior unsub for {key} threw: {ex.Message}"); }
        }
        playerSchemaUnsubs[key] = unsubscribe;

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
        RemovePlayerObject(key, "Player left and object destroyed");
    }

    private void RemovePlayerObject(string key, string reason)
    {
        if (playerSchemaUnsubs.TryGetValue(key, out var unsubscribe))
        {
            try { unsubscribe?.Invoke(); } catch (Exception ex) { Debug.LogWarning($"[NetworkManager] Unsub for {key} threw: {ex.Message}"); }
            playerSchemaUnsubs.Remove(key);
        }

        if (!playerObjects.TryGetValue(key, out GameObject playerObject))
        {
            return;
        }

        if (playerObject != null)
        {
            Destroy(playerObject);
        }

        playerObjects.Remove(key);
        Debug.Log($"{reason}: {key}");
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
        else if (item.type == "VEC")
        {
            prefabToSpawn = itemVecPrefab;
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
        if (item.type == "VEC")
        {
            ConfigureVecPickup(spawnedItem);
        }

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

    private void ConfigureVecPickup(GameObject spawnedItem)
    {
        if (spawnedItem == null)
        {
            return;
        }

        if (spawnedItem.GetComponent<VecPickupMarker>() == null)
        {
            spawnedItem.AddComponent<VecPickupMarker>();
        }

        bool hasTrigger = false;
        Collider[] colliders = spawnedItem.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            if (collider != null && collider.isTrigger)
            {
                hasTrigger = true;
                break;
            }
        }

        if (!hasTrigger)
        {
            SphereCollider trigger = spawnedItem.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.35f;
            trigger.center = new Vector3(0f, 0.55f, 0f);
        }
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

        if (message.itemType == "VEC")
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
            if (player.isDead)
            {
                RemovePlayerObject(key, "Dead player skipped and object destroyed");
                return;
            }

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

    public GameObject FindPlayerObjectByUsername(string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            return null;
        }

        foreach (KeyValuePair<string, GameObject> entry in playerObjects)
        {
            GameObject playerObject = entry.Value;
            if (playerObject == null)
            {
                continue;
            }

            NetworkPlayerSync sync = playerObject.GetComponent<NetworkPlayerSync>();
            if (sync == null || sync.GetState() == null)
            {
                continue;
            }

            if (string.Equals(sync.GetState().username, username, StringComparison.OrdinalIgnoreCase))
            {
                return playerObject;
            }
        }

        return null;
    }

    public GameObject FindRandomAlivePlayerObject(string excludedSessionId = null)
    {
        List<GameObject> alivePlayers = new List<GameObject>();

        foreach (KeyValuePair<string, GameObject> entry in playerObjects)
        {
            if (!string.IsNullOrEmpty(excludedSessionId) && entry.Key == excludedSessionId)
            {
                continue;
            }

            GameObject playerObject = entry.Value;
            if (playerObject == null)
            {
                continue;
            }

            NetworkPlayerSync sync = playerObject.GetComponent<NetworkPlayerSync>();
            PlayerState playerState = sync != null ? sync.GetState() : null;
            if (playerState != null && !playerState.isDead && playerState.hp > 0)
            {
                alivePlayers.Add(playerObject);
            }
        }

        if (alivePlayers.Count == 0)
        {
            return null;
        }

        return alivePlayers[UnityEngine.Random.Range(0, alivePlayers.Count)];
    }

    public int GetAlivePlayerCount()
    {
        if (room == null || room.State == null)
        {
            return 0;
        }

        int serverAliveCount = Mathf.RoundToInt(room.State.aliveCount);
        if (serverAliveCount > 0)
        {
            return serverAliveCount;
        }

        int aliveCount = 0;
        if (room.State.players != null)
        {
            room.State.players.ForEach((key, player) =>
            {
                if (player != null && !player.isDead && player.hp > 0)
                {
                    aliveCount++;
                }
            });
        }

        return aliveCount;
    }

    public int GetMatchPlayerCount()
    {
        if (room == null || room.State == null || room.State.players == null)
        {
            return 0;
        }

        int playerCount = 0;
        room.State.players.ForEach((_, __) => playerCount++);
        return playerCount;
    }

    private void SpawnPlayer(string key, PlayerState playerState)
    {
        if (playerState.isDead)
        {
            RemovePlayerObject(key, "Dead player spawn ignored and object destroyed");
            return;
        }

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

        if (!isLocalPlayer)
        {
            ApplyInitialRemoteWeaponVisuals(playerObj, playerState);
        }

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

    private void ApplyInitialRemoteWeaponVisuals(GameObject playerObj, PlayerState playerState)
    {
        if (playerObj == null || playerState == null)
        {
            return;
        }

        PlayerController playerController = playerObj.GetComponent<PlayerController>();
        if (playerController == null)
        {
            return;
        }

        GameObject meleePrefab = null;
        if (weaponDatabase != null)
        {
            string meleeWeapon = string.IsNullOrEmpty(playerState.meleeWeapon) ? "Sword" : playerState.meleeWeapon;
            WeaponData meleeData = weaponDatabase.GetWeaponData(meleeWeapon);
            if (meleeData != null)
            {
                meleePrefab = meleeData.weaponModelPrefab;
            }
        }

        playerController.EnsureMeleeWeaponVisual(meleePrefab);

        if (!string.IsNullOrEmpty(playerState.rangedWeapon) && weaponDatabase != null)
        {
            WeaponData rangedData = weaponDatabase.GetWeaponData(playerState.rangedWeapon);
            if (rangedData != null)
            {
                playerController.EquipWeapon(
                    rangedData.weaponModelPrefab,
                    rangedData.bulletPrefab,
                    rangedData.fireRate,
                    rangedData.maxAmmo
                );
            }
        }

        playerController.SyncWeaponStateFromServer();
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

    public async Task Logout()
    {
        cancelMatchmakingRequested = true;
        authToken = null;
        hasGameStarted = false;

        if (leaveRoomTask != null)
        {
            await leaveRoomTask;
            leaveRoomTask = null;
        }

        await LeaveCurrentRoom();
        playerObjects.Clear();
        itemObjects.Clear();
        itemWeaponConfigs.Clear();
    }

    public void LogoutLocal(bool leaveRoom = false)
    {
        cancelMatchmakingRequested = true;
        authToken = null;
        hasGameStarted = false;

        var currentRoom = room;
        room = null;
        leaveRoomTask = null;
        playerObjects.Clear();
        itemObjects.Clear();
        itemWeaponConfigs.Clear();

        if (leaveRoom && currentRoom != null)
        {
            _ = LeaveRoomBestEffort(currentRoom);
        }
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

    private async Task LeaveRoomBestEffort(Room<GameState> roomToLeave)
    {
        try
        {
            Task leaveTask = roomToLeave.Leave();
            Task completedTask = await Task.WhenAny(leaveTask, Task.Delay(1500));
            if (completedTask != leaveTask)
            {
                Debug.LogWarning("Logout room leave timed out; continuing.");
                return;
            }

            await leaveTask;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Logout room leave failed: " + ex.Message);
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
        public string code;
        public string reason;
    }

    private string ExtractApiError(string responseString, string fallback, int statusCode = 0)
    {
        if (statusCode == 530 &&
            !string.IsNullOrEmpty(responseString) &&
            responseString.Contains("1033"))
        {
            return "Server is temporarily unavailable (Cloudflare Tunnel offline).";
        }

        try
        {
            PlayerApiError error = JsonConvert.DeserializeObject<PlayerApiError>(responseString);
            return BuildApiErrorMessage(error, fallback);
        }
        catch
        {
            return string.IsNullOrEmpty(fallback) ? "Request failed." : fallback;
        }
    }

    private string BuildApiErrorMessage(PlayerApiError error, string fallback)
    {
        if (error != null && error.code == "ACCOUNT_BANNED")
        {
            return string.IsNullOrEmpty(error.reason)
                ? "Your account has been banned."
                : $"Your account has been banned. Reason: {error.reason}";
        }

        if (error != null && !string.IsNullOrEmpty(error.error))
        {
            return error.error;
        }

        return string.IsNullOrEmpty(fallback) ? "Request failed." : fallback;
    }

    private string NormalizeConnectionError(string message)
    {
        if (!string.IsNullOrEmpty(message) && message.Contains("ACCOUNT_BANNED:"))
        {
            int index = message.IndexOf("ACCOUNT_BANNED:", StringComparison.Ordinal);
            string reason = message.Substring(index + "ACCOUNT_BANNED:".Length).Trim();
            return string.IsNullOrEmpty(reason)
                ? "Your account has been banned."
                : $"Your account has been banned. Reason: {reason}";
        }

        return string.IsNullOrEmpty(message) ? "Unable to connect to match." : message;
    }

    [Serializable]
    public class WalletNonceResponse
    {
        public string nonce;
        public string message;
        public string issuedAt;
        public string expiresAt;
    }

    [Serializable]
    public class WalletVerifyResponse
    {
        public bool success;
        public string walletAddress;
    }

    [Serializable]
    public class NftSyncResponse
    {
        public string walletAddress;
        public string syncedAt;
        public SyncedNftSkinResponse[] nftSkins;
    }

    [Serializable]
    public class NftPurchaseConfirmResponse
    {
        public string walletAddress;
        public string txHash;
        public SyncedNftSkinResponse nftSkin;
    }

    [Serializable]
    public class SyncedNftSkinResponse
    {
        public string skinId;
        public int chainId;
        public string contractAddress;
        public string tokenId;
        public string standard;
        public int balance;
        public bool owned;
        public string lastSyncedAt;
    }

    [Serializable]
    public class PlayerProfileResponse
    {
        public string username;
        public string walletAddress;
        public int vecUnlockedBalance;
        public int vecLockedBalance;
        public int coinBalance;
        public int level;
        public int xp;
        public int xpToNextLevel;
        public float xpProgress;
        public int levelsGained;
        public string equippedPlayerSkin;
        public string[] ownedSkins;
        public ShopSkinResponse[] skinOwnership;
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
        public int vecEarned;
        public bool isWinner;
        public bool isFinalized;
    }

    [Serializable]
    public class ShopSkinResponse
    {
        public string id;
        public string skinId;
        public string displayName;
        public string prefabKey;
        public int price;
        public string currencyType;
        public string ownershipType;
        public SkinNftMappingResponse nft;
        public NftSkinInfoResponse nftInfo;
        public bool owned;
        public bool canEquip;
        public string source;
        public bool equipped;
    }

    [Serializable]
    public class SkinNftMappingResponse
    {
        public int? chainId;
        public string contractAddress;
        public string tokenId;
        public string collectionKey;
    }

    [Serializable]
    public class NftSkinInfoResponse
    {
        public int? chainId;
        public string contractAddress;
        public string tokenId;
        public string standard;
        public int balance;
        public string lastSyncedAt;
    }

    [Serializable]
    public class TransactionHistoryResponse
    {
        public CurrencyTransactionResponse[] transactions;
        public int limit;
        public int offset;
        public int total;
    }

    [Serializable]
    public class CurrencyTransactionResponse
    {
        public string id;
        public string currencyType;
        public string vecBucket;
        public string type;
        public int amount;
        public int balanceBefore;
        public int balanceAfter;
        public string status;
        public string txHash;
        public int? chainId;
        public string contractAddress;
        public string referenceId;
        public string note;
        public string createdAt;
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
    public class DamageTakenMessage
    {
        public string victimId;
        public float damage;
        public bool lethal;
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
