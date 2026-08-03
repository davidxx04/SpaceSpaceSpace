using UnityEngine;

// Tipo de combo, para que el diseño deje claro cómo se resuelve (y para futuros filtros/telemetría).
public enum ComboType
{
    BulletHell, // todo no parreable: se esquiva con los dos dashes
    Sekiro,     // algunos/todos parreables: dash/parry/attack en secuencia
    Mixed,      // mezcla de ambos
}

// Un combo: una SECUENCIA FIJA de ataques con delays entre ellos. El orden es justo lo que el
// jugador memoriza. Combinar ataques = arrastrarlos al array 'sequence' y poner los delays.
// Sin código.
[CreateAssetMenu(menuName = "SpaceSpaceSpace/Boss/Combo", fileName = "BossCombo")]
public class BossComboSO : ScriptableObject
{
    [Tooltip("Identificador para depurar (combo_1, opener, finisher...).")]
    public string id = "combo";

    public ComboType type;

    [System.Serializable]
    public struct Step
    {
        public BossAttackSO attack;

        [Tooltip("Espera tras este ataque antes del siguiente, en segundos. Se ignora si 'launchInterval' > 0.")]
        public float delayAfter;

        [Tooltip("Si > 0, el SIGUIENTE paso se lanza este tiempo después de INICIAR este ataque (no tras " +
                 "que termine) -> permite ataques solapados/concurrentes (p.ej. varios swooshes en pantalla " +
                 "a la vez). 0 = comportamiento clásico: espera a que este ataque termine del todo " +
                 "(telegraph+activo+recovery) y LUEGO aplica 'delayAfter'.")]
        public float launchInterval;
    }

    [Tooltip("Ataques en ORDEN. Se ejecutan uno tras otro (esperando 'delayAfter' entre cada uno), salvo " +
             "que un paso use 'launchInterval' para solaparse con el siguiente.")]
    public Step[] sequence;
}
