using UnityEngine;
using UnityEngine.SceneManagement;

// Scene-scoped flow controller: owns the transitions between the Menu and Game scenes.
//
// NOT persistent on purpose. It holds no state that must survive a scene load (State is derived from
// the current scene), so making it DontDestroyOnLoad only caused trouble: the scene copy that UI
// buttons reference by Inspector got destroyed by the singleton guard on reload, leaving the Start
// button wired to a dead object. Each scene that needs flow control simply has its own GameManager;
// UI references the live scene instance and everything stays valid across Menu <-> Game <-> Menu.
//
// Rule of thumb: persist ONLY what must survive a load, and reach persistent singletons via a static
// API in code (like PoolManager.Spawn), never via Inspector references to their scene copy.
public class GameManager : MonoBehaviour
{
    public const string MenuScene = "Menu";
    public const string GameScene = "Game";

    // Convenience accessor to the current scene's GameManager. Set in Awake, cleared in OnDestroy.
    // May be null in scenes that don't contain a GameManager (callers must handle that).
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
        Instance = this;   // the current scene's manager wins; the next scene's takes over on load
        State = gameObject.scene.name == GameScene ? GameState.Playing : GameState.Menu;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
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
        // Always run unscaled in case a paused/ended game left Time.timeScale at 0.
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }
}
