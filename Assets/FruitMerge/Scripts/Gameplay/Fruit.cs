using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class Fruit : MonoBehaviour
{
    private Rigidbody2D _rb;
    private SpriteRenderer _sr;
    private CircleCollider2D _col;

    private MergeHandler _merge;

    [Header("Yüz")]
    [Tooltip("Face child'ındaki FruitFace. Prefab'da bir kez bağlanır")]
    [SerializeField] private FruitFace _face;

    [Tooltip("12 ifade × 4 boyut tablosu. Prefab'da bir kez bağlanır")]
    [SerializeField] private FaceSet _faceSet;

    /// <summary>FaceDirector buradan erişiyor. Yüz yoksa null.</summary>
    public FruitFace Face => _face;

    public FruitDefinition Definition { get; private set; }
    
    public bool IsMerging { get; set; }
    
    public bool IsDropped { get; private set; }

    public float DropTime { get; private set; }

    /// <summary>
    /// Bu meyve oyuncunun elinden mi düştü, yoksa birleşmeden mi doğdu. İkisi de
    /// <see cref="Drop"/> çağırıp aynı <see cref="DropTime"/>'ı alıyor, ama
    /// <see cref="FaceDirector"/>'ün bakış hedefi için aralarındaki fark önemli:
    /// oyuncunun bıraktığı meyve hızlanmasını beklemeden takip edilirken, birleşme
    /// ürününün bakışı kapmaması gerekiyor.
    /// </summary>
    public bool WasPlayerDropped { get; private set; }

    public Rigidbody2D Body => _rb;
    public float Radius => _col.radius * _targetScale;
    public float TopY => transform.position.y + _col.offset.y * _targetScale + Radius;

    private int _slowFrames;
    private float _targetScale;
    float _popTimer = -1f;
    float _squashTimer = -1f;
    float _squashIntensity;
    GameConfig _config;

    /// <summary>
    /// Fizik adımı sayacı. <c>Time.frameCount</c> KULLANILMIYOR: bir render karesinde
    /// birden fazla fizik adımı olabiliyor ve o zaman aşağıdaki guard yanlış eliyor olurdu.
    /// </summary>
    int _physicsStep;

    /// <summary>
    /// <see cref="TryRearmContinuous"/>'un son çalıştığı fizik adımı. Yığının içindeki
    /// meyvenin 4 komşusu varsa <c>OnCollisionStay2D</c> aynı adımda 4 kez geliyor, ama
    /// o metot temas parametresine hiç bakmıyor — adım başına bir kez yeterli.
    /// </summary>
    int _rearmStep = -1;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _sr = GetComponent<SpriteRenderer>();
        _col = GetComponent<CircleCollider2D>();

        _rb.useAutoMass = false;
    }

    public void Bind(MergeHandler handler, GameConfig config)
    {
        _merge = handler;
        _config = config;   
    }

    public void Initialize(FruitDefinition def)
    {
        Definition = def;
        
        _sr.sprite = def.sprite;
        _sr.color = def.tint;
        _targetScale = def.scale;
        transform.localScale = Vector3.one * def.scale;

       
        _col.radius = def.colliderRadius;
        _col.offset = def.colliderOffset;

        _rb.mass = def.mass;
        _sr.sortingOrder = 100 - def.tier;
        
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        _rb.rotation = 0f;
        transform.rotation  = Quaternion.identity;
        
        IsMerging = false;
        IsDropped = false;
        DropTime = 0f;
        WasPlayerDropped = false;
        _slowFrames = 0;
        _popTimer = -1f;
        _squashTimer = -1f;

        _rb.simulated = false;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;

        // yüz gövdenin bir üstünde çizilsin; aynı atlasta oldukları için batch bozulmaz
        if (_face != null)
            _face.Bind(_faceSet, def.faceSize, def.faceOffset, _sr.sortingOrder + 1, def.sprite, _config);
    }

    public void ResetState()
    {
        _rb.linearVelocity  = Vector2.zero;
        _rb.angularVelocity = 0f;
        _rb.rotation        = 0f;
        transform.rotation  = Quaternion.identity;
        IsMerging  = false;
        IsDropped  = false;
        DropTime   = 0f;
        WasPlayerDropped = false;
        _slowFrames = 0;
        _popTimer  = -1f;
        _squashTimer = -1f;
        _rearmStep = -1;
        _rb.simulated = false;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;

        // havuzdan çıkan meyve önceki oyunun yüzüyle gelmesin
        if (_face != null) _face.ResetFace();
    }

    /// <summary>
    /// Meyveyi fiziğe teslim eder.
    /// </summary>
    /// <param name="byPlayer">
    /// Oyuncu mu bıraktı. Varsayılan YOK, iki çağıran da açıkça söylüyor:
    /// <see cref="DropController"/> <c>true</c>, <see cref="MergeHandler"/> <c>false</c>.
    /// Bir gün üçüncü bir çağıran eklenirse sessizce "oyuncu bıraktı" sayılmasın.
    /// </param>
    public void Drop(bool byPlayer)
    {
        IsDropped = true;
        DropTime = Time.time;
        WasPlayerDropped = byPlayer;

        if (_config != null)
        {
            transform.position += new Vector3(Random.Range(-_config.dropJitterX, _config.dropJitterX), 0f, 0);
        }
        _rb.simulated = true;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (_config != null)
        {
            _rb.angularVelocity = Random.Range(-_config.dropSpin, _config.dropSpin);
        }
    }

    /// <summary>
    /// Meyveyi olduğu yerde dondurur: hızları sıfırlanır ve gövde simülasyondan çıkar.
    ///
    /// Oyun sonunda gerekiyor. <c>Time.timeScale = 0</c> yerine bu yol seçildi çünkü
    /// timeScale sonuç ekranını da dondurmuyor ama <see cref="FruitFace"/> geçişlerini
    /// (<c>Time.deltaTime</c> ile ilerliyorlar) ve meyve suyu parçacıklarını donduruyordu:
    /// yüzler dizzy/squish ifadesine geçemeden yarı yolda kalıyordu. Burada sadece FİZİK
    /// susuyor, animasyonlar normal akmaya devam ediyor.
    ///
    /// <c>simulated = false</c> collider'ı da devre dışı bırakıyor — yani oyun bittikten
    /// sonra artık birleşme de tetiklenmiyor, ki istenen de bu.
    ///
    /// Havuz açısından güvenli: <see cref="ResetState"/> ve <see cref="Initialize"/> zaten
    /// <c>simulated = false</c> ile başlıyor, <see cref="Drop"/> tekrar açıyor.
    /// </summary>
    public void Freeze()
    {
        _rb.linearVelocity  = Vector2.zero;
        _rb.angularVelocity = 0f;
        _rb.simulated       = false;
    }

    public void PlayPop()
    {
        if (_config == null) return;

        _popTimer = 0f;

        transform.localScale = Vector3.one * (_targetScale * _config.popStartScale);

    }

    /// <summary>
    /// Görsel ölçeği hedef ölçeğe geri oturtur ve çalışan pop/squash animasyonunu iptal eder.
    ///
    /// Kurtçuk boost'u yeme sırasında meyveyi küçültüyor (<c>WormBoostDirector.ShrinkFruit</c>).
    /// Boost yarıda kesilirse (pause / menü / oyun sonu) meyve o ölçekte kalıyordu —
    /// <c>CircleCollider2D</c> transform ölçeğiyle birlikte küçüldüğü için meyve FİZİKSEL
    /// olarak da küçük kalıyor ve yığın onun etrafında çöküyordu. Üstelik <see cref="Radius"/>
    /// ve <see cref="TopY"/> hâlâ <c>_targetScale</c>'i kullandığı için doluluk/sınır
    /// hesapları o meyve için yanlış değer üretiyordu.
    /// </summary>
    public void RestoreScale()
    {
        _popTimer    = -1f;
        _squashTimer = -1f;

        transform.localScale = Vector3.one * _targetScale;
    }

    public void PlaySquash(float intensity)
    {
        if (_config == null) return;

        // daha güçlü bir çarpma devam eden ezilmeyi baştan başlatsın, daha zayıfı yok saysın
        if (_squashTimer >= 0f && intensity < _squashIntensity) return;

        _squashIntensity = Mathf.Clamp01(intensity);
        _squashTimer = 0f;
    }

    /// <summary>
    /// Pop / squash animasyonu.
    ///
    /// <b>Kendi <c>Update</c>'i YOK</b> (kural 7): <see cref="FruitTicker"/> tek döngüden
    /// çağırıyor. Sahnede en çok kopyası olan bileşen bu — 60 meyve için 60 ayrı
    /// managed↔native geçişi ödemenin karşılığı yok. Aynı desen <see cref="FruitFace"/>,
    /// <see cref="Worm"/> ve <see cref="ComboPopupItem"/>'da zaten uygulanmış.
    /// </summary>
    public void TickVisual(float dt)
    {
        if (_popTimer < 0f && _squashTimer < 0f) return;

        float popScale = 1f;

        if (_popTimer >= 0f)
        {
            _popTimer += dt;

            float t = Mathf.Clamp01(_popTimer / _config.popDuration);

            float overshoot = (_config.popOverShot - 1f) * Mathf.Sin(t * Mathf.PI);

            popScale = Mathf.Lerp(_config.popStartScale, 1f, t) + overshoot;

            if (t >= 1f)
            {
                popScale = 1f;
                _popTimer = -1f;
            }
        }

        float squashX = 1f, squashY = 1f;

        if (_squashTimer >= 0f)
        {
            _squashTimer += dt;

            float t = Mathf.Clamp01(_squashTimer / _config.squashDuration);

            float minY = Mathf.Lerp(1f, _config.squashMinScale, _squashIntensity);

            float overshoot = (_config.squashOverShot - 1f) * Mathf.Sin(t * Mathf.PI) * _squashIntensity;

            squashY = Mathf.Lerp(minY, 1f, t) + overshoot;
            squashX = 1f + (1f - squashY) * 0.6f;

            if (t >= 1f)
            {
                squashY = 1f;
                squashX = 1f;
                _squashTimer = -1f;
            }
        }

        transform.localScale = new Vector3(_targetScale * popScale * squashX, _targetScale * popScale * squashY, 1f);
    }

    /// <summary>
    /// Dönüş söndürme + Continuous→Discrete geçişi. Fizik yazması fizik adımına ait
    /// olduğu için <see cref="FruitTicker"/>'ın <c>FixedUpdate</c>'inden çağrılıyor
    /// (kendi <c>FixedUpdate</c>'i yok — bkz. <see cref="TickVisual"/>).
    /// </summary>
    public void TickPhysics()
    {
        // Sayaç erken çıkışlardan ÖNCE artıyor: uyuyan meyve uyandığında
        // OnCollisionStay2D'nin guard'ı bayat kalmasın.
        _physicsStep++;

        if (_config == null) return;

        // Daldaki bekleyen meyve simülasyonda değil: hızı da çarpışma modu da
        // ResetState'te sabitlendi, burada yapılacak iş yok.
        if (!_rb.simulated) return;

        // Uyuyan gövdede bu metodun TAMAMI no-op: hız uyku toleransının altında, açısal
        // hız uykuya geçmeden önce zaten 0'a sönmüş (spinSettleRate 180°/sn², uyku eşiği
        // 2°/sn) ve mod da Discrete'e düşmüş. Suika tipi bir oyunda yığının büyük kısmı
        // zamanın çoğunda uyuyor, yani bu kapı en kalabalık anda en çok işi eliyor.
        // Temas gövdeyi uyandırdığı anda tick kendiliğinden geri geliyor.
        if (!_rb.IsAwake()) return;

        float limitSqr = _config.continuousExitSpeed * _config.continuousExitSpeed;
        bool isSlow = _rb.linearVelocity.sqrMagnitude < limitSqr;

        if (isSlow)
        {
            _rb.angularVelocity = Mathf.MoveTowards(_rb.angularVelocity, 0f, _config.spinSettleRate * Time.fixedDeltaTime);
        }

        if (_rb.collisionDetectionMode == CollisionDetectionMode2D.Discrete) return;

        if (isSlow)
        {
            if (++_slowFrames >= _config.continuousExitFrames)
            {
                _rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
            }

        }
        else
        {
            _slowFrames = 0;

        }
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        TryRequestMerge(c);
        TryRequestSquash(c);

        // Enter temas başına en fazla bir kez geldiği için burada guard'a gerek yok.
        TryRearmContinuous();
    }

    void OnCollisionStay2D(Collision2D c)
    {
        // Temas BAŞINA gerekli: her komşuyu ayrı ayrı sınaması lazım.
        TryRequestMerge(c);

        // TryRearmContinuous temas parametresine BAKMIYOR, sadece kendi hızımıza bakıyor —
        // yığının içindeki meyvenin 4 komşusu varsa aynı hesap aynı fizik adımında 4 kez
        // yapılıyordu. Adım başına bir kez yeterli: adım içinde hız değişmiyor, dolayısıyla
        // "sert çarpışmadan sonra ANINDA Continuous'a dön" tünelleme garantisi bozulmuyor.
        if (_rearmStep == _physicsStep) return;

        _rearmStep = _physicsStep;

        TryRearmContinuous();
    }

    // Discrete moddaki meyve sert bir çarpışmadan hızlı çıkarsa, sweep taraması olmadan
    // ince duvar/taban collider'larını "atlayıp" tünelleyebilir — bu yüzden anında geri Continuous'a alınır.
    void TryRearmContinuous()
    {
        if (_config == null) return;
        if (_rb.collisionDetectionMode != CollisionDetectionMode2D.Discrete) return;

        float limitSqr = _config.continuousRearmSpeed * _config.continuousRearmSpeed;
        if (_rb.linearVelocity.sqrMagnitude < limitSqr) return;

        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _slowFrames = 0;
    }

    void TryRequestSquash(Collision2D c)
    {
        if (_config == null || !IsDropped) return;
        if (!c.collider.TryGetComponent(out Fruit other)) return;

        // sadece üstten gelen çarpmada ez — diğer meyve bundan yukarıda olmalı
        if (other.transform.position.y <= transform.position.y) return;

        float speed = c.relativeVelocity.magnitude;
        if (speed < _config.squashMinImpactSpeed) return;

        float intensity = Mathf.InverseLerp(_config.squashMinImpactSpeed, _config.squashMaxImpactSpeed, speed);

        PlaySquash(intensity);
    }

    void TryRequestMerge(Collision2D c)
    {
        if (_merge == null) return;
        if (IsMerging || !IsDropped) return;
        
        if (!c.collider.TryGetComponent(out Fruit other)) return;

        if (other.Definition != Definition) return;
        
        if(other.IsMerging  || !other.IsDropped) return;

        if (GetInstanceID() > other.GetInstanceID()) return;

        _merge.Request(this, other);

    }
    

}
