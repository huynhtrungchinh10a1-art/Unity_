using UnityEngine;

public class AttackStateExit : StateMachineBehaviour
{
    PlayerCombat combat;

    override public void OnStateMachineExit(Animator animator, int stateMachinePathHash)
    {
        if (combat == null)
            combat = animator.GetComponentInParent<PlayerCombat>();

        combat.ForceStopAttack();
    }
}