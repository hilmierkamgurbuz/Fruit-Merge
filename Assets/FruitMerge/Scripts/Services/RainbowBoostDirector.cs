using UnityEngine;

/// <summary>
/// "Rainbow" boost'u — baştan sona.
///
/// Worm/Quake'in aksine ANLIK: hedef seçimi yok, faz yok, animasyon yok. Butona basılınca
/// dalda bekleyen meyve joker (Rainbow) meyveyle DEĞİŞTİRİLİR — bkz.
/// <see cref="DropController.ForceNextPending"/>. Onu bırakmak, nereye bırakılacağı,
/// ne zaman düşeceği — hepsi oyuncuya ve normal <see cref="DropController"/> akışına kalıyor.
///
/// Joker meyve normal bırakma/fizik/birleşme akışının TAMAMINI paylaşıyor —
/// <see cref="Fruit.IsRainbow"/> sadece iki yerde farklı davranıyor:
///  - <see cref="Fruit.TryRequestMerge"/> / <see cref="MergeHandler"/>: tier eşleşmesini
///    atlıyor, ilk dokunduğu meyveyle birleşiyor (üretilen meyve DİĞER tarafın nextTier'ı).
///  - <see cref="Fruit.TickVisual"/> / <see cref="Fruit.Drop"/>: dalda beklerken pulse
///    ediyor, bırakılınca o anki boyutta kilitleniyor.
/// Bu director'ün TEK işi: kullanım hakkı (charge) ve HUD durumu.
///
/// Anlık olduğu için <see cref="IsBusy"/> her zaman <c>false</c> — Worm/Quake gibi bırakma
/// girdisini KİLİTLEMİYOR (bkz. <c>BoostGate.IsAnyBusy</c> → <c>DropController.Update</c>);
/// tam tersine oyuncunun aynı anda bırakabiliyor olması lazım.
///
/// <see cref="IsArmed"/> ise KISA bir süreliğine <c>true</c> oluyor: HUD butonundaki halka
/// (<c>BoostButton._armedGlow</c>) tıklamayı GÖRÜNÜR bir geri bildirime çeviriyor — hedefsiz,
/// anlık bir boost için "hiç yanmayan halka" tıklamanın işe yaramadığı hissini veriyordu.
/// Bu, <see cref="IsBusy"/>'yi ETKİLEMİYOR: bırakma hâlâ kilitlenmiyor, sadece ikon bir an parlıyor.
/// </summary>
[DefaultExecutionOrder(-30)]
public class RainbowBoostDirector : MonoBehaviour, IBoostDirector
{
    public static RainbowBoostDirector Instance { get; private set; }

    public BoostId Id => BoostId.Rainbow;

    [Header("Referanslar")]
    [SerializeField] DropController _dropController;
    [SerializeField] GameConfig _config;

    [Tooltip("joker meyvenin FruitDefinition'ı — isRainbow=true olmalı (Fruit_Rainbow.asset)")]
    [SerializeField] FruitDefinition _rainbowFruit;

    public bool IsBusy => false;

    /// <summary>Sadece kısa bir "kullanıldı" parlamasının süresi boyunca <c>true</c> — bkz.
    /// sınıf üstündeki not.</summary>
    public bool IsArmed => _glowFlashTimer > 0f;

    public int Charges => _charges;

    public bool CanUse => _charges != 0
                          && GameManager.Instance != null
                          && GameManager.Instance.IsPlaying
                          && !BoostGate.IsAnyBusy;   // başka bir boost oynarken kullanılmaz

    int _charges;

    /// <summary>Time.deltaTime ile azalıyor — pause'da timeScale 0 olduğu için otomatik
    /// donuyor, Worm/Quake'teki gibi ayrı bir pause kancasına gerek yok.</summary>
    float _glowFlashTimer;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }

        Instance = this;
    }

    void OnEnable()
    {
        BoostGate.Register(this);

        GameEvents.OnRunStarted += HandleRunStarted;
    }

    void OnDisable()
    {
        BoostGate.Unregister(this);

        GameEvents.OnRunStarted -= HandleRunStarted;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (_dropController == null || _rainbowFruit == null)
        {
            Debug.LogError("RainbowBoostDirector: _dropController ya da _rainbowFruit " +
                           "bağlı değil, bileşen kapatılıyor.", this);

            enabled = false;

            return;
        }

        if (!_rainbowFruit.isRainbow)
            Debug.LogWarning("RainbowBoostDirector: _rainbowFruit.isRainbow false — joker " +
                             "meyve normal bir meyve gibi davranır (sadece aynı tier'la " +
                             "birleşir). Fruit_Rainbow.asset üstünde isRainbow'u işaretle.", this);

        _charges = _config != null ? _config.rainbowChargesPerRun : 0;

        GameEvents.RaiseBoostStateChanged(BoostId.Rainbow, false, _charges);
    }

    void HandleRunStarted()
    {
        _charges = _config != null ? _config.rainbowChargesPerRun : 0;
        _glowFlashTimer = 0f;

        GameEvents.RaiseBoostStateChanged(BoostId.Rainbow, false, _charges);
    }

    /// <summary>
    /// Boost boştayken (parlama sönükken) TEK bir karşılaştırmayla çık — oyunun neredeyse
    /// tamamında burası (kural 7 ile aynı ekonomi, sadece bu director'de tek satır).
    /// </summary>
    void Update()
    {
        if (_glowFlashTimer <= 0f) return;

        _glowFlashTimer -= Time.deltaTime;

        if (_glowFlashTimer > 0f) return;

        _glowFlashTimer = 0f;

        GameEvents.RaiseBoostStateChanged(BoostId.Rainbow, false, _charges);
    }

    /// <summary>HUD butonu. Anlık olduğu için "iptal" diye bir hâl yok — tekrar basmak
    /// (kullanım varsa) sadece dalda bekleyeni yeni bir joker meyveyle değiştirir.</summary>
    public void Toggle()
    {
        if (!CanUse) return;

        if (_charges > 0) _charges--;

        _dropController.ForceNextPending(_rainbowFruit);

        _glowFlashTimer = _config != null ? _config.rainbowGlowFlashDuration : 0.35f;

        // armed=true: HUD halkası hemen yanıyor, Update yukarıdaki sayaç bitince söndürüyor.
        GameEvents.RaiseBoostStateChanged(BoostId.Rainbow, true, _charges);
    }

    /// <summary>Mağazadan satın alma. Sınırsız moddaysa (-1) dokunma.</summary>
    public void AddCharge(int amount)
    {
        if (amount <= 0 || _charges < 0) return;

        _charges += amount;

        GameEvents.RaiseBoostStateChanged(BoostId.Rainbow, IsArmed, _charges);
    }
}
