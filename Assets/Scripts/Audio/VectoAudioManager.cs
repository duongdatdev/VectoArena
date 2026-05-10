using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class VectoAudioManager : MonoBehaviour
{
    public static VectoAudioManager Instance { get; private set; }

    [SerializeField] private VectoAudioLibrary library;
    [SerializeField] private int pooledSources = 24;
    [SerializeField] private float spatialBlend3D = 1f;
    [SerializeField] private float minDistance3D = 1f;
    [SerializeField] private float maxDistance3D = 35f;
    [SerializeField] private Vector3 listenerOffset = Vector3.zero;

    private AudioSource[] sources;
    private AudioSource musicSource;
    private AudioListener audioListener;
    private Transform listenerFollowTarget;
    private Coroutine musicSequenceCoroutine;
    private int nextSourceIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        CreateSources();
        CreateAudioListener();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void GlobalEnsureAudioListener()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        CheckForListener();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckForListener();
    }

    private static void CheckForListener()
    {
        AudioListener listener = Object.FindAnyObjectByType<AudioListener>();
        if (listener == null)
        {
            // Try to find MainCamera first
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.gameObject.AddComponent<AudioListener>();
                Debug.Log($"[VectoAudioManager] Added AudioListener to MainCamera in scene: {SceneManager.GetActiveScene().name}");
            }
            else
            {
                // Fallback: create a global listener object
                GameObject listenerGO = new GameObject("GlobalAudioListener");
                listenerGO.AddComponent<AudioListener>();
                Object.DontDestroyOnLoad(listenerGO);
                Debug.Log($"[VectoAudioManager] Created GlobalAudioListener in scene: {SceneManager.GetActiveScene().name}");
            }
        }
    }

    public static void Play2D(VectoAudioId id)
    {
        Instance?.Play(id, Vector3.zero, false);
    }

    public static void PlayMainMenuMusic()
    {
        Instance?.PlaySequentialMusic(VectoAudioId.MusicMainStart, VectoAudioId.MusicMainLoop);
    }

    public static void PlayBattleMusic()
    {
        Instance?.PlayMusic(VectoAudioId.MusicBrLowLoop);
    }

    public static void Play3D(VectoAudioId id, Vector3 position)
    {
        Instance?.Play(id, position, true);
    }

    public static void PlayWeaponShot(string weaponName, Vector3 position, bool isLocalPlayer)
    {
        Play3D(GetWeaponShotId(weaponName, isLocalPlayer), position);
    }

    public static void PlayMelee(Vector3 position, bool isLocalPlayer)
    {
        Play3D(isLocalPlayer ? VectoAudioId.DefaultMeleeShotLocal : VectoAudioId.DefaultMeleeShot, position);
    }

    public static void PlayPickup(string itemType, Vector3 position, bool isLocalPlayer)
    {
        if (itemType == "MedicalKit")
        {
            Play3D(isLocalPlayer ? VectoAudioId.HealthPickupLocal : VectoAudioId.HealthPickup, position);
            return;
        }

        Play3D(isLocalPlayer ? VectoAudioId.WeaponPickupLocal : VectoAudioId.WeaponPickup, position);
    }

    public static void FollowLocalPlayer(Transform target)
    {
        if (Instance == null)
        {
            return;
        }

        Instance.listenerFollowTarget = target;
        if (Instance.audioListener != null)
        {
            Instance.audioListener.transform.position = target != null ? target.position + Instance.listenerOffset : Vector3.zero;
        }
    }

    private void LateUpdate()
    {
        if (audioListener != null && listenerFollowTarget != null)
        {
            audioListener.transform.position = listenerFollowTarget.position + listenerOffset;
        }
    }

    public void PlayMusic(VectoAudioId id)
    {
        if (musicSequenceCoroutine != null)
        {
            StopCoroutine(musicSequenceCoroutine);
            musicSequenceCoroutine = null;
        }

        PlayMusicInternal(id);
    }

    public void PlaySequentialMusic(VectoAudioId startId, VectoAudioId loopId)
    {
        if (musicSequenceCoroutine != null)
        {
            StopCoroutine(musicSequenceCoroutine);
        }

        musicSequenceCoroutine = StartCoroutine(PlaySequentialMusicRoutine(startId, loopId));
    }

    public void StopMusic()
    {
        if (musicSequenceCoroutine != null)
        {
            StopCoroutine(musicSequenceCoroutine);
            musicSequenceCoroutine = null;
        }

        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    private void PlayMusicInternal(VectoAudioId id)
    {
        VectoAudioEntry entry = GetEntry(id);
        AudioClip clip = library != null ? library.GetRandomClip(id) : null;
        if (entry == null || clip == null)
        {
            return;
        }

        musicSource.clip = clip;
        musicSource.volume = entry.volume;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.Play();
    }

    private void Play(VectoAudioId id, Vector3 position, bool is3D)
    {
        VectoAudioEntry entry = GetEntry(id);
        AudioClip clip = library != null ? library.GetRandomClip(id) : null;
        if (entry == null || clip == null)
        {
            return;
        }

        AudioSource source = GetSource();
        source.transform.position = position;
        source.clip = clip;
        source.volume = entry.volume;
        source.loop = entry.loop;
        source.spatialBlend = is3D ? spatialBlend3D : 0f;
        source.minDistance = minDistance3D;
        source.maxDistance = maxDistance3D;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.Play();

        if (!entry.loop)
        {
            StartCoroutine(StopWhenFinished(source, clip.length));
        }
    }

    private VectoAudioEntry GetEntry(VectoAudioId id)
    {
        if (library == null || library.entries == null)
        {
            return null;
        }

        return library.entries.Find(entry => entry.id == id);
    }

    private AudioSource GetSource()
    {
        AudioSource source = sources[nextSourceIndex];
        nextSourceIndex = (nextSourceIndex + 1) % sources.Length;
        source.Stop();
        return source;
    }

    private void CreateSources()
    {
        sources = new AudioSource[Mathf.Max(1, pooledSources)];
        for (int i = 0; i < sources.Length; i++)
        {
            GameObject sourceObject = new GameObject($"AudioSource_{i}");
            sourceObject.transform.SetParent(transform);
            sources[i] = sourceObject.AddComponent<AudioSource>();
            sources[i].playOnAwake = false;
        }

        GameObject musicObject = new GameObject("MusicSource");
        musicObject.transform.SetParent(transform);
        musicSource = musicObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
    }

    private void CreateAudioListener()
    {
        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude);
        if (listeners.Length > 0)
        {
            audioListener = listeners[0];
            audioListener.enabled = true;
            for (int i = 1; i < listeners.Length; i++)
            {
                listeners[i].enabled = false;
            }
            return;
        }

        GameObject listenerObject = new GameObject("PlayerAudioListener");
        listenerObject.transform.SetParent(transform);
        audioListener = listenerObject.AddComponent<AudioListener>();
        audioListener.enabled = true;
    }

    private IEnumerator StopWhenFinished(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (source != null && !source.loop)
        {
            source.Stop();
        }
    }

    private IEnumerator PlaySequentialMusicRoutine(VectoAudioId startId, VectoAudioId loopId)
    {
        VectoAudioEntry startEntry = GetEntry(startId);
        AudioClip startClip = library != null ? library.GetRandomClip(startId) : null;
        if (startEntry != null && startClip != null)
        {
            musicSource.clip = startClip;
            musicSource.volume = startEntry.volume;
            musicSource.loop = false;
            musicSource.spatialBlend = 0f;
            musicSource.Play();
            yield return new WaitForSeconds(startClip.length);
        }

        PlayMusicInternal(loopId);
        musicSequenceCoroutine = null;
    }

    private static VectoAudioId GetWeaponShotId(string weaponName, bool isLocalPlayer)
    {
        switch (weaponName)
        {
            case "Shotgun":
            case "BlasterShotgun":
                return isLocalPlayer ? VectoAudioId.ShotgunShotLocal : VectoAudioId.ShotgunShot;
            case "Sniper":
            case "HunterSniper":
                return isLocalPlayer ? VectoAudioId.SniperShotLocal : VectoAudioId.SniperShot;
            case "Pistol":
                return isLocalPlayer ? VectoAudioId.PistolShotLocal : VectoAudioId.PistolShot;
            case "MachineGun":
                return isLocalPlayer ? VectoAudioId.SMGShotLocal : VectoAudioId.SMGShot;
            case "Minigun":
                return isLocalPlayer ? VectoAudioId.MinigunShotLocal : VectoAudioId.MinigunShot;
            case "Launcher":
                return isLocalPlayer ? VectoAudioId.QuadzookaShotLocal : VectoAudioId.QuadzookaShot;
            default:
                return isLocalPlayer ? VectoAudioId.AssaultRifleShotLocal : VectoAudioId.AssaultRifleShot;
        }
    }
}
