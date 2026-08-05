using UnityEngine;

public class DropIndicatorController : MonoBehaviour
{
    [SerializeField] Collider2D _floor;
    [SerializeField] GameConfig _config;
    [SerializeField] LayerMask _mask;

    [Tooltip("Rainbow boost'ta göstergedeki noktaların rengini buradan okuyoruz — her " +
             "tier'ın kendi displayColor'ı, gökkuşağı değil GERÇEK meyve renkleri sırayla")]
    [SerializeField] FruitDatabase _database;

    SpriteRenderer _renderer;
    MaterialPropertyBlock _mpb;
    float _fruitBottomWorldY;
    bool _hasPending;

    /// <summary>SpriteDashFlow.shader'daki MAX_PALETTE_COLORS ile birebir aynı olmalı.</summary>
    const int MaxPaletteColors = 16;

    /// <summary>
    /// Meyve renkleri, tier sırasıyla — bir kez kuruluyor (kural 13), her SetPending'de
    /// yeniden hesaplanmıyor. FruitDatabase oyun boyunca değişmiyor.
    /// </summary>
    Vector4[] _paletteCache;
    int _paletteCount;

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

        BuildPalette();
    }

    /// <summary>
    /// FruitDatabase'deki her tier'ın <c>displayColor</c>'ını tier sırasıyla diziye kopyalar.
    /// Rainbow modunda shader'a GİDECEK olan palet bu — sentetik bir HSV gökkuşağı değil,
    /// oyundaki GERÇEK meyve renkleri.
    /// </summary>
    void BuildPalette()
    {
        _paletteCache = new Vector4[MaxPaletteColors];
        _paletteCount = 0;

        if (_database == null)
        {
            Debug.LogWarning("DropIndicatorController: _database bağlı değil — rainbow " +
                             "modunda gösterge renksiz (beyaz) kalır.", this);
            return;
        }

        for (int i = 0; i < _database.fruits.Count && _paletteCount < MaxPaletteColors; i++)
        {
            FruitDefinition def = _database.fruits[i];

            if (def == null) continue;

            Color c = def.displayColor;

            _paletteCache[_paletteCount++] = new Vector4(c.r, c.g, c.b, 1f);
        }
    }

    /// <param name="fruitBottomWorldY">
    /// Bekleyen meyvenin alt kenarının dünya y'si. Meyve artık dropY'de merkezlenmiyor —
    /// tepesi dalın sapına değecek şekilde asılıyor, o yüzden yarıçaptan hesaplanamıyor.
    /// </param>
    /// <param name="isRainbow">
    /// Bekleyen meyve Rainbow boost'un joker meyvesi mi. <c>true</c>ysa <paramref name="tint"/>
    /// yok sayılır — şerit <see cref="SpriteDashFlow"/>'un <c>_RainbowMode</c>'una geçip her
    /// noktayı sırayla GERÇEK bir meyve rengiyle çiziyor (bkz. <see cref="BuildPalette"/> ve
    /// <c>GameConfig.dropIndicatorRainbowRunLength</c>).
    /// </param>
    public void SetPending(float fruitBottomWorldY, Color tint, bool isRainbow = false)
    {
        _fruitBottomWorldY = fruitBottomWorldY;
        _hasPending = true;

        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor("_Color", tint);
        _mpb.SetFloat("_RainbowMode", isRainbow ? 1f : 0f);

        if (isRainbow)
        {
            if (_config != null)
                _mpb.SetFloat("_ColorRunLength", _config.dropIndicatorRainbowRunLength);

            _mpb.SetFloat("_PaletteCount", _paletteCount);
            _mpb.SetVectorArray("_PaletteColors", _paletteCache);
        }

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