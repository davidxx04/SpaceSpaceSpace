// Hooks opcionales de ciclo de vida para objetos pooled. Si un objeto del pool implementa esta
// interfaz, el GameObjectPool la invoca al sacarlo (OnSpawned -> resetear estado de uso) y al
// devolverlo (OnDespawned -> limpiar). No es obligatoria: si el reseteo ya lo hace otra cosa
// (p. ej. Projectile.Launch), no hace falta implementarla.
public interface IPoolable
{
    void OnSpawned();
    void OnDespawned();
}
