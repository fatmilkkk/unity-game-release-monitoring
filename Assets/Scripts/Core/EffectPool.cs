using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Пул объектов для эффектов (частицы сбора монеты, взрыва, гибели).
/// Вместо Instantiate/Destroy на каждое событие эффект берётся из пула и возвращается в него,
/// что убирает всплески сборщика мусора и лишние выделения памяти.
/// </summary>
public static class EffectPool
{
    private static readonly Dictionary<GameObject, Stack<GameObject>> Free = new Dictionary<GameObject, Stack<GameObject>>();
    private static readonly Dictionary<GameObject, GameObject> PrefabOf = new Dictionary<GameObject, GameObject>();

    /// <summary>Сколько объектов создано (для статистики и тестов).</summary>
    public static int CreatedCount { get; private set; }

    /// <summary>Сколько раз объект был переиспользован из пула.</summary>
    public static int ReusedCount { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Clear();
        CreatedCount = 0;
        ReusedCount = 0;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Объекты старой сцены уничтожены — ссылки на них больше не нужны
        if (mode == LoadSceneMode.Single)
            Clear();
    }

    public static void Clear()
    {
        Free.Clear();
        PrefabOf.Clear();
    }

    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        if (Free.TryGetValue(prefab, out Stack<GameObject> stack))
        {
            while (stack.Count > 0)
            {
                GameObject pooled = stack.Pop();
                if (pooled == null)
                    continue;

                pooled.transform.SetPositionAndRotation(position, rotation);
                pooled.SetActive(true);
                Restart(pooled);
                ReusedCount++;
                return pooled;
            }
        }

        GameObject instance = Object.Instantiate(prefab, position, rotation);
        if (!instance.activeSelf)
            instance.SetActive(true); // шаблон может храниться выключенным
        PrefabOf[instance] = prefab;
        CreatedCount++;
        return instance;
    }

    public static GameObject Spawn(GameObject prefab, Vector3 position) => Spawn(prefab, position, Quaternion.identity);

    /// <summary>Вернуть объект в пул. false — объект создан не пулом (его нужно уничтожить обычным образом).</summary>
    public static bool Release(GameObject instance)
    {
        if (instance == null || !PrefabOf.TryGetValue(instance, out GameObject prefab))
            return false;

        instance.SetActive(false);
        if (!Free.TryGetValue(prefab, out Stack<GameObject> stack))
        {
            stack = new Stack<GameObject>();
            Free[prefab] = stack;
        }
        stack.Push(instance);
        return true;
    }

    /// <summary>Заранее создать объекты, чтобы первый взрыв не вызывал подтормаживание.</summary>
    public static void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null)
            return;

        var created = new List<GameObject>(count);
        for (int i = 0; i < count; i++)
            created.Add(Spawn(prefab, new Vector3(0f, -1000f, 0f)));
        foreach (GameObject go in created)
            Release(go);
    }

    public static int FreeCount(GameObject prefab) =>
        prefab != null && Free.TryGetValue(prefab, out Stack<GameObject> s) ? s.Count : 0;

    private static void Restart(GameObject go)
    {
        var ps = go.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Clear(true);
            ps.Play(true);
        }
    }
}
