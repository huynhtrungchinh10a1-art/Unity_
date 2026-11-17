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
    public int checksPerFrame = 2500;

    private List<NPCCombat> npcs = new List<NPCCombat>(2048);
    private int currentIndex = 0;

    public int TotalRegisteredNPCs => npcs.Count;
    public int CulledCount { get; private set; }

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

    public void RegisterNPC(NPCCombat npc)
    {
        if (npc == null || npc.cullingIndex != -1) return;

        npc.cullingIndex = npcs.Count;
        npcs.Add(npc);
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

        if (playerTransform == null)
        {
            FindPlayer();
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
