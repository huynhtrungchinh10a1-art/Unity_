using UnityEngine;
public class PlayerCombat : MonoBehaviour
{
    public Animator anim;
    bool isAttacking = false;
    bool canQueueCombo = false;

    float bufferTime = 0.2f;
    float bufferTimer = 0f;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            bufferTimer = bufferTime;
        }

        if (bufferTimer > 0)
        {
            bufferTimer -= Time.deltaTime;
        }

        HandleBufferedInput();
    }
    void HandleBufferedInput()
    {
        if (bufferTimer <= 0) return;

        if (!isAttacking)
        {
            startAttack();
            bufferTimer = 0f;
        }
        else if (canQueueCombo)
        {
            anim.SetBool("ComboQueued", true);
            bufferTimer = 0f;
        }
    }
    void startAttack()
    {
        isAttacking = true;
        anim.SetTrigger("Attack");

    }
    void OpenComboWindow()
    {
        canQueueCombo = true;
        anim.SetBool("ComboQueued", false);
    }
    void CloseComboWindow()
    {
        canQueueCombo = false;
    }
    public void EndAttack()
    {
        ResetCombat();
    }
    // chờ có stamina
    public void ForceStopAttack()
    {
        ResetCombat();
    }
    void ResetCombat()
    {
        isAttacking = false;
        canQueueCombo = false;
        anim.SetBool("ComboQueued", false);
    }
    public bool IsAttacking()
    {
        return isAttacking;
    }
}