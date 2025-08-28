using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class WaterEnvironmentManager : MonoBehaviour {
    public int width = 256, height = 256;
    public float[,] temperatureMap;
    public float[,] foodMap;
    public float[,] visibilityMap;
    public float[,] currentSpeedMap;
    public Vector2[,] currentDirMap;

    void Awake() {
        temperatureMap = new float[width, height];
        foodMap = new float[width, height];
        visibilityMap = new float[width, height];
        currentSpeedMap = new float[width, height];
        currentDirMap = new Vector2[width, height];
        InitializeMaps();
    }

    void InitializeMaps() {
        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                temperatureMap[x, y] = Mathf.Lerp(15f, 28f, (float)y / height);
                foodMap[x, y] = Random.Range(0f, 1f);
                visibilityMap[x, y] = Random.Range(0.5f, 1f);
                currentSpeedMap[x, y] = Random.Range(0.2f, 1f);
                float angle = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * Mathf.PI * 2;
                currentDirMap[x, y] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
        }
    }

    public Vector2Int WorldToGrid(Vector3 worldPos) {
        Vector3 local = worldPos - (transform.position - new Vector3(width / 2f, 0f, height / 2f));
        int x = Mathf.Clamp((int)local.x, 0, width - 1);
        int z = Mathf.Clamp((int)local.z, 0, height - 1);
        return new Vector2Int(x, z);
    }

    public float GetTemperature(Vector3 pos) => temperatureMap[WorldToGrid(pos).x, WorldToGrid(pos).y];
    public float GetFood(Vector3 pos) => foodMap[WorldToGrid(pos).x, WorldToGrid(pos).y];
    public float GetVisibility(Vector3 pos) => visibilityMap[WorldToGrid(pos).x, WorldToGrid(pos).y];
    public Vector2 GetCurrent(Vector3 pos) => currentDirMap[WorldToGrid(pos).x, WorldToGrid(pos).y] * currentSpeedMap[WorldToGrid(pos).x, WorldToGrid(pos).y];

    void OnDrawGizmosSelected() {
        if (temperatureMap == null) return;

        Vector3 origin = transform.position - new Vector3(width / 2f, 0f, height / 2f);

        for (int x = 0; x < width; x += 4) {
            for (int y = 0; y < height; y += 4) {
                float temp = temperatureMap[x, y];
                float normalizedTemp = Mathf.InverseLerp(15f, 28f, temp);
                Gizmos.color = Color.Lerp(Color.blue, Color.red, normalizedTemp);

                Vector3 pos = origin + new Vector3(x + 0.5f, -0.5f, y + 0.5f);
                Gizmos.DrawCube(pos, new Vector3(1f, 0.1f, 1f));
            }
        }

        Gizmos.color = Color.cyan;
        Vector3 center = transform.position;
        Vector3 size = new Vector3(width, 0.1f, height);
        Gizmos.DrawWireCube(center, size);
    }
}
