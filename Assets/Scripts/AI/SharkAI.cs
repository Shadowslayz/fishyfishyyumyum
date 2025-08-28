using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;

public class SharkAI : MonoBehaviour
{
    [Header("Roaming Center")]
    public Transform roamCenter; // <-- drag a GameObject here in Inspector

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
    public float maxDepth = -45f;

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
    public float roamRadius = 50f; // <-- Added roamRadius as a public variable
    private float directionChangeTimer = 0f;

    [Header("Sucking Timing")]
    public float suckHoldDuration = 3f;

    [Header("Cooldown")]
    public float eatCooldown = 5f; // cooldown duration after eating
    private float eatCooldownTimer = 0f;

    [Header("Shark Avoidance")]
    public float sharkAvoidanceRadius = 20f;   // distance to maintain
    public float sharkAvoidanceStrength = 3f; // how strongly they steer away

    [Header("Debug")]
    public bool debugDirections = false; // default is false

    // --- Private state ---

    private static List<SharkAI> allSharks = new List<SharkAI>();
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
    private Vector3 targetDirection = Vector3.forward;

private System.Random localRand;
private Vector3 roamTargetPoint;
private bool isQuadrantRedirect = false;
private Vector3 quadrantRedirectTarget;


    void Start()
    {
        currentSpeed = normalSpeed;
        waterEnv = UnityEngine.Object.FindFirstObjectByType<WaterEnvironmentManager>();
        verticalOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2);
// Unique RNG per shark (prevents them sharing the same Random sequence)
localRand = new System.Random(GetInstanceID() ^ DateTime.Now.Millisecond);

// Give each shark a random starting yaw rotation
float startYaw = (float)(localRand.NextDouble() * 360.0);
viewOrigin.rotation = Quaternion.Euler(0f, startYaw, 0f);

// Use that yaw to set initial direction
horizontalDirection = viewOrigin.forward;
targetDirection = horizontalDirection;

// Randomize the first direction change time so sharks don’t sync up
directionChangeTimer = (float)(localRand.NextDouble() * directionChangeInterval);


// Keep existing stuff
if (waterDeformationObject) waterDeformationObject.SetActive(false);

// Unique wave offset for spine animation
waveOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

allSharks.Add(this);


    }

    void Update()
    {
        currentSpeed = RaffleHungerManager.Instance != null && RaffleHungerManager.Instance.IsRaffleMode ? raffleSpeed : normalSpeed;
        if (!viewOrigin) return;

        // tick down cooldown timer
        if (eatCooldownTimer > 0f)
            eatCooldownTimer -= Time.deltaTime;

        Vector3 eye = viewOrigin.position;
        Vector2Int grid = waterEnv.WorldToGrid(eye);

        // --- Out-of-bounds handling ---
        if (grid.x <= 1 || grid.y <= 1 || grid.x >= waterEnv.width - 2 || grid.y >= waterEnv.height - 2)
        {
            Vector3 center = waterEnv.transform.position;
            Vector3 back = (center - eye).normalized;

            // Add small random bounce so all sharks don't turn the same way
            float randomAngle = UnityEngine.Random.Range(-25f, 25f);
            back = Quaternion.Euler(0f, randomAngle, 0f) * back;

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
            if (eatCooldownTimer <= 0f && (currentTarget == null || targetTimer <= 0f) && noTargetTimeout <= 0f)
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
                if (rb != null)
                {
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
                wasLookingUp = false;
                holdingSuckRotation = false;
                Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized);
                viewOrigin.rotation = Quaternion.Slerp(viewOrigin.rotation, targetRot, Time.deltaTime * chaseTurnSpeed);
                ApplySharkSeparation();
                CloseMouth();
                if (waterDeformationObject) waterDeformationObject.SetActive(false);

                if (!freezeWhileLookingUp)
                    viewOrigin.position += viewOrigin.forward * speed * Time.deltaTime;
            }


            // --- Eat check ---
            if (Vector3.Distance(viewOrigin.position, currentTarget.transform.position) < 2.5f)
            {
                FishMetadata meta = currentTarget.GetComponent<FishMetadata>();
                if (meta != null && !string.IsNullOrEmpty(meta.email))
                {
                    if (RaffleHungerManager.Instance.IsRaffleMode)
                    {
                        PreySpawner.Instance.EnqueueWinner(meta.id, meta.name, meta.email, meta.ig);
                    }
                }

                Destroy(currentTarget);
                currentTarget = null;
                targetTimer = 0f;

                eatCooldownTimer = eatCooldown; // <-- start cooldown here!

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

// --- Roaming direction (pick random point within area) ---
directionChangeTimer -= Time.deltaTime;
bool needNewTarget = false;

if (directionChangeTimer <= 0f)
{
    needNewTarget = true;
}
else if (Vector3.Distance(viewOrigin.position, roamTargetPoint) < 3f)
{
    // Arrived early, so reset timer and pick new target
    needNewTarget = true;
    directionChangeTimer = (float)(localRand.NextDouble() * directionChangeInterval);
}

if (needNewTarget)
{
    directionChangeTimer = (float)(localRand.NextDouble() * directionChangeInterval);

    // Use world center for roam area
    Vector3 center = roamCenter ? roamCenter.position : Vector3.zero;

    float bestX = 0f, bestZ = 0f, bestMinDist = -1f;
    int tries = 20;
    for (int i = 0; i < tries; i++)
    {
        float candidateX = UnityEngine.Random.Range(center.x - roamRadius, center.x + roamRadius);
        float candidateZ = UnityEngine.Random.Range(center.z - roamRadius, center.z + roamRadius);
        Vector3 candidate = new Vector3(candidateX, 0f, candidateZ);

        float minDist = float.MaxValue;
        foreach (var other in allSharks)
        {
            if (other == this) continue;
            float dist = Vector3.Distance(candidate, new Vector3(other.roamTargetPoint.x, 0f, other.roamTargetPoint.z));
            if (dist < minDist) minDist = dist;
        }

        if (minDist > bestMinDist)
        {
            bestMinDist = minDist;
            bestX = candidateX;
            bestZ = candidateZ;
        }
    }

    // Use fixed max depth of -45 for Y
    float centerY = center.y;
    float minY = centerY + minDepth;
    float maxY = centerY - 45f;
    float randomY = UnityEngine.Random.Range(maxY, minY);

    roamTargetPoint = new Vector3(bestX, randomY, bestZ);

    if (debugDirections)
    {
        Debug.Log($"[{name}] New roamTargetPoint: {roamTargetPoint}");
    }
}

// Always steer toward the current roam target point
targetDirection = (roamTargetPoint - viewOrigin.position).normalized;


// --- Wall avoidance ---
Vector3 pos = viewOrigin.position;
float margin = 5f; // still 5 units
Vector3 wallAvoidance = Vector3.zero;

if (pos.x < margin) wallAvoidance += Vector3.right;
if (pos.x > waterEnv.width - margin) wallAvoidance += Vector3.left;
if (pos.z < margin) wallAvoidance += Vector3.forward;
if (pos.z > waterEnv.height - margin) wallAvoidance += Vector3.back;

if (wallAvoidance != Vector3.zero)
{
    // Stronger force away from wall
    targetDirection = (wallAvoidance * 3f).normalized; // Multiplied for stronger effect
}
else
{
    // Normal roaming direction
    targetDirection = (roamTargetPoint - viewOrigin.position).normalized;
}

// --- Quadrant redirect if out of bounds ---
Vector3 worldCenter = roamCenter ? roamCenter.position : Vector3.zero;

if (Vector3.Distance(pos, worldCenter) > roamRadius)
{
    if (!isQuadrantRedirect)
    {
        isQuadrantRedirect = true;

        float centerX = worldCenter.x;
        float centerZ = worldCenter.z;
        float centerY = worldCenter.y;

        // Determine which corner is opposite
        float targetX = pos.x < centerX ? centerX + roamRadius : centerX - roamRadius;
        float targetZ = pos.z < centerZ ? centerZ + roamRadius : centerZ - roamRadius;

        // Vector from center to opposite corner
        Vector3 center = new Vector3(centerX, 0f, centerZ);
        Vector3 corner = new Vector3(targetX, 0f, targetZ);
        Vector3 toCorner = corner - center;

        // Pick a random point between center and corner (not all the way)
        float t = UnityEngine.Random.Range(0.5f, 0.85f); // 50% to 85% toward the corner
        Vector3 randomPoint = center + toCorner * t;

        // Clamp to stay away from the very edge/corner (e.g., 15 units from edge)
        float edgeBuffer = 15f;
        float clampedX = Mathf.Clamp(randomPoint.x, centerX - roamRadius + edgeBuffer, centerX + roamRadius - edgeBuffer);
        float clampedZ = Mathf.Clamp(randomPoint.z, centerZ - roamRadius + edgeBuffer, centerZ + roamRadius - edgeBuffer);

        // Y range
        float minY = centerY + minDepth;
        float maxY = centerY - 45f;
        float randomY = UnityEngine.Random.Range(maxY, minY);

        quadrantRedirectTarget = new Vector3(clampedX, randomY, clampedZ);

        if (debugDirections)
        {
            Debug.Log($"[{name}] Out of bounds! Redirecting to safe zone near opposite quadrant: {quadrantRedirectTarget}");
        }
    }
}

// Handle quadrant redirect
if (isQuadrantRedirect)
{
    targetDirection = (quadrantRedirectTarget - viewOrigin.position).normalized;

    // If shark is back in bounds AND close to the target, stop redirect
    if ((Vector3.Distance(viewOrigin.position, worldCenter) <= roamRadius) &&
        Vector3.Distance(viewOrigin.position, quadrantRedirectTarget) < 3f)
    {
        isQuadrantRedirect = false;
    }
}


        // Smoothly blend horizontalDirection toward targetDirection
        horizontalDirection = Vector3.Slerp(horizontalDirection, targetDirection, Time.deltaTime * 0.5f);

        // Smoothly rotate shark toward that direction
        viewOrigin.rotation = Quaternion.Slerp(
            viewOrigin.rotation,
            Quaternion.LookRotation(horizontalDirection),
            Time.deltaTime * turnSpeed
        );

        // Move shark forward
        ApplySharkSeparation();
        Vector3 targetViewPos2 = viewOrigin.position + viewOrigin.forward * currentSpeed * Time.deltaTime;
        viewOrigin.position = Vector3.SmoothDamp(viewOrigin.position, targetViewPos2, ref viewOriginVelocity, 0.3f);

        // Add vertical wave-like diving
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
            animator.SetBool("OpenMouth", true);
            mouthIsOpen = true;
        }
    }

    void CloseMouth()
    {
        if (animator && mouthIsOpen)
        {
            animator.SetBool("OpenMouth", false);
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

    int boneCount = spineBones.Length;
    for (int i = 0; i < boneCount; i++)
    {
        float offset = i * waveSpacing;

        // ✅ Make wave amplitude increase toward the tail
        float tailFactor = (float)i / (boneCount - 1); // 0 at head, 1 at tail

        float angle = Mathf.Sin(time - offset) * waveMagnitude * tailFactor;

        // ✅ Smooth wave without extra random phase jitter
        spineBones[i].localRotation = Quaternion.Euler(0f, 0f, angle);
    }
}


    public bool IsPullingPlankton(GameObject plankton)
    {
        return currentTarget != null && currentTarget == plankton;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 origin = viewOrigin.position;
        // if (!viewOrigin) return;
        // Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        // for (float angleY = -fovAngle / 2f; angleY <= fovAngle / 2f; angleY += 10f)
        // {
        //     for (float angleX = -verticalFOVAngle / 2f; angleX <= verticalFOVAngle / 2f; angleX += 10f)
        //     {
        //         Quaternion rot = Quaternion.Euler(angleX, angleY, 0);
        //         Gizmos.DrawLine(origin, origin + rot * viewOrigin.forward * fovDistance);
        //     }
        // }
        // Gizmos.DrawWireSphere(origin, detectionRadius);

        // Smoothed movement direction (green)
        Gizmos.color = Color.green;
        Gizmos.DrawLine(origin, origin + horizontalDirection.normalized * 5f);

        // Target direction (cyan) -- always relative to shark's position
        Gizmos.color = Color.cyan;
        Vector3 targetDirWorld = origin + (targetDirection.normalized * 7f);
        Gizmos.DrawLine(origin, targetDirWorld);

        // Actual movement direction (red)
        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin, origin + viewOrigin.forward.normalized * 6f);

        // Center bias (magenta)
        if (roamCenter != null)
        {
            Vector3 toCenter = (roamCenter.position - origin).normalized;
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(origin, origin + toCenter * 10f);
        }
    }
    void ApplySharkSeparation()
    {
        if (!viewOrigin) return;

        Vector3 avoidance = Vector3.zero;
        int count = 0;

        // Current shark XZ position
        Vector3 myPosXZ = new Vector3(viewOrigin.position.x, 0f, viewOrigin.position.z);

        foreach (var other in allSharks)
        {
            if (other == this || !other.viewOrigin) continue;

            Vector3 otherPosXZ = new Vector3(other.viewOrigin.position.x, 0f, other.viewOrigin.position.z);
            float dist = Vector3.Distance(myPosXZ, otherPosXZ);

            if (dist < sharkAvoidanceRadius && dist > 0.01f)
            {
                // Push away more strongly the closer they are
                Vector3 away = (myPosXZ - otherPosXZ).normalized;
                avoidance += away * (1f - (dist / sharkAvoidanceRadius));
                count++;
            }
        }

        if (count > 0)
        {
            avoidance /= count;
            avoidance.y = 0f;

            Vector3 desiredDir = avoidance.normalized;

            // Smooth steering (no snap)
            Vector3 flatForward = new Vector3(viewOrigin.forward.x, 0f, viewOrigin.forward.z);
            viewOrigin.forward = Vector3.Slerp(flatForward, desiredDir, Time.deltaTime * turnSpeed * sharkAvoidanceStrength);

            // Smoothly update roaming direction as well
            horizontalDirection = Vector3.Slerp(horizontalDirection, desiredDir, Time.deltaTime * turnSpeed);
        }
    }
}
