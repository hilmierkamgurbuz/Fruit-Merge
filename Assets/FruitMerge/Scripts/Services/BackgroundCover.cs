using UnityEngine;

/// <summary>
/// Arka plan sprite'ını, kameranın o anki kadrajını HER ZAMAN kaplayacak kadar ölçekler.
///
/// <see cref="CameraFit"/> uzun ekranlarda dikeyde fazladan alan açıyor. Arka plan
/// sprite'ı sabit boyutlu (dünyada y ∈ [-6.25, 11.97]) olduğu için 20:9'da altta ~0.1,
/// 21:9'da ~0.44 birimlik boyanmamış şerit kalıyordu — ekranın %1'i kadar ama gözle
/// görülür bir çizgi.
///
/// <b>Ölçek RAF ÇİZGİSİ etrafında yapılıyor</b> (<see cref="_pivotY"/>), sprite'ın kendi
/// merkezi etrafında değil: raf çizgisi zemin collider'ıyla hizalı duruyor, merkeze göre
/// ölçekleseydik meyvelerin üstünde durduğu zemin ile çizili raf birbirinden kayardı.
/// Görselin üstü ve altı düz renk olduğu için pivot etrafında gerilmek gözle fark
/// edilmiyor.
///
/// <b>Maliyet sıfır:</b> <c>Update</c> yok. <see cref="CameraFit.FrameChanged"/> olayına
/// abone — kadraj değişmedikçe (yani pratikte açılışta bir kez) hiç çalışmıyor.
///
/// Taban değerler <see cref="Reset"/>'te bir kez yakalanıyor. Çalışma anında okusaydık
/// kendi yazdığımız ölçeği taban sanıp her domain reload'da biraz daha büyürdük.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
[DefaultExecutionOrder(-105)]
public class BackgroundCover : MonoBehaviour
{
    [Tooltip("Kadrajı buradan okuyor.")]
    [SerializeField] CameraFit _cameraFit;

    [Tooltip("Ölçeğin sabit kalacağı yükseklik — görseldeki raf çizgisi. Zemin " +
             "collider'ının üst yüzeyiyle hizalı olmalı.")]
    [SerializeField] float _pivotY = -2.42f;

    [Tooltip("Kadrajın dışına taşacak emniyet payı (dünya birimi).")]
    [SerializeField] float _overscan = 0.1f;

    [Header("Taban değerler (bileşen eklenirken yakalanır)")]
    [SerializeField] Vector3 _basePosition = new Vector3(-0.13f, 2.86f, 0f);
    [SerializeField] Vector3 _baseScale    = Vector3.one;

    SpriteRenderer _renderer;

    float _appliedBottom = float.NaN;
    float _appliedTop    = float.NaN;

    void Reset()
    {
        _basePosition = transform.localPosition;
        _baseScale    = transform.localScale;
    }

    void OnEnable()
    {
        _renderer = GetComponent<SpriteRenderer>();

        if (_cameraFit != null) _cameraFit.FrameChanged += Apply;

        Apply();
    }

    void OnDisable()
    {
        if (_cameraFit != null) _cameraFit.FrameChanged -= Apply;
    }

    void OnValidate()
    {
        _appliedBottom = float.NaN;
        _appliedTop    = float.NaN;
    }

    void Apply()
    {
        if (_cameraFit == null || _renderer == null || _renderer.sprite == null) return;

        float viewBottom = _cameraFit.ViewBottom - _overscan;
        float viewTop    = _cameraFit.ViewTop    + _overscan;

        // CameraFit henüz hesaplamadıysa ikisi de 0 olur — anlamsız, bekle.
        if (Mathf.Approximately(viewBottom, viewTop)) return;

        if (viewBottom.Equals(_appliedBottom) && viewTop.Equals(_appliedTop)) return;

        _appliedBottom = viewBottom;
        _appliedTop    = viewTop;

        // Taban (ölçeksiz) sprite'ın dünya kenarları
        float extentY = _renderer.sprite.bounds.extents.y * _baseScale.y;

        float baseTop    = _basePosition.y + extentY;
        float baseBottom = _basePosition.y - extentY;

        // Pivot ile kenar arasındaki mesafe kaç katına çıkmalı
        float needDown = SafeRatio(_pivotY - viewBottom, _pivotY - baseBottom);
        float needUp   = SafeRatio(viewTop - _pivotY,    baseTop - _pivotY);

        float k = Mathf.Max(1f, needDown, needUp);

        transform.localScale = _baseScale * k;

        // Pivot etrafında ölçekleme: pivot yerinde kalır, gerisi k katı uzaklaşır.
        transform.localPosition = new Vector3(_basePosition.x * k,
                                              _pivotY + (_basePosition.y - _pivotY) * k,
                                              _basePosition.z);
    }

    /// <summary>Payda sıfıra yakınsa 1 döner — pivot kenarın üstündeyse bölme patlamasın.</summary>
    static float SafeRatio(float need, float have)
    {
        if (have <= 0.0001f) return 1f;

        return need / have;
    }
}
