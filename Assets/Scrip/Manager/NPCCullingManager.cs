using System.Collections.Generic;
using UnityEngine;

public class NPCCullingManager : MonoBehaviour
{
    private static NPCCullingManager _instance;
    public static NPCCullingManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<NPCCullingManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("[NPCCullingManager]");
                    _instance = go.AddComponent<NPCCullingManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Distance Settings")]
    public float cullDistance = 45f;
    public float hysteresis = 3f; // Đệm 3m chống nhấp nháy khi NPC đi lại quanh ranh giới 45m
    public Transform playerTransform;

    [Header("Performance")]
    public int checksPerFrame = 5000;

    [Header("Debug & Cinematic")]
    public bool disableCulling = false;

    private List<NPCCombat> npcs = new List<NPCCombat>(10240);
    private int currentIndex = 0;

    public int TotalRegisteredNPCs => npcs.Count;
    public int CulledCount { get; private set; }
    public List<NPCCombat> AllNPCs => npcs;

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

    private float retryFindPlayerTimer = 0f;

    void Start()
    {
        if (playerTransform == null) FindPlayer();
    }

    private void FindPlayer()
    {
        if (PlayerMovement.PlayerTransform != null)
        {
            playerTransform = PlayerMovement.PlayerTransform;
            return;
        }

        PlayerMovement pm = FindObjectOfType<PlayerMovement>();
        if (pm != null)
        {
            playerTransform = pm.transform;
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    public void RegisterNPC(NPCCombat npc)
    {
        if (npc == null || npc.cullingIndex != -1) return;

        npc.cullingIndex = npcs.Count;
        npcs.Add(npc);

        // Khởi tạo trạng thái ban đầu: nếu ở xa Player > cullDistance thì cho ngủ đông ngay lập tức khi vừa spawn!
        // (Nếu disableCulling = true thì không cho ngủ đông)
        if (!disableCulling && playerTransform != null)
        {
            Vector3 p = playerTransform.position;
            Vector3 n = npc.transform.position;
            float dx = n.x - p.x;
            float dz = n.z - p.z;
            if ((dx * dx + dz * dz) > (cullDistance * cullDistance))
            {
                npc.SetCulled(true);
            }
        }
    }

    public void UnregisterNPC(NPCCombat npc)
    {
        if (npc == null || npc.cullingIndex < 0 || npc.cullingIndex >= npcs.Count) return;

        int idx = npc.cullingIndex;
        int lastIdx = npcs.Count - 1;

        if (idx != lastIdx)
        {
            NPCCombat lastNPC = npcs[lastIdx];
            npcs[idx] = lastNPC;
            if (lastNPC != null)
            {
                lastNPC.cullingIndex = idx;
            }
        }

        npcs.RemoveAt(lastIdx);
        npc.cullingIndex = -1;

        if (currentIndex >= npcs.Count)
        {
            currentIndex = 0;
        }
    }

    void Update()
    {
        int count = npcs.Count;
        if (count == 0) return;

        // Nếu bật chế độ tắt culling (dùng để quay clip): Đảm bảo toàn bộ NPC được thức dậy hiển thị
        if (disableCulling)
        {
            int checksToWake = Mathf.Min(checksPerFrame, count);
            for (int i = 0; i < checksToWake; i++)
            {
                if (currentIndex >= npcs.Count) currentIndex = 0;
                NPCCombat npc = npcs[currentIndex];
                if (npc != null && npc.IsCulled)
                {
                    npc.SetCulled(false);
                }
                currentIndex++;
            }
            CulledCount = 0;
            return;
        }

        if (playerTransform == null)
        {
            retryFindPlayerTimer -= Time.deltaTime;
            if (retryFindPlayerTimer <= 0f)
            {
                retryFindPlayerTimer = 1.0f;
                FindPlayer();
            }
            if (playerTransform == null) return;
        }

        Vector3 playerPos = playerTransform.position;
        float nearDistSq = cullDistance * cullDistance;
        float farDistSq = (cullDistance + hysteresis) * (cullDistance + hysteresis);

        int checksToRun = Mathf.Min(checksPerFrame, count);
        int currentCulled = 0;

        for (int i = 0; i < checksToRun; i++)
        {
            if (currentIndex >= npcs.Count)
            {
                currentIndex = 0;
            }

            NPCCombat npc = npcs[currentIndex];
            if (npc != null && npc.MyHealth != null && npc.MyHealth.isAlive)
            {
                Vector3 npcPos = npc.transform.position;
                float dx = npcPos.x - playerPos.x;
                float dz = npcPos.z - playerPos.z;
                float distSq = dx * dx + dz * dz;

                if (npc.IsCulled)
                {
                    // Đang bị tắt -> chỉ bật lại khi lại gần hơn cullDistance
                    if (distSq <= nearDistSq)
                    {
                        npc.SetCulled(false);
                    }
                    else
                    {
                        currentCulled++;
                    }
                }
                else
                {
                    // Đang bật -> chỉ tắt khi đi xa hơn cullDistance + hysteresis
                    if (distSq > farDistSq)
                    {
                        npc.SetCulled(true);
                        currentCulled++;
                    }
                }
            }

            currentIndex++;
        }

        CulledCount = currentCulled;
    }
}
