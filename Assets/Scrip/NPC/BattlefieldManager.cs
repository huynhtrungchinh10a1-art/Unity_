using UnityEngine;
using System.Collections.Generic;

public class BattlefieldManager : MonoBehaviour
{
    public static BattlefieldManager Instance { get; private set; }

    [Header("Grid Settings")]
    public float cellSize = 10f;
    public int maxNpcsPerCell = 15;

    private Dictionary<Vector2Int, List<NPCCombat>> grid = new Dictionary<Vector2Int, List<NPCCombat>>();

    void Awake()
    {
        if (Instance == null) 
        {
            Instance = this;
        } 
        else 
        {
            Destroy(gameObject);
        }
    }

    // Tính toán tọa độ ô (Grid Cell) từ vị trí thực (World Position)
    public Vector2Int GetCell(Vector3 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / cellSize),
            Mathf.FloorToInt(position.z / cellSize)
        );
    }

    // Đăng ký lần đầu
    public void RegisterNPC(NPCCombat npc, Vector2Int cell)
    {
        if (!grid.ContainsKey(cell))
        {
            grid[cell] = new List<NPCCombat>();
        }
        grid[cell].Add(npc);
    }

    // Cập nhật khi đi sang ô khác
    public void UpdateNPCPosition(NPCCombat npc, Vector2Int oldCell, Vector2Int newCell)
    {
        if (oldCell == newCell) return;
        
        if (grid.ContainsKey(oldCell))
        {
            grid[oldCell].Remove(npc);
        }
        
        if (!grid.ContainsKey(newCell))
        {
            grid[newCell] = new List<NPCCombat>();
        }
        grid[newCell].Add(npc);
    }

    // Hủy đăng ký khi chết
    public void UnregisterNPC(NPCCombat npc, Vector2Int cell)
    {
        if (grid.ContainsKey(cell))
        {
            grid[cell].Remove(npc);
        }
    }

    // Kiểm tra xem ô đó có đang quá đông người không
    public bool IsZoneCrowded(Vector3 position)
    {
        Vector2Int cell = GetCell(position);
        if (grid.TryGetValue(cell, out List<NPCCombat> npcs))
        {
            return npcs.Count >= maxNpcsPerCell;
        }
        return false;
    }

    // Lấy danh sách những người trong ô
    public List<NPCCombat> GetNPCsInCell(Vector3 position)
    {
        Vector2Int cell = GetCell(position);
        if (grid.TryGetValue(cell, out List<NPCCombat> npcs))
        {
            return npcs;
        }
        return null;
    }

    void OnDrawGizmos()
    {
        // Vẽ lưới ô Grid (kích thước cellSize)
        Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.2f); // Màu xám nhạt, trong suốt

        float size = cellSize;
        if (size <= 0.1f) size = 10f; // Tránh chia cho 0

        // Giới hạn vẽ (từ -600 đến 600)
        float minX = -600f;
        float maxX = 600f;
        float minZ = -600f;
        float maxZ = 600f;

        int startX = Mathf.FloorToInt(minX / size);
        int endX = Mathf.CeilToInt(maxX / size);
        int startZ = Mathf.FloorToInt(minZ / size);
        int endZ = Mathf.CeilToInt(maxZ / size);

        // Vẽ các đường thẳng dọc
        for (int i = startX; i <= endX; i++)
        {
            float posX = i * size;
            Gizmos.DrawLine(new Vector3(posX, 0, minZ), new Vector3(posX, 0, maxZ));
        }

        // Vẽ các đường thẳng ngang
        for (int j = startZ; j <= endZ; j++)
        {
            float posZ = j * size;
            Gizmos.DrawLine(new Vector3(minX, 0, posZ), new Vector3(maxX, 0, posZ));
        }

        // Khi game đang chạy, tô màu các ô có NPC
        if (Application.isPlaying)
        {
            foreach (var pair in grid)
            {
                Vector2Int cell = pair.Key;
                List<NPCCombat> npcs = pair.Value;

                // Lọc lấy các NPC thực sự đang hoạt động và còn sống
                List<NPCCombat> activeNpcs = new List<NPCCombat>();
                if (npcs != null)
                {
                    foreach (var npc in npcs)
                    {
                        if (npc != null && npc.enabled)
                        {
                            activeNpcs.Add(npc);
                        }
                    }
                }

                int npcCount = activeNpcs.Count;
                if (npcCount > 0)
                {
                    // Lấy Y trung bình của các NPC trong ô để bám theo địa hình gồ ghề
                    float avgY = 0f;
                    foreach (var npc in activeNpcs)
                    {
                        avgY += npc.transform.position.y;
                    }
                    avgY /= npcCount;

                    // Vị trí trung tâm ô Grid
                    Vector3 cellCenter = new Vector3(
                        (cell.x + 0.5f) * size,
                        avgY,
                        (cell.y + 0.5f) * size
                    );

                    // Tạo hộp 3D có chiều cao 1.5m để hiển thị rõ trên màn hình Scene
                    Vector3 boxSize = new Vector3(size - 0.1f, 1.5f, size - 0.1f);
                    Vector3 boxCenter = new Vector3(cellCenter.x, cellCenter.y + 0.75f, cellCenter.z);

                    // Màu sắc tùy thuộc vào mật độ NPC
                    if (npcCount >= maxNpcsPerCell)
                    {
                        Gizmos.color = new Color(1f, 0f, 0f, 0.25f); // Đỏ mờ cảnh báo đông
                    }
                    else
                    {
                        Gizmos.color = new Color(0f, 1f, 0f, 0.1f); // Xanh lá mờ bình thường
                    }

                    // Vẽ khối nền
                    Gizmos.DrawCube(boxCenter, boxSize);

                    // Vẽ viền hộp sắc nét
                    Gizmos.color = npcCount >= maxNpcsPerCell ? Color.red : Color.green;
                    Gizmos.DrawWireCube(boxCenter, boxSize);

#if UNITY_EDITOR
                    // Vẽ nhãn chữ ghi rõ thông tin ô và tình trạng
                    string labelText = $"Cell: ({cell.x}, {cell.y})\nNPCs: {npcCount}/{maxNpcsPerCell}";
                    GUIStyle style = new GUIStyle();
                    style.normal.textColor = npcCount >= maxNpcsPerCell ? Color.red : Color.green;
                    style.fontSize = 11;
                    style.fontStyle = FontStyle.Bold;
                    style.alignment = TextAnchor.MiddleCenter;

                    // Vẽ nhãn chữ ngay phía trên hộp 3D một chút
                    UnityEditor.Handles.Label(boxCenter + Vector3.up * 1.0f, labelText, style);
#endif
                }
            }
        }
    }
}
