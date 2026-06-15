using UnityEngine;

public class AttackStateExit : StateMachineBehaviour
{

    override public void OnStateMachineExit(Animator animator, int stateMachinePathHash)
    {
        PlayerCombat combat = animator.GetComponentInParent<PlayerCombat>();
        if (combat != null)
        {
            combat.ForceStopAttack();
        }

        NPCCombat npcCombat = animator.GetComponentInParent<NPCCombat>();
        if (npcCombat != null)
        {
            NPCAnimatorHandler animHandler = animator.GetComponentInParent<NPCAnimatorHandler>();
            if (animHandler != null)
            {
                animHandler.ResetAttack();
            }

            animator.SetBool("IsBlocked", false);
        }
    }
}