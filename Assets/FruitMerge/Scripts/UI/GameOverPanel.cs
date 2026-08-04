using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sonuç ekranı: skor, rekor, sırayla dolan yıldızlar, rekor kırıldıysa şerit.
///
/// Açılış sırası zamanlanmış:
///   0.00  panel açılır, game_over.wav çalar (panel_open KAPALI — aynı bantta çakışıyor)
///   0.70  1. yıldız dolar + star.wav
///   1.05  2. yıldız + star.wav (bir tık tiz)
///   1.40  3. yıldız + star.wav
///   +0.30 rekor kırıldıysa şerit + new_record.wav
///
/// Sesler üst üste binmesin diye kademeli. <c>AudioService.PlayStar(index)</c> pitch'i
/// indeksle yükseltiyor, yani yıldızlar çıkan bir arpej gibi duyuluyor.
///
/// Kendi <c>Update</c>'ini TANIMLAMIYOR: Unity yalnızca en türemiş Update'i çağırır ve
/// bu <see cref="UIPanel"/>'in fade'ini sessizce durdururdu. Bunun yerine
/// <see cref="OnTick"/> override ediliyor.
/// </summary>
[DefaultExecutionOrder(100)]
public class GameOverPanel : UIPanel
{
    [Header("Metinler")]
    [SerializeField] TextMeshProUGUI _scoreLabel;
    [SerializeField] TextMeshProUGUI _bestLabel;

    [Header("Butonlar")]
    [SerializeField] Button _restartButton;

    [SerializeField] Button _menuButton;

    [Header("Yıldızlar")]
    [Tooltip("soldan sağa 3 yıldız")]
    [SerializeField] Image[] _stars = new Image[3];

    [SerializeField] Sprite _starEmpty;
    [SerializeField] Sprite _starFilled;

    [Header("Yeni rekor")]
    [Tooltip("rekor kırılmadıysa gizli kalan şerit")]
    [SerializeField] GameObject _newRecordRibbon;

    [Header("Ayar")]
    [SerializeField] GameConfig _config;

    // panel açılış sesi yok — game_over.wav (220 Hz + 300-800 Hz harmonikleri) ile
    // panel_open (520 Hz) aynı bantta çakışıyor
    protected override bool PlaysOpenSfx => false;

    int _starTarget;
    int _starsShown;
    float _revealTimer;
    bool _revealing;

    bool _newRecordPending;
    bool _newRecordShown;

    float[] _punch;
    Vector3[] _starBaseScale;

    protected override void Awake()
    {
        base.Awake();

        int count = _stars != null ? _stars.Length : 0;

        _punch = new float[count];
        _starBaseScale = new Vector3[count];

        for (int i = 0; i < count; i++)
            _starBaseScale[i] = _stars[i] != null ? _stars[i].rectTransform.localScale : Vector3.one;

        if (_restartButton != null) _restartButton.onClick.AddListener(HandleRestartClicked);
        if (_menuButton != null)    _menuButton.onClick.AddListener(HandleMenuClicked);

        if (_newRecordRibbon != null) _newRecordRibbon.SetActive(false);
    }

    void OnDestroy()
    {
        if (_restartButton != null) _restartButton.onClick.RemoveListener(HandleRestartClicked);
        if (_menuButton != null)    _menuButton.onClick.RemoveListener(HandleMenuClicked);
    }

    void OnEnable()
    {
        GameEvents.OnGameOver     += HandleGameOver;
        GameEvents.OnNewRecord    += HandleNewRecord;
        GameEvents.OnRunStarted   += HandleRunStarted;
        GameEvents.OnStateChanged += HandleStateChanged;
    }

    void OnDisable()
    {
        GameEvents.OnGameOver     -= HandleGameOver;
        GameEvents.OnNewRecord    -= HandleNewRecord;
        GameEvents.OnRunStarted   -= HandleRunStarted;
        GameEvents.OnStateChanged -= HandleStateChanged;
    }

    /// <summary>
    /// Panel GameOver dışına çıkınca kapanmalı. Eskiden hiç Hide() çağrılmıyordu:
    /// MENU'ye basınca panel açık kalıyor, üstüne menü biniyordu; PLAY'e basıp menü
    /// kapanınca sonuç ekranı hâlâ ortada duruyordu.
    /// </summary>
    void HandleStateChanged(GameState s)
    {
        if (s == GameState.GameOver) return;

        if (IsOpen) Hide();

        _revealing = false;
    }

    // ---------------------------------------------------------------- olaylar

    /// <summary>
    /// OnGameOver ile OnNewRecord'un abone sırası garanti değil, o yüzden bayrakla
    /// tutuyoruz. Yıldız gösterimi zaten gecikmeli başladığı için sıra ne olursa olsun
    /// şerit doğru zamanda çıkıyor.
    /// </summary>
    void HandleNewRecord(int score) => _newRecordPending = true;

    void HandleRunStarted()
    {
        _newRecordPending = false;
        _newRecordShown = false;
    }

    void HandleGameOver(int finalScore)
    {
        if (_scoreLabel != null) _scoreLabel.SetText("{0}", finalScore);

        if (_bestLabel != null && SaveService.Instance != null)
            _bestLabel.SetText("{0}", SaveService.Instance.HighScore);

        ResetStars();

        if (_newRecordRibbon != null) _newRecordRibbon.SetActive(false);

        _newRecordShown = false;
        _starTarget = StarsFor(finalScore);
        _starsShown = 0;
        _revealTimer = _config != null ? _config.starRevealDelay : 0.7f;
        _revealing = true;

        Show();
    }

    int StarsFor(int score)
    {
        if (_config == null) return 0;

        if (score >= _config.star3Score) return 3;
        if (score >= _config.star2Score) return 2;
        if (score >= _config.star1Score) return 1;

        return 0;
    }

    // ------------------------------------------------------------------ döngü

    protected override void OnTick(float dt)
    {
        // Panel kapalıyken yapacak iş yok: yıldız punch'ı da gösterim sayacı da yalnızca
        // panel açıkken anlamlı. Oyunun neredeyse tamamı bu satırda bitiyor — eskiden
        // TickPunch her karede 3 yıldızlık döngüyü boşa dönüyordu.
        if (!IsOpen && !_revealing) return;

        TickPunch(dt);

        if (!_revealing) return;

        _revealTimer -= dt;

        if (_revealTimer > 0f) return;

        if (_starsShown < _starTarget)
        {
            RevealStar(_starsShown);

            _starsShown++;

            _revealTimer = _config != null ? _config.starRevealInterval : 0.35f;

            return;
        }

        if (_newRecordPending && !_newRecordShown)
        {
            ShowNewRecord();

            _newRecordShown = true;
        }

        _revealing = false;

        // Yıldızlar yerine oturdu — coin ödülü artık akabilir. Bu olay OnGameOver'dan
        // ayrı: ödül paraları yıldızlardan çıktığı için yıldızların dolmasını beklemek
        // zorunda. Yıldız kazanılmasa da (0) yayınlanıyor, meyve ödülü yine verilecek.
        GameEvents.RaiseStarsRevealed(_starTarget);
    }

    void RevealStar(int index)
    {
        if (_stars == null || index < 0 || index >= _stars.Length) return;

        if (_stars[index] != null && _starFilled != null) _stars[index].sprite = _starFilled;

        _punch[index] = 1f;

        if (AudioService.Instance != null) AudioService.Instance.PlayStar(index);

        // Titreşim sesle AYNI karede: yıldızın dolması tek bir olay olarak hissedilsin
        if (HapticService.Instance != null) HapticService.Instance.PlayStar(index);
    }

    void ShowNewRecord()
    {
        if (_newRecordRibbon != null) _newRecordRibbon.SetActive(true);

        if (AudioService.Instance != null) AudioService.Instance.PlayNewRecord();

        // Rekor titreşimi OnNewRecord'a bağlanamaz: o olay oyun sonunda, kaybetme
        // titreşiminin tam üstünde yayınlanıyor. Şerit BURADA çıkıyor, titreşim de burada.
        if (HapticService.Instance != null) HapticService.Instance.PlayNewRecord();

        // Konfeti de aynı gerekçeyle burada: GameEvents.OnNewRecord oyun sonunda,
        // şerit henüz çıkmamışken yayınlanıyor. Kutlama şeridin çıktığı ANDA olmalı.
        if (ConfettiDirector.Instance != null) ConfettiDirector.Instance.PlayRain();
    }

    void ResetStars()
    {
        if (_stars == null) return;

        for (int i = 0; i < _stars.Length; i++)
        {
            if (_stars[i] != null)
            {
                if (_starEmpty != null) _stars[i].sprite = _starEmpty;

                _stars[i].rectTransform.localScale = _starBaseScale[i];
            }

            _punch[i] = 0f;
        }
    }

    /// <summary>Beliren yıldızın şişip geri oturması.</summary>
    void TickPunch(float dt)
    {
        if (_stars == null) return;

        float duration = _config != null ? Mathf.Max(0.01f, _config.starPunchDuration) : 0.22f;
        float peak     = _config != null ? _config.starPunchScale : 1.7f;

        for (int i = 0; i < _stars.Length; i++)
        {
            if (_punch[i] <= 0f) continue;

            _punch[i] -= dt / duration;

            if (_stars[i] == null) { _punch[i] = 0f; continue; }

            if (_punch[i] <= 0f)
            {
                _punch[i] = 0f;
                _stars[i].rectTransform.localScale = _starBaseScale[i];
                continue;
            }

            float t = 1f - _punch[i];
            float s = Mathf.Lerp(peak, 1f, t * t * (3f - 2f * t));

            _stars[i].rectTransform.localScale = _starBaseScale[i] * s;
        }
    }

    // ---------------------------------------------------------------- butonlar

    void HandleRestartClicked()
    {
        if (AudioService.Instance != null) AudioService.Instance.PlayUIClick();

        if (GameManager.Instance != null) GameManager.Instance.Restart();
    }

    // ui_click çalmıyoruz: MENU'ye geçince bu panel kapanıyor ve panel_close (1368 Hz)
    // ile ui_click (1475 Hz) neredeyse aynı bantta, üst üste tek bulanık tık oluyor
    void HandleMenuClicked()
    {
        if (GameManager.Instance != null) GameManager.Instance.GoToMenu();
    }
}
