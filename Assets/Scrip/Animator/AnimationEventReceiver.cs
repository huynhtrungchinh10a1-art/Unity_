using UnityEngine;

public class AnimationEventReceiver : MonoBehaviour
{
    public DamageHitbox[] hitboxes;

    void Start()
    {
        foreach (var box in hitboxes)
        {
            if (box != null) box.Init(gameObject);
        }
    }

    public void AnimEvent_EnableHitbox(int index)
    {
        if (index >= 0 && index < hitboxes.Length && hitboxes[index] != null)
        {
            hitboxes[index].EnableHitbox();
        }
    }

    public void AnimEvent_DisableHitbox()
    {
        foreach (var box in hitboxes)
        {
            if (box != null) box.DisableHitbox();
        }
    }
}