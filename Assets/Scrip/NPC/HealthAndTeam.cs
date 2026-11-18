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

    // Threat tracking
    public Dictionary<GameObject, float> threatMap = new Dictionary<GameObject, float>();
    public float threatDecayRate = 5f;

    public System.Action<GameObject, float> onDamaged;
    public System.Action onDie;

    private ActiveDefense defense;

    void Awake()
    {
        currentHealth = maxHealth;
        defense = GetComponent<ActiveDefense>();
    }

    private float threatDecayTimer = 0f;
    private static readonly List<GameObject> reusableThreatKeys = new List<GameObject>(32);

    void Update()
    {
        if (threatMap.Count > 0)
        {
            threatDecayTimer -= Time.deltaTime;
            if (threatDecayTimer <= 0f)
            {
                threatDecayTimer = 1.0f;
                DecayThreats(1.0f);
            }
        }
    }
    public void ChangeTeam(Team newTeam)
    {
        if (teamCurrent == newTeam) return;

        teamCurrent = newTeam;
    }
    public bool TakeDamage(float damage, GameObject attacker)
    {
        if (!isAlive) return false;

        Animator anim = GetComponent<Animator>();
        if (anim != null && anim.GetCurrentAnimatorStateInfo(0).IsTag("Roll"))
        {
            return false;
        }

        // Bỏ qua sát thương từ đồng đội (chống Friendly Fire)
        if (attacker != null && attacker != gameObject)
        {
            HealthAndTeam attackerHealth = attacker.GetComponentInParent<HealthAndTeam>();
            if (attackerHealth != null && !IsEnemy(attackerHealth.teamCurrent))
            {
                return false;
            }
        }

        float finalDamage = damage;
        bool isBlocked = false;

        if (defense != null)
        {
            isBlocked = defense.TryBlock(ref finalDamage);
        }

        currentHealth -= finalDamage;

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
            return isBlocked;
        }

        if (finalDamage > 0)
        {
            AddThreat(attacker, finalDamage);
            if (attacker != null)
            {
                AddAttacker(attacker);
            }
        }
        onDamaged?.Invoke(attacker, finalDamage);

        if (attacker != null && (attacker.CompareTag("Player") || attacker.GetComponent<PlayerMovement>() != null))
        {
            if (BossHUD.Instance != null && gameObject != attacker)
            {
                BossHUD.Instance.SetTarget(this);
            }
        }

        return isBlocked;
    }
    void Die()
    {
        if (!isAlive) return;
        isAlive = false;
        onDie?.Invoke();
        NotifyAllAttackers();
    }

    void NotifyAllAttackers()
    {
        var attackersCopy = currentAttackers.ToArray();
        foreach (var attacker in attackersCopy)
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

    // Threat methods
    public void AddThreat(GameObject source, float amount)
    {
        if (source == null) return;
        if (threatMap.ContainsKey(source))
            threatMap[source] += amount;
        else
            threatMap[source] = amount;
    }

    public float GetThreat(GameObject source)
    {
        if (source != null && threatMap.ContainsKey(source))
            return threatMap[source];
        return 0f;
    }

    void DecayThreats(float elapsed)
    {
        if (threatMap.Count == 0) return;

        reusableThreatKeys.Clear();
        foreach (var kvp in threatMap)
        {
            reusableThreatKeys.Add(kvp.Key);
        }

        for (int i = 0; i < reusableThreatKeys.Count; i++)
        {
            GameObject key = reusableThreatKeys[i];
            if (key == null) { threatMap.Remove(key); continue; }
            threatMap[key] -= threatDecayRate * elapsed;
            if (threatMap[key] <= 0f)
                threatMap.Remove(key);
        }
    }

    public void CleanDeadAttackers()
    {
        for (int i = currentAttackers.Count - 1; i >= 0; i--)
        {
            if (currentAttackers[i] == null)
            {
                currentAttackers.RemoveAt(i);
                continue;
            }
            HealthAndTeam h = currentAttackers[i].GetComponentInParent<HealthAndTeam>();
            if (h != null && !h.isAlive)
            {
                currentAttackers.RemoveAt(i);
            }
        }
        attackerCount = currentAttackers.Count;
    }

    public int GetMeleeAttackerCount()
    {
        CleanDeadAttackers();
        int count = 0;
        for (int i = 0; i < currentAttackers.Count; i++)
        {
            if (currentAttackers[i] == null) continue;
            if (currentAttackers[i].GetComponentInParent<ArcherCombat>() == null)
            {
                count++;
            }
        }
        return count;
    }

    public int GetTotalAttackerCount()
    {
        CleanDeadAttackers();
        return currentAttackers.Count;
    }

    public void AddAttacker(GameObject attacker)
    {
        if (!isAlive || attacker == null || attacker == gameObject) return;

        HealthAndTeam attackerHealth = attacker.GetComponentInParent<HealthAndTeam>();
        if (attackerHealth != null && !IsEnemy(attackerHealth.teamCurrent)) return;

        CleanDeadAttackers();

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