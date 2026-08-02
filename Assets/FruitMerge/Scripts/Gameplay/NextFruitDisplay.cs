using UnityEngine;

/// <summary>
/// Dalın üst yuvasında sıradaki meyveyi (yüzüyle birlikte) gösterir ve bırakma anında
/// onu aşağıya, bekleyen meyvenin yerine devreder.
///
/// Akış:
///  1. <see cref="Show"/> — meyve yuvaya oturur, yumuşakça belirir
///  2. Oyuncu bırakır → <see cref="BeginHandoff"/> — meyve yuvadan aşağı kayar ve
///     aynı anda gerçek boyutuna BÜYÜR, yani bekleyen meyveye dönüşür
///  3. Gerçek bekleyen meyve doğduğunda <see cref="Show"/> yeniden çağrılır; bu obje
///     yuvaya geri sıçrar ve yeni sıradaki meyveyle belirir
///
/// Devir bittiğinde bu sprite gerçek meyveyle aynı yerde, aynı boyutta ve aynı
/// görüntüde olduğu için geçiş dikişsiz görünür.
///
/// Fizik yok — dekoratif iki SpriteRenderer (gövde + yüz).
/// Kendi Update'i var ama bu TEK bir obje; "tek Update" kuralı 60 meyvelik
/// bileşenler için geçerli, tek dekoratif obje için değil.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class NextFruitDisplay : MonoBehaviour
{
    enum State { Hidden, Idle, Handoff, FadeIn }

    [Header("Referanslar")]
    [SerializeField] GameConfig _config;

    [Tooltip("gövdenin üstüne çizilecek yüz. Boş bırakılırsa yüz gösterilmez")]
    [SerializeField] SpriteRenderer _faceRenderer;

    [Tooltip("12 ifade × 4 boyut tablosu — meyvelerdekiyle aynı asset")]
    [SerializeField] FaceSet _faceSet;

    [Header("Yerleşim (DropZone origin'ine göre local)")]
    [Tooltip("dalın üst yuvasının tabanı. Meyvenin ALT kenarı buraya oturur")]
    [SerializeField] float _cradleLocalY = 0.651f;

    [Tooltip("meyve yuvaya ne kadar gömülsün. 1 = tam üstünde durur, 0.8 = biraz içine oturmuş görünür")]
    [SerializeField] float _sitFactor = 0.8f;

    [Header("Ölçek")]
    [Tooltip("önizleme ölçeği. 1 = gerçek boyut. Dalın üstünde sınırlı yer var, " +
             "büyük meyveler gerçek boyutta HUD'a giriyor")]
    [SerializeField] float _previewScale = 0.5f;

    [Header("Geçiş süreleri")]
    [Tooltip("yuvadan aşağı kayma + büyüme süresi. GameConfig.pendingSpawnMaxWait'ten " +
             "KÜÇÜK tut, yoksa gerçek meyve devir bitmeden doğar ve zıplama görünür")]
    [SerializeField] float _handoffDuration = 0.35f;

    [Tooltip("yeni sıradaki meyvenin belirme süresi")]
    [SerializeField] float _fadeInDuration = 0.25f;

    [Header("Çizim")]
    [Tooltip("dalın (90) önünde, meyvelerin (96-100) arkasında")]
    [SerializeField] int _sortingOrder = 95;

    SpriteRenderer _sr;

    State _state = State.Hidden;
    float _t;

    float _idleY;
    float _idleScale;
    float _handoffY;
    float _handoffScale;
    Color _tint;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _sr.sortingOrder = _sortingOrder;

        if (_faceRenderer != null) _faceRenderer.sortingOrder = _sortingOrder + 1;

        SetVisible(false);
    }

    /// <summary>Sıradaki meyveyi yuvaya yerleştirip yumuşakça gösterir.</summary>
    public void Show(FruitDefinition def)
    {
        if (def == null || def.sprite == null)
        {
            SetVisible(false);
            _state = State.Hidden;
            return;
        }

        _sr.sprite = def.sprite;
        _tint = def.tint;

        float fullRadius = def.colliderRadius * def.scale;
        float previewRadius = fullRadius * _previewScale;

        // yuvadaki hali
        _idleScale = def.scale * _previewScale;
        _idleY = _cradleLocalY + previewRadius * _sitFactor;

        // devir sonundaki hali: gerçek boyut, tepesi sapın ucunda (DropController ile aynı kural)
        _handoffScale = def.scale;
        _handoffY = (_config != null ? _config.dropperTwigTipY : 0.25f) - fullRadius;

        transform.localScale = Vector3.one * _idleScale;
        transform.localPosition = new Vector3(0f, _idleY, 0f);

        ApplyFace(def);

        _sr.enabled = true;
        _state = State.FadeIn;
        _t = 0f;

        ApplyAlpha(0f);
    }

    /// <summary>
    /// Bırakma anında çağrılır: meyve yuvadan aşağı kayar ve gerçek boyutuna büyür.
    /// </summary>
    public void BeginHandoff()
    {
        if (_state == State.Hidden) return;

        _state = State.Handoff;
        _t = 0f;

        ApplyAlpha(1f);
    }

    public void Clear()
    {
        _state = State.Hidden;
        SetVisible(false);
    }

    void Update()
    {
        switch (_state)
        {
            case State.FadeIn:
                TickFadeIn();
                break;

            case State.Handoff:
                TickHandoff();
                break;
        }
    }

    void TickFadeIn()
    {
        _t += Time.deltaTime / Mathf.Max(0.01f, _fadeInDuration);

        if (_t >= 1f)
        {
            _t = 0f;
            _state = State.Idle;
            ApplyAlpha(1f);
            transform.localScale = Vector3.one * _idleScale;
            return;
        }

        float e = _t * _t * (3f - 2f * _t);   // smoothstep

        ApplyAlpha(e);

        transform.localScale = Vector3.one * (_idleScale * Mathf.Lerp(0.75f, 1f, e));
    }

    void TickHandoff()
    {
        _t += Time.deltaTime / Mathf.Max(0.01f, _handoffDuration);

        float e = Mathf.Clamp01(_t);
        float s = e * e * (3f - 2f * e);      // smoothstep

        transform.localPosition = new Vector3(0f, Mathf.Lerp(_idleY, _handoffY, s), 0f);

        transform.localScale = Vector3.one * Mathf.Lerp(_idleScale, _handoffScale, s);

        // Bittiğinde gizlemiyoruz: gerçek bekleyen meyve doğana kadar burada durup
        // onun yerini tutuyor. Show() çağrılınca yuvaya geri sıçrayacak.
        if (_t >= 1f) _state = State.Idle;
    }

    void ApplyFace(FruitDefinition def)
    {
        if (_faceRenderer == null) return;

        Sprite face = _faceSet != null ? _faceSet.Get(FaceExpression.Idle, def.faceSize) : null;

        if (face == null)
        {
            _faceRenderer.enabled = false;
            return;
        }

        _faceRenderer.enabled = true;
        _faceRenderer.sprite = face;

        // FruitFace ile aynı normalizasyon: yüzü gövdenin tuval genişliğine oturt
        float faceW = face.bounds.size.x;
        float bodyW = def.sprite.bounds.size.x;

        float scale = faceW > 0.0001f ? bodyW / faceW : 1f;

        _faceRenderer.transform.localScale = Vector3.one * scale;
        _faceRenderer.transform.localPosition = def.faceOffset;
        _faceRenderer.transform.localRotation = Quaternion.identity;
    }

    void SetVisible(bool visible)
    {
        _sr.enabled = visible;

        if (_faceRenderer != null) _faceRenderer.enabled = visible;

        if (!visible) return;

        ApplyAlpha(1f);
    }

    void ApplyAlpha(float a)
    {
        Color c = _tint;
        c.a = a;
        _sr.color = c;

        if (_faceRenderer == null) return;

        // yüz kendi renginde (beyaz tint), sadece alpha takip ediyor
        Color fc = Color.white;
        fc.a = a;
        _faceRenderer.color = fc;
    }
}
