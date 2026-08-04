using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Tek parmağın / farenin o KAREDEKİ hâli, tek yerden.
///
/// <see cref="DropController"/> ve <see cref="WormBoostDirector"/> aynı touch/mouse okuma
/// bloğunu birebir kopyalamıştı. İkisi de artık buradan besleniyor — bir backend farkı
/// çıktığında düzeltilecek tek bir yer var.
///
/// <b>Neden <see cref="IsOverUI"/> ayrı bir metot ve neden bu kadar uğraşıyor:</b>
/// Sahnedeki EventSystem yeni Input System'in <c>InputSystemUIInputModule</c>'ünü
/// kullanıyor, ama oynanış girdisi ESKİ <c>Input</c> API'sinden okunuyor (Active Input
/// Handling = Both). İki API'nin pointer numaraları aynı uzayda DEĞİL:
/// eski <c>Touch.fingerId</c> 0'dan başlıyor, yeni modülün <c>touchId</c>'si 1'den.
/// Üstelik modülün arama fonksiyonu <c>touchId != 0</c> şartı koyuyor, yani
/// <c>IsPointerOverGameObject(0)</c> — ilk parmak — cihazda HER ZAMAN false dönüyordu.
/// Sonuç: HUD butonuna basmak hem butonu tetikliyor hem de dünyaya girdi olarak sızıyordu.
///
/// Parametresiz aşırı yükleme "o anki pointer"a bakıyor ve iki backend'de de doğru
/// çalışıyor; asıl güvendiğimiz o. Parmak numaralı olanlar sadece yedek.
/// </summary>
public static class PointerInput
{
    /// <summary>Bu karede yeni bir dokunuş/tık BAŞLADI mı.</summary>
    public static bool Began { get { Sample(); return _began; } }

    /// <summary>Parmak/tuş şu an basılı mı.</summary>
    public static bool Held { get { Sample(); return _held; } }

    /// <summary>Bu karede BIRAKILDI mı.</summary>
    public static bool Released { get { Sample(); return _released; } }

    /// <summary>Ekran koordinatı.</summary>
    public static Vector2 Position { get { Sample(); return _position; } }

    /// <summary>Eski API'nin parmak numarası; fare ise -1.</summary>
    public static int FingerId { get { Sample(); return _fingerId; } }

    static int     _frame = -1;
    static bool    _began, _held, _released;
    static Vector2 _position;
    static int     _fingerId = -1;

    /// <summary>
    /// Kare başına BİR KEZ okur. Ham <c>Input</c> bir kare içinde değişmediği için
    /// çağrı sırası önemli değil — script execution order'ı farklı iki abone de
    /// aynı değerleri görüyor.
    /// </summary>
    static void Sample()
    {
        if (_frame == Time.frameCount) return;

        _frame = Time.frameCount;

        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);

            _position = t.position;
            _fingerId = t.fingerId;

            _began    = t.phase == TouchPhase.Began;
            _held     = t.phase == TouchPhase.Began || t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary;
            _released = t.phase == TouchPhase.Ended;
        }
        else
        {
            _position = Input.mousePosition;
            _fingerId = -1;

            _began    = Input.GetMouseButtonDown(0);
            _held     = Input.GetMouseButton(0);
            _released = Input.GetMouseButtonUp(0);
        }
    }

    /// <summary>Bu pointer şu an bir UI elemanının üstünde mi.</summary>
    public static bool IsOverUI()
    {
        EventSystem es = EventSystem.current;

        if (es == null) return false;

        // Asıl güvenilir yol: "o anki pointer" — her iki backend'de de doğru.
        if (es.IsPointerOverGameObject()) return true;

        int f = FingerId;

        if (f < 0) return false;

        // Yedek: eski fingerId (0'dan) ile yeni touchId (1'den) arasındaki kayma.
        return es.IsPointerOverGameObject(f) || es.IsPointerOverGameObject(f + 1);
    }

    // Domain reload kapalıyken statikler bir sonraki oturuma taşınmasın —
    // GameEvents.ResetStatics ve BoostGate.ResetStatics ile aynı desen.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _frame    = -1;
        _began    = false;
        _held     = false;
        _released = false;
        _position = default;
        _fingerId = -1;
    }
}
