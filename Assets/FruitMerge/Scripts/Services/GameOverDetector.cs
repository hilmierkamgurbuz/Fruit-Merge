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

    void OnEnable()  { GameEvents.OnStateChanged += HandleStateChanged; }
    void OnDisable() { GameEvents.OnStateChanged -= HandleStateChanged; }

    void HandleStateChanged(GameState s)
    {
        if (s == GameState.Playing) { _fired = false; _violationTimer = 0f; }
    }

    void Update()
    {
        bool playing = GameManager.Instance != null && GameManager.Instance.IsPlaying && !_fired;
        if (!playing) { SetLineAlpha(0f); return; }

        // Boost oynarken oyunu bitirme: kurtçuklar tam da yığını indirmek için çağrıldı,
        // hedef meyve yenirken sayacın dolması haksızlık olurdu.
        if (WormBoostDirector.Instance != null && WormBoostDirector.Instance.IsBusy)
        {
            _violationTimer = 0f;
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
        float floorY = _floor != null ? _floor.bounds.max.y : transform.position.y - 5f;
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
    
    void UpdateLineVisual()
    {
        float show = _config.dangerShowRatio;
        if (_fillRatio < show) { SetLineAlpha(0f); return; }

        float t     = Mathf.Clamp01((_fillRatio - show) / Mathf.Max(0.0001f, 1f - show));
        float hz    = Mathf.Lerp(_config.dangerBlinkHzMin, _config.dangerBlinkHzMax, t);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * hz * 2f * Mathf.PI);
        float peak  = Mathf.Lerp(_config.dangerMinAlpha, _config.dangerMaxAlpha, t);

        SetLineAlpha(Mathf.Lerp(peak * 0.35f, peak, pulse));
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