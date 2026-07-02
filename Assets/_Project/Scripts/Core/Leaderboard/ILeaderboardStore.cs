using System.Collections.Generic;

// Seam de intercambio local <-> online. La fachada Leaderboard contiene TODA la lógica de
// negocio (ordenar, recortar a 100, calcular rango); un backend solo se encarga del
// almacenamiento crudo de la lista. Hoy hay una única implementación local (JsonLeaderboardStore);
// si algún día se quiere un ranking online, se implementa esta misma interfaz contra el servicio
// y se inyecta con Leaderboard.SetBackend — sin tocar ni la UI ni el flujo de partida.
//
// Nota: un backend online real sería asíncrono; cuando llegue, se adaptará la fachada. El diseño
// síncrono actual es el correcto para almacenamiento local.
public interface ILeaderboardStore
{
    // Carga las entradas guardadas. Nunca lanza: si no hay datos o están corruptos, devuelve
    // una lista vacía (el llamador no debe defenderse de nulls ni excepciones).
    List<LeaderboardEntry> Load();

    // Persiste la lista tal cual (la fachada ya la entrega ordenada y recortada).
    void Save(List<LeaderboardEntry> entries);
}
