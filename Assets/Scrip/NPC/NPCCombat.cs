using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class NPCCombat : MonoBehaviour
{
    [Header("View and Targeting")]
    public float baseDetectionRange = 15f;
    public float chaseRange = 25f;
    public float attackRange = 1.5f;
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

    [Header("Animation Settings")]
    public Animator anim;
    public float walkSpeed = 2.5f;
    public float runSpeed = 5.0f;
    public float runDistanceRatio = 1.5f;
    public float attackCooldownDuration = 2f;
    public float rotationSpeed = 5f;
    public float attackAngle = 30f;
    public float attackCooldownTimer = 0f;
    private CharacterController controller;
    private float verticalVelocity;

    private NavMeshAgent agent;
    private HealthAndTeam myHealth;

    // target
    private Transform currentTarget;
    private HealthAndTeam currentTargetHealth;
    private bool ignorePlayerPriority;

    // timer & range
    private float lastSwitchTime;
    private float commitTimer;
    private float noTargetTimer;
    private float currentDetectionRange;
    private float currentChaseRange;
    private float ignorePlayerPriorityTimer;
    public float ignoreDuration = 3f;

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
        controller = GetComponent<CharacterController>();
        myHealth = GetComponent<HealthAndTeam>();
        anim = GetComponentInChildren<Animator>();

        characterLayerMask = LayerMask.GetMask("Character");

        playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            playerHealth = playerObject.GetComponent<HealthAndTeam>();

        myHealth.onDamaged += OnDamaged;
        myHealth.onDie += OnDie;

        currentDetectionRange = baseDetectionRange;
        currentChaseRange = chaseRange;
        agent.speed = walkSpeed;
        agent.updateRotation = false;
        agent.updatePosition = false;

        scanOffset = Random.Range(0f, scanInterval);
    }

    void Update()
    {
        HandleFury();
        HandleAnimationRootMotion();
        UpdateAnimationSpeed();
        HandleAttack();

        float scanTime = (Time.time + scanOffset) % scanInterval;
        if (scanTime < Time.deltaTime)
        {
            UpdatePerception();
            HandleDecisionAndAction();
        }

        HandleMovement();

        if (ignorePlayerPriority)
        {
            ignorePlayerPriorityTimer -= Time.deltaTime;

            if (ignorePlayerPriorityTimer <= 0)
            {
                ignorePlayerPriority = false;
            }
        }

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
        return myHealth.IsEnemy(h.teamCurrent);
    }

    // Expansion
    void HandleExpansion()
    {
        bool hasValidTarget = currentTargetHealth != null && currentTargetHealth.isAlive;

        if (hasValidTarget)
        {
            if (currentDetectionRange > baseDetectionRange && Vector3.Distance(transform.position, currentTarget.position) <= chaseRange)
            {
                currentDetectionRange = baseDetectionRange;
                currentChaseRange = chaseRange;
            }
            return;
        }

        if (visibleEnemies.Count == 0)
        {
            noTargetTimer += scanInterval;
            if (noTargetTimer >= lostTargetTimeToExpand && currentDetectionRange < maxDetectionRange)
            {
                currentDetectionRange = Mathf.Min(currentDetectionRange * expansionFactor, maxDetectionRange);
                currentChaseRange = Mathf.Min(currentChaseRange * expansionFactor, maxDetectionRange);
                noTargetTimer = 0;
            }
        }
        // tu tu roi reset
        else
        {
            noTargetTimer = 0;
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
        if (isFuryMode && playerHealth != null && playerHealth.isAlive && myHealth.IsEnemy(playerHealth.teamCurrent))
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

        float score =
        (normAtt * weightAttackerCount) +
        (normDist * weightDistance);

        if (!ignorePlayerPriority && enemy.CompareTag("Player"))
        {
            score -= 999f;
        }

        return score;
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
        agent.nextPosition = transform.position;
    }

    void HandleMovement()
    {
        if (anim != null && anim.applyRootMotion) return;

        if (controller.isGrounded)
            verticalVelocity = -2f;
        else
            verticalVelocity -= 9.81f * Time.deltaTime;

        if (currentTarget == null || currentTargetHealth == null || !currentTargetHealth.isAlive)
        {
            agent.ResetPath();
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
            agent.nextPosition = transform.position;
            return;
        }

        float dist = Vector3.Distance(transform.position, currentTarget.position);

        if (dist > currentChaseRange && !isFuryMode)
        {
            ClearTarget();
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
            agent.nextPosition = transform.position;
            return;
        }

        if (dist > attackRange)
        {
            agent.SetDestination(currentTarget.position);

            Vector3 desiredVelocity = agent.desiredVelocity;
            desiredVelocity.y = verticalVelocity;
            controller.Move(desiredVelocity * Time.deltaTime);

            // vua xoay vua di
            Vector3 moveDir = agent.desiredVelocity;
            moveDir.y = 0;
            if (moveDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }
        else
        {
            // dung yen quay mat
            agent.ResetPath();
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);

            Vector3 dirToTarget = (currentTarget.position - transform.position);
            dirToTarget.y = 0;
            if (dirToTarget.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dirToTarget.normalized);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }

        // thong bao cho agent
        agent.nextPosition = transform.position;
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
        else
        {
            ignorePlayerPriority = true;
            ignorePlayerPriorityTimer = ignoreDuration;
        }
    }

    public void OnTargetDied(GameObject deadTarget)
    {
        if (currentTarget != null && currentTarget.gameObject == deadTarget)
            ClearTarget();
    }

    public void OnDie()
    {
        if (anim != null)
        {
            int deadIndex = Random.Range(0, 3);
            anim.SetInteger("DeadIndex", deadIndex);
            anim.SetTrigger("DoDead");
        }
        agent.isStopped = true;
        enabled = false;
    }

    void HandleAnimationRootMotion()
    {
        if (anim == null) return;

        AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo next = anim.GetNextAnimatorStateInfo(0);

        bool isUsingRootMotion =
            state.IsTag("Combo") ||
            state.IsTag("Dead") ||
            state.IsTag("Impact") ||
            (anim.IsInTransition(0) &&
                (next.IsTag("Combo") || next.IsTag("Dead") || next.IsTag("Impact")));

        anim.applyRootMotion = isUsingRootMotion;

        if (isUsingRootMotion)
        {
            agent.ResetPath();
        }

        agent.nextPosition = transform.position;
    }


    void UpdateAnimationSpeed()
    {
        if (anim == null) return;
        if (anim.applyRootMotion)
        {
            anim.SetFloat("Speed", 0f);
            return;
        }

        if (currentTarget == null || currentTargetHealth == null || !currentTargetHealth.isAlive)
        {
            anim.SetFloat("Speed", 0f);
            return;
        }

        float distToTarget = Vector3.Distance(transform.position, currentTarget.position);
        float runThreshold = attackRange * runDistanceRatio;
        float targetAgentSpeed = (distToTarget > runThreshold) ? runSpeed : walkSpeed;

        if (Mathf.Abs(agent.speed - targetAgentSpeed) > 0.01f)
            agent.speed = targetAgentSpeed;

        float animSpeed = controller.velocity.magnitude / runSpeed;
        anim.SetFloat("Speed", Mathf.Clamp01(animSpeed));
    }

    void HandleAttack()
    {
        if (attackCooldownTimer > 0)
        {
            attackCooldownTimer -= Time.deltaTime;
            return;
        }

        if (currentTarget == null || currentTargetHealth == null || !currentTargetHealth.isAlive) return;

        float dist = Vector3.Distance(transform.position, currentTarget.position);
        if (dist <= attackRange)
        {
            Vector3 direction = (currentTarget.position - transform.position).normalized;
            direction.y = 0;

            if (direction != Vector3.zero)
            {
                float angleToTarget = Vector3.Angle(transform.forward, direction);

                if (angleToTarget <= attackAngle)
                {
                    int attackIndex = Random.Range(0, 5);
                    anim.SetInteger("AttackIndex", attackIndex);
                    anim.SetTrigger("DoAttack");

                    attackCooldownTimer = attackCooldownDuration;
                }
            }
        }
    }

    void OnAnimatorMove()
    {
        if (anim == null || !anim.applyRootMotion) return;

        Vector3 delta = anim.deltaPosition;
        delta.y += verticalVelocity * Time.deltaTime;

        controller.Move(delta);
        transform.rotation *= anim.deltaRotation;

        agent.nextPosition = transform.position;
    }

}