using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Đạo Diễn Đại Chiến Điện Ảnh (Cinematic Battle Director)
/// Quản lý 10.000 NPC xung phong và bắn mưa tên 70 độ:
/// - Mặc định đứng yên dàn trận chờ lệnh (không tự ý lao vào đánh trước)
/// - Căn cự ly theo ĐIỂM MỐC TRUNG TÂM (Battlefield Center Landmark): Cả 2 phe đều lấy mốc này để tính khoảng cách chạy và bắn
/// - Lính cận chiến chạy THẲNG theo hàng ngũ về phía điểm mốc (không bị chụm vào 1 điểm)
/// - Cung thủ bắn THẲNG theo hàng ngũ xòe cánh quạt rơi qua điểm mốc (uốn cong người 70 độ ngửa lên trời)
/// - Bấm [F2] để phát lệnh BẮT ĐẦU / TẠM DỪNG XUNG PHONG
/// - Bấm [F3] để RESET TOÀN BỘ 10.000 NPC về vị trí xuất phát tức thì (không cần Play lại)
/// </summary>
public class CinematicBattleDirector : MonoBehaviour
{
    public static CinematicBattleDirector Instance { get; private set; }

    [Header("1. Chế Độ Đạo Diễn (Director Mode)")]
    [Tooltip("Bật chế độ đạo diễn: Lính sẽ tuân theo lệnh của Director thay vì AI thông thường")]
    public bool enableDirectorMode = true;

    [Tooltip("Cờ trạng thái xung phong: Khi TRUE lính mới chạy và bắn tên. Mặc định là FALSE để đợi bấm F2.")]
    public bool isCinematicActive = false;

    [Header("2. Phím Tắt Điều Khiển (Hotkeys)")]
    public KeyCode startChargeKey = KeyCode.F2;
    public KeyCode resetBattlefieldKey = KeyCode.F3;

    [Header("3. Điểm Mốc Trung Tâm Chiến Trường (Battlefield Center Landmark)")]
    [Tooltip("Kéo 1 GameObject làm cột mốc trung tâm (ví dụ điểm giữa 2 phe, cột cờ). Nếu để trống sẽ tự lấy tọa độ (0, 0, 0)")]
    public Transform battleCenterAnchor;
    [Tooltip("Tọa độ mốc dự phòng nếu không kéo Transform vào ô trên")]
    public Vector3 customCenterPoint = Vector3.zero;

    [Header("4. Cự Ly Va Chạm & Tầm Rơi Của Tên")]
    [Tooltip("Bộ binh chạy lấn qua điểm mốc trung tâm bao nhiêu mét để 2 phe giáp lá cà va chạm nhau")]
    public float meleeClashOvershoot = 10f;

    [Tooltip("Cung thủ bắn rơi qua điểm mốc trung tâm bao nhiêu mét để trúng sâu vào đội hình địch")]
    public float archerTargetPastCenter = 150f;

    [Header("5. Tốc Độ & Kiểm Soát Culling")]
    public float meleeRunSpeed = 5.0f;
    [Tooltip("Tích vào đây nếu muốn tắt hoàn toàn Culling khi quay clip")]
    public bool disableCullingForRecording = false;

    [Header("6. Cung Thủ Mưa Tên 70 Độ (Archer Sky Volley)")]
    public float archerShootAngle = 70f;
    public float archerGravity = 18f;
    public float minShootInterval = 3.0f;
    public float maxShootInterval = 5.0f;
    public float horizontalSpreadAngle = 3.5f;
    public float distanceSpread = 45f;
    public int arrowsPerVolley = 4;
    public float arrowDamage = 10f;

    private class CinematicMelee
    {
        public NPCCombat npc;
        public Transform transform;
        public Animator anim;
        public Vector3 initialPosition;
        public Quaternion initialRotation;
        public Vector3 forward;
        public float chargedDistance;
        public float targetChargeDistance;
    }

    private class CinematicArcher
    {
        public NPCCombat npc;
        public Transform transform;
        public Animator anim;
        public ArcherAnimatorHandler handler;
        public Vector3 initialPosition;
        public Quaternion initialRotation;
        public Vector3 forward;
        public float shootTimer;
        public GameObject arrowPrefab;
        public Transform spawnPoint;
        public bool isAiming;
        public float targetShootDistance;
    }

    private List<CinematicMelee> meleeUnits = new List<CinematicMelee>(6000);
    private List<CinematicArcher> archerUnits = new List<CinematicArcher>(4000);
    private HashSet<NPCCombat> registeredNPCs = new HashSet<NPCCombat>();

    private Terrain activeTerrain;
    private float terrainBaseY;
    private bool isInitialized = false;
    private bool wasCinematicActive = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);

        activeTerrain = Terrain.activeTerrain;
        if (activeTerrain != null)
        {
            terrainBaseY = activeTerrain.transform.position.y;
        }
    }

    void OnEnable()
    {
        if (Instance == null) Instance = this;
    }

    void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (NPCCullingManager.Instance != null)
        {
            NPCCullingManager.Instance.disableCulling = disableCullingForRecording;
        }
    }

    public Vector3 GetCenterPosition()
    {
        if (battleCenterAnchor != null) return battleCenterAnchor.position;
        return customCenterPoint;
    }

    public void RegisterUnit(NPCCombat npc)
    {
        if (npc == null || registeredNPCs.Contains(npc)) return;
        registeredNPCs.Add(npc);

        ArcherAnimatorHandler archerHandler = npc.GetComponent<ArcherAnimatorHandler>();
        Animator anim = npc.GetComponentInChildren<Animator>();

        Vector3 center = GetCenterPosition();
        float distToCenter = Vector3.Dot(center - npc.transform.position, npc.transform.forward);

        if (archerHandler != null)
        {
            archerUnits.Add(new CinematicArcher
            {
                npc = npc,
                transform = npc.transform,
                anim = anim,
                handler = archerHandler,
                initialPosition = npc.transform.position,
                initialRotation = npc.transform.rotation,
                forward = npc.transform.forward,
                shootTimer = Random.Range(0.5f, minShootInterval),
                arrowPrefab = archerHandler.arrowPrefab,
                spawnPoint = archerHandler.arrowSpawnPoint,
                isAiming = false,
                targetShootDistance = Mathf.Max(50f, distToCenter + archerTargetPastCenter)
            });
        }
        else
        {
            meleeUnits.Add(new CinematicMelee
            {
                npc = npc,
                transform = npc.transform,
                anim = anim,
                initialPosition = npc.transform.position,
                initialRotation = npc.transform.rotation,
                forward = npc.transform.forward,
                chargedDistance = 0f,
                targetChargeDistance = Mathf.Max(20f, distToCenter + meleeClashOvershoot)
            });
        }

        if (!isCinematicActive)
        {
            if (anim != null) anim.SetFloat("Speed", 0f);
        }
        else
        {
            CharacterController cc = npc.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            UnityEngine.AI.NavMeshAgent ag = npc.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (ag != null) ag.enabled = false;
        }
    }

    [ContextMenu("Thu Thập Toàn Bộ Lính (Scan Armies)")]
    public void CollectAndInitializeUnits()
    {
        meleeUnits.Clear();
        archerUnits.Clear();
        registeredNPCs.Clear();

        if (NPCCullingManager.Instance != null && NPCCullingManager.Instance.AllNPCs.Count > 0)
        {
            var list = NPCCullingManager.Instance.AllNPCs;
            for (int i = 0; i < list.Count; i++)
            {
                RegisterUnit(list[i]);
            }
        }
        else
        {
            NPCCombat[] allNPCs = FindObjectsOfType<NPCCombat>(true);
            if (allNPCs != null)
            {
                for (int i = 0; i < allNPCs.Length; i++)
                {
                    RegisterUnit(allNPCs[i]);
                }
            }
        }

        RecalculateTargetDistances();
        isInitialized = true;
        Debug.Log($"<color=green>[CinematicBattleDirector]</color> Đã nạp thành công: {meleeUnits.Count} Bộ binh & {archerUnits.Count} Cung thủ!");
    }

    public void RecalculateTargetDistances()
    {
        Vector3 center = GetCenterPosition();

        for (int i = 0; i < meleeUnits.Count; i++)
        {
            CinematicMelee u = meleeUnits[i];
            if (u == null) continue;
            float distToCenter = Vector3.Dot(center - u.initialPosition, u.forward);
            u.targetChargeDistance = Mathf.Max(20f, distToCenter + meleeClashOvershoot);
        }

        for (int i = 0; i < archerUnits.Count; i++)
        {
            CinematicArcher a = archerUnits[i];
            if (a == null) continue;
            float distToCenter = Vector3.Dot(center - a.initialPosition, a.forward);
            a.targetShootDistance = Mathf.Max(50f, distToCenter + archerTargetPastCenter);
        }
    }

    void Update()
    {
        // 1. Phím tắt Bắt đầu / Tạm dừng Xung phong (F2)
        if (Input.GetKeyDown(startChargeKey))
        {
            if (!isCinematicActive)
            {
                StartCinematicCharge();
            }
            else
            {
                StopCinematicCharge();
            }
        }

        // 2. Phím tắt Reset toàn bộ chiến trường về vị trí xuất phát (F3)
        if (Input.GetKeyDown(resetBattlefieldKey))
        {
            ResetBattlefield();
        }

        // 3. Đồng bộ Culling
        if (NPCCullingManager.Instance != null && NPCCullingManager.Instance.disableCulling != disableCullingForRecording)
        {
            NPCCullingManager.Instance.disableCulling = disableCullingForRecording;
        }

        // Theo dõi bật/tắt từ Inspector
        if (isCinematicActive != wasCinematicActive)
        {
            wasCinematicActive = isCinematicActive;
            if (isCinematicActive) StartCinematicCharge();
            else StopCinematicCharge();
        }

        if (!enableDirectorMode || !isCinematicActive) return;

        if (!isInitialized || (meleeUnits.Count == 0 && archerUnits.Count == 0))
        {
            CollectAndInitializeUnits();
            if (!isInitialized || (meleeUnits.Count == 0 && archerUnits.Count == 0)) return;
        }

        float dt = Time.deltaTime;

        // 4. XỬ LÝ BỘ BINH LAO VÀO NHAU (CHỈ XỬ LÝ LÍNH GẦN CAM KHÔNG BỊ CULLING)
        int meleeCount = meleeUnits.Count;
        float moveStep = meleeRunSpeed * dt;

        for (int i = 0; i < meleeCount; i++)
        {
            CinematicMelee u = meleeUnits[i];
            if (u == null || u.transform == null) continue;

            if (u.npc != null && u.npc.IsCulled) continue;

            if (u.chargedDistance < u.targetChargeDistance)
            {
                Vector3 pos = u.transform.position + u.forward * moveStep;

                if (activeTerrain != null)
                {
                    pos.y = activeTerrain.SampleHeight(pos) + terrainBaseY;
                }

                u.transform.position = pos;
                u.chargedDistance += moveStep;

                if (u.anim != null)
                {
                    u.anim.SetFloat("Speed", 1.0f);
                }
            }
            else
            {
                if (u.anim != null)
                {
                    u.anim.SetFloat("Speed", 0f);
                }
            }
        }

        // 5. XỬ LÝ CUNG THỦ BẮN MƯA TÊN 70 ĐỘ (CHỈ BẮN KHI GẦN CAM KHÔNG BỊ CULLING)
        int archerCount = archerUnits.Count;
        for (int i = 0; i < archerCount; i++)
        {
            CinematicArcher a = archerUnits[i];
            if (a == null || a.transform == null) continue;

            if (a.npc != null && a.npc.IsCulled) continue;

            a.shootTimer -= dt;

            // Trước khi bắn 0.8s: Giương cung ngửa lên trời (Aiming posture góc cao 70 độ UỐN CONG CỘT SỐNG)
            if (a.shootTimer <= 0.8f && !a.isAiming)
            {
                a.isAiming = true;
                if (a.handler != null)
                {
                    a.handler.SetAiming(true, true); // Uốn cong cột sống 70 độ & kéo căng dây cung
                }
                else
                {
                    if (a.anim != null) a.anim.SetBool("isAiming", true);
                    if (a.handler != null && a.handler.bowAnim != null) a.handler.bowAnim.SetBool("isPulling", true);
                }
            }

            if (a.shootTimer <= 0f)
            {
                FireArcherVolley(a);
                a.isAiming = false;
                if (a.handler != null)
                {
                    a.handler.SetAiming(false, false);
                }
                else
                {
                    if (a.anim != null) a.anim.SetBool("isAiming", false);
                    if (a.handler != null && a.handler.bowAnim != null) a.handler.bowAnim.SetBool("isPulling", false);
                }
                a.shootTimer = Random.Range(minShootInterval, maxShootInterval);
            }
        }
    }

    private void FireArcherVolley(CinematicArcher archer)
    {
        if (archer.arrowPrefab == null && archer.handler != null)
        {
            archer.arrowPrefab = archer.handler.arrowPrefab;
        }
        if (archer.arrowPrefab == null) return;

        Vector3 spawnBase = archer.spawnPoint != null 
            ? archer.spawnPoint.position 
            : (archer.transform.position + Vector3.up * 1.5f + archer.forward * 0.5f);

        Vector3 baseForward = archer.forward;
        float g = archerGravity;
        float baseShootDist = archer.targetShootDistance;

        for (int k = 0; k < arrowsPerVolley; k++)
        {
            float fanAngleY = ((k - 1.5f) * 1.8f) + Random.Range(-horizontalSpreadAngle * 0.4f, horizontalSpreadAngle * 0.4f);
            Quaternion rotSpread = Quaternion.Euler(0, fanAngleY, 0);
            Vector3 dirXZ = (rotSpread * baseForward).normalized;

            float angleDeg = archerShootAngle + ((k % 2 == 0 ? 1f : -1f) * 1.2f) + Random.Range(-0.8f, 0.8f);
            float theta = angleDeg * Mathf.Deg2Rad;
            float cosTheta = Mathf.Cos(theta);
            float sinTheta = Mathf.Sin(theta);

            float dist = Mathf.Max(50f, baseShootDist + (k - 1.5f) * 20f + Random.Range(-distanceSpread * 0.5f, distanceSpread * 0.5f));

            float sin2Theta = Mathf.Sin(2f * theta);
            if (sin2Theta <= 0.01f) sin2Theta = 0.01f;
            float v = Mathf.Sqrt((g * dist) / sin2Theta);

            Vector3 shootDir = (dirXZ * cosTheta + Vector3.up * sinTheta).normalized;
            Quaternion arrowRot = Quaternion.LookRotation(shootDir);

            Vector3 offset = (archer.transform.right * (k - 1.5f) * 0.15f) + (archer.transform.up * ((k % 2) - 0.5f) * 0.08f);
            Vector3 arrowPos = spawnBase + offset;

            GameObject arrowObj = ArrowPoolManager.Instance != null 
                ? ArrowPoolManager.Instance.GetArrow(arrowPos, arrowRot, archer.arrowPrefab)
                : Instantiate(archer.arrowPrefab, arrowPos, arrowRot);

            if (arrowObj != null)
            {
                ArrowProjectile proj = arrowObj.GetComponent<ArrowProjectile>();
                if (proj != null)
                {
                    proj.Init(archer.transform.gameObject, arrowDamage, v, g);
                }
            }
        }

        if (archer.anim != null)
        {
            archer.anim.SetTrigger("DoShoot");
        }
        if (archer.handler != null && archer.handler.bowAnim != null)
        {
            archer.handler.bowAnim.SetTrigger("DoRelease");
        }
    }

    [ContextMenu("▶ BẮT ĐẦU TỔNG XUNG PHONG (F2)")]
    public void StartCinematicCharge()
    {
        isCinematicActive = true;
        wasCinematicActive = true;

        if (!isInitialized || meleeUnits.Count == 0)
        {
            CollectAndInitializeUnits();
        }

        RecalculateTargetDistances();

        // Tắt toàn bộ controller và agent để không bị va chạm capsule vật lý cản trở
        for (int i = 0; i < meleeUnits.Count; i++)
        {
            if (meleeUnits[i] != null && meleeUnits[i].npc != null)
            {
                var cc = meleeUnits[i].npc.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                var ag = meleeUnits[i].npc.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (ag != null) ag.enabled = false;
            }
        }
        for (int i = 0; i < archerUnits.Count; i++)
        {
            if (archerUnits[i] != null && archerUnits[i].npc != null)
            {
                var cc = archerUnits[i].npc.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                var ag = archerUnits[i].npc.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (ag != null) ag.enabled = false;
            }
        }

        Debug.Log("<color=yellow>[CinematicBattleDirector]</color> ▶ ĐÃ BẮT ĐẦU ĐẠI XUNG PHONG & MƯA TÊN!");
    }

    [ContextMenu("⏹ DỪNG XUNG PHONG")]
    public void StopCinematicCharge()
    {
        isCinematicActive = false;
        wasCinematicActive = false;

        for (int i = 0; i < meleeUnits.Count; i++)
        {
            if (meleeUnits[i] != null && meleeUnits[i].anim != null)
            {
                meleeUnits[i].anim.SetFloat("Speed", 0f);
            }
        }
        for (int i = 0; i < archerUnits.Count; i++)
        {
            if (archerUnits[i] != null)
            {
                if (archerUnits[i].handler != null)
                {
                    archerUnits[i].handler.SetAiming(false, false);
                }
                else
                {
                    if (archerUnits[i].anim != null) archerUnits[i].anim.SetBool("isAiming", false);
                    if (archerUnits[i].handler != null && archerUnits[i].handler.bowAnim != null)
                        archerUnits[i].handler.bowAnim.SetBool("isPulling", false);
                }
            }
        }

        Debug.Log("<color=red>[CinematicBattleDirector]</color> ⏹ ĐÃ DỪNG XUNG PHONG!");
    }

    [ContextMenu("🔄 RESET TOÀN BỘ VỀ VỊ TRÍ XUẤT PHÁT (F3)")]
    public void ResetBattlefield()
    {
        isCinematicActive = false;
        wasCinematicActive = false;

        // 1. Reset Bộ binh
        for (int i = 0; i < meleeUnits.Count; i++)
        {
            CinematicMelee u = meleeUnits[i];
            if (u == null || u.transform == null) continue;

            u.transform.position = u.initialPosition;
            u.transform.rotation = u.initialRotation;
            u.chargedDistance = 0f;

            if (u.anim != null)
            {
                u.anim.SetFloat("Speed", 0f);
            }
        }

        // 2. Reset Cung thủ
        for (int i = 0; i < archerUnits.Count; i++)
        {
            CinematicArcher a = archerUnits[i];
            if (a == null || a.transform == null) continue;

            a.transform.position = a.initialPosition;
            a.transform.rotation = a.initialRotation;
            a.shootTimer = Random.Range(0.5f, minShootInterval);
            a.isAiming = false;

            if (a.handler != null)
            {
                a.handler.SetAiming(false, false);
            }
            else
            {
                if (a.anim != null) a.anim.SetBool("isAiming", false);
                if (a.handler != null && a.handler.bowAnim != null) a.handler.bowAnim.SetBool("isPulling", false);
            }
        }

        // 3. Dọn sạch toàn bộ mũi tên đang bay trên trời
        ArrowProjectile[] activeArrows = FindObjectsOfType<ArrowProjectile>();
        for (int i = 0; i < activeArrows.Length; i++)
        {
            if (activeArrows[i] != null)
            {
                if (ArrowPoolManager.Instance != null)
                    ArrowPoolManager.Instance.ReturnArrow(activeArrows[i].gameObject);
                else
                    Destroy(activeArrows[i].gameObject);
            }
        }

        // 4. Dọn sạch toàn bộ mũi tên cắm dưới đất
        if (GroundArrowManager.Instance != null)
        {
            GroundArrowManager.Instance.ClearAllGroundedArrows();
        }

        RecalculateTargetDistances();
        Debug.Log("<color=cyan>[CinematicBattleDirector]</color> 🔄 ĐÃ RESET TOÀN BỘ CHIẾN TRƯỜNG VỀ VỊ TRÍ GỐC! Sẵn sàng bấm F2 quay cảnh mới!");
    }
}
