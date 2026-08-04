using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "Tatlı kurtçuklar" boost'u — baştan sona.
///
/// Akış:
///  1. <b>Armed</b>   — HER meyvenin üstünde saat yönünde dönen bir nişangâh belirir.
///  2. <b>Approach</b> — oyuncu bir meyve seçer; o meyvede pulse halkaları sırayla oynarken
///                       kurtlar ekranın iki yanından sürünerek gelir.
///  3. <b>Eat</b>      — meyve renginde sis bulutu meyveyi kaplar, merge'ün meyve suyu
///                       parçacıklarıyla kırıntılar dökülür. Sisin en yoğun anında
///                       (<see cref="GameConfig.wormFruitVanishAt"/>) meyve yok edilir ve
///                       yığın boşluğa çöker.
///  4. <b>Leave</b>    — sis dağılırken kurtlar GELDİKLERİ YÖNDE devam edip ekrandan çıkar.
///
/// Mimari:
///  - Tek <see cref="Update"/> (kural 7). Kurtlar ve nişangâhlar <c>Tick</c>'leniyor,
///    kendi <c>Update</c>'leri yok.
///  - Coroutine yok (kural 8) — bütün zamanlama float sayaç.
///  - Kurtlar ve nişangâhlar açılışta bir kez yaratılıp saklanıyor (kural 13); boost
///    çalışırken hiçbir şey Instantiate edilmiyor.
///  - Sis <see cref="EffectDirector"/>'ün paylaşımlı parçacık sistemine Emit ediliyor,
///    kırıntılar zaten var olan <see cref="EffectDirector.PlayJuice"/>'tan geliyor.
/// </summary>
[DefaultExecutionOrder(-30)]
public class WormBoostDirector : MonoBehaviour, IBoostDirector
{
    public static WormBoostDirector Instance { get; private set; }

    public BoostId Id => BoostId.Worms;

    enum State { Idle, Armed, Approach, Eat, Leave }

    [Header("Referanslar")]
    [SerializeField] FruitPool  _pool;
    [SerializeField] GameConfig _config;
    [SerializeField] Camera     _camera;

    [Header("Hedefleme görselleri")]
    [Tooltip("silahlıyken her meyvenin üstünde dönen nişangâh")]
    [SerializeField] Sprite _crosshair;

    [Tooltip("seçilen meyvede sırayla oynayan halkalar — target_crosshair_pulse_01..04")]
    [SerializeField] Sprite[] _pulseFrames;

    [Header("Kurt görselleri")]
    [SerializeField] Sprite _wormHead;
    [SerializeField] Sprite _wormHeadOpen;
    [SerializeField] Sprite _wormHeadFull;
    [SerializeField] Sprite _wormBody;
    [SerializeField] Sprite _wormBodyFat;
    [SerializeField] Sprite _wormTail;

    [Header("Ön ısıtma")]
    [Tooltip("kaç nişangâh önceden yaratılsın. Tahtada bundan çok meyve olursa liste büyür")]
    [SerializeField] int _crosshairPrewarm = 44;

    [Header("Renk")]
    [Tooltip("nişangâh art'ı neredeyse beyaz üretilmiş — asıl rengi buradan geliyor. " +
             "Beyaz 11 meyvenin 9'unda okunuyor; ananas/hindistan cevizi gibi açık " +
             "meyvelerde koyu bir tona çevirmek isteyebilirsin")]
    [SerializeField] Color _cursorTint = Color.white;

    [SerializeField] Color _pulseTint = Color.white;

    /// <summary>En büyük meyve (karpuz) 6 kurt çağırıyor — havuzun üst sınırı bu.</summary>
    const int MaxWorms = 6;

    /// <summary>Kurt sayısı = tier/2 + 1 → kiraz 1, karpuz 6.</summary>
    public static int WormCountForTier(int tier) => Mathf.Clamp(tier / 2 + 1, 1, MaxWorms);

    public bool IsBusy => _state != State.Idle;

    public bool IsArmed => _state == State.Armed;

    public int Charges => _charges;

    public bool CanArm => _charges != 0
                          && GameManager.Instance != null
                          && GameManager.Instance.IsPlaying
                          && !BoostGate.IsAnyBusy;   // başka bir boost oynarken silahlanma

    State _state;
    float _stateTime;
    int   _charges;

    /// <summary>
    /// Silahlanmayı SAĞLAYAN dokunuş, hedef seçimi olarak da okunmasın.
    ///
    /// HUD butonu <c>onClick</c>'i parmak KALKARKEN tetikliyor; aynı karede
    /// <see cref="TickArmed"/> o bırakmayı görüp "boşluğa dokunuldu" diyerek
    /// <see cref="Cancel"/> çağırıyordu. Boost bir kare içinde silahlanıp iptal
    /// oluyordu — ekranda nişangâh hiç görünmüyor, sonra da kilitsiz kalan dokunuş
    /// <see cref="DropController"/>'a düşüp meyve bırakıyordu.
    ///
    /// <see cref="Toggle"/> kilidi koyuyor, sadece YENİ bir basış açıyor.
    /// </summary>
    bool _gestureBlocked;

    // ---- hedefleme -------------------------------------------------------
    readonly List<SpriteRenderer> _cursors = new List<SpriteRenderer>(48);
    Transform      _cursorParent;
    SpriteRenderer _pulse;
    float _cursorAngle;
    float _cursorAlpha;

    /// <summary>Son turda kaç nişangâh kullanıldı — sönme döngüsü sadece bu kadarını gezer.</summary>
    int _cursorsUsed;
    int   _pulseFrame = -1;
    float _pulseTimer;

    /// <summary>
    /// Pulse dizisinin ÖLÇEK REFERANSI — ilk karenin dünya genişliği. Dört kare
    /// giderek büyüyor (3.79 → 4.35 birim); her kareyi kendi genişliğine göre
    /// normalize etseydik o büyüme sıfırlanır, halka nefes almak yerine dururdu.
    /// </summary>
    float _pulseRefUnit = 1f;

    /// <summary>
    /// Nişangâh sprite'ının dünya genişliği. Bütün nişangâhlar AYNI sprite'ı kullanıyor,
    /// yani bu SABİT — eskiden <see cref="PlaceCursors"/> içinde meyve başına her karede
    /// <c>sprite.rect</c> + <c>sprite.pixelsPerUnit</c> (iki native property) okunuyordu.
    /// 44 meyveli tahtada kare başına 88 gereksiz erişim. <see cref="_pulseRefUnit"/> ile
    /// aynı desen: bir kez, kurulumda.
    /// </summary>
    float _cursorRefUnit = 1f;

    // ---- kurtlar ---------------------------------------------------------
    Worm[] _worms;
    int    _wormsActive;

    // ---- hedef -----------------------------------------------------------
    Fruit            _target;
    FruitDefinition  _targetDef;
    Vector2          _targetPos;
    float            _targetRadius;
    float            _targetBaseScale;
    bool             _fruitVanished;

    // ---- efekt -----------------------------------------------------------
    float _smokeAccum;
    int   _crumbBursts;
    int   _crumbWorm;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }

        Instance = this;

        if (_camera == null) _camera = Camera.main;

        // Kurt halkaları localPosition ile yerleştiriliyor (dünya koordinatı olarak).
        // Kök başka bir yerdeyse tüm zincir kayar — burada sabitliyoruz.
        transform.position   = Vector3.zero;
        transform.rotation   = Quaternion.identity;
        transform.localScale = Vector3.one;
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
        if (_config == null)
        {
            Debug.LogError("WormBoostDirector: GameConfig bağlı değil, bileşen kapatılıyor.", this);

            enabled = false;

            return;
        }

        BuildCursors();
        BuildWorms();

        _charges = _config.wormsChargesPerRun;

        GameEvents.RaiseBoostStateChanged(BoostId.Worms, false, _charges);
    }

    // ----------------------------------------------------------------- kurulum

    // NOT — kurulum bilinçli olarak Start'ta, PrewarmQueue'da DEĞİL.
    //
    // Bir denemede nişangâhlar ve kurtlar PrewarmQueue'ya taşınmıştı (açılış ekranı boyunca
    // karelere yayılsınlar diye). Geri alındı: proje Play Mode'da "Reload Domain" VE
    // "Reload Scene" kapalı çalışıyor, yani serialize EDİLMEYEN instance alanları (ısıtma
    // sayacı, kurt dizisi) oturumlar arasında yaşıyor ve ikinci Play'de tutarsız hale
    // geliyordu. Sonuç: PrewarmStep her karede patlıyor, PrewarmQueue.Done hiç Total'a
    // ulaşmıyor ve SplashPanel'in çubuğu dolmadığı için oyun açılış ekranında kilitleniyordu.
    //
    // Buradaki tek karelik maliyet (44 nişangâh + 6 kurt) o riske değmiyor. FruitPool ve
    // ComboPopupDirector'ün PrewarmQueue kullanması SORUN DEĞİL: onların ısıtması
    // ObjectPool<T> üzerinden gidiyor ve durumları bu kadar kırılgan değil.

    void BuildCursors()
    {
        var parent = new GameObject("Cursors");

        parent.transform.SetParent(transform, false);

        _cursorParent = parent.transform;

        // Bütün nişangâhlar AYNI sprite'ı kullanıyor, yani dünya birimi sabit — bir kez
        // burada hesaplanıyor (bkz. _cursorRefUnit). Eskiden PlaceCursors meyve başına
        // her karede sprite'tan okuyordu.
        if (_crosshair != null)
            _cursorRefUnit = _crosshair.rect.width / _crosshair.pixelsPerUnit;

        for (int i = 0; i < _crosshairPrewarm; i++) CreateCursor();

        var pulseGo = new GameObject("Pulse");

        pulseGo.transform.SetParent(transform, false);

        _pulse = pulseGo.AddComponent<SpriteRenderer>();
        _pulse.sortingOrder = _config.boostCursorSortingOrder + 1;
        _pulse.color        = _pulseTint;

        if (_pulseFrames != null && _pulseFrames.Length > 0 && _pulseFrames[0] != null)
            _pulseRefUnit = _pulseFrames[0].rect.width / _pulseFrames[0].pixelsPerUnit;

        pulseGo.SetActive(false);
    }

    void BuildWorms()
    {
        _worms = new Worm[MaxWorms];

        int n = Mathf.Max(2, _config.wormSegmentCount);

        for (int i = 0; i < MaxWorms; i++)
        {
            var go = new GameObject("Worm" + i);

            go.transform.SetParent(transform, false);

            var w = go.AddComponent<Worm>();

            // her kurdun kendi sıralama aralığı — halkalar başka bir kurdunkiyle karışmasın
            w.Build(_config, n, _config.wormSortingOrder + i * (n + 1),
                    _wormHead, _wormHeadOpen, _wormHeadFull, _wormBody, _wormBodyFat, _wormTail);

            _worms[i] = w;
        }
    }

    SpriteRenderer CreateCursor()
    {
        var go = new GameObject("Cursor" + _cursors.Count);

        go.transform.SetParent(_cursorParent, false);

        var sr = go.AddComponent<SpriteRenderer>();

        sr.sprite       = _crosshair;
        sr.sortingOrder = _config.boostCursorSortingOrder;
        sr.color        = new Color(_cursorTint.r, _cursorTint.g, _cursorTint.b, 0f);

        go.SetActive(false);

        _cursors.Add(sr);

        return sr;
    }

    // ----------------------------------------------------------------- olaylar

    void HandleRunStarted()
    {
        Abort();

        _charges = _config != null ? _config.wormsChargesPerRun : 0;

        GameEvents.RaiseBoostStateChanged(BoostId.Worms, false, _charges);
    }

    void HandleStateChanged(GameState s)
    {
        // PAUSE = DONDUR, iptal etme. Eskiden burada Abort() çağrılıyordu: oyuncu kurtlar
        // meyveyi yerken pause'a bastığı an kurtlar gidiyor, meyve kurtuluyor ve kullanım
        // boşa gidiyordu (kullanım BeginBoost'ta zaten harcanmış oluyor). Artık sahne
        // olduğu gibi donuyor ve Continue kaldığı yerden devam ediyor.
        //
        // Dondurmayı timeScale = 0 hallediyor: bütün faz sayaçları ve TickWorms
        // Time.deltaTime ile ilerliyor, sis parçacıkları da donuyor. Update aşağıda
        // pause'da erken çıkıyor — asıl sebebi zaman değil GİRDİ: TryReadTap pause
        // panelindeki dokunuşu hedef seçimi sanardı.
        if (s == GameState.Paused) return;

        if (s == GameState.Playing)
        {
            ResumeFromPause();

            return;
        }

        // Menü / oyun sonu: burada gerçekten iptal — yarım kalmış bir boost ekranda
        // asılı kalmasın.
        Abort();
    }

    /// <summary>
    /// Pause'dan dönüş. Yeni oyunda da çağrılıyor ama <c>_state == Idle</c> olduğu için
    /// hiçbir şey yapmıyor (yeni oyunun sıfırlaması <see cref="HandleRunStarted"/>'da).
    /// </summary>
    void ResumeFromPause()
    {
        if (_state == State.Idle) return;

        // Continue'ye basan dokunuşun bırakılması hedef seçimi sayılmasın — Toggle'daki
        // kilidin aynısı. Update pause'da erken çıktığı için o bırakma zaten görülmüyor,
        // bu ikinci kalkan.
        if (_state == State.Armed) _gestureBlocked = true;

        // FaceDirector OnStateChanged(Playing)'de her şeyi sıfırlıyor (yeni oyun için
        // doğru) — boostun yüz durumunu geri yazmak bize kalıyor.
        if (FaceDirector.Instance != null)
        {
            FaceDirector.Instance.SetBoostFocus(
                _target != null && _target.gameObject.activeSelf ? _target.transform : null);

            // Kalan süreyi hesaplamak yerine tüm sekansı yeniden peşin ödüyoruz: fazla
            // bastırmak zararsız (yalnızca uyuklayan yüzleri geciktiriyor).
            FaceDirector.Instance.SuppressSleepFor(_config.wormApproachDuration +
                                                   _config.wormEatDuration +
                                                   _config.wormLeaveDuration);
        }

        // Kemirme titreşimini HapticService pause'da susturdu (o servis unscaledDeltaTime
        // ile dönüyor); yeme devam ediyorsa treni yeniden başlat.
        if (_state == State.Eat && !_fruitVanished) GameEvents.RaiseWormsChewingChanged(true);
    }

    // ----------------------------------------------------------------- genel API

    /// <summary>HUD butonu. Silahlıyken tekrar basmak iptal eder.</summary>
    public void Toggle()
    {
        if (_state == State.Armed) { Cancel(); return; }

        if (_state != State.Idle) return;

        if (!CanArm) return;

        _state          = State.Armed;
        _stateTime      = 0f;
        _cursorAngle    = 0f;
        _gestureBlocked = true;   // butona basan dokunuşun bırakılması seçim sayılmasın

        GameEvents.RaiseBoostStateChanged(BoostId.Worms, true, _charges);
    }

    public void Cancel()
    {
        if (_state != State.Armed) return;

        _state = State.Idle;

        GameEvents.RaiseBoostStateChanged(BoostId.Worms, false, _charges);
    }

    /// <summary>Mağazadan satın alma. Sınırsız moddaysa (-1) dokunma.</summary>
    public void AddCharge(int amount)
    {
        if (amount <= 0 || _charges < 0) return;

        _charges += amount;

        GameEvents.RaiseBoostStateChanged(BoostId.Worms, _state == State.Armed, _charges);
    }

    // ----------------------------------------------------------------- döngü

    void Update()
    {
        // Boost tamamen boştayken TEK BİR karşılaştırma yapıp çık. Aşağıdaki
        // TickCursorFade 44 nişangâhı geziyor; oyunun %99'unda boost çalışmıyor ve
        // o listeyi her karede gezmenin hiçbir karşılığı yok.
        if (_state == State.Idle && _cursorAlpha <= 0f) return;

        GameManager gm = GameManager.Instance;

        // Pause: DONDUR. timeScale 0 olduğu için dt zaten 0, ama GİRDİ timeScale'e bağlı
        // değil — TryReadTap çalışmaya devam etseydi pause panelindeki bir dokunuş hedef
        // seçimi sayılırdı.
        if (gm != null && gm.State == GameState.Paused) return;

        float dt = Time.deltaTime;

        if (_state != State.Idle && (gm == null || !gm.IsPlaying))
        {
            Abort();
            return;
        }

        // Silahlıyken (oyuncu hedefe karar veriyor) meyveler uyuklamasın. Süresi belli
        // olmayan tek faz bu, o yüzden kare başına tazeleniyor — tek float ataması.
        // Approach/Eat/Leave için gerek yok: BeginBoost tüm süreyi peşin ödüyor.
        if (_state == State.Armed && FaceDirector.Instance != null)
            FaceDirector.Instance.NotifyActivity();

        switch (_state)
        {
            case State.Armed:    TickArmed(dt);    break;
            case State.Approach: TickApproach(dt); break;
            case State.Eat:      TickEat(dt);      break;
            case State.Leave:    TickLeave(dt);    break;
        }

        TickCursorFade(dt);
    }

    // ---------------------------------------------------------------- ARMED

    void TickArmed(float dt)
    {
        _cursorAngle += _config.boostCrosshairSpinSpeed * dt;

        PlaceCursors();

        if (TryReadTap(out Vector2 world))
        {
            Fruit hit = FindFruitAt(world);

            if (hit != null) BeginBoost(hit);
            else             Cancel();          // boşluğa dokunmak iptal eder
        }
    }

    /// <summary>Silahlıyken her meyvenin üstüne bir nişangâh koyar ve döndürür.</summary>
    void PlaceCursors()
    {
        // ArmFruitsForShaking / FindFruitAt aynı kontrolü yapıyor, burası atlamıştı.
        if (_pool == null) return;

        var fruits = _pool.Active;

        int used = 0;

        for (int i = 0; i < fruits.Count; i++)
        {
            Fruit f = fruits[i];

            if (f == null || !f.IsDropped || f.IsMerging) continue;

            SpriteRenderer sr = used < _cursors.Count ? _cursors[used] : CreateCursor();

            used++;

            if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);

            var t = sr.transform;

            t.position      = f.transform.position;
            t.localRotation = Quaternion.Euler(0f, 0f, _cursorAngle);

            // nişangâhın sprite'ı 1 dünya biriminden farklı — meyveye göre ölçekle.
            // Sprite'ın dünya birimi SABİT olduğu için _cursorRefUnit'ten okunuyor
            // (eskiden meyve başına her karede sprite'tan yeniden hesaplanıyordu).
            float world = f.Radius * 2f * _config.boostCrosshairScale;

            float k = world / Mathf.Max(0.0001f, _cursorRefUnit);

            t.localScale = new Vector3(k, k, 1f);
        }

        for (int i = used; i < _cursorsUsed; i++)
            if (_cursors[i].gameObject.activeSelf) _cursors[i].gameObject.SetActive(false);

        _cursorsUsed = used;
    }

    void TickCursorFade(float dt)
    {
        float target = _state == State.Armed ? 1f : 0f;

        float speed = 1f / Mathf.Max(0.01f, _config.boostCrosshairFade);

        _cursorAlpha = Mathf.MoveTowards(_cursorAlpha, target, speed * dt);

        // sadece o an kullanımda olanları gez — havuzun tamamını (44) değil
        for (int i = 0; i < _cursorsUsed; i++)
        {
            SpriteRenderer sr = _cursors[i];

            if (!sr.gameObject.activeSelf) continue;

            Color c = sr.color;

            if (!Mathf.Approximately(c.a, _cursorAlpha))
            {
                c.a = _cursorAlpha;
                sr.color = c;
            }

            if (_cursorAlpha <= 0f) sr.gameObject.SetActive(false);
        }
    }

    // ---------------------------------------------------------------- seçim

    bool TryReadTap(out Vector2 world)
    {
        world = default;

        // Silahlandıktan SONRA başlayan ilk basış kilidi açıyor.
        if (PointerInput.Began) _gestureBlocked = false;

        if (!PointerInput.Released) return false;

        if (_gestureBlocked)
        {
            _gestureBlocked = false;

            return false;
        }

        // HUD butonuna basılan dokunuş hedef seçimi sayılmasın
        if (PointerInput.IsOverUI()) return false;

        world = _camera.ScreenToWorldPoint(PointerInput.Position);

        return true;
    }

    /// <summary>Dokunulan noktadaki meyve. Birden fazlaysa merkezi en yakın olan.</summary>
    Fruit FindFruitAt(Vector2 world)
    {
        var fruits = _pool.Active;

        Fruit best = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < fruits.Count; i++)
        {
            Fruit f = fruits[i];

            if (f == null || !f.IsDropped || f.IsMerging) continue;

            float r = f.Radius;

            float sqr = ((Vector2)f.transform.position - world).sqrMagnitude;

            if (sqr > r * r) continue;

            if (sqr < bestSqr) { bestSqr = sqr; best = f; }
        }

        return best;
    }

    // ---------------------------------------------------------------- başlat

    void BeginBoost(Fruit fruit)
    {
        _target          = fruit;
        _targetDef       = fruit.Definition;
        _targetPos       = fruit.transform.position;
        _targetRadius    = fruit.Radius;
        _targetBaseScale = fruit.Definition.scale;
        _fruitVanished   = false;

        // Kilit: 4 saniye boyunca bu meyve başka bir meyveyle BİRLEŞMESİN, yoksa
        // referansımız altımızdan havuza döner. MergeHandler.LateUpdate bunu
        // çalıştırmadan önce yeniden kontrol ediyor, yani kilit güvenli.
        fruit.IsMerging = true;

        if (FaceDirector.Instance != null)
        {
            // Yüzleri FaceDirector yönetiyor: hedef korkar, DİĞER meyveler surprised
            // olup onu seyreder. Buradan Express ile zorlasaydık danger/kutlama
            // mantığıyla aynı yüz üzerinde çekişirdik.
            FaceDirector.Instance.SetBoostFocus(fruit.transform);

            // Uyuklama sayacını tüm sekans boyunca peşin öde — TEK çağrı. Boost
            // 5.5 sn sürüyor, uyuklama eşiği 5 sn; sayaç sıfırlanmasa meyveler
            // kurtlar hâlâ ekrandayken uyumaya başlıyordu.
            FaceDirector.Instance.SuppressSleepFor(_config.wormApproachDuration +
                                                   _config.wormEatDuration +
                                                   _config.wormLeaveDuration);
        }

        if (_charges > 0) _charges--;

        SpawnWorms();

        ShowPulse();

        _state       = State.Approach;
        _stateTime   = 0f;
        _smokeAccum  = 0f;
        _crumbBursts = 0;
        _crumbWorm   = 0;

        GameEvents.RaiseBoostStateChanged(BoostId.Worms, false, _charges);
    }

    void SpawnWorms()
    {
        int count = WormCountForTier(_targetDef.tier);

        // yarısı soldan yarısı sağdan; tek sayıda fazlalık rastgele bir tarafa
        int left  = count / 2;
        int right = count / 2;

        if ((count & 1) == 1)
        {
            if (Random.value < 0.5f) left++;
            else                     right++;
        }

        float edgeX = _camera.orthographicSize * _camera.aspect;

        _wormsActive = count;

        int index = 0;

        index = SpawnSide(left,  true,  180f, edgeX, index, count);
        index = SpawnSide(right, false,   0f, edgeX, index, count);
    }

    /// <summary>Bir taraftan gelen kurtları meyvenin çevresindeki yaya dağıtır.</summary>
    int SpawnSide(int count, bool fromLeft, float baseAngle, float edgeX, int index, int total)
    {
        for (int i = 0; i < count; i++)
        {
            // tek kurt tam ortaya, birden fazlası yaya eşit dağılır
            float spread = count > 1
                ? (i / (float)(count - 1) - 0.5f) * 2f * _config.wormSlotArcHalfAngle
                : 0f;

            float angle = baseAngle + spread;

            // yukarıdaki yuvaya giden kurt yukarıdan yaklaşsın
            float lane = Mathf.Sin(angle * Mathf.Deg2Rad) * _config.wormLaneSpread;

            _worms[index].Configure(_target.transform, fromLeft, angle, _targetRadius,
                                    lane, edgeX, index / (float)Mathf.Max(1, total));

            index++;
        }

        return index;
    }

    // ---------------------------------------------------------------- APPROACH

    void TickApproach(float dt)
    {
        _stateTime += dt;

        TickWorms(dt);

        TickPulse(dt);

        if (_stateTime < _config.wormApproachDuration) return;

        _state     = State.Eat;
        _stateTime = 0f;

        HidePulse();

        // Kemirme başladı. Ses/titreşim director'e bağlanmasın diye olay yayınlıyoruz
        // (OnQuakeStarted ile aynı desen) — dinleyen şu an HapticService: yeme, ekranda
        // sisle ve kırıntıyla görülen bir SÜREÇ, parmağın da o süre boyunca kemirmeyi
        // hissetmesi gerekiyor.
        GameEvents.RaiseWormsChewingChanged(true);
    }

    /// <summary>
    /// Seçim onayı: dört pulse karesi <see cref="GameConfig.boostPulseDuration"/> içinde
    /// BİR KEZ oynar, büyüyerek söner ve biter. Kurtların gelişi boyunca sürmüyor —
    /// öyleyken 2 saniye ekranda kalıp ne olduğu anlaşılmayan bir halka oluyordu.
    /// </summary>
    void TickPulse(float dt)
    {
        if (_pulse == null || _pulseFrames == null || _pulseFrames.Length == 0) return;

        if (!_pulse.gameObject.activeSelf) return;

        _pulseTimer += dt;

        float dur = Mathf.Max(0.02f, _config.boostPulseDuration);

        float u = _pulseTimer / dur;

        if (u >= 1f) { HidePulse(); return; }

        int frame = Mathf.Clamp((int)(u * _pulseFrames.Length), 0, _pulseFrames.Length - 1);

        if (frame != _pulseFrame)                        // kural 9
        {
            _pulseFrame   = frame;
            _pulse.sprite = _pulseFrames[frame];
        }

        // Sönme sadece SON kısımda. Baştan itibaren sönerse (1 − u²) son iki kare
        // zaten yarı saydam gelir ve halkanın büyümesi görünmez olur.
        Color c = _pulseTint;
        c.a = _pulseTint.a * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, u)));
        _pulse.color = c;

        var t = _pulse.transform;

        t.position = CurrentTargetPosition();

        float world = _targetRadius * 2f * _config.boostPulseScale;

        // İLK karenin genişliğine göre ölçekleniyor, o anki karenin değil —
        // böylece dizinin kendi büyümesi ekranda korunuyor.
        float k = world / Mathf.Max(0.0001f, _pulseRefUnit);

        t.localScale = new Vector3(k, k, 1f);
    }

    void ShowPulse()
    {
        if (_pulse == null || _pulseFrames == null || _pulseFrames.Length == 0) return;

        _pulseTimer = 0f;
        _pulseFrame = 0;

        _pulse.sprite = _pulseFrames[0];
        _pulse.color  = _pulseTint;

        _pulse.gameObject.SetActive(true);
    }

    void HidePulse()
    {
        if (_pulse != null && _pulse.gameObject.activeSelf) _pulse.gameObject.SetActive(false);

        _pulseFrame = -1;
        _pulseTimer = 0f;
    }

    // ---------------------------------------------------------------- EAT

    void TickEat(float dt)
    {
        float prev = _stateTime;

        _stateTime += dt;

        TickWorms(dt);

        Vector2 centre = CurrentTargetPosition();

        EmitSmoke(dt, centre);

        EmitCrumbs(centre);

        ShrinkFruit();

        // Sisin en yoğun anında meyveyi yok et — göz geçişi görmez, yığın çöker.
        if (!_fruitVanished && prev < _config.wormFruitVanishAt
                            && _stateTime >= _config.wormFruitVanishAt)
        {
            VanishFruit();
        }

        if (_stateTime < _config.wormEatDuration) return;

        // Emniyet: wormFruitVanishAt yanlışlıkla wormEatDuration'a eşit/büyük ayarlanırsa
        // yukarıdaki eşik hiç geçilmez ve meyve tahtada kalırdı.
        if (!_fruitVanished) VanishFruit();

        _state     = State.Leave;
        _stateTime = 0f;
    }

    /// <summary>
    /// Sis rampası. Parçacık ömrü kadar ERKEN kesiliyor ki bulut, yeme süresi
    /// biterken tam olarak dağılmış olsun.
    /// </summary>
    void EmitSmoke(float dt, Vector2 centre)
    {
        if (EffectDirector.Instance == null) return;

        float window = Mathf.Max(0.05f, _config.wormEatDuration - _config.eatSmokeLifetime);

        float u = _stateTime / window;

        if (u >= 1f) return;

        float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.22f));
        float fall = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.72f) / 0.28f));

        float intensity = rise * fall;

        _smokeAccum += _config.eatSmokeRate * intensity * dt;

        int count = Mathf.FloorToInt(_smokeAccum);

        if (count <= 0) return;

        _smokeAccum -= count;

        EffectDirector.Instance.EmitEatSmoke(
            centre,
            _targetDef.displayColor,
            _targetRadius * _config.eatSmokeRadiusFactor,
            _targetRadius * _config.eatSmokeParticleSize,
            _config.eatSmokeMaxAlpha,
            count,
            _config.eatSmokeLifetime);
    }

    /// <summary>
    /// Kırıntılar: merge'ün meyve suyu parçacıklarının aynısı, kurtların ağzından.
    /// Tümü meyve yok olana KADARKİ pencereye sıkıştırılıyor — meyve gittikten sonra
    /// hâlâ kırıntı saçmak "ortada bir şey yok ama hâlâ yiyorlar" hissi veriyordu.
    /// </summary>
    void EmitCrumbs(Vector2 centre)
    {
        if (EffectDirector.Instance == null) return;

        if (_fruitVanished) return;

        int want = Mathf.FloorToInt(
            _stateTime / Mathf.Max(0.01f, _config.wormFruitVanishAt) * _config.eatCrumbBursts) + 1;

        if (want <= _crumbBursts) return;

        _crumbBursts = want;

        Vector2 at = centre;

        if (_wormsActive > 0)
        {
            _crumbWorm = (_crumbWorm + 1) % _wormsActive;

            Worm w = _worms[_crumbWorm];

            if (w != null && w.gameObject.activeSelf) at = w.HeadPosition;
        }

        EffectDirector.Instance.PlayJuice(at, _targetDef, _config.eatCrumbIntensity);
    }

    /// <summary>Meyve yenirken küçülür — sis yoğunlaşırken içeride eriyor.</summary>
    void ShrinkFruit()
    {
        if (_fruitVanished || _target == null || !_target.gameObject.activeSelf) return;

        float u = Mathf.Clamp01(_stateTime / Mathf.Max(0.01f, _config.wormFruitVanishAt));

        float k = Mathf.Lerp(1f, _config.eatFruitMinScale, u) * _targetBaseScale;

        _target.transform.localScale = new Vector3(k, k, 1f);
    }

    void VanishFruit()
    {
        _fruitVanished = true;

        // Kemirme bitti: kurtlar da tam bu anda FinishMeal ile çiğnemeyi bırakıyor.
        GameEvents.RaiseWormsChewingChanged(false);

        // seyredilecek meyve kalmadı — diğer yüzler normale dönsün
        if (FaceDirector.Instance != null) FaceDirector.Instance.SetBoostFocus(null);

        if (_target != null && _target.gameObject.activeSelf)
        {
            Vector2 at = _target.transform.position;

            // Despawn IsMerging'i sıfırlamıyor; havuza dönerken ResetState zaten yapıyor.
            _pool.Despawn(_target);

            // Puan verilecekse buradan: ScoreSystem'in dışarıdan puan ekleyen bir API'si
            // henüz yok, o yüzden şimdilik sadece olay yayınlanıyor (wormsScoreOnEat = 0).
            GameEvents.RaiseFruitEaten(_targetDef, at);
        }

        _target = null;

        // Yemek bitti: kurtlar çiğnemeyi bırakır, tok yüze geçer, gövdeleri şişer.
        // Sis dağılana kadar (yeme süresinin kalan yarısı) sadece kıpırdanırlar.
        for (int i = 0; i < _wormsActive; i++) _worms[i].FinishMeal();
    }

    // ---------------------------------------------------------------- LEAVE

    void TickLeave(float dt)
    {
        _stateTime += dt;

        TickWorms(dt);

        bool anyAlive = false;

        for (int i = 0; i < _wormsActive; i++)
            if (!_worms[i].IsDone) { anyAlive = true; break; }

        if (anyAlive && _stateTime < _config.wormLeaveDuration + 0.5f) return;

        Finish();
    }

    void Finish()
    {
        for (int i = 0; i < _wormsActive; i++) _worms[i].Deactivate();

        _wormsActive = 0;
        _state       = State.Idle;
        _target      = null;

        HidePulse();

        GameEvents.RaiseBoostStateChanged(BoostId.Worms, false, _charges);
    }

    // ---------------------------------------------------------------- yardımcı

    void TickWorms(float dt)
    {
        for (int i = 0; i < _wormsActive; i++) _worms[i].Tick(dt);
    }

    /// <summary>
    /// Meyve fizikte kayabiliyor; yok olduktan SONRA da sis ve kırıntılar son bilinen
    /// yerde devam etsin diye konum her karede tazeleniyor.
    /// </summary>
    Vector2 CurrentTargetPosition()
    {
        if (_target != null && _target.gameObject.activeSelf)
            _targetPos = _target.transform.position;

        return _targetPos;
    }

    /// <summary>Her şeyi anında toparla — pause, menü, oyun sonu, yeni oyun.</summary>
    void Abort()
    {
        if (_state == State.Idle && _wormsActive == 0) return;

        // Hedef hâlâ tahtadaysa merge kilidini VE ShrinkFruit'in küçülttüğü ölçeği geri al.
        //
        // Ölçek geri alınmadığı için yemenin ortasında pause'a basmak meyveyi kalıcı
        // küçük bırakıyordu: CircleCollider2D transform ölçeğiyle birlikte küçüldüğü için
        // meyve FİZİKSEL olarak da küçülüyor, yığın onun etrafında çöküyor ve Radius/TopY
        // hâlâ _targetScale'i kullandığı için doluluk/sınır hesapları yanlış oluyordu.
        if (_target != null && _target.gameObject.activeSelf)
        {
            _target.IsMerging = false;
            _target.RestoreScale();
        }

        // Yemenin ortasında iptal edildiyse kemirme titreşimi asılı kalmasın
        if (_state == State.Eat && !_fruitVanished) GameEvents.RaiseWormsChewingChanged(false);

        if (FaceDirector.Instance != null) FaceDirector.Instance.SetBoostFocus(null);

        if (_worms != null)
            for (int i = 0; i < _worms.Length; i++)
                if (_worms[i] != null) _worms[i].Deactivate();

        _wormsActive   = 0;
        _target        = null;
        _fruitVanished = false;
        _state         = State.Idle;

        HidePulse();

        for (int i = 0; i < _cursors.Count; i++)
            if (_cursors[i] != null) _cursors[i].gameObject.SetActive(false);

        _cursorAlpha = 0f;

        GameEvents.RaiseBoostStateChanged(BoostId.Worms, false, _charges);
    }
}
