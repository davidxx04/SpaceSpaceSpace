using System.Collections.Generic;
using System.Linq;

// Fachada ESTÁTICA de la tabla de puntuaciones. Siguiendo la regla del proyecto (ver comentario
// de GameManager): la persistencia se alcanza por una API estática en código, no por un
// MonoBehaviour/singleton de escena — mismo espíritu que PoolManager.Spawn.
//
// Aquí vive toda la lógica de negocio (orden descendente, recorte a 100, cálculo de rango); el
// almacenamiento crudo se delega en un ILeaderboardStore intercambiable (local hoy, online el día
// de mañana). La lista se cachea en memoria y se recarga de forma perezosa en el primer acceso.
public static class Leaderboard
{
    // Tamaño máximo de la tabla (100 filas, según diseño).
    public const int MaxEntries = 100;

    // Backend de almacenamiento. Local por defecto; sustituible por tests o por un futuro online.
    private static ILeaderboardStore backend = new JsonLeaderboardStore();

    // Cache en memoria, SIEMPRE ordenada por score descendente. null = aún no cargada.
    private static List<LeaderboardEntry> cache;

    // Inyecta otro backend (tests / futuro ranking online). Invalida la cache para releer.
    public static void SetBackend(ILeaderboardStore store)
    {
        backend = store;
        cache = null;
    }

    // Todas las entradas, ordenadas de mayor a menor score (solo lectura).
    public static IReadOnlyList<LeaderboardEntry> GetAll()
    {
        EnsureLoaded();
        return cache;
    }

    // ¿Un score de este valor entraría en la tabla? Para el enganche futuro de introducir nombre:
    // solo se pide el nickname si la puntuación va a figurar. Hay hueco si no está llena o si supera
    // a la más baja actual.
    public static bool Qualifies(int score)
    {
        EnsureLoaded();
        if (cache.Count < MaxEntries) return true;
        return score > cache[cache.Count - 1].score;
    }

    // Rango 1-based en el que ENTRARÍA un score si se insertara ahora, sin insertarlo (-1 si no
    // califica). Para el mensaje "TOP X" de la pantalla de nickname, que se muestra ANTES de escribir
    // el nombre. Mismo criterio de empates que Insert: la entrada nueva queda por debajo de las que ya
    // tienen ese score, así que su posición es "cuántas entradas tienen score >= el mío" + 1.
    public static int PeekRank(int score)
    {
        if (!Qualifies(score)) return -1;

        EnsureLoaded();
        int above = 0;
        for (int i = 0; i < cache.Count; i++)
            if (cache[i].score >= score) above++;
        return above + 1;
    }

    // Vacía la tabla y persiste el borrado. Pensado para reset/pruebas (p. ej. LeaderboardSeeder).
    public static void Clear()
    {
        cache = new List<LeaderboardEntry>();
        backend.Save(cache);
    }

    // Inserta una entrada, reordena, recorta a MaxEntries y persiste. Devuelve el rango 1-based
    // resultante, o -1 si no llegó a entrar en la tabla. (Para el enganche futuro de nickname.)
    public static int Insert(string nickname, int score)
    {
        EnsureLoaded();

        var entry = new LeaderboardEntry(Sanitize(nickname), score);
        cache.Add(entry);

        // OrderByDescending es estable: ante empates conserva el orden previo, así una entrada nueva
        // queda POR DEBAJO de las que ya tenían ese score (no desbanca a quien empata).
        cache = cache.OrderByDescending(e => e.score).ToList();

        int index = cache.IndexOf(entry);

        // Recorta al máximo antes de guardar.
        if (cache.Count > MaxEntries)
            cache.RemoveRange(MaxEntries, cache.Count - MaxEntries);

        backend.Save(cache);

        // Si quedó fuera del recorte, no figura.
        return (index >= 0 && index < MaxEntries) ? index + 1 : -1;
    }

    private static void EnsureLoaded()
    {
        if (cache != null) return;
        // El backend nunca lanza ni devuelve null; aun así ordenamos por si el fichero venía
        // desordenado (editado a mano) y recortamos por si traía de más.
        cache = backend.Load().OrderByDescending(e => e.score).ToList();
        if (cache.Count > MaxEntries)
            cache.RemoveRange(MaxEntries, cache.Count - MaxEntries);
    }

    private static string Sanitize(string nickname)
    {
        return string.IsNullOrWhiteSpace(nickname) ? "---" : nickname.Trim();
    }
}
