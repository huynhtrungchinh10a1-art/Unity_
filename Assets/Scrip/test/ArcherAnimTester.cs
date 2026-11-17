using UnityEngine;

public class ArcherAnimTester : MonoBehaviour
{
    private Animator anim;
    private PlayerMovement playerMovement;
    private PlayerCombat playerCombat;
    private HealthAndTeam health;

    [Header("GUI Display")]
    public bool showOnScreenGuide = true;

    void Awake()
    {
        anim = GetComponent<Animator>();
        if (anim == null)
            anim = GetComponentInChildren<Animator>();

        playerMovement = GetComponent<PlayerMovement>();
        playerCombat = GetComponent<PlayerCombat>();
        health = GetComponent<HealthAndTeam>();
    }

    void Update()
    {
        if (anim == null) return;

        // ==================== 1. TEST LĂN (ROLL) ====================
        // R hoặc Space: Lăn né đòn (DoRoll)
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (playerMovement != null)
            {
                playerMovement.TriggerRollManual();
            }
            else
            {
                anim.SetTrigger("DoRoll");
            }
            Debug.Log("[TEST] Player: Lăn né đòn (DoRoll)");
        }

        // ==================== 2. TEST DÍNH ĐÒN (IMPACT) ====================
        // H: Bị đánh trúng người (TakeAttack)
        if (Input.GetKeyDown(KeyCode.H))
        {
            if (playerCombat != null) playerCombat.ForceStopAttack();
            anim.SetTrigger("TakeAttack");
            Debug.Log("[TEST] Player: Dính đòn (TakeAttack / Impact)");
        }

        // ==================== 3. TEST CHẾT (3 DEATHS) ====================
        // Z: Chết kiểu 1 (DeadIndex = 0)
        if (Input.GetKeyDown(KeyCode.Z))
        {
            TriggerDeath(0);
        }

        // X: Chết kiểu 2 (DeadIndex = 1)
        if (Input.GetKeyDown(KeyCode.X))
        {
            TriggerDeath(1);
        }

        // C: Chết kiểu 3 (DeadIndex = 2)
        if (Input.GetKeyDown(KeyCode.C))
        {
            TriggerDeath(2);
        }

        // ==================== 4. HỒI SINH / RESET ĐỂ TEST TIẾP ====================
        // Phím 0 hoặc Backspace: Reset về trạng thái bình thường
        if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Backspace))
        {
            ResetPlayer();
        }
    }

    void TriggerDeath(int index)
    {
        if (playerCombat != null) playerCombat.ForceStopAttack();
        if (playerMovement != null) playerMovement.SetDeadState(true);

        anim.SetInteger("DeadIndex", index);
        anim.SetTrigger("DoDead");
        Debug.Log($"[TEST] Player: Chết kiểu {index + 1} (DeadIndex = {index})");
    }

    public void ResetPlayer()
    {
        if (health != null)
        {
            health.currentHealth = health.maxHealth;
            health.isAlive = true;
        }

        if (playerMovement != null)
        {
            playerMovement.SetDeadState(false);
            playerMovement.enabled = true;
        }

        if (playerCombat != null)
        {
            playerCombat.ForceStopAttack();
            playerCombat.enabled = true;
        }

        anim.Rebind();
        anim.Update(0f);
        Debug.Log("[TEST] Player: ĐÃ HỒI SINH / RESET VỀ IDLE ĐỂ TEST TIẾP!");
    }

    void OnGUI()
    {
        if (!showOnScreenGuide) return;

        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = 13;
        style.normal.textColor = Color.yellow;

        AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
        string currentTag = "Locomotion/Khác";
        if (state.IsTag("Combo")) currentTag = "<color=orange>Combo Đấm</color>";
        else if (state.IsTag("Roll")) currentTag = "<color=cyan>Đang Lăn (Roll)</color>";
        else if (state.IsTag("Impact")) currentTag = "<color=red>Dính đòn (Impact)</color>";
        else if (state.IsTag("Dead")) currentTag = "<color=gray>Đã Chết (Dead)</color>";

        string guide = "=== BẢNG TEST ANIMATION PLAYER ===\n" +
                       $"Trạng thái hiện tại: {currentTag}\n" +
                       "------------------------------------\n" +
                       "[Space / R]   : Lăn né đòn (DoRoll)\n" +
                       "[H]           : Trúng đòn (TakeAttack)\n" +
                       "------------------------------------\n" +
                       "[Z]           : Chết kiểu 1 (DeadIndex = 0)\n" +
                       "[X]           : Chết kiểu 2 (DeadIndex = 1)\n" +
                       "[C]           : Chết kiểu 3 (DeadIndex = 2)\n" +
                       "------------------------------------\n" +
                       "[Phím 0 / Backspace]: HỒI SINH / RESET";

        GUI.Box(new Rect(15, 15, 330, 240), guide, style);
    }
}
