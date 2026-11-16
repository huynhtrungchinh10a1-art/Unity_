using System.Collections.Generic;
using UnityEngine;

public class ArcherAnimatorHandler : NPCAnimatorHandler
{
    [Header("Bow Reference")]
    public Animator bowAnim;

    [Header("Arrow Settings")]
    public GameObject visualArrow;          
    public GameObject arrowPrefab;          
    public Transform arrowSpawnPoint;       

    [Header("Ammo Settings")]
    public int shotsPerArrow = 30;
    public bool autoRefillWhenEmpty = false;

    [Header("Quiver Settings")]
    public Transform quiverContainer;
    public string arrowTag = "QuiverArrow";

    [HideInInspector]
    public List<GameObject> quiverArrows = new List<GameObject>();

    [Header("High-Arc Volley Skill (Mưa Tên 70 độ)")]
    public float highSkillAngle = 70f;
    public bool isAimingHighSkill = false;
    public int volleyArrowCount = 4;
    public float highVolleyGravity = 18f;

    private int currentShotCount = 0;

    public bool IsMeleeAttacking { get; private set; } = false;

    protected override void Awake()
    {
        base.Awake();
        InitQuiverArrows();
    }

    public override void UpdateRootMotionState()
    {
        base.UpdateRootMotionState();

        if (IsMeleeAttacking)
        {
            IsUsingRootMotion = true;
            if (anim != null) anim.applyRootMotion = true;
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.velocity = Vector3.zero;
                agent.nextPosition = transform.position;
            }
        }
    }

    public void InitQuiverArrows()
    {
        if (quiverArrows.Count > 0 || quiverContainer == null || string.IsNullOrEmpty(arrowTag)) return;

        quiverArrows.Clear();
        Transform[] allChildren = quiverContainer.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in allChildren)
        {
            if (child == quiverContainer) continue;

            if (child.CompareTag(arrowTag))
            {
                quiverArrows.Add(child.gameObject);
            }
        }
    }

    public bool HasArrow()
    {
        if (quiverArrows == null || quiverArrows.Count == 0) return true;

        for (int i = 0; i < quiverArrows.Count; i++)
        {
            if (quiverArrows[i] != null && quiverArrows[i].activeSelf)
                return true;
        }
        return false;
    }

    private void LateUpdate()
    {
        // Khi đang rút tên ngắm bắn mưa tên góc cao (70 độ):
        // Xoay Spine và Chest ngửa lên trên để dáng bắn giương cung thẳng đứng lên trời cực kỳ hoành tráng
        if (anim != null && anim.GetBool("isAiming") && isAimingHighSkill)
        {
            Transform spine = anim.GetBoneTransform(HumanBodyBones.Spine);
            Transform chest = anim.GetBoneTransform(HumanBodyBones.Chest);

            float tiltAngle = Mathf.Clamp(highSkillAngle - 10f, 40f, 75f);
            Quaternion tiltSpine = Quaternion.AngleAxis(-tiltAngle * 0.5f, transform.right);
            Quaternion tiltChest = Quaternion.AngleAxis(-tiltAngle * 0.5f, transform.right);

            if (spine != null) spine.rotation = tiltSpine * spine.rotation;
            if (chest != null) chest.rotation = tiltChest * chest.rotation;
        }
    }

    public void SetAiming(bool isAiming, bool isHighSkill = false)
    {
        if (isAiming && !HasArrow())
        {
            isAiming = false;
        }

        isAimingHighSkill = isAiming && isHighSkill;

        if (anim != null)
            anim.SetBool("isAiming", isAiming);

        if (bowAnim != null)
            bowAnim.SetBool("isPulling", isAiming);

        if (visualArrow != null)
            visualArrow.SetActive(isAiming);
    }

    public void PlayRangedAttack(int testTrajectoryMode = 0)
    {
        if (!HasArrow())
        {
            SetAiming(false);
            return;
        }

        bool wasHighSkill = isAimingHighSkill;

        if (anim != null)
        {
            anim.SetTrigger("DoShoot");
            anim.SetBool("isAiming", false);
        }

        if (bowAnim != null)
        {
            bowAnim.SetTrigger("DoRelease");
            bowAnim.SetBool("isPulling", false);
        }

        if (visualArrow != null)
            visualArrow.SetActive(false);

        int firedCount = 1;
        if (wasHighSkill)
        {
            SpawnHighVolleyArrows();
            firedCount = volleyArrowCount;
        }
        else
        {
            SpawnArrowProjectile(testTrajectoryMode);
        }

        isAimingHighSkill = false;
        ConsumeQuiverArrow(firedCount);
    }

    private void ConsumeQuiverArrow(int amount = 1)
    {
        if (quiverArrows == null || quiverArrows.Count == 0) return;

        currentShotCount += amount;
        int requiredShots = Mathf.Max(1, shotsPerArrow);

        while (currentShotCount >= requiredShots)
        {
            currentShotCount -= requiredShots;

            for (int i = quiverArrows.Count - 1; i >= 0; i--)
            {
                if (quiverArrows[i] != null && quiverArrows[i].activeSelf)
                {
                    quiverArrows[i].SetActive(false);
                    break;
                }
            }

            if (!HasArrow())
            {
                if (autoRefillWhenEmpty)
                {
                    RefillQuiver();
                }
                break;
            }
        }
    }

    public void RefillQuiver()
    {
        currentShotCount = 0;
        if (quiverArrows == null) return;
        foreach (var arrow in quiverArrows)
        {
            if (arrow != null) arrow.SetActive(true);
        }
    }

    private void SpawnArrowProjectile(int testTrajectoryMode = 0)
    {
        if (arrowPrefab == null) return;

        Vector3 spawnPos = arrowSpawnPoint != null ? arrowSpawnPoint.position : (transform.position + Vector3.up * 1.4f + transform.forward * 0.5f);
        Vector3 shootDirection = transform.forward;

        if (testTrajectoryMode == 1)
        {
            shootDirection = transform.forward;
        }
        else if (testTrajectoryMode == 2)
        {
            shootDirection = (transform.forward + Vector3.up * 0.6f).normalized;
        }
        else
        {
            NPCCombat combat = GetComponent<NPCCombat>();
            if (combat != null && combat.CurrentTarget != null)
            {
                // Lấy điểm ngắm ngẫu nhiên phân bố đều khắp các bộ phận trên cơ thể mục tiêu (Ngực, Eo, Bụng, Tay, Đùi)
                Vector3 targetPoint = combat.CurrentTarget.position + Vector3.up * 1.0f;
                CharacterController cc = combat.CurrentTarget.GetComponent<CharacterController>();
                Animator targetAnim = combat.CurrentTarget.GetComponentInChildren<Animator>();
                bool foundTargetBone = false;

                if (cc != null)
                {
                    Vector3 center = cc.bounds.center;
                    Vector3 extents = cc.bounds.extents;
                    Vector3 bodyRight = combat.CurrentTarget.right;

                    if (targetAnim != null && targetAnim.isHuman)
                    {
                        HumanBodyBones[] candidateBones = new HumanBodyBones[]
                        {
                            HumanBodyBones.Head,
                            HumanBodyBones.Chest,
                            HumanBodyBones.Chest,
                            HumanBodyBones.Spine,
                            HumanBodyBones.Spine,
                            HumanBodyBones.Hips,
                            HumanBodyBones.LeftUpperArm,
                            HumanBodyBones.RightUpperArm,
                            HumanBodyBones.LeftUpperLeg,
                            HumanBodyBones.RightUpperLeg
                        };

                        HumanBodyBones chosen = candidateBones[Random.Range(0, candidateBones.Length)];
                        Transform boneT = targetAnim.GetBoneTransform(chosen);
                        if (boneT != null)
                        {
                            Vector3 bonePos = boneT.position + Random.insideUnitSphere * 0.04f;
                            float topClampRatio = (chosen == HumanBodyBones.Head) ? 0.9f : 0.7f;
                            float safeY = Mathf.Clamp(bonePos.y, center.y - extents.y * 0.55f, center.y + extents.y * topClampRatio);

                            Vector3 toBoneXZ = new Vector3(bonePos.x - center.x, 0f, bonePos.z - center.z);
                            float maxSafeRadius = extents.x * 0.8f;
                            if (toBoneXZ.magnitude > maxSafeRadius)
                            {
                                toBoneXZ = toBoneXZ.normalized * maxSafeRadius;
                            }

                            targetPoint = new Vector3(center.x + toBoneXZ.x, safeY, center.z + toBoneXZ.z);
                            foundTargetBone = true;
                        }
                    }

                    if (!foundTargetBone)
                    {
                        float randX = Random.Range(-0.4f, 0.4f) * extents.x;
                        float randY = Random.Range(-0.4f, 0.85f) * extents.y;
                        targetPoint = center + bodyRight * randX + Vector3.up * randY;
                    }
                }
                else
                {
                    Collider col = combat.CurrentTarget.GetComponent<Collider>();
                    if (col != null)
                    {
                        targetPoint = col.bounds.center + Random.insideUnitSphere * 0.2f;
                    }
                }

                float speed = 35f;
                float gravity = 6f;

                ArrowProjectile projComponent = arrowPrefab.GetComponent<ArrowProjectile>();
                if (projComponent != null)
                {
                    speed = projComponent.speed;
                    gravity = projComponent.gravity;
                }

                // Thu thập vận tốc đối thủ để bắn đón đầu (Lead Target) cho cả đòn bắn thường
                Vector3 targetVel = Vector3.zero;
                CharacterController targetCC = combat.CurrentTarget.GetComponent<CharacterController>();
                if (targetCC != null && targetCC.velocity.sqrMagnitude > 0.01f)
                {
                    targetVel = targetCC.velocity;
                }
                else
                {
                    UnityEngine.AI.NavMeshAgent targetAgent = combat.CurrentTarget.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (targetAgent != null && targetAgent.velocity.sqrMagnitude > 0.01f)
                    {
                        targetVel = targetAgent.velocity;
                    }
                }
                targetVel.y = 0;

                // Ước tính thời gian bay sơ bộ: t ~ dist / speed
                Vector3 rawToTargetXZ = new Vector3(targetPoint.x - spawnPos.x, 0f, targetPoint.z - spawnPos.z);
                float flightTime = Mathf.Clamp(rawToTargetXZ.magnitude / speed, 0f, 2.0f);

                // Điểm ngắm đón đầu vị trí đối thủ
                Vector3 predictedTarget = targetPoint + targetVel * flightTime;

                // Tính toán công thức ném xiên chuẩn xác tuyệt đối (Closed-form Ballistic)
                Vector3 toTarget = predictedTarget - spawnPos;
                Vector3 toTargetXZ = new Vector3(toTarget.x, 0f, toTarget.z);
                float distXZ = toTargetXZ.magnitude;
                float diffY = toTarget.y;

                float v2 = speed * speed;
                float v4 = v2 * v2;
                float gx = gravity * distXZ;
                float underRoot = v4 - gravity * (gravity * distXZ * distXZ + 2f * diffY * v2);

                if (underRoot >= 0f && gx > 0.001f)
                {
                    float tanTheta = (v2 - Mathf.Sqrt(underRoot)) / gx;
                    float angle = Mathf.Atan(tanTheta);

                    Vector3 dirXZ = toTargetXZ.normalized;
                    shootDirection = (dirXZ * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle)).normalized;
                }
                else
                {
                    shootDirection = (toTargetXZ.normalized + Vector3.up).normalized;
                }
            }
        }

        Quaternion spawnRot = Quaternion.LookRotation(shootDirection);

        GameObject arrowObj = Instantiate(arrowPrefab, spawnPos, spawnRot);
        ArrowProjectile proj = arrowObj.GetComponent<ArrowProjectile>();
        if (proj != null)
        {
            proj.Init(gameObject);
        }
    }

    private void SpawnHighVolleyArrows()
    {
        if (arrowPrefab == null) return;

        NPCCombat combat = GetComponent<NPCCombat>();
        if (combat == null || combat.CurrentTarget == null)
        {
            SpawnArrowProjectile(0);
            return;
        }

        Transform targetT = combat.CurrentTarget;
        Vector3 spawnPos = arrowSpawnPoint != null ? arrowSpawnPoint.position : (transform.position + Vector3.up * 1.4f + transform.forward * 0.5f);

        // Vận tốc của đối thủ để bắn đón đầu (lead target)
        Vector3 targetVel = Vector3.zero;
        CharacterController targetCC = targetT.GetComponent<CharacterController>();
        if (targetCC != null && targetCC.velocity.sqrMagnitude > 0.01f)
        {
            targetVel = targetCC.velocity;
        }
        else
        {
            UnityEngine.AI.NavMeshAgent targetAgent = targetT.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (targetAgent != null && targetAgent.velocity.sqrMagnitude > 0.01f)
            {
                targetVel = targetAgent.velocity;
            }
        }
        targetVel.y = 0;

        // Chọn 4 bộ phận xương/khu vực khác nhau của đối thủ
        List<Vector3> targetPoints = GetVolleyTargetPoints(targetT);

        int count = Mathf.Min(volleyArrowCount, targetPoints.Count);
        for (int i = 0; i < count; i++)
        {
            Vector3 rawTargetPos = targetPoints[i];

            // Vị trí xuất phát hơi dàn nhẹ sang 2 bên để 4 mũi tên không bị đè trùng khít
            Vector3 arrowSpawn = spawnPos + (transform.right * (i - 1.5f) * 0.08f) + (transform.up * ((i % 2) - 0.5f) * 0.05f);

            // Góc bắn cao cỡ 70 độ (lệch nhẹ +- 1.5 độ giữa các mũi tên)
            float launchAngle = (highSkillAngle + Random.Range(-1.5f, 1.5f)) * Mathf.Deg2Rad;
            float cosTheta = Mathf.Cos(launchAngle);
            float sinTheta = Mathf.Sin(launchAngle);
            float tanTheta = Mathf.Tan(launchAngle);

            float g = highVolleyGravity;

            // Tính toán khoảng cách ban đầu để ước tính thời gian bay
            Vector3 toTargetXZ = new Vector3(rawTargetPos.x - arrowSpawn.x, 0f, rawTargetPos.z - arrowSpawn.z);
            float distXZ = Mathf.Max(0.5f, toTargetXZ.magnitude);
            float diffY = rawTargetPos.y - arrowSpawn.y;

            // Ước tính thời gian bay t
            float denomRough = Mathf.Max(0.1f, distXZ * tanTheta - diffY);
            float roughV2 = (g * distXZ * distXZ) / (2f * cosTheta * cosTheta * denomRough);
            float roughV = Mathf.Sqrt(Mathf.Max(1f, roughV2));
            float flightTime = Mathf.Clamp(distXZ / (roughV * cosTheta), 0.3f, 4.0f);

            // Bắn đón đầu theo hướng chạy của đối thủ
            Vector3 predictedTarget = rawTargetPos + targetVel * flightTime;

            Vector3 finalToTarget = predictedTarget - arrowSpawn;
            Vector3 finalToTargetXZ = new Vector3(finalToTarget.x, 0f, finalToTarget.z);
            float finalDistXZ = Mathf.Max(0.5f, finalToTargetXZ.magnitude);
            float finalDiffY = finalToTarget.y;

            float denom = finalDistXZ * tanTheta - finalDiffY;
            if (denom <= 0.01f) denom = 0.01f;

            // Công thức tính tốc độ chuẩn xác tuyệt đối để mũi tên bay góc 70 độ trúng đích
            float v2 = (g * finalDistXZ * finalDistXZ) / (2f * cosTheta * cosTheta * denom);
            float calculatedSpeed = Mathf.Sqrt(Mathf.Clamp(v2, 20f, 2500f));

            Vector3 dirXZ = finalToTargetXZ.normalized;
            Vector3 shootDir = (dirXZ * cosTheta + Vector3.up * sinTheta).normalized;
            Quaternion spawnRot = Quaternion.LookRotation(shootDir);

            GameObject arrowObj = Instantiate(arrowPrefab, arrowSpawn, spawnRot);
            ArrowProjectile proj = arrowObj.GetComponent<ArrowProjectile>();
            if (proj != null)
            {
                proj.Init(gameObject, -1f, calculatedSpeed, g);
            }
        }
    }

    private List<Vector3> GetVolleyTargetPoints(Transform targetT)
    {
        List<Vector3> points = new List<Vector3>();
        Animator targetAnim = targetT.GetComponentInChildren<Animator>();
        CharacterController cc = targetT.GetComponent<CharacterController>();
        Vector3 center = cc != null ? cc.bounds.center : (targetT.position + Vector3.up * 1.0f);
        Vector3 right = targetT.right;

        if (targetAnim != null && targetAnim.isHuman)
        {
            // Danh sách toàn bộ các xương tiềm năng trên cơ thể đối thủ
            HumanBodyBones[] allCandidateBones = new HumanBodyBones[]
            {
                HumanBodyBones.Head,
                HumanBodyBones.Chest,
                HumanBodyBones.Spine,
                HumanBodyBones.Hips,
                HumanBodyBones.LeftUpperArm,
                HumanBodyBones.LeftLowerArm,
                HumanBodyBones.RightUpperArm,
                HumanBodyBones.RightLowerArm,
                HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.RightUpperLeg,
                HumanBodyBones.RightLowerLeg,
                HumanBodyBones.LeftShoulder,
                HumanBodyBones.RightShoulder
            };

            // Lọc các xương tồn tại thực tế và không trùng nhau
            List<Transform> availableBones = new List<Transform>();
            foreach (var b in allCandidateBones)
            {
                Transform boneT = targetAnim.GetBoneTransform(b);
                if (boneT != null && !availableBones.Contains(boneT))
                {
                    availableBones.Add(boneT);
                }
            }

            // Xáo trộn ngẫu nhiên danh sách xương (Fisher-Yates Shuffle) để mỗi lần bắn ra 4 xương hoàn toàn ngẫu nhiên
            for (int i = 0; i < availableBones.Count; i++)
            {
                int randIdx = Random.Range(i, availableBones.Count);
                Transform temp = availableBones[i];
                availableBones[i] = availableBones[randIdx];
                availableBones[randIdx] = temp;
            }

            // Chọn 4 xương ngẫu nhiên khác biệt
            int takeCount = Mathf.Min(4, availableBones.Count);
            for (int i = 0; i < takeCount; i++)
            {
                points.Add(availableBones[i].position + Random.insideUnitSphere * 0.04f);
            }

            // Fallback nếu thiếu xương
            while (points.Count < 4)
            {
                points.Add(center + Random.insideUnitSphere * 0.35f);
            }
        }
        else
        {
            // Nếu không phải Humanoid, chia đều 4 điểm quanh trọng tâm kèm độ lệch ngẫu nhiên
            points.Add(center + Vector3.up * 0.45f + Random.insideUnitSphere * 0.06f);
            points.Add(center - right * 0.28f + Vector3.up * 0.1f + Random.insideUnitSphere * 0.06f);
            points.Add(center + right * 0.28f + Vector3.up * 0.1f + Random.insideUnitSphere * 0.06f);
            points.Add(center - Vector3.up * 0.35f + Random.insideUnitSphere * 0.06f);
        }

        return points;
    }

    public override void PlayBlock(bool isBlocking)
    {
        IsMeleeAttacking = false;

        SetAiming(false);
        if (visualArrow != null)
            visualArrow.SetActive(false);

        if (anim == null) return;
        anim.SetTrigger("DoBlock");
        anim.SetBool("IsBlocked", isBlocking);
        if (isBlocking)
        {
            anim.ResetTrigger("DoShoot");
            anim.ResetTrigger("DoMelee");
        }
    }

    public override void PlayRoll()
    {
        IsMeleeAttacking = false;

        SetAiming(false);
        if (visualArrow != null)
            visualArrow.SetActive(false);

        base.PlayRoll();
    }

    public override void PlayAttack(int attackIndex)
    {
        PlayMeleeAttack();
    }

    public void PlayMeleeAttack()
    {
        SetAiming(false);
        if (visualArrow != null)
            visualArrow.SetActive(false);

        if (anim == null) return;

        // Snap xoay mặt tức thì về phía mục tiêu ngay tại thời điểm bắt đầu đòn đánh
        if (combat != null && combat.CurrentTarget != null)
        {
            Vector3 dir = (combat.CurrentTarget.position - transform.position);
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        anim.SetInteger("AttackIndex", Random.Range(0, 2));
        anim.SetTrigger("DoMelee"); 

        // Khóa đòn melee: Giữ hướng nhìn vào mục tiêu và kích hoạt Root Motion
        IsMeleeAttacking = true;

        IsUsingRootMotion = true;
        anim.applyRootMotion = true;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.nextPosition = transform.position;
        }
    }

    public override void ResetAttack()
    {
        IsMeleeAttacking = false;

        if (anim == null) return;
        anim.ResetTrigger("DoShoot");
        anim.ResetTrigger("DoMelee");
    }

    public override void UpdateLocomotion(float speed)
    {
        if (anim == null) return;
        anim.SetFloat("Speed", speed, 0.15f, Time.deltaTime);
    }
}
