using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tüm SFX'i tek yerden çalar.
///
/// Mimari:
///  - round-robin AudioSource havuzu (GameConfig.audioSourceCount kanal)
///  - retrigger guard: aynı klip GameConfig.sfxRetriggerGuard içinde ikinci kez çalmaz
///  - pitch varyasyonu: tier bazlı (büyük meyve = kalın ses) + her çalışta ±jitter
///
/// Sahne yeniden yüklenince (Restart) ses kesilmesin diye DontDestroyOnLoad.
/// </summary>
[DefaultExecutionOrder(-50)]
public class AudioService : MonoBehaviour
{
    public static AudioService Instance { get; private set; }

    [Header("Referanslar")]
    [SerializeField] GameConfig _config;

    [Tooltip("birleşme pitch'ini tier sayısına göre ölçeklemek için")]
    [SerializeField] FruitDatabase _database;

    [Header("Oyun sesleri")]
    [Tooltip("meyve bırakma")]
    [SerializeField] AudioClip _dropSfx;

    [Tooltip("FruitDefinition.mergeSfx boş kalırsa kullanılacak yedek")]
    [SerializeField] AudioClip _mergeSfx;

    [Tooltip("karpuz + karpuz — normal birleşmeden AYRI klip, farkı klip seviyesinden gelir")]
    [SerializeField] AudioClip _maxTierSfx;

    [Tooltip("oyun sonu")]
    [SerializeField] AudioClip _gameOverSfx;

    [Header("Arayüz sesleri")]
    [SerializeField] AudioClip _uiClickSfx;
    [SerializeField] AudioClip _panelOpenSfx;
    [SerializeField] AudioClip _panelCloseSfx;

    [Header("Sonuç ekranı — EK D yapılınca bağlanacak")]
    [SerializeField] AudioClip _starSfx;
    [SerializeField] AudioClip _newRecordSfx;

    [Header("Ayarlar — Bölüm 17 yapılınca bağlanacak")]
    [SerializeField] AudioClip _toggleOnSfx;
    [SerializeField] AudioClip _toggleOffSfx;

    [Header("Boost — deprem")]
    [Tooltip("t=0'daki keskin zemin çatlaması / tok darbe (~0.3 sn)")]
    [SerializeField] AudioClip _quakeCrackSfx;

    [Tooltip("alçak gürültü. LOOP'lanıyor ve sesi depremin sarsıntı zarfından sürülüyor — " +
             "bu yüzden klibin süresi önemli değil, döngülenebilir kısa bir kayıt yeterli")]
    [SerializeField] AudioClip _quakeRumbleSfx;

    [Tooltip("gürültünün en yüksek sesi (0-1). Diğer SFX'lerin altında kalsın, " +
             "merge sesleri gürültünün içinde kaybolmasın")]
    [Range(0f, 1f)] [SerializeField] float _quakeRumbleVolume = 0.55f;

    [Header("Müzik")]
    [Tooltip("arka plan müziği. LOOP'lanıyor ve SFX havuzundan tamamen AYRI bir kanalda çalıyor.\n\n" +
             "⚠️ Import ayarı SFX'ten FARKLI olmalı: Streaming + Vorbis. SFX ayarını (Decompress " +
             "On Load + PCM) verirsen 64 sn'lik klip RAM'de ~10 MB'ı aşıyor")]
    [SerializeField] AudioClip _musicClip;

    [Tooltip("müziğin sesi. SFX'in altında kalmalı — müzik zemin, efektler ön planda")]
    [Range(0f, 1f)] [SerializeField] float _musicVolume = 0.4f;

    [Header("Seviye")]
    [Tooltip("tüm SFX'in ortak çarpanı. Klip seviyeleri dosyaların içinde hazır — 1'de bırak")]
    [Range(0f, 1f)] [SerializeField] float _masterVolume = 1f;

    [Header("Pitch")]
    [Tooltip("her çalışta uygulanan rastgele sapma (±oran) — aynı ses monoton duyulmasın")]
    [Range(0f, 0.2f)] [SerializeField] float _pitchJitter = 0.05f;

    [Tooltip("tier 0 birleşmesinin pitch'i (küçük meyve = ince ses)")]
    [SerializeField] float _mergePitchLowTier = 1.4f;

    [Tooltip("en yüksek tier birleşmesinin pitch'i (büyük meyve = kalın ses)")]
    [SerializeField] float _mergePitchHighTier = 0.7f;

    const int   DefaultSourceCount = 6;
    const float DefaultGuard       = 0.06f;
    const float DefaultMergeGuard  = 0.012f;
    const int   DefaultMaxTier     = 10;

    AudioSource[] _sources;
    int _next;

    /// <summary>
    /// Gürültünün KENDİ kanalı. Havuzdaki 6 kanal "çal ve unut" — round-robin oldukları için
    /// bir sonraki ses gürültünün üstüne yazardı. Gürültü ise sürekli çalıp sesi her karede
    /// değişen tek ses, o yüzden ayrı duruyor.
    /// </summary>
    AudioSource _rumbleSource;

    /// <summary>
    /// Müziğin KENDİ kanalı. Havuzdaki 6 SFX kanalı round-robin çalıştığı için müzik oraya
    /// konsa bir sonraki efekt onun üstüne yazardı. Ayrıca müziğin sesi SFX'ten bağımsız
    /// ayarlanıyor ve ayardan kapatılıp açılabiliyor.
    /// </summary>
    AudioSource _musicSource;

    bool _musicEnabled = true;

    /// <summary>Müzik bir kez başladı mı — açıp kapatmak parçayı baştan başlatmasın diye.</summary>
    bool _musicStarted;

    // ayarlardan gelen aç/kapa. Kapalıyken Play() hiç iş yapmadan döner.
    bool _sfxEnabled = true;

    /// <summary>
    /// Referans kimliğiyle karşılaştıran comparer.
    ///
    /// Varsayılan <c>EqualityComparer&lt;AudioClip&gt;.Default</c>, <c>UnityEngine.Object</c>'in
    /// override ettiği <c>Equals</c>/<c>GetHashCode</c>'una gidiyor — içinde "yok edilmiş
    /// obje" kontrolü var ve managed↔native sınırına yakın çalışıyor. Bizim istediğimiz
    /// şey zaten "aynı klip mi", yani saf referans eşitliği.
    ///
    /// .NET'in <c>ReferenceEqualityComparer</c>'ı .NET 5+ API'si, Unity'nin .NET Standard
    /// 2.1 profilinde yok — bu yüzden üç satırla kendimiz yazıyoruz.
    /// </summary>
    sealed class ClipReferenceComparer : IEqualityComparer<AudioClip>
    {
        public static readonly ClipReferenceComparer Instance = new ClipReferenceComparer();

        public bool Equals(AudioClip a, AudioClip b) => ReferenceEquals(a, b);

        public int GetHashCode(AudioClip clip) =>
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(clip);
    }

    /// <summary>Aynı klibin guard süresi içinde ikinci kez çalmasını engelleyen kayıtlar.</summary>
    readonly Dictionary<AudioClip, float> _lastPlayTime =
        new Dictionary<AudioClip, float>(16, ClipReferenceComparer.Instance);

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildSources();
    }

    void OnEnable()
    {
        // Awake'te yok edilmeye işaretlenen kopya abone olmasın — aynı karede çift ses çıkar
        if (Instance != this) return;

        GameEvents.OnFruitDropped  += HandleFruitDropped;
        GameEvents.OnMerged        += HandleMerged;
        GameEvents.OnMaxTierMerged += HandleMaxTierMerged;
        GameEvents.OnGameOver      += HandleGameOver;
        GameEvents.OnSettingsChanged += HandleSettingsChanged;
        GameEvents.OnQuakeStarted  += HandleQuakeStarted;
    }

    void OnDisable()
    {
        if (Instance != this) return;

        GameEvents.OnFruitDropped  -= HandleFruitDropped;
        GameEvents.OnMerged        -= HandleMerged;
        GameEvents.OnMaxTierMerged -= HandleMaxTierMerged;
        GameEvents.OnGameOver      -= HandleGameOver;
        GameEvents.OnSettingsChanged -= HandleSettingsChanged;
        GameEvents.OnQuakeStarted  -= HandleQuakeStarted;
    }

    // Awake'te değil Start'ta: SaveService.Awake'in kaydı yüklemesini beklemeliyiz
    void Start() => ApplySettings();

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void HandleSettingsChanged() => ApplySettings();

    void ApplySettings()
    {
        if (SaveService.Instance == null) return;

        _sfxEnabled   = SaveService.Instance.SfxOn;
        _musicEnabled = SaveService.Instance.MusicOn;

        ApplyMusicState();
    }

    /// <summary>
    /// Müziği ayara göre çalıştırır/duraklatır. <c>Stop</c> değil <c>Pause</c> kullanılıyor:
    /// oyuncu müziği kapatıp açtığında parça baştan başlamasın, kaldığı yerden devam etsin.
    /// </summary>
    void ApplyMusicState()
    {
        if (_musicSource == null || _musicClip == null) return;

        _musicSource.volume = Mathf.Clamp01(_musicVolume);

        if (!_musicEnabled)
        {
            if (_musicSource.isPlaying) _musicSource.Pause();
            return;
        }

        if (_musicSource.isPlaying) return;

        if (_musicStarted)
        {
            _musicSource.UnPause();
        }
        else
        {
            _musicSource.clip = _musicClip;
            _musicSource.Play();
            _musicStarted = true;
        }
    }

    void BuildSources()
    {
        int count = _config != null ? Mathf.Max(1, _config.audioSourceCount) : DefaultSourceCount;

        _sources = new AudioSource[count];

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"SFX_{i}");
            go.transform.SetParent(transform, false);

            AudioSource src = go.AddComponent<AudioSource>();

            src.playOnAwake  = false;
            src.loop         = false;
            src.spatialBlend = 0f;   // 2D — konum önemsiz
            src.dopplerLevel = 0f;
            src.ignoreListenerPause = true;

            _sources[i] = src;
        }

        var rumbleGo = new GameObject("SFX_QuakeRumble");
        rumbleGo.transform.SetParent(transform, false);

        _rumbleSource = rumbleGo.AddComponent<AudioSource>();

        _rumbleSource.playOnAwake  = false;
        _rumbleSource.loop         = true;      // süre depremden geliyor, klipten değil
        _rumbleSource.spatialBlend = 0f;
        _rumbleSource.dopplerLevel = 0f;
        _rumbleSource.ignoreListenerPause = true;
        _rumbleSource.volume       = 0f;

        var musicGo = new GameObject("Music");
        musicGo.transform.SetParent(transform, false);

        _musicSource = musicGo.AddComponent<AudioSource>();

        _musicSource.playOnAwake  = false;
        _musicSource.loop         = true;      // kesintisiz zemin
        _musicSource.spatialBlend = 0f;
        _musicSource.dopplerLevel = 0f;
        _musicSource.ignoreListenerPause = true;
        _musicSource.volume       = 0f;
    }

    // ---------------------------------------------------------------- olaylar

    void HandleFruitDropped(FruitDefinition def) => PlayDrop();

    void HandleMerged(FruitDefinition produced, Vector2 pos) => PlayMerge(produced);

    void HandleMaxTierMerged(FruitDefinition def, Vector2 pos) => PlayMaxTier();

    // Zincirleme birleşmenin AYRI bir sesi yok. Eskiden combo.wav (1725 Hz, zil benzeri)
    // çalıyordu; istenmedi. Zincirin her halkası kendi merge sesini kendi tier pitch'iyle
    // çalıyor — bunun duyulabilmesi için merge'e ayrı ve çok kısa bir guard verildi.
    void HandleGameOver(int finalScore) => PlayGameOver();

    /// <summary>
    /// Deprem başladı: çatlama sesi + gürültü döngüsü. Gürültünün SESİ burada ayarlanmıyor —
    /// <see cref="SetQuakeRumbleLevel"/> ile depremin sarsıntı zarfından sürülüyor, böylece
    /// duyulan şiddet görülen şiddetle birebir aynı oluyor.
    ///
    /// Titreşim artık burada değil: aynı olayı <see cref="HapticService"/> de dinliyor ve
    /// deprem boyunca zarfa bağlı bir darbe treni sürüyor.
    /// </summary>
    void HandleQuakeStarted()
    {
        PlayQuakeCrack();
        StartQuakeRumble();
    }

    // ------------------------------------------------------------- genel API

    public void PlayDrop() => Play(_dropSfx, Jitter(1f));

    public void PlayMerge(FruitDefinition def)
    {
        AudioClip clip = def != null && def.mergeSfx != null ? def.mergeSfx : _mergeSfx;

        // merge kendi kısa guard'ını kullanıyor — zincirin halkaları birbirini susturmasın
        Play(clip, Jitter(MergePitch(def != null ? def.tier : 0)), MergeGuardSeconds);
    }

    public void PlayMaxTier() => Play(_maxTierSfx, Jitter(1f));

    // müzikal cümleler — pitch'e dokunma
    public void PlayGameOver()   => Play(_gameOverSfx,   1f);
    public void PlayNewRecord()  => Play(_newRecordSfx,  1f);
    public void PlayPanelOpen()  => Play(_panelOpenSfx,  1f);
    public void PlayPanelClose() => Play(_panelCloseSfx, 1f);

    public void PlayUIClick() => Play(_uiClickSfx, Jitter(1f));

    /// <summary>Sonuç ekranındaki yıldızlar — sırayla yükselen pitch.</summary>
    public void PlayStar(int index) => Play(_starSfx, Mathf.Min(1f + index * 0.08f, 1.3f));

    public void PlayToggle(bool on) => Play(on ? _toggleOnSfx : _toggleOffSfx, 1f);

    public void SetMasterVolume(float volume) => _masterVolume = Mathf.Clamp01(volume);

    // -------------------------------------------------------------- deprem sesi

    public void PlayQuakeCrack() => Play(_quakeCrackSfx, Jitter(1f));

    /// <summary>Gürültü döngüsünü sessizden başlatır. Şiddeti <see cref="SetQuakeRumbleLevel"/> veriyor.</summary>
    public void StartQuakeRumble()
    {
        if (_rumbleSource == null || _quakeRumbleSfx == null || !_sfxEnabled) return;

        _rumbleSource.clip   = _quakeRumbleSfx;
        _rumbleSource.volume = 0f;
        _rumbleSource.Play();
    }

    /// <summary>
    /// Gürültünün o andaki şiddeti (0-1) — deprem director'ü her karede sarsıntı zarfını
    /// buraya yazıyor. Ayrı bir fade kodu yok: zarf zaten yumuşak iniyor.
    /// </summary>
    public void SetQuakeRumbleLevel(float level01)
    {
        if (_rumbleSource == null) return;

        // Kural 3: volume 0-1. Yükseklik farkı klip seviyesinden değil, iki çarpandan geliyor.
        _rumbleSource.volume = Mathf.Clamp01(level01) * _quakeRumbleVolume * Mathf.Clamp01(_masterVolume);
    }

    public void StopQuakeRumble()
    {
        if (_rumbleSource == null) return;

        _rumbleSource.volume = 0f;

        _rumblePaused = false;

        if (_rumbleSource.isPlaying) _rumbleSource.Stop();
    }

    /// <summary>Gürültü pause'da sussun — <c>rumblePaused</c> ile sürdürülebilir kalıyor.</summary>
    bool _rumblePaused;

    /// <summary>
    /// Oyun duraklatıldı: gürültüyü DURAKLAT (durdurma değil).
    ///
    /// Ses <c>timeScale</c>'e bağlı DEĞİL ve kanallar <c>ignoreListenerPause</c> ile
    /// kuruluyor — yani pause'da hiçbir şey susmuyordu ve deprem gürültüsü pause paneli
    /// açıkken sabit sesle uğuldamaya devam ediyordu. <c>Stop</c> değil <c>Pause</c>:
    /// Continue ile kaldığı yerden devam ediyor, klip baştan başlamıyor.
    /// </summary>
    public void PauseQuakeRumble()
    {
        if (_rumbleSource == null || !_rumbleSource.isPlaying) return;

        _rumbleSource.Pause();

        _rumblePaused = true;
    }

    /// <summary>Pause'dan dönüş: gürültü kaldığı yerden devam eder.</summary>
    public void ResumeQuakeRumble()
    {
        if (_rumbleSource == null || !_rumblePaused) return;

        _rumblePaused = false;

        // Ses ayarı pause sırasında kapatılmış olabilir.
        if (!_sfxEnabled) return;

        _rumbleSource.UnPause();
    }

    // Titreşim buradan taşındı: eski VibrateOnce() Handheld.Vibrate() çağırıyordu, yani
    // Android'de ~500 ms tam güç tek bir buzz — şiddet/süre/kademe yok. Artık ayrı bir
    // servis var (HapticService + HapticDevice) ve olayları kendisi dinliyor.

    // ----------------------------------------------------------------- çekirdek

    void Play(AudioClip clip, float pitch) => Play(clip, pitch, GuardSeconds);

    void Play(AudioClip clip, float pitch, float guardSeconds)
    {
        if (clip == null || _sources == null) return;

        // ses kapalı: guard kaydı da tutmuyoruz, tekrar açılınca temiz başlasın
        if (!_sfxEnabled) return;

        // unscaledTime: panel açıkken timeScale 0 olsa da guard doğru saymalı
        float now = Time.unscaledTime;

        if (_lastPlayTime.TryGetValue(clip, out float last) && now - last < guardSeconds) return;

        _lastPlayTime[clip] = now;

        AudioSource src = _sources[_next];
        _next = (_next + 1) % _sources.Length;

        src.clip   = clip;
        src.volume = Mathf.Clamp01(_masterVolume);
        src.pitch  = pitch;
        src.Play();
    }

    float GuardSeconds => _config != null ? _config.sfxRetriggerGuard : DefaultGuard;

    float MergeGuardSeconds => _config != null ? _config.mergeRetriggerGuard : DefaultMergeGuard;

    float Jitter(float basePitch) =>
        basePitch * (1f + UnityEngine.Random.Range(-_pitchJitter, _pitchJitter));

    float MergePitch(int tier)
    {
        int max = _database != null ? Mathf.Max(1, _database.MaxTier) : DefaultMaxTier;

        float t = Mathf.Clamp01(tier / (float)max);

        return Mathf.Lerp(_mergePitchLowTier, _mergePitchHighTier, t);
    }
}
