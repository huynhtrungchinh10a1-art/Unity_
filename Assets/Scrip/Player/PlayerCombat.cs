using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    public Animator anim;

    [Header("Impact Settings")]
    public float impactCooldown = 0.2f;
    private float lastImpactTime = -10f;

    private HealthAndTeam health;
    private PlayerMovement movement;

    bool isAttacking = false;
    bool canQueueCombo = false;

    float bufferTime = 0.1f;
    float bufferTimer = 0f;

    void Start()
    {
        if (anim == null)
            anim = GetComponentInChildren<Animator>();

        health = GetComponent<HealthAndTeam>();
        movement = GetComponent<PlayerMovement>();

        if (health != null)
        {
            health.onDamaged += OnDamaged;
            health.onDie += OnDie;
        }
    }

    void OnDestroy()
    {
        if (health != null)
        {
            health.onDamaged -= OnDamaged;
            health.onDie -= OnDie;
        }
    }

    void Update()
    {
        if (anim == null) return;
        if (health != null && !health.isAlive) return;

        AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);

        // Chặn nhận input đánh khi đang Roll, dính đòn (Impact) hoặc đã Chết
        if (state.IsTag("Roll") || state.IsTag("Impact") || state.IsTag("Dead"))
        {
            bufferTimer = 0f;
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            bufferTimer = bufferTime;
        }

        if (bufferTimer > 0)
        {
            bufferTimer -= Time.deltaTime;
        }

        HandleBufferedInput();
    }

    void HandleBufferedInput()
    {
        if (bufferTimer <= 0) return;

        if (!isAttacking)
        {
            startAttack();
            bufferTimer = 0f;
        }
        else if (canQueueCombo)
        {
            anim.SetBool("ComboQueued", true);
            bufferTimer = 0f;
        }
    }

    void startAttack()
    {
        isAttacking = true;
        anim.SetTrigger("Attack");
    }

    void OpenComboWindow()
    {
        canQueueCombo = true;
        anim.SetBool("ComboQueued", false);
    }

    void CloseComboWindow()
    {
        canQueueCombo = false;
    }

    public void EndAttack()
    {
        ResetCombat();
    }

    public void ForceStopAttack()
    {
        ResetCombat();
        if (anim != null)
        {
            anim.ResetTrigger("Attack");
            anim.SetBool("ComboQueued", false);
        }
    }

    void ResetCombat()
    {
        isAttacking = false;
        canQueueCombo = false;
        if (anim != null)
        {
            anim.SetBool("ComboQueued", false);
        }
    }

    public bool IsAttacking()
    {
        if (anim == null) return false;
        AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
        return isAttacking || state.IsTag("Combo");
    }

    void OnDamaged(GameObject attacker, float damage)
    {
        if (health != null && !health.isAlive) return;

        AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
        if (state.IsTag("Roll") || state.IsTag("Dead")) return;

        // Giãn cách tối thiểu giữa 2 lần giật đòn (0.2s), cho phép ngắt đòn cũ nếu đòn mới tới
        if (Time.time - lastImpactTime < impactCooldown)
        {
            return;
        }

        lastImpactTime = Time.time;

        ForceStopAttack();

        anim.ResetTrigger("TakeAttack");
        anim.SetTrigger("TakeAttack");
    }

    void OnDie()
    {
        ForceStopAttack();

        if (movement != null)
        {
            movement.SetDead(true);
        }

        int deadIndex = Random.Range(0, 3);
        anim.SetInteger("DeadIndex", deadIndex);
        anim.SetTrigger("DoDead");
        enabled = false;
    }
}
