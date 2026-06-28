using System;
using System.Collections;
using UnityEngine;

// Parte destruible y regenerable del boss. Vive en un HIJO del boss, en la capa Enemy, con su propio
// Health + Collider2D (las balas del jugador ya le pegan: su Hitbox resuelve GetComponentInParent
// <IDamageable>, así que la parte responde sola). Al destruirse da puntuación y reaparece tras un delay.
//
// v1: SOLO puntuación. NO toca los ataques del boss (las secuencias memorizables siguen fijas).
// El core del boss tiene su PROPIO Health, independiente de las partes.
[RequireComponent(typeof(Health))]
public class BossPart : MonoBehaviour
{
    [Tooltip("Puntos que otorga al destruirse.")]
    [SerializeField] private int scoreValue = 100;

    [Tooltip("Tiempo hasta que la parte regenera y vuelve a ser destruible, en segundos.")]
    [SerializeField] private float regenDelay = 8f;

    [Tooltip("Objeto visual a ocultar mientras está destruida (opcional; por defecto este GameObject).")]
    [SerializeField] private GameObject visual;

    [Tooltip("Collider a desactivar mientras está destruida (opcional; por defecto el de este objeto).")]
    [SerializeField] private Collider2D bodyCollider;

    // Lo escuchará el futuro ScoreManager / tabla de high-scores. Estático para no acoplar nada todavía.
    public static event Action<int> Destroyed;

    private Health health;

    private void Awake()
    {
        health = GetComponent<Health>();
        if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
    }

    private void OnEnable() => health.Died += OnDied;
    private void OnDisable() => health.Died -= OnDied;

    private void OnDied()
    {
        Destroyed?.Invoke(scoreValue);
        SetDestroyedVisualState(true);
        StartCoroutine(RegenAfterDelay());
    }

    private IEnumerator RegenAfterDelay()
    {
        yield return new WaitForSeconds(regenDelay);
        health.ResetHealth();              // Current vuelve a Max -> de nuevo "viva" y dañable
        SetDestroyedVisualState(false);
    }

    private void SetDestroyedVisualState(bool destroyed)
    {
        GameObject vis = visual != null ? visual : gameObject;
        if (vis != gameObject) vis.SetActive(!destroyed);   // no desactivamos este GO (cortaría la corrutina)
        if (bodyCollider != null) bodyCollider.enabled = !destroyed;
    }
}
