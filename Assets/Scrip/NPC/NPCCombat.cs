using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class NPCCombat : MonoBehaviour
{
    [Header("View and Targeting")]
    public float baseDetectionRange = 15f;
    public float chaseRange = 25f;
    public float attackRange = 2f;
    public float scanInterval = 0.5f;

    [Header("Expansion")]
    public float lostTargetTimeToExpand = 3f;
    public float expansionFactor = 1.5f;
    public float maxDetectionRange = 80f;

    [Header("Switch Target")]
    public float switchTargetCooldown = 2f;
    public float outnumberedCooldownMultiplier = 0.5f;
    public float commitLockTime = 1.2f;

    [Header("Scoring (Global)")]
    public float weightAttackerCount = 0.6f;
    public float weightDistance = 0.4f;
    public float hysteresisBonus = 0.15f;
    public float maxExpectedAttackers = 5f;

    [Header("Reactive")]
    public float reactiveBase = 0.5f;
    public float reactiveDamageScale = 0.3f;

    [Header("Fury")]
    public float furyDuration = 3f;

    private NavMeshAgent agent;
    private HealthAndTeam myHealth;
    private Animator anim;

    // target
    private Transform currentTarget;
    private HealthAndTeam currentTargetHealth;

    // timer
    private float lastSwitchTime;
    private float commitTimer;
    private float noTargetTimer;
    private float currentDetectionRange;

    // fury
    private bool isFuryMode;
    private float furyTimer;

    // player
    private GameObject playerObject;
    private HealthAndTeam playerHealth;
    private LayerMask characterLayerMask;

    private List<HealthAndTeam> visibleEnemies = new List<HealthAndTeam>();

    // random
    private float scanOffset;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        myHealth = GetComponent<HealthAndTeam>();
        anim = GetComponentInChildren<Animator>();

        characterLayerMask = LayerMask.GetMask("Character");

        playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            playerHealth = playerObject.GetComponent<HealthAndTeam>();

        myHealth.onDamaged += OnDamaged;

        currentDetectionRange = baseDetectionRange;

        scanOffset = Random.Range(0f, scanInterval);
    }

    void Update()
    {
        HandleFury();

        float scanTime = (Time.time + scanOffset) % scanInterval;
        if (scanTime < Time.deltaTime)
        {
            UpdatePerception();
            HandleDecisionAndAction();
        }

        HandleMovement();
    }

    void UpdatePerception()
    {
        visibleEnemies.Clear();

        // scan nhanh
        Collider[] hits = Physics.OverlapSphere(transform.position, currentDetectionRange, characterLayerMask);
        foreach (var hit in hits)
        {
            HealthAndTeam h = hit.GetComponent<HealthAndTeam>();
            if (IsValidEnemy(h))
                visibleEnemies.Add(h);
        }

        HandleExpansion();
    }

    bool IsValidEnemy(HealthAndTeam h)
    {
        if (h == null) return false;
        if (h == myHealth) return false;
        if (!h.isAlive) return false;
        return IsEnemy(h.teamCurrent);
    }

    bool IsEnemy(Team otherTeam)
    {
        if (myHealth.teamCurrent == Team.Neutral || otherTeam == Team.Neutral)
            return false;
        return myHealth.teamCurrent != otherTeam;
    }

    // Expansion
    void HandleExpansion()
    {
        if (visibleEnemies.Count == 0)
        {
            noTargetTimer += Time.deltaTime;
            if (noTargetTimer >= lostTargetTimeToExpand && currentDetectionRange < maxDetectionRange)
            {
                currentDetectionRange = Mathf.Min(currentDetectionRange * expansionFactor, maxDetectionRange);
                noTargetTimer = 0;
            }
        }
        else
        {
            noTargetTimer = 0;
            currentDetectionRange = baseDetectionRange;
        }
    }

    void HandleDecisionAndAction()
    {
        if (Time.time < commitTimer) return;

        HealthAndTeam best = DecideTarget();

        if (best != null && best != currentTargetHealth)
        {
            float cooldown = GetEffectiveCooldown();
            if (Time.time - lastSwitchTime >= cooldown)
            {
                SwitchToTarget(best);
                commitTimer = Time.time + commitLockTime;
                lastSwitchTime = Time.time;
            }
        }
    }

    HealthAndTeam DecideTarget()
    {
        float bestScore = float.MaxValue;
        HealthAndTeam best = null;

        // Reactive layer
        if (myHealth.currentAttackers.Count > 0)
        {
            Transform lowestAttacker = GetLowestHealthAttacker();
            if (lowestAttacker != null)
            {
                HealthAndTeam h = lowestAttacker.GetComponent<HealthAndTeam>();
                float reactiveScore = ComputeReactiveScore(h);
                best = h;
                bestScore = reactiveScore;
            }
        }

        // Global layer
        foreach (var enemy in visibleEnemies)
        {
            float score = ComputeGlobalScore(enemy);

            // uu tien target cu 1 ti
            if (enemy == currentTargetHealth)
                score -= hysteresisBonus;

            if (score < bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }

        // Fury
        if (isFuryMode && playerHealth != null && playerHealth.isAlive && IsEnemy(playerHealth.teamCurrent))
        {
            float distToPlayer = Vector3.Distance(transform.position, playerObject.transform.position);
            if (distToPlayer <= currentDetectionRange)
            {
                float furyScore = ComputeGlobalScore(playerHealth) - 999f;
                if (furyScore < bestScore)
                    best = playerHealth;
            }
        }

        return best;
    }

    // tinh score (so attacker - khoang cach)
    float ComputeGlobalScore(HealthAndTeam enemy)
    {
        float normAtt = Mathf.Clamp01(enemy.attackerCount / maxExpectedAttackers);
        float dist = Vector3.Distance(transform.position, enemy.transform.position);
        float normDist = Mathf.Clamp01(dist / currentDetectionRange);
        return (normAtt * weightAttackerCount) + (normDist * weightDistance);
    }

    // tinh do yeu duoi
    float ComputeReactiveScore(HealthAndTeam attacker)
    {
        float healthPercent = attacker.currentHealth / attacker.maxHealth;
        float pressure = 1f - healthPercent;
        float reactivePriority = Mathf.Clamp01(reactiveBase + pressure * reactiveDamageScale);
        return 1f - reactivePriority;
    }

    void SwitchToTarget(HealthAndTeam newTarget)
    {
        if (currentTargetHealth != null)
            currentTargetHealth.RemoveAttacker(gameObject);

        currentTarget = newTarget.transform;
        currentTargetHealth = newTarget;
        newTarget.AddAttacker(gameObject);
    }

    void ClearTarget()
    {
        if (currentTargetHealth != null)
            currentTargetHealth.RemoveAttacker(gameObject);

        currentTarget = null;
        currentTargetHealth = null;
        agent.ResetPath();
    }

    void HandleMovement()
    {
        if (currentTarget == null || currentTargetHealth == null || !currentTargetHealth.isAlive)
        {
            agent.ResetPath();
            return;
        }

        float dist = Vector3.Distance(transform.position, currentTarget.position);

        if (dist > chaseRange && !isFuryMode)
        {
            ClearTarget();
            return;
        }

        if (dist > attackRange)
            agent.SetDestination(currentTarget.position);
        else
            agent.ResetPath();
    }

    // fury
    void HandleFury()
    {
        if (!isFuryMode) return;
        furyTimer -= Time.deltaTime;
        if (furyTimer <= 0)
            isFuryMode = false;
    }

    // bi nhieu nguoi danh thi biet chay nhanh hon
    float GetEffectiveCooldown()
    {
        if (myHealth.currentAttackers.Count >= 2)
            return switchTargetCooldown * outnumberedCooldownMultiplier;
        return switchTargetCooldown;
    }

    // tim th yeu nhat
    Transform GetLowestHealthAttacker()
    {
        if (myHealth.currentAttackers.Count == 0) return null;

        float minHealth = float.MaxValue;
        Transform lowest = null;
        foreach (GameObject obj in myHealth.currentAttackers)
        {
            if (obj == null) continue;
            HealthAndTeam h = obj.GetComponent<HealthAndTeam>();
            if (h != null && h.isAlive && h.currentHealth < minHealth)
            {
                minHealth = h.currentHealth;
                lowest = obj.transform;
            }
        }
        return lowest;
    }

    // event
    void OnDamaged(GameObject attacker, float damage)
    {
        if (attacker.CompareTag("Player"))
        {
            isFuryMode = true;
            furyTimer = furyDuration;
        }
    }

    public void OnTargetDied(GameObject deadTarget)
    {
        if (currentTarget != null && currentTarget.gameObject == deadTarget)
            ClearTarget();
    }
}