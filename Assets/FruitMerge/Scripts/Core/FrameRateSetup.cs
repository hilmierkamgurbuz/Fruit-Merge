using UnityEngine;

/// <summary>
/// Hedef kare hızını AÇIKÇA belirler.
///
/// <b>Neden gerekli:</b> proje hiçbir yerde <c>Application.targetFrameRate</c> yazmıyordu,
/// yani değer -1 — "platformun varsayılanı". Android'de bu tarihsel olarak <b>30 FPS</b>
/// demek (pil tasarrufu için). Telefonda oyunun ağır hissettirmesinin en olası sebebi bu.
///
/// <c>QualitySettings.vSyncCount</c> bu işi göremiyor: Android/iOS'ta yok sayılıyor.
/// Projedeki Android kalite seviyesi (Medium) <c>vSyncCount: 1</c> tanımlıyor ama cihazda
/// hiçbir etkisi yok — kare hızını yalnızca <c>targetFrameRate</c> belirliyor.
///
/// <b>Cihazın tazeleme hızına uyum:</b> 60 Hz telefonda 60, 120 Hz telefonda 120 istemek
/// yerine ekranın gerçek hızını okuyup <see cref="MaxFrameRate"/> ile sınırlıyoruz.
/// Böylece 90/120 Hz ekranlarda ekran hızına eşit olmayan bir hedef vermekten doğan
/// düzensiz kare aralığı (judder) oluşmuyor.
///
/// Player Settings'te <c>androidUseSwappy</c> (Optimized Frame Pacing) zaten açık —
/// Swappy bu hedefi düzgün aralıklarla dağıtıyor.
///
/// <b>Ayar:</b> üst sınır <see cref="MaxFrameRate"/>. 120 Hz ekranda 120, 90 Hz'de 90,
/// 60 Hz'de 60 çalışıyor — hedef her zaman ekranın gerçek hızına eşit, o yüzden kare
/// aralığı düzgün. Pil için geri çekmek istersen bu sabiti düşür.
/// </summary>
public static class FrameRateSetup
{
    /// <summary>Üst sınır. Cihaz daha hızlıysa buraya kırpılıyor.</summary>
    const int MaxFrameRate = 120;

    /// <summary>Ekran hızı okunamazsa kullanılacak değer.</summary>
    const int Fallback = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        // vSync açıkken targetFrameRate yok sayılıyor (masaüstü/editör). Mobilde zaten
        // etkisiz; ikisini birden yazmak davranışı her platformda aynı yapıyor.
        QualitySettings.vSyncCount = 0;

        int refresh = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);

        if (refresh <= 0) refresh = Fallback;

        Application.targetFrameRate = Mathf.Clamp(refresh, 30, MaxFrameRate);
    }
}
