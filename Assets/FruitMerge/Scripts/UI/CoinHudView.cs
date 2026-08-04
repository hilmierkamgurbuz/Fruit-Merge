using TMPro;
using UnityEngine;

/// <summary>
/// Cüzdan. Panellerin de üstünde kalması gerektiği için HUDCanvas'ta değil
/// OverlayCanvas'ta duruyor.
///
/// <b>Her zaman görünmüyor.</b> Oynanış sırasında sağ üst köşe skorun; cüzdan oraya
/// çıkıp dikkat dağıtmıyor. Görünme kuralı iki girdinin OR'u:
///  - <b>durum</b>: menü ve sonuç ekranı. Splash (Boot) ve pause'da gizli.
///  - <b>mağaza</b>: oynanış sırasında boost satın alma paneli açıksa görünüyor —
///    oyuncu neyle ödeyeceğini görmeden satın alma ekranına bakamaz.
/// İki girdi ayrı olaylardan geldiği için ikisi de bir alanda tutuluyor ve karar
/// <see cref="Apply"/>'da bir yerde veriliyor; abone sırası ne olursa olsun sonuç aynı.
///
/// Sayı <b>birer birer</b> artıyor: <see cref="GameEvents.OnCoinsChanged"/> hedefi
/// söylüyor, gösterilen değer hedefe doğru saniyede <c>coinCountSpeed</c> hızında
/// yürüyor. Uçan paralar HUD'a tek tek indiği için hedef de kademeli yükseliyor,
/// yani sayaç paraların ritmini takip ediyor.
///
/// <see cref="Update"/> boşta ilk satırda çıkıyor: sayılacak fark ya da yarım kalmış
/// bir geçiş yoksa hiçbir iş yapılmıyor (kural 7).
/// </summary>
[DefaultExecutionOrder(100)]
public class CoinHudView : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] GameConfig _config;

    [SerializeField] TextMeshProUGUI _label;

    [Tooltip("coin görseli. Uçan paraların hedefi burası")]
    [SerializeField] RectTransform _icon;

    [Tooltip("görünürlük bununla yönetiliyor — SetActive ile değil, çünkü kapalı bir " +
             "obje OnDisable'da aboneliğini bırakır ve bir daha haber alamaz")]
    [SerializeField] CanvasGroup _group;

    [Tooltip("belirme/kaybolma süresi (sn). Mağaza paneli 0.18 sn'de açıldığı için " +
             "cüzdanın anında 'pat' diye belirmesi yamalı duruyordu")]
    [SerializeField] float _fadeDuration = 0.18f;

    int   _target;
    float _shown;
    bool  _hasValue;

    // Görünürlüğün iki bağımsız girdisi. Ayrı olaylardan geldikleri için ayrı
    // tutuluyorlar; kararı Apply veriyor.
    GameState _state = GameState.Boot;
    bool      _shopOpen;

    float _alphaTarget;

    void Awake()
    {
        // BoostButton ile aynı desen: grup garanti var olsun, böylece aşağıdaki hiçbir
        // yerde (Update dahil, her kare) null kontrolü gerekmiyor.
        if (_group == null) _group = GetComponent<CanvasGroup>();

        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

        // Boot (splash) sırasında görünmesin. İlk GameState olayında karar yenileniyor.
        _alphaTarget  = 0f;
        _group.alpha  = 0f;

        SetInteractive(false);
    }

    void OnEnable()
    {
        GameEvents.OnCoinsChanged     += HandleCoinsChanged;
        GameEvents.OnStateChanged     += HandleStateChanged;
        GameEvents.OnBoostShopToggled += HandleShopToggled;
    }

    void OnDisable()
    {
        GameEvents.OnCoinsChanged     -= HandleCoinsChanged;
        GameEvents.OnStateChanged     -= HandleStateChanged;
        GameEvents.OnBoostShopToggled -= HandleShopToggled;
    }

    /// <summary>
    /// İlk değer ANINDA yazılıyor, sayılmıyor: kayıttan 340 coin ile açılan oyuncu
    /// menüde 0'dan 340'a kadar dönen bir sayaç izlemek zorunda kalmamalı. Sayma
    /// animasyonu yalnızca oyun içindeki artışlar için.
    /// </summary>
    void HandleCoinsChanged(int total)
    {
        _target = total;

        if (!_hasValue)
        {
            _hasValue = true;
            _shown    = total;

            Write(total);
        }
    }

    void HandleStateChanged(GameState s)
    {
        _state = s;

        Apply();
    }

    void HandleShopToggled(bool open)
    {
        _shopOpen = open;

        Apply();
    }

    /// <summary>
    /// Görünürlük kararının TEK yeri. İki girdi de kendi olayında alana yazılıp buraya
    /// düşüyor, böylece olayların hangi sırayla geldiği sonucu değiştirmiyor.
    /// </summary>
    void Apply()
    {
        // Oynanışta sağ üst köşe skorun — cüzdan yalnızca menüde, sonuç ekranında
        // ve mağaza açıkken çıkıyor.
        bool show = _state == GameState.Menu
                 || _state == GameState.GameOver
                 || _shopOpen;

        _alphaTarget = show ? 1f : 0f;

        SetInteractive(show);
    }

    void Update()
    {
        bool counting = _hasValue && !Mathf.Approximately(_shown, _target);
        bool fading   = !Mathf.Approximately(_group.alpha, _alphaTarget);

        if (!counting && !fading) return;

        float dt = Time.unscaledDeltaTime;

        if (fading)
            _group.alpha = Mathf.MoveTowards(_group.alpha, _alphaTarget,
                dt / Mathf.Max(0.01f, _fadeDuration));

        if (counting)
        {
            float speed = _config != null ? _config.coinCountSpeed : 45f;

            _shown = Mathf.MoveTowards(_shown, _target, speed * dt);

            Write(Mathf.RoundToInt(_shown));
        }
    }

    // SetText("{0}", int) string birleştirmiyor — her karede çağrıldığı için önemli (kural 11)
    void Write(int value)
    {
        if (_label != null) _label.SetText("{0}", value);
    }

    // Alpha'yı BURADA değiştirmiyoruz — o Update'te yumuşak geçiyor. Girdi engelleme
    // ise anında değişmeli: sönerken hâlâ tıklama yutmasın.
    void SetInteractive(bool on)
    {
        if (_group == null) return;

        _group.interactable   = on;
        _group.blocksRaycasts = on;
    }
}
