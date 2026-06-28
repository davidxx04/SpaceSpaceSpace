using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Punto central de pooling del juego. Singleton persistente (DontDestroyOnLoad, como GameManager) para
// precalentar los pools al iniciar el juego (incluso en el Menú) y reutilizarlos entre partidas. En
// cada cambio de escena devuelve al pool todo lo que quedara activo (reset limpio, sin arrastrar balas
// del run anterior).
//
// API estática cómoda (los disparadores son clases C# puras como AttackState/BossContext, sin refs de
// Inspector):
//   PoolManager.Spawn(prefab, pos, rot)         -> por prefab (caso habitual)
//   PoolManager.Spawn(key, factory, pos, rot)   -> sin prefab (factory propia)
//   PoolManager.Despawn(instance)               -> devuelve al pool (o Destroy si no es pooled)
public class PoolManager : MonoBehaviour
{
    [Serializable]
    public struct PreloadEntry
    {
        public GameObject prefab;
        [Min(0)] public int prewarm;
        [Min(1)] public int maxSize;
    }

    [Tooltip("Pools a precalentar al arrancar (p. ej. bala del jugador y BossBullet).")]
    [SerializeField] private PreloadEntry[] preload;

    public static PoolManager Instance { get; private set; }

    private const int DefaultMaxSize = 512;

    private readonly Dictionary<object, GameObjectPool> pools = new Dictionary<object, GameObjectPool>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (preload != null)
        {
            foreach (PreloadEntry entry in preload)
            {
                if (entry.prefab == null) continue;
                GameObject prefab = entry.prefab;   // copia local para el closure
                GameObjectPool pool = GetOrCreatePool(prefab, () => Instantiate(prefab), Mathf.Max(1, entry.maxSize));
                pool.Prewarm(entry.prewarm);
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    // Reset limpio en cada carga de escena: lo activo del run anterior vuelve al pool.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObjectPool pool in pools.Values) pool.ReleaseAllActive();
    }

    // --- API estática ---

    // Spawn tipado por prefab (lo más cómodo): devuelve el componente T del prefab.
    public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
    {
        if (prefab == null) { Debug.LogWarning("[PoolManager] Spawn<T> con prefab null."); return null; }
        GameObject go = Spawn(prefab.gameObject, position, rotation);
        return go != null ? go.GetComponent<T>() : null;
    }

    // Spawn por prefab (GameObject).
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) { Debug.LogWarning("[PoolManager] Spawn con prefab null."); return null; }
        PoolManager m = EnsureInstance();
        GameObjectPool pool = m.GetOrCreatePool(prefab, () => Instantiate(prefab), DefaultMaxSize);
        return pool.Get(position, rotation);
    }

    // Spawn sin prefab: tú das la clave y la factory (para objetos creados por código/datos).
    public static GameObject Spawn(object key, Func<GameObject> factory, Vector3 position, Quaternion rotation, int maxSize = DefaultMaxSize)
    {
        if (factory == null) { Debug.LogWarning("[PoolManager] Spawn con factory null."); return null; }
        PoolManager m = EnsureInstance();
        GameObjectPool pool = m.GetOrCreatePool(key, factory, maxSize);
        return pool.Get(position, rotation);
    }

    public static void Despawn(GameObject instance)
    {
        if (instance == null) return;
        if (instance.TryGetComponent<PooledObject>(out var po)) po.Release();
        else Destroy(instance);   // no salió de un pool: fallback seguro
    }

    public static void Despawn(Component instance)
    {
        if (instance != null) Despawn(instance.gameObject);
    }

    // Devuelve al pool TODAS las instancias activas de 'prefab' (solo su pool). Lo usa el boss para
    // limpiar sus ataques en vuelo (áreas/swooshes/balas) al morir/staggear, sin tocar otros pools
    // (p. ej. el de la bala del jugador). Si no hay pool para ese prefab, no hace nada.
    public static void ReleaseActive(GameObject prefab)
    {
        if (prefab == null || Instance == null) return;
        if (Instance.pools.TryGetValue(prefab, out GameObjectPool pool)) pool.ReleaseAllActive();
    }

    public static void ReleaseActive(Component prefab)
    {
        if (prefab != null) ReleaseActive(prefab.gameObject);
    }

    // --- internos ---

    private GameObjectPool GetOrCreatePool(object key, Func<GameObject> factory, int maxSize)
    {
        if (!pools.TryGetValue(key, out GameObjectPool pool))
        {
            pool = new GameObjectPool(key, factory, transform, maxSize);
            pools[key] = pool;
        }
        return pool;
    }

    // Garantiza que exista un PoolManager (si nadie lo colocó en escena, crea uno básico sin preload)
    // para que la API estática nunca falle.
    private static PoolManager EnsureInstance()
    {
        if (Instance == null)
        {
            var go = new GameObject("PoolManager (auto)");
            go.AddComponent<PoolManager>();   // su Awake fija Instance y hace DontDestroyOnLoad
        }
        return Instance;
    }
}
