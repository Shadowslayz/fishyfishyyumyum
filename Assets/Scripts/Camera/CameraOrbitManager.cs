using UnityEngine;
using System.Collections;

public class CameraOrbitManager : MonoBehaviour
{
    public static CameraOrbitManager Instance { get; private set; }
    public bool IsOrbiting { get; private set; } = false;

    [Header("Orbit")]
    public Transform orbitCamera;      // Assign your main camera or a pivot in Inspector
    public float orbitDuration = 5f;
    public float rotationSpeed = 1f; // Or whatever default speed you want

    private Vector3 prevPosition;
    private Quaternion prevRotation;

    void Awake() {
        if (Instance != null && Instance != this) Destroy(gameObject);
        Instance = this;
    }

    public void OrbitAroundFish(Transform target, System.Action onDone = null)
    {
        if (!IsOrbiting) StartCoroutine(OrbitRoutine(target, onDone));
    }

    private IEnumerator OrbitRoutine(Transform target, System.Action onDone)
    {
        IsOrbiting = true;

        // Save camera state
        prevPosition = orbitCamera.position;
        prevRotation = orbitCamera.rotation;

        // Simple orbit animation
        float elapsed = 0f;
        float orbitRadius = 5f;
        float orbitHeight = 2f;
        while (elapsed < orbitDuration)
        {
            float angle = rotationSpeed * 360f * (elapsed / orbitDuration);
            Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * orbitRadius;
            orbitCamera.position = target.position + offset + Vector3.up * orbitHeight;
            orbitCamera.LookAt(target.position + Vector3.up * 1.0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Restore camera
        orbitCamera.position = prevPosition;
        orbitCamera.rotation = prevRotation;
        IsOrbiting = false;
        onDone?.Invoke();
    }
}
