using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.Rendering.HighDefinition;

public class FloatingVFXController : MonoBehaviour
{
    [Header("VFX and Water")]
    public VisualEffect vfx;
    public WaterSurface waterSurface;

    [Header("Offset Above Water")]
    public float offsetY = 0.2f;

    private WaterSearchParameters search;
    private WaterSearchResult result;

    void Start()
    {
        search = new WaterSearchParameters();
    }

    void Update()
    {
        search.startPosition = transform.position;

        // Get water height
        waterSurface.FindWaterSurfaceHeight(search, out result);

        // Move GameObject to water height
        Vector3 newPosition = transform.position;
        newPosition.y = result.height + offsetY;
        transform.position = newPosition;

        // Send to VFX Graph
        if (vfx != null)
        {
            vfx.SetVector3("CenterPosition", newPosition);
        }
    }
}
