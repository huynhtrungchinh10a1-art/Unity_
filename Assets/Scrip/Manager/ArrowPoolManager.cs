using System.Collections.Generic;
using UnityEngine;

public class ArrowPoolManager : MonoBehaviour
{
    private static ArrowPoolManager _instance;
    public static ArrowPoolManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<ArrowPoolManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("[ArrowPoolManager]");
                    _instance = go.AddComponent<ArrowPoolManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Pool Settings")]
    public GameObject arrowPrefab;
    public int initialPoolSize = 2000;

    [Header("Debug Stats (Theo dõi trực quan)")]
    [SerializeField] private int activeArrowsCount = 0;
    [SerializeField] private int availableInPoolCount = 0;
    [SerializeField] private int totalCreatedCount = 0;

    private Queue<GameObject> pool = new Queue<GameObject>(512);
    private Transform poolContainer;
    private bool isPrewarmed = false;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        SetupContainer();

        if (arrowPrefab != null && !isPrewarmed)
        {
            Prewarm(initialPoolSize);
        }
    }

    private void SetupContainer()
    {
        if (poolContainer == null)
        {
            GameObject container = new GameObject("ArrowPool_Container");
            container.transform.parent = transform;
            poolContainer = container.transform;
        }
    }

    public void Prewarm(int count)
    {
        if (arrowPrefab == null || isPrewarmed) return;

        SetupContainer();

        for (int i = 0; i < count; i++)
        {
            CreateNewArrowForPool();
        }

        isPrewarmed = true;
        UpdateStats();
    }

    private GameObject CreateNewArrowForPool()
    {
        GameObject arrow = Instantiate(arrowPrefab, poolContainer);
        arrow.SetActive(false);
        pool.Enqueue(arrow);
        totalCreatedCount++;
        return arrow;
    }

    public GameObject GetArrow(Vector3 position, Quaternion rotation, GameObject fallbackPrefab = null)
    {
        if (arrowPrefab == null && fallbackPrefab != null)
        {
            arrowPrefab = fallbackPrefab;
            if (!isPrewarmed)
            {
                Prewarm(initialPoolSize);
            }
        }

        SetupContainer();

        GameObject arrow = null;
        while (pool.Count > 0)
        {
            arrow = pool.Dequeue();
            if (arrow != null) break;
        }

        if (arrow == null)
        {
            if (arrowPrefab != null)
            {
                arrow = Instantiate(arrowPrefab, poolContainer);
                totalCreatedCount++;
            }
            else
            {
                Debug.LogWarning("[ArrowPoolManager] Chưa gán arrowPrefab!");
                return null;
            }
        }

        arrow.transform.parent = null;
        arrow.transform.position = position;
        arrow.transform.rotation = rotation;
        arrow.transform.localScale = Vector3.one;

        ArrowProjectile proj = arrow.GetComponent<ArrowProjectile>();
        if (proj != null)
        {
            proj.ResetProjectile();
        }

        arrow.SetActive(true);
        activeArrowsCount++;
        UpdateStats();

        return arrow;
    }

    public void ReturnArrow(GameObject arrow)
    {
        if (arrow == null) return;

        arrow.SetActive(false);
        if (poolContainer != null)
        {
            arrow.transform.parent = poolContainer;
        }

        pool.Enqueue(arrow);
        activeArrowsCount = Mathf.Max(0, activeArrowsCount - 1);
        UpdateStats();
    }

    private void UpdateStats()
    {
        availableInPoolCount = pool.Count;
    }
}
