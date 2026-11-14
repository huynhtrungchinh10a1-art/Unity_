using UnityEngine;

public class CombatDefenseTester : MonoBehaviour
{
    [Header("1. Kéo 2 nhân vật vào đây")]
    [Tooltip("Nhân vật tấn công (ví dụ: Archer hoặc NPC Kiếm)")]
    public GameObject attacker;

    [Tooltip("Nhân vật bị đánh / phòng thủ (ví dụ: NPC Kiếm hoặc Archer)")]
    public GameObject defender;

    [Header("2. Cài đặt thử nghiệm")]
    [Tooltip("Khoảng cách thử nghiệm (m)")]
    public float testDistance = 1.5f;

    [Tooltip("Tắt AI tự động khi Start để bạn tự bấm test từng đòn")]
    public bool freezeAIOnStart = true;

    [Tooltip("Ép Defender luôn luôn Block 100% (bỏ qua tỉ lệ 50% hên xui)")]
    public bool force100PercentBlock = true;

    // Cache components
    private NPCCombat attackerCombat;
    private NPCCombat defenderCombat;
    private NPCAnimatorHandler attackerAnim;
    private NPCAnimatorHandler defenderAnim;
    private ActiveDefense attackerDefense;
    private ActiveDefense defenderDefense;
    private HealthAndTeam attackerHealth;
    private HealthAndTeam defenderHealth;

    private float originalDefenderBlockChance = 0.5f;
    private string lastLog = "Sẵn sàng test. Bấm các phím số hoặc nút trên màn hình.";

    void Start()
    {
        InitComponents();

        if (freezeAIOnStart)
        {
            SetAIEnabled(false);
        }

        if (defenderDefense != null)
        {
            originalDefenderBlockChance = defenderDefense.blockChance;
            if (force100PercentBlock)
            {
                defenderDefense.blockChance = 1.0f;
            }
        }
    }

    void InitComponents()
    {
        if (attacker != null)
        {
            attackerCombat = attacker.GetComponent<NPCCombat>();
            attackerAnim = attacker.GetComponent<NPCAnimatorHandler>();
            attackerDefense = attacker.GetComponent<ActiveDefense>();
            attackerHealth = attacker.GetComponent<HealthAndTeam>();
        }

        if (defender != null)
        {
            defenderCombat = defender.GetComponent<NPCCombat>();
            defenderAnim = defender.GetComponent<NPCAnimatorHandler>();
            defenderDefense = defender.GetComponent<ActiveDefense>();
            defenderHealth = defender.GetComponent<HealthAndTeam>();
        }
    }

    void Update()
    {
        // Phím 1: Đặt khoảng cách chuẩn và xoay 2 đứa đối mặt nhau
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            AlignAndSetDistance(testDistance);
        }

        // Phím 2: Attacker ra đòn cận chiến (Melee Attack)
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            TriggerAttackerMelee();
        }

        // Phím 3: Thử nghiệm riêng vòng quét WarnAttack (không cần animation)
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            TestWarnAttackSphere();
        }

        // Phím 4: Ép Defender Block ngay lập tức
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            ForceDefenderBlock();
        }

        // Phím 5: Bật / Tắt AI tự động
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            ToggleAI();
        }
    }

    // ==================== CÁC HÀM THỬ NGHIỆM ====================

    /// <summary>
    /// Đặt 2 nhân vật đứng đối diện nhau ở khoảng cách chính xác
    /// </summary>
    public void AlignAndSetDistance(float dist)
    {
        if (attacker == null || defender == null) return;

        Vector3 center = (attacker.transform.position + defender.transform.position) * 0.5f;
        Vector3 forward = attacker.transform.forward;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        forward.y = 0;
        forward.Normalize();

        attacker.transform.position = center - forward * (dist * 0.5f);
        defender.transform.position = center + forward * (dist * 0.5f);

        // Xoay đối mặt nhau
        attacker.transform.rotation = Quaternion.LookRotation(forward);
        defender.transform.rotation = Quaternion.LookRotation(-forward);

        lastLog = $"[TEST] Đã đặt 2 đứa đối mặt nhau ở khoảng cách: {dist:F2}m";
        Debug.Log(lastLog);
    }

    /// <summary>
    /// Cho Attacker tung đòn cận chiến
    /// </summary>
    public void TriggerAttackerMelee()
    {
        if (attacker == null) return;

        // Xoay Attacker đối mặt Defender ngay trước khi ra đòn
        if (defender != null)
        {
            Vector3 dir = (defender.transform.position - attacker.transform.position);
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
            {
                attacker.transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        // Nếu là Archer
        ArcherAnimatorHandler archerAnim = attacker.GetComponent<ArcherAnimatorHandler>();
        if (archerAnim != null)
        {
            archerAnim.PlayMeleeAttack();
            lastLog = $"[TEST] Archer tung đòn Melee (Kick/Punch)! Hãy xem Defender có Block không.";
            Debug.Log(lastLog);
            return;
        }

        // Nếu là NPC Kiếm
        if (attackerAnim != null)
        {
            attackerAnim.PlayAttack(0);
            lastLog = $"[TEST] NPC Kiếm tung đòn chém! Hãy xem Defender có Block không.";
            Debug.Log(lastLog);
        }
    }

    /// <summary>
    /// Kiểm tra độc lập vòng quét Physics.OverlapSphere của ActiveDefense
    /// </summary>
    public void TestWarnAttackSphere()
    {
        if (attacker == null || defender == null || attackerDefense == null)
        {
            lastLog = "[TEST ERROR] Thiếu Attacker, Defender hoặc ActiveDefense!";
            Debug.LogError(lastLog);
            return;
        }

        Vector3 checkPos = attacker.transform.position + attacker.transform.forward * attackerDefense.warningOffset;
        LayerMask mask = LayerMask.GetMask("Character");
        Collider[] hitColliders = Physics.OverlapSphere(checkPos, attackerDefense.warningRadius, mask);

        float curDist = Vector3.Distance(attacker.transform.position, defender.transform.position);
        Debug.Log($"<color=cyan>========== KIỂM TRA VÒNG QUÉT WARN ATTACK ==========</color>\n" +
                  $"Khoảng cách giữa 2 đứa: {curDist:F2}m\n" +
                  $"Tâm quả cầu checkPos: offset {attackerDefense.warningOffset}m phía trước\n" +
                  $"Bán kính warningRadius: {attackerDefense.warningRadius}m (quét tới {attackerDefense.warningOffset + attackerDefense.warningRadius:F2}m)\n" +
                  $"LayerMask 'Character': {mask.value}\n" +
                  $"Số Collider quét trúng: {hitColliders.Length}");

        bool foundDefender = false;

        foreach (var col in hitColliders)
        {
            if (col.gameObject == attacker) continue;

            Debug.Log($"   -> Quét trúng Collider: <b>'{col.name}'</b> trên GameObject: <b>'{col.gameObject.name}'</b> (Layer: {LayerMask.LayerToName(col.gameObject.layer)})");

            HealthAndTeam directHealth = col.GetComponent<HealthAndTeam>();
            HealthAndTeam parentHealth = col.GetComponentInParent<HealthAndTeam>();

            ActiveDefense directDef = col.GetComponent<ActiveDefense>();
            ActiveDefense parentDef = col.GetComponentInParent<ActiveDefense>();

            Debug.Log($"      [Kiểm tra GetComponent]: Direct Health: {(directHealth != null ? "CÓ" : "<color=red>NULL</color>")} | Direct Defense: {(directDef != null ? "CÓ" : "<color=red>NULL</color>")}");
            Debug.Log($"      [Kiểm tra GetComponentInParent]: Parent Health: {(parentHealth != null ? "CÓ" : "<color=red>NULL</color>")} | Parent Defense: {(parentDef != null ? "CÓ" : "<color=red>NULL</color>")}");

            if (parentHealth != null && parentHealth.gameObject == defender)
            {
                foundDefender = true;
            }
        }

        if (foundDefender)
        {
            lastLog = $"[TEST] Vòng quét CÓ chạm vào Defender! Xem Console để thấy chi tiết GetComponent.";
        }
        else
        {
            lastLog = $"[TEST] Vòng quét KHÔNG CHẠM tới Defender! (Khoảng cách {curDist:F2}m vượt quá tầm quét {attackerDefense.warningOffset + attackerDefense.warningRadius:F2}m hoặc sai Layer).";
        }
        Debug.Log(lastLog);
    }

    /// <summary>
    /// Ép Defender Block ngay lập tức
    /// </summary>
    public void ForceDefenderBlock()
    {
        if (defenderDefense == null)
        {
            lastLog = "[TEST ERROR] Defender không có component ActiveDefense!";
            Debug.LogError(lastLog);
            return;
        }

        // Đảm bảo 100% block
        defenderDefense.blockChance = 1.0f;
        defenderDefense.ReactToWarning(attacker);

        // Đo góc quay mặt
        if (attacker != null)
        {
            Vector3 dir = (attacker.transform.position - defender.transform.position);
            dir.y = 0;
            float angle = Vector3.Angle(defender.transform.forward, dir);
            lastLog = $"[TEST] Đã ép Defender Block! Góc lệch so với Attacker: {angle:F1} độ (0 độ = nhìn thẳng mặt).";
            Debug.Log(lastLog);
        }
    }

    public void ToggleAI()
    {
        bool currentState = attackerCombat != null && attackerCombat.enabled;
        SetAIEnabled(!currentState);
        lastLog = $"[TEST] AI chiến đấu: {(!currentState ? "ĐÃ BẬT" : "ĐÃ TẮT")}";
        Debug.Log(lastLog);
    }

    private void SetAIEnabled(bool state)
    {
        if (attackerCombat != null) attackerCombat.enabled = state;
        if (defenderCombat != null) defenderCombat.enabled = state;
    }

    // ==================== ONGUI HIỂN THỊ TRÊN MÀN HÌNH ====================
    void OnGUI()
    {
        float width = 380f;
        float height = 360f;
        GUI.Box(new Rect(10, 10, width, height), "<b>[COMBAT & DEFENSE TESTER]</b>");

        GUILayout.BeginArea(new Rect(20, 35, width - 20, height - 40));

        if (attacker == null || defender == null)
        {
            GUILayout.Label("<color=red>Vui lòng kéo Attacker và Defender vào Inspector!</color>");
            GUILayout.EndArea();
            return;
        }

        float dist = Vector3.Distance(attacker.transform.position, defender.transform.position);

        // Thông tin 2 bên
        GUILayout.Label($"<b>Attacker:</b> {attacker.name} (Layer: {LayerMask.LayerToName(attacker.layer)})");
        GUILayout.Label($"<b>Defender:</b> {defender.name} (Layer: {LayerMask.LayerToName(defender.layer)})");
        GUILayout.Label($"<b>Khoảng cách hiện tại:</b> <color=yellow>{dist:F2}m</color>");

        if (defenderDefense != null)
        {
            GUILayout.Label($"<b>Defender Block Chance:</b> {defenderDefense.blockChance * 100f:F0}%");
        }

        GUILayout.Space(5);

        // Nút bấm
        if (GUILayout.Button("[1] Căn khoảng cách & Xoay đối mặt (1.5m)"))
        {
            AlignAndSetDistance(testDistance);
        }

        if (GUILayout.Button("[2] Attacker tung đòn cận chiến (Melee)"))
        {
            TriggerAttackerMelee();
        }

        if (GUILayout.Button("[3] Thử nghiệm vòng quét WarnAttack"))
        {
            TestWarnAttackSphere();
        }

        if (GUILayout.Button("[4] Ép Defender Block (Force Block)"))
        {
            ForceDefenderBlock();
        }

        bool isAIOn = attackerCombat != null && attackerCombat.enabled;
        if (GUILayout.Button($"[5] AI tự động: {(isAIOn ? "<color=green>BẬT</color>" : "<color=red>TẮT</color>")}"))
        {
            ToggleAI();
        }

        GUILayout.Space(5);
        GUILayout.Label($"<b>Log:</b> {lastLog}");

        GUILayout.EndArea();
    }

    // ==================== VẼ GIZMOS TRONG SCENE VIEW ====================
    void OnDrawGizmos()
    {
        if (attacker == null || defender == null) return;

        // Vẽ đường nối giữa 2 đứa
        Gizmos.color = Color.white;
        Gizmos.DrawLine(attacker.transform.position + Vector3.up, defender.transform.position + Vector3.up);

        // Vẽ vòng quét WarnAttack của Attacker
        ActiveDefense def = attacker.GetComponent<ActiveDefense>();
        if (def != null)
        {
            Vector3 sphereCenter = attacker.transform.position + attacker.transform.forward * def.warningOffset + Vector3.up * 0.8f;
            float distToDefender = Vector3.Distance(sphereCenter, defender.transform.position + Vector3.up * 0.8f);

            // Xanh lá nếu chạm Defender, Vàng nếu không chạm
            Gizmos.color = (distToDefender <= def.warningRadius) ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(sphereCenter, def.warningRadius);
        }

        // Vẽ hướng nhìn của 2 đứa
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(attacker.transform.position + Vector3.up, attacker.transform.forward * 1.5f);
        Gizmos.DrawRay(defender.transform.position + Vector3.up, defender.transform.forward * 1.5f);
    }
}

