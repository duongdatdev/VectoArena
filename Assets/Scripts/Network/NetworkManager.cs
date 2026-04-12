using UnityEngine;
using Colyseus;
using System.Threading.Tasks;
using System;

public class NetworkManager : MonoBehaviour
{
    //local Node.js server endpoint. Change this before pushing to production.
    private const string ServerURL = "ws://localhost:2567";
    
    private Client client;

    // using a generic object here for now. 
    // remember to swap this out with actual schema later.
    private Room<GameState> room; 

    async void Start()
    {
        client = new Client(ServerURL);
        await ConnectToServer();
    }

    private async Task ConnectToServer()
    {
        try
        {
            Debug.Log("Attempting to connect to the Colyseus server...");
            
            room = await client.JoinOrCreate<GameState>("battle"); 
            
            Debug.Log("Successfully connected! Session ID: " + room.SessionId);
        }
        catch (Exception ex)
        {
            Debug.LogError("Connection failed: " + ex.Message);
        }
    }
    
    async void OnDestroy()
    {
        if (room != null)
        {
            await room.Leave();
        }
    }
}