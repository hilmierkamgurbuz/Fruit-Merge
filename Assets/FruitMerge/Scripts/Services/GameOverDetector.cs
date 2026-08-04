using UnityEngine;

public class GameOverDetector : MonoBehaviour
{
    [SerializeField] FruitPool _pool;
    [SerializeField] GameConfig _config;
    [SerializeField] float _lineHalfWidth = 3f;
    [SerializeField] SpriteRenderer _lineRenderer;
    [SerializeField]  Collider2D _floor;


    private float _fillRatio;
    float _violationTimer;
    float _checkTimer;
    bool  _fired;

    /// <summary>
    /// Yığının doluluk oranı (0-1). Zaten gameOverCheckInterval periyoduyla hesaplanıyor —
    /// FaceDirector buradan okuyor, yeniden hesaplamıyor.
    /// </summary>
    public float FillRatio => _fillRatio;

    /// <summary>Danger line'ın dünya yüksekliği. Yüzlerin baktığı nokta.</summary>
    public float LineY => transform.position.y;

    float _cachedFloorY;
    bool  _floorCached;

    /// <summary>
    /// Zeminin üst yüzeyi. Zemin hareket etmediği için bir kez hesaplanıp saklanıyor —
    /// Collider2D.bounds native bir çağrı, her karede meyve başına istemiyoruz.
    /// </summary>
    public float FloorY
    {
        get
        {
            if (!_floorCached)
            {
                _cachedFloorY = _floor != null ? _floor.bounds.max.y : transform.position.y - 5f;
                _floorCached = true;
            }

            return _cachedFloorY;
        }
    }

    void OnEnable()
    {
        GameEvents.OnStateChanged += HandleStateChanged;
        GameEvents.OnRunStarted   += HandleRunStarted;
    }

    void OnDisable()
    {
        GameEvents.OnStateChanged -= HandleStateChanged;
        GameEvents.OnRunStarted   -= HandleRunStarted;
    }

    void HandleStateChanged(GameState s)
    {
        if (s == GameState.Playing) { _fired = false; _violationTimer = 0f; }
    }

    /// <summary>
    /// Yeni oyun: doluluk oranını sıfırla. OnStateChanged(Playing) DEĞİL, çünkü o
    /// pause'dan dönüşte de geliyor ve oranı orada sıfırlamak çizgiyi bir anlık
    /// söndürürdü.
    ///
    /// Çizgi artık oyun boyunca görünür olduğu için bu şart: oyun sonunda oran ~1
    /// kalıyor, tahta temizlendiği ilk 100 ms'de çizgi bomboş tahtanın üstünde
    /// kırmızı kırmızı çakıyordu.
    /// </summary>
    void HandleRunStarted()
    {
        _fillRatio  = 0f;
        _checkTimer = 0f;
    }

    void Update()
    {
        bool playing = GameManager.Instance != null && GameManager.Instance.IsPlaying && !_fired;
        if (!playing) { SetLineAlpha(0f); return; }

        // Boost oynarken oyunu bitirme: boost tam da yığını indirmek için çağrıldı, iş
        // yaparken sayacın dolması haksızlık olurdu. Deprem için ayrıca gerekli — meyveler
        // sarsılırken kısa süre çizginin üstüne çıkabiliyorlar.
        if (BoostGate.IsAnyBusy)
        {
            _violationTimer = 0f;

            // Çizgiyi sürmeye devam et: boost 2-3 saniye sürüyor, buradan dönersek
            // çizgi o süre boyunca yanıp sönmenin ortasında bir alpha'da donup kalıyordu.
            // _fillRatio güncellenmediği için son bilinen tehlike seviyesinde nabız atıyor.
            UpdateLineVisual();

            return;
        }

        _checkTimer -= Time.deltaTime;
        if (_checkTimer <= 0f)
        {
            _checkTimer = _config.gameOverCheckInterval;
            _fillRatio  = ComputeFillRatio();

            if (HasViolation()) _violationTimer += _config.gameOverCheckInterval;
            else                _violationTimer  = 0f;

            if (_violationTimer >= _config.gameOverDelay)
            {
                _fired = true;
                GameEvents.RaiseGameOver(ScoreSystem.Instance != null ? ScoreSystem.Instance.Score : 0);
                SetLineAlpha(0f);
                return;
            }
            
        }
        
        UpdateLineVisual();
        
    }
    
    bool HasViolation()
    {
        float lineY = transform.position.y;
        var fruits = _pool.Active;

        for (int i = 0; i < fruits.Count; i++)
        {
            Fruit f = fruits[i];
            if (f == null) continue;
            if (!f.IsDropped) continue;
            if (f.IsMerging) continue;

            // FruitDefinition.countForGameOver şimdiye kadar hiçbir yerde OKUNMUYORDU:
            // alanı kapatan biri "bu meyve oyunu bitirmez" sanıyor ama hiçbir etkisi
            // olmuyordu. Varsayılan true olduğu için mevcut davranış değişmiyor.
            if (f.Definition != null && !f.Definition.countForGameOver) continue;

            if (Time.time - f.DropTime < _config.dropGracePeriod) continue;
            if (f.transform.position.y < lineY) continue;
            if (f.Body.linearVelocity.sqrMagnitude >
                _config.settleVelocityThreshold * _config.settleVelocityThreshold) continue;

            return true;
        }

        return false;
    }
    
    float ComputeFillRatio()
    {
        // Önbellekli property (bkz. FloorY): Collider2D.bounds native bir çağrı ve zemin
        // hiç hareket etmiyor. Buradaki kopya hesap o önbelleği boşa çıkarıyordu.
        float floorY = FloorY;
        float span   = transform.position.y - floorY;
        if (span <= 0.0001f) return 0f;

        float highest = floorY;
        var fruits = _pool.Active;
        for (int i = 0; i < fruits.Count; i++)
        {
            Fruit f = fruits[i];
            if (f == null || !f.IsDropped || f.IsMerging) continue;

            // az önce bırakılan meyveyi sayma — yerçekimi henüz hızlandırmadığı için
            // durgun sanılıp anlık olarak dropY yüksekliğinde doluluk hesaplanır
            if (Time.time - f.DropTime < _config.dropGracePeriod) continue;

            // havada olan meyveyi sayma — yoksa dropY (4.2) oranı > 1 yapar
            if (f.Body.linearVelocity.sqrMagnitude >
                _config.settleVelocityThreshold * _config.settleVelocityThreshold) continue;

            if (f.TopY > highest) highest = f.TopY;
        }

        return Mathf.Clamp01((highest - floorY) / span);
    }
    
    /// <summary>
    /// Çizginin görünürlüğü. İki kademe var:
    ///
    ///  - <b>boşta</b> — yığın <c>dangerShowRatio</c>'nun altında: çizgi sabit ve soluk
    ///    duruyor. Eskiden tamamen kaybolurdu ve oyuncu sınırı ancak yığın oraya
    ///    dayandığında öğreniyordu; artık nereye kadar yığabileceğini baştan görüyor.
    ///  - <b>tehlikede</b> — eşiğin üstünde: nabız atıyor. Hem hız hem tepe alpha yığın
    ///    yükseldikçe artıyor, yani "yaklaşıyor" ile "az kaldı" birbirinden ayırt ediliyor.
    ///
    /// Nabzın DİBİ boştaki alpha'nın altına inmiyor: eşiği geçtiği an çizgi bir kare
    /// için sönük hâlinden daha da soluklaşıp geri gelirdi, bu da titreme gibi görünürdü.
    /// </summary>
    void UpdateLineVisual()
    {
        float idle = _config.dangerIdleAlpha;
        float show = _config.dangerShowRatio;

        if (_fillRatio < show) { SetLineAlpha(idle); return; }

        float t     = Mathf.Clamp01((_fillRatio - show) / Mathf.Max(0.0001f, 1f - show));
        float hz    = Mathf.Lerp(_config.dangerBlinkHzMin, _config.dangerBlinkHzMax, t);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * hz * 2f * Mathf.PI);
        float peak  = Mathf.Lerp(_config.dangerMinAlpha, _config.dangerMaxAlpha, t);

        SetLineAlpha(Mathf.Lerp(Mathf.Max(idle, peak * 0.35f), peak, pulse));
    }

    void SetLineAlpha(float a)
    {
        if (_lineRenderer == null) return;
        Color c = _lineRenderer.color;
        if (Mathf.Approximately(c.a, a)) return;
        c.a = a;
        _lineRenderer.color = c;
    }
    

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        Vector3 p = transform.position;
        Gizmos.DrawLine(new Vector3(p.x - _lineHalfWidth, p.y, 0f),
                        new Vector3(p.x + _lineHalfWidth, p.y, 0f));
    }
#endif
}