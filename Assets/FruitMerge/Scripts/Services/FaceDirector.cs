using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tüm meyve yüzlerini tek Update'ten yönetir.
///
/// Öncelik sırası (K1 kararı: kutlama danger'ın ÜSTÜNDE):
/// <code>
/// 1. Oyun sonu          (dizzy / squish)  -> kalıcı kilit
/// 2. Meyve ifade kilidi (love 2 sn)       -> FruitFace içinde, global modu ezer
/// 3. Boost odağı        (scared + surprised) -> kurtçuk boost'u hedef seçtiğinde
/// 4. Kalabalık kutlaması (happy 2 sn)     -> tier >= faceCrowdReactionMinTier
/// 5. Danger — MEYVE BAŞINA:
///       tepesi çizgiyi geçmiş            -> scared
///       çizgiye %15'ten az kalmış        -> worried
/// 5. Sleepy             (5 sn hareket yok)
/// 6. Düşme/sürükleme takibi (idle + bakış)
/// 7. Idle
/// </code>
///
/// <b>Performans yapısı</b>
///  - Tek <c>Update</c>. Meyvelerin ve yüzlerin kendi Update'i yok (kural 7).
///  - <c>FindObjectsOfType</c> YOK. Meyve listesi <c>FruitPool.Active</c> — havuzun
///    zaten tuttuğu <c>List&lt;Fruit&gt;</c>, index'le geziliyor.
///  - <b>İfade kararı 10 Hz</b> (<c>faceMoodInterval</c>). Her karede dönen tek şey
///    <c>face.Tick(dt)</c> ve bakış hedefi — ikisi de saf matematik.
///  - Danger histerezis state'i meyvenin kendi <c>FruitFace</c>'inde duruyor:
///    Dictionary yok, arama yok, allocation yok.
///  - Zemin yüksekliği <c>GameOverDetector.FloorY</c>'de bir kez önbelleğe alınıyor —
///    <c>Collider2D.bounds</c> native çağrısı meyve başına tekrarlanmıyor.
/// </summary>
[DefaultExecutionOrder(50)]
public class FaceDirector : MonoBehaviour
{
    public static FaceDirector Instance { get; private set; }

    [Header("Referanslar")]
    [SerializeField] GameConfig _config;

    [Tooltip("danger line ve zemin yüksekliği buradan okunuyor")]
    [SerializeField] GameOverDetector _detector;

    float _moodTimer;
    float _lastActivityTime;
    float _crowdHappyLeft;

    bool _gameOverApplied;

    /// <summary>En az bir meyve worried/scared mı — Faz 3D'deki tam ekran uyarısı için.</summary>
    public bool DangerActive { get; private set; }

    Transform _falling;

    // oyuncunun parmağında duran, henüz bırakılmamış meyve
    Transform _pending;
    float _lastPendingX;
    bool _hasPendingX;

    // her karede kullanılan bakış hedefi ve çizgi yüksekliği (10 Hz'te tazelenir)
    Transform _lookTarget;
    float _lineY;

    /// <summary>
    /// Deprem boost'u oynuyor mu. Doluyken tahtanın TAMAMI <c>Surprised</c> oluyor.
    /// <see cref="_boostFocus"/> gibi tek bir hedef yok — sarsılan şey her şey.
    /// </summary>
    bool _quakeMood;

    /// <summary>
    /// Kurtçuk boost'unun hedefi. Doluyken tahtanın tamamı bu meyveyi seyrediyor:
    /// hedef <c>scared</c>, diğerleri <c>surprised</c> ve hepsinin bakışı orada.
    /// Yüzleri dışarıdan <c>Express</c> ile zorlamak yerine burada tutuluyor —
    /// yoksa boost ile danger/kutlama mantığı aynı yüz üzerinde çekişirdi.
    /// </summary>
    Transform _boostFocus;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        _lastActivityTime = Time.time;
    }

    void OnEnable()
    {
        if (Instance != this) return;

        GameEvents.OnFruitDropped   += HandleFruitDropped;
        GameEvents.OnMerged         += HandleMerged;
        GameEvents.OnMaxTierMerged   += HandleMaxTierMerged;
        GameEvents.OnGameOver       += HandleGameOver;
        GameEvents.OnStateChanged   += HandleStateChanged;
    }

    void OnDisable()
    {
        if (Instance != this) return;

        GameEvents.OnFruitDropped   -= HandleFruitDropped;
        GameEvents.OnMerged         -= HandleMerged;
        GameEvents.OnMaxTierMerged   -= HandleMaxTierMerged;
        GameEvents.OnGameOver       -= HandleGameOver;
        GameEvents.OnStateChanged   -= HandleStateChanged;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------ genel API

    /// <summary>
    /// "Ortalık hâlâ hareketli" bildirimi — uyuklama sayacını sıfırlar.
    ///
    /// Uyuklama ölçütü "son BIRAKMADAN beri geçen süre" (<c>faceIdleToSleepy</c>, 5 sn).
    /// Kurtçuk boost'u 5.5 saniye sürüyor ve o süre boyunca meyve bırakılmıyor — sayaç
    /// dolduğu için meyveler boost daha bitmeden uyuklamaya başlıyordu. Boost gibi
    /// bırakma içermeyen ama açıkça "bir şeyler oluyor" olan durumlar bunu her karede
    /// çağırır; sayaç ancak o iş bittikten sonra işlemeye başlar.
    /// </summary>
    public void NotifyActivity() => _lastActivityTime = Time.time;

    /// <summary>
    /// Uyuklama sayacını <paramref name="seconds"/> saniye İLERİ atar: geri sayım
    /// o süre dolduktan SONRA başlar. Süresi baştan bilinen sahneler (kurtçuk boost'u)
    /// bunu bir kez çağırıp bitiyor — her karede sayaç tazelemeye gerek kalmıyor.
    /// </summary>
    public void SuppressSleepFor(float seconds) => _lastActivityTime = Time.time + Mathf.Max(0f, seconds);

    /// <summary>
    /// Boost odağını ayarlar. <c>null</c> vermek normal moda döndürür.
    /// Odak bir sonraki karar turunda (10 Hz) yüzlere yansır.
    /// </summary>
    public void SetBoostFocus(Transform target)
    {
        if (_boostFocus == target) return;

        _boostFocus = target;

        // odak değişince bir sonraki karar turunu bekleme, hemen uygula
        _moodTimer = 0f;
    }

    /// <summary>
    /// Deprem modu. Açıkken bütün meyveler <c>Surprised</c> olup <b>kendi hareket yönlerine</b>
    /// bakıyor (bkz. <see cref="TickFaces"/>). Tek hedefli <see cref="SetBoostFocus"/>'un
    /// aksine burada odaklanılacak bir meyve yok; bu yüzden ayrı bir bayrak, ama aynı öncelik
    /// katmanında duruyor — oyun sonu ve meyvenin kendi <c>Express</c> kilidi bunu hâlâ eziyor.
    /// </summary>
    public void SetQuakeMood(bool active)
    {
        if (_quakeMood == active) return;

        _quakeMood = active;

        // deprem bir anda başlıyor, bir sonraki 10 Hz turunu bekleme
        _moodTimer = 0f;
    }

    // ---------------------------------------------------------------- olaylar

    /// <summary>
    /// Bırakma anı. İfade kararı normalde 10 Hz'te dönüyor ama bakış hedefinin
    /// devri BEKLEYEMEZ: karar turunu kaçıran bırakmada yüzler 100 ms'ye kadar eski
    /// hedefe (artık var olmayan parmaktaki meyveye) bakıp merkeze kayıyordu.
    /// Sayacı sıfırlamak bir sonraki karede yeniden değerlendirmeyi zorluyor —
    /// <see cref="SetBoostFocus"/> ile aynı yöntem.
    /// </summary>
    void HandleFruitDropped(FruitDefinition def)
    {
        _lastActivityTime = Time.time;

        _moodTimer = 0f;
    }

    void HandleMerged(FruitDefinition produced, Vector2 position)
    {
        if (produced == null || _config == null) return;

        // Nitelikli her birleşme sayacı SIFIRDAN başlatır — zincirde kutlama kesintisiz sürer
        if (produced.tier >= _config.faceCrowdReactionMinTier)
            _crowdHappyLeft = _config.faceMergeReactionTime;
    }

    void HandleMaxTierMerged(FruitDefinition def, Vector2 position)
    {
        // iki karpuz da yok oldu, love verilecek meyve kalmadı — sadece kalabalık sevinir
        if (_config != null) _crowdHappyLeft = _config.faceMergeReactionTime;
    }

    void HandleGameOver(int finalScore)
    {
        _gameOverApplied = true;

        FruitPool pool = FruitPool.Instance;

        if (pool == null) return;

        float lineY = _detector != null ? _detector.LineY : 0f;

        IReadOnlyList<Fruit> active = pool.Active;

        for (int i = 0; i < active.Count; i++)
        {
            Fruit f = active[i];

            if (f == null || f.Face == null) continue;

            f.Face.ClearLook();

            // çizginin üstünde kalanlar sersem, altındakiler ezilmiş
            FaceExpression e = f.transform.position.y > lineY
                ? FaceExpression.Dizzy
                : FaceExpression.Squish;

            f.Face.Express(e, float.MaxValue);
        }
    }

    void HandleStateChanged(GameState s)
    {
        if (s != GameState.Playing) return;

        _gameOverApplied = false;
        DangerActive = false;
        _crowdHappyLeft = 0f;
        _lastActivityTime = Time.time;
        _falling = null;
        _pending = null;
        _lookTarget = null;
        _hasPendingX = false;
        _boostFocus = null;
        _quakeMood = false;
    }

    // ----------------------------------------------------------------- döngü

    void Update()
    {
        FruitPool pool = FruitPool.Instance;

        if (pool == null || _config == null) return;

        float dt = Time.deltaTime;

        IReadOnlyList<Fruit> active = pool.Active;

        if (!_gameOverApplied)
        {
            if (_crowdHappyLeft > 0f) _crowdHappyLeft -= dt;

            _moodTimer -= dt;

            if (_moodTimer <= 0f)
            {
                _moodTimer = Mathf.Max(0.02f, _config.faceMoodInterval);

                EvaluateAndAssign(active);
            }
        }

        TickFaces(active, dt);
    }

    /// <summary>
    /// 10 Hz'te çalışan karar turu: hedefleri bul, her meyvenin ifadesini ata.
    /// Bu turun dışında hiçbir ifade değişmiyor.
    /// </summary>
    void EvaluateAndAssign(IReadOnlyList<Fruit> active)
    {
        float now = Time.time;

        _falling = null;
        _pending = null;

        float newest = float.MinValue;

        for (int i = 0; i < active.Count; i++)
        {
            Fruit f = active[i];

            if (f == null || f.IsMerging) continue;

            // Bırakılmamış meyve = oyuncunun parmağında sürüklediği
            if (!f.IsDropped)
            {
                _pending = f.transform;
                continue;
            }

            // "Düşüyor" için iki şart: yakın zamanda bırakılmış VE aşağı gidiyor.
            // Sadece "hızlı" demek yetmiyor — tahta dolunca büyük meyveler birbirini
            // itip sürekli hareket ediyor ve bakış hedefini kalıcı olarak çalıyorlardı.
            if (now - f.DropTime > _config.faceFallFollowTime) continue;

            // Hız kapısı, OYUNCUNUN bıraktığı meyvenin ilk anlarında uygulanmıyor.
            // Meyve duruyorken bırakılıyor; yerçekiminin eşiğe ulaşması ~0.15 sn sürüyor
            // ve o pencerede meyve "düşen" sayılmıyordu. Yeni bekleyen meyve de hemen
            // doğmadığı için (DropController düşen uzaklaşana kadar bekletiyor) bakış
            // hedefi tamamen boşa düşüyor, bütün yüzler merkeze kayıp sonra takibe
            // geri dönüyordu.
            //
            // WasPlayerDropped şart: birleşmeden doğan meyve de Drop() çağırıp aynı
            // DropTime'ı alıyor. Muafiyeti ona da versek her birleşme bakışı kendine
            // çekerdi — oysa hız kapısı tam olarak onları elemek için var.
            bool justReleased = f.WasPlayerDropped &&
                                now - f.DropTime <= _config.faceFallGrace;

            if (!justReleased &&
                f.Body.linearVelocity.y > -_config.faceFallSpeedThreshold) continue;

            if (f.DropTime <= newest) continue;

            newest = f.DropTime;
            _falling = f.transform;
        }

        // Sürükleme de "oyuncu aktif" sayılır — parmağıyla oynarken meyveler uyumasın
        if (_pending != null)
        {
            float x = _pending.position.x;

            if (_hasPendingX && Mathf.Abs(x - _lastPendingX) > 0.01f) _lastActivityTime = now;

            _lastPendingX = x;
            _hasPendingX = true;
        }
        else
        {
            _hasPendingX = false;
        }

        // Düşen meyve daha dramatik; yoksa parmakta sürüklenen. Bırakma anında ikisi
        // birlikte var olur (Drop hemen yeni pending üretiyor).
        _lookTarget = _falling != null ? _falling : _pending;

        // --- ifadeleri ata ---
        _lineY = _detector != null ? _detector.LineY : 0f;

        float floorY = _detector != null ? _detector.FloorY : _lineY - 5f;
        float span = _lineY - floorY;

        bool celebrating = _crowdHappyLeft > 0f;
        bool sleepy = now - _lastActivityTime > _config.faceIdleToSleepy;

        // hedef havuza döndüyse (yendi) odak kendiliğinden düşsün
        if (_boostFocus != null && !_boostFocus.gameObject.activeSelf) _boostFocus = null;

        bool boosting = _boostFocus != null;

        bool anyDanger = false;

        for (int i = 0; i < active.Count; i++)
        {
            Fruit f = active[i];

            if (f == null) continue;

            FruitFace face = f.Face;

            if (face == null) continue;

            FaceDangerState danger = ClassifyDanger(f, floorY, span, now);

            face.DangerState = danger;

            if (danger != FaceDangerState.None) anyDanger = true;

            // Boost odağı her şeyi bastırır: kurtçuklar bir meyveye saldırırken
            // tahtanın geri kalanının kutlaması ya da uyuklaması anlamsız olurdu.
            if (boosting)
            {
                face.SetExpression(f.transform == _boostFocus
                    ? FaceExpression.Scared
                    : FaceExpression.Surprised);
            }
            // Deprem: kimse kutlamıyor, kimse uyuklamıyor — hepsi şaşkın.
            // Boost odağının hemen ALTINDA çünkü ikisi aynı anda olamaz (BoostGate engelliyor);
            // yine de bir sıra gerekiyorsa hedefli boost daha spesifik olan.
            else if (_quakeMood)                          face.SetExpression(FaceExpression.Surprised);
            // K1: kutlama danger'ı bastırır
            else if (celebrating)                         face.SetExpression(FaceExpression.Happy);
            else if (danger == FaceDangerState.Scared)    face.SetExpression(FaceExpression.Scared);
            else if (danger == FaceDangerState.Worried)   face.SetExpression(FaceExpression.Worried);
            else if (sleepy)                              face.SetExpression(FaceExpression.Sleepy);
            else                                          face.SetExpression(FaceExpression.Idle);
        }

        DangerActive = anyDanger;
    }

    /// <summary>
    /// Meyvenin danger line'a yakınlığını tek orana indirger ve histerezisli sınıflandırır.
    /// <c>proximity = (tepe - zemin) / (çizgi - zemin)</c> — 0 tabanda, 1.0 tam çizgide.
    /// </summary>
    FaceDangerState ClassifyDanger(Fruit f, float floorY, float span, float now)
    {
        // parmaktaki meyve dropY'de (çizginin çok üstünde) duruyor — tehlike sayılmaz
        if (!f.IsDropped) return FaceDangerState.None;

        // yeni bırakılmış meyve havada çizgiyi geçerken korkmuş görünmesin
        if (now - f.DropTime < _config.dropGracePeriod) return FaceDangerState.None;

        if (span <= 0.0001f) return FaceDangerState.None;

        float proximity = (f.TopY - floorY) / span;

        FaceDangerState prev = f.Face.DangerState;
        float h = _config.faceDangerHysteresis;

        // Bir durumda kalmak için eşiğin h kadar altına kadar tolerans var, girmek için
        // eşiği tam geçmek gerekir.
        float scaredThreshold  = prev == FaceDangerState.Scared
            ? _config.faceScaredRatio - h
            : _config.faceScaredRatio;

        if (proximity >= scaredThreshold) return FaceDangerState.Scared;

        float worriedThreshold = prev != FaceDangerState.None
            ? _config.faceWorriedRatio - h
            : _config.faceWorriedRatio;

        if (proximity >= worriedThreshold) return FaceDangerState.Worried;

        return FaceDangerState.None;
    }

    /// <summary>Her karede: bakış hedefi + geçiş yumuşatma. İfade burada DEĞİŞMEZ.</summary>
    void TickFaces(IReadOnlyList<Fruit> active, float dt)
    {
        // Boost odağının konumu döngünün DIŞINDA bir kez okunuyor. Transform.position
        // native bir çağrı; meyve başına okusaydık 40 meyvede kare başına 40 gereksiz
        // geçiş olurdu (kural 11 — sıcak döngüde tekrar eden native erişim yok).
        bool hasFocus = _boostFocus != null;

        Vector2 focusPos = hasFocus ? (Vector2)_boostFocus.position : default;

        // Deprem bakış eşiği döngünün DIŞINDA bir kez kareleniyor — meyve başına
        // çarpma yapmaya gerek yok (kural 11)
        float quakeLookMinSqr = _quakeMood
            ? _config.quakeLookMinSpeed * _config.quakeLookMinSpeed
            : 0f;

        for (int i = 0; i < active.Count; i++)
        {
            Fruit f = active[i];

            if (f == null) continue;

            FruitFace face = f.Face;

            if (face == null) continue;

            if (!_gameOverApplied)
            {
                // boost sırasında tahtanın tamamı hedefi seyreder; hedef ise
                // gözünü kaçıracak yeri olmadığı için düz bakar
                if (hasFocus)
                {
                    if (f.transform == _boostFocus) face.ClearLook();
                    else                            face.SetLookPoint(focusPos);
                }
                // Deprem: her meyve KENDİ gittiği yöne bakıyor. Yön director'den değil
                // meyvenin gerçek hızından okunuyor — böylece her meyve bağımsız ve bakışlar
                // hareket değiştikçe kendiliğinden değişiyor. Bakış zaten faceLookSpeed ile
                // yumuşatıldığı için hızın gürültüsü göze titreme olarak yansımıyor.
                // SetLookPoint deltayı normalize ediyor, o yüzden 1 birim mesafe yeter.
                else if (_quakeMood)
                {
                    Vector2 vel = f.Body.linearVelocity;

                    if (vel.sqrMagnitude > quakeLookMinSqr)
                        face.SetLookPoint((Vector2)f.transform.position + vel / vel.magnitude);
                    else
                        face.ClearLook();
                }
                // korkan/endişeli meyve çizgiye bakar, diğerleri parmaktaki/düşen meyveye
                else if (face.DangerState != FaceDangerState.None)
                {
                    face.SetLookPoint(new Vector2(f.transform.position.x, _lineY));
                }
                else if (_lookTarget != null && _lookTarget != f.transform)
                {
                    face.SetLookPoint(_lookTarget.position);
                }
                else
                {
                    face.ClearLook();
                }
            }

            face.Tick(dt);
        }
    }
}
