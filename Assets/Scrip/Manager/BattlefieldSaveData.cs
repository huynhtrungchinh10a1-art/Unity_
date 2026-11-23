using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BattlefieldData", menuName = "Custom/Battlefield Data")]
public class BattlefieldSaveData : ScriptableObject
{
    [System.Serializable]
    public class SavedCorpseGroup
    {
        public string groupKey;
        public Mesh mesh;
        public Material[] materials;
        public List<Matrix4x4> matrices = new List<Matrix4x4>();
    }

    [System.Serializable]
    public class SavedArrowGroup
    {
        public Mesh arrowMesh;
        public Material arrowMaterial;
        public List<Matrix4x4> arrowMatrices = new List<Matrix4x4>();
    }

    [System.Serializable]
    public class SavedWeaponGroup
    {
        public string groupKey;
        public Mesh mesh;
        public Material[] materials;
        public List<Matrix4x4> matrices = new List<Matrix4x4>();
    }

    [Header("1. Thông Tin Chung")]
    public string saveTimestamp;
    public int totalCorpsesCount = 0;
    public int totalArrowsCount = 0;
    public int totalWeaponsCount = 0;

    [Header("2. Danh Sách Xác Lính")]
    public List<SavedCorpseGroup> corpseGroups = new List<SavedCorpseGroup>();

    [Header("3. Danh Sách Mũi Tên")]
    public SavedArrowGroup arrowData = new SavedArrowGroup();

    [Header("4. Danh Sách Vũ Khí Rơi (Kiếm, Khiên, Cung)")]
    public List<SavedWeaponGroup> weaponGroups = new List<SavedWeaponGroup>();
}

