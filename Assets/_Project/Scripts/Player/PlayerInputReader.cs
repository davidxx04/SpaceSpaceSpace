using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Aísla el Input System del resto del código del jugador.
// Los estados nunca hablan con ArcadeControls directamente: leen MoveInput y se
// suscriben a los eventos de aquí. Así hay un único punto donde re-mapear los
// controles para la cabina (un encoder I-PAC emite pulsaciones de teclado) sin tocar
// la lógica de juego.
public class PlayerInputReader : ArcadeControls.IPlayerActions, IDisposable
{
    private readonly ArcadeControls controls;

    // Vector de movimiento continuo (consultar cada frame).
    public Vector2 MoveInput { get; private set; }

    // Estado de "mantenido" del botón de ataque (para disparo automático; consultar cada frame).
    public bool AttackHeld => controls.Player.Attack.IsPressed();

    // Se disparan una vez al pulsar el botón correspondiente.
    public event Action RollPerformed;
    public event Action AttackPerformed;   // cableado, sin uso aún
    public event Action ParryPerformed;    // cableado, sin uso aún

    public PlayerInputReader()
    {
        controls = new ArcadeControls();
        controls.Player.AddCallbacks(this);
    }

    public void Enable() => controls.Player.Enable();
    public void Disable() => controls.Player.Disable();

    public void Dispose()
    {
        controls.Player.RemoveCallbacks(this);
        controls.Dispose();
    }

    // --- Callbacks de ArcadeControls.IPlayerActions ---

    public void OnMove(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();
    }

    public void OnRol(InputAction.CallbackContext context)
    {
        if (context.performed) RollPerformed?.Invoke();
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (context.performed) AttackPerformed?.Invoke();
    }

    public void OnParry(InputAction.CallbackContext context)
    {
        if (context.performed) ParryPerformed?.Invoke();
    }
}
