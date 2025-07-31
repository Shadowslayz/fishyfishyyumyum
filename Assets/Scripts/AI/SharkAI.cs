using UnityEngine;
using System;
using System.Collections.Generic;

using System.Collections;
public class SharkAI : MonoBehaviour
{
    [Header("References")]
    public Transform viewOrigin;
    public GameObject waterDeformationObject;
    public Animator animator; // <-- assign your Animator here!


    [Header("Model Offset (Optional)")]
    public Vector3 modelOffset = Vector3.zero;
    public Vector3 rotationOffsetEuler = new Vector3(0, 180, 0);

    [Header("Vision")]
    public float detectionRadius = 10f;
    public float fovAngle = 120f;
    public float verticalFOVAngle = 90f;
    public float fovDistance = 15f;

    [Header("Diving and Swimming")]
    public float normalSpeed = 5f;
    public float raffleSpeed = 1f;
    public float stealthSpeed = 2f;
    public float diveAmplitude = 2f;
    public float diveFrequency = 0.3f;
    public float minDepth = -0.5f;
    public float maxDepth = -20f;

    [Header("Shark Memory")]
    public float targetMemoryTime = 3f;

    [Header("Suck Strength")]
    public float fishPullStrength = 6f;
    public float planktonPullStrength = 3f;

    [Header("Spine Animation")]
    public Transform[] spineBones;
    public float waveSpeed = 4f;
    public float waveMagnitude = 20f;
    public float waveSpacing = 0.3f;
    

    [Header("Turning/Damping")]
    public float turnSpeed = 0.7f;
    public float chaseTurnSpeed = 1.3f;

    [Header("Roaming")]
    public float directionChangeInterval = 3f;
    private float directionChangeTimer = 0f;

    [Header("Sucking Timing")]
    public float suckHoldDuration = 3f;

    // --- Private state ---
    private float suckHoldTimer = 0f;
    private float waveOffset;
    private static Queue<WinnerEntry> winnerQueue = new Queue<WinnerEntry>();
    private float currentSpeed;
    private WaterEnvironmentManager waterEnv;
    private GameObject currentTarget;
    private float targetTimer;
    private float noTargetTimeout = 0f;
    private float lastSeenTargetTime = 0f; 
    private const float giveUpTime = 10f;
    private Coroutine postSuckRoutine;
    private float verticalOffset;
    private float smoothY = 0f, smoothVelocity = 0f;
    private Vector3 horizontalDirection;
    private bool wasLookingUp;
    private bool freezeWhileLookingUp;
    private Vector3 viewOriginVelocity = Vector3.zero;
    private bool mouthIsOpen = false;
    private bool isCurrentlySucking = false;
    private Quaternion heldSuckRotation;
    private bool holdingSuckRotation = false;




    void Start()
    {   
        currentSpeed = normalSpeed;
        waterEnv = UnityEngine.Object.FindFirstObjectByType<WaterEnvironmentManager>();
        verticalOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2);
        horizontalDirection = viewOrigin.forward;
        if (waterDeformationObject) waterDeformationObject.SetActive(false);
        waveOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

    }

    void Update()
    {   
        currentSpeed = RaffleHungerManager.Instance != null && RaffleHungerManager.Instance.IsRaffleMode ? raffleSpeed : normalSpeed;
        if (!viewOrigin) return;

        Vector3 eye = viewOrigin.position;
        Vector2Int grid = waterEnv.WorldToGrid(eye);

        // --- Out-of-bounds handling ---
        if (grid.x <= 1 || grid.y <= 1 || grid.x >= waterEnv.width - 2 || grid.y >= waterEnv.height - 2)
        {
            Vector3 center = waterEnv.transform.position;
            Vector3 back = (center - eye).normalized;
            viewOrigin.forward = Vector3.Lerp(viewOrigin.forward, back, Time.deltaTime * turnSpeed);
            Vector3 targetViewPos = viewOrigin.position + viewOrigin.forward * currentSpeed * Time.deltaTime;
            viewOrigin.position = Vector3.SmoothDamp(viewOrigin.position, targetViewPos, ref viewOriginVelocity, 0.3f);
            return;
        }

        float surfaceY = WaterSurfaceScript.Instance?.GetWaterSurfaceHeight(eye) ?? eye.y;
        float upperLimit = surfaceY + minDepth;
        float lowerLimit = surfaceY + maxDepth;
        float offsetY = Mathf.Sin(Time.time * diveFrequency + verticalOffset) * diveAmplitude;
        smoothY = Mathf.SmoothDamp(smoothY, offsetY, ref smoothVelocity, 0.5f);
        if (!(RaffleHungerManager.Instance != null && RaffleHungerManager.Instance.IsRaffleMode && RaffleHungerManager.Instance.Hunger <= 0f))
        {
            // --- Target searching logic ---
            if ((currentTarget == null || targetTimer <= 0f) && noTargetTimeout <= 0f)
            {
                Collider[] hits = Physics.OverlapSphere(eye, detectionRadius);
                GameObject bestTarget = null;
                float bestFishDist = float.MaxValue;
                float bestPlanktonDist = float.MaxValue;

                foreach (var hit in hits)
                {
                    Vector3 dir = hit.transform.position - eye;
                    float dist = dir.magnitude;
                    float horizAngle = Vector3.Angle(new Vector3(viewOrigin.forward.x, 0, viewOrigin.forward.z), new Vector3(dir.x, 0, dir.z));
                    float vertAngle = Vector3.Angle(new Vector3(0, viewOrigin.forward.y, viewOrigin.forward.z), new Vector3(0, dir.y, dir.z));

                    if (horizAngle <= fovAngle / 2f && vertAngle <= verticalFOVAngle / 2f && dist <= fovDistance)
                    {
                        if (hit.CompareTag("Fish") && dist < bestFishDist)
                        {
                            bestTarget = hit.gameObject;
                            bestFishDist = dist;
                        }
                        else if (hit.CompareTag("Plankton") && bestTarget == null && dist < bestPlanktonDist)
                        {
                            bestTarget = hit.gameObject;
                            bestPlanktonDist = dist;
                        }
                    }
                }

                if (bestTarget)
                {
                    currentTarget = bestTarget;
                    targetTimer = bestTarget.CompareTag("Plankton") ? targetMemoryTime : 0f;
                    noTargetTimeout = 1.5f;
                    lastSeenTargetTime = Time.time;
                }
            }
            else
            {
                targetTimer -= Time.deltaTime;
                noTargetTimeout -= Time.deltaTime;
            }            
        }


        freezeWhileLookingUp = false;

        // --- Target chase logic (move/rotate viewOrigin) ---
        if (currentTarget != null)
        {
            Vector3 toTarget = currentTarget.transform.position - eye;
            float speed = currentTarget.CompareTag("Fish") ? stealthSpeed : currentSpeed;
            float minDepthY = surfaceY + minDepth;
            float sharkAtSurface = Mathf.Abs(viewOrigin.position.y - minDepthY);

            // XZ horizontal distance between shark and fish
            Vector2 sharkXZ = new Vector2(viewOrigin.position.x, viewOrigin.position.z);
            Vector2 fishXZ = new Vector2(currentTarget.transform.position.x, currentTarget.transform.position.z);
            float horizontalDist = Vector2.Distance(sharkXZ, fishXZ);

            float verticalDist = toTarget.y;
            bool canSuck = currentTarget.CompareTag("Fish") &&
                        verticalDist > 0.2f &&
                        sharkAtSurface < 0.35f &&
                        horizontalDist < 0.5f;

            // --- Sucking logic with hold timer ---
            if (!isCurrentlySucking && canSuck)
            {
                // Start sucking!
                isCurrentlySucking = true;
                suckHoldTimer = suckHoldDuration;
                OpenMouth();
                if (waterDeformationObject) waterDeformationObject.SetActive(true);

                // Freeze all fish logic, physics, and colliders
                var fishAI = currentTarget.GetComponent<FishAI>();
                if (fishAI != null) fishAI.enabled = false;
                var rb = currentTarget.GetComponent<Rigidbody>();
                if (rb != null) {
                    rb.isKinematic = true;
                    rb.linearVelocity = Vector3.zero;
                }
                foreach (var col in currentTarget.GetComponentsInChildren<Collider>())
                    col.enabled = false;

                // Capture and "hold" the look direction at the start of sucking
                heldSuckRotation = Quaternion.LookRotation(currentTarget.transform.position - viewOrigin.position, Vector3.up);
                holdingSuckRotation = true;
            }

            // If sucking is active, hold for the timer
            if (isCurrentlySucking)
            {
                suckHoldTimer -= Time.deltaTime;

                // HOLD vertical orientation while sucking!
                if (holdingSuckRotation)
                    viewOrigin.rotation = Quaternion.Slerp(viewOrigin.rotation, heldSuckRotation, Time.deltaTime * chaseTurnSpeed);

                float lungeTargetY = Mathf.Min(currentTarget.transform.position.y, surfaceY + minDepth + 0.6f);
                viewOrigin.position = new Vector3(
                    viewOrigin.position.x,
                    Mathf.MoveTowards(viewOrigin.position.y, lungeTargetY, stealthSpeed * Time.deltaTime),
                    viewOrigin.position.z
                );
                freezeWhileLookingUp = true;

                // Pull fish to mouth
                float absVerticalDist = Mathf.Abs(toTarget.y);
                float distance = toTarget.magnitude;
                float dynamicPull = fishPullStrength + absVerticalDist * 2f;

                float moveStep = dynamicPull * Time.deltaTime;

                // If the step is greater than the distance, just snap it to the shark's mouth:
                if (moveStep >= distance)
                {
                    currentTarget.transform.position = eye; // Snap to mouth
                }
                else
                {
                    currentTarget.transform.position = Vector3.MoveTowards(
                    currentTarget.transform.position,
                    eye, // The shark's mouth
                    moveStep
                );
                }

                // Optionally clamp y to shark's mouth for the last bit:
                if (distance < 0.3f)
                {
                    Vector3 snap = currentTarget.transform.position;
                    snap.y = eye.y;
                    currentTarget.transform.position = snap;
                }

                // When timer ends or target is lost, stop sucking
                if (suckHoldTimer <= 0f || currentTarget == null)
                {
                    isCurrentlySucking = false;
                    holdingSuckRotation = false;
                    CloseMouth();
                    if (waterDeformationObject) waterDeformationObject.SetActive(false);
                }
            }
            else
            {
                // Not currently sucking - normal chase logic
                wasLookingUp = false;
                holdingSuckRotation = false;
                Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized);
                viewOrigin.rotation = Quaternion.Slerp(viewOrigin.rotation, targetRot, Time.deltaTime * chaseTurnSpeed);
                CloseMouth();
                if (waterDeformationObject) waterDeformationObject.SetActive(false);

                if (!freezeWhileLookingUp)
                    viewOrigin.position += viewOrigin.forward * speed * Time.deltaTime;
            }


            // --- Eat check ---
            if (Vector3.Distance(viewOrigin.position, currentTarget.transform.position) < 2.5f)
            {
                // Try to get FishMetadata (preferred way)
                FishMetadata meta = currentTarget.GetComponent<FishMetadata>();
                if (meta != null && !string.IsNullOrEmpty(meta.email)) {
                    if (RaffleHungerManager.Instance.IsRaffleMode) {
                        PreySpawner.Instance.EnqueueWinner(meta.id, meta.name, meta.email, meta.ig);
                    }
                }
                // Clean up as before
                Destroy(currentTarget);
                currentTarget = null;
                targetTimer = 0f;
                if (postSuckRoutine != null) StopCoroutine(postSuckRoutine);
                postSuckRoutine = StartCoroutine(DisableDeformationAfterDelay());
                CloseMouth();
                if (waterDeformationObject) waterDeformationObject.SetActive(false);
                isCurrentlySucking = false;
            }

            // --- Give up if target is lost for too long ---
            if (currentTarget != null)
            {
                float dist = Vector3.Distance(viewOrigin.position, currentTarget.transform.position);
                if (dist < detectionRadius * 0.6f)
                    lastSeenTargetTime = Time.time;
            }

            if (Time.time - lastSeenTargetTime > giveUpTime)
            {
                currentTarget = null;
                if (postSuckRoutine != null) StopCoroutine(postSuckRoutine);
                postSuckRoutine = StartCoroutine(DisableDeformationAfterDelay());
                CloseMouth();
                isCurrentlySucking = false;
            }

            // --- Clamp Y to minDepth, unless lunging/sucking ---
            if (!isCurrentlySucking)
            {
                float clampedY = Mathf.Min(viewOrigin.position.y, surfaceY + minDepth);
                viewOrigin.position = new Vector3(viewOrigin.position.x, clampedY, viewOrigin.position.z);
            }

            return;
        }

        else if (wasLookingUp)
        {
            wasLookingUp = false;
            CloseMouth();
        }

        if (waterDeformationObject && postSuckRoutine == null)
            waterDeformationObject.SetActive(false);

        // --- Roaming direction timer ---
        directionChangeTimer -= Time.deltaTime;
        if (directionChangeTimer <= 0f)
        {
            Vector3 newRoamDir;
            do {
                newRoamDir = new Vector3(
                    UnityEngine.Random.Range(-0.6f, 0.6f), 
                    0, 
                    UnityEngine.Random.Range(0.4f, 1f)
                );
            } while (newRoamDir.magnitude < 0.5f);
            horizontalDirection = newRoamDir.normalized;
            directionChangeTimer = directionChangeInterval;
        }
        // Smoothly keep heading toward the chosen direction
        viewOrigin.rotation = Quaternion.Slerp(
            viewOrigin.rotation,
            Quaternion.LookRotation(horizontalDirection),
            Time.deltaTime * turnSpeed
        );

        Vector3 targetViewPos2 = viewOrigin.position + viewOrigin.forward * currentSpeed * Time.deltaTime;
        viewOrigin.position = Vector3.SmoothDamp(viewOrigin.position, targetViewPos2, ref viewOriginVelocity, 0.3f);

        // --- Clamp Y to minDepth for roaming (never leap out of water) ---
        float downwardBias = smoothY;
        float roamClampedY = Mathf.Min(viewOrigin.position.y + downwardBias * Time.deltaTime, surfaceY + minDepth);
        viewOrigin.position = new Vector3(viewOrigin.position.x, roamClampedY, viewOrigin.position.z);
    }

    void LateUpdate()
    {
        if (!viewOrigin) return;
        transform.position = viewOrigin.position + viewOrigin.TransformDirection(modelOffset);
        transform.rotation = viewOrigin.rotation * Quaternion.Euler(rotationOffsetEuler);
        float surfaceY = WaterSurfaceScript.Instance?.GetWaterSurfaceHeight(viewOrigin.position) ?? viewOrigin.position.y;
        float maxY = surfaceY + minDepth;
        if (viewOrigin.position.y > maxY)
            viewOrigin.position = new Vector3(viewOrigin.position.x, maxY, viewOrigin.position.z);
        AnimateSpineWaveRotationZ();
    }

    void OpenMouth()
    {
        if (waterDeformationObject && !waterDeformationObject.activeSelf)
            waterDeformationObject.SetActive(true);
        if (animator && !mouthIsOpen)
        {
            animator.SetBool("OpenMouth", true);   // Opens mouth
            mouthIsOpen = true;
        }
    }

    void CloseMouth()
    {
        if (animator && mouthIsOpen)
        {
            animator.SetBool("OpenMouth", false);  // Closes mouth
            mouthIsOpen = false;
        }
        if (waterDeformationObject && waterDeformationObject.activeSelf)
            waterDeformationObject.SetActive(false);
    }

    private IEnumerator DisableDeformationAfterDelay()
    {
        yield return new WaitForSeconds(0.5f);
        if (waterDeformationObject) waterDeformationObject.SetActive(false);
        postSuckRoutine = null;
        CloseMouth();
    }

    void AnimateSpineWaveRotationZ()
    {
        if (spineBones == null || spineBones.Length == 0) return;
        float time = Time.time * waveSpeed + waveOffset;
        for (int i = 0; i < spineBones.Length; i++)
        {
            float offset = i * waveSpacing;
            float angle = Mathf.Sin(time - offset) * waveMagnitude;
            spineBones[i].localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    public bool IsPullingPlankton(GameObject plankton)
    {
        return currentTarget != null && currentTarget == plankton;
    }

    void OnDrawGizmosSelected()
    {
        if (!viewOrigin) return;
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Vector3 origin = viewOrigin.position;
        for (float angleY = -fovAngle / 2f; angleY <= fovAngle / 2f; angleY += 10f)
        {
            for (float angleX = -verticalFOVAngle / 2f; angleX <= verticalFOVAngle / 2f; angleX += 10f)
            {
                Quaternion rot = Quaternion.Euler(angleX, angleY, 0);
                Gizmos.DrawLine(origin, origin + rot * viewOrigin.forward * fovDistance);
            }
        }
        Gizmos.DrawWireSphere(origin, detectionRadius);
    }
}
