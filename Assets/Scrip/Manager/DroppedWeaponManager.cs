using System.Collections.Generic;
using UnityEngine;

public class DroppedWeaponManager : MonoBehaviour
{
    private static DroppedWeaponManager _instance;
    public static DroppedWeaponManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<DroppedWeaponManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("[DroppedWeaponManager]");
                    _instance = go.AddComponent<DroppedWeaponManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Distance Settings")]
    public float cullDistance = 45f;
    public Transform playerTransform;

    [Header("Debug Stats")]
    [SerializeField] private int totalWeaponsOnGround = 0;
    [SerializeField] private int culledWeaponsCount = 0;

    private List<DroppedWeapon> weapons = new List<DroppedWeapon>(256);
    private float checkTimer = 0f;
    private const float CHECK_INTERVAL = 0.2f;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        FindPlayer();
    }

    void Start()
    {
        if (playerTransform == null) FindPlayer();
    }

    private void FindPlayer()
    {
        if (PlayerMovement.PlayerTransform != null)
        {
            playerTransform = PlayerMovement.PlayerTransform;
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    public void RegisterWeapon(DroppedWeapon weapon)
    {
        if (weapon != null && !weapons.Contains(weapon))
        {
            weapons.Add(weapon);
            totalWeaponsOnGround = weapons.Count;
        }
    }

    public void UnregisterWeapon(DroppedWeapon weapon)
    {
        if (weapon != null)
        {
            weapons.Remove(weapon);
            totalWeaponsOnGround = weapons.Count;
        }
    }

    void Update()
    {
        checkTimer += Time.deltaTime;
        if (checkTimer < CHECK_INTERVAL) return;
        checkTimer = 0f;

        if (weapons.Count == 0) return;

        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null) return;
        }

        Vector3 playerPos = playerTransform.position;
        float cullDistSq = cullDistance * cullDistance;
        int culled = 0;

        for (int i = weapons.Count - 1; i >= 0; i--)
        {
            DroppedWeapon w = weapons[i];
            if (w == null)
            {
                weapons.RemoveAt(i);
                continue;
            }

            Vector3 wPos = w.transform.position;
            float dx = wPos.x - playerPos.x;
            float dz = wPos.z - playerPos.z;
            float distSq = dx * dx + dz * dz;

            bool isFar = distSq > cullDistSq;
            w.SetVisible(!isFar);

            if (isFar) culled++;
        }

        totalWeaponsOnGround = weapons.Count;
        culledWeaponsCount = culled;
    }
}
