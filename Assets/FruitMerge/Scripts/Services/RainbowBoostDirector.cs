using UnityEngine;

/// <summary>
/// "Rainbow" boost'u — baştan sona.
///
/// Worm'daki gibi ARM/CANCEL/COMMIT ekonomisi, ama hedef seçimi yok: butona basmak dalda
/// bekleyen meyveyi joker (Rainbow) meyveyle DEĞİŞTİRİR ve boost'u SİLAHLANDIRIR — tıpkı
/// Worm'un her meyvenin üstünde nişangâh belirmesi gibi, burada da "silahlanmak bedava".
/// Charge SADECE gerçekten bırakılınca (commit) harcanıyor:
///
///  - <b>Arm</b>   — <see cref="Toggle"/> (armed değilken): dalda bekleyen meyve joker
///                   meyveyle değişir, önceki tanım <see cref="_previousPendingDef"/>'te saklanır.
///                   Charge HENÜZ harcanmıyor.
///  - <b>Cancel</b> — <see cref="Toggle"/> (armed'ken tekrar basmak): joker meyve saklanan
///                   eski tanımla DEĞİŞTİRİLİR (bkz. <see cref="DropController.CancelForcedPending"/>).
///                   Hiçbir şey harcanmadı, olduğu gibi geri alınır.
///  - <b>Commit</b> — oyuncu joker meyveyi gerçekten BIRAKIR. <see cref="GameEvents.OnFruitDropped"/>'i
///                   dinleyip bunun joker meyve olduğunu görünce charge'ı burada harcıyoruz.
///
/// Joker meyve normal bırakma/fizik/birleşme akışının TAMAMINI paylaşıyor —
/// <see cref="Fruit.IsRainbow"/> sadece iki yerde farklı davranıyor:
///  - <see cref="Fruit.TryRequestMerge"/> / <see cref="MergeHandler"/>: tier eşleşmesini
///    atlıyor, ilk dokunduğu meyveyle birleşiyor (üretilen meyve DİĞER tarafın nextTier'ı).
///  - <see cref="Fruit.TickVisual"/>: arkasındaki hale dönmeye devam ediyor, gövde sabit.
/// Bu director'ün TEK işi: kullanım hakkı (charge) ve HUD durumu.
///
/// <see cref="IsBusy"/> her zaman <c>false</c> — Worm/Quake gibi bırakma girdisini
/// KİLİTLEMİYOR (bkz. <c>BoostGate.IsAnyBusy</c> → <c>DropController.Update</c>); armed
/// haldeyken bile oyuncu joker meyveyi normal şekilde bırakabiliyor olması lazım.
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

    /// <summary>Dalda bir joker meyve asılıyken (henüz bırakılmadan/iptal edilmeden) <c>true</c>.</summary>
    public bool IsArmed => _armed;

    public int Charges => _charges;

    public bool CanUse => _charges != 0
                          && GameManager.Instance != null
                          && GameManager.Instance.IsPlaying
                          && !BoostGate.IsAnyBusy;   // başka bir boost oynarken kullanılmaz

    int  _charges;
    bool _armed;

    /// <summary>Silahlanma ANINDA dalda bekleyen meyvenin tanımı — Cancel bunu aynen geri
    /// koyuyor. O an dalda hiçbir şey yoktuysa (nadir <c>_awaitingPending</c> aralığı) null.</summary>
    FruitDefinition _previousPendingDef;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }

        Instance = this;
    }

    void OnEnable()
    {
        BoostGate.Register(this);

        GameEvents.OnRunStarted   += HandleRunStarted;
        GameEvents.OnStateChanged += HandleStateChanged;
        GameEvents.OnFruitDropped += HandleFruitDropped;
    }

    void OnDisable()
    {
        BoostGate.Unregister(this);

        GameEvents.OnRunStarted   -= HandleRunStarted;
        GameEvents.OnStateChanged -= HandleStateChanged;
        GameEvents.OnFruitDropped -= HandleFruitDropped;
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

    // ----------------------------------------------------------------- olaylar

    void HandleRunStarted()
    {
        _armed = false;
        _previousPendingDef = null;

        _charges = _config != null ? _config.rainbowChargesPerRun : 0;

        GameEvents.RaiseBoostStateChanged(BoostId.Rainbow, false, _charges);
    }

    /// <summary>
    /// Menüye dönüldüyse silahlı kalmayı bırak. <see cref="DropController.HandleStateChanged"/>
    /// zaten dalı kendi başına boşaltıyor — burada sadece KENDİ bayraklarımızı temizliyoruz,
    /// DropController'a tekrar dokunmuyoruz.
    ///
    /// Pause'a BİLEREK dokunulmuyor (Worm/Quake ile aynı fikir): oyuncu joker meyve
    /// dalda asılıyken pause'a basarsa silahlı hal donarak korunmalı, iptal olmamalı.
    /// </summary>
    void HandleStateChanged(GameState s)
    {
        if (s != GameState.Menu) return;

        _armed = false;
        _previousPendingDef = null;
    }

    /// <summary>
    /// COMMIT anı: oyuncu bir meyve bıraktı. Bıraktığı joker meyveyse (armed'ken) charge'ı
    /// burada harcıyoruz ve silahı indiriyoruz — iptal penceresi kapandı.
    /// </summary>
    void HandleFruitDropped(FruitDefinition dropped)
    {
        if (!_armed) return;
        if (dropped == null || !dropped.isRainbow) return;

        _armed = false;
        _previousPendingDef = null;

        if (_charges > 0) _charges--;

        GameEvents.RaiseBoostStateChanged(BoostId.Rainbow, false, _charges);
    }

    // ----------------------------------------------------------------- genel API

    /// <summary>HUD butonu. Silahlıyken tekrar basmak Worm'daki gibi İPTAL ediyor.</summary>
    public void Toggle()
    {
        if (_armed) { Cancel(); return; }

        if (!CanUse) return;

        _previousPendingDef = _dropController.PendingDefinition;

        _dropController.ForceNextPending(_rainbowFruit);

        _armed = true;

        // Charge BURADA harcanmıyor — bkz. HandleFruitDropped (commit).
        GameEvents.RaiseBoostStateChanged(BoostId.Rainbow, true, _charges);
    }

    void Cancel()
    {
        if (!_armed) return;

        _armed = false;

        _dropController.CancelForcedPending(_previousPendingDef);

        _previousPendingDef = null;

        GameEvents.RaiseBoostStateChanged(BoostId.Rainbow, false, _charges);
    }

    /// <summary>Mağazadan satın alma. Sınırsız moddaysa (-1) dokunma.</summary>
    public void AddCharge(int amount)
    {
        if (amount <= 0 || _charges < 0) return;

        _charges += amount;

        GameEvents.RaiseBoostStateChanged(BoostId.Rainbow, _armed, _charges);
    }
}
