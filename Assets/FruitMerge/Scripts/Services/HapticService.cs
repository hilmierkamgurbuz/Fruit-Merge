using UnityEngine;

/// <summary>
/// Oyunun titreşim (haptics) yönetmeni. Ne zaman, ne kadar sert titreneceğine tek yerden
/// karar veriyor; cihazla konuşma işi <see cref="HapticDevice"/>'ın.
///
/// <b>Mimari — <see cref="AudioService"/>'in ikizi.</b> Aynı desen bilinçli: olay güdümlü,
/// kendi kanalları var, ayarı <c>SaveService</c>'ten okuyor, sahne yeniden yüklenince
/// ölmesin diye <c>DontDestroyOnLoad</c>. Titreşim de ses gibi bir GERİ BİLDİRİM katmanı —
/// oyun mantığı onun varlığını bilmiyor, sadece olay yayınlıyor.
///
/// <b>Üç kanal var, hepsi TEK motoru paylaşıyor:</b>
///  1. <b>Tek darbeler</b> (bırakma, birleşme, combo) — <see cref="Pulse"/>, guard'lı.
///  2. <b>Süreklilik trenleri</b> (deprem, kemirme) — kendi aralıklarıyla üst üste binen
///     darbeler. Motor tek olduğu için tren çalarken tek darbeler susturuluyor.
///  3. <b>Diziler</b> (oyun sonu, karpuz, rekor) — zamanlanmış 2-3 darbelik kutlama/ceza
///     kalıpları. Dizi her şeyi bastırır: kaybetme titreşiminin ortasına merge tıkı girmemeli.
///
/// Coroutine yok (kural 8): bütün zamanlama <see cref="Update"/> içinde float sayaç.
/// Sıcak yolda allocation yok (kural 11). Bütün sayaçlar <c>unscaledDeltaTime</c> ile
/// dönüyor — oyun sonu ve pause'da <c>timeScale</c> 0 olabiliyor, titreşim yine bitmeli.
/// </summary>
[DefaultExecutionOrder(-50)]
public class HapticService : MonoBehaviour
{
    public static HapticService Instance { get; private set; }

    [Header("Referanslar")]
    [SerializeField] GameConfig _config;

    [Tooltip("birleşme şiddetini tier sayısına göre ölçeklemek için")]
    [SerializeField] FruitDatabase _database;

    /// <summary>Meyve veritabanı bağlı değilse tier normalizasyonu için varsayılan üst sınır.</summary>
    const int DefaultMaxTier = 10;

    /// <summary>
    /// Süreklilik trenindeki darbe süresinin aralığa oranı. 1'in ÜSTÜNDE olması şart:
    /// darbeler üst üste binmezse sürekli bir sarsıntı yerine ayrı ayrı "tık tık tık"
    /// hissediliyor.
    /// </summary>
    const float TrainOverlap = 1.6f;

    /// <summary>
    /// Zamanlanmış darbe kalıbı. Üç paralel dizi: şiddet (0-1), süre (sn) ve dizinin
    /// başından itibaren tetiklenme anı (sn).
    ///
    /// Tek uzun bir darbe yerine kalıp kullanılıyor çünkü iOS'ta darbe süresi
    /// ayarlanamıyor — "ağır" hissi iki platformda da ancak RİTİMLE anlatılabiliyor.
    /// </summary>
    readonly struct Sequence
    {
        public readonly float[] Intensity;
        public readonly float[] Duration;
        public readonly float[] StartAt;

        public Sequence(float[] intensity, float[] duration, float[] startAt)
        {
            Intensity = intensity;
            Duration  = duration;
            StartAt   = startAt;
        }

        public int Count => Intensity == null ? 0 : Intensity.Length;

        /// <summary>Son darbenin bittiği an — dizi bundan önce serbest bırakılmamalı.</summary>
        public float TotalTime => Count == 0 ? 0f : StartAt[Count - 1] + Duration[Count - 1];
    }

    // Kalıplar statik: her çağrıda dizi yaratmak çöp üretirdi.

    /// <summary>Oyun sonu: iki kısa uyarı vuruşu + uzun, ağır kapanış. Oyunun en sert titreşimi.</summary>
    static readonly Sequence GameOverSeq = new Sequence(
        new[] { 0.6f,   0.6f,   1f    },
        new[] { 0.055f, 0.055f, 0.32f },
        new[] { 0f,     0.13f,  0.30f });

    /// <summary>Karpuz: iki tok vuruş + kuyruk. Kutlama, ama oyun sonundan kısa.</summary>
    static readonly Sequence MaxTierSeq = new Sequence(
        new[] { 0.85f, 0.85f, 1f    },
        new[] { 0.05f, 0.05f, 0.18f },
        new[] { 0f,    0.12f, 0.26f });

    /// <summary>Yeni rekor: yükselen üçlü — sesteki new_record arpejinin parmaktaki karşılığı.</summary>
    static readonly Sequence NewRecordSeq = new Sequence(
        new[] { 0.5f,   0.7f,  1f    },
        new[] { 0.035f, 0.04f, 0.14f },
        new[] { 0f,     0.10f, 0.20f });

    /// <summary>Efsane combo (x10+): şiddet zaten tavanda, farkı çift vuruşun ritmi taşıyor.</summary>
    static readonly Sequence LegendarySeq = new Sequence(
        new[] { 1f,    0.8f  },
        new[] { 0.05f, 0.04f },
        new[] { 0f,    0.085f });

    /// <summary>Oyuncunun ayarı (<c>SaveService.VibrationOn</c>) + geliştirici çarpanı.</summary>
    bool _enabled;

    // --- tek darbe kanalı ---
    float _guardTimer;
    float _pendingIntensity;
    float _pendingDuration;

    // --- deprem treni ---
    bool  _quakeActive;
    float _quakeLevel;
    float _quakePulseTimer;

    // --- kemirme treni ---
    bool  _chewing;
    float _chewTimer;

    // --- dizi ---
    Sequence _seq;
    float    _seqTime;
    float    _seqScale;
    int      _seqStep;

    /// <summary>Yalnızca Editör günlüğü için: çalan dizinin adı.</summary>
    string _seqReason;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Pahalı kısım (Android'de JNI servis araması, iOS'ta Taptic jeneratörlerinin
        // ısıtılması) burada bir kez oluyor — ilk birleşmede takılma olmasın.
        HapticDevice.Init();
    }

    void OnEnable()
    {
        // Awake'te yok edilmeye işaretlenen kopya abone olmasın — çift titreşim çıkar
        if (Instance != this) return;

        GameEvents.OnFruitDropped        += HandleFruitDropped;
        GameEvents.OnComboMerge          += HandleComboMerge;
        GameEvents.OnMaxTierMerged       += HandleMaxTierMerged;
        GameEvents.OnGameOver            += HandleGameOver;
        GameEvents.OnQuakeStarted        += HandleQuakeStarted;
        GameEvents.OnWormsChewingChanged += HandleChewingChanged;
        GameEvents.OnFruitEaten          += HandleFruitEaten;
        GameEvents.OnStateChanged        += HandleStateChanged;
        GameEvents.OnRunStarted          += HandleRunStarted;
        GameEvents.OnSettingsChanged     += HandleSettingsChanged;
    }

    void OnDisable()
    {
        if (Instance != this) return;

        GameEvents.OnFruitDropped        -= HandleFruitDropped;
        GameEvents.OnComboMerge          -= HandleComboMerge;
        GameEvents.OnMaxTierMerged       -= HandleMaxTierMerged;
        GameEvents.OnGameOver            -= HandleGameOver;
        GameEvents.OnQuakeStarted        -= HandleQuakeStarted;
        GameEvents.OnWormsChewingChanged -= HandleChewingChanged;
        GameEvents.OnFruitEaten          -= HandleFruitEaten;
        GameEvents.OnStateChanged        -= HandleStateChanged;
        GameEvents.OnRunStarted          -= HandleRunStarted;
        GameEvents.OnSettingsChanged     -= HandleSettingsChanged;
    }

    // Awake'te değil Start'ta: SaveService.Awake'in kaydı yüklemesini beklemeliyiz
    void Start() => ApplySettings();

    void OnDestroy()
    {
        if (Instance != this) return;

        HapticDevice.Shutdown();

        Instance = null;
    }

    /// <summary>Uygulama arka plana giderken motor titrerken kalmasın.</summary>
    void OnApplicationPause(bool paused)
    {
        if (paused) StopAll();
    }

    // ----------------------------------------------------------------- ayarlar

    void HandleSettingsChanged() => ApplySettings();

    void ApplySettings()
    {
        bool next = SaveService.Instance != null
                    && SaveService.Instance.VibrationOn
                    && Strength > 0f;

        if (next == _enabled) return;

        _enabled = next;

        if (!_enabled) StopAll();
    }

    float Strength => _config != null ? Mathf.Clamp01(_config.hapticStrength) : 1f;

    // ----------------------------------------------------------------- olaylar

    void HandleFruitDropped(FruitDefinition def)
    {
        if (_config == null || !_config.hapticOnDrop) return;

        // Bırakma sesiyle aynı anda, ondan çok daha hafif: saniyede bir olabilen bir olay,
        // sert olursa oyun boyunca rahatsız ediyor.
        Pulse(_config.hapticDropIntensity, _config.hapticMergeDuration * 0.7f, "drop");
    }

    /// <summary>
    /// Her birleşme buradan geçiyor — <c>OnComboMerge</c> combo 1'de de yayınlanıyor.
    /// Şiddet iki şeyden besleniyor: meyvenin TIER'ı (küçük meyve ince tık, karpuza yakın
    /// meyve tok darbe) ve COMBO KADEMESİ. Kademe eşikleri combo popup'ıyla birebir aynı,
    /// yani parmağın hissettiği sıçrama ekranda okunan kelimeyle ("Nice!" / "Legendary!")
    /// aynı anda oluyor.
    /// </summary>
    void HandleComboMerge(FruitDefinition produced, Vector2 position, int combo)
    {
        if (_config == null) return;

        bool comboStage = combo >= _config.hapticComboMinCombo;

        if (!comboStage && !_config.hapticOnMerge) return;

        float intensity = Mathf.Lerp(_config.hapticMergeIntensityLow,
                                     _config.hapticMergeIntensityHigh,
                                     TierT(produced));

        float duration = _config.hapticMergeDuration;

        if (!comboStage)
        {
            Pulse(intensity, duration, "merge");
            return;
        }

        // Kademe 0 (x2-3) bile düz birleşmeden bir adım güçlü olmalı — zincirin başladığı
        // an hissedilmezse combo'nun titreşimi "bir yerden sonra" başlıyor gibi oluyor.
        int steps = ComboTierOf(combo) + 1;

        intensity += steps * _config.hapticComboIntensityStep;
        duration  += steps * _config.hapticComboDurationStep;

        if (_config.hapticComboLegendaryDouble && ComboTierOf(combo) == 3)
        {
            PlaySequence(LegendarySeq, intensity, "combo-legendary");
            return;
        }

        Pulse(intensity, duration, "combo");
    }

    /// <summary>0 düşük · 1 orta · 2 yüksek · 3 efsane — <c>ComboPopupDirector.TierOf</c>'un aynısı.</summary>
    int ComboTierOf(int combo)
    {
        if (combo >= _config.comboTierLegendaryMin) return 3;
        if (combo >= _config.comboTierHighMin)      return 2;
        if (combo >= _config.comboTierMidMin)       return 1;

        return 0;
    }

    void HandleMaxTierMerged(FruitDefinition def, Vector2 position)
    {
        if (_config == null) return;

        PlaySequence(MaxTierSeq, _config.hapticMaxTierStrength, "max-tier");
    }

    void HandleGameOver(int finalScore)
    {
        if (_config == null) return;

        // Sürekli kanalları önce kapat: deprem yarısında kaybedilirse tren, oyun sonu
        // dizisinin üstüne biner.
        StopContinuous();

        PlaySequence(GameOverSeq, _config.hapticGameOverStrength, "game-over");
    }

    void HandleQuakeStarted()
    {
        if (_config == null || _config.hapticQuakeMaxIntensity <= 0f) return;

        _quakeActive     = true;
        _quakeLevel      = 0f;
        _quakePulseTimer = 0f;

        // Guard'da bekleyen tek darbe artık geçersiz: deprem başladı, o darbe trenin
        // ortasında geç bir tık olarak çıkardı.
        _pendingIntensity = 0f;
    }

    void HandleChewingChanged(bool chewing)
    {
        if (_config == null || _config.hapticChewIntensity <= 0f)
        {
            _chewing = false;
            return;
        }

        _chewing   = chewing;
        _chewTimer = 0f;

        if (chewing) _pendingIntensity = 0f;
        else         HapticDevice.Cancel();
    }

    void HandleFruitEaten(FruitDefinition def, Vector2 position)
    {
        if (_config == null) return;

        // Kemirme treni bu darbeyi bastırmasın: yutma, sürecin FİNALİ.
        _chewing = false;

        Pulse(_config.hapticEatenIntensity, _config.hapticEatenDuration, "eaten");
    }

    void HandleStateChanged(GameState s)
    {
        if (s == GameState.Playing) return;

        StopContinuous();

        // Oyun sonu dizisi tam bu sırada başlıyor (GameManager state'i OnGameOver'ın İÇİNDE
        // değiştiriyor) — motoru susturursak kaybetme titreşimi hiç hissedilmez.
        if (s == GameState.GameOver) return;

        StopAll();
    }

    void HandleRunStarted() => StopAll();

    // ---------------------------------------------------------------- genel API

    /// <summary>
    /// Tek darbe isteği. Guard içindeyse istek ATILMIYOR — en güçlüsü saklanıp guard
    /// bitince çalıyor (bkz. <see cref="GameConfig.hapticGuard"/>).
    /// </summary>
    /// <param name="reason">yalnızca Editör günlüğü için; cihazda kullanılmıyor</param>
    public void Pulse(float intensity01, float duration, string reason = null)
    {
        if (!_enabled) return;

        // Dizi ve süreklilik trenleri motoru sahiplenmiş durumda: araya giren tek darbe
        // ikisini de bulanıklaştırıyor.
        if (_seq.Count > 0 || _quakeActive || _chewing) return;

        if (intensity01 <= 0f) return;

        if (_guardTimer <= 0f)
        {
            Fire(intensity01, duration, reason);
            return;
        }

        if (intensity01 <= _pendingIntensity) return;

        _pendingIntensity = intensity01;
        _pendingDuration  = duration;
    }

    // Buton tıklarına titreşim BİLEREK bağlanmadı: her butona tık koymak, oynanışın
    // anlamlı darbelerini (birleşme, combo) sıradanlaştırıyor. İstenirse ilgili
    // onClick handler'ında tek satır: Pulse(0.3f, 0.02f).

    /// <summary>
    /// Titreşim ayarı AÇILDIĞINDA çalıyor: oyuncu neyi açtığını hemen hissetsin.
    /// Sesteki toggle_on.wav'ın karşılığı — ayarı kapatırken hiçbir şey çalmıyor,
    /// zaten kapattı.
    /// </summary>
    public void PlaySettingConfirm() => Pulse(0.7f, 0.05f, "setting-on");

    /// <summary>Sonuç ekranında dolan yıldız. Ses gibi indeksle yükseliyor.</summary>
    public void PlayStar(int index)
    {
        if (_config == null) return;

        Pulse(_config.hapticStarIntensity * (1f + index * 0.15f), 0.03f, "star");
    }

    public void PlayNewRecord()
    {
        if (_config == null) return;

        PlaySequence(NewRecordSeq, _config.hapticNewRecordStrength, "new-record");
    }

    // -------------------------------------------------------------- deprem treni

    /// <summary>
    /// Depremin o andaki 0-1 şiddeti — <see cref="AudioService.SetQuakeRumbleLevel"/> ile
    /// birebir aynı desen ve aynı zarftan besleniyor: hissedilen şiddet, görülen ve duyulan
    /// şiddetle aynı anda iniyor.
    /// </summary>
    public void SetQuakeLevel(float level01) => _quakeLevel = level01;

    public void StopQuake()
    {
        if (!_quakeActive) return;

        _quakeActive = false;
        _quakeLevel  = 0f;

        // Son darbe motorun içinde sürüyor olabilir; deprem bittiğinde el titremeyi
        // kesmiş olmalı.
        HapticDevice.Cancel();
    }

    /// <summary>
    /// Deprem treni pause'dan sonra kaldığı yerden devam etsin.
    ///
    /// <see cref="HandleStateChanged"/> pause'da bütün sürekli kanalları kapatıyor (doğru:
    /// bu servis <c>unscaledDeltaTime</c> ile döndüğü için pause'da titremeye devam
    /// ederdi). Ama deprem pause'da artık İPTAL EDİLMİYOR, donuyor — dolayısıyla dönüşte
    /// trenin yeniden açılması gerekiyor. Şiddeti zarftan gelmeye devam ediyor
    /// (<see cref="SetQuakeLevel"/>).
    /// </summary>
    public void ResumeQuake()
    {
        if (_quakeActive || _config == null || _config.hapticQuakeMaxIntensity <= 0f) return;

        _quakeActive     = true;
        _quakePulseTimer = 0f;
    }

    // -------------------------------------------------------------------- döngü

    void Update()
    {
        if (!_enabled) return;

        // timeScale 0 olabiliyor (pause, oyun sonu) — titreşim yine akmalı
        float dt = Time.unscaledDeltaTime;

        if (_guardTimer > 0f) _guardTimer -= dt;

        // Dizi her şeyi bastırıyor: kutlama/ceza kalıbının ortasına başka darbe girmemeli.
        if (TickSequence(dt)) return;

        if (TickQuake(dt)) return;

        if (TickChew(dt)) return;

        FlushPending();
    }

    /// <returns>dizi hâlâ çalıyorsa true</returns>
    bool TickSequence(float dt)
    {
        if (_seq.Count == 0) return false;

        _seqTime += dt;

        while (_seqStep < _seq.Count && _seqTime >= _seq.StartAt[_seqStep])
        {
            Fire(_seq.Intensity[_seqStep] * _seqScale, _seq.Duration[_seqStep], _seqReason);

            _seqStep++;
        }

        if (_seqStep < _seq.Count || _seqTime < _seq.TotalTime) return true;

        _seq = default;

        return false;
    }

    /// <returns>deprem treni çalıyorsa true</returns>
    bool TickQuake(float dt)
    {
        if (!_quakeActive || _config == null) return false;

        float level = Mathf.Clamp01(_quakeLevel);

        // Zarfın kuyruğu: hissedilmeyecek kadar zayıf ama motoru meşgul edecek darbeler
        // üretmenin anlamı yok. Tren burada susuyor, kanal açık kalıyor.
        if (level < _config.hapticQuakeMinLevel)
        {
            _quakePulseTimer = 0f;

            return true;
        }

        _quakePulseTimer -= dt;

        if (_quakePulseTimer > 0f) return true;

        float interval = Mathf.Max(0.02f, _config.hapticQuakePulseInterval);

        _quakePulseTimer = interval;

        Fire(level * _config.hapticQuakeMaxIntensity, interval * TrainOverlap, "quake");

        return true;
    }

    /// <returns>kemirme treni çalıyorsa true</returns>
    bool TickChew(float dt)
    {
        if (!_chewing || _config == null) return false;

        _chewTimer -= dt;

        if (_chewTimer > 0f) return true;

        _chewTimer = Mathf.Max(0.03f, _config.hapticChewInterval);

        // Kemirme darbeleri BİLEREK üst üste binmiyor (deprem treninin tersi): aradaki
        // sessizlik "kıtır kıtır" hissini veren şey.
        Fire(_config.hapticChewIntensity, _config.hapticChewDuration, "chew");

        return true;
    }

    void FlushPending()
    {
        if (_pendingIntensity <= 0f || _guardTimer > 0f) return;

        Fire(_pendingIntensity, _pendingDuration, "pending");

        _pendingIntensity = 0f;
    }

    // ------------------------------------------------------------------ çekirdek

    void PlaySequence(Sequence sequence, float scale, string reason)
    {
        if (!_enabled || sequence.Count == 0 || scale <= 0f) return;

        _seq       = sequence;
        _seqScale  = scale;
        _seqTime   = 0f;
        _seqStep   = 0;
        _seqReason = reason;

        // Guard'da bekleyen darbe kalıbın içine karışmasın
        _pendingIntensity = 0f;

        // Guard'ı da sıfırlıyoruz: kalıbın İLK darbesi, hemen öncesindeki bir merge tıkının
        // guard'ına takılıp gecikmemeli — dizi motoru devraldı.
        _guardTimer = 0f;

        // İlk darbeyi bir sonraki kareye bırakmıyoruz: bu servisin execution order'ı -50,
        // olayı yayınlayan sistemlerin çoğu 0 — Update'i beklemek görüntüden bir kare
        // geriye düşmek olurdu.
        TickSequence(0f);
    }

    /// <summary>
    /// Motora giden TEK yol. Guard'ı burada kuruyoruz, böylece hangi kanal tetiklerse
    /// tetiklesin iki darbe arasındaki en kısa mesafe garanti.
    /// </summary>
    void Fire(float intensity01, float duration, string reason)
    {
        intensity01 = Mathf.Clamp01(intensity01) * Strength;

        if (intensity01 <= 0f) return;

        _guardTimer = _config != null ? _config.hapticGuard : 0.05f;

#if UNITY_EDITOR
        // Editör'de motor yok — titreşimi HİSSEDEMEZSİN. Kancanın doğru yerde ve doğru
        // şiddette tetiklendiği ancak buradan görülüyor. Cihaz derlemesinde hiç derlenmez.
        //
        // Süreklilik TRENLERİ günlüğe girmiyor: deprem saniyede ~14, kemirme ~9 darbe
        // üretiyor ve hepsi aynı satırı basıyor. Doğrulanacak bir şey söylemedikleri gibi
        // Profiler'ın GC Alloc grafiğinde oyun kodunun üretmediği bir sivrilme oluşturup
        // boost ölçümlerini yanlış okumaya sebep oluyorlardı.
        if (_config != null && _config.hapticEditorLog && reason != "quake" && reason != "chew")
            Debug.Log($"[Haptic] {reason ?? "pulse"} · şiddet {intensity01:0.00} · " +
                      $"{Mathf.RoundToInt(duration * 1000f)} ms");
#endif

        HapticDevice.Pulse(intensity01, duration);
    }

    void StopContinuous()
    {
        _quakeActive = false;
        _quakeLevel  = 0f;
        _chewing     = false;
    }

    void StopAll()
    {
        StopContinuous();

        _seq              = default;
        _pendingIntensity = 0f;
        _guardTimer       = 0f;

        HapticDevice.Cancel();
    }

    float TierT(FruitDefinition def)
    {
        if (def == null) return 0f;

        int max = _database != null ? Mathf.Max(1, _database.MaxTier) : DefaultMaxTier;

        return Mathf.Clamp01(def.tier / (float)max);
    }
}
