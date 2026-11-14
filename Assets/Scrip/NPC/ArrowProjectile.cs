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
    public float characterEmbedDepth = 0.35f;
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
            Destroy(gameObject);
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

                // Nếu va phải đồng đội: Mũi tên tiếp tục bay xuyên qua đồng đội
                HealthAndTeam th = h.collider.GetComponentInParent<HealthAndTeam>();
                if (th != null && th.isAlive && ownerHealth != null && !ownerHealth.IsEnemy(th.teamCurrent))
                {
                    continue;
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
        // Nếu va phải đồng đội: Mũi tên tiếp tục bay xuyên qua đồng đội để bay tới quân địch
        if (targetHealth != null && targetHealth.isAlive)
        {
            if (ownerHealth != null && !ownerHealth.IsEnemy(targetHealth.teamCurrent))
            {
                return;
            }
        }

        hasHit = true;

        // Nếu cắm vào ĐẤT / MÔI TRƯỜNG (không phải cắm vào nhân vật):
        // Ép góc cắm luôn chúc từ trên trời xuống (35 - 55 độ) để đuôi tên vểnh lên cao, tạo cảm giác tên bắn từ trên cắm xuống
        if (targetHealth == null)
        {
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
        float baseDepth = (targetHealth != null && targetHealth.isAlive) ? characterEmbedDepth : groundEmbedDepth;
        float actualDepth = Mathf.Clamp(baseDepth + Random.Range(-0.02f, 0.02f), 0.04f, arrowLength * 0.9f);

        // Đặt tâm sao cho đầu nhọn cắm vào hitPoint một đoạn actualDepth, còn lại phần lớn thân tên trồi lên trên mặt đất / cơ thể
        transform.position = hitPoint + transform.forward * (actualDepth - halfLen);

        if (targetHealth != null && targetHealth.isAlive)
        {
            if (ownerHealth == null || ownerHealth.IsEnemy(targetHealth.teamCurrent))
            {
                targetHealth.TakeDamage(damage, owner);
            }

            // Gắn mũi tên vào xương gần nhất của nhân vật để uốn lượn theo cử động
            Transform closestBone = FindClosestBone(other.transform, hitPoint);
            transform.parent = closestBone;
        }
        // Nếu cắm vào đất (Terrain) / môi trường: Không đổi parent để giữ vị trí độc lập, không bị ảnh hưởng scale

        Collider col = GetComponent<Collider>();
        if (col != null) Destroy(col);

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);

        if (!stayOnMap)
        {
            Destroy(gameObject, disappearDelay);
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
        Destroy(this);
    }
}

