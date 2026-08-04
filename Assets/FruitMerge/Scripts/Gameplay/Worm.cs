using UnityEngine;

/// <summary>
/// Tek bir kurtçuk. Kafa + gövde halkaları + kuyruktan oluşan bir zincir.
///
/// Mimari — neden kare kare animasyon (flipbook) YOK:
///  - Zincir, <b>tek bir yol fonksiyonunun farklı yay uzunluklarında örneklenmesiyle</b>
///    diziliyor: kafa s'de ise i. halka s − (i × aralık)'ta. Halka geçmişini saklayan
///    bir tampon (snake trail) bile gerekmiyor, dolayısıyla allocation da yok.
///  - Sürünme, halka <b>aralığına</b> uygulanan yürüyen bir sinüs dalgasından geliyor:
///    gövde sıkışıp açılıyor — tırtıl yürüyüşünün kendisi. Ekstra sprite yok.
///  - Yön değişimi <see cref="SpriteRenderer.flipX"/>; soldan gelen kurt için ayrı asset yok.
///
/// <see cref="Update"/> YOK (kural 7) — <see cref="WormBoostDirector"/> tek Update'inde
/// <see cref="Tick"/> çağırıyor. Coroutine de yok (kural 8), her şey float sayaç.
/// </summary>
public class Worm : MonoBehaviour
{
    enum Phase { Approach, Eat, Leave, Done }

    public bool IsDone => _phase == Phase.Done;

    /// <summary>Kurdun kafasının o anki dünya konumu — kırıntıları buradan saçıyoruz.</summary>
    public Vector2 HeadPosition => _segments != null && _segments.Length > 0
        ? (Vector2)_segments[0].position
        : Vector2.zero;

    /// <summary>Ağzın açık olduğu kare — yeni ısırık tam bu anda başlıyor.</summary>
    public bool JustBit { get; private set; }

    GameConfig _config;

    Transform[]      _segments;
    SpriteRenderer[] _renderers;

    Sprite _spHeadIdle, _spHeadOpen, _spHeadFull, _spBody, _spBodyFat, _spTail;

    // ---- yol -------------------------------------------------------------
    float _dir;                          // +1 sağa gidiyor, -1 sola
    float _xStart, _xTarget, _xEnd;
    float _yStart, _ySlot,   _yEnd;
    float _sTarget, _sEnd;               // yay uzunlukları (yatay mesafe)
    float _wobbleFreq, _wobblePhase, _wobbleAmp, _wobbleDampSigma;

    // ---- ölçü ------------------------------------------------------------
    float _diameter, _spacing, _length;

    // ---- zaman -----------------------------------------------------------
    Phase _phase;
    float _time;
    float _approachDur, _eatDur, _leaveDur;
    float _waveT;
    float _chewTimer;
    bool  _mouthOpen;

    // ---- hedef -----------------------------------------------------------
    Transform _fruit;
    Vector2   _fruitAtArrival;
    Vector2   _fruitDelta;

    Sprite _currentHead;
    bool   _fatApplied;
    bool   _mealDone;

    /// <summary>
    /// Halkaları bir kez yaratır. Havuz ön ısıtmasında çağrılır — oynanış sırasında
    /// hiçbir şey Instantiate edilmez (kural 13).
    /// </summary>
    public void Build(GameConfig config, int segmentCount, int sortingBase,
                      Sprite head, Sprite headOpen, Sprite headFull,
                      Sprite body, Sprite bodyFat, Sprite tail)
    {
        _config = config;

        _spHeadIdle = head;
        _spHeadOpen = headOpen != null ? headOpen : head;
        _spHeadFull = headFull != null ? headFull : head;
        _spBody     = body;
        _spBodyFat  = bodyFat != null ? bodyFat : body;
        _spTail     = tail != null ? tail : body;

        int n = Mathf.Max(2, segmentCount);

        _segments  = new Transform[n];
        _renderers = new SpriteRenderer[n];

        for (int i = 0; i < n; i++)
        {
            var go = new GameObject(i == 0 ? "Head" : (i == n - 1 ? "Tail" : "Body" + i));

            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();

            sr.sprite = i == 0 ? _spHeadIdle : (i == n - 1 ? _spTail : _spBody);

            // kafa en üstte, kuyruk en altta — halkalar birbirine binerken doğru sıra
            sr.sortingOrder = sortingBase + (n - i);

            _segments[i]  = go.transform;
            _renderers[i] = sr;
        }

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Bir sefer için kurdu hazırlar ve geliş fazını başlatır.
    /// </summary>
    /// <param name="fruit">yenecek meyvenin transform'u</param>
    /// <param name="fromLeft">soldan mı geliyor</param>
    /// <param name="slotAngleDeg">meyvenin çevresinde hangi açıya yapışacak</param>
    /// <param name="fruitRadius">meyvenin dünya yarıçapı</param>
    /// <param name="laneOffsetY">aynı taraftaki kurtların dikey ayrımı</param>
    /// <param name="edgeX">ekranın yarı genişliği</param>
    /// <param name="phase01">bu kurdun sürünme dalgasındaki faz farkı</param>
    public void Configure(Transform fruit, bool fromLeft, float slotAngleDeg, float fruitRadius,
                          float laneOffsetY, float edgeX, float phase01)
    {
        _fruit          = fruit;
        _fruitAtArrival = fruit != null ? (Vector2)fruit.position : Vector2.zero;
        _fruitDelta     = Vector2.zero;

        _dir = fromLeft ? 1f : -1f;

        _diameter = Mathf.Clamp(fruitRadius * _config.wormSizeFactor,
                                _config.wormSizeMin, _config.wormSizeMax);

        _spacing = _diameter * _config.wormSegmentSpacing;
        _length  = _diameter + _spacing * (_segments.Length - 1);

        float rad = slotAngleDeg * Mathf.Deg2Rad;

        // halka meyvenin kenarına otursun: merkez, yarıçap + halkanın yarısı kadar dışarıda
        float slotR = fruitRadius + _diameter * 0.35f;

        Vector2 slot = _fruitAtArrival + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * slotR;

        _xTarget = slot.x;
        _ySlot   = slot.y;

        float margin = _config.wormSpawnMarginX + _length;

        _xStart = fromLeft ? -(edgeX + margin) :  (edgeX + margin);
        _xEnd   = fromLeft ?  (edgeX + margin) : -(edgeX + margin);

        _yStart = _ySlot + laneOffsetY;
        _yEnd   = _ySlot + laneOffsetY * 0.6f;

        _sTarget = Mathf.Abs(_xTarget - _xStart);
        _sEnd    = Mathf.Abs(_xEnd    - _xStart);

        _wobbleAmp       = _config.wormPathWobble;
        _wobbleFreq      = 2f * Mathf.PI / Mathf.Max(0.25f, _length * 1.2f);
        _wobblePhase     = phase01 * Mathf.PI * 2f;
        _wobbleDampSigma = Mathf.Max(0.15f, _length * 0.6f);

        _approachDur = Mathf.Max(0.05f, _config.wormApproachDuration);
        _eatDur      = Mathf.Max(0.05f, _config.wormEatDuration);
        _leaveDur    = Mathf.Max(0.05f, _config.wormLeaveDuration);

        _phase      = Phase.Approach;
        _time       = 0f;
        _waveT      = phase01 * Mathf.PI * 2f;
        _chewTimer  = 0f;
        _mouthOpen  = false;
        _fatApplied = false;
        _mealDone   = false;
        JustBit     = false;

        // Halka ölçeği kurdun ömrü boyunca SABİT (_diameter yukarıda hesaplandı, taper
        // yalnızca indekse bağlı) — eskiden ApplySegment her karede, halka başına yeniden
        // yazıyordu. 6 kurt × 5 halka = kare başına 30 gereksiz transform yazımı (kural 9).
        for (int i = 0; i < _segments.Length; i++)
        {
            float taper = 1f - 0.05f * i;

            _segments[i].localScale = new Vector3(_diameter * taper, _diameter * taper, 1f);
        }

        ApplyHeadSprite(_spHeadIdle);

        // force: havuzdan gelen kurt önceki seferin şişmiş gövdesiyle dönmesin
        ApplyBodySprites(false, true);

        gameObject.SetActive(true);

        Place(0f);
    }

    /// <summary>
    /// Meyve bitti. Kurt ÇİĞNEMEYİ BIRAKIR — yoksa sis dağılana kadar boşluğu ısırmaya
    /// devam eder ve "yenecek bir şey kalmadı" hissi kaçar. Bundan sonrası sadece
    /// tok tok kıpırdanmak; ağız kapanır, gövde şişer.
    /// </summary>
    public void FinishMeal()
    {
        _fruit    = null;
        _mealDone = true;

        _mouthOpen = false;

        ApplyHeadSprite(_spHeadFull);
        ApplyBodySprites(true);
    }

    public void Deactivate()
    {
        _fruit = null;
        _phase = Phase.Done;

        gameObject.SetActive(false);
    }

    public void Tick(float dt)
    {
        if (_phase == Phase.Done) return;

        JustBit = false;

        _time  += dt;
        _waveT += dt * _config.wormWaveSpeed;

        // meyve fizikte hâlâ hareket ediyorsa kurtlar onunla birlikte kaysın
        if (_fruit != null) _fruitDelta = (Vector2)_fruit.position - _fruitAtArrival;

        float s;

        switch (_phase)
        {
            case Phase.Approach:
            {
                float u = Mathf.Clamp01(_time / _approachDur);

                s = Mathf.Lerp(0f, _sTarget, EaseInOut(u));

                if (u >= 1f) { _phase = Phase.Eat; _time = 0f; }

                break;
            }

            case Phase.Eat:
            {
                s = _sTarget;

                TickChew(dt);

                if (_time >= _eatDur)
                {
                    _phase = Phase.Leave;
                    _time  = 0f;

                    ApplyHeadSprite(_spHeadFull);
                    ApplyBodySprites(true);
                }

                break;
            }

            default:
            {
                float u = Mathf.Clamp01(_time / _leaveDur);

                s = Mathf.Lerp(_sTarget, _sEnd, EaseIn(u));

                if (u >= 1f)
                {
                    _phase = Phase.Done;

                    gameObject.SetActive(false);

                    return;
                }

                break;
            }
        }

        Place(s);
    }

    // ------------------------------------------------------------- çiğneme

    void TickChew(float dt)
    {
        if (_mealDone) return;

        _chewTimer -= dt;

        if (_chewTimer > 0f) return;

        _mouthOpen = !_mouthOpen;

        // ağız açık kalma süresi kapalıdan kısa — "kap, çiğne" ritmi
        _chewTimer = _mouthOpen ? 0.11f : 0.17f;

        ApplyHeadSprite(_mouthOpen ? _spHeadOpen : _spHeadIdle);

        JustBit = _mouthOpen;
    }

    // ------------------------------------------------------------- yerleşim

    void Place(float s)
    {
        int n = _segments.Length;

        float acc = 0f;

        Vector2 prev = Vector2.zero;

        for (int i = 0; i < n; i++)
        {
            if (i > 0)
            {
                // yürüyen sıkışma dalgası: gövde açılıp kapanıyor
                float wave = Mathf.Sin(_waveT - i * _config.wormWavePhasePerSegment);

                acc += _spacing * (1f + _config.wormWaveAmplitude * wave);
            }

            float si = s - acc;

            Vector2 p = PathAt(si);

            // Yön HER ZAMAN hamlesiz konumdan hesaplanıyor. Hamle (0.16 × çap) yolun
            // 0.05'lik örnekleme adımından büyük olabiliyor; hamleli konumdan
            // hesaplanınca ileri vektörü işaret değiştirip kafayı ters çeviriyordu.
            Vector2 forward = i == 0 ? PathAt(si + 0.05f) - p : prev - p;

            Vector2 draw = p;

            // ısırık hamlesi: ağız açıkken kafa meyveye doğru biraz atılıyor
            if (i == 0 && _phase == Phase.Eat && _mouthOpen)
                draw += new Vector2(_dir, 0f) * (_diameter * 0.16f);

            ApplySegment(i, draw, forward, n);

            prev = p;
        }
    }

    void ApplySegment(int i, Vector2 pos, Vector2 forward, int n)
    {
        Transform t = _segments[i];

        t.localPosition = pos;

        // localScale burada YAZILMIYOR: sabit olduğu için Configure'da bir kez ayarlanıyor
        // (kuyruğa doğru incelme dahil).

        if (forward.sqrMagnitude < 1e-6f) return;

        var sr = _renderers[i];

        bool flip = forward.x < 0f;

        if (sr.flipX != flip) sr.flipX = flip;

        float ang = flip
            ? Mathf.Atan2(-forward.y, -forward.x) * Mathf.Rad2Deg
            : Mathf.Atan2( forward.y,  forward.x) * Mathf.Rad2Deg;

        t.localRotation = Quaternion.Euler(0f, 0f, ang);
    }

    /// <summary>
    /// Yolun s yay uzunluğundaki noktası. Zincirin TAMAMI bu tek fonksiyondan
    /// besleniyor — kafa s'de, i. halka s − aralık×i'de.
    /// </summary>
    Vector2 PathAt(float s)
    {
        float x = _xStart + _dir * s;

        float y;

        if (s <= _sTarget)
        {
            float u = _sTarget > 0.0001f ? s / _sTarget : 1f;

            y = Mathf.Lerp(_yStart, _ySlot, EaseInOut(Mathf.Clamp01(u)));
        }
        else
        {
            float v = (s - _sTarget) / Mathf.Max(0.0001f, _sEnd - _sTarget);

            y = Mathf.Lerp(_ySlot, _yEnd, EaseInOut(Mathf.Clamp01(v)));
        }

        // Yılan gibi salınım. Hedefin çevresinde SÖNÜYOR, yoksa kafa meyvenin
        // yanına değil salınımın tepesine otururdu.
        float d = (s - _sTarget) / _wobbleDampSigma;

        float damp = 1f - Mathf.Exp(-d * d);

        y += Mathf.Sin(s * _wobbleFreq + _wobblePhase) * _wobbleAmp * damp;

        return new Vector2(x, y) + _fruitDelta;
    }

    // ------------------------------------------------------------- sprite

    void ApplyHeadSprite(Sprite s)
    {
        if (s == null || _currentHead == s) return;   // kural 9: sadece değişince ata

        _currentHead = s;

        _renderers[0].sprite = s;
    }

    void ApplyBodySprites(bool fat, bool force = false)
    {
        if (!force && _fatApplied == fat) return;

        _fatApplied = fat;

        int n = _segments.Length;

        // sadece kafaya en yakın iki halka şişsin — karnı tıka basa dolmuş gibi
        for (int i = 1; i < n - 1 && i <= 2; i++)
            _renderers[i].sprite = fat ? _spBodyFat : _spBody;
    }

    static float EaseInOut(float u) => u * u * (3f - 2f * u);

    static float EaseIn(float u) => u * u;
}
