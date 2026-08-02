using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Açılış ekranı. GameManager <c>Boot</c>'ta başlıyor ve o state'e geçiş olayı hiç
/// ateşlenmiyor (zaten başlangıç durumu) — bu yüzden bir state-change beklemek yerine
/// kendi <c>Start()</c>'ında gösterir. Boot -> Menu geçişini bu panel yapıyor.
///
/// <b>Yükleme çubuğu sahte değil:</b> <see cref="PrewarmQueue"/>'daki havuzlar
/// (<see cref="FruitPool"/> 40 + <see cref="ComboPopupDirector"/> 6 obje) artık
/// <c>Awake</c>'te tek karede değil, burada kare başına
/// <see cref="GameConfig.splashPrewarmPerFrame"/> kadar ısıtılıyor. Çubuk
/// "yapılan iş / toplam iş" oranını gösteriyor.
///
/// Çubuk ayrıca <see cref="GameConfig.splashMinDuration"/>'dan hızlı dolamaz: iş
/// erken biterse 0'dan 1'e sıçrayıp göz kırpması gibi geçerdi.
///
/// <b>Menü ile ASLA aynı anda görünmez:</b> <c>GoToMenu()</c> çağrısı <c>Hide()</c>'ın
/// yanında değil, fade TAMAMEN bittikten sonra çalışan <see cref="OnHidden"/>'da.
/// (İlk sürümde ikisi aynı karedeydi; 0.18 sn boyunca splash sönerken menü açılıyordu.)
/// </summary>
[DefaultExecutionOrder(100)]
public class SplashPanel : UIPanel
{
    [Tooltip("Image Type=Filled, Horizontal, Origin=Left — fillAmount buradan sürülüyor")]
    [SerializeField] Image _fill;

    [SerializeField] GameConfig _config;

    const float FallbackMinDuration = 1.2f;
    const int   FallbackPerFrame    = 2;

    float _elapsed;
    bool  _loading;
    bool  _handedOver;

    // Açılışta panel_open/panel_close çalmasın — menüdeki aynı gerekçe: bu bir açılır
    // pencere değil, ekranın kendisi; ayrıca AudioService henüz kayıttaki ses ayarını
    // uygulamamış olabiliyor, yani ses kapalıyken bile bir kez duyulurdu.
    protected override bool PlaysOpenSfx  => false;
    protected override bool PlaysCloseSfx => false;

    void Start()
    {
        Show();

        // Açılışta içeri fade YOK: 0.18 sn boyunca yarı saydam splash'ın altından
        // oyun tahtası görünürdü. İlk kareden itibaren tam opak.
        _group.alpha = 1f;

        _elapsed = 0f;
        _loading = true;

        if (_fill != null) _fill.fillAmount = 0f;
    }

    protected override void OnTick(float unscaledDeltaTime)
    {
        if (!_loading) return;

        _elapsed += unscaledDeltaTime;

        int budget = _config != null
            ? Mathf.Max(1, _config.splashPrewarmPerFrame)
            : FallbackPerFrame;

        PrewarmQueue.Step(budget);

        float minDuration = _config != null
            ? Mathf.Max(0.01f, _config.splashMinDuration)
            : FallbackMinDuration;

        int total = PrewarmQueue.Total;

        // İki ilerlemenin KÜÇÜĞÜ: iş bitmeden dolmaz, minimum süre dolmadan da dolmaz.
        float work = total > 0 ? PrewarmQueue.Done / (float)total : 1f;
        float time = _elapsed / minDuration;

        float t = Mathf.Clamp01(Mathf.Min(work, time));

        if (_fill != null) _fill.fillAmount = t;

        if (t < 1f) return;

        _loading = false;

        Hide();
    }

    /// <summary>
    /// Fade bitti, panel artık TAM görünmez. Menü ancak şimdi açılabilir.
    /// </summary>
    protected override void OnHidden()
    {
        if (_handedOver) return;

        _handedOver = true;

        if (GameManager.Instance != null) GameManager.Instance.GoToMenu();

        // Splash bir daha açılmıyor: tuvalden tamamen çıksın, boşuna batch/overdraw olmasın.
        gameObject.SetActive(false);
    }
}
