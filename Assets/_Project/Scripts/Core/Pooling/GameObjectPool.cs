using System;
using System.Collections.Generic;
using UnityEngine;

// Motor de pooling para un único "tipo" de objeto. A propósito NO sabe nada de "prefab": se construye
// con una 'factory' (Func<GameObject> que crea una instancia nueva) y una 'key' arbitraria. El caso
// prefab es solo una factory que hace Instantiate(prefab) (lo monta PoolManager). Así, si en el futuro
// las balas no vienen de un prefab (creadas por código/datos), este motor no cambia.
//
// Política de tamaño: arranca con Prewarm(N) inactivas; si se agotan, instancia más hasta 'maxSize';
// al tocar 'maxSize' recicla la instancia activa MÁS ANTIGUA (+warning). Nunca devuelve null por
// agotamiento ni crea entidades sin límite.
public class GameObjectPool
{
    private readonly object key;
    private readonly Func<GameObject> factory;
    private readonly Transform parent;   // padre donde "duermen" las instancias (el PoolManager)
    private readonly int maxSize;

    private readonly Stack<GameObject> idle = new Stack<GameObject>();
    private readonly List<GameObject> active = new List<GameObject>();   // orden FIFO -> active[0] = más antiguo

    public object Key => key;
    public int IdleCount => idle.Count;
    public int ActiveCount => active.Count;
    public int Total => idle.Count + active.Count;

    public GameObjectPool(object key, Func<GameObject> factory, Transform parent, int maxSize)
    {
        this.key = key;
        this.factory = factory;
        this.parent = parent;
        this.maxSize = Mathf.Max(1, maxSize);
    }

    // Pre-crea 'count' instancias inactivas (sin superar maxSize). Evita tirones en combate.
    public void Prewarm(int count)
    {
        for (int i = 0; i < count && Total < maxSize; i++)
        {
            GameObject go = Create();
            if (go == null) break;
            go.SetActive(false);
            idle.Push(go);
        }
    }

    // Saca una instancia lista para usar en (position, rotation).
    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        GameObject go;

        if (idle.Count > 0)
        {
            go = idle.Pop();
        }
        else if (Total < maxSize)
        {
            go = Create();
        }
        else
        {
            // Tope alcanzado: recicla la activa más antigua (red de seguridad ante bugs / picos).
            go = active[0];
            active.RemoveAt(0);
            Deactivate(go);
            Debug.LogWarning($"[Pool:{key}] maxSize={maxSize} alcanzado; reciclando el objeto más antiguo.");
        }

        if (go == null) return null;

        go.transform.SetPositionAndRotation(position, rotation);
        go.SetActive(true);
        active.Add(go);

        if (go.TryGetComponent<IPoolable>(out var poolable)) poolable.OnSpawned();
        return go;
    }

    // Devuelve una instancia al pool. Idempotente (un doble Release no rompe nada).
    public void Release(GameObject go)
    {
        if (go == null) return;

        bool wasActive = active.Remove(go);
        if (!wasActive && idle.Contains(go)) return;   // ya estaba devuelta

        Deactivate(go);
        idle.Push(go);
    }

    // Devuelve TODO lo activo al pool (lo llama PoolManager al cambiar de escena: reset limpio).
    public void ReleaseAllActive()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            GameObject go = active[i];
            active.RemoveAt(i);
            if (go == null) continue;
            Deactivate(go);
            idle.Push(go);
        }
    }

    private GameObject Create()
    {
        GameObject go = factory();
        if (go == null)
        {
            Debug.LogError($"[Pool:{key}] la factory devolvió null.");
            return null;
        }

        if (!go.TryGetComponent<PooledObject>(out var po)) po = go.AddComponent<PooledObject>();
        po.SetPool(this);

        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    private void Deactivate(GameObject go)
    {
        if (go.TryGetComponent<IPoolable>(out var poolable)) poolable.OnDespawned();
        go.SetActive(false);
    }
}
