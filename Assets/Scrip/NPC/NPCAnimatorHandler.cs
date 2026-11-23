using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class NPCAnimatorHandler : MonoBehaviour
{
    protected Animator anim;
    protected CharacterController controller;
    protected NavMeshAgent agent;
    protected NPCCombat combat;
    protected HealthAndTeam myHealth;

    // Roll collision state
    private List<Collider> ignoredColliders = new List<Collider>();
    private bool wasRollingLastFrame = false;

    // để NPCCombat đọc
    public bool IsUsingRootMotion { get; protected set; }
    public bool IsRolling { get; protected set; }
    public bool IsAttackingCombo { get; protected set; }

    protected virtual void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        controller = GetComponent<CharacterController>();
        agent = GetComponent<NavMeshAgent>();
        combat = GetComponent<NPCCombat>();
        myHealth = GetComponent<HealthAndTeam>();
    }

    // ========== Virtual Methods ==========

    public virtual void UpdateRootMotionState()
    {
        if (anim == null) return;

        AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo next = anim.GetNextAnimatorStateInfo(0);

        IsAttackingCombo = state.IsTag("Combo") || (anim.IsInTransition(0) && next.IsTag("Combo"));

        IsUsingRootMotion =
            IsAttackingCombo ||
            state.IsTag("Dead") ||
            state.IsTag("Impact") ||
            state.IsTag("Roll") ||
            (anim.IsInTransition(0) &&
                (next.IsTag("Dead") || next.IsTag("Impact") || next.IsTag("Roll")));

        IsRolling = state.IsTag("Roll") || (anim.IsInTransition(0) && next.IsTag("Roll"));

        anim.applyRootMotion = IsUsingRootMotion;

        if (IsUsingRootMotion)
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
        }

        agent.nextPosition = transform.position;
    }

    public virtual void PlayAttack(int attackIndex)
    {
        if (anim == null) return;
        anim.SetInteger("AttackIndex", attackIndex);
        anim.SetTrigger("DoAttack");
        IsAttackingCombo = true;
        IsUsingRootMotion = true;
        anim.applyRootMotion = true;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
    }

    public virtual void PlayRoll()
    {
        if (anim == null) return;
        IsRolling = true;
        anim.SetTrigger("DoRoll");
    }

    public virtual void PlayDeath(int deadIndex)
    {
        if (anim == null) return;
        anim.SetInteger("DeadIndex", deadIndex);
        anim.SetTrigger("DoDead");
        IsUsingRootMotion = true;
        anim.applyRootMotion = true;
    }

    public virtual void PlayBlock(bool isBlocking)
    {
        if (anim == null) return;
        anim.SetTrigger("DoBlock");
        anim.SetBool("IsBlocked", isBlocking);
        if (isBlocking)
        {
            anim.ResetTrigger("DoAttack");
        }
    }

    public virtual void ResetAttack()
    {
        if (anim == null) return;
        anim.ResetTrigger("DoAttack");
    }

    public virtual void UpdateLocomotion(float speed)
    {
        if (anim == null) return;
        anim.SetFloat("Speed", speed);
    }

    public virtual bool IsBlocked()
    {
        if (anim == null) return false;
        return anim.GetBool("IsBlocked");
    }

    // ========== OnAnimatorMove (Unity Callback) ==========

    void OnAnimatorMove()
    {
        if (anim == null || !anim.applyRootMotion) return;
        if (controller == null || !controller.enabled) return;

        Vector3 delta = anim.deltaPosition;
        if (combat != null)
        {
            delta.y += combat.VerticalVelocity * Time.deltaTime;
        }

        AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
        bool rolling = state.IsTag("Roll");

        if (rolling)
        {
            HandleRollCollision();
            controller.Move(delta);
            wasRollingLastFrame = true;
        }
        else
        {
            if (wasRollingLastFrame)
            {
                RestoreRollCollision();
            }

            delta = ApplySeparationForce(delta);
            controller.Move(delta);
        }

        transform.rotation *= anim.deltaRotation;
        agent.nextPosition = transform.position;
    }

    // ========== Private Helpers ==========

    private void HandleRollCollision()
    {
        int characterLayer = LayerMask.NameToLayer("Character");
        if (characterLayer != -1)
        {
            Collider[] nearby = Physics.OverlapSphere(transform.position, 3f, 1 << characterLayer);
            foreach (var col in nearby)
            {
                if (col != controller && col.gameObject != gameObject)
                {
                    Physics.IgnoreCollision(controller, col, true);
                    if (!ignoredColliders.Contains(col))
                        ignoredColliders.Add(col);
                }
            }
        }
    }

    private void RestoreRollCollision()
    {
        foreach (var col in ignoredColliders)
        {
            if (col != null)
                Physics.IgnoreCollision(controller, col, false);
        }
        ignoredColliders.Clear();
        wasRollingLastFrame = false;
    }

    private Vector3 ApplySeparationForce(Vector3 delta)
    {
        if (BattlefieldManager.Instance == null || combat == null) return delta;

        List<NPCCombat> allies = BattlefieldManager.Instance.GetNPCsInCell(transform.position);
        if (allies == null) return delta;

        Vector3 separation = Vector3.zero;
        int count = 0;

        for (int i = 0; i < allies.Count; i++)
        {
            NPCCombat ally = allies[i];
            if (ally == null || ally.gameObject == null || ally == combat) continue;
            if (ally.MyHealth == null || !ally.MyHealth.isAlive) continue;
            if (myHealth != null && myHealth.IsEnemy(ally.MyHealth.teamCurrent)) continue;

            float dist = Vector3.Distance(transform.position, ally.transform.position);
            if (dist < combat.separationDistance && dist > 0.01f)
            {
                Vector3 dir = (transform.position - ally.transform.position).normalized;
                separation += dir * (combat.separationDistance - dist);
                count++;
            }
        }

        if (count > 0)
        {
            separation /= count;
            float dot = Vector3.Dot(separation, transform.forward);
            if (dot < 0)
            {
                separation -= transform.forward * dot;
            }
            delta += separation * Time.deltaTime * combat.separationForce;
        }

        return delta;
    }
}
