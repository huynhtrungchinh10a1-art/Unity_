using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MassArmySpawner : MonoBehaviour
{
    [Header("1. Prefabs (4 Chỗ Kéo Thả Prefab)")]
    public GameObject teamASwordPrefab;

    public GameObject teamAArcherPrefab;

    public GameObject teamBSwordPrefab;

    public GameObject teamBArcherPrefab;

    [Header("2. Số Lượng Quân (Mỗi Team: 3k Kiếm + 2k Cung = 5k)")]
    public int swordCountPerTeam = 3000;

    public int archerCountPerTeam = 2000;

    [Header("3. Đội Hình & Hàng Lối")]
    public int unitsPerRow = 100;

    public float spacingX = 1.8f;

    public float spacingZ = 2.0f;

    public float opposingDistance = 45.0f;

    [Header("4. Điểm Mốc Cuối Hàng (Tùy Chọn)")]
    public Transform teamABacklineAnchor;

    public Transform teamBBacklineAnchor;

    [Header("5. Chống Kẹt Đất & Lơ Lửng (Ground Snapping)")]
    public bool snapToGround = true;

    public float raycastStartHeight = 60f;

    public float groundOffset = 0.05f;

    [Header("6. Hiệu Năng & Đồng Bộ Loading")]
    public bool spawnOnStart = true;

    public bool spawnInBatches = true;

    public int spawnsPerFrame = 250;

    public bool syncWithLoadingScreen = true;

    [Header("Debug Status")]
    [SerializeField] private int currentSpawnedCount = 0;
    [SerializeField] private bool isSpawning = false;

    private Transform teamAParent;
    private Transform teamBParent;
    private Coroutine spawnCoroutine;

    void Start()
    {
        if (spawnOnStart)
        {
            StartSpawn();
        }
    }

    [ContextMenu("Spawn 10.000 NPC Ngay")]
    public void StartSpawn()
    {
        if (isSpawning)
        {
            Debug.LogWarning("[MassArmySpawner] Đang trong quá trình spawn, vui lòng đợi!");
            return;
        }

        // Kiểm tra tối thiểu phải có prefab phe A
        if (teamASwordPrefab == null && teamAArcherPrefab == null)
        {
            Debug.LogError("[MassArmySpawner] Chưa gán Prefab cho Phe A!");
            return;
        }

        ClearAllSpawned();

        // Tạo thư mục cha để gom lính gọn gàng trên Hierarchy
        GameObject goA = new GameObject("[Army_TeamA]");
        goA.transform.position = transform.position;
        teamAParent = goA.transform;

        GameObject goB = new GameObject("[Army_TeamB]");
        goB.transform.position = transform.position;
        teamBParent = goB.transform;

        if (spawnInBatches && Application.isPlaying)
        {
            spawnCoroutine = StartCoroutine(SpawnBatchedRoutine());
        }
        else
        {
            SpawnInstant();
        }
    }

    [ContextMenu("Xóa Toàn Bộ Lính Đã Spawn")]
    public void ClearAllSpawned()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        isSpawning = false;
        currentSpawnedCount = 0;

        GameObject oldA = GameObject.Find("[Army_TeamA]");
        if (oldA != null) DestroyImmediate(oldA);

        GameObject oldB = GameObject.Find("[Army_TeamB]");
        if (oldB != null) DestroyImmediate(oldB);
    }

    private void SpawnInstant()
    {
        List<SpawnSlot> slots = CalculateAllSpawnSlots();
        int total = slots.Count;

        for (int i = 0; i < total; i++)
        {
            CreateNPCAt(slots[i]);
        }

        currentSpawnedCount = total;
        if (syncWithLoadingScreen && LoadingTutorialScreen.Instance != null)
        {
            LoadingTutorialScreen.Instance.CompleteLoading();
        }
        Debug.Log($"<color=green>[MassArmySpawner]</color> Đã tạo xong tức thì {currentSpawnedCount} NPC đại chiến!");
    }

    private IEnumerator SpawnBatchedRoutine()
    {
        isSpawning = true;
        currentSpawnedCount = 0;

        List<SpawnSlot> slots = CalculateAllSpawnSlots();
        int total = slots.Count;
        int spawnedInCurrentFrame = 0;

        for (int i = 0; i < total; i++)
        {
            CreateNPCAt(slots[i]);
            currentSpawnedCount++;
            spawnedInCurrentFrame++;

            // Đồng bộ tiến trình với màn hình Loading
            if (syncWithLoadingScreen && LoadingTutorialScreen.Instance != null && i % 100 == 0)
            {
                float progress = (float)currentSpawnedCount / Mathf.Max(1, total);
                LoadingTutorialScreen.Instance.SetProgress(progress, $"Đang dàn trận: {currentSpawnedCount} / {total} quân...");
            }

            if (spawnedInCurrentFrame >= spawnsPerFrame)
            {
                spawnedInCurrentFrame = 0;
                yield return null; // Nhường frame cho máy hiển thị mượt
            }
        }

        isSpawning = false;

        // Báo cho màn hình loading biết đã dàn trận xong 100%
        if (syncWithLoadingScreen && LoadingTutorialScreen.Instance != null)
        {
            LoadingTutorialScreen.Instance.CompleteLoading();
        }

        Debug.Log($"<color=green>[MassArmySpawner]</color> Hoàn tất sinh {currentSpawnedCount} NPC đại chiến mượt mà!");
    }

    private struct SpawnSlot
    {
        public Vector3 position;
        public Quaternion rotation;
        public Team team;
        public GameObject prefab;
        public Transform parent;
    }

    private List<SpawnSlot> CalculateAllSpawnSlots()
    {
        int totalPerTeam = swordCountPerTeam + archerCountPerTeam;
        int totalNPCs = totalPerTeam * 2;
        List<SpawnSlot> slots = new List<SpawnSlot>(totalNPCs);

        int cols = Mathf.Max(1, unitsPerRow);
        int archerRows = Mathf.CeilToInt((float)archerCountPerTeam / cols);
        int swordRows = Mathf.CeilToInt((float)swordCountPerTeam / cols);
        int totalRows = archerRows + swordRows;

        Vector3 center = transform.position;

        // Phục hồi fallback nếu thiếu prefab phe B thì dùng lại của phe A
        GameObject prefabSwordA = teamASwordPrefab;
        GameObject prefabArcherA = (teamAArcherPrefab != null) ? teamAArcherPrefab : teamASwordPrefab;
        GameObject prefabSwordB = (teamBSwordPrefab != null) ? teamBSwordPrefab : prefabSwordA;
        GameObject prefabArcherB = (teamBArcherPrefab != null) ? teamBArcherPrefab : prefabArcherA;

        // ==================== TÍNH TOÁN CHO PHE A (Nhìn về phía +Z) ====================
        // Vị trí cuối hàng (Backline) của Phe A
        Vector3 backlinePosA;
        if (teamABacklineAnchor != null)
        {
            backlinePosA = teamABacklineAnchor.position;
        }
        else
        {
            // Tự tính: Lùi về -Z sao cho hàng kiếm cuối cùng cách tiền tuyến opposingDistance/2
            float zStart = center.z - (opposingDistance * 0.5f) - (totalRows * spacingZ);
            float xStart = center.x - (cols * spacingX * 0.5f);
            backlinePosA = new Vector3(xStart, center.y, zStart);
        }

        // Tạo Phe A TỪ DƯỚI LÊN: Cung thủ tạo trước (ở xa), Kiếm tạo sau (ở gần tiền tuyến)
        int aArcherCreated = 0;
        int aSwordCreated = 0;

        for (int r = 0; r < totalRows; r++)
        {
            bool isArcherRow = (r < archerRows);

            for (int c = 0; c < cols; c++)
            {
                if (isArcherRow)
                {
                    if (aArcherCreated >= archerCountPerTeam) continue;
                    aArcherCreated++;
                }
                else
                {
                    if (aSwordCreated >= swordCountPerTeam) continue;
                    aSwordCreated++;
                }

                Vector3 pos = backlinePosA + new Vector3(c * spacingX, 0, r * spacingZ);
                Quaternion rot = Quaternion.Euler(0, 0, 0); // Nhìn thẳng về +Z

                slots.Add(new SpawnSlot
                {
                    position = pos,
                    rotation = rot,
                    team = Team.TeamA,
                    prefab = isArcherRow ? prefabArcherA : prefabSwordA,
                    parent = teamAParent
                });
            }
        }

        // ==================== TÍNH TOÁN CHO PHE B (Nhìn về phía -Z đối diện) ====================
        // Vị trí cuối hàng (Backline) của Phe B
        Vector3 backlinePosB;
        if (teamBBacklineAnchor != null)
        {
            backlinePosB = teamBBacklineAnchor.position;
        }
        else
        {
            // Tự tính: Lùi về +Z sao cho hàng kiếm cuối cùng cách tiền tuyến opposingDistance/2
            float zStart = center.z + (opposingDistance * 0.5f) + (totalRows * spacingZ);
            float xStart = center.x - (cols * spacingX * 0.5f);
            backlinePosB = new Vector3(xStart, center.y, zStart);
        }

        // Tạo Phe B TỪ DƯỚI LÊN: Cung thủ ở xa nhất (+Z), Kiếm ở gần tiền tuyến (-Z hướng về A)
        int bArcherCreated = 0;
        int bSwordCreated = 0;

        for (int r = 0; r < totalRows; r++)
        {
            bool isArcherRow = (r < archerRows);

            for (int c = 0; c < cols; c++)
            {
                if (isArcherRow)
                {
                    if (bArcherCreated >= archerCountPerTeam) continue;
                    bArcherCreated++;
                }
                else
                {
                    if (bSwordCreated >= swordCountPerTeam) continue;
                    bSwordCreated++;
                }

                // Phe B tiến dần về phía -Z (về phía trung tâm đối đầu phe A)
                Vector3 pos = backlinePosB + new Vector3(c * spacingX, 0, -r * spacingZ);
                Quaternion rot = Quaternion.Euler(0, 180, 0); // Nhìn ngược về -Z

                slots.Add(new SpawnSlot
                {
                    position = pos,
                    rotation = rot,
                    team = Team.TeamB,
                    prefab = isArcherRow ? prefabArcherB : prefabSwordB,
                    parent = teamBParent
                });
            }
        }

        return slots;
    }

    private void CreateNPCAt(SpawnSlot slot)
    {
        if (slot.prefab == null) return;

        Vector3 spawnPos = slot.position;
        if (snapToGround)
        {
            spawnPos = GetGroundedPosition(spawnPos);
        }

        GameObject npcObj = Instantiate(slot.prefab, spawnPos, slot.rotation, slot.parent);

        // Thiết lập phe phái
        HealthAndTeam ht = npcObj.GetComponent<HealthAndTeam>();
        if (ht != null)
        {
            ht.teamCurrent = slot.team;
        }

        // Đảm bảo NavMeshAgent dính sát mặt đất và không bị kẹt
        UnityEngine.AI.NavMeshAgent agent = npcObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.Warp(spawnPos);
        }
    }

    private Vector3 GetGroundedPosition(Vector3 rawPos)
    {
        // Bắn tia Raycast từ trên trời thẳng đứng xuống dưới để dò bề mặt đất/đồi núi
        Vector3 rayOrigin = new Vector3(rawPos.x, rawPos.y + raycastStartHeight, rawPos.z);
        int obstructionMask = ~LayerMask.GetMask("Character", "Ignore Raycast");

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastStartHeight * 2f, obstructionMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * groundOffset;
        }

        // Dự phòng nếu không va chạm Collider: Dò bề mặt NavMesh
        if (UnityEngine.AI.NavMesh.SamplePosition(rawPos, out UnityEngine.AI.NavMeshHit navHit, 15f, UnityEngine.AI.NavMesh.AllAreas))
        {
            return navHit.position + Vector3.up * groundOffset;
        }

        return rawPos;
    }

    private void OnDrawGizmosSelected()
    {
        // Vẽ trực quan phạm vi 2 đội quân trong Scene View
        Vector3 center = transform.position;
        int cols = Mathf.Max(1, unitsPerRow);
        int totalRows = Mathf.CeilToInt((float)(swordCountPerTeam + archerCountPerTeam) / cols);

        float armyWidth = cols * spacingX;
        float armyDepth = totalRows * spacingZ;

        // Vùng Phe A (Xanh lam)
        Gizmos.color = new Color(0.2f, 0.4f, 1f, 0.4f);
        Vector3 centerA = center - new Vector3(0, 0, (opposingDistance * 0.5f) + (armyDepth * 0.5f));
        Gizmos.DrawWireCube(centerA, new Vector3(armyWidth, 2f, armyDepth));

        // Vùng Phe B (Đỏ)
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
        Vector3 centerB = center + new Vector3(0, 0, (opposingDistance * 0.5f) + (armyDepth * 0.5f));
        Gizmos.DrawWireCube(centerB, new Vector3(armyWidth, 2f, armyDepth));

        // Khoảng trống tiền tuyến giữa 2 phe (Vàng)
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(center - new Vector3(armyWidth * 0.5f, 0, opposingDistance * 0.5f), center + new Vector3(armyWidth * 0.5f, 0, -opposingDistance * 0.5f));
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(MassArmySpawner))]
public class MassArmySpawnerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MassArmySpawner spawner = (MassArmySpawner)target;
        int totalPerTeam = spawner.swordCountPerTeam + spawner.archerCountPerTeam;
        int totalAll = totalPerTeam * 2;

        GUILayout.Space(15);
        GUI.backgroundColor = new Color(0.2f, 0.85f, 0.3f);
        if (GUILayout.Button($"▶ SPAWN ĐẠI CHIẾN ({totalAll} NPC)", GUILayout.Height(38)))
        {
            spawner.StartSpawn();
        }

        GUI.backgroundColor = new Color(0.95f, 0.3f, 0.25f);
        if (GUILayout.Button("🗑 XÓA TOÀN BỘ LÍNH", GUILayout.Height(28)))
        {
            spawner.ClearAllSpawned();
        }
        GUI.backgroundColor = Color.white;
    }
}
#endif

