using UnityEngine;

public class ArcherAnimatorHandler : NPCAnimatorHandler
{
    public void UpdateBlendTree(float moveX, float moveZ)
    {
        if (anim == null) return;
        
        // Truyền trục X (Strafe) và Z (Forward/Backward) vào Blend Tree Layer 0
        anim.SetFloat("MoveX", moveX, 0.1f, Time.deltaTime);
        anim.SetFloat("MoveZ", moveZ, 0.1f, Time.deltaTime);
    }

    public void SetAiming(bool isAiming)
    {
        if (anim == null) return;
        
        // Bật cờ này để Animator biết đường Weight = 1 cho Layer 1 (Mask nửa trên)
        anim.SetBool("isAiming", isAiming);
    }

    public void PlayRangedAttack()
    {
        if (anim == null) return;
        anim.SetTrigger("DoShoot"); // Trigger bắn mũi tên
    }

    public void PlayMeleeAttack()
    {
        if (anim == null) return;
        // Tái sử dụng base.PlayAttack hoặc tạo trigger chém/đá riêng
        anim.SetInteger("AttackIndex", Random.Range(0, 2)); // Giả sử có 2 đòn melee
        anim.SetTrigger("DoMelee"); 
    }
}
