using UnityEngine;
using UnityEngine.EventSystems;

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

        if (_pool != null) _pool.DespawnAll();

        if (_dropIndicator != null) _dropIndicator.Hide();

        if (_nextDisplay != null) _nextDisplay.Clear();
    }

    void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;

        // Boost hedef beklerken ya da kurtlar sahnedeyken bırakma yok — aynı dokunuş
        // hem meyveyi seçip hem yeni meyve bırakmasın.
        if (WormBoostDirector.Instance != null && WormBoostDirector.Instance.IsBusy)
        {
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
        bool held = false, released = false;
        Vector2 screenPos = default;
        int fingerId = -1;

        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);

            screenPos = t.position;
            fingerId  = t.fingerId;

            held      = t.phase == TouchPhase.Began || t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary;
            released  = t.phase == TouchPhase.Ended;
        }
        else
        {
            screenPos = Input.mousePosition;
            held      = Input.GetMouseButton(0);
            released  = Input.GetMouseButtonUp(0);
        }

        if (!held && !released) return;

        bool overUI = fingerId >= 0
            ? EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fingerId)
            : EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (overUI) return;

        Vector3 world = _camera.ScreenToWorldPoint(screenPos);

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

        _pending.Drop();

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
