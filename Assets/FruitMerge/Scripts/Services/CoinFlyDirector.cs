using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Oyun sonu coin ödülünü, ekranın ortasından kalkan bir para patlaması olarak
/// coin HUD'ına uçurur.
///
/// Mimari — neden ParticleSystem DEĞİL:
///  - Hedef bir <b>UI elemanı</b>. Parçacık sistemi dünya uzayında yaşıyor, HUD ise
///    ekran uzayında; ikisini her karede birbirine çevirmek hem pahalı hem de canvas
///    ölçeklendiği anda yanlış. Paralar UI <see cref="Image"/> olarak uçuyor, böylece
///    HUD'la aynı uzayda kalıyorlar ve hedefe piksel piksel oturuyorlar.
///  - Bütün paralar TEK bir <c>particle_coin</c> sprite'ını paylaşıyor (bkz.
///    <c>_coinSprite</c>) → aynı sprite aynı materyal demektir, batching için atlas
///    şart değil, tek draw call bundan geliyor.
///
/// Performans:
///  - <b>Havuz Awake'te kuruluyor</b> (kural 13): oynanış sırasında tek bir
///    <c>Instantiate</c> yok. Havuz dolarsa para uçmaz ama <b>değeri anında hesaba
///    geçer</b> — oyuncu görsel şölen için para kaybetmez.
///  - <b>Tek Update</b> (kural 7) ve hiç para uçmuyorken ilk satırda çıkıyor.
///  - Döngüde allocation yok (kural 11): paralar bir <c>struct</c> dizisinde, dizi
///    sabit boyutlu, arama basit bir <c>for</c>.
///
/// Tek katman: eskiden paraların bir kısmı sonuç panelinin ÖNÜNDE (yıldızlardan çıkanlar),
/// bir kısmı ARKASINDA (tahtadaki meyvelerden çıkanlar) doğuyordu — iki farklı kalkış
/// noktası, iki ayrı katman gerektiriyordu. Ödül artık tek bir noktadan (ekranın ortası)
/// aktığı için bu ayrımın nedeni ortadan kalktı: bütün paralar aynı tek katmanda uçuyor.
/// </summary>
[DefaultExecutionOrder(-40)]
public class CoinFlyDirector : MonoBehaviour
{
    public static CoinFlyDirector Instance { get; private set; }

    [Header("Referanslar")]
    [SerializeField] GameConfig _config;

    [Tooltip("paraların vardığı nokta — HUD'daki coin ikonu")]
    [SerializeField] RectTransform _target;

    [Tooltip("paraların uçtuğu katman; OverlayCanvas'ta, sonuç panelinin ve cüzdanın üstünde")]
    [SerializeField] RectTransform _layer;

    [Header("Para görseli")]
    [Tooltip("bütün paralar aynı görseli (particle_coin) kullanıyor, miktar para " +
             "SAYISINDAN okunuyor; eskiden 10/20/30 için ayrı görsel vardı ama merkez " +
             "patlamasında para değeri artık tek tek okunmuyor, akışın kalabalığı " +
             "miktarı anlatıyor")]
    [SerializeField] Sprite _coinSprite;

    [Header("Havuz")]
    [Tooltip("patlama coinBurstCount kadar para istiyor, havuz taşarsa para uçmaz ama " +
             "değeri ANINDA hesaba geçer — oyuncu görsel şölen için para kaybetmez")]
    [SerializeField] int _poolSize = 24;

    /// <summary>
    /// Uçan tek bir para. <c>class</c> değil <c>struct</c>: dizi tek blok bellekte
    /// duruyor, her karede gezerken cache dostu ve hiç allocation yok.
    /// </summary>
    struct FlyingCoin
    {
        public RectTransform rt;
        public Image         image;
        public int           value;
        public float         delay;   // kalkışa kalan süre
        public float         t;       // 0-1 uçuş ilerlemesi
        public Vector2       p0;      // kalkış (katman yerel)
        public Vector2       p1;      // bezier kontrol noktası
        public Vector2       p2;      // hedef (katman yerel)
        public float         spin;    // derece/sn, işaretli — yön rastgele seçilir
        public float         angle;   // o anki dönüş (derece)
        public float         size;    // boyut çarpanı (coinBurstSizeJitter'dan)
        public bool          active;
    }

    FlyingCoin[] _coins;
    int _activeCount;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        BuildPool();
    }

    void OnEnable()
    {
        if (Instance != this) return;

        GameEvents.OnRunStarted   += HandleRunStarted;
        GameEvents.OnStateChanged += HandleStateChanged;
    }

    void OnDisable()
    {
        if (Instance != this) return;

        GameEvents.OnRunStarted   -= HandleRunStarted;
        GameEvents.OnStateChanged -= HandleStateChanged;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------------------------------------------------------------- havuz

    // NOT — havuz bilinçli olarak Awake'te, PrewarmQueue'da DEĞİL. Gerekçe
    // WormBoostDirector.BuildCursors'ın üstünde yazıyor.

    void BuildPool()
    {
        int size = Mathf.Max(1, _poolSize);

        _coins = new FlyingCoin[size];

        for (int i = 0; i < _coins.Length; i++)
            _coins[i] = CreateCoin(i);
    }

    /// <summary>
    /// Havuz elemanı. Prefab yerine koddan kuruluyor: para tek bir <see cref="Image"/>,
    /// bir prefab asseti bakımı gereken ikinci bir yer olurdu.
    /// </summary>
    FlyingCoin CreateCoin(int index)
    {
        var go = new GameObject("Coin_" + index, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        var rt = (RectTransform)go.transform;

        rt.SetParent(_layer, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);

        float size = _config != null ? _config.coinFlySize : 96f;

        rt.sizeDelta = new Vector2(size, size);

        var img = go.GetComponent<Image>();

        // Paralar tıklamayı yutmamalı: sonuç ekranındaki butonların üstünden geçiyorlar.
        img.raycastTarget = false;

        go.SetActive(false);

        var coin = new FlyingCoin();

        coin.rt    = rt;
        coin.image = img;

        return coin;
    }

    // ---------------------------------------------------------------- genel API

    /// <summary>
    /// Oyun sonu coin ödülünü EKRANIN ORTASINDAN kalkan tek bir para patlaması olarak
    /// HUD'a yollar. Ödül eskiden kaynağına göre (yıldız/meyve) farklı noktalardan
    /// kalkıyordu; artık kaynağı ne olursa olsun tek bir merkez noktasından akan bir
    /// şölen olarak okunuyor — miktarı tek bir paranın üstündeki sayı değil, akışın
    /// kalabalığı anlatıyor.
    /// </summary>
    /// <param name="totalValue">bu patlamada dağıtılacak toplam coin</param>
    /// <param name="delay">ilk paranın kalkışına kadar bekleme (sn); sonraki paralar
    /// bunun üstüne <c>coinBurstStagger</c> ekleyerek sırayla kalkar</param>
    /// <returns>
    /// Bu patlamanın SON parasının kalkış anı (<paramref name="delay"/> ile aynı
    /// zaman ekseninde). Çağıran, ardından başka bir patlama sıraya koyacaksa bu
    /// değeri kullanmalı — para sayısı <c>coinBurstCount</c>'tan az olabildiği için
    /// (bkz. <c>Mathf.Clamp</c> aşağıda) gerçek kuyruk süresi çağıranın kendi
    /// hesaplayabileceği bir şey değil, kırpma kuralını bilen tek yer burası olmalı.
    /// <paramref name="totalValue"/> ≤ 0 ise hiç para kalkmaz, kuyruk da yoktur —
    /// <paramref name="delay"/> aynen geri döner. Havuz dolup uçamayan paralar da
    /// zaman almaz (anında hesaba geçer), o yüzden hesaba katılmazlar.
    /// </returns>
    public float SpawnBurst(int totalValue, float delay)
    {
        if (totalValue <= 0) return delay;

        // Değeri 0 olan para doğurmanın anlamı yok — 10 coin'i 14 paraya bölemezsin.
        int count = Mathf.Clamp(_config != null ? _config.coinBurstCount : 14, 1, totalValue);

        float originYRatio = _config != null ? _config.coinBurstOriginYRatio : 0.06f;

        Vector2 origin = new Vector2(Screen.width * 0.5f, Screen.height * (0.5f + originYRatio));

        if (!ResolveEndpoints(origin, out Vector2 start, out Vector2 end))
        {
            Credit(totalValue);
            return delay;
        }

        float stagger = _config != null ? _config.coinBurstStagger : 0.045f;

        // Toplam değer birebir korunmalı — bir coin bile kaybolmayacak. Kalan
        // (totalValue % count) ilk paralara birer birer dağıtılıyor, yoksa tamsayı
        // bölmesi coin kaybettirirdi.
        int baseValue = totalValue / count;
        int remainder = totalValue % count;

        for (int i = 0; i < count; i++)
        {
            int payValue = baseValue + (i < remainder ? 1 : 0);

            int slot = FindFreeSlot();

            // Havuz dolu: gösteri olmasın ama para kaybolmasın (mevcut desen).
            if (slot < 0)
            {
                Credit(payValue);
                continue;
            }

            Launch(slot, start, end, payValue, delay + i * stagger);
        }

        return delay + (count - 1) * stagger;
    }

    /// <summary>Havadaki bütün paraları anında toplar — değerleri yine hesaba geçer.</summary>
    public void FlushAll()
    {
        if (_coins == null) return;

        for (int i = 0; i < _coins.Length; i++)
        {
            if (!_coins[i].active) continue;

            Land(i);
        }
    }

    // ---------------------------------------------------------------- çekirdek

    /// <summary>
    /// Ekran pikselindeki kalkış noktasını ve HUD hedefini <see cref="_layer"/>'ın yerel
    /// koordinatına çevirir. <see cref="SpawnBurst"/> bunu patlama başına BİR kez çağırıyor —
    /// aynı hedef hesabının patlamadaki her para için tekrarlanmasına gerek yok.
    /// </summary>
    bool ResolveEndpoints(Vector2 screenPos, out Vector2 start, out Vector2 end)
    {
        start = default;
        end   = default;

        if (_layer == null) return false;

        if (!ScreenToLayer(screenPos, out start)) return false;

        Vector2 targetScreen = _target != null
            ? (Vector2)_target.position
            : new Vector2(Screen.width, Screen.height);

        return ScreenToLayer(targetScreen, out end);
    }

    /// <summary>
    /// Bir parayı havalandırır. Tek çağıran <see cref="SpawnBurst"/> olduğu için saçılma
    /// doğrudan <c>coinBurstSpread</c>'den okunuyor: tek kalkış noktasından (ekranın
    /// ortası) aynı anda <c>coinBurstCount</c> kadar para çıkıyor, hepsi aynı piksele
    /// binseydi tek bir kalın çizgi gibi görünürdü — bu değer o kalabalığın ne kadar
    /// yayılacağını belirliyor.
    /// </summary>
    void Launch(int slot, Vector2 start, Vector2 end, int value, float delay)
    {
        float arc     = _config != null ? _config.coinFlyArc     : 260f;
        float scatter = _config != null ? _config.coinBurstSpread : 170f;

        // Kalkış noktasını hafifçe dağıt: aynı patlamadan çıkan paralar üst üste binmesin.
        start += Random.insideUnitCircle * scatter;

        // Kavis: iki nokta arasının ortasından, gidiş yönüne DİK bir sapma. İşaret
        // rastgele, yani paralar bazen soldan bazen sağdan dolanıyor.
        Vector2 mid  = (start + end) * 0.5f;
        Vector2 dir  = end - start;
        Vector2 perp = new Vector2(-dir.y, dir.x).normalized;

        float amount = arc * Random.Range(0.45f, 1f) * (Random.value < 0.5f ? -1f : 1f);

        float jitter    = _config != null ? _config.coinBurstSizeJitter : 0.25f;
        float spinSpeed = _config != null ? _config.coinBurstSpinSpeed  : 220f;

        ref FlyingCoin c = ref _coins[slot];

        c.value = value;
        c.delay = Mathf.Max(0f, delay);
        c.t     = 0f;
        c.p0    = start;
        c.p1    = mid + perp * amount;
        c.p2    = end;
        c.size  = Mathf.Max(0.1f, 1f + Random.Range(-jitter, jitter));

        // Yön rastgele: yoksa bütün paralar aynı tarafa dönüyor, kalabalık bir
        // pervane gibi durur.
        c.spin   = Random.Range(0.6f, 1f) * spinSpeed * (Random.value < 0.5f ? -1f : 1f);
        c.angle  = 0f;
        c.active = true;

        c.image.sprite = _coinSprite;

        c.rt.anchoredPosition = start;

        // Havuzdan gelen slot önceki uçuşun dönüşünü miras almasın.
        c.rt.localRotation = Quaternion.identity;
        c.rt.localScale    = Vector3.one * c.size;

        // Bekleme süresince görünmesin — para kaynağından "kopuyormuş" gibi belirsin.
        c.rt.gameObject.SetActive(c.delay <= 0f);

        _activeCount++;
    }

    void Update()
    {
        // Hiç para uçmuyorsa tek karşılaştırmayla çık (kural 7).
        if (_activeCount == 0) return;

        // Sonuç ekranı timeScale'e dokunmuyor ama pause edilirse paralar donmalı değil,
        // UI zamanında akmalı (kural 4).
        float dt = Time.unscaledDeltaTime;

        float duration  = _config != null ? Mathf.Max(0.05f, _config.coinFlyDuration) : 0.75f;
        float endScale  = _config != null ? _config.coinFlyEndScale : 0.55f;

        for (int i = 0; i < _coins.Length; i++)
        {
            if (!_coins[i].active) continue;

            ref FlyingCoin c = ref _coins[i];

            if (c.delay > 0f)
            {
                c.delay -= dt;

                if (c.delay > 0f) continue;

                c.rt.gameObject.SetActive(true);
            }

            c.t += dt / duration;

            if (c.t >= 1f) { Land(i); continue; }

            // Yavaş başlayıp hızlanma: para önce kaynağından ayrılıyor, sonra HUD'a
            // çekiliyor. Düz lineer hareket "kayıyor" gibi duruyordu.
            float e = c.t * c.t * (3f - 2f * c.t);
            e = e * e;

            float inv = 1f - e;

            c.rt.anchoredPosition = inv * inv * c.p0
                                  + 2f * inv * e * c.p1
                                  + e * e * c.p2;

            // Uçarken dönüyor (madeni paranın havada takla atması) ve hedefe yaklaşınca
            // hafifçe küçülüyor; boyut çarpanı bu küçülmenin üstüne biniyor.
            c.angle += c.spin * dt;
            c.rt.localRotation = Quaternion.Euler(0f, 0f, c.angle);

            float s = Mathf.Lerp(1f, endScale, e) * c.size;

            c.rt.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>Para hedefe vardı: gizle, slotu boşalt, değeri hesaba geçir.</summary>
    void Land(int index)
    {
        ref FlyingCoin c = ref _coins[index];

        c.active = false;
        c.rt.gameObject.SetActive(false);

        _activeCount--;

        Credit(c.value);

        c.value = 0;
    }

    void Credit(int value)
    {
        if (value <= 0 || SaveService.Instance == null) return;

        SaveService.Instance.AddCoins(value);
    }

    /// <summary>Boş slot arar. Tek havuz, tek katman — basit bir <c>for</c> yeterli.</summary>
    int FindFreeSlot()
    {
        if (_coins == null) return -1;

        for (int i = 0; i < _coins.Length; i++)
            if (!_coins[i].active) return i;

        return -1;
    }

    /// <summary>
    /// Ekran pikselini katmanın yerel koordinatına çevirir. Bütün canvas'lar
    /// Screen Space - Overlay olduğu için kamera <c>null</c> geçiliyor.
    /// </summary>
    bool ScreenToLayer(Vector2 screenPos, out Vector2 local)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _layer, screenPos, null, out local);
    }

    // ---------------------------------------------------------------- olaylar

    // Yeni oyun başlarken havada para kalmasın; değerleri kaybolmadan hesaba geçsin.
    void HandleRunStarted() => FlushAll();

    void HandleStateChanged(GameState s)
    {
        if (s == GameState.Menu) FlushAll();
    }
}
