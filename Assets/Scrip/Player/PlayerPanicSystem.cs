using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[RequireComponent(typeof(HealthAndTeam))]
public class PlayerPanicSystem : MonoBehaviour
{
    public static PlayerPanicSystem Instance { get; private set; }

    [Header("Panic & Courage Stats")]
    public float currentPanic = 0f;

    public float maxCourageCap = 100f;

    public bool enablePanicDeath = true; // Sợ 100%: Đột tử vì vỡ mật

    [Header("Zone Settings (2 Vòng trực quan)")]
    public float outerZoneRadius = 18f;
    public float dangerZoneRadius = 4.5f;
    public float closeAllyRadius = 5.5f;
    public float scanInterval = 0.2f;
    public LayerMask characterLayerMask;

    [Header("Panic Gain Rates (Khi bị đe dọa)")]
    public float panicRatePerOuterEnemy = 0f;
    public float panicRatePerInnerEnemy = 3.0f;
    public float maxPanicGainRate = 12.0f;

    [Header("Panic Recovery Rates (Cơ chế Hồi Phục Sợ Hãi)")]
    public float calmDownRate = 5.0f;
    public float allyReliefPerPerson = 1.0f;
    public float maxAllyCalmRate = 8.0f;

    [Header("Visual Witness Death (Tận mắt thấy đồng đội chết)")]
    public Camera playerCamera;

    public float panicOnWitnessAllyDeath = 15f;

    [Header("Audio Settings (Âm lượng & Test)")]
    public AudioSource heartbeatAudioSource;
    public AudioSource breathingAudioSource;
    public AudioClip heartbeatClip;
    public AudioClip heavyBreathingClip;

    [Range(0f, 1f)] public float breathingVolume = 1.0f;
    [Range(0f, 1f)] public float minBreathingVolRatio = 0.12f;

    [Range(0f, 1f)] public float heartbeatVolume = 1.0f;

    [Header("Audio Boost (Khuếch đại âm lượng x2, x3 - Chống rè & Lệch)")]
    [Range(1f, 4f)]
    public float breathingBoost = 2.0f;

    [Range(1f, 4f)]
    public float heartbeatBoost = 2.0f;

    public float audioFadeSpeed = 4.0f;

    public float heartbeatStartThreshold = 15f;

    [Header("Battle Ambience Audio (Âm thanh chiến trận hỗn chiến - 2 Layer chồng nhau)")]
    public AudioSource battleAmbienceAudioSource1;
    public AudioClip battleAmbienceClip1;
    [Range(0f, 1f)] public float layer1VolumeScale = 1.0f;

    public AudioSource battleAmbienceAudioSource2;
    public AudioClip battleAmbienceClip2;
    [Range(0f, 1f)] public float layer2VolumeScale = 1.0f;

    [Range(0f, 1f)] public float maxBattleAmbienceVolume = 0.85f;
    [Range(0f, 1f)] public float minBattleAmbienceVolume = 0.15f; 
    public int maxCrowdForFullVolume = 15; 
    public float battleAudioFadeSpeed = 2.5f; 

    [Header("Testing Tool")]
    public bool freezePanic = false;

    [Header("Visual Effects: Panic (Đen 4 góc)")]
    public Volume postProcessVolume;
    [Range(0f, 1f)] public float vignetteThreshold = 0.5f;
    [Range(0f, 1f)] public float basePanicVignetteIntensity = 0.25f;
    [Range(0f, 1f)] public float maxPanicVignetteIntensity = 0.50f;
    [Range(0f, 1f)] public float maxPanicUIAlpha = 0.70f; // Độ đậm tối đa của ảnh UI đen
    public Image uiPanicVignetteImage; // Tấm ảnh đen 4 góc trên Canvas

    [Header("Visual Effects: Low Health (Đỏ 4 góc khi máu thấp)")]
    [Range(0f, 1f)] public float lowHealthThreshold = 0.40f; // Dưới 40% máu là bắt đầu đỏ 4 góc
    [Range(0f, 1f)] public float baseBloodVignetteIntensity = 0.25f; // Cường độ bắt đầu
    [Range(0f, 1f)] public float maxBloodVignetteIntensity = 0.42f; // Cường độ đỏ tối đa (giảm bớt không bị quá đỏ)
    [Range(0f, 1f)] public float maxBloodUIAlpha = 0.03f; // Độ đậm tối đa của ảnh UI đỏ
    public Color bloodVignetteColor = new Color(0.50f, 0.06f, 0.08f, 1f); // Màu đỏ máu sẫm (dịu mắt, không bị neon)
    public Image uiBloodVignetteImage; // Tấm ảnh vết máu đỏ 4 góc trên Canvas

    [Header("Visual Effects: Heartbeat Pulsing (Co bóp nhịp tim)")]
    public bool enableVignettePulse = true;
    public float pulseSpeedMin = 3.5f;
    public float pulseSpeedMax = 7.5f;
    public float pulseAmount = 0.05f;
    private Vignette vignette;

    [Header("Debug Status (Theo dõi thời gian thực)")]
    [SerializeField] private string debugStateDescription = "Bình thường";
    [SerializeField] private int debugOuterEnemies = 0;
    [SerializeField] private int debugInnerEnemies = 0;
    [SerializeField] private int debugAlliesNearby = 0; // Tổng đồng đội trong 18m (dùng cho Battle Ambience)
    [SerializeField] private int debugCloseAllies = 0;   // Đồng đội sát cánh trong closeAllyRadius (dùng cho che chở)
    [SerializeField] private int debugTrackedAlliesCount = 0;
    [SerializeField] private float debugCurrentPanicRate = 0f;
    [SerializeField] private float debugBattleVolume = 0f;

    private float targetBattleVolume = 0f;

    private HealthAndTeam myHealth;
    private CharacterController controller;
    private float scanTimer = 0f;

    // Buffer cố định cho OverlapSphereNonAlloc (256 chỗ để không bị tràn khi nghìn quân hỗn chiến)
    private readonly Collider[] scanCollidersBuffer = new Collider[256];

    // Quản lý đăng ký onDie cho đồng đội trong vòng chiến trường 18m
    private readonly Dictionary<HealthAndTeam, System.Action> trackedAllies = new Dictionary<HealthAndTeam, System.Action>();
    private readonly HashSet<HealthAndTeam> currentScanAllies = new HashSet<HealthAndTeam>();
    private readonly HashSet<HealthAndTeam> currentScanEnemies = new HashSet<HealthAndTeam>();
    private readonly List<HealthAndTeam> alliesToRemove = new List<HealthAndTeam>();

    public System.Action<float, float> onPanicChanged;
    public float PanicNormalized => Mathf.Clamp01(currentPanic / Mathf.Max(maxCourageCap, 1f));
    private float lastReportedPanic = -1f;
    private Animator anim;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(this); return; }

        // Bắt buộc reset về 0 để tránh giá trị thử nghiệm còn lưu trong Inspector
        currentPanic = 0f;

        myHealth = GetComponent<HealthAndTeam>();
        controller = GetComponent<CharacterController>();
        anim = GetComponentInChildren<Animator>();

        if (characterLayerMask.value == 0)
            characterLayerMask = LayerMask.GetMask("Character");

        SetupAudioSources();
    }

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (postProcessVolume == null)
            postProcessVolume = FindObjectOfType<Volume>();

        if (postProcessVolume != null && postProcessVolume.profile != null)
            postProcessVolume.profile.TryGet(out vignette);
    }

    void OnDisable()
    {
        StopAllAudio();
        CleanupTrackedAllies();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        StopAllAudio();
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

        // Quét cục bộ định kỳ 0.2s/lần
        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = scanInterval;
            PerformLocalScan();
        }

        UpdatePanicLevel();
        UpdateAudio();
        UpdateVisualEffects();
        CheckPanicSuddenDeath();
    }


    private void PerformLocalScan()
    {
        debugOuterEnemies = 0;
        debugInnerEnemies = 0;
        debugAlliesNearby = 0;
        debugCloseAllies = 0;

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, outerZoneRadius, scanCollidersBuffer, characterLayerMask);
        currentScanAllies.Clear();
        currentScanEnemies.Clear();

        Vector3 myPos = transform.position;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = scanCollidersBuffer[i];
            if (col == null) continue;

            HealthAndTeam h = col.GetComponentInParent<HealthAndTeam>();
            if (h == null || h == myHealth || !h.isAlive) continue;

            // 1. Phân loại Kẻ địch (Chỉ đếm 1 lần cho mỗi con NPC, chống trùng do nhiều Collider)
            if (myHealth.IsEnemy(h.teamCurrent))
            {
                if (currentScanEnemies.Add(h))
                {
                    float distance = Vector3.Distance(myPos, h.transform.position);
                    if (distance <= dangerZoneRadius)
                        debugInnerEnemies++;
                    else
                        debugOuterEnemies++;
                }
            }
            // 2. Phân loại Đồng đội (Đang sống trong vòng chiến trường 18m, chống trùng Collider)
            else
            {
                if (currentScanAllies.Add(h))
                {
                    debugAlliesNearby++; // Dùng cho Battle Ambience Audio 18m

                    float distance = Vector3.Distance(myPos, h.transform.position);
                    if (distance <= closeAllyRadius)
                    {
                        debugCloseAllies++; // Chỉ đồng đội sát cánh mới hỗ trợ giảm sợ hãi
                    }

                    // Đăng ký onDie ngay khi đồng đội bước vào vòng 18m (nếu chưa theo dõi)
                    if (!trackedAllies.ContainsKey(h))
                    {
                        System.Action deathCallback = () => HandleTrackedAllyDied(h);
                        h.onDie += deathCallback;
                        trackedAllies.Add(h, deathCallback);
                    }
                }
            }
        }

        // 3. Tự động hủy theo dõi những đồng đội đã chạy ra khỏi vòng 18m
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

        // 4. Tính toán âm lượng âm thanh chiến trận (Battle Ambience)
        int totalEnemies = debugOuterEnemies + debugInnerEnemies;
        int totalAllies = debugAlliesNearby;
        int totalCombatants = totalEnemies + totalAllies;

        // ĐIỀU KIỆN CHÍNH XÁC (Không dùng Raycast, chỉ dựa trên số lượng 2 phe trong bán kính 18m):
        // - BẮT BUỘC PHẢI CÓ ÍT NHẤT 1 KẺ ĐỊCH VÀ 1 ĐỒNG ĐỘI (totalEnemies >= 1 && totalAllies >= 1)
        // - Đi nguyên đám toàn địch, hoặc đi nguyên đám toàn bạn -> HOÀN TOÀN KHÔNG BẬT (targetBattleVolume = 0)
        if (totalEnemies >= 1 && totalAllies >= 1)
        {
            // Bật cả 2 clip chung hết! Càng đông người quanh đó thì âm thanh càng to (tăng dần đến max ở maxCrowdForFullVolume)
            float crowdRatio = Mathf.Clamp01((float)(totalCombatants - 2) / Mathf.Max(1f, maxCrowdForFullVolume - 2f));
            targetBattleVolume = Mathf.Lerp(minBattleAmbienceVolume, maxBattleAmbienceVolume, crowdRatio);
        }
        else
        {
            // Đi nguyên đám 1 phe (toàn địch hoặc toàn bạn), hoặc vắng vẻ -> TẮT CẢ 2 CLIP
            targetBattleVolume = 0f;
        }
    }


    private void HandleTrackedAllyDied(HealthAndTeam deadAlly)
    {
        if (deadAlly == null) return;

        if (trackedAllies.TryGetValue(deadAlly, out var action))
        {
            deadAlly.onDie -= action;
            trackedAllies.Remove(deadAlly);
        }

        // Kiểm tra xem Camera người chơi lúc này có đang nhìn thấy đồng đội bị chém chết không
        if (IsTargetVisibleInCamera(deadAlly.transform.position))
        {
            AddPanic(panicOnWitnessAllyDeath);
            Debug.Log($"<color=red>[Panic]</color> Tận mắt chứng kiến đồng đội {deadAlly.gameObject.name} hy sinh trước mặt! +{panicOnWitnessAllyDeath} Sợ Hãi.");
        }
    }


    private bool IsTargetVisibleInCamera(Vector3 worldPos)
    {
        Camera cam = playerCamera != null ? playerCamera : Camera.main;
        if (cam == null) return false;

        Vector3 targetPoint = worldPos + Vector3.up * 1.0f;
        Vector3 camToTarget = targetPoint - cam.transform.position;
        float dist = camToTarget.magnitude;

        // 1. Kiểm tra Viewport
        Vector3 viewPos = cam.WorldToViewportPoint(targetPoint);
        bool inScreen = viewPos.z > 0 && viewPos.x >= 0f && viewPos.x <= 1f && viewPos.y >= 0f && viewPos.y <= 1f;
        if (!inScreen) return false;

        // 2. Bắn Raycast kiểm tra Line of Sight
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

        if (freezePanic)
        {
            debugCurrentPanicRate = 0f;
            debugStateDescription = "Đang KHÓA sợ hãi (Freeze - Không tự tụt)";
            return;
        }

        // TÌNH HUỐNG 1: CÓ KẺ ĐỊCH ÁP SÁT CẬN CHIẾN (Trong vòng nguy hiểm) HOẶC ĐỊCH TẦM XA GÂY ÁP LỰC
        float rawEnemyPressure = (debugInnerEnemies * panicRatePerInnerEnemy) +
                                 (debugOuterEnemies * panicRatePerOuterEnemy);

        if (debugInnerEnemies > 0 || rawEnemyPressure > 0.01f)
        {
            // Đồng đội che chở: CHỈ TÍNH ĐỒNG ĐỘI SÁT CÁNH (trong closeAllyRadius)
            float allyProtection = debugCloseAllies * allyReliefPerPerson;
            float netPressure = rawEnemyPressure - allyProtection;

            if (netPressure > 0f)
            {
                // KHỐA TRẦN TỐC ĐỘ TĂNG: Chống hiện tượng "tăng phi mã" khi bị nhiều địch vây
                float gainRate = Mathf.Min(netPressure, maxPanicGainRate);
                debugCurrentPanicRate = gainRate;
                currentPanic += gainRate * Time.deltaTime;
                debugStateDescription = $"Đang bị áp sát ({debugInnerEnemies} địch) (Tăng +{gainRate:F1}/s)";
            }
            else
            {
                // Có đồng đội đứng sát bên đông hơn địch sát sườn -> Yên tâm, giảm nhẹ sợ hãi
                float calmRate = Mathf.Min(Mathf.Abs(netPressure), maxAllyCalmRate);
                debugCurrentPanicRate = -calmRate;
                currentPanic -= calmRate * Time.deltaTime;
                debugStateDescription = $"Đồng đội sát cánh che chở (Giảm -{calmRate:F1}/s)";
            }
        }
        // TÌNH HUỐNG 2: KHÔNG CÓ ĐỊCH ÁP SÁT TRONG VÒNG NGUY HIỂM (debugInnerEnemies == 0)
        // Kể cả ngoài xa 10-18m vẫn có lính hỗn chiến (debugOuterEnemies > 0),
        // người chơi đã có khoảng thở -> TỰ ĐỘNG HỒI PHỤC BÌNH TĨNH!
        else
        {
            // Tốc độ hồi phục cơ bản + nếu có đồng đội bên cạnh thì bình tĩnh nhanh hơn
            float recoveryRate = calmDownRate + (debugCloseAllies * 0.5f);
            recoveryRate = Mathf.Min(recoveryRate, maxAllyCalmRate);

            debugCurrentPanicRate = -recoveryRate;
            currentPanic -= recoveryRate * Time.deltaTime;

            if (debugOuterEnemies > 0)
            {
                debugStateDescription = $"Tạm thoát cận chiến, định thần lại (Giảm -{recoveryRate:F1}/s)";
            }
            else
            {
                debugStateDescription = $"An toàn ra xa giao tranh (Giảm -{recoveryRate:F1}/s)";
            }
        }

        currentPanic = Mathf.Clamp(currentPanic, 0f, maxCourageCap);

        if (Mathf.Abs(lastReportedPanic - currentPanic) > 0.05f || (currentPanic == 0f && lastReportedPanic != 0f) || (currentPanic >= maxCourageCap && lastReportedPanic < maxCourageCap))
        {
            lastReportedPanic = currentPanic;
            onPanicChanged?.Invoke(currentPanic, maxCourageCap);
        }

        CheckPanicSuddenDeath();
    }

    public void AddPanic(float amount)
    {
        currentPanic = Mathf.Clamp(currentPanic + amount, 0f, maxCourageCap);
        onPanicChanged?.Invoke(currentPanic, maxCourageCap);
        CheckPanicSuddenDeath();
    }

    private void CheckPanicSuddenDeath()
    {
        if (!enablePanicDeath) return;
        if (myHealth == null || !myHealth.isAlive) return;

        if (currentPanic >= maxCourageCap)
        {
            myHealth.TakeDamage(myHealth.maxHealth * 10f, gameObject);
            StopAllAudio();
        }
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

    private AudioSourceBooster breathingBooster;
    private AudioSourceBooster heartbeatBooster;

    private void SetupAudioSources()
    {
        heartbeatAudioSource = PrepareAudioSource(heartbeatAudioSource, heartbeatClip, "[Audio_Heartbeat]", out heartbeatBooster);
        breathingAudioSource = PrepareAudioSource(breathingAudioSource, heavyBreathingClip, "[Audio_Breathing]", out breathingBooster);
        battleAmbienceAudioSource1 = PrepareAudioSource(battleAmbienceAudioSource1, battleAmbienceClip1, "[Audio_BattleAmbience1]", out _);
        battleAmbienceAudioSource2 = PrepareAudioSource(battleAmbienceAudioSource2, battleAmbienceClip2, "[Audio_BattleAmbience2]", out _);
    }

    private AudioSource PrepareAudioSource(AudioSource source, AudioClip clip, string childName, out AudioSourceBooster booster)
    {
        booster = null;
        if (source == null)
        {
            Transform child = transform.Find(childName);
            if (child == null)
            {
                GameObject go = new GameObject(childName);
                go.transform.SetParent(transform, false);
                child = go.transform;
            }
            source = child.GetComponent<AudioSource>();
            if (source == null) source = child.gameObject.AddComponent<AudioSource>();
        }

        booster = source.GetComponent<AudioSourceBooster>();
        if (booster == null) booster = source.gameObject.AddComponent<AudioSourceBooster>();

        // Tắt playOnAwake và đặt volume = 0 để tuyệt đối không bị nổ tiếng khi vừa load cảnh
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f; // 2D Stereo
        source.volume = 0f;
        source.Stop();

        if (clip != null && source.clip == null)
            source.clip = clip;

        return source;
    }

    private void UpdateAudio()
    {
        // Đồng bộ hệ số khuếch đại Boost theo thời gian thực (chống rè và lệch)
        if (breathingBooster != null) breathingBooster.boostMultiplier = breathingBoost;
        if (heartbeatBooster != null) heartbeatBooster.boostMultiplier = heartbeatBoost;

        // 1. Thở dốc: Đọc trực tiếp IsRunning từ Animator (PlayerMovement đã lọc chuẩn xác di chuyển, không đấm, panic < 80)
        if (anim == null && PlayerMovement.Instance != null)
            anim = PlayerMovement.Instance.anim;

        bool isRunning = (anim != null) ? anim.GetBool("IsRunning") : false;

        if (breathingAudioSource != null && breathingAudioSource.clip != null)
        {
            // Âm lượng tiếng thở tăng dần theo độ sợ hãi: Sợ = 0 thì thở nhẹ (12%), Sợ = 100 thì thở dốc cực to (100%)
            // Khi đang nằm giả chết: TẮT hoàn toàn tiếng thở để nín thở im lìm, chỉ giữ lại tiếng tim đập thình thịch!
            bool isFeignDead = (PlayerFeignDeath.Instance != null && PlayerFeignDeath.Instance.IsFeignDead);
            float panicVolMultiplier = Mathf.Lerp(minBreathingVolRatio, 1.0f, PanicNormalized);
            float targetBreathingVol = (!isFeignDead && isRunning) ? (breathingVolume * panicVolMultiplier) : 0f;

            breathingAudioSource.pitch = Mathf.Lerp(0.95f, 1.3f, PanicNormalized);
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
                // Bắt đầu đập rõ từ 40% và tăng lên 100% âm lượng
                targetHeartbeatVol = Mathf.Lerp(0.4f, 1.0f, factor) * heartbeatVolume;
                // Nhịp đập tăng thêm khoảng 20% (Pitch từ 1.05 lên 1.65) để tim đập nhanh và gấp gáp hơn
                heartbeatAudioSource.pitch = Mathf.Lerp(1.05f, 1.65f, factor);
            }

            heartbeatAudioSource.volume = Mathf.MoveTowards(heartbeatAudioSource.volume, targetHeartbeatVol, audioFadeSpeed * Time.deltaTime);

            if (heartbeatAudioSource.volume > 0.01f && !heartbeatAudioSource.isPlaying) heartbeatAudioSource.Play();
            else if (heartbeatAudioSource.volume <= 0.001f && heartbeatAudioSource.isPlaying) heartbeatAudioSource.Pause();
        }

        // 3. Tiếng chiến trận hỗn chiến (War Ambience - Cả 2 Clip bật chung hết)
        UpdateBattleAmbienceLayer(battleAmbienceAudioSource1, targetBattleVolume * layer1VolumeScale);
        UpdateBattleAmbienceLayer(battleAmbienceAudioSource2, targetBattleVolume * layer2VolumeScale);

        debugBattleVolume = targetBattleVolume;
    }

    private void UpdateBattleAmbienceLayer(AudioSource source, float targetVol)
    {
        if (source == null || source.clip == null) return;

        source.volume = Mathf.MoveTowards(source.volume, targetVol, battleAudioFadeSpeed * Time.deltaTime);

        if (source.volume > 0.01f && !source.isPlaying) 
            source.Play();
        else if (source.volume <= 0.001f && source.isPlaying) 
            source.Pause();
    }

    private void StopAllAudio()
    {
        if (heartbeatAudioSource != null && heartbeatAudioSource.isPlaying) heartbeatAudioSource.Stop();
        if (breathingAudioSource != null && breathingAudioSource.isPlaying) breathingAudioSource.Stop();
        if (battleAmbienceAudioSource1 != null && battleAmbienceAudioSource1.isPlaying) battleAmbienceAudioSource1.Stop();
        if (battleAmbienceAudioSource2 != null && battleAmbienceAudioSource2.isPlaying) battleAmbienceAudioSource2.Stop();
    }

    private void UpdateVisualEffects()
    {
        // 1. Tính toán mức độ Sợ Hãi 
        float panicFactor = 0f;
        float panicRatio = PanicNormalized;
        if (panicRatio >= vignetteThreshold)
        {
            float t = Mathf.Clamp01((panicRatio - vignetteThreshold) / Mathf.Max(0.001f, 1f - vignetteThreshold));
            panicFactor = Mathf.Pow(t, 0.7f); 
        }

        // 2. Tính toán mức độ Nguy Kịch của Máu (Máu: dưới 40% tụt về 0%)
        float bloodFactor = 0f;
        if (myHealth != null && myHealth.isAlive)
        {
            float maxHp = Mathf.Max(1f, myHealth.maxHealth);
            float currentHpRatio = Mathf.Clamp01(myHealth.currentHealth / maxHp);
            if (currentHpRatio < lowHealthThreshold)
            {
                // Máu càng tụt sát về 0 thì bloodFactor càng tiến tới 1.0 (cực kỳ nguy kịch)
                bloodFactor = Mathf.Clamp01((lowHealthThreshold - currentHpRatio) / lowHealthThreshold);
            }
        }

        // 3. Hiệu ứng co bóp/nhấp nháy nhịp tim riêng biệt cho từng loại
        float panicPulse = 0f;
        if (enableVignettePulse && panicFactor > 0f)
        {
            float speed = Mathf.Lerp(pulseSpeedMin, pulseSpeedMax, panicFactor);
            panicPulse = (Mathf.Sin(Time.time * speed) * 0.5f + 0.5f) * pulseAmount * panicFactor;
        }

        float bloodPulse = 0f;
        if (enableVignettePulse && bloodFactor > 0f)
        {
            float speed = Mathf.Lerp(pulseSpeedMin, pulseSpeedMax, bloodFactor);
            bloodPulse = (Mathf.Sin(Time.time * speed) * 0.5f + 0.5f) * pulseAmount * bloodFactor;
        }

        // 4. Điều khiển Global Volume Vignette (URP)
        if (vignette != null)
        {
            float panicIntensity = Mathf.Lerp(basePanicVignetteIntensity, maxPanicVignetteIntensity, panicFactor) + panicPulse;
            float bloodIntensity = Mathf.Lerp(baseBloodVignetteIntensity, maxBloodVignetteIntensity, bloodFactor) + bloodPulse;

            float targetIntensity = (panicFactor > 0f || bloodFactor > 0f) ? Mathf.Max(panicIntensity, bloodIntensity) : basePanicVignetteIntensity;
            vignette.intensity.value = Mathf.MoveTowards(vignette.intensity.value, targetIntensity, Time.deltaTime * 2.5f);

            // Chuyển màu Vignette: Chỉ chuyển sang đỏ khi máu tụt dưới 40% (bloodFactor > 0)
            vignette.color.overrideState = true;
            Color targetColor = Color.Lerp(Color.black, bloodVignetteColor, bloodFactor);
            vignette.color.value = Color.Lerp(vignette.color.value, targetColor, Time.deltaTime * 3.5f);
        }

        // 5. Điều khiển Tấm ảnh UI ĐEN (Sợ hãi) - CHỈ BẬT KHI SỢ HÃI >= 50
        if (uiPanicVignetteImage != null)
        {
            float targetAlpha = (panicFactor > 0.001f) ? Mathf.Clamp01((panicFactor * maxPanicUIAlpha) + panicPulse) : 0f;
            Color c = uiPanicVignetteImage.color;
            c.a = Mathf.MoveTowards(c.a, targetAlpha, Time.deltaTime * 2.5f);
            uiPanicVignetteImage.color = c;
        }

        // 6. Điều khiển Tấm ảnh UI ĐỎ (Vết máu) - CHỈ BẬT KHI MÁU DƯỚI 40% (HOÀN TOÀN KHÔNG BỊ NHẤP NHÁY KHI CHỈ CÓ SỢ HÃI)
        if (uiBloodVignetteImage != null)
        {
            float targetAlpha = (bloodFactor > 0.001f) ? Mathf.Clamp01((bloodFactor * maxBloodUIAlpha) + bloodPulse) : 0f;
            Color c = uiBloodVignetteImage.color;
            c.a = Mathf.MoveTowards(c.a, targetAlpha, Time.deltaTime * 3.0f);
            uiBloodVignetteImage.color = c;
        }
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        // VÒNG NGUY HIỂM (Đỏ): Kẻ địch áp sát sát người (3.5m)
        Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, dangerZoneRadius);

        // VÒNG CHIẾN TRƯỜNG (Vàng): Quét địch, đếm đồng đội bảo vệ, và tầm nhìn đồng đội chết (18m)
        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.45f);
        Gizmos.DrawWireSphere(transform.position, outerZoneRadius);
    }
}


public class AudioSourceBooster : MonoBehaviour
{
    [Range(1f, 5f)] public float boostMultiplier = 1f;

    void OnAudioFilterRead(float[] data, int channels)
    {
        float boost = boostMultiplier;
        if (boost <= 1.001f) return;

        for (int i = 0; i < data.Length; i++)
        {
            float s = data[i] * boost;
            // Thuật toán Soft-Knee Limiter:
            // - Dưới ngưỡng 0.95: khuếch đại tuyến tính 100% trung thực (bit-perfect).
            // - Trên ngưỡng 0.95: nén mềm tiệm cận 0.999 để chống hiện tượng cắt ngọn sóng (Hard Clipping) gây rè loa.
            if (s > 0.95f)
            {
                s = 0.95f + (s - 0.95f) / (1f + (s - 0.95f) * 2f);
                if (s > 0.999f) s = 0.999f;
            }
            else if (s < -0.95f)
            {
                float neg = -s;
                neg = 0.95f + (neg - 0.95f) / (1f + (neg - 0.95f) * 2f);
                if (neg > 0.999f) neg = 0.999f;
                s = -neg;
            }
            data[i] = s;
        }
    }
}
