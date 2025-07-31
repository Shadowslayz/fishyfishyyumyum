using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.Rendering.HighDefinition;

public class FloatingVFXController : MonoBehaviour
{
    public VisualEffect vfx;               // The VFX Graph component
    public WaterSurface waterSurface;      // Assign your HDRP Water Surface
    public float offsetY = 0.1f;           // Slight height above water surface

    WaterSearchParameters searchParams = new WaterSearchParameters();
    WaterSearchResult searchResult = new WaterSearchResult();

    void Update()
    {
        if (waterSurface == null || vfx == null) return;

        // Prepare water search
        searchParams.startPositionWS = searchResult.candidateLocationWS;
        searchParams.targetPositionWS = transform.position;
        searchParams.error = 0.01f;
        searchParams.maxIterations = 8;

        // Project onto water
        if (waterSurface.ProjectPointOnWaterSurface(searchParams, out searchResult))
        {
            Vector3 projected = searchResult.projectedPositionWS;
            projected.y += offsetY;
            transform.position = projected;
            vfx.SetVector3("CenterPosition", projected);
        }
    }
}
