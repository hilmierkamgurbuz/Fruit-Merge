using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Duraklatma paneli + ayarlar. Ayrı bir ayarlar ekranı yok, hepsi burada.
///
/// Panel state machine'e bağlı, butona bağlı değil: GameManager state'i
/// Paused'a çevirir, panel OnStateChanged'i duyup kendini açar. Tek doğru kaynak
/// GameManager kalır, panel ile oyun durumu asla ayrışmaz.
/// </summary>
[DefaultExecutionOrder(100)]
public class PausePanel : UIPanel
{
    [Header("Butonlar")]
    [SerializeField] Button _resumeButton;
    [SerializeField] Button _restartButton;

    [Tooltip("sağ üstteki çarpı — devam ile aynı işi yapar")]
    [SerializeField] Button _closeButton;

    [SerializeField] Button _menuButton;

    [Header("Ses ayarı")]
    [SerializeField] Button _sfxButton;
    [SerializeField] Image  _sfxIcon;
    [SerializeField] Sprite _sfxOnSprite;
    [SerializeField] Sprite _sfxOffSprite;

    [Header("Müzik ayarı")]
    [SerializeField] Button _musicButton;
    [SerializeField] Image  _musicIcon;
    [SerializeField] Sprite _musicOnSprite;
    [SerializeField] Sprite _musicOffSprite;

    [Header("Titreşim ayarı")]
    [SerializeField] Button _vibrationButton;
    [SerializeField] Image  _vibrationIcon;
    [SerializeField] Sprite _vibrationOnSprite;
    [SerializeField] Sprite _vibrationOffSprite;

    protected override void Awake()
    {
        base.Awake();

        if (_resumeButton != null)     _resumeButton.onClick.AddListener(HandleResumeClicked);
        if (_closeButton != null)      _closeButton.onClick.AddListener(HandleResumeClicked);
        if (_restartButton != null)    _restartButton.onClick.AddListener(HandleRestartClicked);
        if (_sfxButton != null)        _sfxButton.onClick.AddListener(HandleSfxClicked);
        if (_musicButton != null)      _musicButton.onClick.AddListener(HandleMusicClicked);
        if (_vibrationButton != null)  _vibrationButton.onClick.AddListener(HandleVibrationClicked);
        if (_menuButton != null)       _menuButton.onClick.AddListener(HandleMenuClicked);
    }

    void OnDestroy()
    {
        if (_resumeButton != null)     _resumeButton.onClick.RemoveListener(HandleResumeClicked);
        if (_closeButton != null)      _closeButton.onClick.RemoveListener(HandleResumeClicked);
        if (_restartButton != null)    _restartButton.onClick.RemoveListener(HandleRestartClicked);
        if (_sfxButton != null)        _sfxButton.onClick.RemoveListener(HandleSfxClicked);
        if (_musicButton != null)      _musicButton.onClick.RemoveListener(HandleMusicClicked);
        if (_vibrationButton != null)  _vibrationButton.onClick.RemoveListener(HandleVibrationClicked);
        if (_menuButton != null)       _menuButton.onClick.RemoveListener(HandleMenuClicked);
    }

    void OnEnable()  { GameEvents.OnStateChanged += HandleStateChanged; }
    void OnDisable() { GameEvents.OnStateChanged -= HandleStateChanged; }

    void HandleStateChanged(GameState s)
    {
        // IsOpen kontrolü şart: açılışta state Playing'e geçiyor, kapalı paneli
        // bir daha kapatmaya çalışırsak panel_close sesi boşa çalar
        if (s == GameState.Paused)
        {
            if (!IsOpen) Show();
        }
        else
        {
            if (IsOpen) Hide();
        }
    }

    protected override void OnShow() => RefreshIcons();

    // --------------------------------------------------------------- butonlar

    // ui_click (1475 Hz) ile panel_close (1368 Hz) neredeyse aynı bantta —
    // üst üste çalınca tek bulanık tık gibi duyuluyor. Kapanışta panel_close
    // yeterli, ui_click çalmıyoruz.
    void HandleResumeClicked()
    {
        if (GameManager.Instance != null) GameManager.Instance.Resume();
    }

    // MENU'ye geçince panel kapanıyor, panel_close yeterli — ui_click onunla aynı bantta
    void HandleMenuClicked()
    {
        if (GameManager.Instance != null) GameManager.Instance.GoToMenu();
    }

    // Restart sahneyi yeniden yüklüyor, state değişmediği için panel_close hiç
    // çalmaz — burada ui_click tek geri bildirim, çakışma yok.
    void HandleRestartClicked()
    {
        if (AudioService.Instance != null) AudioService.Instance.PlayUIClick();

        if (GameManager.Instance != null) GameManager.Instance.Restart();
    }

    // ------------------------------------------------------------ ayar düğmeleri

    void HandleSfxClicked()
    {
        if (SaveService.Instance == null) return;

        bool next = !SaveService.Instance.SfxOn;

        // kapatırken onay sesini SUSMADAN ÖNCE çal, yoksa hiç duyulmaz
        if (!next && AudioService.Instance != null) AudioService.Instance.PlayToggle(false);

        SaveService.Instance.SetSfxOn(next);

        // açarken tam tersi: önce ayar geçsin, sonra ses çıksın
        if (next && AudioService.Instance != null) AudioService.Instance.PlayToggle(true);

        RefreshIcons();
    }

    void HandleMusicClicked()
    {
        if (SaveService.Instance == null) return;

        bool next = !SaveService.Instance.MusicOn;

        SaveService.Instance.SetMusicOn(next);

        if (AudioService.Instance != null) AudioService.Instance.PlayToggle(next);

        RefreshIcons();
    }

    void HandleVibrationClicked()
    {
        if (SaveService.Instance == null) return;

        bool next = !SaveService.Instance.VibrationOn;

        SaveService.Instance.SetVibrationOn(next);

        if (AudioService.Instance != null) AudioService.Instance.PlayToggle(next);

        // Ayarı AÇARKEN tek darbe: oyuncu neyi açtığını gözüyle değil parmağıyla onaylasın.
        // Kapatırken bir şey çalmıyor — zaten kapattı. Sıra önemli: ayar SetVibrationOn ile
        // geçmiş olmalı, yoksa servis isteği kapalı sanıp yutar.
        if (next && HapticService.Instance != null) HapticService.Instance.PlaySettingConfirm();

        RefreshIcons();
    }

    void RefreshIcons()
    {
        if (SaveService.Instance == null) return;

        SetIcon(_sfxIcon,       SaveService.Instance.SfxOn,       _sfxOnSprite,       _sfxOffSprite);
        SetIcon(_musicIcon,     SaveService.Instance.MusicOn,     _musicOnSprite,     _musicOffSprite);
        SetIcon(_vibrationIcon, SaveService.Instance.VibrationOn, _vibrationOnSprite, _vibrationOffSprite);
    }

    static void SetIcon(Image target, bool on, Sprite onSprite, Sprite offSprite)
    {
        if (target == null) return;

        Sprite next = on ? onSprite : offSprite;

        if (next == null || target.sprite == next) return;

        target.sprite = next;
    }
}
