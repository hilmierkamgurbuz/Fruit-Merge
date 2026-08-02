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
    
    public Rigidbody2D Body => _rb;
    public float Radius => _col.radius * _targetScale;
    public float TopY => transform.position.y + _col.offset.y * _targetScale + Radius;

    private int _slowFrames;
    private float _targetScale;
    float _popTimer = -1f;
    float _squashTimer = -1f;
    float _squashIntensity;
    GameConfig _config;

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
        _slowFrames = 0;
        _popTimer  = -1f;
        _squashTimer = -1f;
        _rb.simulated = false;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;

        // havuzdan çıkan meyve önceki oyunun yüzüyle gelmesin
        if (_face != null) _face.ResetFace();
    }

    public void Drop()
    {
        IsDropped = true;
        DropTime = Time.time;

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

    public void PlayPop()
    {
        if (_config == null) return;

        _popTimer = 0f;

        transform.localScale = Vector3.one * (_targetScale * _config.popStartScale);

    }

    public void PlaySquash(float intensity)
    {
        if (_config == null) return;

        // daha güçlü bir çarpma devam eden ezilmeyi baştan başlatsın, daha zayıfı yok saysın
        if (_squashTimer >= 0f && intensity < _squashIntensity) return;

        _squashIntensity = Mathf.Clamp01(intensity);
        _squashTimer = 0f;
    }

    void Update()
    {
        if (_popTimer < 0f && _squashTimer < 0f) return;

        float popScale = 1f;

        if (_popTimer >= 0f)
        {
            _popTimer += Time.deltaTime;

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
            _squashTimer += Time.deltaTime;

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

    private void FixedUpdate()
    {
        if (_config == null) return;

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
        TryRearmContinuous();
    }

    void OnCollisionStay2D(Collision2D c)
    {
        TryRequestMerge(c);
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
