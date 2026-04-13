using UnityEngine;
using Colyseus;
using System.Threading.Tasks;
using System;

using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

public class NetworkManager : MonoBehaviour
{
    //singleton instance
    public static NetworkManager Instance;
    
    //local Node.js server endpoint. Change this before pushing to production.
    private const string ServerURL = "ws://localhost:2567";
    private const string HttpURL = "http://localhost:2567";
    
    private Client client;
    private string authToken;

    // using a generic object here for now. 
    // remember to swap this out with actual schema later.
    private Room<GameState> room;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
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

    private async Task ConnectAndJoinBattle()
    {
        try
        {
            Debug.Log("Attempting to connect to the server...");
            
            // Pass token to options if needed
            var options = new Dictionary<string, object> { { "accessToken", authToken } };
            room = await client.JoinOrCreate<GameState>("battle", options); 
            
            Debug.Log("Successfully connected! Session ID: " + room.SessionId);
        }
        catch (Exception ex)
        {
            Debug.LogError("Connection failed: " + ex.Message);
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