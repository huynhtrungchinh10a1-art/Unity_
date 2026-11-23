using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class GroundArrowManager : MonoBehaviour
{
    private static GroundArrowManager _instance;
    public static GroundArrowManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<GroundArrowManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("[GroundArrowManager]");
                    _instance = go.AddComponent<GroundArrowManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Rendering")]
    public Mesh arrowMesh;
    public Material arrowMaterial;
    public ShadowCastingMode castShadows = ShadowCastingMode.On;
    public bool receiveShadows = true;

    [Header("Fog / Distance Culling")]
    public bool enableDistanceCulling = true;
    public float maxRenderDistance = 45f;
    public Transform playerTransform;

    // Unity DrawMeshInstanced cho phép tối đa 1023 matrices trên mỗi lệnh vẽ
    private const int BATCH_SIZE = 1000;
    private List<List<Matrix4x4>> batches = new List<List<Matrix4x4>>();
    private List<Matrix4x4> currentBatch = new List<Matrix4x4>();

    // Bộ nhớ đệm phục vụ culling
    private List<Matrix4x4> visibleBuffer = new List<Matrix4x4>(BATCH_SIZE);
    private Matrix4x4[] drawArray = new Matrix4x4[BATCH_SIZE];

    public int TotalGroundedArrows { get; private set; } = 0;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }
    }

    public void RegisterArrow(Mesh mesh, Material mat, Matrix4x4 matrix)
    {
        if (arrowMesh == null && mesh != null) arrowMesh = mesh;
        if (arrowMaterial == null && mat != null) arrowMaterial = mat;

        // VỆ SINH CAO ĐỘ: Đảm bảo mũi tên không bao giờ lơ lửng trên không (> 0.35m so với mặt đất)
        if (Terrain.activeTerrain != null)
        {
            Vector3 pos = new Vector3(matrix.m03, matrix.m13, matrix.m23);
            float terrainY = Terrain.activeTerrain.SampleHeight(pos) + Terrain.activeTerrain.transform.position.y;
            if (pos.y - terrainY > 0.35f || pos.y < terrainY - 0.25f)
            {
                matrix.m13 = terrainY + 0.12f;
            }
        }

        if (currentBatch.Count >= BATCH_SIZE)
        {
            batches.Add(currentBatch);
            currentBatch = new List<Matrix4x4>(BATCH_SIZE);
        }

        currentBatch.Add(matrix);
        TotalGroundedArrows++;
    }

    void LateUpdate()
    {
        if (arrowMesh == null || arrowMaterial == null || TotalGroundedArrows == 0) return;

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

        // Vẽ các batch đã đầy
        for (int b = 0; b < batches.Count; b++)
        {
            RenderBatch(batches[b], centerPos, maxDistSq);
        }

        // Vẽ batch hiện tại đang nạp dở
        if (currentBatch.Count > 0)
        {
            RenderBatch(currentBatch, centerPos, maxDistSq);
        }
    }

    private void RenderBatch(List<Matrix4x4> batch, Vector3 camPos, float maxDistSq)
    {
        int count = batch.Count;
        if (count == 0) return;

        if (!enableDistanceCulling)
        {
            // Không culling: vẽ thẳng toàn bộ batch
            batch.CopyTo(drawArray, 0);
            Graphics.DrawMeshInstanced(
                arrowMesh, 0, arrowMaterial, drawArray, count,
                null, castShadows, receiveShadows
            );
            return;
        }

        // Có Distance Culling theo sương mù quanh Player:
        visibleBuffer.Clear();
        for (int i = 0; i < count; i++)
        {
            Matrix4x4 m = batch[i];
            // Lấy tọa độ X, Y, Z từ ma trận
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
            Graphics.DrawMeshInstanced(
                arrowMesh, 0, arrowMaterial, drawArray, visibleCount,
                null, castShadows, receiveShadows
            );
        }
    }

    public List<Matrix4x4> GetAllArrowMatrices()
    {
        List<Matrix4x4> list = new List<Matrix4x4>(TotalGroundedArrows);
        for (int b = 0; b < batches.Count; b++)
        {
            list.AddRange(batches[b]);
        }
        list.AddRange(currentBatch);
        return list;
    }

    public void LoadPreloadedArrows(Mesh mesh, Material mat, List<Matrix4x4> matrices)
    {
        if (mesh != null) arrowMesh = mesh;
        if (mat != null)
        {
            arrowMaterial = mat;
            if (!arrowMaterial.enableInstancing) arrowMaterial.enableInstancing = true;
        }

        if (matrices == null || matrices.Count == 0) return;

        for (int i = 0; i < matrices.Count; i++)
        {
            RegisterArrow(arrowMesh, arrowMaterial, matrices[i]);
        }
    }

    [ContextMenu("Xóa Toàn Bộ Tên Cắm Đất")]
    public void ClearAllGroundedArrows()
    {
        batches.Clear();
        currentBatch.Clear();
        TotalGroundedArrows = 0;
    }
}
