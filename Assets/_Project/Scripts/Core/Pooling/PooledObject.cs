using UnityEngine;

// Marca que esta instancia salió de un pool y recuerda de cuál, para poder auto-devolverse sin que
// el código de gameplay tenga que conocer el pool. El GameObjectPool lo añade automáticamente a cada
// instancia que crea. Si por lo que sea el objeto no salió de un pool, Release() hace Destroy como
// fallback seguro.
[DisallowMultipleComponent]
public class PooledObject : MonoBehaviour
{
    private GameObjectPool pool;

    public void SetPool(GameObjectPool owner) => pool = owner;

    // Devuelve este objeto a su pool (o lo destruye si no tiene pool).
    public void Release()
    {
        if (pool != null) pool.Release(gameObject);
        else Destroy(gameObject);
    }
}
