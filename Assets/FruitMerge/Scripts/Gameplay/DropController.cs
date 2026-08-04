using UnityEngine;

/// <summary>
/// <b>Execution order 50 — EventSystem'den (0) SONRA.</b> Aynı karede önce UI tıklaması
/// çözülsün istiyoruz: buton <c>onClick</c>'i çalıştıktan sonra buraya gelindiğinde
/// <see cref="BoostGate.IsAnyBusy"/> ve <see cref="PointerInput.IsOverUI"/> güncel oluyor.
/// Sıra tersken PLAY'e basan tık, menü kapanmadan önce bir meyve bırakıyordu.
/// </summary>
[DefaultExecutionOrder(50)]
public class DropController : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] FruitPool _pool;
    [SerializeField] SpawnQueue _spawnQueue;
    [SerializeField] GameConfig _config;
    [SerializeField] Transform _pendingParent;
    [SerializeField] Camera _camera;
    [SerializeField] DropIndicatorController _dropIndicator;

    [Tooltip("dalın üst yuvasındaki sıradaki meyve göstergesi")]
    [SerializeField] NextFruitDisplay _nextDisplay;

    Fruit _pending;
    float _cooldownTimer;
    float _bufferTimer;

    // Yeni bekleyen meyve, bırakılan meyve yeterince uzaklaşana kadar bekletiliyor —
    // yoksa düşenin tam tepesinde beliriyor ve üst üste binmiş görünüyor.
    Fruit _lastDropped;
    bool  _awaitingPending;
    float _pendingWaitTimer;

    /// <summary>
    /// Bu dokunuşun TAMAMI dünya girdisi sayılmıyor. Kilit BASIŞ anında konuyor,
    /// bırakma anında değil — bırakmada UI kontrolü yapmak yetmiyordu:
    ///
    ///  - PLAY'e basan tıkın bırakılması, menü kapanır kapanmaz ilk meyveyi düşürüyordu.
    ///  - Boost butonuna basan dokunuş hem butonu tetikleyip hem tahtaya sızıyordu.
    ///
    /// Sadece yeni bir BASIŞ temizliyor, yani sızan bırakma hiçbir yolla geçemiyor.
    /// </summary>
    bool _gestureBlocked;

    void OnEnable()
    {
        GameEvents.OnRunStarted   += HandleRunStarted;
        GameEvents.OnStateChanged += HandleStateChanged;
    }

    void OnDisable()
    {
        GameEvents.OnRunStarted   -= HandleRunStarted;
        GameEvents.OnStateChanged -= HandleStateChanged;
    }

    void Start()
    {
        if (_camera == null) _camera = Camera.main;

        // _config bu sınıfın HER karesinde okunuyor; boş kalırsa sessizce NRE yağmuru
        // yerine tek bir anlaşılır hata verip duruyoruz. (_pool, _dropIndicator ve
        // _nextDisplay için zaten null kontrolleri var, _config atlanmıştı.)
        if (_config == null)
        {
            Debug.LogError("DropController: GameConfig bağlı değil — bırakma yüksekliği " +
                           "okunamıyor, bileşen kapatılıyor.", this);

            enabled = false;

            return;
        }

        transform.position = new Vector3(transform.position.x, _config.dropY, 0f);

        // Bekleyen meyve artık burada doğmuyor. Açılışta state Menu olduğu için
        // menünün arkasında dalda meyve asılı kalırdı — OnRunStarted'ı bekliyoruz.
    }

    void HandleRunStarted()
    {
        // Restart artık sahneyi yeniden yüklemiyor, tahtayı burada boşaltmak zorundayız.
        // Sıra önemli: önce bekleyeni bırak, sonra havuzu boşalt, en son yeni meyveyi doğur.
        ClearPending();

        if (_pool != null) _pool.DespawnAll();

        _awaitingPending = false;
        _lastDropped = null;
        _cooldownTimer = 0f;
        _bufferTimer = 0f;

        // Oyunu başlatan dokunuş (menüdeki PLAY, sonuç ekranındaki RESTART) hâlâ ekranda
        // olabilir; onun bırakılması ilk meyveyi düşürmesin. Yeni basış bekleniyor.
        _gestureBlocked = true;

        PreparePending();
    }

    void HandleStateChanged(GameState s)
    {
        if (s != GameState.Menu) return;

        // Menüye dönüldü: tahtayı ve dalı boşalt. Restart sahneyi yeniden yüklediği için
        // oradan gelmiyor — bu yol sadece pause/sonuç ekranındaki MENU butonundan.
        ClearPending();

        _awaitingPending = false;
        _lastDropped = null;
        _bufferTimer = 0f;
        _gestureBlocked = true;

        if (_pool != null) _pool.DespawnAll();

        if (_dropIndicator != null) _dropIndicator.Hide();

        if (_nextDisplay != null) _nextDisplay.Clear();
    }

    void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;

        // Herhangi bir boost oynarken bırakma yok — aynı dokunuş hem hedef seçip hem yeni
        // meyve bırakmasın, ve deprem sırasında yığına yeni meyve eklenmesin.
        if (BoostGate.IsAnyBusy)
        {
            // Boost oynarken basışları hiç görmüyoruz, dolayısıyla kilidi de
            // güncelleyemiyoruz. Boost bittiği anda havada kalan dokunuşun bırakılması
            // meyve düşürmesin: "boşluğa dokunup kurtçukları iptal et" hareketi tam da
            // bunu yapıyordu (iptal aynı karede, DropController'dan önce oluyor).
            _gestureBlocked = true;

            TickPendingSpawn();
            return;
        }

        TickPendingSpawn();

        TickTimers();

        HandleInput();
    }

    void TickTimers()
    {
        if (_cooldownTimer > 0f)
        {
            _cooldownTimer -= Time.deltaTime;

            // Bekleyen meyve henüz doğmadıysa tamponu HARCAMA — yoksa oyuncunun
            // erken dokunuşu sessizce kaybolur
            if (_cooldownTimer <= 0f && _bufferTimer > 0f && _pending != null)
            {
                _bufferTimer = 0f;
                Drop();
                return;
            }
        }

        if (_bufferTimer > 0f) _bufferTimer -= Time.deltaTime;
    }

    /// <summary>
    /// Bırakılan meyve yeterince uzaklaştıysa (ya da emniyet süresi doldu ise)
    /// yeni bekleyen meyveyi doğurur.
    /// </summary>
    void TickPendingSpawn()
    {
        if (!_awaitingPending) return;

        _pendingWaitTimer -= Time.deltaTime;

        bool clear = _pendingWaitTimer <= 0f;

        if (!clear)
        {
            // meyve birleşip havuza döndüyse ortada bir şey kalmadı
            if (_lastDropped == null || !_lastDropped.gameObject.activeSelf || _lastDropped.IsMerging)
            {
                clear = true;
            }
            else
            {
                // Gereken düşüş = iki meyvenin yarıçapları + pay. Yeni meyve dropY'de
                // duracak, alt kenarı dropY - rYeni; düşenin tepesi y + rEski.
                float needed = _lastDropped.Radius + PeekPendingRadius() + _config.pendingSpawnPadding;

                float fallen = _config.dropY - _lastDropped.transform.position.y;

                clear = fallen >= needed;
            }
        }

        if (!clear) return;

        _awaitingPending = false;
        _lastDropped = null;

        PreparePending();
    }

    /// <summary>Sıradaki meyvenin dünya yarıçapı — tüketmeden.</summary>
    float PeekPendingRadius()
    {
        FruitDefinition def = _spawnQueue.Peek();

        return def != null ? def.colliderRadius * def.scale : 0f;
    }

    void HandleInput()
    {
        // Kilit kararı BASIŞ anında veriliyor ve dokunuş boyunca sabit kalıyor.
        if (PointerInput.Began) _gestureBlocked = PointerInput.IsOverUI();

        bool held     = PointerInput.Held;
        bool released = PointerInput.Released;

        if (!held && !released) return;

        if (_gestureBlocked)
        {
            // Dokunuş bitti — kilit sadece burada kalkıyor, sıradaki basış serbest.
            if (released) _gestureBlocked = false;

            return;
        }

        // Tahtada başlayıp parmağını HUD'un üstüne kaydıranı da tutuyoruz: meyve
        // görünmeyen bir yere bırakılmasın.
        if (PointerInput.IsOverUI()) return;

        Vector3 world = _camera.ScreenToWorldPoint(PointerInput.Position);

        float limit = DropLimitX();

        float x = Mathf.Clamp(world.x, -limit, limit);

        transform.position = new Vector3(x, _config.dropY, 0f);

        if (!released) return;

        // bekleyen meyve yoksa da tamponla — birazdan doğacak
        if (_cooldownTimer > 0f || _pending == null)
        {
            _bufferTimer = _config.inputBufferTime;
            return;
        }

        Drop();
    }

    float DropLimitX()
    {
        // Bekleyen meyve yokken sıradakinin yarıçapını kullan, yoksa sınır genişler ve
        // meyve duvarın içinde doğar
        float radius = _pending != null ? _pending.Radius : PeekPendingRadius();

        return Mathf.Max(0f, _config.wallInnerX - radius - _config.dropEdgePadding - _config.dropJitterX);
    }

    void Drop()
    {
        if (_pending == null) return;

        _pending.transform.SetParent(_pool.ActiveParent, true);

        _pending.Drop(true);

        GameEvents.RaiseFruitDropped(_pending.Definition);

        _lastDropped = _pending;

        _pending = null;

        _cooldownTimer = _config.dropCooldown;

        // yeni meyve hemen doğmuyor; düşen uzaklaşınca TickPendingSpawn doğuracak
        _awaitingPending = true;
        _pendingWaitTimer = _config.pendingSpawnMaxWait;

        if (_dropIndicator != null) _dropIndicator.Hide();

        // sıradaki meyve yuvadan aşağı kayıp bekleyen meyvenin yerini almaya başlar
        if (_nextDisplay != null) _nextDisplay.BeginHandoff();
    }

    void PreparePending()
    {
        FruitDefinition def = _spawnQueue.Next();

        _pending = _pool.Spawn(def, Vector2.zero);

        _pending.transform.SetParent(_pendingParent, false);

        // Meyvenin TEPESİ sapın ucuna değsin: küçük meyve yukarıda, büyük meyve aşağıda
        // asılır. Sabit merkezde kiraz daldan kopuk görünüyordu.
        float hangY = _config.dropperTwigTipY - _pending.Radius;

        _pending.transform.localPosition = new Vector3(0f, hangY, 0f);

        // göstergeye meyvenin gerçek alt kenarını ver — artık merkez dropY'de değil
        float bottomWorldY = _config.dropY + hangY - _pending.Radius;

        // Aynı alan bu dosyanın iki başka yerinde (Drop, HandleStateChanged) null
        // kontrolüyle kullanılıyor — burada da aynı standart.
        if (_dropIndicator != null)
            _dropIndicator.SetPending(bottomWorldY, _pending.Definition.displayColor);

        // Next() tüketti, Peek() artık BİR SONRAKİ meyveyi veriyor — yuvaya o yerleşir.
        // Devirden gelen sprite bu anda yuvaya geri sıçrayıp yeni meyveyle belirir.
        if (_nextDisplay != null) _nextDisplay.Show(_spawnQueue.Peek());
    }

    public void ClearPending()
    {
        if (_pending == null) return;

        _pending.transform.SetParent(_pool.ActiveParent, false);

        _pool.Despawn(_pending);

        _pending = null;
    }
}
