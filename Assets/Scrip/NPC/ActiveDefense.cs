using UnityEngine;

[RequireComponent(typeof(HealthAndTeam))]
[RequireComponent(typeof(Animator))]
public class ActiveDefense : MonoBehaviour
{
    [Header("warning")]
    public float warningOffset = 0.75f;
    public float warningRadius = 1.0f;
    private LayerMask characterLayerMask;

    [Header("defense")]
    public float blockChance = 0.5f;

    private Animator anim;
    private HealthAndTeam myHealth;
    private NPCCombat myCombat;

    void Awake()
    {
        anim = GetComponent<Animator>();
        myHealth = GetComponent<HealthAndTeam>();
        myCombat = GetComponent<NPCCombat>();
        characterLayerMask = LayerMask.GetMask("Character");
    }

    public void AnimEvent_WarnAttack()
    {
        Vector3 checkPos = transform.position + transform.forward * warningOffset;
        Collider[] hitColliders = Physics.OverlapSphere(checkPos, warningRadius, characterLayerMask);

        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.gameObject == this.gameObject) continue;

            HealthAndTeam targetHealth = hitCollider.GetComponent<HealthAndTeam>();
            if (targetHealth != null && targetHealth.isAlive && myHealth.IsEnemy(targetHealth.teamCurrent))
            {
                ActiveDefense targetDefense = hitCollider.GetComponent<ActiveDefense>();
                if (targetDefense != null)
                {
                    targetDefense.ReactToWarning();
                }
            }
        }
    }

    public void ReactToWarning()
    {
        bool isBlocking = Random.value < blockChance;

        if (anim != null)
        {
            anim.SetTrigger("DoBlock");
            anim.SetBool("IsBlocked", isBlocking);

            if (isBlocking)
            {
                anim.ResetTrigger("DoAttack");
            }
        }
    }

    public bool TryBlock(ref float damage)
    {
        if (anim != null && anim.GetBool("IsBlocked"))
        {
            damage = 0f;
            ResetCooldowns();
            return true;
        }
        return false;
    }

    private void ResetCooldowns()
    {
        if (myCombat != null)
        {
            myCombat.attackCooldownTimer = 0f;
        }
    }
}