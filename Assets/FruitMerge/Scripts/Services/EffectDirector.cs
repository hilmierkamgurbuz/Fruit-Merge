using UnityEngine;

/// <summary>
/// Birleşme efektleri: meyvenin her yanından kendi renginde meyve suyu fışkırması.
///
/// Mimari — neden havuz/Update yok:
///  - <b>tek paylaşımlı ParticleSystem</b>. Her birleşmede yeni sistem yaratmak yerine
///    Emit() çağırıyoruz; parçacık havuzunu Unity'nin kendisi native tarafta yönetiyor.
///    Bu yüzden burada ne obje havuzu ne Update döngüsü var — ikisi de gereksiz.
///  - <b>EmitParams struct</b>: çağrı başına allocation yok (performans kuralı 11).
///  - Damla sayısı/boyutu/hızı meyvenin tier'ına göre ölçekleniyor: kiraz birkaç damla
///    sıçratır, karpuz patlar.
///
/// Renk <see cref="FruitDefinition.displayColor"/>'dan geliyor — her tier için ayrı
/// asset üretmeye gerek yok.
/// </summary>
[DefaultExecutionOrder(-40)]
public class EffectDirector : MonoBehaviour
{
    public static EffectDirector Instance { get; private set; }

    [Header("Referanslar")]
    [Tooltip("damla sayısını tier'a göre ölçeklemek için")]
    [SerializeField] FruitDatabase _database;

    [Header("Parçacık sistemleri")]
    [Tooltip("ana meyve suyu damlaları — büyük, ağır, yere düşer")]
    [SerializeField] ParticleSystem _juiceDroplets;

    [Tooltip("ince serpinti — küçük, hızlı, daha geniş yayılır. Opsiyonel, boş bırakılabilir")]
    [SerializeField] ParticleSystem _juiceMist;

    [Tooltip("kurtçuk boost'unun meyve renginde sisi. Aynı paylaşımlı-sistem deseni: " +
             "efekt başına ParticleSystem yaratılmıyor, buraya Emit ediliyor")]
    [SerializeField] ParticleSystem _eatSmoke;

    [Tooltip("deprem boost'unun zemin tozu. Shape'i BOX olmalı (yatay şerit) — sis ve " +
             "meyve suyu daire kullanıyor, bu zemin boyunca çıkıyor")]
    [SerializeField] ParticleSystem _quakeDust;

    [Tooltip("deprem boost'unun kenarlardan düşen molozu. Shape'i BOX (DİKEY şerit), " +
             "gravityModifier POZİTİF olmalı — parçalar düşüyor")]
    [SerializeField] ParticleSystem _quakeRubble;

    [Header("Rainbow")]
    [Range(0f, 1f)]
    [Tooltip("rainbow boost'un joker meyvesi birleşince çıkan damlaların HSV doygunluğu")]
    [SerializeField] float _rainbowSaturation = 0.85f;

    [Range(0f, 1f)]
    [Tooltip("rainbow damlalarının HSV parlaklığı")]
    [SerializeField] float _rainbowValue = 1f;

    [Header("Damla sayısı")]
    [Tooltip("en küçük meyve (kiraz) kaç damla sıçratsın")]
    [SerializeField] int _countMin = 10;

    [Tooltip("en büyük meyve (karpuz) kaç damla sıçratsın")]
    [SerializeField] int _countMax = 34;

    [Tooltip("karpuz + karpuz birleşmesinde damla sayısı çarpanı")]
    [SerializeField] float _maxTierMultiplier = 1.8f;

    [Header("Geometri")]
    [Tooltip("damlaların çıktığı dairenin yarıçapı = meyve yarıçapı × bu. " +
             "1'e yakın olması suyun meyvenin KENARINDAN çıkmasını sağlar")]
    [SerializeField] float _emitRadiusFactor = 0.8f;

    [Tooltip("damla boyutu = meyve yarıçapı × bu")]
    [SerializeField] float _sizeFactor = 0.22f;

    [Header("Fışkırma hızı")]
    [Tooltip("en küçük meyvenin fışkırma hızı (birim/sn)")]
    [SerializeField] float _speedMin = 2.2f;

    [Tooltip("en büyük meyvenin fışkırma hızı")]
    [SerializeField] float _speedMax = 4.5f;

    [Header("Serpinti farkı")]
    [Tooltip("serpinti damlalarının boyut çarpanı — daha küçük olsun")]
    [SerializeField] float _mistSizeFactor = 0.45f;

    [Tooltip("serpinti hız çarpanı — daha hızlı ve geniş yayılsın")]
    [SerializeField] float _mistSpeedFactor = 1.5f;

    [Tooltip("serpinti damla sayısı çarpanı")]
    [SerializeField] float _mistCountFactor = 0.8f;

    const int DefaultMaxTier = 10;

    /// <summary>
    /// <see cref="MergeHandler"/> bir wildcard birleşmede ÖNCE <c>OnRainbowMerged</c>, hemen
    /// ARDINDAN (aynı çağrı içinde, senkron) <c>OnMerged</c>/<c>OnMaxTierMerged</c> yayınlıyor —
    /// bkz. MergeHandler.Execute. Bu bayrak o ikinci olayın normal tek-renk PlayJuice'unu
    /// atlatıyor: aynı noktada aynı anda İKİ patlama (biri düz renk, biri rengarenk) üst üste
    /// binmesin diye. Set edildiği karede İKİ olay da senkron işlendiği için tek kare içinde
    /// tüketiliyor, sızıntı riski yok.
    /// </summary>
    bool _skipNextJuice;

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
        if (Instance != this) return;

        GameEvents.OnMerged        += HandleMerged;
        GameEvents.OnMaxTierMerged += HandleMaxTierMerged;
        GameEvents.OnRainbowMerged += HandleRainbowMerged;
        GameEvents.OnRunStarted    += HandleRunStarted;
    }

    void OnDisable()
    {
        if (Instance != this) return;

        GameEvents.OnMerged        -= HandleMerged;
        GameEvents.OnMaxTierMerged -= HandleMaxTierMerged;
        GameEvents.OnRainbowMerged -= HandleRainbowMerged;
        GameEvents.OnRunStarted    -= HandleRunStarted;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------------------------------------------------------------- olaylar

    void HandleMerged(FruitDefinition produced, Vector2 position)
    {
        if (_skipNextJuice) { _skipNextJuice = false; return; }

        PlayJuice(position, produced, 1f);
    }

    void HandleMaxTierMerged(FruitDefinition def, Vector2 position)
    {
        if (_skipNextJuice) { _skipNextJuice = false; return; }

        PlayJuice(position, def, _maxTierMultiplier);
    }

    void HandleRainbowMerged(FruitDefinition def, Vector2 position)
    {
        _skipNextJuice = true;

        PlayRainbowBurst(position, def, 1f);
    }

    // Yeni oyun başlarken havada kalan damla olmasın. OnStateChanged(Playing) yerine
    // OnRunStarted: pause'dan dönüşte havadaki damlaları silmek gereksizdi.
    void HandleRunStarted() => ClearAll();

    // ------------------------------------------------------------- genel API

    /// <summary>
    /// Verilen noktada meyve suyu patlaması. Boostlar (Faz 8) da bunu kullanabilir.
    /// </summary>
    public void PlayJuice(Vector2 position, FruitDefinition source, float countMultiplier)
    {
        if (source == null) return;

        float radius = source.colliderRadius * source.scale;

        if (radius <= 0f) return;

        float t = TierT(source.tier);

        Color tint = source.displayColor;

        Emit(_juiceDroplets, position, tint, radius, t, countMultiplier, 1f, 1f);

        Emit(_juiceMist, position, tint, radius, t,
             countMultiplier * _mistCountFactor, _mistSizeFactor, _mistSpeedFactor);
    }

    /// <summary>
    /// Rainbow boost'un joker meyvesi birleşince çıkan patlama. <see cref="PlayJuice"/>'un
    /// BİREBİR AYNISI — aynı iki sistem (<see cref="_juiceDroplets"/>/<see cref="_juiceMist"/>),
    /// aynı şekil/boyut/hız/sayı hesabı. TEK fark: <see cref="Emit"/> helper'ı tüm damlaları
    /// TEK bir <c>EmitParams.startColor</c> ile aynı anda gönderirken, burası her damlayı
    /// KENDİ hue'suyla teker teker gönderiyor — böylece tek bir fışkırma rengi yerine
    /// gerçekten rengarenk bir patlama oluyor.
    /// </summary>
    public void PlayRainbowBurst(Vector2 position, FruitDefinition source, float countMultiplier)
    {
        if (source == null) return;

        float radius = source.colliderRadius * source.scale;

        if (radius <= 0f) return;

        float t = TierT(source.tier);

        EmitRainbow(_juiceDroplets, position, radius, t, countMultiplier, 1f, 1f);

        EmitRainbow(_juiceMist, position, radius, t,
                    countMultiplier * _mistCountFactor, _mistSizeFactor, _mistSpeedFactor);
    }

    /// <summary>
    /// Kurtçuk boost'unun sisi. Meyveyi kaplayan, meyve renginde bir bulut —
    /// çağıran her karede kaç parçacık istediğini kendisi hesaplar (yoğunluk rampası
    /// orada), burası sadece Emit eder.
    /// </summary>
    /// <param name="center">meyvenin merkezi</param>
    /// <param name="tint">meyvenin displayColor'ı</param>
    /// <param name="radius">parçacıkların doğduğu dairenin yarıçapı</param>
    /// <param name="particleSize">tek parçacığın çapı (dünya birimi)</param>
    /// <param name="alpha">o andaki yoğunluk (0-1)</param>
    /// <param name="count">bu karede kaç parçacık</param>
    /// <param name="lifetime">parçacık ömrü (sn)</param>
    public void EmitEatSmoke(Vector2 center, Color tint, float radius,
                             float particleSize, float alpha, int count, float lifetime)
    {
        if (_eatSmoke == null || count <= 0) return;

        var shape = _eatSmoke.shape;
        shape.radius = Mathf.Max(0.03f, radius);

        var main = _eatSmoke.main;
        main.startSizeMultiplier = Mathf.Max(0.02f, particleSize);
        main.startLifetimeMultiplier = Mathf.Max(0.05f, lifetime);

        tint.a = Mathf.Clamp01(alpha);

        var p = new ParticleSystem.EmitParams();

        p.position = center;
        p.applyShapeToPosition = true;
        p.startColor = tint;

        _eatSmoke.Emit(p, count);
    }

    /// <summary>
    /// Deprem boost'unun tozu. Sisin aksine noktasal değil bir <b>şerit</b> boyunca çıkıyor —
    /// sarsılan şey tek bir meyve değil. Çağıran bunu üç kez çağırıyor: zemin için YATAY,
    /// iki duvar için DİKEY şerit. Şeridin şekli <paramref name="halfExtents"/> ile geliyor,
    /// bu yüzden tek metot üçüne de hizmet ediyor.
    ///
    /// Yoğunluk rampasını çağıran hesaplıyor (deprem zarfı orada), burası sadece Emit ediyor —
    /// <see cref="EmitEatSmoke"/> ile aynı sözleşme.
    /// </summary>
    /// <param name="center">şeridin merkezi</param>
    /// <param name="halfExtents">şeridin yarı boyutları. Zemin: (genişlik, ~0) · duvar: (~0, yükseklik)</param>
    /// <param name="tint">toz rengi</param>
    /// <param name="count">bu karede kaç parçacık</param>
    /// <param name="alpha">o andaki yoğunluk (0-1)</param>
    /// <param name="particleSize">tek parçacığın çapı (dünya birimi)</param>
    /// <param name="lifetime">parçacık ömrü (sn)</param>
    public void EmitQuakeDust(Vector2 center, Vector2 halfExtents, Color tint,
                              int count, float alpha, float particleSize, float lifetime)
    {
        if (_quakeDust == null || count <= 0) return;

        // ShapeModule bir struct sarmalayıcı — atama doğrudan sisteme yazıyor.
        var shape = _quakeDust.shape;
        shape.scale = new Vector3(Mathf.Max(0.01f, halfExtents.x * 2f),
                                  Mathf.Max(0.01f, halfExtents.y * 2f), 0f);

        var main = _quakeDust.main;
        main.startSizeMultiplier     = Mathf.Max(0.02f, particleSize);
        main.startLifetimeMultiplier = Mathf.Max(0.05f, lifetime);

        tint.a = Mathf.Clamp01(alpha);

        var p = new ParticleSystem.EmitParams();

        p.position = center;
        p.applyShapeToPosition = true;
        p.startColor = tint;

        _quakeDust.Emit(p, count);
    }

    /// <summary>
    /// Deprem boost'unun düşen molozu. Tozun aksine <b>dikey</b> bir şeritten çıkıyor:
    /// çağıran sol ve sağ kenar için ayrı ayrı çağırıyor. Düşme işini sistemin kendi
    /// <c>gravityModifier</c>'ı yapıyor, burada hız verilmiyor.
    /// </summary>
    /// <param name="center">şeridin merkezi (bir kenarın üstü)</param>
    /// <param name="verticalSpread">şeridin dikey uzunluğu — hepsi aynı yükseklikten düşmesin</param>
    /// <param name="tint">moloz rengi</param>
    /// <param name="count">bu karede kaç parça</param>
    /// <param name="particleSize">tek parçanın çapı (dünya birimi)</param>
    /// <param name="lifetime">parça ömrü (sn)</param>
    public void EmitQuakeRubble(Vector2 center, float verticalSpread, Color tint,
                                int count, float particleSize, float lifetime)
    {
        if (_quakeRubble == null || count <= 0) return;

        var shape = _quakeRubble.shape;
        shape.scale = new Vector3(0.05f, Mathf.Max(0.1f, verticalSpread), 0f);

        var main = _quakeRubble.main;
        main.startSizeMultiplier     = Mathf.Max(0.02f, particleSize);
        main.startLifetimeMultiplier = Mathf.Max(0.05f, lifetime);

        var p = new ParticleSystem.EmitParams();

        p.position = center;
        p.applyShapeToPosition = true;
        p.startColor = tint;

        _quakeRubble.Emit(p, count);
    }

    public void ClearAll()
    {
        if (_juiceDroplets != null) _juiceDroplets.Clear();
        if (_juiceMist != null)     _juiceMist.Clear();
        if (_eatSmoke != null)      _eatSmoke.Clear();
        if (_quakeDust != null)     _quakeDust.Clear();
        if (_quakeRubble != null)   _quakeRubble.Clear();
    }

    // ---------------------------------------------------------------- çekirdek

    void Emit(ParticleSystem ps, Vector2 position, Color tint,
              float radius, float t, float countMul, float sizeMul, float speedMul)
    {
        if (ps == null) return;

        int count = Mathf.RoundToInt(Mathf.Lerp(_countMin, _countMax, t) * countMul);

        if (count <= 0) return;

        // Şekil yarıçapını meyveye göre ayarla — su gövdenin kenarından çıksın.
        // ShapeModule bir struct sarmalayıcı, atama doğrudan sisteme yazıyor.
        var shape = ps.shape;
        shape.radius = Mathf.Max(0.03f, radius * _emitRadiusFactor);

        var main = ps.main;
        main.startSpeedMultiplier = Mathf.Lerp(_speedMin, _speedMax, t) * speedMul;
        main.startSizeMultiplier  = Mathf.Max(0.015f, radius * _sizeFactor * sizeMul);

        // EmitParams struct — allocation yok
        var p = new ParticleSystem.EmitParams();

        p.position = position;
        p.applyShapeToPosition = true;
        p.startColor = tint;

        ps.Emit(p, count);
    }

    /// <summary>
    /// <see cref="Emit"/> ile AYNI şekil/boyut/hız kurulumu — tek fark, TEK bir tint yerine
    /// her damlayı kendi rastgele hue'suyla teker teker <c>Emit(p, 1)</c> ile gönderiyor.
    /// Sayı/boyut/hız hesabı kasıtlı olarak <see cref="Emit"/> ile BİREBİR aynı satırlar:
    /// rainbow patlaması normal meyve suyundan görsel olarak ayrışmasın, sadece rengi.
    /// </summary>
    void EmitRainbow(ParticleSystem ps, Vector2 position, float radius, float t,
                     float countMul, float sizeMul, float speedMul)
    {
        if (ps == null) return;

        int count = Mathf.RoundToInt(Mathf.Lerp(_countMin, _countMax, t) * countMul);

        if (count <= 0) return;

        var shape = ps.shape;
        shape.radius = Mathf.Max(0.03f, radius * _emitRadiusFactor);

        var main = ps.main;
        main.startSpeedMultiplier = Mathf.Lerp(_speedMin, _speedMax, t) * speedMul;
        main.startSizeMultiplier  = Mathf.Max(0.015f, radius * _sizeFactor * sizeMul);

        var p = new ParticleSystem.EmitParams();

        p.position = position;
        p.applyShapeToPosition = true;

        for (int i = 0; i < count; i++)
        {
            // i/count ile eşit aralıklı hue + küçük bir rastgele sapma: tamamen düzenli
            // bir gökkuşağı sırası yerine biraz daha organik bir karışım.
            float hue = (i / (float)count + Random.value * 0.08f) % 1f;

            p.startColor = Color.HSVToRGB(hue, _rainbowSaturation, _rainbowValue);

            ps.Emit(p, 1);
        }
    }

    float TierT(int tier)
    {
        int max = _database != null ? Mathf.Max(1, _database.MaxTier) : DefaultMaxTier;

        return Mathf.Clamp01(tier / (float)max);
    }
}
