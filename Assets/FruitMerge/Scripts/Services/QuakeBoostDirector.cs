using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// "Deprem" boost'u — baştan sona.
///
/// Kurtçuk boost'unun aksine <b>hedefsiz</b>: butona basılır, tahtanın tamamı sarsılır,
/// hiçbir meyve silinmez.
///
/// Akış: <b>Shake</b> (uzun titreşim) → <b>Settle</b> (yatışma).
///
/// <b>Amaç fırlatmak değil TİTREŞTİRMEK.</b> Her meyveye kısa aralıklarla rastgele YÖNLÜ
/// (yukarı/aşağı/sağa/sola karışık) küçük bir hız veriliyor ve mevcut hız her seferinde
/// sönümleniyor. Sönümleme olmadan itmeler üst üste binip meyveleri ekranın tepesine
/// fırlatıyordu — bkz. <see cref="GameConfig.quakeKickDamping"/>.
///
/// Sıkışık yığın böyle çalkalanırken birbirine değmeyen meyveler değiyor ve
/// <c>Fruit.OnCollisionStay2D → MergeHandler</c> zinciri devreye giriyor.
/// <b>Deprem için tek satır merge kodu yazılmadı.</b>
///
/// Mimari:
///  - <see cref="Update"/> GÖRSEL/SES (kamera genliği, gürültü seviyesi, toz, moloz, faz
///    sayaçları), <see cref="FixedUpdate"/> sadece FİZİK (itmeler). Fizik yazması fizik adımına ait.
///  - Coroutine yok (kural 8) — bütün zamanlama float sayaç.
///  - Sıcak döngüde allocation yok (kural 11): <c>for</c> + index, LINQ yok.
///  - Hiçbir obje yaratmıyor: bütün görsel iş paylaşımlı parçacık sistemlerinde
///    (<see cref="EffectDirector"/>) ve kamerada.
/// </summary>
[DefaultExecutionOrder(-30)]
public class QuakeBoostDirector : MonoBehaviour, IBoostDirector
{
    public static QuakeBoostDirector Instance { get; private set; }

    public BoostId Id => BoostId.Quake;

    enum State { Idle, Shake, Settle }

    [Header("Referanslar")]
    [SerializeField] FruitPool  _pool;
    [SerializeField] GameConfig _config;

    [Tooltip("itme çarpanını tier'a göre ölçeklemek için")]
    [SerializeField] FruitDatabase _database;

    [Tooltip("Wall_Bottom'ın collider'ı. Zemin yüzeyi (toz şeridi) buradan ölçülüyor — " +
             "GameOverDetector ile aynı desen, bir kez okunup saklanıyor")]
    [SerializeField] Collider2D _floor;

    /// <summary>Meyve veritabanı bağlı değilse tier normalizasyonu için varsayılan üst sınır.</summary>
    const int DefaultMaxTier = 10;

    public bool IsBusy => _state != State.Idle;

    /// <summary>
    /// Hedefsiz bir boost olduğu için "silahlı" ve "meşgul" çakışıyor. HUD butonundaki
    /// parlama halkasını bu besliyor: deprem oynarken buton parlıyor.
    /// </summary>
    public bool IsArmed => _state != State.Idle;

    public int Charges => _charges;

    public bool CanArm => _charges != 0
                          && GameManager.Instance != null
                          && GameManager.Instance.IsPlaying
                          && !BoostGate.IsAnyBusy;   // başka bir boost oynarken başlamaz

    State _state;
    float _stateTime;
    int   _charges;

    /// <summary>Bir sonraki itmeye kalan süre (sn). FixedUpdate'te azalıyor.</summary>
    float _kickTimer;

    /// <summary>
    /// Yön dilimlerine eklenen, her depremde değişen ofset. Aynı meyve her deprem aynı yön
    /// dizisini almasın diye.
    /// </summary>
    int _slotSalt;

    /// <summary>Kesirli parçacık sayısı birikimi — kare süresinden bağımsız sabit toz debisi.</summary>
    float _dustAccum;

    /// <summary>Aynısı moloz için. Debi düşük olduğu için birikim şart.</summary>
    float _rubbleAccum;

    float _floorY;

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
    }

    void OnDisable()
    {
        BoostGate.Unregister(this);

        GameEvents.OnRunStarted   -= HandleRunStarted;
        GameEvents.OnStateChanged -= HandleStateChanged;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // Collider2D.bounds native bir çağrı ve zemin hiç hareket etmiyor — bir kez oku.
        if (_floor != null)
        {
            _floorY = _floor.bounds.max.y;
        }
        else
        {
            _floorY = 0f;

            Debug.LogWarning("QuakeBoostDirector: _floor bağlı değil — zemin yüksekliği 0 " +
                             "sayılıyor, toz yanlış yerde çıkacak. " +
                             "Environment/Container/Wall_Bottom'ı bağla.");
        }

        _charges = _config != null ? _config.quakeChargesPerRun : 0;

        GameEvents.RaiseBoostStateChanged(BoostId.Quake, false, _charges);
    }

    // ----------------------------------------------------------------- olaylar

    void HandleRunStarted()
    {
        Abort();

        _charges = _config != null ? _config.quakeChargesPerRun : 0;

        GameEvents.RaiseBoostStateChanged(BoostId.Quake, false, _charges);
    }

    void HandleStateChanged(GameState s)
    {
        // PAUSE = DONDUR, iptal etme. Eskiden burada Abort() çağrılıyordu: oyuncu depremin
        // ortasında pause'a bastığı an sarsıntı kesiliyor ve kullanım boşa gidiyordu
        // (kullanım Begin'de zaten harcanmış oluyor). Artık sahne olduğu gibi donuyor ve
        // Continue kaldığı yerden devam ediyor.
        //
        // Zamanın ve fiziğin durmasını timeScale = 0 hallediyor: _stateTime Time.deltaTime
        // ile ilerliyor, FixedUpdate hiç çağrılmıyor, parçacıklar da donuyor. Burada
        // yalnızca timeScale'e BAĞLI OLMAYAN kanalı susturuyoruz — ses.
        if (s == GameState.Paused)
        {
            if (_state == State.Idle) return;

            if (AudioService.Instance != null) AudioService.Instance.PauseQuakeRumble();

            // Titreşimi HapticService kendi OnStateChanged'inde susturuyor (o servis
            // unscaledDeltaTime ile döndüğü için pause'da titremeye devam ederdi).
            return;
        }

        if (s == GameState.Playing)
        {
            ResumeFromPause();

            return;
        }

        // Menü / oyun sonu: burada gerçekten iptal — yarım kalmış bir deprem kamerayı
        // kaydırılmış halde dondurmasın.
        Abort();
    }

    /// <summary>
    /// Pause'dan dönüş. Yeni oyunda da çağrılıyor ama <c>_state == Idle</c> olduğu için
    /// hiçbir şey yapmıyor (yeni oyunun sıfırlaması <see cref="HandleRunStarted"/>'da).
    /// </summary>
    void ResumeFromPause()
    {
        if (_state == State.Idle) return;

        if (AudioService.Instance != null) AudioService.Instance.ResumeQuakeRumble();

        if (HapticService.Instance != null) HapticService.Instance.ResumeQuake();

        // FaceDirector OnStateChanged(Playing)'de her şeyi sıfırlıyor (yeni oyun için
        // doğru) — depremin yüz durumunu geri yazmak bize kalıyor.
        if (FaceDirector.Instance != null)
        {
            FaceDirector.Instance.SetQuakeMood(true);
            FaceDirector.Instance.SuppressSleepFor(TotalDuration);
        }
    }

    // ----------------------------------------------------------------- genel API

    /// <summary>
    /// HUD butonu. Hedefsiz olduğu için "iptal" diye bir hâl yok — deprem başladıysa
    /// tekrar basmak hiçbir şey yapmaz.
    /// </summary>
    public void Toggle()
    {
        if (_state != State.Idle) return;

        if (!CanArm) return;

        Begin();
    }

    /// <summary>Mağazadan satın alma. Sınırsız moddaysa (-1) dokunma.</summary>
    public void AddCharge(int amount)
    {
        if (amount <= 0 || _charges < 0) return;

        _charges += amount;

        GameEvents.RaiseBoostStateChanged(BoostId.Quake, false, _charges);
    }

    void Abort()
    {
        if (_state == State.Idle) return;

        Stop();
    }

    // -------------------------------------------------------------------- başlat

    void Begin()
    {
        if (_charges > 0) _charges--;

        _state       = State.Shake;
        _stateTime   = 0f;
        _kickTimer   = 0f;
        _dustAccum   = 0f;
        _rubbleAccum = 0f;

        // Sıkışık yığında meyveler uyuyor olabilir; ayrıca duvarlar sadece 0.3 birim kalın.
        // İkisinin de çaresi itmeler başlamadan ÖNCE hazırlanıyor.
        ArmFruitsForShaking();

        // Her deprem farklı bir yön dizisi üretsin, yoksa aynı meyve her seferinde aynı
        // yönlere gider
        _slotSalt = Random.Range(0, 10000);

        // İlk andaki vuruş — düz bir rampa yerine "deprem başladı" snap'i.
        if (CameraShaker.Instance != null)
            CameraShaker.Instance.Punch(_config.quakeStartPunch, _config.quakeAttackTime);

        if (FaceDirector.Instance != null)
        {
            FaceDirector.Instance.SetQuakeMood(true);

            // Uyuklama ölçütü "son bırakmadan beri geçen süre" (5 sn) ve deprem boyunca meyve
            // bırakılmıyor — sayacı TEK ÇAĞRIYLA tüm boost süresi kadar ileri at, yoksa
            // meyveler deprem sürerken uyuklamaya başlıyor.
            FaceDirector.Instance.SuppressSleepFor(TotalDuration);
        }

        // Ses ve titreşim buradan değil olaydan besleniyor — depremi duyan başka sistemler
        // (ileride görev/başarım) director'e bağlanmasın.
        GameEvents.RaiseQuakeStarted();

        GameEvents.RaiseBoostStateChanged(BoostId.Quake, true, _charges);
    }

    float TotalDuration => _config == null
        ? 0f
        : _config.quakeShakeDuration + _config.quakeSettleDuration;

    /// <summary>
    /// Sarsılacak meyveleri hazırlar: <c>Continuous</c> çarpışma tespiti. Duvarlar 0.3 birim
    /// kalın, hızlanan meyve sweep taraması olmadan tünelleyebilir. Geri <c>Discrete</c>'e
    /// düşürmeyi <c>Fruit.FixedUpdate</c> zaten kendisi yapıyor — burada yeni state tutulmuyor.
    /// </summary>
    void ArmFruitsForShaking()
    {
        if (_pool == null) return;

        var fruits = _pool.Active;

        for (int i = 0; i < fruits.Count; i++)
        {
            Fruit f = fruits[i];

            if (f == null || !f.IsDropped || f.Body == null) continue;

            f.Body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
    }

    // -------------------------------------------------------------------- döngü

    void Update()
    {
        // Boost boştayken tek bir enum karşılaştırmasıyla çık — oyunun %99'unda burası.
        if (_state == State.Idle) return;

        GameManager gm = GameManager.Instance;

        // Pause: DONDUR. timeScale 0 olduğu için dt zaten 0 ve aşağıdaki hiçbir şey
        // ilerlemez, ama açıkça çıkmak niyeti okunur kılıyor — ayrıca SetRumble'ı
        // yazmayı bırakmamız kamerayı dinlenme konumuna oturtuyor (CameraShaker kendi
        // kendini toparlıyor), yani pause paneli eğik bir kadrajın üstünde açılmıyor.
        if (gm != null && gm.State == GameState.Paused) return;

        if (gm == null || !gm.IsPlaying)
        {
            Abort();
            return;
        }

        float dt = Time.deltaTime;

        _stateTime += dt;

        float env = Envelope();

        if (CameraShaker.Instance != null) CameraShaker.Instance.SetRumble(env);

        // Duyulan şiddet görülen şiddetle birebir aynı olsun diye gürültü de aynı zarftan.
        if (AudioService.Instance != null) AudioService.Instance.SetQuakeRumbleLevel(env);

        // Hissedilen şiddet de aynı zarftan: kamera, gürültü ve titreşim birlikte iniyor.
        if (HapticService.Instance != null) HapticService.Instance.SetQuakeLevel(env);

        EmitDust(env, dt);
        EmitRubble(env, dt);

        AdvancePhase();
    }

    void AdvancePhase()
    {
        if (_state == State.Shake)
        {
            if (_stateTime < _config.quakeShakeDuration) return;

            _state     = State.Settle;
            _stateTime = 0f;
            return;
        }

        if (_state == State.Settle && _stateTime >= _config.quakeSettleDuration) Stop();
    }

    /// <summary>
    /// Sarsıntının 0-1 şiddeti. Kamera genliği, itme gücü, toz/moloz debisi ve gürültünün sesi
    /// <b>hepsi aynı</b> zarftan besleniyor — "kamera sallanıyor ama meyveler durdu" ya da
    /// tersi hiç olmuyor.
    ///
    /// <c>quakeAttackTime</c> içinde 0'dan 1'e çıkar, sonuna <c>quakeReleaseTime</c> kalınca
    /// 0'a iner — ani duruş "oyun dondu" gibi görünüyor.
    /// </summary>
    float Envelope()
    {
        if (_state != State.Shake) return 0f;

        float attack = Mathf.Max(0.0001f, _config.quakeAttackTime);
        float rise   = Mathf.Clamp01(_stateTime / attack);

        float release   = Mathf.Max(0.0001f, _config.quakeReleaseTime);
        float remaining = _config.quakeShakeDuration - _stateTime;

        return rise * Mathf.Clamp01(remaining / release);
    }

    // -------------------------------------------------------------------- fizik

    /// <summary>
    /// İtmeler. <see cref="Update"/> yerine burada çünkü <c>Rigidbody2D</c> yazması fizik
    /// adımına ait — fizik 50 Hz (timestep 0.02).
    /// </summary>
    void FixedUpdate()
    {
        if (_state != State.Shake) return;

        _kickTimer -= Time.fixedDeltaTime;

        if (_kickTimer > 0f) return;

        float jitter = _config.quakeKickIntervalJitter;

        _kickTimer = Mathf.Max(Time.fixedDeltaTime,
                               _config.quakeKickInterval + Random.Range(-jitter, jitter));

        // Zarf Update'te hesaplanıyor, burada bir kare eskimiş olabilir — zarf sert bir sınır
        // değil bir rampa, o yüzden fark görünmez.
        ApplyKicks(Envelope());
    }

    void ApplyKicks(float env)
    {
        if (env <= 0f || _pool == null) return;

        var fruits = _pool.Active;

        if (fruits.Count == 0) return;

        float strength = _config.quakeKickStrength * env;
        float jitter   = strength * Mathf.Clamp01(_config.quakeKickJitterRatio);

        float maxSpeed    = Mathf.Max(0.01f, _config.quakeMaxSpeed);
        float maxSpeedSqr = maxSpeed * maxSpeed;

        // Hareket halindeki meyveyi itmiyoruz — itmelerin birikmesini engelleyen tek şey bu.
        float restSqr = _config.quakeKickRestSpeed * _config.quakeKickRestSpeed;

        float maxRise = _config.quakeMaxRiseSpeed;
        float vScale  = _config.quakeKickVerticalScale;

        // Sarsıntı kaçıncı yön diliminde. Her meyve her dilimde kendine özgü YENİ bir
        // rastgele yön alıyor; dilim boyunca o yöne itiliyor.
        int slots = Mathf.Max(1, _config.quakeKickDirectionSlots);

        int slot = Mathf.Clamp(
            (int)(_stateTime / (_config.quakeShakeDuration / slots)), 0, slots - 1) + _slotSalt;

        int maxTier = _database != null ? Mathf.Max(1, _database.MaxTier) : DefaultMaxTier;

        for (int i = 0; i < fruits.Count; i++)
        {
            Fruit f = fruits[i];

            if (f == null) continue;

            // Daldaki bekleyen meyve sarsılmaz (fizik simülasyonu bile kapalı), birleşen ya da
            // başka bir boost tarafından tutulan meyveye de dokunulmaz.
            if (!f.IsDropped || f.IsMerging) continue;

            Rigidbody2D body = f.Body;

            if (body == null) continue;

            Vector2 v = body.linearVelocity;

            // ⭐ Hâlâ hareket ediyorsa İTMİYORUZ (itmelerin birikmesini bu engelliyor) —
            // AMA yukarı hızını yine de kırpıyoruz. Bu ikinci kısım kritik: sıkışık yığın bir
            // yay gibi davranıp meyveyi fırlatabiliyor, ve eskiden bu kapı hızlı meyveyi
            // tamamen atladığı için onu frenleyecek hiçbir şey kalmıyordu — meyveler böyle
            // duvarın üstünden kaçıyordu.
            if (v.sqrMagnitude > restSqr)
            {
                if (v.y > maxRise)
                {
                    v.y = maxRise;
                    body.linearVelocity = v;
                }

                continue;
            }

            float tierT = f.Definition != null
                ? Mathf.Clamp01(f.Definition.tier / (float)maxTier)
                : 0f;

            float scale = Mathf.Lerp(_config.quakeKickScaleSmall, _config.quakeKickScaleBig, tierT);

            // Yerleşmiş yığın yarım saniyede uyuyor (m_TimeToSleep = 0.5) ve uyuyan bir
            // Rigidbody2D'ye yazılan hız güvenilir şekilde uygulanmıyor — önce uyandır.
            body.WakeUp();

            // Meyveye özgü, ömrü boyunca sabit tohum. DropTime kullanılıyor çünkü zaten
            // önbelleklenmiş bir float — GetInstanceID() native bir çağrı olurdu ve bu döngü
            // saniyede ~16 kez, meyve başına dönüyor.
            float seed = Mathf.Repeat(f.DropTime * 7.13f, 1f);

            // (meyve, dilim) çiftine özgü, deterministik 0-1. Dilim boyunca AYNI kalıyor —
            // yani meyve o dilim boyunca aynı yöne itiliyor (mesafe buradan geliyor), dilim
            // değişince yeni bir yön alıyor. Tohum meyveye özgü olduğu için her meyve
            // BAĞIMSIZ: yığın blok halinde kaymıyor.
            float h = Hash01(seed, slot);

            // ⭐ Açı SADECE alt yarım daireden seçiliyor: 0 = sağ, -90° = aşağı, -180° = sol.
            // Yani yön asla yukarı bakmıyor — sağa/sola/aşağı öteleme.
            float angle = -h * Mathf.PI;

            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * vScale);

            if (dir.sqrMagnitude > 0.0001f) dir /= dir.magnitude;
            else                            dir  = Vector2.right;

            v += (dir * strength + Random.insideUnitCircle * jitter) * scale;

            // Rastgele sapma yukarı bakabiliyor — yükselmeyi burada da kırp
            if (v.y > maxRise) v.y = maxRise;

            if (v.sqrMagnitude > maxSpeedSqr) v = v.normalized * maxSpeed;

            body.linearVelocity = v;

            // Açısal hıza DOKUNULMUYOR: döndürme istenmedi. Meyvenin kendi
            // Fruit.FixedUpdate'i yavaşken dönüşü zaten söndürüyor.

        }
    }

    /// <summary>
    /// (tohum, dilim) çiftinden 0-1 arası deterministik bir sayı. Klasik
    /// <c>frac(sin(x) * büyük sayı)</c> karma yöntemi: tablo yok, allocation yok, dağılımı iyi.
    ///
    /// Deterministik olması ŞART — aynı meyve aynı dilimde her çağrıda aynı yönü almalı,
    /// yoksa yön kare başına zıplar ve hareket yerinde titremeye döner.
    /// </summary>
    static float Hash01(float seed, int slot) =>
        Mathf.Repeat(Mathf.Sin(seed * 127.1f + slot * 311.7f) * 43758.5453f, 1f);

    // -------------------------------------------------------------------- efekt

    /// <summary>
    /// Toz: zeminden VE iki duvarın iç yüzünden. Sadece zeminden çıkınca "yerden toz kalkıyor"
    /// oluyor; deprem hissi için tozun her yerden gelmesi gerekiyor.
    /// </summary>
    void EmitDust(float env, float dt)
    {
        if (env <= 0f || EffectDirector.Instance == null) return;

        _dustAccum += _config.quakeDustRate * env * dt;

        int count = (int)_dustAccum;

        if (count <= 0) return;

        _dustAccum -= count;

        float alpha = _config.quakeDustAlpha * env;

        // Payı duvarlara ve zemine böl
        int wall  = Mathf.RoundToInt(count * Mathf.Clamp01(_config.quakeDustWallShare));
        int floor = count - wall;

        int leftWall  = wall / 2;
        int rightWall = wall - leftWall;

        // Zemin: yatay ince şerit
        if (floor > 0)
            EffectDirector.Instance.EmitQuakeDust(
                new Vector2(0f, _floorY + _config.quakeDustSpawnLift),
                new Vector2(_config.wallInnerX, 0.025f),
                _config.quakeDustColor, floor, alpha,
                _config.quakeDustSize, _config.quakeDustLifetime);

        // Duvarlar: dikey ince şeritler
        float wx = Mathf.Max(0.1f, _config.wallInnerX - _config.quakeDustWallInset);
        float wy = _floorY + _config.quakeDustWallHeight * 0.5f;
        var wallExtents = new Vector2(0.025f, _config.quakeDustWallHeight * 0.5f);

        if (leftWall > 0)
            EffectDirector.Instance.EmitQuakeDust(new Vector2(-wx, wy), wallExtents,
                _config.quakeDustColor, leftWall, alpha,
                _config.quakeDustSize, _config.quakeDustLifetime);

        if (rightWall > 0)
            EffectDirector.Instance.EmitQuakeDust(new Vector2(wx, wy), wallExtents,
                _config.quakeDustColor, rightWall, alpha,
                _config.quakeDustSize, _config.quakeDustLifetime);
    }

    /// <summary>
    /// Ekranın SAĞ ve SOL kenarından düşen küçük toprak renginde molozlar. Depremin "oluyor"
    /// hissinin büyük kısmı bundan geliyor.
    ///
    /// Meyvelerin ARKASINDAN düşüyorlar (sıralama katmanı negatif) — önden geçseler yığından
    /// dikkat çalarlardı.
    /// </summary>
    void EmitRubble(float env, float dt)
    {
        if (env <= 0f || EffectDirector.Instance == null) return;

        _rubbleAccum += _config.quakeRubbleRate * env * dt;

        int count = (int)_rubbleAccum;

        if (count <= 0) return;

        _rubbleAccum -= count;

        float x = Mathf.Max(0.1f, _config.wallInnerX - _config.quakeRubbleEdgeInset);
        float y = _config.dropY + _config.quakeRubbleSpawnYOffset;

        int left  = count / 2;
        int right = count - left;

        // Tek sayı kalırsa hangi tarafa gideceği yazı tura — hep sağa gitmesin
        if (Random.value < 0.5f) { int t = left; left = right; right = t; }

        if (left > 0)
            EffectDirector.Instance.EmitQuakeRubble(new Vector2(-x, y),
                _config.quakeRubbleSpawnSpread, _config.quakeRubbleColor, left,
                _config.quakeRubbleSize, _config.quakeRubbleLifetime);

        if (right > 0)
            EffectDirector.Instance.EmitQuakeRubble(new Vector2(x, y),
                _config.quakeRubbleSpawnSpread, _config.quakeRubbleColor, right,
                _config.quakeRubbleSize, _config.quakeRubbleLifetime);
    }

    // -------------------------------------------------------------------- bitiş

    /// <summary>
    /// Normal bitiş ve <see cref="Abort"/> aynı temizliği yapıyor: deprem hiçbir kalıcı
    /// değişiklik bırakmadığı için "yarısında kesilmiş" ile "tamamlanmış" arasında fark yok.
    /// Toz/moloz temizlenmiyor — havadaki parçacıklar kendi ömürlerini tamamlasın; ayrıca
    /// <c>ClearAll</c> aynı anda uçan merge damlalarını da öldürürdü.
    /// </summary>
    void Stop()
    {
        _state       = State.Idle;
        _stateTime   = 0f;
        _kickTimer   = 0f;
        _dustAccum   = 0f;
        _rubbleAccum = 0f;

        if (CameraShaker.Instance != null) CameraShaker.Instance.StopImmediate();

        if (AudioService.Instance != null) AudioService.Instance.StopQuakeRumble();

        if (HapticService.Instance != null) HapticService.Instance.StopQuake();

        if (FaceDirector.Instance != null) FaceDirector.Instance.SetQuakeMood(false);

        GameEvents.RaiseBoostStateChanged(BoostId.Quake, false, _charges);
    }
}
