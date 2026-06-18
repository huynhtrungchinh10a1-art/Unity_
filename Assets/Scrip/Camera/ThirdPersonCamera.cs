using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    public Transform pivot;
    public Transform cam;
    public Animator anim;
    public Transform firstPersonTarget;

    public float mouseSpeed = 3f;
    public float minY = -30f;
    public float maxY = 60f;
    public float distance = 3f;
    public float pivotHeight = 1.3f;

    float yaw;
    float pitch;

    bool isLocked = false;
    public bool isFirstPerson = false;
    Transform lockTarget;

    void LateUpdate()
    {
        if (isFirstPerson && firstPersonTarget != null)
        {
            pivot.position = firstPersonTarget.position;
            
            // Xoay nhân vật theo hướng yaw của camera ở góc nhìn thứ nhất
            Vector3 targetEuler = target.eulerAngles;
            targetEuler.y = yaw;
            target.rotation = Quaternion.Euler(0, yaw, 0);
        }
        else
        {
            pivot.position = target.position + new Vector3(0, pivotHeight, 0);
        }

        HandleLockToggle();

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
        if (Input.GetKeyDown(KeyCode.P))
        {
            isFirstPerson = !isFirstPerson;
            isLocked = false;
            lockTarget = null;
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
