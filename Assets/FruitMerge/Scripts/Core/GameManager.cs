using UnityEngine;

[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState State { get; private set; } = GameState.Boot;

    public bool IsPlaying => State == GameState.Playing;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnEnable()
    {
        GameEvents.OnGameOver += HandleGameOver;
    }

    void OnDisable()
    {
        GameEvents.OnGameOver -= HandleGameOver;
    }

    // Oyun Boot'ta başlıyor (State'in varsayılanı) ve buna ayrı bir geçiş olayı
    // ateşlenmiyor. Boot -> Menu geçişini SplashPanel, sahte yükleme çubuğu dolunca
    // GoToMenu() çağırarak yapıyor.

    /// <summary>Menüdeki PLAY.</summary>
    public void Play() => StartNewRun();

    /// <summary>
    /// Yeni oyun. OnRunStarted'ı HER ZAMAN yayınlar — bu yüzden SetState'in içinden
    /// çıkarıldı: orada "Paused'dan geliyorsa yayınlama" kuralı vardı ve pause'dan
    /// RESTART'a basınca skor sıfırlanmıyordu.
    /// </summary>
    void StartNewRun()
    {
        Time.timeScale = 1f;

        SetState(GameState.Playing);

        GameEvents.RaiseRunStarted();
    }

    /// <summary>
    /// Pause veya sonuç ekranındaki MENU. Sahne yeniden yüklenmiyor —
    /// tahtayı temizlemek DropController'ın işi (OnStateChanged(Menu)).
    /// </summary>
    public void GoToMenu()
    {
        if (State == GameState.Menu) return;

        Time.timeScale = 1f;

        SetState(GameState.Menu);
    }

    void SetState(GameState next)
    {
        if (State == next) return;

        ExitState(State);
        State = next;
        EnterState(State);

        GameEvents.RaiseStateChanged(next);
    }

    void EnterState(GameState s)
    {
        switch (s)
        {
            case GameState.Menu:
                Time.timeScale = 1f;
                break;
            case GameState.Playing:
                Time.timeScale = 1f;
                break;
            case GameState.Paused:
                Time.timeScale = 0f;
                break;
            case GameState.GameOver:
                break;
        }
    }

    void ExitState(GameState s)
    {

    }

    public void Pause()
    {
        if (State == GameState.Playing)
        {
            SetState(GameState.Paused);
        }
    }

    public void Resume()
    {
        if (State == GameState.Paused)
        {
            SetState(GameState.Playing);
        }
    }

    /// <summary>
    /// Pause veya sonuç ekranındaki RESTART. Sahneyi YENİDEN YÜKLEMİYOR.
    ///
    /// Eskiden LoadScene çağırıyordu; Start() artık Menu'ye girdiği için yeniden yükleme
    /// oyuncuyu menüye atıyordu. Yumuşak sıfırlama hem doğru davranış hem de yükleme
    /// takılması olmuyor — tahtayı DropController, skoru ScoreSystem OnRunStarted'da
    /// temizliyor.
    /// </summary>
    public void Restart() => StartNewRun();

    void HandleGameOver(int score)
    {
        SetState(GameState.GameOver);
    }

    public void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}