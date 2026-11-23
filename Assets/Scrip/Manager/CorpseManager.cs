using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CorpseManager : MonoBehaviour
{
    private static CorpseManager _instance;
    public static CorpseManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<CorpseManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("[CorpseManager]");
                    _instance = go.AddComponent<CorpseManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Rendering & Distance Culling")]
    public float maxRenderDistance = 45f;
    public Transform playerTransform;
    public ShadowCastingMode castShadows = ShadowCastingMode.On;
    public bool receiveShadows = true;

    [Header("Preloaded Battlefield (Bãi Chiến Trường Lưu Sẵn)")]
    public BattlefieldSaveData preloadedBattlefield;
    public bool autoLoadSavedBattlefield = true;

    [Header("Debug Stats (Theo dõi xác chết)")]
    [SerializeField] private int totalGroundedCorpses = 0;
    [SerializeField] private int uniqueCorpseTypesCount = 0;

    private const int BATCH_SIZE = 1000;

    public class CorpseGroup
    {
        public Mesh mesh;
        public Material[] materials;
        public List<List<Matrix4x4>> batches = new List<List<Matrix4x4>>();
        public List<Matrix4x4> currentBatch = new List<Matrix4x4>(BATCH_SIZE);
        public int totalCount = 0;
    }

    private Dictionary<string, CorpseGroup> corpseGroups = new Dictionary<string, CorpseGroup>();
    private List<Matrix4x4> visibleBuffer = new List<Matrix4x4>(BATCH_SIZE);
    private Matrix4x4[] drawArray = new Matrix4x4[BATCH_SIZE];

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        FindPlayer();
    }

    void Start()
    {
        if (playerTransform == null) FindPlayer();

        if (autoLoadSavedBattlefield)
        {
            if (preloadedBattlefield == null)
            {
#if UNITY_EDITOR
                preloadedBattlefield = UnityEditor.AssetDatabase.LoadAssetAtPath<BattlefieldSaveData>("Assets/SavedBattlefield/BattlefieldData.asset");
#endif
            }

            if (preloadedBattlefield != null)
            {
                LoadPreloadedBattlefield(preloadedBattlefield);
            }
        }
    }

    private void FindPlayer()
    {
        if (PlayerMovement.PlayerTransform != null)
        {
            playerTransform = PlayerMovement.PlayerTransform;
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    public void ScheduleCorpseBake(GameObject npcObj, int deadIndex, GameObject[] equippedWeapons = null, float delay = 3f)
    {
        if (npcObj == null) return;

        SkinnedMeshRenderer smr = npcObj.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (smr != null) smr.updateWhenOffscreen = true;

        Animator anim = npcObj.GetComponentInChildren<Animator>(true);
        if (anim != null)
        {
            anim.enabled = true;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        StartCoroutine(RoutineScheduleBake(npcObj, deadIndex, equippedWeapons, delay));
    }

    private IEnumerator RoutineScheduleBake(GameObject npcObj, int deadIndex, GameObject[] equippedWeapons, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (npcObj != null)
        {
            RegisterCorpse(npcObj, deadIndex, equippedWeapons);
        }
    }

    public void RegisterCorpse(GameObject deadNpc, int deadIndex, GameObject[] equippedWeapons = null)
    {
        if (deadNpc == null) return;

        // 1. TÁCH VŨ KHÍ RƠI XUỐNG ĐẤT THÀNH DROPPEDWEAPON ĐỘC LẬP (ĐỂ NHẶT ĐƯỢC VÀ CULLING Ở XA)
        if (equippedWeapons != null && equippedWeapons.Length > 0)
        {
            foreach (var weapon in equippedWeapons)
            {
                if (weapon == null) continue;

                DroppedWeapon dw = weapon.GetComponent<DroppedWeapon>();
                if (dw == null)
                {
                    dw = weapon.AddComponent<DroppedWeapon>();
                }
                dw.DetachFromHand();

                if (DroppedWeaponManager.Instance != null)
                {
                    weapon.transform.SetParent(DroppedWeaponManager.Instance.WeaponContainer);
                }
            }
        }

        // 1.5 CHUYỂN GIAO TOÀN BỘ MŨI TÊN CẮM TRÊN THÂN XÁC SANG GROUNDARROWMANAGER (GPU INSTANCING)
        ArrowProjectile[] stuckArrows = deadNpc.GetComponentsInChildren<ArrowProjectile>(true);
        if (stuckArrows != null && stuckArrows.Length > 0 && GroundArrowManager.Instance != null)
        {
            foreach (var arrow in stuckArrows)
            {
                if (arrow == null) continue;
                MeshFilter mf = arrow.GetComponentInChildren<MeshFilter>(true);
                MeshRenderer mr = arrow.GetComponentInChildren<MeshRenderer>(true);
                if (mf != null && mr != null && mf.sharedMesh != null && mr.sharedMaterial != null)
                {
                    Transform visualT = mf.transform;
                    Vector3 arrowPos = visualT.position;
                    // Kiểm tra và hạ độ cao nếu mũi tên bị lơ lửng trên không do cắm vào capsule cũ
                    float groundY = arrowPos.y;
                    if (Terrain.activeTerrain != null)
                    {
                        groundY = Terrain.activeTerrain.SampleHeight(arrowPos) + Terrain.activeTerrain.transform.position.y;
                    }
                    else
                    {
                        RaycastHit gh;
                        if (Physics.Raycast(arrowPos + Vector3.up * 1f, Vector3.down, out gh, 4f, LayerMask.GetMask("Default", "Terrain")))
                        {
                            groundY = gh.point.y;
                        }
                    }

                    if (arrowPos.y - groundY > 0.4f)
                    {
                        arrowPos.y = groundY + Random.Range(0.04f, 0.15f);
                        visualT.position = arrowPos;
                    }

                    GroundArrowManager.Instance.RegisterArrow(mf.sharedMesh, mr.sharedMaterial, visualT.localToWorldMatrix);
                }
                arrow.ReturnToPool();
            }
        }

        // 2. NẠP PHẦN CƠ THỂ XÁC CHẾT VÀO CORPSEMANAGER (BakeMesh)
        SkinnedMeshRenderer smr = deadNpc.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (smr == null || smr.sharedMesh == null)
        {
            Destroy(deadNpc);
            return;
        }

        // Chuẩn hóa tên và phân nhóm theo Phe (Team) + Material để Team A (Xanh lá) và Team B (Xanh lam) KHÔNG BAO GIỜ bị nhầm lẫn vật liệu!
        HealthAndTeam ht = deadNpc.GetComponent<HealthAndTeam>();
        string teamStr = (ht != null) ? ht.teamCurrent.ToString() : "NoTeam";
        string matKey = (smr.sharedMaterial != null) ? smr.sharedMaterial.name : "DefaultMat";
        string cleanName = System.Text.RegularExpressions.Regex.Replace(deadNpc.name, @"(\s*\(\d+\)|\(Clone\))", "").Trim();
        string groupKey = $"{cleanName}_{teamStr}_{matKey}_Dead_{deadIndex}";

        if (!corpseGroups.TryGetValue(groupKey, out CorpseGroup group))
        {
            // BẢO VỆ CHỐNG CHẾT ĐỨNG & NGỒI XỔM: Ép Animator và Bones về tư thế nằm phẳng nếu chưa ngã hoàn tất
            EnsureFallenPose(deadNpc, smr, deadIndex);

            group = new CorpseGroup();

            // Chụp lại đúng tư thế nằm chết bằng BakeMesh (chỉ chụp 1 lần đầu tiên cho mỗi thế chết!)
            Mesh baked = new Mesh();
            smr.BakeMesh(baked);
            group.mesh = baked;

            // Nạp Material và kích hoạt GPU Instancing
            Material[] mats = smr.sharedMaterials;
            group.materials = mats;
            if (mats != null)
            {
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && !mats[i].enableInstancing)
                    {
                        mats[i].enableInstancing = true;
                    }
                }
            }

            corpseGroups[groupKey] = group;
            uniqueCorpseTypesCount = corpseGroups.Count;
        }

        // Lưu ma trận vị trí và hướng xoay của xác trên mặt đất (sử dụng smr matrix để khớp 100% với BakedMesh)
        Matrix4x4 matrix = (smr != null) ? smr.transform.localToWorldMatrix : deadNpc.transform.localToWorldMatrix;

        if (group.currentBatch.Count >= BATCH_SIZE)
        {
            group.batches.Add(group.currentBatch);
            group.currentBatch = new List<Matrix4x4>(BATCH_SIZE);
        }

        group.currentBatch.Add(matrix);
        group.totalCount++;
        totalGroundedCorpses++;

        // 3. HỦY HOÀN TOÀN GameObject NPC GỐC (xóa sạch 50-60 xương con, AI, Colliders, NavMeshAgent)
        Destroy(deadNpc);
    }

    private void EnsureFallenPose(GameObject deadNpc, SkinnedMeshRenderer smr, int deadIndex)
    {
        if (deadNpc == null || smr == null) return;

        smr.updateWhenOffscreen = true;

        Animator anim = deadNpc.GetComponentInChildren<Animator>(true);
        if (anim == null) return;

        anim.enabled = true;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        Transform hips = anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Hips) : null;
        Transform chest = anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Chest) : null;
        Transform head = anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;

        if (hips == null)
        {
            Transform[] allTransforms = deadNpc.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                string tName = t.name.ToLower();
                if (tName.Contains("hips") || tName.Contains("pelvis"))
                {
                    hips = t;
                    break;
                }
            }
        }

        float rootY = deadNpc.transform.position.y;
        float hipsY = (hips != null) ? (hips.position.y - rootY) : 0f;
        float chestY = (chest != null) ? (chest.position.y - rootY) : 0f;
        float headY = (head != null) ? (head.position.y - rootY) : 0f;

        // Nếu Hips > 0.35m hoặc Chest > 0.35m hoặc Head > 0.40m thì chắc chắn chưa nằm phẳng xuống đất (vẫn đang đứng hoặc ngồi xổm):
        // Force Animator nhảy đến frame cuối cùng của animation chết!
        if (hipsY > 0.35f || chestY > 0.35f || headY > 0.40f)
        {
            string primaryState = GetDeathStateName(deadIndex);
            string[] fallbackStates = new string[] {
                primaryState,
                "Flying Back Death",
                "Sword And Shield Death",
                "Sword And Shield Death2"
            };

            foreach (var st in fallbackStates)
            {
                anim.Play(st, 0, 1.0f);
                anim.Update(0f);
                float curHipsY = (hips != null) ? (hips.position.y - rootY) : 0f;
                float curChestY = (chest != null) ? (chest.position.y - rootY) : 0f;
                if (curHipsY <= 0.35f && curChestY <= 0.35f)
                {
                    break;
                }
            }
        }
    }

    private string GetDeathStateName(int deadIndex)
    {
        switch (deadIndex)
        {
            case 0: return "Flying Back Death";
            case 1: return "Sword And Shield Death";
            case 2: return "Sword And Shield Death2";
            default: return "Flying Back Death";
        }
    }

    void LateUpdate()
    {
        if (corpseGroups.Count == 0 || totalGroundedCorpses == 0) return;

        Vector3 centerPos;
        if (playerTransform != null)
        {
            centerPos = playerTransform.position;
        }
        else if (Camera.main != null)
        {
            centerPos = Camera.main.transform.position;
        }
        else
        {
            centerPos = transform.position;
        }

        float maxDistSq = maxRenderDistance * maxRenderDistance;

        foreach (var kvp in corpseGroups)
        {
            CorpseGroup group = kvp.Value;
            if (group.mesh == null || group.materials == null || group.materials.Length == 0) continue;

            for (int b = 0; b < group.batches.Count; b++)
            {
                RenderBatch(group, group.batches[b], centerPos, maxDistSq);
            }

            if (group.currentBatch.Count > 0)
            {
                RenderBatch(group, group.currentBatch, centerPos, maxDistSq);
            }
        }
    }

    private void RenderBatch(CorpseGroup group, List<Matrix4x4> batch, Vector3 camPos, float maxDistSq)
    {
        int count = batch.Count;
        if (count == 0) return;

        visibleBuffer.Clear();
        for (int i = 0; i < count; i++)
        {
            Matrix4x4 m = batch[i];
            float dx = m.m03 - camPos.x;
            float dz = m.m23 - camPos.z;
            float distSq = dx * dx + dz * dz;

            if (distSq <= maxDistSq)
            {
                visibleBuffer.Add(m);
            }
        }

        int visibleCount = visibleBuffer.Count;
        if (visibleCount > 0)
        {
            visibleBuffer.CopyTo(drawArray, 0);

            int subCount = group.mesh.subMeshCount;
            for (int sub = 0; sub < subCount; sub++)
            {
                Material mat = (sub < group.materials.Length && group.materials[sub] != null) 
                    ? group.materials[sub] 
                    : group.materials[0];

                if (mat != null)
                {
                    Graphics.DrawMeshInstanced(
                        group.mesh, sub, mat, drawArray, visibleCount,
                        null, castShadows, receiveShadows
                    );
                }
            }
        }
    }

    public void LoadPreloadedBattlefield(BattlefieldSaveData data)
    {
        if (data == null) return;

        // 1. NẠP XÁC LÍNH
        if (data.corpseGroups != null && data.corpseGroups.Count > 0)
        {
            foreach (var sg in data.corpseGroups)
            {
                if (sg == null || sg.mesh == null) continue;

                if (!corpseGroups.TryGetValue(sg.groupKey, out CorpseGroup group))
                {
                    group = new CorpseGroup();
                    group.mesh = sg.mesh;
                    group.materials = sg.materials;

                    if (group.materials != null)
                    {
                        for (int i = 0; i < group.materials.Length; i++)
                        {
                            if (group.materials[i] != null && !group.materials[i].enableInstancing)
                            {
                                group.materials[i].enableInstancing = true;
                            }
                        }
                    }

                    corpseGroups[sg.groupKey] = group;
                }

                if (sg.matrices != null && sg.matrices.Count > 0)
                {
                    for (int i = 0; i < sg.matrices.Count; i++)
                    {
                        if (group.currentBatch.Count >= BATCH_SIZE)
                        {
                            group.batches.Add(group.currentBatch);
                            group.currentBatch = new List<Matrix4x4>(BATCH_SIZE);
                        }
                        group.currentBatch.Add(sg.matrices[i]);
                        group.totalCount++;
                        totalGroundedCorpses++;
                    }
                }
            }
            uniqueCorpseTypesCount = corpseGroups.Count;
        }

        // 2. NẠP MŨI TÊN CẮM ĐẤT
        if (data.arrowData != null && data.arrowData.arrowMatrices != null && data.arrowData.arrowMatrices.Count > 0)
        {
            if (GroundArrowManager.Instance != null)
            {
                GroundArrowManager.Instance.LoadPreloadedArrows(
                    data.arrowData.arrowMesh,
                    data.arrowData.arrowMaterial,
                    data.arrowData.arrowMatrices
                );
            }
        }

        // 3. NẠP VŨ KHÍ RƠI (KIẾM, KHIÊN, CUNG) BẰNG GPU INSTANCING
        if (data.weaponGroups != null && data.weaponGroups.Count > 0)
        {
            foreach (var wg in data.weaponGroups)
            {
                if (wg == null || wg.mesh == null || wg.materials == null || wg.matrices == null) continue;

                string groupKey = "Weapon_" + wg.groupKey;
                if (!corpseGroups.TryGetValue(groupKey, out CorpseGroup group))
                {
                    group = new CorpseGroup();
                    group.mesh = wg.mesh;
                    group.materials = wg.materials;

                    if (group.materials != null)
                    {
                        for (int i = 0; i < group.materials.Length; i++)
                        {
                            if (group.materials[i] != null && !group.materials[i].enableInstancing)
                            {
                                group.materials[i].enableInstancing = true;
                            }
                        }
                    }

                    corpseGroups[groupKey] = group;
                }

                for (int i = 0; i < wg.matrices.Count; i++)
                {
                    if (group.currentBatch.Count >= BATCH_SIZE)
                    {
                        group.batches.Add(group.currentBatch);
                        group.currentBatch = new List<Matrix4x4>(BATCH_SIZE);
                    }
                    group.currentBatch.Add(wg.matrices[i]);
                    group.totalCount++;
                    totalGroundedCorpses++;
                }
            }
            uniqueCorpseTypesCount = corpseGroups.Count;
        }

    }

#if UNITY_EDITOR
    [ContextMenu("💾 Lưu Bãi Chiến Trường Hiện Tại (Play Mode)")]
    public void SaveCurrentBattlefieldToAsset()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        // 0. Thu gom ngay lập tức những NPC đã chết nhưng còn đang chờ timer bake
        NPCCombat[] allCombat = FindObjectsOfType<NPCCombat>(true);
        if (allCombat != null)
        {
            foreach (var npc in allCombat)
            {
                if (npc != null && npc.MyHealth != null && !npc.MyHealth.isAlive)
                {
                    RegisterCorpse(npc.gameObject, 0, npc.equippedWeapons);
                }
            }
        }

        // 1. Dọn và thu gom tất cả mũi tên cắm đất còn sót đang wobble vào GroundArrowManager
        ArrowProjectile[] activeArrows = FindObjectsOfType<ArrowProjectile>();
        if (activeArrows != null && GroundArrowManager.Instance != null)
        {
            foreach (var arrow in activeArrows)
            {
                if (arrow != null && arrow.transform.parent == null && !arrow.enabled)
                {
                    MeshFilter mf = arrow.GetComponentInChildren<MeshFilter>();
                    MeshRenderer mr = arrow.GetComponentInChildren<MeshRenderer>();
                    if (mf != null && mr != null && mf.sharedMesh != null && mr.sharedMaterial != null)
                    {
                        GroundArrowManager.Instance.RegisterArrow(mf.sharedMesh, mr.sharedMaterial, mf.transform.localToWorldMatrix);
                        arrow.ReturnToPool();
                    }
                }
            }
        }

        string folderPath = "Assets/SavedBattlefield";
        if (!System.IO.Directory.Exists(folderPath))
        {
            System.IO.Directory.CreateDirectory(folderPath);
            UnityEditor.AssetDatabase.Refresh();
        }

        string assetPath = "Assets/SavedBattlefield/BattlefieldData.asset";

        // 0. Sao lưu toàn bộ Mesh độc lập vào bộ nhớ đệm trước
        // (để tránh trường hợp Asset cũ bị xóa làm mất reference của group.mesh)
        Dictionary<string, Mesh> meshClones = new Dictionary<string, Mesh>();
        foreach (var kvp in corpseGroups)
        {
            if (kvp.Value != null && kvp.Value.mesh != null)
            {
                Mesh copy = Instantiate(kvp.Value.mesh);
                copy.name = "BakedMesh_" + kvp.Key;
                meshClones[kvp.Key] = copy;
            }
        }

        // Xóa asset cũ nếu có để làm sạch sub-assets
        if (UnityEditor.AssetDatabase.LoadAssetAtPath<BattlefieldSaveData>(assetPath) != null)
        {
            UnityEditor.AssetDatabase.DeleteAsset(assetPath);
        }

        BattlefieldSaveData data = ScriptableObject.CreateInstance<BattlefieldSaveData>();
        data.saveTimestamp = System.DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

        UnityEditor.AssetDatabase.CreateAsset(data, assetPath);

        // Lưu từng nhóm xác lính
        foreach (var kvp in corpseGroups)
        {
            var group = kvp.Value;
            if (!meshClones.TryGetValue(kvp.Key, out Mesh meshCopy) || meshCopy == null) continue;

            // Cập nhật lại mesh cho group bằng bản copy sạch
            group.mesh = meshCopy;
            UnityEditor.AssetDatabase.AddObjectToAsset(meshCopy, data);

            BattlefieldSaveData.SavedCorpseGroup sg = new BattlefieldSaveData.SavedCorpseGroup();
            sg.groupKey = kvp.Key;
            sg.mesh = meshCopy;
            sg.materials = group.materials;

            for (int b = 0; b < group.batches.Count; b++)
            {
                sg.matrices.AddRange(group.batches[b]);
            }
            sg.matrices.AddRange(group.currentBatch);

            data.corpseGroups.Add(sg);
            data.totalCorpsesCount += sg.matrices.Count;
        }

        // Lưu mũi tên từ GroundArrowManager
        if (GroundArrowManager.Instance != null)
        {
            data.arrowData.arrowMesh = GroundArrowManager.Instance.arrowMesh;
            data.arrowData.arrowMaterial = GroundArrowManager.Instance.arrowMaterial;
            data.arrowData.arrowMatrices = GroundArrowManager.Instance.GetAllArrowMatrices();
            data.totalArrowsCount = data.arrowData.arrowMatrices.Count;
        }

        // Lưu vũ khí rơi (Kiếm, Khiên, Cung) từ DroppedWeaponManager
        Dictionary<string, BattlefieldSaveData.SavedWeaponGroup> weaponDict = new Dictionary<string, BattlefieldSaveData.SavedWeaponGroup>();
        List<GameObject> allWeapons = new List<GameObject>();

        if (DroppedWeaponManager.Instance != null && DroppedWeaponManager.Instance.WeaponContainer != null)
        {
            Transform container = DroppedWeaponManager.Instance.WeaponContainer;
            for (int i = 0; i < container.childCount; i++)
            {
                allWeapons.Add(container.GetChild(i).gameObject);
            }
        }

        DroppedWeapon[] dws = FindObjectsOfType<DroppedWeapon>(true);
        if (dws != null)
        {
            foreach (var dw in dws)
            {
                if (dw != null && !allWeapons.Contains(dw.gameObject))
                {
                    allWeapons.Add(dw.gameObject);
                }
            }
        }

        foreach (var wObj in allWeapons)
        {
            if (wObj == null) continue;

            // 1. Quét MeshRenderer (kiếm, khiên, phụ kiện)
            MeshRenderer[] mrs = wObj.GetComponentsInChildren<MeshRenderer>(true);
            if (mrs != null)
            {
                foreach (var mr in mrs)
                {
                    if (mr == null) continue;
                    MeshFilter mf = mr.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null || mr.sharedMaterials == null || mr.sharedMaterials.Length == 0) continue;

                    string key = mf.sharedMesh.name;
                    if (!weaponDict.TryGetValue(key, out BattlefieldSaveData.SavedWeaponGroup wg))
                    {
                        wg = new BattlefieldSaveData.SavedWeaponGroup();
                        wg.groupKey = key;
                        wg.mesh = mf.sharedMesh;
                        wg.materials = mr.sharedMaterials;
                        weaponDict[key] = wg;
                    }
                    wg.matrices.Add(mr.transform.localToWorldMatrix);
                }
            }

            // 2. Quét SkinnedMeshRenderer (cung tên có xương)
            SkinnedMeshRenderer[] smrs = wObj.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (smrs != null)
            {
                foreach (var smr in smrs)
                {
                    if (smr == null || smr.sharedMesh == null || smr.sharedMaterials == null || smr.sharedMaterials.Length == 0) continue;

                    string key = smr.sharedMesh.name;
                    if (!weaponDict.TryGetValue(key, out BattlefieldSaveData.SavedWeaponGroup wg))
                    {
                        wg = new BattlefieldSaveData.SavedWeaponGroup();
                        wg.groupKey = key;
                        wg.mesh = smr.sharedMesh;
                        wg.materials = smr.sharedMaterials;
                        weaponDict[key] = wg;
                    }
                    wg.matrices.Add(smr.transform.localToWorldMatrix);
                }
            }
        }

        foreach (var kvp in weaponDict)
        {
            data.weaponGroups.Add(kvp.Value);
            data.totalWeaponsCount += kvp.Value.matrices.Count;
        }

        UnityEditor.EditorUtility.SetDirty(data);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();

        preloadedBattlefield = data;
        Debug.Log($"<color=green>[CorpseManager]</color> <b>LƯU THÀNH CÔNG!</b> Đã lưu {data.totalCorpsesCount} xác lính, {data.totalArrowsCount} mũi tên và {data.totalWeaponsCount} vũ khí vào: {assetPath}");
    }

    [ContextMenu("🗑️ Xóa Dữ Liệu Bãi Chiến Trường Đã Lưu")]
    public void ClearSavedBattlefieldAsset()
    {
        string assetPath = "Assets/SavedBattlefield/BattlefieldData.asset";
        if (UnityEditor.AssetDatabase.LoadAssetAtPath<BattlefieldSaveData>(assetPath) != null)
        {
            UnityEditor.AssetDatabase.DeleteAsset(assetPath);
            UnityEditor.AssetDatabase.Refresh();
        }

        preloadedBattlefield = null;

        if (Application.isPlaying)
        {
            corpseGroups.Clear();
            totalGroundedCorpses = 0;
            uniqueCorpseTypesCount = 0;
            if (GroundArrowManager.Instance != null)
            {
                GroundArrowManager.Instance.ClearAllGroundedArrows();
            }
        }

        Debug.Log("<color=yellow>[CorpseManager]</color> Đã xóa sạch dữ liệu bãi chiến trường đã lưu!");
    }
#endif
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(CorpseManager))]
public class CorpseManagerEditor : UnityEditor.Editor
{
    [UnityEditor.MenuItem("Tools/⚔️ Lưu Bãi Chiến Trường (Play Mode)", false, 100)]
    public static void MenuSaveBattlefield()
    {
        if (CorpseManager.Instance != null)
        {
            CorpseManager.Instance.SaveCurrentBattlefieldToAsset();
        }
        else
        {
            UnityEditor.EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy CorpseManager trong Scene!", "OK");
        }
    }

    [UnityEditor.MenuItem("Tools/🗑️ Xóa Bãi Chiến Trường Đã Lưu", false, 101)]
    public static void MenuClearBattlefield()
    {
        if (CorpseManager.Instance != null)
        {
            CorpseManager.Instance.ClearSavedBattlefieldAsset();
        }
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CorpseManager cm = (CorpseManager)target;

        GUILayout.Space(14);
        GUILayout.Label("⚔️ CÔNG CỤ LƯU / NẠP CHIẾN TRƯỜNG (BATTLEFIELD BAKER)", UnityEditor.EditorStyles.boldLabel);

        // Nút Lưu Bãi Chiến Trường (Chỉ sáng khi Play Mode)
        GUI.backgroundColor = new Color(0.2f, 0.85f, 0.35f);
        if (GUILayout.Button("💾 LƯU BÃI CHIẾN TRƯỜNG HIỆN TẠI (PLAY MODE)", GUILayout.Height(38)))
        {
            if (Application.isPlaying)
            {
                cm.SaveCurrentBattlefieldToAsset();
            }
            else
            {
                UnityEditor.EditorUtility.DisplayDialog(
                    "Thông Báo",
                    "Bạn cần bấm PLAY game, cho lính đánh nhau / bắn mưa tên tan hoang trước rồi mới bấm Lưu nhé!",
                    "Đã Hiểu"
                );
            }
        }

        GUILayout.Space(6);

        // Nút Xóa Dữ Liệu
        GUI.backgroundColor = new Color(0.95f, 0.3f, 0.25f);
        if (GUILayout.Button("🗑️ XÓA DỮ LIỆU ĐÃ LƯU (RESET MAP TRẮNG)", GUILayout.Height(32)))
        {
            if (UnityEditor.EditorUtility.DisplayDialog(
                "Xác Nhận Xóa",
                "Bạn có chắc muốn xóa vĩnh viễn bãi chiến trường đã lưu để trở về map sạch không?",
                "Xóa Ngay",
                "Hủy"
            ))
            {
                cm.ClearSavedBattlefieldAsset();
            }
        }

        GUI.backgroundColor = Color.white;
    }
}
#endif
