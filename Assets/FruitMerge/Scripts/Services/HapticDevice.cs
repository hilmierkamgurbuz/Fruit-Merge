using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Titreşim motorunun PLATFORM katmanı. Tek işi: "şu şiddette, şu kadar süre titret".
/// Hiçbir oyun kuralı bilmiyor — ne zaman titreneceğine <see cref="HapticService"/> karar
/// veriyor, burası sadece cihazla konuşuyor.
///
/// <b>Neden <c>Handheld.Vibrate()</c> değil?</b> Unity'nin o çağrısı tek bir sabit
/// titreşim üretiyor: Android'de ~500 ms tam güç (telefon elinde zıplıyor), iOS'ta eski
/// tarz sistem titreşimi. Şiddet yok, süre yok, kademe yok. Bir merge oyununda hissedilmesi
/// gereken şey "hafif tık" ile "tok darbe" arasındaki FARK, o yüzden iki platformun kendi
/// haptic API'sine iniyoruz.
///
/// <b>Android:</b> <c>android.os.Vibrator</c> + <c>VibrationEffect.createOneShot(ms, amplitude)</c>
/// (API 26+). Genlik desteği olmayan eski cihazda genlik yerine SÜRE ölçekleniyor —
/// hissedilen şiddetin tek kaldığı kaldıraç o.
///
/// <b>iOS:</b> Taptic Engine'i saran küçük bir native eklenti
/// (<c>Assets/Plugins/iOS/FruitMergeHaptics.mm</c>) — <c>UIImpactFeedbackGenerator</c>.
/// iOS'ta darbe süresi AYARLANAMAZ (Taptic transient'leri sabit uzunlukta), o yüzden süre
/// parametresi orada sadece hafif/orta/sert seçimine katkı veriyor. "Uzun" hisler iki
/// platformda da darbe DİZİSİ ile üretiliyor (bkz. <see cref="HapticService"/>).
///
/// Editör'de hiçbir şey yapmıyor: masaüstünde motor yok. Kancaların doğru yerde tetiklendiği
/// <c>GameConfig.hapticEditorLog</c> ile konsoldan izleniyor.
/// </summary>
public static class HapticDevice
{
    /// <summary>Cihazda titreşim motoru var VE erişebildik mi. Editör'de her zaman false.</summary>
    public static bool IsAvailable { get; private set; }

    /// <summary>
    /// Şiddet kademelendirilebiliyor mu. Android'de <c>hasAmplitudeControl()</c>, iOS'ta
    /// her zaman true (hafif/orta/sert üç ayrı jeneratör). Kademesiz cihazda
    /// <see cref="HapticService"/>'in yapacağı bir şey yok — süre telafisi burada yapılıyor.
    /// </summary>
    public static bool HasIntensityControl { get; private set; }

    /// <summary>Motorun anlamlı şekilde tetiklenebildiği en kısa süre (sn).</summary>
    const float MinDuration = 0.008f;

    /// <summary>Tek bir darbenin üst sınırı (sn) — kod hatası telefonu saniyelerce titretmesin.</summary>
    const float MaxDuration = 1.5f;

    static bool _initialized;

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>
    /// <c>Vibrator</c> servisi. Global JNI referansı olduğu için BİR KEZ alınıp saklanıyor:
    /// her darbede <c>getSystemService</c> çağırmak hem yavaş hem çöp üretir.
    /// </summary>
    static AndroidJavaObject _vibrator;

    /// <summary><c>android.os.VibrationEffect</c> — statik fabrika. API 26 altında null kalır.</summary>
    static AndroidJavaClass _effectClass;
#endif

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] static extern bool FruitMergeHapticsInit();

    [DllImport("__Internal")] static extern void FruitMergeHapticsImpact(int style, float intensity);

    [DllImport("__Internal")] static extern void FruitMergeHapticsRelease();
#endif

    /// <summary>
    /// Cihazı hazırlar. İki kez çağrılması zararsız. Pahalı olan kısım (JNI servis araması,
    /// iOS jeneratörlerinin ısıtılması) yalnızca ilk çağrıda oluyor.
    /// </summary>
    public static void Init()
    {
        if (_initialized) return;

        _initialized = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        InitAndroid();
#elif UNITY_IOS && !UNITY_EDITOR
        IsAvailable = FruitMergeHapticsInit();

        // iOS'ta üç ayrı darbe sınıfı var — kademe her zaman mevcut.
        HasIntensityControl = IsAvailable;
#else
        IsAvailable         = false;
        HasIntensityControl = false;
#endif
    }

    /// <summary>
    /// Tek darbe. <paramref name="intensity01"/> 0-1 şiddet, <paramref name="duration"/> saniye.
    /// Şiddet 0 veya cihaz yoksa hiçbir şey yapmaz — çağıran tarafın kontrol etmesi gerekmiyor.
    /// </summary>
    public static void Pulse(float intensity01, float duration)
    {
        if (!IsAvailable) return;

        intensity01 = Mathf.Clamp01(intensity01);

        if (intensity01 <= 0.001f) return;

        duration = Mathf.Clamp(duration, MinDuration, MaxDuration);

#if UNITY_ANDROID && !UNITY_EDITOR
        PulseAndroid(intensity01, duration);
#elif UNITY_IOS && !UNITY_EDITOR
        // Süre iOS'ta uygulanamıyor ama BİLGİ taşıyor: uzun istenen darbe sert olsun.
        // Böylece iki platform aynı çağrıdan benzer bir hikâye çıkarıyor.
        float weight = Mathf.Clamp01(intensity01 * 0.75f + Mathf.Clamp01(duration / 0.2f) * 0.25f);

        int style = weight < 0.34f ? 0 : (weight < 0.67f ? 1 : 2);

        FruitMergeHapticsImpact(style, Mathf.Max(0.05f, intensity01));
#endif
    }

    /// <summary>
    /// Süren titreşimi anında kes. Uygulama arka plana giderken ve boost yarısında iptal
    /// olurken çağrılıyor — telefon elde titrerken kalmasın.
    /// </summary>
    public static void Cancel()
    {
        if (!IsAvailable) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (_vibrator != null) _vibrator.Call("cancel");
#endif
        // iOS'ta kesilecek bir şey yok: Taptic darbeleri milisaniyelik transient'ler.
    }

    /// <summary>JNI/native kaynaklarını bırakır. Servis yok edilirken çağrılıyor.</summary>
    public static void Shutdown()
    {
        if (!_initialized) return;

        Cancel();

#if UNITY_ANDROID && !UNITY_EDITOR
        _effectClass?.Dispose();
        _effectClass = null;

        _vibrator?.Dispose();
        _vibrator = null;
#elif UNITY_IOS && !UNITY_EDITOR
        FruitMergeHapticsRelease();
#endif

        IsAvailable         = false;
        HasIntensityControl = false;
        _initialized        = false;
    }

    // ------------------------------------------------------------------- Android

#if UNITY_ANDROID && !UNITY_EDITOR
    static void InitAndroid()
    {
        try
        {
            int api;

            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                api = version.GetStatic<int>("SDK_INT");

            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                if (activity == null) return;

                if (api >= 31)
                {
                    // API 31'de Vibrator doğrudan alınamıyor (deprecated): önce VibratorManager,
                    // sonra ondan varsayılan motor.
                    using (var manager = activity.Call<AndroidJavaObject>("getSystemService",
                                                                          "vibrator_manager"))
                    {
                        if (manager != null)
                            _vibrator = manager.Call<AndroidJavaObject>("getDefaultVibrator");
                    }
                }
                else
                {
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
            }

            if (_vibrator == null) return;

            if (!_vibrator.Call<bool>("hasVibrator"))
            {
                _vibrator.Dispose();
                _vibrator = null;
                return;
            }

            if (api >= 26)
            {
                _effectClass = new AndroidJavaClass("android.os.VibrationEffect");

                HasIntensityControl = _vibrator.Call<bool>("hasAmplitudeControl");
            }

            IsAvailable = true;
        }
        catch (System.Exception e)
        {
            // Titreşim bir konfor özelliği: erişemiyorsak oyun sessizce titreşimsiz devam eder.
            Debug.LogWarning($"[HapticDevice] Android titreşim motoru açılamadı: {e.Message}");

            IsAvailable         = false;
            HasIntensityControl = false;
        }
    }

    static void PulseAndroid(float intensity01, float duration)
    {
        if (_vibrator == null) return;

        // Genlik kademesi YOKSA cihaz her darbeyi tam güçte veriyor; şiddet farkını sadece
        // süreden çıkarabiliyoruz. Zayıf istekleri kısaltmak (%40'a kadar) tam güç bir
        // "tık"ı hafif bir "tık"tan ayırmaya yetiyor.
        if (!HasIntensityControl) duration *= Mathf.Lerp(0.4f, 1f, intensity01);

        long ms = (long)Mathf.Max(1f, duration * 1000f);

        try
        {
            if (_effectClass != null)
            {
                // -1 = VibrationEffect.DEFAULT_AMPLITUDE (kademesiz cihazın kabul ettiği tek değer)
                int amplitude = HasIntensityControl
                    ? Mathf.Clamp(Mathf.RoundToInt(intensity01 * 255f), 1, 255)
                    : -1;

                // Her darbe yeni bir Java nesnesi üretiyor — using ile hemen bırakılıyor,
                // yoksa global JNI referansları birikiyor.
                using (var effect = _effectClass.CallStatic<AndroidJavaObject>(
                           "createOneShot", ms, amplitude))
                {
                    _vibrator.Call("vibrate", effect);
                }

                return;
            }

            // API 23-25: sadece süre var. Deprecated ama hâlâ çalışıyor.
            _vibrator.Call("vibrate", ms);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[HapticDevice] Titreşim başarısız, titreşim kapatılıyor: {e.Message}");

            IsAvailable = false;
        }
    }
#endif
}
