using System.Collections;
using UnityEngine;

// Clase base de TODOS los ataques del boss. Cada ataque concreto es un ScriptableObject (un asset)
// que hereda de aquí e implementa Execute() como una corrutina con su coreografía completa
// (telegraph -> activo -> recuperación).
//
// REGLA IMPORTANTE: las SO son assets COMPARTIDOS. Nunca guardes estado de la ejecución en campos
// de la instancia (se pisaría si dos bosses usaran el mismo asset). Todo el estado por-ejecución
// va en variables LOCALES dentro de Execute().
//
// Crear un ataque nuevo:
//   1) public class MiAtaqueSO : BossAttackSO { ... }  con [CreateAssetMenu(...)]
//   2) implementa Execute usando ctx.Spawn / ctx.AimToPlayer / BossAttackUtils
//   3) crea el .asset (Create -> SpaceSpaceSpace/Boss/...) y tunéalo en el Inspector
public abstract class BossAttackSO : ScriptableObject
{
    [Tooltip("Identificador para depurar y para reconocer el ataque en combos (bh_atk1, sekiro_atk2...).")]
    public string id = "atk";

    [Tooltip("¿Las balas/golpes de este ataque se pueden parrear? bullet-hell = false, sekiro = true.")]
    public bool parryable = false;

    [Tooltip("Aviso previo (telegraph) antes de la parte activa, en segundos. Da tiempo a reaccionar.")]
    public float telegraphSeconds = 0.4f;

    [Tooltip("Recuperación tras la parte activa, en segundos.")]
    public float recoverySeconds = 0.3f;

    [Tooltip("VFX opcional que se instancia en el boss durante el telegraph (aviso visual).")]
    public GameObject telegraphVfx;

    // Toda la coreografía del ataque. La corre el Director.
    public abstract IEnumerator Execute(BossContext ctx);

    // Helper estándar de telegraph: espera 'telegraphSeconds' y, si hay, muestra el VFX de aviso.
    // Llama: yield return Telegraph(ctx);
    protected IEnumerator Telegraph(BossContext ctx)
    {
        GameObject vfx = null;
        if (telegraphVfx != null && ctx.Boss != null)
            vfx = Object.Instantiate(telegraphVfx, ctx.Boss.position, Quaternion.identity, ctx.Boss);

        if (telegraphSeconds > 0f) yield return new WaitForSeconds(telegraphSeconds);

        if (vfx != null) Object.Destroy(vfx);
    }

    // Helper estándar de recuperación. Llama: yield return Recover();
    protected IEnumerator Recover()
    {
        if (recoverySeconds > 0f) yield return new WaitForSeconds(recoverySeconds);
    }
}
