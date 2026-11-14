using UnityEngine;
using System.Collections;

public class ArcherAnimTester : MonoBehaviour
{
    private Animator anim;
    private ArcherAnimatorHandler archerAnim;
    private ArcherCombat archerCombat;
    private bool isComboRunning = false;

    [Header("Settings")]
    public bool disableCombatOnStart = true;

    [Header("GUI Display")]
    public bool showOnScreenGuide = true;

    void Awake()
    {
        anim = GetComponent<Animator>();
        archerAnim = GetComponent<ArcherAnimatorHandler>();
        archerCombat = GetComponent<ArcherCombat>();
    }

    void Start()
    {
        if (disableCombatOnStart && archerCombat != null)
        {
            archerCombat.enabled = false;
        }
    }

    void Update()
    {
        if (anim == null) return;

        // ==================== 0. BẬT/TẮT AI CHIẾN ĐẤU ====================
        // T: Bật/Tắt AI ArcherCombat
        if (Input.GetKeyDown(KeyCode.T))
        {
            if (archerCombat != null)
            {
                archerCombat.enabled = !archerCombat.enabled;
                Debug.Log("[TEST] AI ArcherCombat: " + (archerCombat.enabled ? "BẬT" : "TẮT"));
            }
        }

        // ==================== 1. NHÓM BẮN CUNG (RANGED) ====================
        // Q: Rút tên và giương cung ngắm (Draw Arrow -> Aim)
        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (archerAnim != null) archerAnim.SetAiming(true);
            else anim.SetBool("isAiming", true);
            Debug.Log("[TEST] Bật ngắm (isAiming = true)");
        }

        // W: Buông dây bắn tên (Shoot)
        if (Input.GetKeyDown(KeyCode.W))
        {
            if (archerAnim != null)
            {
                archerAnim.PlayRangedAttack();
            }
            else
            {
                anim.SetTrigger("DoShoot");
                anim.SetBool("isAiming", false);
            }
            Debug.Log("[TEST] Bắn tên (DoShoot)!");
        }

        // E: Hủy ngắm, hạ cung xuống
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (isComboRunning)
            {
                StopAllCoroutines();
                isComboRunning = false;
            }
            if (archerAnim != null) archerAnim.SetAiming(false);
            else anim.SetBool("isAiming", false);
            Debug.Log("[TEST] Hủy ngắm (isAiming = false)");
        }

        // SPACE: Tự động chạy full combo (Rút tên -> Ngắm 1.2s -> Tự bắn -> Xong)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (!isComboRunning)
            {
                StartCoroutine(RoutineAutoShootCombo(0));
            }
        }

        // 8: Bắn thẳng tắp (Straight Shot)
        if (Input.GetKeyDown(KeyCode.Alpha8))
        {
            if (!isComboRunning)
            {
                StartCoroutine(RoutineAutoShootCombo(1));
            }
        }

        // 9: Bắn cầu vồng bổng lên trời (High Arc / Lob Shot)
        if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            if (!isComboRunning)
            {
                StartCoroutine(RoutineAutoShootCombo(2));
            }
        }

        // ==================== 2. NHÓM CẬN CHIẾN (MELEE) ====================
        // A: Đòn đá cận chiến (AttackIndex = 0)
        if (Input.GetKeyDown(KeyCode.A))
        {
            anim.SetInteger("AttackIndex", 0);
            anim.SetTrigger("DoMelee");
            Debug.Log("[TEST] Cận chiến: ĐÁ (AttackIndex = 0)");
        }

        // L: Đòn đấm cận chiến (AttackIndex = 1)
        if (Input.GetKeyDown(KeyCode.L))
        {
            anim.SetInteger("AttackIndex", 1);
            anim.SetTrigger("DoMelee");
            Debug.Log("[TEST] Cận chiến: ĐẤM (AttackIndex = 1)");
        }

        // ==================== 3. NHÓM DI CHUYỂN (LOCOMOTION) ====================
        // 1: Đứng yên (Idle)
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            anim.SetFloat("Speed", 0f);
            Debug.Log("[TEST] Idle (Speed = 0)");
        }

        // 2: Đi bộ (Walk)
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            anim.SetFloat("Speed", 0.5f);
            Debug.Log("[TEST] Đi bộ (Speed = 0.5)");
        }

        // 3: Chạy nhanh (Run)
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            anim.SetFloat("Speed", 1.0f);
            Debug.Log("[TEST] Chạy (Speed = 1.0)");
        }

        // ==================== 4. LĂN NÉ ĐÒN (ROLL) ====================
        // R: Lăn lộn né đòn
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (archerAnim != null) archerAnim.PlayRoll();
            else anim.SetTrigger("DoRoll");
            Debug.Log("[TEST] Lăn né đòn (DoRoll)");
        }

        // ==================== 5. PHÒNG THỦ & TRÚNG ĐÒN (DEFENSE & HIT) ====================
        // B: Đỡ đòn thành công (Pre Block -> Standing Block)
        if (Input.GetKeyDown(KeyCode.B))
        {
            if (archerAnim != null) archerAnim.PlayBlock(true);
            else
            {
                anim.SetBool("IsBlocked", true);
                anim.SetTrigger("DoBlock");
            }
            Debug.Log("[TEST] Đỡ đòn thành công (DoBlock & IsBlocked = true)");
        }

        // V: Đỡ đòn hụt / Bị phá thủ (IsBlocked = false)
        if (Input.GetKeyDown(KeyCode.V))
        {
            if (archerAnim != null) archerAnim.PlayBlock(false);
            else
            {
                anim.SetBool("IsBlocked", false);
                anim.SetTrigger("DoBlock");
            }
            Debug.Log("[TEST] Đỡ đòn thất bại (DoBlock & IsBlocked = false)");
        }

        // H: Bị đánh trúng người (Impact)
        if (Input.GetKeyDown(KeyCode.H))
        {
            anim.SetTrigger("TakeAttack");
            Debug.Log("[TEST] Dính đòn (TakeAttack / Impact)");
        }

        // ==================== 6. CHẾT (DEATH) ====================
        // Z: Chết ngửa ra sau (DeadIndex = 0)
        if (Input.GetKeyDown(KeyCode.Z))
        {
            anim.SetInteger("DeadIndex", 0);
            anim.SetTrigger("DoDead");
            Debug.Log("[TEST] Chết ngửa ra sau (DeadIndex = 0)");
        }

        // X: Chết chúi về trước (DeadIndex = 1)
        if (Input.GetKeyDown(KeyCode.X))
        {
            anim.SetInteger("DeadIndex", 1);
            anim.SetTrigger("DoDead");
            Debug.Log("[TEST] Chết chúi về trước (DeadIndex = 1)");
        }

        // C: Chết nghiêng sang phải (DeadIndex = 2)
        if (Input.GetKeyDown(KeyCode.C))
        {
            anim.SetInteger("DeadIndex", 2);
            anim.SetTrigger("DoDead");
            Debug.Log("[TEST] Chết nghiêng sang phải (DeadIndex = 2)");
        }
    }

    private IEnumerator RoutineAutoShootCombo(int mode = 0)
    {
        isComboRunning = true;
        if (archerAnim != null) archerAnim.SetAiming(true);
        else anim.SetBool("isAiming", true);

        yield return new WaitForSeconds(1.2f);

        if (archerAnim != null) archerAnim.PlayRangedAttack(mode);
        else
        {
            anim.SetTrigger("DoShoot");
            anim.SetBool("isAiming", false);
        }

        yield return new WaitForSeconds(0.5f);
        isComboRunning = false;
    }

    void OnGUI()
    {
        if (!showOnScreenGuide) return;

        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = 12;
        style.normal.textColor = Color.yellow;

        bool aiming = anim != null && anim.GetBool("isAiming");
        string aiStatus = (archerCombat != null && archerCombat.enabled) ? "<color=green>BẬT (Tự động)</color>" : "<color=cyan>TẮT (Manual Test)</color>";

        string guide = "=== ARCHER ANIMATION TESTER ===\n" +
                       $"AI Combat: {aiStatus}  [Phím T: Bật/Tắt]\n" +
                       $"Đang ngắm: {(aiming ? "<color=red>CÓ</color>" : "KHÔNG")} | Combo: {(isComboRunning ? "<color=orange>CHẠY</color>" : "RẢNH")}\n" +
                       "-----------------------------\n" +
                       "[Q] : Rút tên & Giương cung (Aim)\n" +
                       "[W] : Bắn tên (Shoot Recoil)\n" +
                       "[E] : Hủy ngắm (Hạ cung)\n" +
                       "[SPACE] : Tự động Full Combo Bắn\n" +
                       "[8] : Test BẮN THẲNG (Straight)\n" +
                       "[9] : Test BẮN CẦU VỒNG (High Arc)\n" +
                       "-----------------------------\n" +
                       "[A] : Đá cận chiến (Kick)\n" +
                       "[L] : Đấm cận chiến (Punch)\n" +
                       "-----------------------------\n" +
                       "[1] : Idle  |  [2] : Đi bộ  |  [3] : Chạy\n" +
                       "[R] : Lăn né đòn (Roll)\n" +
                       "-----------------------------\n" +
                       "[B] : Đỡ thành công | [V] : Đỡ xịt\n" +
                       "[H] : Trúng đòn (Impact)\n" +
                       "-----------------------------\n" +
                       "[Z] / [X] / [C] : 3 kiểu Chết";

        GUI.Box(new Rect(15, 15, 300, 395), guide, style);
    }
}
