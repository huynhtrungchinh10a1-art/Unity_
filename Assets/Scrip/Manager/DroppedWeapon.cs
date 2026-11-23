using UnityEngine;

public class DroppedWeapon : MonoBehaviour
{
    [Header("Weapon Info")]
    public string weaponID = "Sword";
    public bool isLootable = false;

    private Renderer[] cachedRenderers;
    private bool isVisible = true;

    void Awake()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        if (string.IsNullOrEmpty(weaponID) || weaponID == "Sword")
        {
            weaponID = gameObject.name.Replace("(Clone)", "").Trim();
        }
    }

    public void DetachFromHand()
    {
        Transform parentContainer = null;
        if (DroppedWeaponManager.Instance != null)
        {
            parentContainer = DroppedWeaponManager.Instance.WeaponContainer;
        }

        transform.SetParent(parentContainer);

        // Bắn tia Raycast xuống đất để thanh kiếm/khiên nằm phẳng tự nhiên trên mặt đất
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 4.0f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            transform.position = hit.point + Vector3.up * 0.04f;
        }

        // Tạo Trigger tròn nhẹ nhàng quanh vũ khí để phát hiện Player lại gần nhặt
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            SphereCollider sc = gameObject.AddComponent<SphereCollider>();
            sc.radius = 1.3f;
            sc.isTrigger = true;
        }
        else
        {
            col.isTrigger = true;
        }

        isLootable = true;

        if (DroppedWeaponManager.Instance != null)
        {
            DroppedWeaponManager.Instance.RegisterWeapon(this);
        }
    }

    public void SetVisible(bool visible)
    {
        if (isVisible == visible) return;
        isVisible = visible;

        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
        }

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
            {
                cachedRenderers[i].enabled = visible;
            }
        }
    }

    // Đã xóa cơ chế tự động nhặt khi đi ngang qua.
    // Khi Player cúi xuống/bấm nút nhặt, script của Player chỉ cần gọi hàm Pickup() bên dưới.
    public void Pickup()
    {
        Debug.Log($"<color=yellow>[Loot]</color> Đã nhặt vũ khí: <b>{weaponID}</b> khỏi mặt đất!");

        if (DroppedWeaponManager.Instance != null)
        {
            DroppedWeaponManager.Instance.UnregisterWeapon(this);
        }

        // Xóa hoàn toàn vật thể kiếm/khiên dưới đất
        Destroy(gameObject);
    }
}
