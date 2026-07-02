using System;

// Una fila de la tabla de puntuaciones: exactamente lo que se guarda por jugador.
// [Serializable] para que JsonUtility pueda escribirla/leerla (ver JsonLeaderboardStore).
// Es una clase (no struct) para poder guardarse por referencia en List<> sin sorpresas de copia.
[Serializable]
public class LeaderboardEntry
{
    public string nickname;
    public int score;

    // Constructor por defecto necesario para la deserialización de JsonUtility.
    public LeaderboardEntry() { }

    public LeaderboardEntry(string nickname, int score)
    {
        this.nickname = nickname;
        this.score = score;
    }
}
