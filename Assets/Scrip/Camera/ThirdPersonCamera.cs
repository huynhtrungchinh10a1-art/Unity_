using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    public Transform pivot;
    public Transform cam;
    public Animator anim;

    public float mouseSpeed = 3f;
    public float minY = -30f;
    public float maxY = 60f;
    public float distance = 4f;

    float yaw;
    float pitch;

    bool isLocked = false;
    Transform lockTarget;

    void LateUpdate()
    {
        pivot.position = target.position + new Vector3(0, 1.6f, 0);

        HandleLockToggle();

        if (isLocked && lockTarget != null)
        {
            LockOnCamera();
        }
        else
        {
            FreeLookCamera();
        }

        anim.SetBool("IsLocked", isLocked);
    }

    void HandleLockToggle()
    {
        if (Input.GetKeyDown(KeyCode.P))
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

        pivot.rotation = Quaternion.Euler(pitch, yaw, 0);
        cam.localPosition = new Vector3(0, 0, -distance);
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
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

        float closest = Mathf.Infinity;
        Transform nearest = null;

        foreach (var e in enemies)
        {
            float dist = Vector3.Distance(target.position, e.transform.position);
            if (dist < closest)
            {
                closest = dist;
                nearest = e.transform;
            }
        }

        if (nearest != null)
        {
            lockTarget = nearest;
            isLocked = true;
        }
    }

    // nhanh hơn animator.get
    public bool IsLocked()
    {
        return isLocked;
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
