using UnityEngine;

using System.IO;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class PreySpawner : MonoBehaviour
{
    public static PreySpawner Instance { get; private set; }
    public GameObject fishPrefab;
    public GameObject newFishSprite;

    public List<Transform> sharkViewOrigins;

    [Header("Non-Respawning Fish")]
    public GameObject nonRespawningFishPrefab;
    public int nonRespawningFishCount = 0;

    [Header("Initial Spawn Count")]
    public int fishCount = 30;

    [Header("Spawner Dimension Settings")]
    public float spawnWidth = 100f;
    public float spawnHeight = 10f;
    public float spawnDepth = 100f;

    [Header("Reproduction Settings")]
    public float fishReproductionIntervalMinutes = 1f;
    public int fishPerNewFish = 5;
    private float fishTimer = 0f;

    [Header("Dynamic Respawn Settings")]
    public bool enableDynamicRespawn = false;
    public float dynamicRespawnInterval = 10f; // seconds
    public int maxFishCount = 100;
    private float dynamicRespawnTimer = 0f;

    [Header("Fish Size Range")]
    public float minFishScale = 0.6f;
    public float maxFishScale = 1.5f;

    [Header("Raffle Controller")]
    public InputActionAsset inputActions;


    // Runtime texture loading
    private List<Texture2D> loadedTextures = new List<Texture2D>();
    private string texturesPath;
    private float refreshTimer = 0f;
    private RevealedFishTracker revealedTracker = new RevealedFishTracker();
    private HashSet<string> knownTextureIds = new HashSet<string>();
    private Queue<string> orbitQueue = new Queue<string>();
    private bool isOrbiting = false;
    private bool waitingForHungerInput = false;
    public float refreshInterval = 5f; // seconds
    private InputAction enterRaffleAction;
    private InputAction[] hungerActions = new InputAction[10]; // For 1-10
    private InputAction addHungerAction;
    private InputAction exitRaffleAction;
    private Queue<WinnerEntry> winnerQueue = new Queue<WinnerEntry>();
    private float winnerCheckTimer = 0f;
    public float winnerQueueDelay = 1f; // seconds between checks
    private bool rafflePending = false;



    void Start()
    {
        Instance = this;
        var raffleMap = inputActions.FindActionMap("Raffle");

        enterRaffleAction = raffleMap.FindAction("EnterRaffle");
        addHungerAction = raffleMap.FindAction("AddHunger");
        exitRaffleAction = raffleMap.FindAction("ExitRaffle");
        for (int i = 0; i < 10; i++)
            hungerActions[i] = raffleMap.FindAction("Hunger" + (i + 1)); // "Hunger1" ... "Hunger10"

        enterRaffleAction.Enable();
        addHungerAction.Enable();
        exitRaffleAction.Enable();
        foreach (var h in hungerActions) h.Enable();

        if (RaffleHungerManager.Instance == null)
        {
            Debug.LogError("RaffleHungerManager.Instance is null!");
        }

        WinnerManager.Instance.LoadWinners();
        revealedTracker.Load();
        Debug.Log(Application.persistentDataPath);
        texturesPath = Path.Combine(Application.persistentDataPath, "FishTextures");
        if (!Directory.Exists(texturesPath)) Directory.CreateDirectory(texturesPath);
        RefreshTextures();

        SpreadOutSharks();
        for (int i = 0; i < fishCount; i++) SpawnFish();
        for (int i = 0; i < nonRespawningFishCount; i++) SpawnNonRespawningFish();
    }

    void Update()
    {
        // Raffle Mode Controls
        // Debug.Log("winnerQueue: " + (winnerQueue != null));
        // Debug.Log("RaffleHungerManager.Instance: " + (RaffleHungerManager.Instance != null));

        if (!RaffleHungerManager.Instance.IsRaffleMode && !waitingForHungerInput)
        {
            fishTimer += Time.deltaTime;
            refreshTimer += Time.deltaTime;
            if (refreshTimer >= refreshInterval)
            {
                RefreshTextures();
                refreshTimer = 0f;
            }

            if (!enableDynamicRespawn && fishTimer >= fishReproductionIntervalMinutes * 60f)
            {
                fishTimer = 0f;
                int currentFish = GameObject.FindGameObjectsWithTag("Fish").Length;
                int toSpawn = currentFish / fishPerNewFish;
                for (int i = 0; i < toSpawn; i++) SpawnFish();
            }

            if (enableDynamicRespawn)
            {
                dynamicRespawnTimer += Time.deltaTime;
                if (dynamicRespawnTimer >= dynamicRespawnInterval)
                {
                    dynamicRespawnTimer = 0f;
                    int currentFish = GameObject.FindGameObjectsWithTag("Fish").Length;
                    int toSpawn = maxFishCount - currentFish;
                    for (int i = 0; i < toSpawn; i++) SpawnFish();
                }
            }

            if (!isOrbiting && orbitQueue.Count > 0)
            {
                string file = orbitQueue.Dequeue();
                string texId = Path.GetFileNameWithoutExtension(file);
                StartCoroutine(SpawnAndOrbitNewFish(texId, file));
            }
        }


        // Winner queue processing
        if (RaffleHungerManager.Instance.IsRaffleMode && winnerQueue.Count > 0 && RaffleHungerManager.Instance.Hunger > 0)
        {
            winnerCheckTimer += Time.deltaTime;
            if (winnerCheckTimer >= winnerQueueDelay)
            {
                var meta = winnerQueue.Dequeue();
                if (!WinnerManager.Instance.IsWinner(meta.email))
                {
                    WinnerManager.Instance.AddWinner(new WinnerEntry
                    {
                        id = meta.id,
                        name = meta.name,
                        email = meta.email,
                        ig = meta.ig,
                        timestamp = System.DateTime.UtcNow.ToString("o")
                    });
                    RaffleHungerManager.Instance.DecrementHunger();
                    Debug.Log($"Winner processed: {meta.name} ({meta.email}) - Hunger now: {RaffleHungerManager.Instance.Hunger}");
                }
                else
                {
                    Debug.Log($"Duplicate winner skipped: {meta.email}");
                }
                winnerCheckTimer = 0f;
            }
        }

        // Enter raffle mode: Ctrl+Shift+R (with Input System)
        var kb = Keyboard.current;
        if (
            kb.leftCtrlKey.isPressed &&
            kb.leftShiftKey.isPressed &&
            kb.rKey.wasPressedThisFrame &&
            !RaffleHungerManager.Instance.IsRaffleMode &&
            !waitingForHungerInput
        )
        {
            if (isOrbiting)
            {
                Debug.Log("Orbit in progress, raffle will start as soon as fish reveal is finished.");
                rafflePending = true;   // <-- just set the flag!
                return;
            }

            if (!HasAnyOptInTexture())
            {
                Debug.Log("Can't start raffle: No eligible fish with emails found.");
                return;
            }

            StartRaffleSetup();
        }


        // Set hunger (1-0)
        if (waitingForHungerInput)
        {
            for (int i = 0; i < 10; i++) // 0 = 10
            {
                if (hungerActions[i].triggered)
                {
                    int selectedHunger = i + 1;
                    // Remove all fish except one (and hide that one)
                    RemoveAllFishExceptOne();

                    // Set hunger
                    RaffleHungerManager.Instance.StartRaffleMode(selectedHunger);
                    waitingForHungerInput = false;

                    // Spawn fish with opt-in texture for raffle (only if hunger > 0)
                    if (selectedHunger > 0)
                    {
                        string[] files = Directory.GetFiles(texturesPath, "*.png");
                        List<string> eligible = new List<string>();

                        foreach (string file in files)
                        {
                            string fileName = Path.GetFileNameWithoutExtension(file);
                            string[] parts = fileName.Split('_');
                            if (parts.Length >= 4)
                            {
                                string email = parts[2];
                                if (!string.IsNullOrEmpty(email) && email != "\"\"")
                                    eligible.Add(file);
                            }
                        }

                        foreach (string file in eligible)
                        {
                            string texId = Path.GetFileNameWithoutExtension(file);
                            for (int j = 0; j < 2; j++) // spawn exactly 2
                                SpawnFishWithExactTexture(texId, file);
                        }
                    }


                    Debug.Log($"Raffle Mode Started with {selectedHunger} Hunger!");
                    Debug.Log("Hunger remaining: " + RaffleHungerManager.Instance.Hunger);
                }
            }
        }


        // Add hunger (H)
        if (RaffleHungerManager.Instance.IsRaffleMode && addHungerAction.triggered)
        {
            RaffleHungerManager.Instance.IncrementHunger();
            Debug.Log("Hunger incremented!");
        }

        // Exit hunger input prompt
        if (waitingForHungerInput && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            waitingForHungerInput = false;
            Debug.Log("Cancelled raffle setup.");
        }

        // Exit raffle mode any time
        if (RaffleHungerManager.Instance.IsRaffleMode && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            RaffleHungerManager.Instance.StopRaffleMode();
            RemoveAllFishExceptOne();
            Debug.Log("Raffle Mode Exited");

            // Respawn initial fish count after raffle ends
            for (int i = 0; i < fishCount; i++) SpawnFish();
        }
    }

    void RemoveAllFishExceptOne()
    {
        var allFish = GameObject.FindGameObjectsWithTag("Fish");
        GameObject fishToKeep = null;

        // Find the first fish to keep (or just the first in array)
        if (allFish.Length > 0)
            fishToKeep = allFish[0];

        // Destroy all other fish
        for (int i = 0; i < allFish.Length; i++)
        {
            if (allFish[i] != fishToKeep)
                Destroy(allFish[i]);
        }

        // Hide the remaining fish if there is one
        if (fishToKeep != null)
        {
            var renderers = fishToKeep.GetComponentsInChildren<Renderer>();
            foreach (var rend in renderers)
            {
                rend.enabled = false;
            }
            // Optionally: also disable any collider or logic scripts
            var collider = fishToKeep.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;
        }
    }

    bool HasAnyOptInTexture()
    {
        string[] files = Directory.GetFiles(texturesPath, "*.png");
        foreach (string file in files)
        {
            Debug.Log(file);
            string fileName = Path.GetFileNameWithoutExtension(file);
            string[] parts = fileName.Split('_');
            if (parts.Length >= 4)
            {
                string email = parts[2];
                if (!string.IsNullOrEmpty(email) && email != "\"\"")
                    return true;
            }
        }
        return false;
    }

    void StartRaffleSetup()
    {
        RaffleUIManager.Instance.ShowRafflePanel();
        Debug.Log("Press 1-0 to set hunger for raffle!");
        waitingForHungerInput = true;
    }
    void SpawnFishWithExactTexture(string texId, string texFile)
    {
        Vector3 pos = GetSpawnPosition();
        GameObject fish = Instantiate(fishPrefab, pos, Quaternion.identity);

        // Load the texture
        byte[] bytes = File.ReadAllBytes(texFile);
        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(bytes);

        Renderer rend = fish.GetComponentInChildren<Renderer>();
        if (rend != null && rend.material != null)
            rend.material.mainTexture = tex;

        // Apply metadata from file name
        string[] parts = texId.Split('_');
        if (parts.Length >= 4)
        {
            var meta = fish.AddComponent<FishMetadata>();
            meta.id = parts[0];
            meta.name = parts[1];
            meta.email = parts[2];
            meta.ig = parts[3];
        }

        // Random scale
        float randScale = Random.Range(minFishScale, maxFishScale);
        fish.transform.localScale = new Vector3(randScale, randScale, randScale);
        fish.SetActive(true);
    }
    public void EnqueueWinner(string id, string name, string email, string ig)
    {
        if (!RaffleHungerManager.Instance.IsRaffleMode) return;
        if (RaffleHungerManager.Instance.Hunger <= 0) return;
        if (WinnerManager.Instance.IsWinner(email)) return;

        winnerQueue.Enqueue(new WinnerEntry
        {
            id = id,
            name = name,
            email = email,
            ig = ig,
            timestamp = System.DateTime.UtcNow.ToString("o")
        });
        Debug.Log($"[Enqueue] Winner {name} ({email}) queued!");
    }


    void RefreshTextures()
    {
        loadedTextures.Clear();
        string[] files = Directory.GetFiles(texturesPath, "*.png");
        foreach (var file in files)
        {
            // Get the texture ID from the filename (without extension)
            string texId = Path.GetFileNameWithoutExtension(file);
            // Only enqueue if not known AND not revealed
            if (!knownTextureIds.Contains(texId) && !revealedTracker.IsRevealed(texId))
            {
                orbitQueue.Enqueue(file); // queue for orbit/animation
                knownTextureIds.Add(texId);
            }
            // Load the texture as usual
            byte[] bytes = File.ReadAllBytes(file);
            Texture2D tex = new Texture2D(2, 2);
            if (tex.LoadImage(bytes))
                loadedTextures.Add(tex);
        }
        // Debug.Log($"FishTextures Reloaded: {loadedTextures.Count} found. {orbitQueue.Count} new.");
    }

    private IEnumerator<WaitForSeconds> SpawnAndOrbitNewFish(string texId, string texFile)
    {
        isOrbiting = true;
        revealedTracker.MarkRevealed(texId);
        // 1. Load texture from file
        byte[] bytes = File.ReadAllBytes(texFile);
        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(bytes);

        // 2. Spawn the fish with ONLY this texture (ensure unique spawn)
        Vector3 pos = GetSpawnPosition();
        GameObject fish = Instantiate(fishPrefab, pos, Quaternion.identity);
        Renderer rend = fish.GetComponentInChildren<Renderer>();
        if (rend != null && rend.material != null) rend.material.mainTexture = tex;
        fish.SetActive(true);
        Debug.Log($"Spawning & orbiting new fish: {texId}");


        // Start the camera orbit (using the real camera orbit manager)
        bool orbitDone = false;
        CameraOrbitManager.Instance.OrbitAroundFish(fish.transform, () => orbitDone = true);

        // Wait until orbit animation finishes
        while (!orbitDone)
            yield return null;


        Debug.Log($"Finished orbit for {texId}");

        isOrbiting = false;
        if (rafflePending)
        {
            rafflePending = false;
            if (HasAnyOptInTexture())
                StartRaffleSetup();
            else
                Debug.Log("Can't start raffle: No eligible fish with emails found.");
        }
    }

    void SpawnFish()
    {
        Vector3 pos = GetSpawnPosition();
        GameObject fish = Instantiate(fishPrefab, pos, Quaternion.identity);

        // Assign a random loaded texture at runtime
        if (loadedTextures.Count > 0)
        {
            Renderer rend = fish.GetComponentInChildren<Renderer>();
            if (rend != null && rend.material != null)
            {
                rend.material.mainTexture = loadedTextures[Random.Range(0, loadedTextures.Count)];
            }
        }

        float randScale = Random.Range(minFishScale, maxFishScale);
        fish.transform.localScale = new Vector3(randScale, randScale, randScale);
        fish.SetActive(true);

    }

    void SpawnNonRespawningFish()
    {
        if (nonRespawningFishPrefab == null) return;
        Vector3 pos = GetSpawnPosition();
        GameObject fish = Instantiate(nonRespawningFishPrefab, pos, Quaternion.identity);

        float randScale = Random.Range(minFishScale, maxFishScale);
        fish.transform.localScale = new Vector3(randScale, randScale, randScale);
    }

    void SpreadOutSharks()
    {
        foreach (Transform viewOrigin in sharkViewOrigins)
        {
            Vector3 pos = GetRandomSharkPosition();
            viewOrigin.position = pos;

            // Optional: Reset their forward direction to a random heading (so they don't all face the same way)
            float randomY = Random.Range(0f, 360f);
            viewOrigin.rotation = Quaternion.Euler(0f, randomY, 0f);
        }
    }

    // Utility: Get a random position in spawn bounds (water surface logic, etc.)
    Vector3 GetRandomSharkPosition()
    {
        Vector3 horizontal = GetRandomXZPosition();
        float surfaceY = GetWaterSurfaceY(horizontal, transform.position.y + 2f);
        float depth = Random.Range(0f, spawnHeight);
        return new Vector3(horizontal.x, surfaceY - depth, horizontal.z);
    }


    Vector3 GetSpawnPosition()
    {
        Vector3 horizontal = GetRandomXZPosition();
        float surfaceY = GetWaterSurfaceY(horizontal, transform.position.y + 2f);
        float depth = Random.Range(0f, spawnHeight);
        return new Vector3(horizontal.x, surfaceY - depth, horizontal.z);
    }

    Vector3 GetRandomXZPosition()
    {
        float x = Random.Range(-spawnWidth / 2f, spawnWidth / 2f);
        float z = Random.Range(-spawnDepth / 2f, spawnDepth / 2f);
        return new Vector3(transform.position.x + x, 0f, transform.position.z + z);
    }

    Vector3 GetRandomLocalOffset()
    {
        float x = Random.Range(-spawnWidth / 2f, spawnWidth / 2f);
        float y = -Random.Range(0f, spawnHeight);
        float z = Random.Range(-spawnDepth / 2f, spawnDepth / 2f);
        return new Vector3(x, y, z);
    }

    float GetWaterSurfaceY(Vector3 positionXZ, float fallback)
    {
        return WaterSurfaceScript.Instance != null
            ? WaterSurfaceScript.Instance.GetWaterSurfaceHeight(positionXZ)
            : fallback;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 center = transform.position - new Vector3(0f, spawnHeight / 2f, 0f);
        Vector3 size = new Vector3(spawnWidth, spawnHeight, spawnDepth);
        Gizmos.DrawWireCube(center, size);
    }
}

[System.Serializable]
public class RevealedFishTracker
{
    public HashSet<string> revealed = new HashSet<string>();
    private string SavePath => Path.Combine(Application.persistentDataPath, "revealed_fish.json");

    [System.Serializable]
    private class Wrapper { public List<string> set; }

    public void Load()
    {
        if (File.Exists(SavePath))
        {
            string json = File.ReadAllText(SavePath);
            var w = JsonUtility.FromJson<Wrapper>(json);
            if (w != null && w.set != null)
                revealed = new HashSet<string>(w.set);
        }
    }

    public void Save()
    {
        var wrap = new Wrapper { set = new List<string>(revealed) };
        string json = JsonUtility.ToJson(wrap, true);
        File.WriteAllText(SavePath, json);
    }

    public bool IsRevealed(string id) => revealed.Contains(id);
    public void MarkRevealed(string id)
    {
        if (revealed.Add(id)) Save();
    }
}
