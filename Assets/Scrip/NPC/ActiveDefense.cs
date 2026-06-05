using UnityEngine;

[RequireComponent(typeof(HealthAndTeam))]
public class ActiveDefense : MonoBehaviour
{
    [Header("warning")]
    public float warningOffset = 0.75f;
    public float warningRadius = 1.0f;
    private LayerMask characterLayerMask;

    [Header("defense")]
    public float blockChance = 0.5f;

    private NPCAnimatorHandler animHandler;
    private HealthAndTeam myHealth;
    private NPCCombat myCombat;

    void Awake()
    {
        animHandler = GetComponent<NPCAnimatorHandler>();
        myHealth = GetComponent<HealthAndTeam>();
        myCombat = GetComponent<NPCCombat>();
        characterLayerMask = LayerMask.GetMask("Character");
    }

    // chay khi attack
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
                    targetDefense.ReactToWarning(gameObject);
                }
            }
        }
    }

    // chay khi phong thu
    public void ReactToWarning(GameObject attacker = null)
    {
        if (animHandler != null && animHandler.IsRolling)
        {
            return;
        }

        bool isBlocking = Random.value < blockChance;

        if (isBlocking && attacker != null)
        {
            Vector3 dir = (attacker.transform.position - transform.position);
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(dir.normalized);
            }
        }

        if (animHandler != null)
        {
            animHandler.PlayBlock(isBlocking);
        }
    }

    public bool TryBlock(ref float damage)
    {
        if (animHandler != null && animHandler.IsBlocked())
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