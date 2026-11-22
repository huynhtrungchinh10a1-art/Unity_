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

    [Header("3. Đội Hình Dàn Trận (Khi Tắt Hỗn Chiến)")]
    public int unitsPerRow = 100;

    public float spacingX = 1.8f;

    public float spacingZ = 2.0f;

    public float opposingDistance = 45.0f;

    public Transform teamABacklineAnchor;

    public Transform teamBBacklineAnchor;

    [Header("4. Hỗn Chiến Ngẫu Nhiên (Chaotic Battle Mode)")]
    public bool chaoticBattleMode = false;

    public int targetNpcsPerCell = 10;

    public float minSpawnDistance = 1.0f;

    [Header("4.1. Phân Bổ 3 Khu Vực Chiến Trường (3-Zone Battlefield)")]
    public bool enable3ZoneSpawning = true;

    [Header("- Tỉ Lệ Diện Tích 3 Khu (Area Ratios)")]
    [Range(0.05f, 0.5f)] public float zone1AreaRatio = 0.20f; 
    [Range(0.1f, 0.8f)]  public float zone2AreaRatio = 0.50f; 

    [Header("- Số Lượng Lính Mỗi Khu (Troop Counts)")]
    public int zone1TroopCount = 1000; 
    public int zone2TroopCount = 6500; 
    public int zone3TroopCount = 2500; 

    [Tooltip("Nếu bật: Khu 1 chủ yếu phe A (bảo hộ Player), Khu 2 hỗn chiến 50/50, Khu 3 chủ yếu phe B")]
    public bool realisticFrontlineTeams = true;

    public Transform corner1;
    public Transform corner2;
    public Transform corner3;
    public Transform corner4;

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

    public static MassArmySpawner Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

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

    private struct NPCSpawnData
    {
        public Team team;
        public GameObject prefab;
        public Transform parent;
    }

    private List<SpawnSlot> CalculateAllSpawnSlots()
    {
        if (chaoticBattleMode)
        {
            return CalculateChaoticSpawnSlots();
        }

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

    private List<SpawnSlot> CalculateChaoticSpawnSlots()
    {
        if (corner1 == null || corner2 == null || corner3 == null || corner4 == null)
        {
            Debug.LogError("[MassArmySpawner] Chưa gán đủ 4 góc mốc!");
            return new List<SpawnSlot>();
        }

        // 1. Tọa độ bao phủ từ 4 góc
        float minX = Mathf.Min(corner1.position.x, corner2.position.x, corner3.position.x, corner4.position.x);
        float maxX = Mathf.Max(corner1.position.x, corner2.position.x, corner3.position.x, corner4.position.x);
        float minZ = Mathf.Min(corner1.position.z, corner2.position.z, corner3.position.z, corner4.position.z);
        float maxZ = Mathf.Max(corner1.position.z, corner2.position.z, corner3.position.z, corner4.position.z);

        // 2. Kích thước Cell (lấy theo BattlefieldManager nếu có)
        float cellSize = 10f;
        if (BattlefieldManager.Instance != null && BattlefieldManager.Instance.cellSize > 1f)
        {
            cellSize = BattlefieldManager.Instance.cellSize;
        }

        if (maxX - minX < cellSize) maxX = minX + cellSize;
        if (maxZ - minZ < cellSize) maxZ = minZ + cellSize;

        if (enable3ZoneSpawning)
        {
            return Calculate3ZoneChaoticSlots(minX, maxX, minZ, maxZ, cellSize);
        }

        int cellsX = Mathf.Max(1, Mathf.FloorToInt((maxX - minX) / cellSize));
        int cellsZ = Mathf.Max(1, Mathf.FloorToInt((maxZ - minZ) / cellSize));

        List<Vector2Int> cellIndices = new List<Vector2Int>(cellsX * cellsZ);
        for (int x = 0; x < cellsX; x++)
        {
            for (int z = 0; z < cellsZ; z++)
            {
                cellIndices.Add(new Vector2Int(x, z));
            }
        }

        // Xáo trộn thứ tự các cell để lính rải đều ngẫu nhiên
        for (int i = cellIndices.Count - 1; i > 0; i--)
        {
            int rnd = Random.Range(0, i + 1);
            Vector2Int tmp = cellIndices[i];
            cellIndices[i] = cellIndices[rnd];
            cellIndices[rnd] = tmp;
        }

        // 3. Gom toàn bộ quân số 2 phe vào danh sách chung
        GameObject prefabSwordA = teamASwordPrefab;
        GameObject prefabArcherA = (teamAArcherPrefab != null) ? teamAArcherPrefab : teamASwordPrefab;
        GameObject prefabSwordB = (teamBSwordPrefab != null) ? teamBSwordPrefab : prefabSwordA;
        GameObject prefabArcherB = (teamBArcherPrefab != null) ? teamBArcherPrefab : prefabArcherA;

        int totalNpcs = (swordCountPerTeam + archerCountPerTeam) * 2;
        List<NPCSpawnData> allNpcs = new List<NPCSpawnData>(totalNpcs);

        for (int i = 0; i < swordCountPerTeam; i++)
        {
            if (prefabSwordA != null)
                allNpcs.Add(new NPCSpawnData { team = Team.TeamA, prefab = prefabSwordA, parent = teamAParent });
        }
        for (int i = 0; i < archerCountPerTeam; i++)
        {
            if (prefabArcherA != null)
                allNpcs.Add(new NPCSpawnData { team = Team.TeamA, prefab = prefabArcherA, parent = teamAParent });
        }
        for (int i = 0; i < swordCountPerTeam; i++)
        {
            if (prefabSwordB != null)
                allNpcs.Add(new NPCSpawnData { team = Team.TeamB, prefab = prefabSwordB, parent = teamBParent });
        }
        for (int i = 0; i < archerCountPerTeam; i++)
        {
            if (prefabArcherB != null)
                allNpcs.Add(new NPCSpawnData { team = Team.TeamB, prefab = prefabArcherB, parent = teamBParent });
        }

        // Xáo trộn ngẫu nhiên hoàn toàn (Fisher-Yates Shuffle)
        for (int i = allNpcs.Count - 1; i > 0; i--)
        {
            int rnd = Random.Range(0, i + 1);
            var tmp = allNpcs[i];
            allNpcs[i] = allNpcs[rnd];
            allNpcs[rnd] = tmp;
        }

        // 4. Phân bổ vào các ô Cell (Round 1: Tối đa limitPerCell, Round 2: Chia đều lính dư)
        int totalCells = cellIndices.Count;
        List<List<NPCSpawnData>> cellBuckets = new List<List<NPCSpawnData>>(totalCells);
        for (int i = 0; i < totalCells; i++)
        {
            cellBuckets.Add(new List<NPCSpawnData>());
        }

        int limitPerCell = Mathf.Max(1, targetNpcsPerCell);
        int npcIndex = 0;
        int totalNpcCount = allNpcs.Count;

        for (int c = 0; c < totalCells && npcIndex < totalNpcCount; c++)
        {
            int take = Mathf.Min(limitPerCell, totalNpcCount - npcIndex);
            for (int k = 0; k < take; k++)
            {
                cellBuckets[c].Add(allNpcs[npcIndex++]);
            }
        }

        int roundRobin = 0;
        while (npcIndex < totalNpcCount)
        {
            cellBuckets[roundRobin].Add(allNpcs[npcIndex++]);
            roundRobin = (roundRobin + 1) % totalCells;
        }

        // 5. Tạo các vị trí ngẫu nhiên có khoảng cách an toàn, tránh đè lên nhau gây lag
        float minDstSqr = minSpawnDistance * minSpawnDistance;
        List<Vector3> placedInCell = new List<Vector3>();
        List<SpawnSlot> slots = new List<SpawnSlot>(totalNpcCount);

        for (int c = 0; c < totalCells; c++)
        {
            Vector2Int coord = cellIndices[c];
            float cellStartX = minX + coord.x * cellSize;
            float cellStartZ = minZ + coord.y * cellSize;

            List<NPCSpawnData> bucket = cellBuckets[c];
            int countInCell = bucket.Count;
            placedInCell.Clear();

            for (int k = 0; k < countInCell; k++)
            {
                NPCSpawnData npc = bucket[k];
                Vector3 chosenPos = Vector3.zero;
                bool posFound = false;

                // Thử tìm vị trí ngẫu nhiên không dính đè lên các NPC trước đó trong ô
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    float rx = Random.Range(0.5f, Mathf.Max(0.6f, cellSize - 0.5f));
                    float rz = Random.Range(0.5f, Mathf.Max(0.6f, cellSize - 0.5f));
                    Vector3 testPos = new Vector3(cellStartX + rx, transform.position.y, cellStartZ + rz);

                    bool tooClose = false;
                    for (int p = 0; p < placedInCell.Count; p++)
                    {
                        if ((placedInCell[p] - testPos).sqrMagnitude < minDstSqr)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (!tooClose)
                    {
                        chosenPos = testPos;
                        posFound = true;
                        break;
                    }
                }

                if (!posFound)
                {
                    float rx = Random.Range(0.5f, Mathf.Max(0.6f, cellSize - 0.5f));
                    float rz = Random.Range(0.5f, Mathf.Max(0.6f, cellSize - 0.5f));
                    chosenPos = new Vector3(cellStartX + rx, transform.position.y, cellStartZ + rz);
                }

                placedInCell.Add(chosenPos);

                slots.Add(new SpawnSlot
                {
                    position = chosenPos,
                    rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                    team = npc.team,
                    prefab = npc.prefab,
                    parent = npc.parent
                });
            }
        }

        return slots;
    }

    private List<SpawnSlot> Calculate3ZoneChaoticSlots(float minX, float maxX, float minZ, float maxZ, float cellSize)
    {
        // 1. Chia chiều dài trục Z thành 3 phân vùng theo diện tích (Khu 1: 20%, Khu 2: 50%, Khu 3: 30%)
        float totalZ = maxZ - minZ;
        float r1 = Mathf.Clamp(zone1AreaRatio, 0.05f, 0.8f);
        float r2 = Mathf.Clamp(zone2AreaRatio, 0.05f, 0.9f - r1);
        float r3 = Mathf.Max(0.05f, 1.0f - r1 - r2);

        float z1End = minZ + totalZ * r1;
        float z2End = z1End + totalZ * r2;

        // 2. Chuẩn bị danh sách Prefab 2 phe
        GameObject prefabSwordA = teamASwordPrefab;
        GameObject prefabArcherA = (teamAArcherPrefab != null) ? teamAArcherPrefab : teamASwordPrefab;
        GameObject prefabSwordB = (teamBSwordPrefab != null) ? teamBSwordPrefab : prefabSwordA;
        GameObject prefabArcherB = (teamBArcherPrefab != null) ? teamBArcherPrefab : prefabArcherA;

        List<NPCSpawnData> listTeamA = new List<NPCSpawnData>(swordCountPerTeam + archerCountPerTeam);
        for (int i = 0; i < swordCountPerTeam; i++)
        {
            if (prefabSwordA != null) listTeamA.Add(new NPCSpawnData { team = Team.TeamA, prefab = prefabSwordA, parent = teamAParent });
        }
        for (int i = 0; i < archerCountPerTeam; i++)
        {
            if (prefabArcherA != null) listTeamA.Add(new NPCSpawnData { team = Team.TeamA, prefab = prefabArcherA, parent = teamAParent });
        }

        List<NPCSpawnData> listTeamB = new List<NPCSpawnData>(swordCountPerTeam + archerCountPerTeam);
        for (int i = 0; i < swordCountPerTeam; i++)
        {
            if (prefabSwordB != null) listTeamB.Add(new NPCSpawnData { team = Team.TeamB, prefab = prefabSwordB, parent = teamBParent });
        }
        for (int i = 0; i < archerCountPerTeam; i++)
        {
            if (prefabArcherB != null) listTeamB.Add(new NPCSpawnData { team = Team.TeamB, prefab = prefabArcherB, parent = teamBParent });
        }

        ShuffleList(listTeamA);
        ShuffleList(listTeamB);

        int totalNpcs = listTeamA.Count + listTeamB.Count;

        // Tính toán số lượng lính cho từng khu theo cấu hình trực tiếp (Khu 1: 1000, Khu 2: 6500, Khu 3: 2500)
        int countZone1 = zone1TroopCount;
        int countZone2 = zone2TroopCount;
        int countZone3 = zone3TroopCount;

        // Tự động chuẩn hóa nếu cấu hình lệch với tổng số lính thực tế của 2 phe
        int configuredSum = countZone1 + countZone2 + countZone3;
        if (configuredSum > 0 && configuredSum != totalNpcs)
        {
            float scale = (float)totalNpcs / configuredSum;
            countZone1 = Mathf.RoundToInt(countZone1 * scale);
            countZone2 = Mathf.RoundToInt(countZone2 * scale);
            countZone3 = totalNpcs - countZone1 - countZone2;
        }

        List<NPCSpawnData> zone1Npcs = new List<NPCSpawnData>(countZone1);
        List<NPCSpawnData> zone2Npcs = new List<NPCSpawnData>(countZone2);
        List<NPCSpawnData> zone3Npcs = new List<NPCSpawnData>(countZone3);

        int idxA = 0;
        int idxB = 0;

        if (realisticFrontlineTeams)
        {
            // Khu 1 (Đầu map): 80% Phe A (Hậu phương bảo hộ Player), 20% Phe B (Lính địch đột kích)
            int z1A = Mathf.Min(listTeamA.Count - idxA, Mathf.RoundToInt(countZone1 * 0.8f));
            int z1B = countZone1 - z1A;
            for (int i = 0; i < z1A && idxA < listTeamA.Count; i++) zone1Npcs.Add(listTeamA[idxA++]);
            for (int i = 0; i < z1B && idxB < listTeamB.Count; i++) zone1Npcs.Add(listTeamB[idxB++]);

            // Khu 3 (Cuối map): 20% Phe A (Lính xung kích), 80% Phe B (Đại bản doanh địch)
            int z3B = Mathf.Min(listTeamB.Count - idxB, Mathf.RoundToInt(countZone3 * 0.8f));
            int z3A = countZone3 - z3B;
            for (int i = 0; i < z3A && idxA < listTeamA.Count; i++) zone3Npcs.Add(listTeamA[idxA++]);
            for (int i = 0; i < z3B && idxB < listTeamB.Count; i++) zone3Npcs.Add(listTeamB[idxB++]);

            // Khu 2 (Giữa map): Toàn bộ số quân còn lại của 2 phe (Đảm bảo dùng hết 100% quân số)
            while (idxA < listTeamA.Count) zone2Npcs.Add(listTeamA[idxA++]);
            while (idxB < listTeamB.Count) zone2Npcs.Add(listTeamB[idxB++]);
        }
        else
        {
            // Phân bổ đều 50/50 theo quân số mỗi khu
            int z1Half = countZone1 / 2;
            for (int i = 0; i < z1Half && idxA < listTeamA.Count; i++) zone1Npcs.Add(listTeamA[idxA++]);
            while (zone1Npcs.Count < countZone1 && idxB < listTeamB.Count) zone1Npcs.Add(listTeamB[idxB++]);

            int z3Half = countZone3 / 2;
            for (int i = 0; i < z3Half && idxA < listTeamA.Count; i++) zone3Npcs.Add(listTeamA[idxA++]);
            while (zone3Npcs.Count < countZone3 && idxB < listTeamB.Count) zone3Npcs.Add(listTeamB[idxB++]);

            while (idxA < listTeamA.Count) zone2Npcs.Add(listTeamA[idxA++]);
            while (idxB < listTeamB.Count) zone2Npcs.Add(listTeamB[idxB++]);
        }

        ShuffleList(zone1Npcs);
        ShuffleList(zone2Npcs);
        ShuffleList(zone3Npcs);

        List<SpawnSlot> allSlots = new List<SpawnSlot>(totalNpcs);

        PopulateZoneSlots(allSlots, zone1Npcs, minX, maxX, minZ, z1End, cellSize);
        PopulateZoneSlots(allSlots, zone2Npcs, minX, maxX, z1End, z2End, cellSize);
        PopulateZoneSlots(allSlots, zone3Npcs, minX, maxX, z2End, maxZ, cellSize);

        Debug.Log($"<color=cyan>[MassArmySpawner]</color> Đã phân bổ 3 Khu Vực: Khu 1 ({zone1Npcs.Count} lính, 0-{Mathf.RoundToInt(r1*100)}%), Khu 2 ({zone2Npcs.Count} lính, {Mathf.RoundToInt(r1*100)}-{Mathf.RoundToInt((r1+r2)*100)}%), Khu 3 ({zone3Npcs.Count} lính, {Mathf.RoundToInt((r1+r2)*100)}-100%)");

        return allSlots;
    }

    private void PopulateZoneSlots(List<SpawnSlot> outSlots, List<NPCSpawnData> npcs, float minX, float maxX, float subMinZ, float subMaxZ, float cellSize)
    {
        if (npcs == null || npcs.Count == 0) return;

        float widthZ = subMaxZ - subMinZ;
        if (widthZ < cellSize) widthZ = cellSize;

        int cellsX = Mathf.Max(1, Mathf.FloorToInt((maxX - minX) / cellSize));
        int cellsZ = Mathf.Max(1, Mathf.FloorToInt(widthZ / cellSize));

        List<Vector2Int> cellIndices = new List<Vector2Int>(cellsX * cellsZ);
        for (int x = 0; x < cellsX; x++)
        {
            for (int z = 0; z < cellsZ; z++)
            {
                cellIndices.Add(new Vector2Int(x, z));
            }
        }
        ShuffleList(cellIndices);

        int totalCells = cellIndices.Count;
        List<List<NPCSpawnData>> buckets = new List<List<NPCSpawnData>>(totalCells);
        for (int i = 0; i < totalCells; i++) buckets.Add(new List<NPCSpawnData>());

        int limitPerCell = Mathf.Max(1, targetNpcsPerCell);
        int npcIdx = 0;
        int totalCount = npcs.Count;

        for (int c = 0; c < totalCells && npcIdx < totalCount; c++)
        {
            int take = Mathf.Min(limitPerCell, totalCount - npcIdx);
            for (int k = 0; k < take; k++) buckets[c].Add(npcs[npcIdx++]);
        }

        int roundRobin = 0;
        while (npcIdx < totalCount)
        {
            buckets[roundRobin].Add(npcs[npcIdx++]);
            roundRobin = (roundRobin + 1) % totalCells;
        }

        float minDstSqr = minSpawnDistance * minSpawnDistance;
        List<Vector3> placedInCell = new List<Vector3>();

        for (int c = 0; c < totalCells; c++)
        {
            Vector2Int coord = cellIndices[c];
            float cellStartX = minX + coord.x * cellSize;
            float cellStartZ = subMinZ + coord.y * cellSize;

            List<NPCSpawnData> bucket = buckets[c];
            int countInCell = bucket.Count;
            placedInCell.Clear();

            for (int k = 0; k < countInCell; k++)
            {
                NPCSpawnData npc = bucket[k];
                Vector3 chosenPos = Vector3.zero;
                bool posFound = false;

                for (int attempt = 0; attempt < 10; attempt++)
                {
                    float rx = Random.Range(0.5f, Mathf.Max(0.6f, cellSize - 0.5f));
                    float rz = Random.Range(0.5f, Mathf.Max(0.6f, cellSize - 0.5f));
                    Vector3 testPos = new Vector3(cellStartX + rx, transform.position.y, cellStartZ + rz);

                    bool tooClose = false;
                    for (int p = 0; p < placedInCell.Count; p++)
                    {
                        if ((placedInCell[p] - testPos).sqrMagnitude < minDstSqr)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (!tooClose)
                    {
                        chosenPos = testPos;
                        posFound = true;
                        break;
                    }
                }

                if (!posFound)
                {
                    float rx = Random.Range(0.5f, Mathf.Max(0.6f, cellSize - 0.5f));
                    float rz = Random.Range(0.5f, Mathf.Max(0.6f, cellSize - 0.5f));
                    chosenPos = new Vector3(cellStartX + rx, transform.position.y, cellStartZ + rz);
                }

                placedInCell.Add(chosenPos);

                outSlots.Add(new SpawnSlot
                {
                    position = chosenPos,
                    rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                    team = npc.team,
                    prefab = npc.prefab,
                    parent = npc.parent
                });
            }
        }
    }

    private static void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int rnd = Random.Range(0, i + 1);
            T tmp = list[i];
            list[i] = list[rnd];
            list[rnd] = tmp;
        }
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

        // Đảm bảo script chiến đấu và điều khiển được bật
        NPCCombat combat = npcObj.GetComponent<NPCCombat>();
        if (combat != null) combat.enabled = true;

        CharacterController cc = npcObj.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = true;

        // Đảm bảo NavMeshAgent dính sát mặt đất và không bị kẹt
        UnityEngine.AI.NavMeshAgent agent = npcObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = true;
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
        string modeLabel = spawner.chaoticBattleMode ? "HỖN CHIẾN NGẪU NHIÊN" : "ĐẠI CHIẾN DÀN TRẬN";
        if (GUILayout.Button($"▶ SPAWN {modeLabel} ({totalAll} NPC)", GUILayout.Height(38)))
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

