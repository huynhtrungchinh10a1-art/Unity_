using UnityEngine;
using System.Collections.Generic;

public enum Team
{
    TeamA,
    TeamB,
    Neutral
}

public class HealthAndTeam : MonoBehaviour
{
    [Header("Team")]
    public Team teamCurrent = Team.Neutral;

    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth;
    public bool isAlive = true;

    public int attackerCount = 0;
    public List<GameObject> currentAttackers = new List<GameObject>();

    public System.Action<GameObject, float> onDamaged;
    public System.Action onDie;

    private ActiveDefense defense;

    void Awake()
    {
        currentHealth = maxHealth;
        defense = GetComponent<ActiveDefense>();
    }
    public void ChangeTeam(Team newTeam)
    {
        if (teamCurrent == newTeam) return;

        teamCurrent = newTeam;
    }
    public bool TakeDamage(float damage, GameObject attacker)
    {
        if (!isAlive) return false;

        float finalDamage = damage;
        bool isBlocked = false;

        if (defense != null)
        {
            isBlocked = defense.TryBlock(ref finalDamage);
        }

        currentHealth -= finalDamage;
        onDamaged?.Invoke(attacker, damage);

        if (currentHealth <= 0)
        {
            Die();
        }

        return isBlocked;
    }
    void Die()
    {
        isAlive = false;
        NotifyAllAttackers();
        onDie?.Invoke();
    }

    void NotifyAllAttackers()
    {
        foreach (var attacker in currentAttackers)
        {
            if (attacker != null)
            {
                NPCCombat npcCombat = attacker.GetComponent<NPCCombat>();
                if (npcCombat != null)
                {
                    npcCombat.OnTargetDied(gameObject);
                }
            }
        }
        currentAttackers.Clear();
        attackerCount = 0;
    }

    public bool IsEnemy(Team otherTeam)
    {
        if (this.teamCurrent == Team.Neutral || otherTeam == Team.Neutral)
            return false;
        return this.teamCurrent != otherTeam;
    }

    public void AddAttacker(GameObject attacker)
    {
        if (!isAlive) return;

        if (!currentAttackers.Contains(attacker))
        {
            currentAttackers.Add(attacker);
            attackerCount = currentAttackers.Count;
        }
    }
    public void RemoveAttacker(GameObject attacker)
    {
        if (currentAttackers.Contains(attacker))
        {
            currentAttackers.Remove(attacker);
            attackerCount = currentAttackers.Count;
        }
    }
}