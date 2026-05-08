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

    public int attackerCount = 0;
    public List<GameObject> currentAttackers = new List<GameObject>();

    public bool isAlive = true;
    public System.Action<GameObject, float> onDamaged;
    public System.Action onDied;

    void Awake()
    {
        currentHealth = maxHealth;
    }
    public void ChangeTeam(Team newTeam)
    {
        if (teamCurrent == newTeam) return;

        teamCurrent = newTeam;
    }
    public void TakeDamage(float damage, GameObject attacker)
    {
        if (!isAlive) return;

        currentHealth -= damage;
        onDamaged?.Invoke(attacker, damage);

        if (currentHealth <= 0)
        {
            Die();
        }
    }
    void Die()
    {
        isAlive = false;
        NotifyAllAttackers();
        onDied?.Invoke();
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