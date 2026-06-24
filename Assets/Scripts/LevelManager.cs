using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// Central level state machine.
/// GarethSetup step 6 creates the UI panels and wires all button/text references.
/// GarethSetup step 7 places checkpoints and creates the CheckpointManager.
/// </summary>
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    // ── Level content ─────────────────────────────────────────────────────────
    [Header("Level Content")]
    public string levelTitle   = "Level 2 · Ancient Gortyna";
    [TextArea(3, 6)]
    public string levelContext =
        "Circa 100 BCE. Gareth arrives in the ancient city of Gortyna — " +
        "the Roman capital of Crete.\n\n" +
        "Navigate the marble ruins and olive groves, avoid the Roman sentries, " +
        "and recover your target artefact.";
    public string artifactName = "The Law Code of Gortyn";
    [TextArea(2, 4)]
    public string artifactDescription =
        "One of the oldest and most complete legal codes in the Western world, " +
        "inscribed on the walls of ancient Gortyna circa 450 BCE.";

    // ── UI Panels (wired by GarethSetup step 6) ───────────────────────────────
    [Header("Panels")]
    public GameObject introPanel;
    public GameObject difficultyPanel;
    public GameObject pausePanel;
    public GameObject completePanel;
    public GameObject gameOverPanel;

    [Header("Text")]
    public Text introTitleText;
    public Text introContextText;
    public Text completeTitleText;
    public Text completeArtifactText;
    public Text completeDescText;

    [Header("Intro Button")]
    public Button btnIntroContinue;

    [Header("Difficulty Panel Buttons")]
    public Button btnDiffEasy;
    public Button btnDiffMedium;
    public Button btnDiffHard;
    public Button btnDiffImpossible;

    [Header("Pause Menu Buttons")]
    public Button btnResume;
    public Button btnPauseDiffEasy;
    public Button btnPauseDiffMedium;
    public Button btnPauseDiffHard;
    public Button btnPauseDiffImpossible;
    public Button btnRestart;
    public Button btnExitLevel;
    public Button btnExitGame;

    [Header("Complete Panel Button")]
    public Button btnCompleteContinue;

    [Header("Game Over Panel Buttons")]
    public Button btnGameOverRestart;
    public Button btnGameOverExit;

    // ── State ──────────────────────────────────────────────────────────────────
    public enum State { Intro, DifficultySelect, Playing, Paused, Complete, GameOver }
    public State CurrentState { get; private set; }

    PlayerController player;
    Vector3          playerStartPosition;
    bool             restartingFromDeath;

    // ── Lifecycle ──────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        player = Object.FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            playerStartPosition = player.transform.position;
            player.onDeath.AddListener(OnPlayerDeath);
        }

        WireButtons();

        if (introTitleText)   introTitleText.text   = levelTitle;
        if (introContextText) introContextText.text = levelContext;

        Enter(State.Intro);
    }

    void WireButtons()
    {
        Wire(btnIntroContinue,         OnIntroContinue);

        Wire(btnDiffEasy,              ChooseEasy);
        Wire(btnDiffMedium,            ChooseMedium);
        Wire(btnDiffHard,              ChooseHard);
        Wire(btnDiffImpossible,        ChooseImpossible);

        Wire(btnResume,                OnResume);
        Wire(btnPauseDiffEasy,         ChooseEasy);
        Wire(btnPauseDiffMedium,       ChooseMedium);
        Wire(btnPauseDiffHard,         ChooseHard);
        Wire(btnPauseDiffImpossible,   ChooseImpossible);
        Wire(btnRestart,               OnRestartLevel);
        Wire(btnExitLevel,             OnExitLevel);
        Wire(btnExitGame,              OnExitGame);

        Wire(btnCompleteContinue,      OnLevelComplete);

        Wire(btnGameOverRestart,       OnGameOverRestart);
        Wire(btnGameOverExit,          OnExitGame);
    }

    static void Wire(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (btn != null) btn.onClick.AddListener(action);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.escapeKey.wasPressedThisFrame)
        {
            if (CurrentState == State.Playing) Enter(State.Paused);
            else if (CurrentState == State.Paused) Enter(State.Playing);
        }
    }

    // ── State machine ──────────────────────────────────────────────────────────
    void Enter(State s)
    {
        CurrentState = s;
        HideAll();

        switch (s)
        {
            case State.Intro:
                Time.timeScale = 0f;
                Show(introPanel);
                break;

            case State.DifficultySelect:
                Time.timeScale = 0f;
                Show(difficultyPanel);
                break;

            case State.Playing:
                Time.timeScale = 1f;
                break;

            case State.Paused:
                Time.timeScale = 0f;
                Show(pausePanel);
                break;

            case State.Complete:
                Time.timeScale = 0f;
                if (completePanel != null)
                {
                    Show(completePanel);
                    if (completeTitleText)    completeTitleText.text    = "Artefact Recovered!";
                    if (completeArtifactText) completeArtifactText.text = artifactName;
                    if (completeDescText)     completeDescText.text     = artifactDescription;
                }
                break;

            case State.GameOver:
                Time.timeScale = 0f;
                Show(gameOverPanel);
                break;
        }
    }

    void HideAll()
    {
        if (introPanel)      introPanel.SetActive(false);
        if (difficultyPanel) difficultyPanel.SetActive(false);
        if (pausePanel)      pausePanel.SetActive(false);
        if (completePanel)   completePanel.SetActive(false);
        if (gameOverPanel)   gameOverPanel.SetActive(false);
    }

    static void Show(GameObject p) { if (p) p.SetActive(true); }

    // ── Button callbacks ───────────────────────────────────────────────────────
    public void OnIntroContinue() => Enter(State.DifficultySelect);

    public void ChooseEasy()       => SetDifficulty(999);
    public void ChooseMedium()     => SetDifficulty(5);
    public void ChooseHard()       => SetDifficulty(3);
    public void ChooseImpossible() => SetDifficulty(1);

    void SetDifficulty(int lives)
    {
        if (player != null)
        {
            if (restartingFromDeath)
            {
                player.Respawn(playerStartPosition, lives);
                restartingFromDeath = false;
            }
            else
            {
                player.SetLives(lives);
            }
        }
        Enter(State.Playing);
    }

    public void OnResume()       => Enter(State.Playing);
    public void OnRestartLevel() { Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
    public void OnExitLevel()    { Time.timeScale = 1f; SceneManager.LoadScene(0); }
    public void OnExitGame()     { Time.timeScale = 1f; Application.Quit(); }

    public void OnLevelComplete() { Time.timeScale = 1f; SceneManager.LoadScene(0); }

    public void OnPlayerDeath()
    {
        if (CurrentState == State.Playing) Enter(State.GameOver);
    }

    public void OnGameOverRestart()
    {
        restartingFromDeath = true;
        Enter(State.DifficultySelect);
    }

    // ── Called by ArtifactPickup ───────────────────────────────────────────────
    public void NotifyArtifactCollected()
    {
        if (CurrentState == State.Playing) Enter(State.Complete);
    }
}
