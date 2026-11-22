using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    public Transform pivot;
    public Transform cam;
    public Animator anim;
    public Transform firstPersonTarget;

    [Header("Mouse & Smoothing Settings (Chống Chóng Mặt)")]
    public float mouseSpeed = 2.0f;
    [Tooltip("Bật làm mượt camera để chống giật/chóng mặt khi lắc chuột nhanh")]
    public bool enableSmoothing = true;
    [Range(0.01f, 0.15f)]
    [Tooltip("Thời gian làm mượt (càng cao càng đầm, 0.03 - 0.05 là chuẩn vàng không bị trễ)")]
    public float smoothTime = 0.04f;

    [Header("Camera Distance & Bounds")]
    public float minY = -30f;
    public float maxY = 60f;
    public float distance = 3f;
    public float pivotHeight = 1.3f;
    public float lockDistance = 25f;

    float yaw;
    float pitch;
    float currentYaw;
    float currentPitch;
    float yawVelocity;
    float pitchVelocity;

    bool isLocked = false;
    public bool isFirstPerson = false;
    Transform lockTarget;

    void Start()
    {
        if (pivot != null)
        {
            Vector3 angles = pivot.eulerAngles;
            yaw = currentYaw = angles.y;
            pitch = currentPitch = angles.x;
            if (pitch > 180f) pitch -= 360f;
        }
    }

    void LateUpdate()
    {
        if (isFirstPerson && firstPersonTarget != null)
        {
            pivot.position = firstPersonTarget.position;
            
            // Xoay nhân vật theo hướng yaw của camera ở góc nhìn thứ nhất
            float activeYaw = enableSmoothing ? currentYaw : yaw;
            target.rotation = Quaternion.Euler(0, activeYaw, 0);
        }
        else
        {
            pivot.position = target.position + new Vector3(0, pivotHeight, 0);
        }

        HandleLockToggle();

        // Tu dong mo khoa neu muc tieu bi tieu diet hoac null
        if (isLocked)
        {
            if (lockTarget == null)
            {
                isLocked = false;
            }
            else
            {
                HealthAndTeam targetH = lockTarget.GetComponent<HealthAndTeam>();
                if (targetH != null && !targetH.isAlive)
                {
                    isLocked = false;
                    lockTarget = null;
                }
            }
        }

        if (isLocked && lockTarget != null)
        {
            LockOnCamera();
        }
        else
        {
            FreeLookCamera();
        }

        anim.SetBool("IsLocked", isLocked || isFirstPerson);
    }

    void HandleLockToggle()
    {
        // Phim P: Bat/Tat goc nhin thu nhat
        if (Input.GetKeyDown(KeyCode.P))
        {
            isFirstPerson = !isFirstPerson;
            isLocked = false;
            lockTarget = null;
        }

        // Phim Tab: Bat/Tat Lock-on khoa muc tieu ke dich
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (!isLocked)
            {
                FindTarget();
            }
            else
            {
                isLocked = false;
                lockTarget = null;
            }
        }
    }

    void FreeLookCamera()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSpeed;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSpeed;

        yaw += mouseX;
        // làm ngược lại cho thuận tay
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minY, maxY);

        if (enableSmoothing)
        {
            currentYaw = Mathf.SmoothDampAngle(currentYaw, yaw, ref yawVelocity, smoothTime);
            currentPitch = Mathf.SmoothDampAngle(currentPitch, pitch, ref pitchVelocity, smoothTime);
            pivot.rotation = Quaternion.Euler(currentPitch, currentYaw, 0);
        }
        else
        {
            pivot.rotation = Quaternion.Euler(pitch, yaw, 0);
        }
        
        if (isFirstPerson && firstPersonTarget != null)
        {
            cam.localPosition = Vector3.zero;
        }
        else
        {
            cam.localPosition = new Vector3(0, 0, -distance);
        }
    }

    void LockOnCamera()
    {
        Vector3 dir = lockTarget.position - target.position;
        dir.y = 0;

        Quaternion rot = Quaternion.LookRotation(dir);
        // slerp xoay từ từ
        target.rotation = Quaternion.Slerp(target.rotation, rot, 10f * Time.deltaTime);

        Vector3 mid = (target.position + lockTarget.position) / 2f;

        pivot.LookAt(mid);
        cam.localPosition = new Vector3(0, 0, -distance);
    }

    void FindTarget()
    {
        HealthAndTeam playerHealth = target != null ? target.GetComponent<HealthAndTeam>() : null;
        HealthAndTeam[] allHealths = FindObjectsOfType<HealthAndTeam>();

        float bestScore = Mathf.Infinity;
        Transform bestTarget = null;
        Vector3 camForward = cam != null ? cam.forward : (pivot != null ? pivot.forward : transform.forward);
        camForward.y = 0;
        camForward.Normalize();

        foreach (var h in allHealths)
        {
            if (h == null || !h.isAlive || (target != null && h.transform == target)) continue;

            // Neu nguoi choi co team, chi khoa vao ke thù
            if (playerHealth != null && !playerHealth.IsEnemy(h.teamCurrent)) continue;

            float dist = Vector3.Distance(target.position, h.transform.position);
            if (dist > lockDistance) continue;

            // Tinh goc so voi huong nhin cua Camera de uu tien muc tieu truoc mat
            Vector3 dirToTarget = (h.transform.position - target.position);
            dirToTarget.y = 0;
            if (dirToTarget.sqrMagnitude < 0.01f) continue;

            float angle = Vector3.Angle(camForward, dirToTarget.normalized);
            // Chi khoa muc tieu trong tam mat (angle < 75 do), tru khi ke dich rat gan (< 8m)
            if (angle > 75f && dist > 8f) continue;

            float score = dist + angle * 0.15f;
            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = h.transform;
            }
        }

        if (bestTarget != null)
        {
            lockTarget = bestTarget;
            isLocked = true;
        }
    }

    // nhanh hơn animator.get
    public bool IsLocked()
    {
        return isLocked || isFirstPerson;
    }

    public Transform GetLockTarget()
    {
        return lockTarget;
    }

    public void AddYaw(float amount)
    {
        yaw += amount;
    }
}
