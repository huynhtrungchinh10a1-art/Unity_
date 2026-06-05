using UnityEngine;
using System.Collections;

public class ArcherAnimatorHandler : NPCAnimatorHandler
{
    [Header("Archer Settings")]
    public float aimDuration = 1.5f;
    public float meleeRangeThreshold = 1.5f;

    private Coroutine aimCoroutine;
    private bool canMeleeRetaliate = false;

    protected override void Awake()
    {
        base.Awake();
        if (myHealth != null)
        {
            myHealth.onDamaged += OnArcherDamaged;
        }
    }

    private void OnDestroy()
    {
        if (myHealth != null)
        {
            myHealth.onDamaged -= OnArcherDamaged;
        }
    }

    private void OnArcherDamaged(GameObject attacker, float damage)
    {
        // Khi bị đánh trúng, kích hoạt khả năng phản công cận chiến 1 lần
        canMeleeRetaliate = true;
    }

    public override void PlayAttack(int attackIndex)
    {
        if (anim == null) return;

        if (combat != null && combat.CurrentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, combat.CurrentTarget.position);
            if (dist <= meleeRangeThreshold)    
            {
                if (canMeleeRetaliate)
                {
                    canMeleeRetaliate = false; // Tiêu thụ đòn đánh trả
                    StopAiming();
                    base.PlayAttack(attackIndex);
                }
                return; // Nếu không có đòn đánh trả, không đánh cận chiến (chỉ chạy đi)
            }
        }

        if (aimCoroutine != null)
        {
            StopCoroutine(aimCoroutine);
        }
        aimCoroutine = StartCoroutine(AimSequence());
    }

    private IEnumerator AimSequence()
    {
        anim.SetBool("isAiming", true);
        yield return new WaitForSeconds(aimDuration);
        anim.SetBool("isAiming", false);
        aimCoroutine = null;
    }

    public void StopAiming()
    {
        if (anim == null) return;

        if (aimCoroutine != null)
        {
            StopCoroutine(aimCoroutine);
            aimCoroutine = null;
        }
        anim.SetBool("isAiming", false);
    }

    public override void PlayRoll()
    {
        StopAiming();
        base.PlayRoll();
    }

    public override void PlayDeath(int deadIndex)
    {
        StopAiming();
        base.PlayDeath(deadIndex);
    }

    public override void PlayBlock(bool isBlocking)
    {
        if (isBlocking)
        {
            StopAiming();
        }
        base.PlayBlock(isBlocking);
    }

    public override void UpdateRootMotionState()
    {
        if (anim == null) return;

        AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo next = anim.GetNextAnimatorStateInfo(0);

        if (state.IsTag("Impact") || next.IsTag("Impact"))
        {
            StopAiming();
        }

        base.UpdateRootMotionState();
    }
}
