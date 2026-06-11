using UnityEngine;
using UnityEngine.SceneManagement;

// Persistent game manager that owns scene flow between the Menu and Game scenes.
// Lives for the whole session (DontDestroyOnLoad) so any scene can reach it via Instance.
public class GameManager : MonoBehaviour
{

    public const string MenuScene = "Menu";
    public const string GameScene = "Game";

    public static GameManager Instance { get; private set; }

    public enum GameState
    {
        Menu,
        Playing
    }

    // Current high-level state, useful for UI and gameplay scripts.
    public GameState State { get; private set; }

    void Awake()
    {
        // Enforce a single instance that survives scene loads.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    // Load the main menu.
    public void LoadMenu()
    {
        LoadScene(MenuScene);
    }

    // Start a new game by loading the gameplay scene.
    public void StartGame()
    {
        LoadScene(GameScene);
    }

    // Quit the application (or stop play mode in the editor).
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void LoadScene(string sceneName)
    {
        // Always run unscaled in case a paused game left Time.timeScale at 0.
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Keep State in sync with whatever scene is now active.
        State = scene.name == GameScene ? GameState.Playing : GameState.Menu;
    }
}
