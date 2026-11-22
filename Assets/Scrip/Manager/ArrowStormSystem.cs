using System.Collections;
using UnityEngine;

public class ArrowStormSystem : MonoBehaviour
{
    public static ArrowStormSystem Instance { get; private set; }

    public enum StormTargetMode 
    { 
        TargetPlayerArea, 
        TargetFrontlineCenter, 
        RandomBattlefield 
    }

    [Header("1. Âm Thanh Báo Động (War Horn Warning)")]
    public AudioClip hornClip;
    [Range(0f, 1f)] public float hornVolume = 1.0f;

    [Header("2. Chu Kỳ & Thời Gian (Timing)")]
    public bool enableAutoStorm = true;
    public float initialDelay = 35f;
    public float minInterval = 45f;
    public float maxInterval = 75f;
    [Tooltip("Thời gian từ lúc tù và kêu đến lúc mưa tên chạm đất (để Player chạy né)")]
    public float warningLeadTime = 3.5f;

    [Header("3. Quy Mô & Sát Thương (Arrow Storm Specs)")]
    public int arrowsPerVolley = 150;
    public float damagePerArrow = 1f;
    public float arrowSpeed = 35f;
    public float arrowGravity = 12f;
    public float skyHeight = 50f;
    public float stormRadius = 15f;

    [Header("4. Khu Vực Rơi (Target Selection)")]
    public StormTargetMode targetMode = StormTargetMode.TargetPlayerArea;
    [Range(0f, 30f)] 
    [Tooltip("Độ lệch tâm so với Player lúc khóa mục tiêu (0 = Thẳng trên đầu)")]
    public float playerCenterOffset = 0f;

    private AudioSource hornAudioSource;
    private bool isStormActive = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        SetupAudio();
    }

    void Start()
    {
        if (enableAutoStorm)
        {
            StartCoroutine(RoutineAutoStormLoop());
        }
    }

    private void SetupAudio()
    {
        Transform child = transform.Find("[Audio_ArrowStorm]");
        if (child == null)
        {
            GameObject go = new GameObject("[Audio_ArrowStorm]");
            go.transform.SetParent(transform, false);
            child = go.transform;
        }

        hornAudioSource = child.GetComponent<AudioSource>();
        if (hornAudioSource == null) hornAudioSource = child.gameObject.AddComponent<AudioSource>();

        hornAudioSource.playOnAwake = false;
        hornAudioSource.spatialBlend = 0f; // 2D Sound: Nghe vang dội toàn chiến trường
        hornAudioSource.volume = hornVolume;
    }

    private IEnumerator RoutineAutoStormLoop()
    {
        yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            float waitTime = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(waitTime);

            if (!isStormActive)
            {
                yield return StartCoroutine(RoutineExecuteStorm());
            }
        }
    }

    [ContextMenu("🏹 Bắn Thử Mưa Tên Ngay")]
    public void TriggerStorm()
    {
        if (!isStormActive)
        {
            StartCoroutine(RoutineExecuteStorm());
        }
        else
        {
            Debug.LogWarning("[ArrowStormSystem] Một đợt mưa tên đang diễn ra!");
        }
    }

    private IEnumerator RoutineExecuteStorm()
    {
        isStormActive = true;

        // 1. KHÓA VỊ TRÍ TÂM RƠI NGAY LẬP TỨC TẠI t = 0 (NGAY KHI TÙ VÀ BẮT ĐẦU KÊU)
        // Khi người chơi nghe tù và và chạy né, vị trí rơi vẫn giữ nguyên ở đây!
        Vector3 lockedCenter = GetTargetCenter();
        float currentRadius = stormRadius;

        // 2. PHÁT TIẾNG TÙ VÀ BÁO HIỆU
        if (hornAudioSource != null && hornClip != null)
        {
            hornAudioSource.volume = hornVolume;
            hornAudioSource.PlayOneShot(hornClip);
        }

        // 3. ĐẾM NGƯỢC THỜI GIAN CẢNH BÁO (warningLeadTime - ví dụ 3.5s)
        // Trong thời gian này, Player nghe tiếng tù và sẽ có đủ thời gian chạy ra khỏi vùng tâm rơi
        yield return new WaitForSeconds(warningLeadTime);

        // 4. PHÓNG ĐỒNG LOẠT TOÀN BỘ MŨI TÊN TỪ TRÊN TRỜI XUỐNG ĐÚNG VỊ TRÍ ĐÃ KHÓA
        int totalArrows = arrowsPerVolley;
        // 10% mũi tên tập trung quanh tâm (mở rộng lên 1.5m) -> Phân bố tự nhiên, không bị chụm quá mức
        int coreArrowCount = Mathf.Max(2, Mathf.RoundToInt(totalArrows * 0.10f));

        for (int i = 0; i < totalArrows; i++)
        {
            bool isCore = (i < coreArrowCount);
            SpawnSkyArrow(lockedCenter, isCore, currentRadius);
        }

        // Chờ đợt tên rơi xong
        yield return new WaitForSeconds(2.5f);
        isStormActive = false;
    }

    private Vector3 GetTargetCenter()
    {
        if (targetMode == StormTargetMode.TargetPlayerArea && PlayerMovement.PlayerTransform != null)
        {
            if (playerCenterOffset <= 0.01f)
            {
                return PlayerMovement.PlayerTransform.position;
            }

            Vector2 offset = Random.insideUnitCircle * playerCenterOffset;
            return PlayerMovement.PlayerTransform.position + new Vector3(offset.x, 0f, offset.y);
        }

        if (targetMode == StormTargetMode.TargetFrontlineCenter && MassArmySpawner.Instance != null)
        {
            return MassArmySpawner.Instance.transform.position;
        }

        if (PlayerMovement.PlayerTransform != null)
        {
            return PlayerMovement.PlayerTransform.position;
        }

        return transform.position;
    }

    private void SpawnSkyArrow(Vector3 center, bool isCoreHit, float radius)
    {
        if (ArrowPoolManager.Instance == null) return;

        // Vị trí mục tiêu dưới đất:
        // isCoreHit: Rơi tập trung trong phạm vi mở rộng 1.5m quanh tâm đã khóa
        // !isCoreHit: Rải đều khắp bán kính radius để tạo màn mưa tên dày đặc
        Vector2 circle = isCoreHit 
            ? (Random.insideUnitCircle * Mathf.Min(1.5f, radius)) 
            : (Random.insideUnitCircle * radius);

        Vector3 groundTargetPos = center + new Vector3(circle.x, 0f, circle.y);

        // Xuất phát từ trên trời cao
        float heightVariance = Random.Range(-1.5f, 1.5f);
        Vector3 spawnPos = groundTargetPos + Vector3.up * (skyHeight + heightVariance);

        // Hướng bắn chúc thẳng xuống mục tiêu
        Vector3 aimTarget = groundTargetPos + new Vector3(Random.Range(-0.25f, 0.25f), 0f, Random.Range(-0.25f, 0.25f));
        Vector3 flyDirection = (aimTarget - spawnPos).normalized;
        Quaternion rot = Quaternion.LookRotation(flyDirection);

        GameObject arrowObj = ArrowPoolManager.Instance.GetArrow(spawnPos, rot);
        if (arrowObj == null) return;

        ArrowProjectile proj = arrowObj.GetComponent<ArrowProjectile>();
        if (proj != null)
        {
            float randomizedSpeed = arrowSpeed + Random.Range(-1.5f, 1.5f);
            proj.Init(null, damagePerArrow, randomizedSpeed, arrowGravity);
        }
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(ArrowStormSystem))]
public class ArrowStormSystemEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ArrowStormSystem storm = (ArrowStormSystem)target;

        GUILayout.Space(12);
        GUI.backgroundColor = new Color(0.95f, 0.45f, 0.2f);
        if (GUILayout.Button("🏹 BẮN THỬ MƯA TÊN NGAY (TEST)", GUILayout.Height(36)))
        {
            if (Application.isPlaying)
            {
                storm.TriggerStorm();
            }
            else
            {
                Debug.LogWarning("[ArrowStormSystem] Vui lòng bấm PLAY game trước khi test bắn mưa tên!");
            }
        }
        GUI.backgroundColor = Color.white;
    }
}
#endif
