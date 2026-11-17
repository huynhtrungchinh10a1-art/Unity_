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
    }

    private void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else if (Camera.main != null)
        {
            playerTransform = Camera.main.transform;
        }
    }

    public void ScheduleCorpseBake(GameObject npcObj, int deadIndex, GameObject[] equippedWeapons = null, float delay = 3f)
    {
        if (npcObj == null) return;
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
            }
        }

        // 2. NẠP PHẦN CƠ THỂ XÁC CHẾT VÀO CORPSEMANAGER (BakeMesh)
        SkinnedMeshRenderer smr = deadNpc.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr == null || smr.sharedMesh == null)
        {
            Destroy(deadNpc);
            return;
        }

        // Tách lấy tên gốc của Prefab nhân vật (bỏ bớt các hậu tố " (Clone)", " (1)", ...)
        string cleanName = deadNpc.name.Replace("(Clone)", "").Trim();
        int parenIdx = cleanName.IndexOf('(');
        if (parenIdx > 0) cleanName = cleanName.Substring(0, parenIdx).Trim();

        string groupKey = $"{cleanName}_Dead_{deadIndex}";

        if (!corpseGroups.TryGetValue(groupKey, out CorpseGroup group))
        {
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

        // Lưu ma trận vị trí và hướng xoay của xác trên mặt đất
        Matrix4x4 matrix = deadNpc.transform.localToWorldMatrix;

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
}
