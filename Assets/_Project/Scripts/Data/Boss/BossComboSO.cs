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
        [Tooltip("Espera tras este ataque antes del siguiente, en segundos.")]
        public float delayAfter;
    }

    [Tooltip("Ataques en ORDEN. Se ejecutan uno tras otro (esperando 'delayAfter' entre cada uno).")]
    public Step[] sequence;
}
