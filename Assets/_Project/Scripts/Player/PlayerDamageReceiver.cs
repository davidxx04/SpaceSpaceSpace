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

    [Tooltip("Segundos de invulnerabilidad de cortesía tras recibir un golpe real (i-frames). Evita encadenar golpes. 0 = sin gracia.")]
    [SerializeField] private float hitInvulnerabilitySeconds = 1f;

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

            // Reacción de gameplay del propio jugador: habilita encadenar (re-parry / cancelar) al acertar.
            player.NotifyParrySuccess();
            return;
        }

        // 2) I-frames del rol.
        if (player.IsInvulnerable)
        {
            if (logForDebug) Debug.Log("[Player] i-frames: daño ignorado");
            return;
        }

        // 3) Daño normal (de momento el jugador aún no tiene Health).
        // Gracia: concede i-frames ANTES de avisar, para que los oyentes ya vean IsInvulnerable activo.
        player.GrantInvulnerability(hitInvulnerabilitySeconds);
        if (logForDebug) Debug.Log($"[Player] te habría dado: {info.amount}");
        Hit?.Invoke(info);
    }
}
