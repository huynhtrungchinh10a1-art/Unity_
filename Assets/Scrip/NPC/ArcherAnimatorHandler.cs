using UnityEngine;

public class ArcherAnimatorHandler : NPCAnimatorHandler
{
    public void UpdateBlendTree(float moveX, float moveZ)
    {
        if (anim == null) return;

        anim.SetFloat("MoveX", moveX, 0.1f, Time.deltaTime);
        anim.SetFloat("MoveZ", moveZ, 0.1f, Time.deltaTime);
    }

    public void SetAiming(bool isAiming)
    {
        if (anim == null) return;
        
        anim.SetBool("isAiming", isAiming);
    }

    public void PlayRangedAttack()
    {
        if (anim == null) return;
        anim.SetTrigger("DoShoot");
    }

    public void PlayMeleeAttack()
    {
        if (anim == null) return;

        anim.SetInteger("AttackIndex", Random.Range(0, 2));
        anim.SetTrigger("DoMelee"); 
    }
}
