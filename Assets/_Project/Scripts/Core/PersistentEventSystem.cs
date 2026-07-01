using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

// Mantiene UN ÚNICO EventSystem para toda la sesión (mismo patrón singleton que GameManager).
//
// Por qué existe: cada escena traía su propio EventSystem y encima con módulos de input distintos
// (Menu = StandaloneInputModule legacy, Game = InputSystemUIInputModule nuevo). Alternar escenas
// reconstruía el módulo de UI en cada carga y acababa dejando el input del menú muerto tras un par
// de partidas. Con un único EventSystem persistente, el módulo de UI se crea UNA vez y no se
// reconstruye nunca, así que ese bug desaparece de raíz.
//
// Se coloca en el EventSystem de la escena Menu (la primera que carga). Ese EventSystem debe usar el
// "Input System UI Input Module" (nuevo Input System), coherente con el resto del proyecto.
[RequireComponent(typeof(EventSystem))]
public class PersistentEventSystem : MonoBehaviour
{
    private static PersistentEventSystem instance;

    private void Awake()
    {
        // Fuerza una sola instancia que sobreviva a los cambios de escena.
        if (instance != null && instance != this)
        {
            Destroy(gameObject);   // duplicado (p.ej. al recargar Menu) -> fuera
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // Copia PRIVADA del InputActionAsset del módulo. El asset de acciones de UI (por defecto uno
        // compartido) no lleva conteo de referencias: si otro EventSystem transitorio se destruye al
        // cambiar de escena, su OnDisable llama Disable() sobre ese asset y apagaría también NUESTRAS
        // acciones. Con una copia propia, nadie más puede dejarnos sin input.
        var module = GetComponent<InputSystemUIInputModule>();
        if (module != null && module.actionsAsset != null)
            module.actionsAsset = Instantiate(module.actionsAsset);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    // Red de seguridad: si una escena recién cargada trae su propio EventSystem (Game/Leaderboard),
    // lo elimina. Tener dos EventSystems activos hace que Unity desactive uno y el input se vuelve
    // impredecible. Lo ideal es borrar esos EventSystems de las otras escenas; esto cubre despistes.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        var systems = FindObjectsOfType<EventSystem>();
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] != null && systems[i].gameObject != gameObject)
                Destroy(systems[i].gameObject);
        }
    }
}
