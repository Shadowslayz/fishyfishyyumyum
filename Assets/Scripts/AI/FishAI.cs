using UnityEngine;

public class FishAI : MonoBehaviour {
    public float speed = 3f;
    public float changeInterval = 2f;
    public float surfaceOffset = 0.5f;
    public float diveStrength = 1.5f;

    [Header("Spine Animation")]
    public Transform[] spineBones;
    public float waveSpeed = 4f;
    public float waveMagnitude = 20f; // degrees
    public float waveSpacing = 0.3f;

    [Header("Life Settings")]
    public float lifeTimeMinutes = 2f;

    public bool beingSucked = false;

    private float waveTimeOffset;
    private float timer;
    private float redirectTimer = 0f;
    private float lifeTimer = 0f;
    private bool expired = false;
    private Vector3 direction;
    private WaterEnvironmentManager waterEnv;
    private PreySpawner spawner;

    void Start() {
        waterEnv = Object.FindFirstObjectByType<WaterEnvironmentManager>();
        spawner = Object.FindFirstObjectByType<PreySpawner>();
        changeInterval += Random.Range(-0.5f, 0.5f);
        waveTimeOffset = Random.Range(0f, Mathf.PI * 2f);
        PickNewDirection();
    }

    void Update() {
        AnimateSpineWaveX();
        if (beingSucked) return;
        // Handle lifetime
        if (!expired)
        {
            lifeTimer += Time.deltaTime;
            if (lifeTimer >= lifeTimeMinutes * 60f)
            {
                expired = true;
            }
        }

        if (expired) {
            // Fall down until -700, then destroy
            transform.position += Vector3.down * 50f * Time.deltaTime;
            if (transform.position.y <= -700f) {
                Destroy(gameObject);
            }
            return;
        }

        timer += Time.deltaTime;
        if (timer >= changeInterval && redirectTimer <= 0f) {
            PickNewDirection();
        }

        Vector3 pos = transform.position;
        Vector3 center = spawner.transform.position;
        Vector3 halfSize = new Vector3(spawner.spawnWidth / 2f, spawner.spawnHeight, spawner.spawnDepth / 2f);
        Vector3 relativePos = pos - center;

        bool outOfBounds =
            Mathf.Abs(relativePos.x) > halfSize.x ||
            Mathf.Abs(relativePos.z) > halfSize.z ||
            relativePos.y > 0 || relativePos.y < -spawner.spawnHeight;

        if (outOfBounds || redirectTimer > 0f) {
            Vector3 toCenter = (center - pos).normalized;
            direction = Vector3.Lerp(direction, toCenter, Time.deltaTime * 2f);
            redirectTimer += Time.deltaTime;
            if (redirectTimer > 2f) redirectTimer = 0f;
        }

        if (WaterSurfaceScript.Instance != null) {
            float surfaceY = WaterSurfaceScript.Instance.GetWaterSurfaceHeight(transform.position) - surfaceOffset;
            if (transform.position.y > surfaceY) {
                transform.position += Vector3.down * diveStrength * Time.deltaTime;
            }
        }

        transform.forward = Vector3.Lerp(transform.forward, direction.normalized, Time.deltaTime * 2f);
        Vector3 newPos = transform.position + transform.forward * speed * Time.deltaTime;

        newPos.x = Mathf.Clamp(newPos.x, center.x - halfSize.x, center.x + halfSize.x);
        newPos.y = Mathf.Clamp(newPos.y, center.y - halfSize.y, center.y);
        newPos.z = Mathf.Clamp(newPos.z, center.z - halfSize.z, center.z + halfSize.z);

        transform.position = newPos;

        if (WaterSurfaceScript.Instance != null) {
            float surfaceY = WaterSurfaceScript.Instance.GetWaterSurfaceHeight(transform.position) - surfaceOffset;
            float distanceAbove = transform.position.y - surfaceY;

            if (distanceAbove > 0.05f && distanceAbove < 1f) {
                Vector3 corrected = transform.position;
                corrected.y = Mathf.Lerp(corrected.y, surfaceY, Time.deltaTime * 3f);
                transform.position = corrected;
            } else if (distanceAbove >= 1f) {
                Vector3 corrected = transform.position;
                corrected.y = surfaceY;
                transform.position = corrected;
            }
        }
    }

    void PickNewDirection() {
        direction = new Vector3(Random.Range(-1f, 1f), Random.Range(-0.2f, 0.2f), Random.Range(-1f, 1f)).normalized;
        timer = 0f;
    }

    void AnimateSpineWaveX() {
        float time = (Time.time + waveTimeOffset) * waveSpeed;

        for (int i = 0; i < spineBones.Length; i++) {
            float offset = i * waveSpacing;
            float angle = Mathf.Sin(time - offset) * waveMagnitude;
            spineBones[i].localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
