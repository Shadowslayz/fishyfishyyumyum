using UnityEngine;
using UnityEngine.InputSystem;

public class DayNightCycle : MonoBehaviour
{
    [Header("Assign your .inputactions asset")]
    public InputActionAsset inputActionsAsset;

    [Header("Celestial References")]
    public Transform celestialRotator;
    public Light sun1;
    public Light sun2;
    public Light moon1;
    public Light moon2;

    [Header("Cycle Settings")]
    [Range(0f, 1f)]
    public float initialNormalizedTime = 0f;
    public float daySpeed = 10f;
    public float nightSpeed = 40f;

    [Header("Glow Settings (by Tag)")]
    public string glowTag = "FISH";
    public float dayEmissionIntensity = 0f;
    public float nightEmissionIntensity = 5f;

    private bool isNight = false;
    private GameObject[] glowingCreatures;
    private InputAction toggleMoonAction;
    private System.Action<InputAction.CallbackContext> toggleMoonHandler;

    void Start()
    {
        // Set initial sky rotation
        float initialAngle = initialNormalizedTime * 360f;
        celestialRotator.localRotation = Quaternion.Euler(initialAngle, 0f, 0f);

        // Load tagged creatures
        glowingCreatures = GameObject.FindGameObjectsWithTag(glowTag);

        // Setup input
        var map = inputActionsAsset.FindActionMap("General", true);
        toggleMoonAction = map.FindAction("ToggleMoon", true);
        toggleMoonHandler = ctx => ToggleNightMode();
        toggleMoonAction.performed += toggleMoonHandler;
        toggleMoonAction.Enable();

        // Initialize
        UpdateSunMoonLights();
        UpdateShadows();
        UpdateGlowState();
    }

    private void OnDisable()
    {
        if (toggleMoonAction != null)
        {
            toggleMoonAction.performed -= toggleMoonHandler;
            toggleMoonAction.Disable();
        }
    }

    void Update()
    {
        float speed = isNight ? nightSpeed : daySpeed;
        celestialRotator.Rotate(Vector3.right, speed * Time.deltaTime);
    }

    public void ToggleNightMode()
    {
        isNight = !isNight;
        UpdateSunMoonLights();
        UpdateShadows();
        RefreshGlowingFishes();
    }

    public void RefreshGlowingFishes()
    {
        glowingCreatures = GameObject.FindGameObjectsWithTag(glowTag);
        UpdateGlowState();
    }

    void UpdateGlowState()
    {
        foreach (GameObject obj in glowingCreatures)
        {
            if (obj == null) continue;

            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                foreach (Material mat in renderer.materials)
                {
                    if (mat.HasProperty("_EmissiveColor") && mat.HasProperty("_BaseColor"))
                    {
                        Color baseColor = mat.GetColor("_BaseColor");
                        Color glow = baseColor * (isNight ? nightEmissionIntensity : dayEmissionIntensity);
                        mat.SetColor("_EmissiveColor", glow);
                        mat.EnableKeyword("_EMISSIVE_COLOR");
                        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    }
                }
            }
        }
    }

    void UpdateSunMoonLights()
    {
        // Turn on/off appropriate lights
        sun1.enabled = !isNight;
        sun2.enabled = !isNight;
        moon1.enabled = isNight;
        moon2.enabled = isNight;

        // Set primary sun for RenderSettings
        RenderSettings.sun = isNight ? moon1 : sun1;

        Debug.Log($"[DayNight] isNight = {isNight}");
        Debug.Log($"Sun1 enabled: {sun1.enabled}, Moon1 enabled: {moon1.enabled}");
    }

    void UpdateShadows()
    {
        if (isNight)
        {
            // Night: only one moon can cast shadows
            bool moon1Visible = Vector3.Dot(moon1.transform.forward, Vector3.down) > 0f;
            bool moon2Visible = Vector3.Dot(moon2.transform.forward, Vector3.down) > 0f;

            moon1.shadows = moon1Visible ? LightShadows.Soft : LightShadows.None;
            moon2.shadows = moon2Visible ? LightShadows.Soft : LightShadows.None;

            sun1.shadows = LightShadows.None;
            sun2.shadows = LightShadows.None;
        }
        else
        {
            // Day: only one sun can cast shadows
            bool sun1Visible = Vector3.Dot(sun1.transform.forward, Vector3.down) > 0f;
            bool sun2Visible = Vector3.Dot(sun2.transform.forward, Vector3.down) > 0f;

            sun1.shadows = sun1Visible ? LightShadows.Soft : LightShadows.None;
            sun2.shadows = sun2Visible ? LightShadows.Soft : LightShadows.None;

            moon1.shadows = LightShadows.None;
            moon2.shadows = LightShadows.None;
        }
    }
}
