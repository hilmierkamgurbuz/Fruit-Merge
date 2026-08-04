using UnityEngine;

public class DropIndicatorController : MonoBehaviour
{
    [SerializeField] Collider2D _floor;
    [SerializeField] GameConfig _config;
    [SerializeField] LayerMask _mask;

    SpriteRenderer _renderer;
    MaterialPropertyBlock _mpb;
    float _fruitBottomWorldY;
    bool _hasPending;

    /// <summary>
    /// Zeminin üst yüzeyi. <c>Collider2D.bounds</c> native bir çağrı ve zemin hiç hareket
    /// etmiyor — <c>GameOverDetector.FloorY</c> ve <c>QuakeBoostDirector.Start</c> ile aynı
    /// desen. Eskiden her karede yeniden okunuyordu.
    /// </summary>
    float _floorY;

    void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _mpb = new MaterialPropertyBlock();
    }

    void Start()
    {
        _floorY = _floor != null ? _floor.bounds.max.y : transform.position.y - 5f;

        if (_floor == null)
            Debug.LogWarning("DropIndicatorController: _floor bağlı değil — gösterge " +
                             "zemine kadar uzamayacak. Wall_Bottom'ın collider'ını bağla.", this);

        if (_config == null)
            Debug.LogError("DropIndicatorController: GameConfig bağlı değil, bileşen " +
                           "kapatılıyor.", this);
    }

    /// <param name="fruitBottomWorldY">
    /// Bekleyen meyvenin alt kenarının dünya y'si. Meyve artık dropY'de merkezlenmiyor —
    /// tepesi dalın sapına değecek şekilde asılıyor, o yüzden yarıçaptan hesaplanamıyor.
    /// </param>
    public void SetPending(float fruitBottomWorldY, Color tint)
    {
        _fruitBottomWorldY = fruitBottomWorldY;
        _hasPending = true;

        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor("_Color", tint);
        _renderer.SetPropertyBlock(_mpb);
    }

    /// <summary>
    /// Bekleyen meyve yokken göstergeyi kapat. Bırakma ile yeni meyvenin doğması
    /// arasındaki boşlukta, düşen meyvenin rengiyle asılı kalmasın.
    /// </summary>
    public void Hide() => _hasPending = false;

    void Update()
    {
        bool playing = GameManager.Instance != null && GameManager.Instance.IsPlaying;

        bool visible = playing && _hasPending;

        // enabled setter'ı native bir çağrı: yalnızca durum DEĞİŞTİYSE yaz (kural 9).
        if (_renderer.enabled != visible) _renderer.enabled = visible;

        if (!visible || _config == null) return;

        float topWorldY = _fruitBottomWorldY - _config.dropIndicatorSkin;
        Vector2 origin = new Vector2(transform.position.x, topWorldY);

        float maxDist = Mathf.Max(0.01f, topWorldY - _floorY + 1f);

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, maxDist, _mask);
        float endWorldY = hit.collider != null ? hit.point.y : _floorY;

        float span = Mathf.Max(0.05f, topWorldY - endWorldY);
        Vector2 size = _renderer.size;
        size.y = span;
        _renderer.size = size;

        float worldCenterY = (topWorldY + endWorldY) * 0.5f;
        Vector3 lp = transform.localPosition;
        lp.y = worldCenterY - _config.dropY;
        transform.localPosition = lp;
    }
}
