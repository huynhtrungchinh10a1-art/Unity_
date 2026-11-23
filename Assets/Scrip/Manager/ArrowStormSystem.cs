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
    [Tooltip("Kéo Player vào đây, hoặc để trống để code tự động tìm")]
    public Transform playerTransform;
    [Range(0f, 30f)] 
    [Tooltip("Độ lệch tâm so với Player lúc khóa mục tiêu (0 = Thẳng trên đầu)")]
    public float playerCenterOffset = 0f;

    [Header("5. Vùng Đỏ Cảnh Báo Dưới Đất (Danger Zone Indicator)")]
    public bool enableDangerZone = true;
    public Color dangerColor = new Color(1f, 0.15f, 0.15f, 0.45f);
    public Color dangerBorderColor = new Color(1f, 0.2f, 0.2f, 0.95f);
    [Tooltip("Nếu để = 0 thì tự động lấy theo Storm Radius")]
    public float customDangerRadius = 0f;

    private AudioSource hornAudioSource;
    private bool isStormActive = false;

    // Thành phần dựng Vùng Đỏ Cảnh Báo chuẩn AAA ôm sát mặt đất
    private GameObject dangerZoneObj;
    private MeshFilter dangerMeshFilter;
    private MeshRenderer dangerMeshRenderer;
    private LineRenderer dangerLineRenderer;
    private LineRenderer dangerInnerLineRenderer;
    private Material dangerFillMaterial;
    private Material dangerBorderMaterial;
    private Material dangerInnerLineMaterial;
    private Mesh dangerMesh;
    private const int CIRCLE_SEGMENTS = 64;
    private Vector3[] circleVertices = new Vector3[CIRCLE_SEGMENTS + 1];
    private int[] circleTriangles = new int[CIRCLE_SEGMENTS * 3];
    private Vector2[] circleUVs = new Vector2[CIRCLE_SEGMENTS + 1];

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        GetPlayerTransform();
        SetupAudio();
        SetupDangerZone();
    }

    void OnValidate()
    {
        if (playerTransform == null && Application.isPlaying)
        {
            GetPlayerTransform();
        }
    }

    void OnDestroy()
    {
        if (dangerFillMaterial != null) Destroy(dangerFillMaterial);
        if (dangerBorderMaterial != null) Destroy(dangerBorderMaterial);
        if (dangerInnerLineMaterial != null) Destroy(dangerInnerLineMaterial);
        if (dangerMesh != null) Destroy(dangerMesh);
        if (dangerZoneObj != null) Destroy(dangerZoneObj);
    }

    void Start()
    {
        if (playerTransform == null) GetPlayerTransform();

        if (enableAutoStorm)
        {
            StartCoroutine(RoutineAutoStormLoop());
        }
    }

    public Transform GetPlayerTransform()
    {
        if (playerTransform != null) return playerTransform;

        if (PlayerMovement.PlayerTransform != null)
        {
            playerTransform = PlayerMovement.PlayerTransform;
            return playerTransform;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            return playerTransform;
        }

        PlayerMovement pm = FindObjectOfType<PlayerMovement>();
        if (pm != null)
        {
            playerTransform = pm.transform;
            return playerTransform;
        }

        return null;
    }

    private void SetupDangerZone()
    {
        if (dangerZoneObj != null) return;

        dangerZoneObj = new GameObject("[DangerZone_Indicator]");
        dangerZoneObj.transform.position = Vector3.zero;
        dangerZoneObj.transform.rotation = Quaternion.identity;
        dangerZoneObj.transform.localScale = Vector3.one;

        dangerMeshFilter = dangerZoneObj.AddComponent<MeshFilter>();
        dangerMeshRenderer = dangerZoneObj.AddComponent<MeshRenderer>();
        dangerLineRenderer = dangerZoneObj.AddComponent<LineRenderer>();

        GameObject innerObj = new GameObject("[InnerProgressRing]");
        innerObj.transform.SetParent(dangerZoneObj.transform, false);
        dangerInnerLineRenderer = innerObj.AddComponent<LineRenderer>();

        Shader fillShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (fillShader == null) fillShader = Shader.Find("Sprites/Default");
        if (fillShader == null) fillShader = Shader.Find("Unlit/Color");

        dangerFillMaterial = new Material(fillShader);
        if (dangerFillMaterial.HasProperty("_Surface")) dangerFillMaterial.SetFloat("_Surface", 1);
        if (dangerFillMaterial.HasProperty("_Blend")) dangerFillMaterial.SetFloat("_Blend", 0);
        if (dangerFillMaterial.HasProperty("_Cull")) dangerFillMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        dangerFillMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        dangerFillMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        dangerFillMaterial.SetInt("_ZWrite", 0);
        dangerFillMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;
        dangerMeshRenderer.material = dangerFillMaterial;
        dangerMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dangerMeshRenderer.receiveShadows = false;

        Shader borderShader = Shader.Find("Sprites/Default");
        if (borderShader == null) borderShader = fillShader;

        dangerBorderMaterial = new Material(borderShader);
        if (dangerBorderMaterial.HasProperty("_Cull")) dangerBorderMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        dangerLineRenderer.material = dangerBorderMaterial;
        dangerLineRenderer.loop = true;
        dangerLineRenderer.positionCount = CIRCLE_SEGMENTS;
        dangerLineRenderer.startWidth = 0.4f;
        dangerLineRenderer.endWidth = 0.4f;
        dangerLineRenderer.useWorldSpace = true;
        dangerLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dangerLineRenderer.receiveShadows = false;

        dangerInnerLineMaterial = new Material(borderShader);
        if (dangerInnerLineMaterial.HasProperty("_Cull")) dangerInnerLineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        dangerInnerLineRenderer.material = dangerInnerLineMaterial;
        dangerInnerLineRenderer.loop = true;
        dangerInnerLineRenderer.positionCount = CIRCLE_SEGMENTS;
        dangerInnerLineRenderer.startWidth = 0.35f;
        dangerInnerLineRenderer.endWidth = 0.35f;
        dangerInnerLineRenderer.useWorldSpace = true;
        dangerInnerLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dangerInnerLineRenderer.receiveShadows = false;

        dangerMesh = new Mesh();
        dangerMesh.name = "DangerCircleMesh";
        dangerMeshFilter.mesh = dangerMesh;

        dangerZoneObj.SetActive(false);
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
        float currentRadius = (customDangerRadius > 0.1f) ? customDangerRadius : stormRadius;

        // 2. PHÁT TIẾNG TÙ VÀ BÁO HIỆU
        if (hornAudioSource != null && hornClip != null)
        {
            hornAudioSource.volume = hornVolume;
            hornAudioSource.PlayOneShot(hornClip);
        }

        // 3. HIỂN THỊ VÙNG ĐỎ CẢNH BÁO DƯỚI ĐẤT TẠI ĐÚNG VỊ TRÍ ĐÃ KHÓA
        if (enableDangerZone)
        {
            UpdateDangerZoneMesh(lockedCenter, currentRadius);
        }

        // 4. ĐẾM NGƯỢC THỜI GIAN CẢNH BÁO (warningLeadTime - ví dụ 3.5s)
        // Trong thời gian này, Player nghe tiếng tù và & thấy vòng tròn đỏ nhấp nháy dồn dập
        float elapsed = 0f;
        while (elapsed < warningLeadTime)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / warningLeadTime);

            if (enableDangerZone)
            {
                // Cập nhật vòng trong mở rộng dần từ tâm ra viền ngoài (Telegraph Ring)
                UpdateInnerRing(lockedCenter, currentRadius * progress);

                if (dangerFillMaterial != null && dangerBorderMaterial != null)
                {
                    float pulseFreq = Mathf.Lerp(2.5f, 9.0f, progress);
                    float pulseVal = (Mathf.Sin(Time.time * pulseFreq * Mathf.PI * 2f) + 1f) * 0.5f;

                    Color cFill = dangerColor;
                    cFill.a = Mathf.Lerp(dangerColor.a * 0.35f, dangerColor.a, pulseVal);
                    if (dangerFillMaterial.HasProperty("_BaseColor")) dangerFillMaterial.SetColor("_BaseColor", cFill);
                    if (dangerFillMaterial.HasProperty("_Color")) dangerFillMaterial.SetColor("_Color", cFill);

                    Color cBorder = dangerBorderColor;
                    cBorder.a = Mathf.Lerp(dangerBorderColor.a * 0.55f, dangerBorderColor.a, pulseVal);
                    if (dangerBorderMaterial.HasProperty("_BaseColor")) dangerBorderMaterial.SetColor("_BaseColor", cBorder);
                    if (dangerBorderMaterial.HasProperty("_Color")) dangerBorderMaterial.SetColor("_Color", cBorder);

                    if (dangerInnerLineMaterial != null)
                    {
                        Color cInner = dangerBorderColor;
                        cInner.a = 0.95f;
                        if (dangerInnerLineMaterial.HasProperty("_BaseColor")) dangerInnerLineMaterial.SetColor("_BaseColor", cInner);
                        if (dangerInnerLineMaterial.HasProperty("_Color")) dangerInnerLineMaterial.SetColor("_Color", cInner);
                    }
                }
            }
            yield return null;
        }

        // 5. MŨI TÊN CHẠM ĐẤT -> TẮT VÙNG ĐỎ
        if (dangerZoneObj != null) dangerZoneObj.SetActive(false);

        // 6. PHÓNG ĐỒNG LOẠT TOÀN BỘ MŨI TÊN TỪ TRÊN TRỜI XUỐNG ĐÚNG VỊ TRÍ ĐÃ KHÓA
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

    private void UpdateDangerZoneMesh(Vector3 center, float radius)
    {
        if (dangerZoneObj == null) SetupDangerZone();

        Vector3 groundCenter = GetGroundPos(center);
        dangerZoneObj.transform.position = Vector3.zero;

        circleVertices[0] = groundCenter + Vector3.up * 0.08f;
        circleUVs[0] = new Vector2(0.5f, 0.5f);

        Vector3[] linePositions = new Vector3[CIRCLE_SEGMENTS];

        for (int i = 0; i < CIRCLE_SEGMENTS; i++)
        {
            float angle = (float)i / CIRCLE_SEGMENTS * Mathf.PI * 2f;
            Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Vector3 worldPt = center + offset;
            Vector3 groundedPt = GetGroundPos(worldPt) + Vector3.up * 0.08f;

            circleVertices[i + 1] = groundedPt;
            circleUVs[i + 1] = new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f);
            linePositions[i] = groundedPt + Vector3.up * 0.04f;
        }

        int triIdx = 0;
        for (int i = 0; i < CIRCLE_SEGMENTS; i++)
        {
            // Clockwise winding order để pháp tuyến (Normal) hướng lên trời (+Y)
            circleTriangles[triIdx++] = 0;
            circleTriangles[triIdx++] = (i + 1 >= CIRCLE_SEGMENTS) ? 1 : i + 2;
            circleTriangles[triIdx++] = i + 1;
        }

        dangerMesh.Clear();
        dangerMesh.vertices = circleVertices;
        dangerMesh.uv = circleUVs;
        dangerMesh.triangles = circleTriangles;
        dangerMesh.RecalculateNormals();

        dangerLineRenderer.SetPositions(linePositions);
        dangerZoneObj.SetActive(true);

        UpdateInnerRing(center, 0.1f);
    }

    private void UpdateInnerRing(Vector3 center, float innerRadius)
    {
        if (dangerInnerLineRenderer == null) return;

        if (innerRadius <= 0.25f)
        {
            dangerInnerLineRenderer.enabled = false;
            return;
        }

        dangerInnerLineRenderer.enabled = true;
        Vector3[] innerPositions = new Vector3[CIRCLE_SEGMENTS];

        for (int i = 0; i < CIRCLE_SEGMENTS; i++)
        {
            float angle = (float)i / CIRCLE_SEGMENTS * Mathf.PI * 2f;
            Vector3 offset = new Vector3(Mathf.Cos(angle) * innerRadius, 0f, Mathf.Sin(angle) * innerRadius);
            Vector3 worldPt = center + offset;
            innerPositions[i] = GetGroundPos(worldPt) + Vector3.up * 0.14f;
        }

        dangerInnerLineRenderer.SetPositions(innerPositions);
    }

    private Vector3 GetGroundPos(Vector3 rawPos)
    {
        if (Terrain.activeTerrain != null)
        {
            float terrainY = Terrain.activeTerrain.SampleHeight(rawPos) + Terrain.activeTerrain.transform.position.y;
            return new Vector3(rawPos.x, terrainY, rawPos.z);
        }

        Vector3 origin = new Vector3(rawPos.x, rawPos.y + 40f, rawPos.z);
        int obstructionMask = ~LayerMask.GetMask("Character", "Ignore Raycast");
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 80f, obstructionMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }
        return rawPos;
    }

    private Vector3 GetTargetCenter()
    {
        Transform pT = GetPlayerTransform();

        if (targetMode == StormTargetMode.TargetPlayerArea)
        {
            if (pT != null)
            {
                if (playerCenterOffset <= 0.01f)
                {
                    return pT.position;
                }

                Vector2 offset = Random.insideUnitCircle * playerCenterOffset;
                return pT.position + new Vector3(offset.x, 0f, offset.y);
            }
            else
            {
                Debug.LogWarning("[ArrowStormSystem] TargetPlayerArea: Không tìm thấy PlayerTransform! Sử dụng vị trí fallback.");
            }
        }

        if (targetMode == StormTargetMode.TargetFrontlineCenter && MassArmySpawner.Instance != null)
        {
            return MassArmySpawner.Instance.transform.position;
        }

        if (pT != null)
        {
            return pT.position;
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
