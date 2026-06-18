using UnityEngine;
using UnityEngine.AI;

public class ArcherCombat : NPCCombat
{
    [Header("Melee")]
    public float meleeRange = 1.5f;
    public float meleeReactionWindow = 1.5f;
    public float meleeCooldown = 5f;

    private float meleeCooldownTimer = 0f;
    private float lastTimeDamaged = -999f;

    [Header("Aiming")]
    public float aimDuration = 1.5f;
    private bool isAiming = false;
    private float aimTimer = 0f;

    private ArcherAnimatorHandler archerAnim;

    protected override void Start()
    {
        base.Start();
        archerAnim = GetComponent<ArcherAnimatorHandler>();

        if (MyHealth != null)
        {
            MyHealth.onDamaged += OnArcherDamaged;
        }
    }

    private void OnDestroy()
    {
        if (MyHealth != null)
        {
            MyHealth.onDamaged -= OnArcherDamaged;
        }
    }

    private void OnArcherDamaged(GameObject attacker, float damage)
    {
        lastTimeDamaged = Time.time;
    }

    protected override void Update()
    {
        if (meleeCooldownTimer > 0)
            meleeCooldownTimer -= Time.deltaTime;

        base.Update();
    }

    protected override void HandleAttack()
    {
        if (CurrentTarget == null || !MyHealth.isAlive) return;

        float dist = Vector3.Distance(transform.position, CurrentTarget.position);

        if (dist <= meleeRange)
        {
            if (Time.time - lastTimeDamaged <= meleeReactionWindow && meleeCooldownTimer <= 0)
            {
                isAiming = false;
                archerAnim.SetAiming(false);

                archerAnim.PlayMeleeAttack();
                meleeCooldownTimer = meleeCooldown;
                attackCooldownTimer = attackCooldownDuration; // cho chac
                return;
            }
        }

        // ban cung
        if (attackCooldownTimer > 0)
        {
            attackCooldownTimer -= Time.deltaTime;
            return;
        }

        if (dist <= attackRange)
        {
            if (!isAiming)
            {
                isAiming = true;
                aimTimer = aimDuration;
                archerAnim.SetAiming(true);
            }
            else
            {
                aimTimer -= Time.deltaTime;
                if (aimTimer <= 0)
                {
                    archerAnim.PlayRangedAttack();
                    attackCooldownTimer = attackCooldownDuration;
                    isAiming = false;
                    archerAnim.SetAiming(false);
                }
            }
        }
        else
        {
            if (isAiming)
            {
                isAiming = false;
                archerAnim.SetAiming(false);
            }
        }
    }

    protected override void HandleMovement()
    {
        if (archerAnim != null && archerAnim.IsUsingRootMotion) return;

        if (controller.isGrounded) verticalVelocity = -2f;
        else verticalVelocity -= 9.81f * Time.deltaTime;

        if (CurrentTarget == null || !MyHealth.isAlive)
        {
            agent.ResetPath();
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
            UpdateAnimatorBlendTree();
            return;
        }

        float dist = Vector3.Distance(transform.position, CurrentTarget.position);
        Vector3 dirToTarget = (CurrentTarget.position - transform.position);
        dirToTarget.y = 0;

        if (dist < kiteRange)
        {
            Vector3 fleeDirection = -dirToTarget.normalized;
            Vector3 fleePos = transform.position + fleeDirection * 5f;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(fleePos, out hit, 5f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
            else
            {
                agent.SetDestination(fleePos);
            }

            if (isAiming)
            {
                agent.speed = walkSpeed;
                LookAtTarget(dirToTarget);
            }
            else
            {
                agent.speed = runSpeed;
                LookAtTarget(agent.desiredVelocity);
            }
        }
        else if (dist > attackRange)
        {
            agent.SetDestination(CurrentTarget.position);
            agent.speed = isAiming ? walkSpeed : runSpeed;

            if (isAiming) LookAtTarget(dirToTarget);
            else LookAtTarget(agent.desiredVelocity);
        }
        else
        {
            agent.ResetPath();
            LookAtTarget(dirToTarget);
        }

        Vector3 finalVelocity = agent.desiredVelocity;
        finalVelocity.y = verticalVelocity;
        controller.Move(finalVelocity * Time.deltaTime);
        agent.nextPosition = transform.position;

        UpdateAnimatorBlendTree();
    }

    private void LookAtTarget(Vector3 lookDirection)
    {
        lookDirection.y = 0;
        if (lookDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
    }

    private void UpdateAnimatorBlendTree()
    {
        if (archerAnim == null) return;

        Vector3 localVelocity = transform.InverseTransformDirection(controller.velocity);

        float speedNormalizedX = localVelocity.x / runSpeed;
        float speedNormalizedZ = localVelocity.z / runSpeed;

        archerAnim.UpdateBlendTree(speedNormalizedX, speedNormalizedZ);
    }
}
