using UnityEngine;

/// <summary>
/// <b>Tasarım çerçevesini</b> her cihazda eksiksiz gösterir. Oyun dünyası hiç değişmiyor;
/// değişen tek şey, ekranın artakalan kısmının ne kadar olduğu.
///
/// <b>Neden bu kural — adalet:</b> tahtayı ekrana göre büyütmek/küçültmek, iki cihazda
/// oynayan iki oyuncuya FARKLI oyun verir: birinde havuz daha çok meyve alır, skor
/// tavanı yükselir, meyveler tahtaya oranla küçük kalır. Bu yüzden tahta dünya
/// koordinatında SABİT; kamera ona uyum sağlıyor, tersi değil.
///
/// <code>
/// orthographicSize = max(designHalfHeight, designHalfWidth / aspect)
/// </code>
///
/// İki durumdan biri oluyor:
///  - <b>Dar/uzun ekran</b> (telefonlar, aspect &lt; 0.56): genişlik belirleyici.
///    <c>orthographicSize × aspect = designHalfWidth</c> tam olarak tuttuğu için tahta
///    ekranın genişliğini TAM doldurur — hangi telefon olursa olsun meyveler ekranın
///    aynı oranını kaplar. Artan yer dikeyde kalır ve UI'a gider.
///  - <b>Geniş ekran</b> (tablet, aspect &gt; 0.56): yükseklik belirleyici, tahtanın
///    iki yanında boşluk kalır. Oynanış yine birebir aynı.
///
/// Üst sınır (max ortho) BİLEREK yok: sınır devreye girse tasarım çerçevesi kırpılır ve
/// yukarıdaki garanti bozulurdu. Açılan boşluğun boyanması <see cref="BackgroundCover"/>'ın
/// işi.
///
/// <b>Referans değerler neden kameradan OKUNMUYOR da alanda tutuluyor:</b> bileşen
/// <c>ExecuteAlways</c> ile edit mode'da da çalışıyor ve kameranın boyutunu yazıyor.
/// Referansı her açılışta kameradan okusaydık, kendi yazdığımız değeri referans sanıp
/// her domain reload'da biraz daha uzaklaşırdık. <see cref="Reset"/> bu değerleri
/// bileşen İLK EKLENDİĞİNDE bir kez yakalıyor — sonrasında sabit.
///
/// <b>Maliyet:</b> kare başına iki float karşılaştırması. Oran ve hedef değişmedikçe
/// hiçbir şey yazılmıyor, allocation yok.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(-110)]
public class CameraFit : MonoBehaviour
{
    [Tooltip("Tasarım çerçevesinin yatay yarı-genişliği buradan okunuyor: wallInnerX. " +
             "DropController da aynı değeri kullanıyor, ikisi ayrışmasın diye tek kaynak.")]
    [SerializeField] GameConfig _config;

    [Tooltip("Duvarın iç yüzü ile ekran kenarı arasında bırakılacak pay (dünya birimi). " +
             "0 = tam çakışsın. Bu değeri değiştirmek OYUNU DEĞİŞTİRMEZ, sadece kadrajı " +
             "biraz açar; her cihazda aynı uygulandığı için adalet bozulmaz.")]
    [SerializeField] float _edgePadding;

    [Header("Tasarım çerçevesi (bileşen eklenirken kameradan yakalanır)")]
    [Tooltip("Çerçevenin dikey yarı-yüksekliği. Oyunun tasarlandığı 9:16 kadrajın " +
             "orthographicSize'ı. Hiçbir ekranda bundan azı gösterilmiyor.")]
    [SerializeField] float _baseOrthoSize = 5.5f;

    [Tooltip("Çerçevenin dikey merkezi (kamera Y'si).")]
    [SerializeField] float _baseCameraY = 0.5f;

    [Tooltip("Uzun ekranda AÇILAN fazla dikey alan nereye gitsin: 0 = hepsi yukarı, " +
             "0.5 = eşit bölünür, 1 = hepsi aşağı. Bu alan oynanışa değil UI'a ait — " +
             "0.5'te hem üstteki HUD hem alttaki boost/zincir şeridi pay alıyor.")]
    [Range(0f, 1f)]
    [SerializeField] float _verticalBias = 0.5f;

    /// <summary>Kameranın o anki görünür dikey aralığı. <see cref="BackgroundCover"/> okuyor.</summary>
    public float ViewBottom { get; private set; }
    public float ViewTop    { get; private set; }

    /// <summary>
    /// Kameranın 9:16 tasarım kadrajına göre ne kadar uzaklaştığı (9:16'da 1, 20:9'da
    /// ~1.24). HUD'u bu orana bağlamak İSTENMEDİ — HUD'un büyümesi CanvasScaler'ın
    /// Match=1 ayarından geliyor, yani ekran yüksekliğinden. Bu değer teşhis/ayar için
    /// açıkta duruyor.
    /// </summary>
    public float ZoomOut { get; private set; } = 1f;

    /// <summary>Kadraj her değiştiğinde bir kez tetiklenir — her karede değil.</summary>
    public event System.Action FrameChanged;

    Camera _camera;

    float _appliedAspect = -1f;
    float _appliedTarget = -1f;

    bool _warned;

    /// <summary>Bileşen ilk eklendiğinde (ve Inspector'daki Reset'te) çerçeveyi yakalar.</summary>
    void Reset()
    {
        Camera cam = GetComponent<Camera>();

        if (cam != null && cam.orthographic) _baseOrthoSize = cam.orthographicSize;

        _baseCameraY = transform.localPosition.y;
    }

    void OnEnable()
    {
        _camera = GetComponent<Camera>();

        Invalidate();

        Apply();
    }

    // Yönelim/çözünürlük değişimini yakalamanın olay tabanlı güvenilir bir yolu yok.
    void Update() => Apply();

    void OnValidate() => Invalidate();

    void Invalidate()
    {
        _appliedAspect = -1f;
        _appliedTarget = -1f;
    }

    void Apply()
    {
        if (_camera == null) _camera = GetComponent<Camera>();

        if (_camera == null || !_camera.orthographic) return;

        if (_config == null)
        {
            if (!_warned)
            {
                _warned = true;

                Debug.LogWarning("CameraFit: GameConfig bağlı değil — tasarım çerçevesinin " +
                                 "genişliği okunamıyor, kadraj olduğu gibi bırakılıyor.", this);
            }

            return;
        }

        float aspect = _camera.aspect;

        if (aspect <= 0f) return;

        float target = _config.wallInnerX + _edgePadding;

        if (target <= 0f) return;

        if (Mathf.Approximately(aspect, _appliedAspect) &&
            Mathf.Approximately(target, _appliedTarget)) return;

        _appliedAspect = aspect;
        _appliedTarget = target;

        // Çerçevenin TAMAMI sığsın: genişlik ya da yükseklik, hangisi daha çok yer
        // istiyorsa o belirliyor. Kırpma yok — kırpmak adaleti bozardı.
        float size = Mathf.Max(_baseOrthoSize, target / aspect);

        _camera.orthographicSize = size;

        float extra = size - _baseOrthoSize;

        Vector3 p = transform.localPosition;

        p.y = _baseCameraY + extra * (1f - 2f * _verticalBias);

        transform.localPosition = p;

        ViewBottom = p.y - size;
        ViewTop    = p.y + size;

        ZoomOut = size / _baseOrthoSize;

        // Sarsıntı bitince kamera dinlenme konumuna BİREBİR geri yazılıyor; o konumu
        // güncellemezsek deprem sonrası kamera eski kadraja sıçrardı.
        if (Application.isPlaying && CameraShaker.Instance != null)
            CameraShaker.Instance.SetRest(p);

        FrameChanged?.Invoke();
    }
}
