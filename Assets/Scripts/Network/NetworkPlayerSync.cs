using UnityEngine;
using VectoArena.Schema;
using Colyseus;

public class NetworkPlayerSync : MonoBehaviour
{
    private PlayerState state;
    private string sessionId;
    private Room<GameState> room;
    private bool isLocalPlayer;

    [Header("Interpolation Settings")]
    [SerializeField] private float positionLerpSpeed = 10f;
    [SerializeField] private float rotationLerpSpeed = 10f;

    public void Initialize(PlayerState playerState, string sid, Room<GameState> roomInstance)
    {
        this.state = playerState;
        this.sessionId = sid;
        this.room = roomInstance;
        this.isLocalPlayer = (state.id == room.SessionId);

        if (!isLocalPlayer)
        {
            // initial position and rotation for remote player
            transform.position = new Vector3(state.x, state.y, state.z);
            transform.rotation = Quaternion.Euler(0, state.rotation, 0);
            
            // script that should only run for the local player
            if (TryGetComponent<PlayerController>(out var controller))
            {
                controller.enabled = false;
            }
        }
    }

    private void Update()
    {
        if (state == null) return;

        if (isLocalPlayer)
        {
            SyncLocalMovementToServer();
        }
        else
        {
            SyncRemoteMovementToClient();
        }
    }

    private void SyncLocalMovementToServer()
    {
        // send every frame for now, but in production, you'd want rate limiting.
        room.Send("move", new
        {
            x = transform.position.x,
            y = transform.position.y,
            z = transform.position.z,
            rotation = transform.eulerAngles.y
        });
    }

    private void SyncRemoteMovementToClient()
    {
        // smoothly interpolate to the target position and rotation from the server
        Vector3 targetPosition = new Vector3(state.x, state.y, state.z);
        Quaternion targetRotation = Quaternion.Euler(0, state.rotation, 0);

        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * positionLerpSpeed);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * rotationLerpSpeed);
    }
}
