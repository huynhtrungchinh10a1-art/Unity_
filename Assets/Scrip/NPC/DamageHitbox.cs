using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class DamageHitbox : MonoBehaviour
{
    public float damage = 1f;

    private Collider hitboxCollider;
    private GameObject owner;

    private HealthAndTeam ownerHealth;
    private NPCCombat ownerCombat;

    private HashSet<GameObject> hitTargets = new HashSet<GameObject>();

    void Awake()
    {
        hitboxCollider = GetComponent<Collider>();

        // mac dinh tat
        if (hitboxCollider != null)
        {
            hitboxCollider.enabled = false;
        }
    }

    public void Init(GameObject wielder)
    {
        owner = wielder;
        ownerHealth = wielder.GetComponent<HealthAndTeam>();
        ownerCombat = wielder.GetComponent<NPCCombat>();
    }

    public void EnableHitbox()
    {
        hitTargets.Clear();
        if (hitboxCollider != null)
            hitboxCollider.enabled = true;
    }

    public void DisableHitbox()
    {
        if (hitboxCollider != null)
            hitboxCollider.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (owner == null) return;
        if (other.gameObject == owner) return;

        if (hitTargets.Contains(other.gameObject)) return;

        HealthAndTeam targetHealth = other.GetComponent<HealthAndTeam>();
        if (targetHealth != null && targetHealth.isAlive)
        {
            if (ownerHealth.IsEnemy(targetHealth.teamCurrent))
            {
                hitTargets.Add(other.gameObject);
                bool wasBlocked = targetHealth.TakeDamage(damage, owner);

                if (wasBlocked)
                {
                    ApplyRecoilToOwner();
                }
            }
        }
    }

    private void ApplyRecoilToOwner()
    {
        if (ownerCombat != null) ownerCombat.attackCooldownTimer += 2f;
    }

}