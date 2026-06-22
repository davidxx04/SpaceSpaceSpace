using UnityEngine;

// Parpadeo de un sprite por código: mientras 'Active' es true, enciende/apaga el SpriteRenderer a
// una cadencia configurable (el típico titileo de invulnerabilidad). Es GENÉRICO y reutilizable
// (i-frames del jugador, del boss, de un objeto recogible...): no sabe nada del juego; quien lo usa
// solo activa/desactiva 'Active'. Al desactivarse restaura la visibilidad, así nunca deja la nave
// invisible por accidente.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteBlink : MonoBehaviour
{
    [Tooltip("Parpadeos (ciclos encendido+apagado) por segundo.")]
    [SerializeField, Min(0.1f)] private float blinksPerSecond = 10f;

    private SpriteRenderer sr;
    private bool active;
    private float timer;

    // El driver (p. ej. PlayerHurtFx durante los i-frames) pone esto a true/false.
    public bool Active
    {
        get => active;
        set
        {
            if (active == value) return;
            active = value;
            timer = 0f;
            if (sr != null) sr.enabled = true;   // al arrancar y al parar, parte de visible
        }
    }

    private void Awake() => sr = GetComponent<SpriteRenderer>();

    // Por seguridad: si el GameObject se desactiva en mitad de un parpadeo, no dejarlo invisible.
    private void OnDisable()
    {
        if (sr != null) sr.enabled = true;
    }

    private void Update()
    {
        if (!active) return;

        float halfCycle = 0.5f / blinksPerSecond;   // medio ciclo: alterna encendido/apagado
        timer += Time.deltaTime;
        while (timer >= halfCycle)
        {
            timer -= halfCycle;
            sr.enabled = !sr.enabled;
        }
    }
}
