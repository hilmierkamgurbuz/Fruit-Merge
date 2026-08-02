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

    // ayarlardan gelen aç/kapa. Kapalıyken Play() hiç iş yapmadan döner.
    bool _sfxEnabled = true;

    readonly Dictionary<AudioClip, float> _lastPlayTime = new Dictionary<AudioClip, float>(16);

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
    }

    void OnDisable()
    {
        if (Instance != this) return;

        GameEvents.OnFruitDropped  -= HandleFruitDropped;
        GameEvents.OnMerged        -= HandleMerged;
        GameEvents.OnMaxTierMerged -= HandleMaxTierMerged;
        GameEvents.OnGameOver      -= HandleGameOver;
        GameEvents.OnSettingsChanged -= HandleSettingsChanged;
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

        _sfxEnabled = SaveService.Instance.SfxOn;
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
    }

    // ---------------------------------------------------------------- olaylar

    void HandleFruitDropped(FruitDefinition def) => PlayDrop();

    void HandleMerged(FruitDefinition produced, Vector2 pos) => PlayMerge(produced);

    void HandleMaxTierMerged(FruitDefinition def, Vector2 pos) => PlayMaxTier();

    // Zincirleme birleşmenin AYRI bir sesi yok. Eskiden combo.wav (1725 Hz, zil benzeri)
    // çalıyordu; istenmedi. Zincirin her halkası kendi merge sesini kendi tier pitch'iyle
    // çalıyor — bunun duyulabilmesi için merge'e ayrı ve çok kısa bir guard verildi.
    void HandleGameOver(int finalScore) => PlayGameOver();

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
