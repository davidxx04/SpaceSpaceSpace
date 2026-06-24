# Boss — arquitectura y cómo iterar

3 capas + una FSM de alto nivel. La idea: separar **qué** se hace, **en qué orden** y **con qué ritmo**.

```
BossAttackSO   (asset)  -> UNA acción atómica (telegraph -> activo -> recuperación). parryable: bh=false / sekiro=true
BossComboSO    (asset)  -> SECUENCIA FIJA de ataques + delays (lo que el jugador memoriza)
BossAttackDirector (code)-> decide suelto vs combo y el ritmo, según la fase
BossPhaseData  (asset)  -> repertorio + agresividad por umbral de vida
FSM: Intro -> Combat -> Stagger -> Defeated   (BossController es el hub/contexto)
```

## Crear un ataque nuevo (el flujo de iteración)
1. Nueva clase en `Attacks/`:
   ```csharp
   [CreateAssetMenu(menuName = "SpaceSpaceSpace/Boss/Attacks/Mi Ataque", fileName = "MiAtaque")]
   public class MiAtaqueSO : BossAttackSO
   {
       public float damage = 10f;
       public override IEnumerator Execute(BossContext ctx)
       {
           yield return Telegraph(ctx);                 // aviso
           Vector2 o = ctx.MuzzlePosition(0);
           foreach (var d in BossAttackUtils.Radial(12)) // p.ej. anillo de 12 balas
               ctx.Spawn(o, d, 8f, 20f, false, damage, parryable);
           yield return Recover();
       }
   }
   ```
2. En el Project: **Create → SpaceSpaceSpace/Boss/Attacks/Mi Ataque** y tunea los números en el Inspector.
3. Arrastra el `.asset` a un `BossPhaseData` (`singles`) o a un `BossComboSO` (`sequence`).
   Ningún otro archivo cambia. Puedes editar valores **en Play** y persisten (como `RollData`).

Reglas: las SO son assets **compartidos** → no guardes estado de ejecución en campos; usa locales
dentro de `Execute`. Helpers listos: `ctx.AimToPlayer`, `ctx.Spawn`, `ctx.MuzzlePosition`,
`BossAttackUtils.Fan/Radial/AngleToDir`, y `Telegraph(ctx)` / `Recover()` de la clase base.

## Combos y fases
- **Combo** = `BossComboSO`: arrastra ataques en orden + `delayAfter`. Etiqueta `type` (BulletHell/Sekiro).
- **Fase** = `BossPhaseData`: `enterAtHealthFraction` (umbral de vida), `useCombos`, `singles[]`,
  `combos[]`, `selection` (Sequential = memorizable / Random) y `gapRange` (agresividad).
- En `BossController.phases[]` pon las fases **ordenadas de más vida a menos** (1.0, 0.66, 0.33...).
  El disparador es la vida del core; cambiarlo (tiempo, postura...) = editar `BossController.UpdatePhase()`.

## Cableado en el Editor (checklist)
- **Capas**: boss core y cada parte en `Enemy (7)`. Las balas del boss apuntan a `Player (6)`.
- **Boss (raíz)**: `BossController` + `Health` (core) + `Collider2D` (en Enemy) + `BossAttackDirector`
  (+ `BossMovement` cuando toque). Asigna en el Inspector: core, director, muzzles, `bossProjectile`, phases.
- **Bala del boss**: duplica el prefab de bala del jugador, pon su `Hitbox.targetLayers = Player`,
  cámbiale el sprite. Asígnala en `BossController.bossProjectile`.
- **Partes**: hijos en `Enemy` con `Collider2D` + `Health` + `BossPart` (+ `DamageFlash` opcional).
- **Arena**: GameObject vacío con `ArenaBounds` en la escena (capa de pared que colisione con Player).
- **Muzzles**: hijos vacíos del boss donde quieras que salgan las balas (opcional; si no, sale del centro).

## Parry / Stagger
El `source` de cada bala es el boss → al parrear, `BossController.OnParried()` suma postura. Al llegar a
`staggerParryThreshold`, entra en `Stagger` (ventana de castigo). Un parry **no** corta el combo
(las balas parreables desaparecen solas al impactar): así las secuencias siguen siendo memorizables.
