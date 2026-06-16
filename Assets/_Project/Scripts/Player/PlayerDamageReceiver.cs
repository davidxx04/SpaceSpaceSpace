using System;
using UnityEngine;

// Punto de entrada de daño del jugador (su IDamageable). Intercepta los golpes ANTES de
// aplicarlos, en este orden:
//   1) Parry      -> si IsParrying y el ataque es parryable: se anula y se avisa al atacante.
//   2) I-frames   -> si IsInvulnerable (rol): se ignora.
//   3) Daño normal-> (futuro) reenviar a un Health del jugador.
public class PlayerDamageReceiver : MonoBehaviour, IDamageable
{
    [SerializeField] private PlayerController player;

    [Tooltip("Logs en consola para depurar el parry. Quítalo cuando ya no haga falta.")]
    [SerializeField] private bool logForDebug = true;

    // Para feedback (VFX de parry, sonido, puntuación...) y, en el futuro, la barra de vida.
    public event Action<DamageInfo> ParrySuccess;
    public event Action<DamageInfo> Hit;

    private void Awake()
    {
        if (player == null) player = GetComponent<PlayerController>();
    }

    public void TakeDamage(DamageInfo info)
    {
        // 1) Parry: ventana activa + ataque parreable.
        if (player.IsParrying && info.parryable)
        {
            if (logForDebug) Debug.Log("[Player] PARRY!");
            ParrySuccess?.Invoke(info);

            if (info.source != null && info.source.TryGetComponent<IParryable>(out var parried))
            {
                parried.OnParried();
            }
            return;
        }

        // 2) I-frames del rol.
        if (player.IsInvulnerable)
        {
            if (logForDebug) Debug.Log("[Player] i-frames: daño ignorado");
            return;
        }

        // 3) Daño normal (de momento el jugador aún no tiene Health).
        if (logForDebug) Debug.Log($"[Player] te habría dado: {info.amount}");
        Hit?.Invoke(info);
    }
}
