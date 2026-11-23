using UnityEngine;

public class ArrowProjectile : MonoBehaviour
{
    [Header("Flight Settings")]
    public float speed = 35f;
    public float gravity = 6f;
    public float maxAirTime = 15f;
    public float arrowLength = 0.73f;

    [Header("Combat Settings")]
    public float damage = 1f;

    [Header("Stick Settings (Cắm lại trên Map)")]
    public bool stayOnMap = true;
    public float disappearDelay = 10f;
    public float groundEmbedDepth = 0.12f;
    public float characterEmbedDepth = 0.46f;
    [Range(20f, 80f)]
    public float minGroundAngle = 35f;
    [Range(20f, 80f)]
    public float maxGroundAngle = 55f;

    private GameObject owner;
    private HealthAndTeam ownerHealth;
    private bool hasHit = false;
    private Vector3 velocity;
    private float airTimer = 0f;

    public void Init(GameObject shooter, float customDamage = -1f, float customSpeed = -1f, float customGravity = -1f)
    {
        owner = shooter;
        if (customDamage > 0)
        {
            damage = customDamage;
        }

        if (customSpeed > 0)
        {
            speed = customSpeed;
        }

        if (customGravity > 0)
        {
            gravity = customGravity;
        }

        if (owner != null)
        {
            ownerHealth = owner.GetComponentInParent<HealthAndTeam>();
        }

        velocity = transform.forward * speed;
    }

    void Update()
    {
        if (hasHit) return;

        // Đếm thời gian bay trên không. Chỉ hủy khi bay lạc vô tận ra ngoài map
        airTimer += Time.deltaTime;
        if (airTimer >= maxAirTime)
        {
            ReturnToPool();
            return;
        }

        // Trọng lực nhẹ kéo mũi tên rơi nhẹ theo đường cong parabol
        velocity.y -= gravity * Time.deltaTime;

        float halfLen = arrowLength * 0.5f;

        Vector3 currentPos = transform.position;
        Vector3 nextPos = currentPos + velocity * Time.deltaTime;

        // Xoay đầu mũi tên theo đúng hướng vận tốc bay
        if (velocity.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(velocity);
        }

        // Raycast kiểm tra va chạm từ ĐẦU MŨI TÊN (currentTip) tới nextTip
        Vector3 currentTip = currentPos + transform.forward * halfLen;
        Vector3 nextTip = nextPos + (velocity.sqrMagnitude > 0.01f ? velocity.normalized : transform.forward) * halfLen;
        Vector3 step = nextTip - currentTip;

        RaycastHit[] hits = Physics.RaycastAll(currentTip, step.normalized, step.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        bool hitValid = false;

        if (hits != null && hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                // Bỏ qua chính mình hoặc mũi tên khác trên trời (chống dính mũi tên giữa không trung!)
                if (h.collider.gameObject == gameObject || h.collider.transform.IsChildOf(transform)) continue;
                if (h.collider.GetComponentInParent<ArrowProjectile>() != null) continue;

                // Bỏ qua người bắn
                if (owner != null && (h.collider.gameObject == owner || h.collider.transform.IsChildOf(owner.transform))) continue;

                // Nếu va phải đồng đội hoặc đối tượng đã chết: Mũi tên tiếp tục bay xuyên qua
                HealthAndTeam th = h.collider.GetComponentInParent<HealthAndTeam>();
                if (th != null)
                {
                    if (!th.isAlive) continue;
                    if (ownerHealth != null && !ownerHealth.IsEnemy(th.teamCurrent)) continue;
                }

                OnHitTarget(h.collider, h.point);
                hitValid = true;
                break;
            }
        }

        if (!hitValid && !hasHit)
        {
            transform.position = nextPos;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit || other == null) return;
        if (other.isTrigger) return;
        if (other.gameObject == gameObject || other.transform.IsChildOf(transform)) return;
        if (other.GetComponentInParent<ArrowProjectile>() != null) return;

        float halfLen = arrowLength * 0.5f;
        Vector3 tipPoint = transform.position + transform.forward * halfLen;
        OnHitTarget(other, tipPoint);
    }

    private void OnHitTarget(Collider other, Vector3 hitPoint)
    {
        if (hasHit || other == null) return;
        if (other.gameObject == gameObject || other.transform.IsChildOf(transform)) return;
        if (other.GetComponentInParent<ArrowProjectile>() != null) return;
        if (owner != null && (other.gameObject == owner || other.transform.IsChildOf(owner.transform))) return;

        HealthAndTeam targetHealth = other.GetComponentInParent<HealthAndTeam>();
        // Nếu va phải đồng đội hoặc đối tượng đã chết: Không cho mũi tên cắm trên không
        if (targetHealth != null)
        {
            if (!targetHealth.isAlive) return;
            if (ownerHealth != null && !ownerHealth.IsEnemy(targetHealth.teamCurrent)) return;
        }

        hasHit = true;
        bool isCharacter = (targetHealth != null);

        // Nếu cắm vào ĐẤT / MÔI TRƯỜNG (không phải cắm vào nhân vật):
        // Ép góc cắm luôn chúc từ trên trời xuống (35 - 55 độ) để đuôi tên vểnh lên cao, tạo cảm giác tên bắn từ trên cắm xuống
        if (!isCharacter)
        {
            // Ép trực tiếp cao độ mặt đất theo Terrain (chuẩn xác 100%, không bị ảnh hưởng bởi collider cản tia trên trời)
            if (Terrain.activeTerrain != null)
            {
                hitPoint.y = Terrain.activeTerrain.SampleHeight(hitPoint) + Terrain.activeTerrain.transform.position.y;
            }
            else
            {
                RaycastHit groundHit;
                if (Physics.Raycast(hitPoint + Vector3.up * 1.5f, Vector3.down, out groundHit, 5.0f, LayerMask.GetMask("Default")))
                {
                    hitPoint = groundHit.point;
                }
            }

            Vector3 flightHoriz = velocity;
            flightHoriz.y = 0;
            if (flightHoriz.sqrMagnitude < 0.001f)
            {
                flightHoriz = transform.forward;
                flightHoriz.y = 0;
            }
            if (flightHoriz.sqrMagnitude < 0.001f)
            {
                flightHoriz = Vector3.forward;
            }
            flightHoriz.Normalize();

            // Tính góc chúc xuống hiện tại của mũi tên
            float currentDownAngle = Vector3.Angle(flightHoriz, velocity);
            float targetAngle = Mathf.Clamp(currentDownAngle, minGroundAngle, maxGroundAngle);
            if (currentDownAngle < minGroundAngle)
            {
                targetAngle = Random.Range(minGroundAngle, maxGroundAngle);
            }

            float rad = targetAngle * Mathf.Deg2Rad;
            Vector3 stickDir = flightHoriz * Mathf.Cos(rad) + Vector3.down * Mathf.Sin(rad);
            transform.rotation = Quaternion.LookRotation(stickDir);
        }

        float halfLen = arrowLength * 0.5f;
        float baseDepth = isCharacter ? characterEmbedDepth : groundEmbedDepth;
        float actualDepth = Mathf.Clamp(baseDepth + Random.Range(-0.02f, 0.02f), 0.04f, arrowLength * 0.9f);

        // Đặt tâm sao cho đầu nhọn cắm vào hitPoint một đoạn actualDepth, còn lại phần lớn thân tên trồi lên trên mặt đất / cơ thể
        transform.position = hitPoint + transform.forward * (actualDepth - halfLen);

        if (isCharacter)
        {
            if (targetHealth.isAlive && (ownerHealth == null || ownerHealth.IsEnemy(targetHealth.teamCurrent)))
            {
                targetHealth.TakeDamage(damage, owner);
            }

            // Gắn mũi tên vào xương gần nhất của nhân vật (kể cả khi vừa chết/đang ngã) để chuyển động uốn lượn theo cơ thể
            Transform closestBone = FindClosestBone(other.transform, hitPoint);
            if (closestBone != null)
            {
                transform.SetParent(closestBone, true);
            }

            // Nếu cắm vào NPC: Cập nhật renderers cho NPC và tự động tắt mesh nếu NPC đang bị culled ở xa
            NPCCombat npcCombat = other.GetComponentInParent<NPCCombat>();
            if (npcCombat != null)
            {
                npcCombat.RefreshRenderers();
                if (npcCombat.IsCulled)
                {
                    MeshRenderer mr = GetComponentInChildren<MeshRenderer>();
                    if (mr != null) mr.enabled = false;
                }
            }
        }
        // Nếu cắm vào đất (Terrain) / môi trường: Không đổi parent để giữ vị trí độc lập, không bị ảnh hưởng scale

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        if (!stayOnMap)
        {
            StartCoroutine(RoutineReturnDelay(disappearDelay));
        }

        // Rung lắc nhẹ đuôi tên khi vừa cắm vào
        StartCoroutine(RoutineWobble());
    }

    private Transform FindClosestBone(Transform hitTransform, Vector3 point)
    {
        Animator anim = hitTransform.GetComponentInParent<Animator>();
        if (anim != null && anim.isHuman)
        {
            HumanBodyBones[] bones = new HumanBodyBones[]
            {
                HumanBodyBones.Chest,
                HumanBodyBones.Spine,
                HumanBodyBones.Hips,
                HumanBodyBones.Head,
                HumanBodyBones.LeftUpperArm,
                HumanBodyBones.LeftLowerArm,
                HumanBodyBones.RightUpperArm,
                HumanBodyBones.RightLowerArm,
                HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.RightUpperLeg,
                HumanBodyBones.RightLowerLeg
            };

            Transform closest = null;
            float minDistance = float.MaxValue;

            foreach (var b in bones)
            {
                Transform boneTransform = anim.GetBoneTransform(b);
                if (boneTransform != null)
                {
                    float d = Vector3.Distance(boneTransform.position, point);
                    if (d < minDistance)
                    {
                        minDistance = d;
                        closest = boneTransform;
                    }
                }
            }

            if (closest != null) return closest;
        }

        SkinnedMeshRenderer smr = hitTransform.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr == null) smr = hitTransform.GetComponentInParent<SkinnedMeshRenderer>();
        if (smr != null && smr.bones != null && smr.bones.Length > 0)
        {
            Transform closest = smr.bones[0];
            float minDistance = Vector3.Distance(closest.position, point);
            for (int i = 1; i < smr.bones.Length; i++)
            {
                if (smr.bones[i] != null)
                {
                    float d = Vector3.Distance(smr.bones[i].position, point);
                    if (d < minDistance)
                    {
                        minDistance = d;
                        closest = smr.bones[i];
                    }
                }
            }
            return closest;
        }

        return hitTransform;
    }

    private System.Collections.IEnumerator RoutineWobble()
    {
        Quaternion initialRot = transform.localRotation;
        float duration = 0.35f;
        float elapsed = 0f;
        float frequency = 45f;
        float magnitude = 5f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float damping = 1f - (elapsed / duration);
            float angleOffset = Mathf.Sin(elapsed * frequency) * magnitude * damping;
            transform.localRotation = initialRot * Quaternion.Euler(angleOffset, 0f, 0f);
            yield return null;
        }

        transform.localRotation = initialRot;

        // Nếu cắm trên mặt đất (không gắn vào xương nhân vật):
        // Chuyển giao ma trận cho GroundArrowManager (Total War GPU Instancing) và trả mũi tên về Pool
        if (transform.parent == null)
        {
            // Bảo vệ kép chống lơ lửng trên không: ép vị trí sát mặt đất trước khi nướng GPU Instancing
            if (Terrain.activeTerrain != null)
            {
                float terrY = Terrain.activeTerrain.SampleHeight(transform.position) + Terrain.activeTerrain.transform.position.y;
                float halfLen = arrowLength * 0.5f;
                Vector3 groundPoint = new Vector3(transform.position.x, terrY, transform.position.z);
                transform.position = groundPoint + transform.forward * (groundEmbedDepth - halfLen);
            }
            else
            {
                RaycastHit groundHit;
                if (Physics.Raycast(transform.position + Vector3.up * 1.5f, Vector3.down, out groundHit, 5.0f, LayerMask.GetMask("Default")))
                {
                    float halfLen = arrowLength * 0.5f;
                    transform.position = groundHit.point + transform.forward * (groundEmbedDepth - halfLen);
                }
            }

            MeshFilter mf = GetComponentInChildren<MeshFilter>();
            MeshRenderer mr = GetComponentInChildren<MeshRenderer>();
            if (mf != null && mr != null && mf.sharedMesh != null && mr.sharedMaterial != null)
            {
                Transform visualT = mf.transform;
                GroundArrowManager.Instance.RegisterArrow(mf.sharedMesh, mr.sharedMaterial, visualT.localToWorldMatrix);
                ReturnToPool();
                yield break;
            }
        }

        // Nếu cắm vào xương nhân vật: tắt script ArrowProjectile để ngừng tính toán (thay vì Destroy(this))
        enabled = false;
    }

    public void ReturnToPool()
    {
        if (ArrowPoolManager.Instance != null)
        {
            ArrowPoolManager.Instance.ReturnArrow(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private System.Collections.IEnumerator RoutineReturnDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnToPool();
    }

    public void ResetProjectile()
    {
        StopAllCoroutines();
        hasHit = false;
        airTimer = 0f;
        transform.parent = null;
        transform.localScale = Vector3.one;
        enabled = true;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        MeshRenderer mr = GetComponentInChildren<MeshRenderer>();
        if (mr != null) mr.enabled = true;
    }
}

