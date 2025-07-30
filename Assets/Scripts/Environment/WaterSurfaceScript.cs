using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[ExecuteAlways]
public class WaterSurfaceScript : MonoBehaviour {
    public static WaterSurfaceScript Instance { get; private set; }
    private Camera mainCam;

    void Awake() {
        Instance = this;
        mainCam = Camera.main;
    }

    public float GetWaterSurfaceHeight(Vector3 position) {
        if (!mainCam || !TryGetComponent(out UnityEngine.Rendering.HighDefinition.WaterSurface surface))
            return position.y;

        WaterSearchParameters searchParams = new WaterSearchParameters {
            startPositionWS = position + Vector3.up * 50f,
            targetPositionWS = position - Vector3.up * 50f,
        };

        if (surface.ProjectPointOnWaterSurface(searchParams, out WaterSearchResult result)) {
            return result.projectedPositionWS.y;
        }

        return position.y;
    }
}