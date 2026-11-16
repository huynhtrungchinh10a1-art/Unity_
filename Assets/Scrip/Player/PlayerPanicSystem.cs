using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(HealthAndTeam))]
public class PlayerPanicSystem : MonoBehaviour
{
    public static PlayerPanicSystem Instance { get; private set; }

    [Header("Panic & Courage Stats")]
    public float currentPanic = 0f;

    public float maxCourageCap = 100f;

    [Header("Proximity Detection (Kẻ địch ở gần)")]
    public float outerZoneRadius = 12f;

    public float dangerZoneRadius = 3.5f;

    public float scanInterval = 0.2f;

    public LayerMask characterLayerMask;

    [Header("Panic Rates (Tốc độ tăng/giảm sợ hãi)")]
    public float panicRatePerOuterEnemy = 1.0f;

    public float dangerZoneMultiplier = 3.5f;

    public float allyReliefPerPerson = 0.5f;

    public float calmDownRate = 4.0f;

    [Header("Visual Witness Death (Đăng ký onDie cục bộ)")]
    public float witnessMaxDistance = 22f;

    public Camera playerCamera;

    public float panicOnWitnessAllyDeath = 15f;

    [Header("Audio Settings")]
    public AudioSource heartbeatAudioSource;
    public AudioSource breathingAudioSource;
    public AudioClip heartbeatClip;
    public AudioClip heavyBreathingClip;

    [Range(0f, 1f)] public float maxHeartbeatVolume = 1.0f;
    [Range(0f, 1f)] public float maxBreathingVolume = 0.85f;
    public float audioFadeSpeed = 3.0f;
    public float heartbeatStartThreshold = 25f;

    [Header("Debug Status (Read-Only)")]
    [SerializeField] private int debugOuterEnemies = 0;
    [SerializeField] private int debugInnerEnemies = 0;
    [SerializeField] private int debugAlliesNearby = 0;
    [SerializeField] private int debugTrackedAlliesCount = 0;
    [SerializeField] private float debugCurrentPanicRate = 0f;

    private HealthAndTeam myHealth;
    private CharacterController controller;
    private float scanTimer = 0f;

    private readonly Collider[] scanCollidersBuffer = new Collider[64];

    // Danh sách các đồng đội trong bán kính đang được theo dõi sự kiện onDie
    private readonly Dictionary<HealthAndTeam, System.Action> trackedAllies = new Dictionary<HealthAndTeam, System.Action>();
    private readonly HashSet<HealthAndTeam> currentScanAllies = new HashSet<HealthAndTeam>();
    private readonly List<HealthAndTeam> alliesToRemove = new List<HealthAndTeam>();

    public System.Action<float, float> onPanicChanged;
    public float PanicNormalized => Mathf.Clamp01(currentPanic / Mathf.Max(maxCourageCap, 1f));

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(this); return; }

        myHealth = GetComponent<HealthAndTeam>();
        controller = GetComponent<CharacterController>();

        if (characterLayerMask.value == 0)
            characterLayerMask = LayerMask.GetMask("Character");

        SetupAudioSources();
    }

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    void OnDisable()
    {
        CleanupTrackedAllies();
    }

    void OnDestroy()
    {
        CleanupTrackedAllies();
    }

    void Update()
    {
        if (myHealth != null && !myHealth.isAlive)
        {
            StopAllAudio();
            CleanupTrackedAllies();
            return;
        }

        // Quét cục bộ định kỳ (0.2s/lần) - siêu nhẹ, chỉ 5 lần/giây
        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = scanInterval;
            PerformLocalScan();
        }

        UpdatePanicLevel();
        UpdateAudio();
    }


    private void PerformLocalScan()
    {
        debugOuterEnemies = 0;
        debugInnerEnemies = 0;
        debugAlliesNearby = 0;

        float maxScanRadius = Mathf.Max(outerZoneRadius, witnessMaxDistance);
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, maxScanRadius, scanCollidersBuffer, characterLayerMask);

        currentScanAllies.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = scanCollidersBuffer[i];
            if (col == null) continue;

            HealthAndTeam h = col.GetComponentInParent<HealthAndTeam>();
            if (h == null || h == myHealth || !h.isAlive) continue;

            float distance = Vector3.Distance(transform.position, h.transform.position);

            // 1. Kẻ địch
            if (myHealth.IsEnemy(h.teamCurrent))
            {
                if (distance <= outerZoneRadius)
                {
                    if (distance <= dangerZoneRadius)
                        debugInnerEnemies++;
                    else
                        debugOuterEnemies++;
                }
            }
            // 2. Đồng đội
            else
            {
                if (distance <= outerZoneRadius)
                {
                    debugAlliesNearby++;
                }

                // Nếu đồng đội nằm trong tầm mắt (witnessMaxDistance)
                if (distance <= witnessMaxDistance)
                {
                    currentScanAllies.Add(h);

                    if (!trackedAllies.ContainsKey(h))
                    {
                        System.Action deathCallback = () => HandleTrackedAllyDied(h);
                        h.onDie += deathCallback;
                        trackedAllies.Add(h, deathCallback);
                    }
                }
            }
        }

        alliesToRemove.Clear();
        foreach (var kvp in trackedAllies)
        {
            if (kvp.Key == null || !currentScanAllies.Contains(kvp.Key))
            {
                alliesToRemove.Add(kvp.Key);
            }
        }

        for (int i = 0; i < alliesToRemove.Count; i++)
        {
            HealthAndTeam ally = alliesToRemove[i];
            if (ally != null && trackedAllies.TryGetValue(ally, out var action))
            {
                ally.onDie -= action;
            }
            trackedAllies.Remove(ally);
        }

        debugTrackedAlliesCount = trackedAllies.Count;
    }


    private void HandleTrackedAllyDied(HealthAndTeam deadAlly)
    {
        if (deadAlly == null) return;

        // Gỡ bỏ đăng ký ngay khi đồng đội chết
        if (trackedAllies.TryGetValue(deadAlly, out var action))
        {
            deadAlly.onDie -= action;
            trackedAllies.Remove(deadAlly);
        }

        // Kiểm tra xem Camera của người chơi lúc này CÓ ĐANG NHÌN THẤY đồng đội bị chém chết không
        if (IsTargetVisibleInCamera(deadAlly.transform.position))
        {
            AddPanic(panicOnWitnessAllyDeath);
            Debug.Log($"<color=red>[Panic]</color> Tận mắt chứng kiến đồng đội {deadAlly.gameObject.name} hy sinh trước mắt! +{panicOnWitnessAllyDeath} Sợ Hãi.");
        }
    }

    private bool IsTargetVisibleInCamera(Vector3 worldPos)
    {
        Camera cam = playerCamera != null ? playerCamera : Camera.main;
        if (cam == null) return false;

        Vector3 targetPoint = worldPos + Vector3.up * 1.0f;
        Vector3 camToTarget = targetPoint - cam.transform.position;
        float dist = camToTarget.magnitude;

        // 1. Kiểm tra Viewport (trong khung hình màn hình)
        Vector3 viewPos = cam.WorldToViewportPoint(targetPoint);
        bool inScreen = viewPos.z > 0 && viewPos.x >= 0f && viewPos.x <= 1f && viewPos.y >= 0f && viewPos.y <= 1f;
        if (!inScreen) return false;

        // 2. Bắn Raycast kiểm tra Line of Sight (không bị vách đá/tường chắn)
        int obstructionMask = ~LayerMask.GetMask("Character", "Ignore Raycast");
        if (Physics.Raycast(cam.transform.position, camToTarget.normalized, out RaycastHit hit, dist, obstructionMask, QueryTriggerInteraction.Ignore))
        {
            return false; // Bị che khuất bởi địa hình
        }

        return true;
    }

    private void UpdatePanicLevel()
    {
        float previousPanic = currentPanic;

        if (debugInnerEnemies > 0 || debugOuterEnemies > 0)
        {
            float outerInflow = debugOuterEnemies * panicRatePerOuterEnemy;
            float innerInflow = debugInnerEnemies * (panicRatePerOuterEnemy * dangerZoneMultiplier);
            float allyRelief = debugAlliesNearby * allyReliefPerPerson;

            debugCurrentPanicRate = Mathf.Max(0f, (outerInflow + innerInflow) - allyRelief);
            currentPanic += debugCurrentPanicRate * Time.deltaTime;
        }
        else
        {
            debugCurrentPanicRate = -calmDownRate;
            currentPanic -= calmDownRate * Time.deltaTime;
        }

        currentPanic = Mathf.Clamp(currentPanic, 0f, maxCourageCap);

        if (Mathf.Abs(previousPanic - currentPanic) > 0.01f)
            onPanicChanged?.Invoke(currentPanic, maxCourageCap);
    }

    public void AddPanic(float amount)
    {
        currentPanic = Mathf.Clamp(currentPanic + amount, 0f, maxCourageCap);
        onPanicChanged?.Invoke(currentPanic, maxCourageCap);
    }

    private void CleanupTrackedAllies()
    {
        foreach (var kvp in trackedAllies)
        {
            if (kvp.Key != null && kvp.Value != null)
            {
                kvp.Key.onDie -= kvp.Value;
            }
        }
        trackedAllies.Clear();
        debugTrackedAlliesCount = 0;
    }

    #region Audio System

    private void SetupAudioSources()
    {
        if (heartbeatAudioSource == null)
        {
            heartbeatAudioSource = gameObject.AddComponent<AudioSource>();
            heartbeatAudioSource.playOnAwake = false;
            heartbeatAudioSource.loop = true;
            heartbeatAudioSource.spatialBlend = 0f;
        }

        if (breathingAudioSource == null)
        {
            breathingAudioSource = gameObject.AddComponent<AudioSource>();
            breathingAudioSource.playOnAwake = false;
            breathingAudioSource.loop = true;
            breathingAudioSource.spatialBlend = 0f;
        }

        if (heartbeatClip != null && heartbeatAudioSource.clip == null)
            heartbeatAudioSource.clip = heartbeatClip;

        if (heavyBreathingClip != null && breathingAudioSource.clip == null)
            breathingAudioSource.clip = heavyBreathingClip;
    }

    private void UpdateAudio()
    {
        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        bool isMoving = (controller != null) ? (new Vector3(controller.velocity.x, 0, controller.velocity.z).sqrMagnitude > 0.1f)
                                             : (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f);

        if (breathingAudioSource != null && breathingAudioSource.clip != null)
        {
            float targetBreathingVol = (isRunning && isMoving) ? maxBreathingVolume : 0f;
            breathingAudioSource.pitch = Mathf.Lerp(1.0f, 1.25f, PanicNormalized);
            breathingAudioSource.volume = Mathf.MoveTowards(breathingAudioSource.volume, targetBreathingVol, audioFadeSpeed * Time.deltaTime);

            if (breathingAudioSource.volume > 0.01f && !breathingAudioSource.isPlaying) breathingAudioSource.Play();
            else if (breathingAudioSource.volume <= 0.001f && breathingAudioSource.isPlaying) breathingAudioSource.Pause();
        }

        // 2. Tim đập theo độ sợ hãi
        if (heartbeatAudioSource != null && heartbeatAudioSource.clip != null)
        {
            float targetHeartbeatVol = 0f;
            if (currentPanic >= heartbeatStartThreshold)
            {
                float factor = Mathf.InverseLerp(heartbeatStartThreshold, maxCourageCap, currentPanic);
                targetHeartbeatVol = factor * maxHeartbeatVolume;
                heartbeatAudioSource.pitch = Mathf.Lerp(0.9f, 1.4f, factor);
            }

            heartbeatAudioSource.volume = Mathf.MoveTowards(heartbeatAudioSource.volume, targetHeartbeatVol, audioFadeSpeed * Time.deltaTime);

            if (heartbeatAudioSource.volume > 0.01f && !heartbeatAudioSource.isPlaying) heartbeatAudioSource.Play();
            else if (heartbeatAudioSource.volume <= 0.001f && heartbeatAudioSource.isPlaying) heartbeatAudioSource.Pause();
        }
    }

    private void StopAllAudio()
    {
        if (heartbeatAudioSource != null && heartbeatAudioSource.isPlaying) heartbeatAudioSource.Stop();
        if (breathingAudioSource != null && breathingAudioSource.isPlaying) breathingAudioSource.Stop();
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        // Vòng ngoài (Vàng)
        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, outerZoneRadius);

        // Vòng áp sát sát người (Đỏ)
        Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, dangerZoneRadius);

        // Vòng tầm nhìn thấy đồng đội chết (Xanh dương)
        Gizmos.color = new Color(0f, 0.7f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, witnessMaxDistance);
    }
}
