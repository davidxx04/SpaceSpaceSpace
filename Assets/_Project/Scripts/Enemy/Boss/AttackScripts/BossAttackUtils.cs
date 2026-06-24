using UnityEngine;

// Matemática reutilizable para escribir ataques diminutos. Sin estado: solo helpers puros.
// Así un ataque nuevo se reduce a "pedir unas direcciones y disparar".
public static class BossAttackUtils
{
    public static float DirToAngle(Vector2 dir) => Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

    public static Vector2 AngleToDir(float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
    }

    // Abanico de 'count' direcciones centrado en baseDir, abarcando 'spreadDeg' en total
    // (mismo reparto que el disparo en abanico del jugador en AttackState.Fire).
    public static Vector2[] Fan(Vector2 baseDir, int count, float spreadDeg)
    {
        count = Mathf.Max(1, count);
        var dirs = new Vector2[count];
        float baseAngle = DirToAngle(baseDir);
        float start = baseAngle - spreadDeg * 0.5f;
        float step = count > 1 ? spreadDeg / (count - 1) : 0f;
        for (int i = 0; i < count; i++)
            dirs[i] = AngleToDir(count > 1 ? start + step * i : baseAngle);
        return dirs;
    }

    // 'count' direcciones repartidas uniformemente en 360° (anillo bullet-hell), con un offset inicial.
    public static Vector2[] Radial(int count, float startDeg = 0f)
    {
        count = Mathf.Max(1, count);
        var dirs = new Vector2[count];
        float step = 360f / count;
        for (int i = 0; i < count; i++) dirs[i] = AngleToDir(startDeg + step * i);
        return dirs;
    }
}
