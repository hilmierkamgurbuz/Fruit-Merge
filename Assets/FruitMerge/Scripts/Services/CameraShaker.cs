using UnityEngine;

/// <summary>
/// Kameranın sarsılması. Boost'tan bağımsız duruyor çünkü ileride merge darbesi, oyun sonu ve
/// diğer boost'lar da bunu kullanacak.
///
/// İki giriş var:
///  - <see cref="SetRumble"/> — SÜREKLİ sarsıntı. Çağıran her karede yeniden yazar; yazmayı
///    bırakınca sarsıntı kendiliğinden ölür (bkz. <see cref="_rumbleFrame"/>). Böylece bir
///    director çökse ya da <c>Abort</c>'u atlasa bile kamera sonsuza kadar titrer halde kalmıyor.
///  - <see cref="Punch"/> — TEK SEFERLİK sönen darbe. Kendi süresini kendi sayıyor.
///
/// Neden Perlin: <c>Random.insideUnitCircle</c> her karede bağımsız bir noktaya zıplıyor,
/// sonuç epileptik bir kırpışma oluyor. <see cref="Mathf.PerlinNoise"/> komşu kareler arasında
/// sürekli olduğu için gerçek bir sarsıntı gibi okunuyor. Ayrıca allocation yapmıyor.
///
/// Ofset <see cref="LateUpdate"/>'te yazılıyor — bütün oynanış <c>Update</c>'leri bittikten
/// sonra, kamera işinin geleneksel yeri. Sarsıntı bittiğinde dinlenme konumu <b>birebir</b>
/// geri yazılıyor: lerp ile yaklaşmak kamerayı kalıcı olarak birkaç binde bir kaydırırdı.
/// </summary>
[DefaultExecutionOrder(100)]
public class CameraShaker : MonoBehaviour
{
    public static CameraShaker Instance { get; private set; }

    [Header("Referanslar")]
    [Tooltip("genlik ve frekans buradan okunuyor (quakeShakeAmplitude / quakeShakeFrequency)")]
    [SerializeField] GameConfig _config;

    /// <summary>Sarsıntının başladığı konum. Awake'te bir kez okunuyor, bitişte birebir geri yazılıyor.</summary>
    Vector3 _rest;

    float _rumble;

    /// <summary>
    /// <see cref="SetRumble"/>'ın son yazıldığı kare. Bu kareden 1'den fazla geride kalırsa
    /// sarsıntı 0 kabul edilir — "çağıran unuttu" durumunda kamera kendi kendini toparlıyor.
    /// Tolerans 1 kare, çünkü çağıranın <c>Update</c>'i bu bileşenden önce de sonra da olabilir.
    /// </summary>
    int _rumbleFrame = -10;

    float _punchAmp;
    float _punchTimer;
    float _punchDuration;

    /// <summary>Perlin örnekleme konumu. <c>Time.time</c> yerine biriken kendi sayacı — pause'da durur.</summary>
    float _noiseTime;

    /// <summary>Kamera şu an kaydırılmış durumda mı. Sadece kaydırılmışsa geri yazmak gerekiyor.</summary>
    bool _offsetApplied;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }

        Instance = this;

        _rest = transform.localPosition;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------ genel API

    /// <summary>
    /// Sürekli sarsıntının o andaki şiddeti (0-1). <b>Her karede çağır</b> — bir kare
    /// atlanırsa tolere edilir, ikisi atlanırsa sarsıntı söner.
    /// </summary>
    public void SetRumble(float amplitude01)
    {
        _rumble      = Mathf.Clamp01(amplitude01);
        _rumbleFrame = Time.frameCount;
    }

    /// <summary>
    /// Dinlenme konumunu güncelle. <see cref="CameraFit"/> çağırıyor: ekran oranı
    /// değişince kamera dikeyde kayıyor ve <see cref="_rest"/> bayatlıyor — sarsıntı
    /// bitince kamera birebir ESKİ kadraja geri yazılırdı.
    /// </summary>
    public void SetRest(Vector3 rest)
    {
        _rest = rest;

        // Sarsıntının ortasındaysak bu karenin ofseti zaten yeni dinlenme konumundan
        // hesaplanacak; sadece kaydırılmış hâlde beklerken anında oturtmak gerekiyor.
        if (_offsetApplied) return;

        transform.localPosition = _rest;
    }

    /// <summary>Tek seferlik, süresi boyunca sönen darbe. Sürekli sarsıntının üstüne biner.</summary>
    public void Punch(float amplitude01, float duration)
    {
        if (duration <= 0f) return;

        float amp = Mathf.Clamp01(amplitude01);

        // Devam eden daha güçlü bir darbeyi zayıf bir yenisi kesmesin (PlaySquash ile aynı mantık).
        if (_punchTimer > 0f && amp < CurrentPunch()) return;

        _punchAmp      = amp;
        _punchDuration = duration;
        _punchTimer    = duration;
    }

    /// <summary>
    /// Her şeyi kes ve kamerayı ANINDA dinlenme konumuna oturt. Pause / restart / oyun sonu.
    /// </summary>
    public void StopImmediate()
    {
        _rumble      = 0f;
        _rumbleFrame = -10;
        _punchTimer  = 0f;
        _punchAmp    = 0f;

        if (_offsetApplied)
        {
            transform.localPosition = _rest;
            _offsetApplied = false;
        }
    }

    // -------------------------------------------------------------------- döngü

    void LateUpdate()
    {
        float dt = Time.deltaTime;

        if (_punchTimer > 0f) _punchTimer = Mathf.Max(0f, _punchTimer - dt);

        // Çağıran yazmayı bıraktıysa sürekli sarsıntı yok sayılır.
        float rumble = (Time.frameCount - _rumbleFrame) <= 1 ? _rumble : 0f;

        // İkisi toplanmıyor, en büyüğü alınıyor — üst üste binince genlik kaçmasın.
        float amount = Mathf.Max(rumble, CurrentPunch());

        if (amount <= 0f)
        {
            // Boştayken tek karşılaştırmayla çık. Sadece bir kez geri yazılıyor.
            if (_offsetApplied)
            {
                transform.localPosition = _rest;
                _offsetApplied = false;
            }

            return;
        }

        float amplitude = amount * (_config != null ? _config.quakeShakeAmplitude : 0f);

        if (amplitude <= 0f) return;

        _noiseTime += dt * (_config != null ? _config.quakeShakeFrequency : 0f);

        // İki ayrı gürültü hattı: aynı hattan iki örnek alsak X ve Y birlikte hareket eder ve
        // kamera köşegen bir çizgide gidip gelirdi. Sabitler sadece hatları ayırmak için.
        float nx = Mathf.PerlinNoise(_noiseTime, 0.37f) * 2f - 1f;
        float ny = Mathf.PerlinNoise(0.73f, _noiseTime) * 2f - 1f;

        transform.localPosition = new Vector3(_rest.x + nx * amplitude,
                                              _rest.y + ny * amplitude,
                                              _rest.z);

        _offsetApplied = true;
    }

    /// <summary>Darbenin o andaki şiddeti — süresi boyunca doğrusal olarak sönüyor.</summary>
    float CurrentPunch()
    {
        if (_punchTimer <= 0f || _punchDuration <= 0f) return 0f;

        return _punchAmp * (_punchTimer / _punchDuration);
    }
}
