using UnityEngine;

// ============================================================================
//  SOLO PARA PRUEBAS — borra este archivo (y su GameObject en la escena) cuando
//  exista el boss. Ataca de forma intermitente con una Hitbox parreable apuntando a la
//  capa Player y enseña una caja roja mientras golpea, para poder probar el parry sin boss.
// ============================================================================
[RequireComponent(typeof(Hitbox))]
public class TestEnemyAttacker : MonoBehaviour, IParryable
{
    [SerializeField] private float interval = 2f;          // cada cuánto ataca
    [SerializeField] private float activeDuration = 0.35f; // dura el golpe (ventana para parrear)
    [SerializeField] private float damage = 10f;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer box;           // un sprite cuadrado del tamaño de la hitbox
    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField] private Color strikeColor = Color.red;

    private Hitbox hitbox;
    private float timer;
    private bool striking;
    private float strikeEndTime;

    private void Awake()
    {
        hitbox = GetComponent<Hitbox>();
    }

    private void OnEnable()
    {
        if (box != null) box.color = idleColor;
    }

    private void Update()
    {
        if (!striking)
        {
            timer += Time.deltaTime;
            if (timer >= interval) StartStrike();
        }
        else if (Time.time >= strikeEndTime)
        {
            EndStrike();
        }
    }

    private void StartStrike()
    {
        striking = true;
        timer = 0f;
        strikeEndTime = Time.time + activeDuration;
        if (box != null) box.color = strikeColor;

        // Ataque parreable hacia el jugador.
        var info = new DamageInfo(damage, gameObject, Vector2.zero) { parryable = true };
        hitbox.Activate(info);
    }

    private void EndStrike()
    {
        striking = false;
        if (box != null) box.color = idleColor;
        hitbox.Deactivate();
    }

    // Reacción de prueba al ser parreado: corta el golpe (stun simulado).
    public void OnParried()
    {
        Debug.Log("[TEST] Atacante parreado -> golpe cortado");
        EndStrike();
    }
}
