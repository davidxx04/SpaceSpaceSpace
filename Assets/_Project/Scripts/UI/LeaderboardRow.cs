using TMPro;
using UnityEngine;

// Vista de UNA fila de la tabla. Pura vista, sin lógica: LeaderboardPopup instancia este prefab
// por cada entrada y llama a Set(...). El rango no se guarda en los datos (es solo nickname +
// score); se deriva de la posición y se pinta aquí.
public class LeaderboardRow : MonoBehaviour
{
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text nicknameText;
    [SerializeField] private TMP_Text scoreText;

    public void Set(int rank, string nickname, int score)
    {
        if (rankText != null) rankText.text = rank.ToString();
        if (nicknameText != null) nicknameText.text = nickname;
        if (scoreText != null) scoreText.text = score.ToString();
    }
}
