using UnityEngine;

// Utilidad DESECHABLE para probar el leaderboard antes de que exista la entrada de nickname al
// terminar la partida. Rellena la tabla con datos de ejemplo para ver el popup y el scroll con las
// 100 filas. Usa los context-menus del componente (clic derecho sobre él en el Inspector); no crea
// comandos de menú del editor. Borrable (fichero + objeto) igual que TestEnemyAttacker.
public class LeaderboardSeeder : MonoBehaviour
{
    [Tooltip("Cuántas entradas de ejemplo generar al 'Seed test data'.")]
    [SerializeField] private int count = 100;

    [SerializeField]
    private string[] sampleNames =
    {
        "ACE", "NOVA", "REX", "KAI", "ZED", "IRIS", "MAX", "LUNA", "ODIN", "VEGA",
        "NEO", "RIA", "JAX", "MIA", "LEO", "SOL", "FOX", "ARI", "DOC", "PIX"
    };

    [ContextMenu("Seed test data")]
    private void Seed()
    {
        Leaderboard.Clear();
        for (int i = 0; i < count; i++)
        {
            string name = sampleNames[Random.Range(0, sampleNames.Length)] + Random.Range(1, 100);
            Leaderboard.Insert(name, Random.Range(500, 100000));
        }
        Debug.Log($"[LeaderboardSeeder] Insertadas {count} entradas de ejemplo.");
    }

    [ContextMenu("Clear leaderboard")]
    private void Clear()
    {
        Leaderboard.Clear();
        Debug.Log("[LeaderboardSeeder] Tabla vaciada.");
    }
}
