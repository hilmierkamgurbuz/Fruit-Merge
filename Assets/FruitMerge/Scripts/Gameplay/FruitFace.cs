using UnityEngine;

/// <summary>
/// Meyvenin yüzü. Meyve gövdesinin child'ı olarak duruyor.
///
/// <b>Update'i YOK.</b> FaceDirector tek Update'te <see cref="Tick"/> çağırıyor —
/// 60 meyve için 60 managed↔native geçişi yerine 1 (performans kuralı 7).
///
/// Yüz gövdeye SABİT: localRotation identity, yani meyve dönerken yüz de onunla döner.
/// Gözler sapın olduğu tarafta, ağız altta kalır. (Dünya uzayında dik tutmak yüzü
/// gövdenin çiziminden ayırıyordu.)
///
/// İfade değişimleri yumuşak: eski yüz sönüyor, ortada sprite değişiyor, yeni yüz
/// doluyor. Tek SpriteRenderer ile crossfade yapılamadığı için bu yol seçildi —
/// ikinci bir renderer meyve başına iki kat sprite demek olurdu.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class FruitFace : MonoBehaviour
{
    SpriteRenderer _sr;
    Transform _owner;

    FaceSet _set;
    FaceSize _size;
    Vector2 _baseOffset;

    float _lookRadius;
    float _lookSpeed;
    float _transitionDuration;

    FaceExpression _current;
    Sprite _currentSprite;

    // yumuşak geçiş
    FaceExpression _target;
    Sprite _targetSprite;
    bool _transitioning;
    float _transitionT;

    // meyve bazlı ifade kilidi (love gibi). Kilit süresince global mod bu yüzü ezemez.
    float _lockLeft;

    bool _hasLook;
    Vector2 _lookPoint;
    Vector2 _lookOffset;

    public bool IsLocked => _lockLeft > 0f;

    /// <summary>
    /// Danger line durumu. FaceDirector histerezis için buraya yazıyor — meyve başına
    /// state'i objenin kendisinde tutmak Dictionary aramasından da allocation'dan da kurtarıyor.
    /// </summary>
    public FaceDangerState DangerState { get; set; }

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _owner = transform.parent;
    }

    /// <summary>Meyve havuzdan çıkıp Initialize edilirken çağrılır.</summary>
    public void Bind(FaceSet set, FaceSize size, Vector2 offset, int sortingOrder,
                     Sprite bodySprite, GameConfig config)
    {
        _set = set;
        _size = size;
        _baseOffset = offset;

        _sr.sortingOrder = sortingOrder;

        _lookRadius         = config != null ? config.faceLookRadius : 0.08f;
        _lookSpeed          = config != null ? config.faceLookSpeed : 8f;
        _transitionDuration = config != null ? Mathf.Max(0.01f, config.faceTransitionDuration) : 0.14f;

        // Yüz sprite'ı ile gövde sprite'ı farklı çözünürlükte (gövde 470 px kırpılı,
        // yüz 512/256/128/64 tam tuval, hepsi ppu ~512). Yüzü gövdenin tuval genişliğine
        // normalize et — hangi boyut sınıfı olursa olsun oturur, çözünürlük de render
        // boyutuyla eşleşir.
        float scale = 1f;

        Sprite reference = _set != null ? _set.Get(FaceExpression.Idle, _size) : null;

        if (reference != null && bodySprite != null)
        {
            float faceW = reference.bounds.size.x;
            float bodyW = bodySprite.bounds.size.x;

            if (faceW > 0.0001f) scale = bodyW / faceW;
        }

        transform.localScale = Vector3.one * scale;

        ResetFace();
    }

    public void ResetFace()
    {
        _lockLeft = 0f;
        _hasLook = false;
        _lookOffset = Vector2.zero;

        DangerState = FaceDangerState.None;

        _transitioning = false;
        _transitionT = 0f;

        _current = FaceExpression.Idle;
        _target = FaceExpression.Idle;
        _currentSprite = null;
        _targetSprite = null;

        // gövdeye sabit: dönüşü parent'tan miras al
        transform.localRotation = Quaternion.identity;
        transform.localPosition = _baseOffset;

        SetAlpha(1f);

        ApplyInstant(FaceExpression.Idle);
    }

    /// <summary>Global moddan gelen ifade. Kilit varsa yok sayılır.</summary>
    public void SetExpression(FaceExpression expression)
    {
        if (_lockLeft > 0f) return;

        Apply(expression);
    }

    /// <summary>
    /// Belirli süre kilitlenen ifade — birleşmede <c>love</c>, oyun sonunda
    /// <c>dizzy</c>/<c>squish</c>. Global mod bu süre boyunca bu yüzü değiştiremez.
    /// </summary>
    public void Express(FaceExpression expression, float duration)
    {
        _lockLeft = duration;

        Apply(expression);
    }

    public void SetLookPoint(Vector2 worldPoint)
    {
        _hasLook = true;
        _lookPoint = worldPoint;
    }

    public void ClearLook() => _hasLook = false;

    public void Tick(float dt)
    {
        if (_lockLeft > 0f) _lockLeft -= dt;

        TickTransition(dt);

        TickLook(dt);
    }

    // ------------------------------------------------------------ yumuşak geçiş

    void TickTransition(float dt)
    {
        if (!_transitioning) return;

        _transitionT += dt / _transitionDuration;

        // ortada sprite'ı değiştir — alpha o an 0, değişim görünmez
        if (_transitionT >= 0.5f && _currentSprite != _targetSprite)
        {
            _currentSprite = _targetSprite;
            _current = _target;

            _sr.sprite = _targetSprite;
        }

        if (_transitionT >= 1f)
        {
            _transitioning = false;
            _transitionT = 0f;

            SetAlpha(1f);
            return;
        }

        // 0 -> 0.5 sönme, 0.5 -> 1 dolma
        float a = _transitionT < 0.5f
            ? 1f - _transitionT * 2f
            : (_transitionT - 0.5f) * 2f;

        SetAlpha(a);
    }

    void Apply(FaceExpression expression)
    {
        if (_set == null) { _current = expression; return; }

        // aynı hedefe tekrar tekrar çağrılıyor (director her tick çağırıyor) — yeniden başlatma
        if (_transitioning && expression == _target) return;
        if (!_transitioning && expression == _current) return;

        Sprite next = _set.Get(expression, _size);

        if (next == null) return;

        // ilk atama veya aynı sprite'a düşen farklı ifade: geçişe gerek yok
        if (_currentSprite == null)
        {
            ApplyInstant(expression);
            return;
        }

        if (next == _currentSprite)
        {
            _current = expression;
            return;
        }

        _target = expression;
        _targetSprite = next;
        _transitioning = true;
        _transitionT = 0f;
    }

    void ApplyInstant(FaceExpression expression)
    {
        _current = expression;
        _target = expression;

        if (_set == null) return;

        Sprite next = _set.Get(expression, _size);

        if (next == null || next == _currentSprite) return;

        _currentSprite = next;
        _targetSprite = next;

        _sr.sprite = next;
    }

    void SetAlpha(float a)
    {
        Color c = _sr.color;

        if (Mathf.Approximately(c.a, a)) return;

        c.a = a;
        _sr.color = c;
    }

    // ------------------------------------------------------------------- bakış

    /// <summary>
    /// Bakış hedefine oturma eşiği (dünya birimi²). <c>Lerp</c> hedefe asimptotik
    /// yaklaştığı için <c>_lookOffset</c> asla tam olarak <c>want</c>'a eşitlenmiyordu —
    /// bakış hedefi olmayan, durgun bir yüz için bile her karede <c>localPosition</c>
    /// yazılıyordu. 60 meyvede kare başına 60 gereksiz transform kirletme + bağlı
    /// <c>SpriteRenderer</c> bounds güncellemesi demekti.
    ///
    /// Eşik <c>faceLookRadius</c>'un (0.18) on binde biri, yani alt-piksel mertebesinde;
    /// gözle ayırt edilemiyor. Fark eşiğin üstüne çıktığı anda eski davranış birebir
    /// geri geliyor.
    /// </summary>
    const float LookSnapSqr = 1e-8f;

    void TickLook(float dt)
    {
        Vector2 want = Vector2.zero;

        if (_hasLook && _owner != null)
        {
            Vector2 delta = _lookPoint - (Vector2)_owner.position;

            if (delta.sqrMagnitude > 0.0001f)
            {
                // Yüz gövdeyle döndüğü için hedef yönünü gövdenin local frame'ine çevir,
                // yoksa dönen meyvede bakış yönü şaşar.
                Vector3 localDir = _owner.InverseTransformDirection(delta.normalized);

                Vector2 flat = new Vector2(localDir.x, localDir.y);

                if (flat.sqrMagnitude > 0.0001f) want = flat.normalized * _lookRadius;
            }
        }

        // Zaten hedefte: transform'a hiç dokunma (bkz. LookSnapSqr).
        if ((want - _lookOffset).sqrMagnitude <= LookSnapSqr)
        {
            if (_lookOffset != want)
            {
                _lookOffset = want;

                transform.localPosition = _baseOffset + _lookOffset;
            }

            return;
        }

        _lookOffset = Vector2.Lerp(_lookOffset, want, Mathf.Clamp01(dt * _lookSpeed));

        transform.localPosition = _baseOffset + _lookOffset;
    }
}
