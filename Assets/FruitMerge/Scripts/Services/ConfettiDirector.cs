using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// İki kutlama anında konfeti: karpuz birleşince birleşme noktasında patlama,
/// yeni rekor kırılınca sonuç ekranında yukarıdan yağmur.
///
/// Mimari — neden UI uzayı, neden ParticleSystem DEĞİL:
///  - Projedeki bütün canvas'lar Screen Space - Overlay (HUDCanvas order 0,
///    PanelCanvas 2, OverlayCanvas 3). Overlay canvas her zaman dünyanın üstüne
///    kompozit edilir, yani dünya uzayındaki bir <c>ParticleSystem</c> sonuç
///    panelinin ARKASINDA kalır ve rekor konfetisi hiç görünmez. Bu yüzden
///    konfeti, <see cref="CoinFlyDirector"/>'ün kullandığı tekniğin aynısıyla
///    UI uzayında yaşıyor: havuzlu UI <see cref="Image"/>'lar, <c>struct</c>
///    dizisi, tek <c>Update</c>, allocation yok.
///  - Karpuz patlaması tahtada olduğu için dünya uzayında da çalışırdı ama iki
///    ayrı konfeti sistemi bakım borcu olurdu — tek sistem her iki yerde çalışıyor.
///
/// Performans:
///  - <b>Havuz Awake'te kuruluyor</b> (kural 13): oynanış sırasında Instantiate yok.
///  - <b>Tek Update</b> (kural 7) ve hiç parça uçmuyorken ilk satırda çıkıyor.
///  - Döngüde allocation yok (kural 11): parçalar bir <c>struct</c> dizisinde.
///  - UI zamanı <c>Time.unscaledDeltaTime</c> (kural 4) — sonuç ekranı ve
///    pause'da timeScale 0 olabiliyor, kutlama yine akmalı.
///
/// Hareketin çeşitliliği PARÇA BAŞINA rastgelelikten geliyor, doğuş noktasından değil:
/// ilk sürümde bütün parçalar aynı sürtünme/salınım değerlerini paylaşıyordu, hepsi
/// ~0.4 sn içinde aynı terminal hıza oturup aralarındaki mesafe donuyordu — 110 parça
/// bile tek bir blok gibi iniyordu (yaşanmış hata). Şimdi her parça kendi sürtünme/
/// salınım/takla değerini doğarken çekiyor (bkz. <see cref="Piece"/>), bu yüzden
/// <see cref="PlayBurstAtScreen"/> ve <see cref="PlayRain"/> aynı <see cref="TrySpawn"/>
/// üzerinden otomatik olarak dağınık görünüyor.
/// </summary>
[DefaultExecutionOrder(-40)]
public class ConfettiDirector : MonoBehaviour
{
    public static ConfettiDirector Instance { get; private set; }

    [Header("Referanslar")]
    [SerializeField] GameConfig _config;

    [Tooltip("konfetinin doğduğu katman — OverlayCanvas'ta, her şeyin (sonuç " +
             "paneli dahil) üstünde")]
    [SerializeField] RectTransform _layer;

    [Tooltip("karpuzun dünya konumunu ekrana çevirmek için. Runtime'da aranmıyor, " +
             "buradan veriliyor (kural 11)")]
    [SerializeField] Camera _worldCamera;

    [Header("Konfeti görselleri")]
    [Tooltip("6 konfeti görseli (particle_confetti_01..06). Havuza BLOKLAR halinde dağıtılıyor")]
    [SerializeField] Sprite[] _sprites = new Sprite[6];

    /// <summary>
    /// Uçan tek bir konfeti parçası. <c>class</c> değil <c>struct</c>: dizi tek blok
    /// bellekte duruyor, her karede gezerken cache dostu ve hiç allocation yok.
    /// </summary>
    struct Piece
    {
        public RectTransform rt;
        public Image         image;
        public Vector2       pos;
        public Vector2       vel;
        public float         angle;
        public float         spin;
        public float         life;
        public float         lifetime;
        public float         size;
        public float         flutterPhase;
        public float         delay;       // kalkışa kalan süre

        // Parça BAŞINA sapan hareket değerleri — hepsi ortak olsaydı bütün parçalar
        // aynı terminal hıza oturup tek blok gibi inerdi (bkz. sınıf yorumu).
        public float         drag;
        public float         flutterAmp;
        public float         flutterFreq;
        public float         tumbleFreq;
        public float         tumblePhase;

        public bool          active;
    }

    Piece[] _pieces;
    int     _activeCount;

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

        GameEvents.OnMaxTierMerged += HandleMaxTierMerged;
        GameEvents.OnRunStarted    += HandleRunStarted;
        GameEvents.OnStateChanged  += HandleStateChanged;
    }

    void OnDisable()
    {
        if (Instance != this) return;

        GameEvents.OnMaxTierMerged -= HandleMaxTierMerged;
        GameEvents.OnRunStarted    -= HandleRunStarted;
        GameEvents.OnStateChanged  -= HandleStateChanged;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------------------------------------------------------------- havuz

    // NOT — havuz bilinçli olarak Awake'te, PrewarmQueue'da DEĞİL. Bir denemede karelere
    // yayılmıştı ve geri alındı; gerekçe WormBoostDirector.BuildCursors'ın üstünde yazıyor
    // (Play Mode'da Reload Domain/Scene kapalı olduğu için ısıtma sayacı oturumlar arasında
    // yaşıyor ve açılış ekranı kilitleniyordu).

    void BuildPool()
    {
        int count = _config != null ? Mathf.Max(1, _config.confettiPoolSize) : 140;
        float size = _config != null ? _config.confettiSize : 64f;

        int spriteCount = _sprites != null ? _sprites.Length : 0;

        _pieces = new Piece[count];

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Confetti_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

            var rt = (RectTransform)go.transform;

            rt.SetParent(_layer, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);

            var img = go.GetComponent<Image>();

            // Konfeti tıklamayı yutmamalı: sonuç ekranındaki butonların üstünden geçiyor.
            img.raycastTarget = false;

            // Sprite'ları BLOKLAR halinde ata: UI batch'i hiyerarşi SIRASINA göre
            // kırılıyor, aynı sprite'lı kardeşler yan yana olduğu için ekranda kırk
            // parça uçarken 6 draw call'dan fazlası olmuyor. Rastgele atansaydı her
            // parça batch'i kırardı. _sprites boş/null elemanlıysa da çökmeden çalışır
            // (Image'a null sprite atamak güvenli, sadece o slot görünmez olur).
            if (spriteCount > 0)
                img.sprite = _sprites[i * spriteCount / count];

            go.SetActive(false);

            _pieces[i].rt    = rt;
            _pieces[i].image = img;
        }
    }

    // ---------------------------------------------------------------- genel API

    /// <summary>Dünya konumunda (karpuzun birleşme noktasında) patlama.</summary>
    public void PlayBurstAtWorld(Vector3 worldPos)
    {
        if (_worldCamera == null) return;

        PlayBurstAtScreen(_worldCamera.WorldToScreenPoint(worldPos));
    }

    /// <summary>Ekran koordinatında patlama — her yöne saçılan parçalar.</summary>
    public void PlayBurstAtScreen(Vector2 screenPos)
    {
        if (_layer == null) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, screenPos, null, out Vector2 local))
            return;

        int count       = _config != null ? _config.confettiBurstCount    : 26;
        float speedMin  = _config != null ? _config.confettiBurstSpeedMin : 1100f;
        float speedMax  = _config != null ? _config.confettiBurstSpeedMax : 2000f;
        float upBias    = _config != null ? _config.confettiBurstUpBias   : 0.55f;

        for (int i = 0; i < count; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));

            // Yukarı eğilim olmadan parçaların yarısı aşağı fırlıyor ve anında
            // ekranın altına iniyor.
            dir.y = Mathf.Lerp(dir.y, Mathf.Abs(dir.y), upBias);
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;

            float speed = Random.Range(speedMin, speedMax);

            TrySpawn(local, dir * speed, 0f);
        }
    }

    /// <summary>Ekranın üst kenarından, genişliğe yayılmış bir konfeti yağmuru.</summary>
    public void PlayRain()
    {
        if (_layer == null) return;

        int count        = _config != null ? _config.confettiRainCount        : 110;
        float duration   = _config != null ? _config.confettiRainDuration     : 1.8f;
        float speedMin   = _config != null ? _config.confettiRainSpeedMin     : 120f;
        float speedMax   = _config != null ? _config.confettiRainSpeedMax     : 520f;
        float topMargin  = _config != null ? _config.confettiRainTopMargin    : 60f;
        float topSpread  = _config != null ? _config.confettiRainTopSpread    : 450f;
        float delayJitter = _config != null ? _config.confettiRainDelayJitter : 0.7f;
        float drift       = _config != null ? _config.confettiRainDrift       : 260f;

        // Katman tam ekran stretch olduğu için yerel koordinat merkezi ekran ortası;
        // rect cihaz genişliğine göre doğru değeri veriyor — sabit 1080 yazmak 20:9
        // telefonda yanlış olurdu.
        float w = _layer.rect.width;
        float h = _layer.rect.height;

        // Eşit aralıklı bırakma ritmik bir dalga üretiyor; göz o düzenliliği "toplu
        // hareket" olarak okuyor. Adımı rastgele kaydırmak diziyi bozup dağıtıyor.
        float step = duration / Mathf.Max(1, count);

        for (int i = 0; i < count; i++)
        {
            // Hepsi tam aynı Y'de doğsa ekrana kusursuz bir yatay HAT halinde girerlerdi.
            Vector2 pos = new Vector2(Random.Range(-w * 0.5f, w * 0.5f), h * 0.5f + topMargin + Random.Range(0f, topSpread));

            // Dümdüz aşağı inen parçalar paralel çizgiler gibi durur — yatay sürüklenme dağıtır.
            Vector2 vel = new Vector2(Random.Range(-drift, drift), -Random.Range(speedMin, speedMax));

            float delay = Mathf.Max(0f, i * step + Random.Range(-step * delayJitter, step * delayJitter));

            TrySpawn(pos, vel, delay);
        }
    }

    /// <summary>Bütün aktif parçaları anında gizler — yeni oyunda havada parça kalmasın.</summary>
    public void ClearAll()
    {
        if (_pieces == null) return;

        for (int i = 0; i < _pieces.Length; i++)
        {
            if (!_pieces[i].active) continue;

            Release(i);
        }
    }

    // ---------------------------------------------------------------- çekirdek

    /// <summary>
    /// Boş bir slotu doğurur. Havuz dolarsa <c>false</c> döner — konfeti kritik bir
    /// geri bildirim değil, sessizce vazgeçilir.
    /// </summary>
    bool TrySpawn(Vector2 localPos, Vector2 velocity, float delay)
    {
        int slot = FindFreeSlot();

        if (slot < 0) return false;

        float sizeJitter    = _config != null ? _config.confettiSizeJitter    : 0.3f;
        float spinSpeed     = _config != null ? _config.confettiSpinSpeed     : 540f;
        float lifetime      = _config != null ? _config.confettiLifetime      : 3.2f;
        float drag          = _config != null ? _config.confettiDrag          : 2.2f;
        float dragJitter    = _config != null ? _config.confettiDragJitter    : 0.45f;
        float flutterSpeed  = _config != null ? _config.confettiFlutterSpeed  : 230f;
        float flutterFreq   = _config != null ? _config.confettiFlutterFrequency : 3.2f;
        float flutterJitter = _config != null ? _config.confettiFlutterJitter : 0.5f;
        float tumbleSpeed   = _config != null ? _config.confettiTumbleSpeed   : 2.1f;

        ref Piece p = ref _pieces[slot];

        p.pos          = localPos;
        p.vel          = velocity;
        p.size         = Mathf.Max(0.1f, 1f + Random.Range(-sizeJitter, sizeJitter));
        p.spin         = Random.Range(0.35f, 1f) * spinSpeed * (Random.value < 0.5f ? -1f : 1f);
        p.flutterPhase = Random.value * 10f;
        p.life         = 0f;
        p.lifetime     = lifetime;

        // Parça BAŞINA sürtünme/salınım/takla sapması — ASIL çare burada: sürtünme
        // herkeste aynı olsaydı bütün parçalar ~0.4 sn içinde aynı terminal hıza
        // oturup aralarındaki mesafe donardı, tek blok gibi görünürlerdi.
        p.drag        = Mathf.Max(0.1f, drag * (1f + Random.Range(-dragJitter, dragJitter)));
        p.flutterAmp  = Mathf.Max(0f, flutterSpeed * (1f + Random.Range(-flutterJitter, flutterJitter)));
        p.flutterFreq = Mathf.Max(0.01f, flutterFreq * (1f + Random.Range(-flutterJitter, flutterJitter)));
        p.tumbleFreq  = Mathf.Max(0.01f, tumbleSpeed * (1f + Random.Range(-flutterJitter, flutterJitter)));
        p.tumblePhase = Random.value * 10f;

        // Hepsi aynı açıyla doğsa hizalanmış görünürlerdi.
        p.angle = Random.Range(0f, 360f);
        p.delay = Mathf.Max(0f, delay);
        p.active = true;

        p.rt.anchoredPosition = p.pos;
        p.rt.localRotation    = Quaternion.Euler(0f, 0f, p.angle);
        p.rt.localScale       = new Vector3(p.size, p.size, 1f);

        Color c = p.image.color;
        c.a = 1f;
        p.image.color = c;

        // Bekleme süresince görünmesin — kalkış aynı anda değil, dağılmış hissetsin.
        p.rt.gameObject.SetActive(p.delay <= 0f);

        _activeCount++;

        return true;
    }

    /// <summary>
    /// Boş slot arar. RASTGELE bir başlangıç indeksinden itibaren tarıyor: baştan
    /// taransa hep aynı bloktan (aynı sprite'lı) parça seçilirdi, altı çeşidin sadece
    /// biri görünürdü. Çeşitlilik buradan geliyor, sprite ataması sabit kalıyor.
    /// </summary>
    int FindFreeSlot()
    {
        if (_pieces == null || _pieces.Length == 0) return -1;

        int start = Random.Range(0, _pieces.Length);

        for (int i = 0; i < _pieces.Length; i++)
        {
            int idx = (start + i) % _pieces.Length;

            if (!_pieces[idx].active) return idx;
        }

        return -1;
    }

    /// <summary>Parça gizlenir, slot boşalır.</summary>
    void Release(int index)
    {
        ref Piece p = ref _pieces[index];

        p.active = false;
        p.rt.gameObject.SetActive(false);

        _activeCount--;
    }

    void Update()
    {
        // Hiç parça uçmuyorsa tek karşılaştırmayla çık (kural 7).
        if (_activeCount == 0) return;

        // UI zamanı: sonuç ekranı ve pause'da timeScale 0 olabiliyor, kutlama yine
        // akmalı (kural 4).
        float dt = Time.unscaledDeltaTime;

        float gravity       = _config != null ? _config.confettiGravity        : 1600f;
        float fadeRatio     = _config != null ? _config.confettiFadeRatio      : 0.18f;
        float size          = _config != null ? _config.confettiSize           : 64f;
        float tumbleMinScale = _config != null ? _config.confettiTumbleMinScale : 0.15f;

        float halfHeight = _layer != null ? _layer.rect.height * 0.5f : Screen.height * 0.5f;

        for (int i = 0; i < _pieces.Length; i++)
        {
            if (!_pieces[i].active) continue;

            ref Piece p = ref _pieces[i];

            if (p.delay > 0f)
            {
                p.delay -= dt;

                if (p.delay > 0f) continue;

                p.rt.gameObject.SetActive(true);
            }

            p.life += dt;

            if (p.life >= p.lifetime) { Release(i); continue; }

            // Yerçekimi ortak — düşüşü ayıran şey sürtünme (aşağıda), gravity herkeste aynı olabilir.
            p.vel.y -= gravity * dt;

            // Sürtünme PARÇA BAŞINA (p.drag): konfeti taş gibi düşmemeli, terminal hızı
            // yerçekimi/sürtünme oranında sınırlıyor. Ortak bir sürtünme kullanılsaydı
            // bütün parçalar aynı terminal hıza oturup tek blok gibi görünürdü.
            p.vel *= Mathf.Max(0f, 1f - p.drag * dt);

            // Salınım PARÇA BAŞINA genlik/frekansla: kağıt parçasının havada sağa sola
            // savrulması. Sadece faz rastgele olsaydı hepsi aynı ritimde sallanırdı.
            p.pos.x += Mathf.Sin((p.life + p.flutterPhase) * p.flutterFreq * Mathf.PI * 2f) * p.flutterAmp * dt;

            p.pos += p.vel * dt;
            p.rt.anchoredPosition = p.pos;

            p.angle += p.spin * dt;
            p.rt.localRotation = Quaternion.Euler(0f, 0f, p.angle);

            // Takla: sadece Z dönüşü olan kağıt hep aynı genişlikte görünüp "sticker"
            // gibi durur. X ölçeğini sinüsle daraltıp genişletmek parçanın kendi dikey
            // ekseni etrafında döndüğü illüzyonunu veriyor — bunun başka yolu yok,
            // localScale'i artık HER KAREDE yazıyoruz (eskiden sadece doğuşta yazılırdı).
            float tumble = Mathf.Lerp(tumbleMinScale, 1f,
                Mathf.Abs(Mathf.Cos((p.life + p.tumblePhase) * p.tumbleFreq * Mathf.PI * 2f)));

            p.rt.localScale = new Vector3(p.size * tumble, p.size, 1f);

            // Sönme SADECE ömrün son diliminde: ondan önce alpha'ya hiç dokunmuyoruz,
            // gereksiz yazma canvas'ı boşuna kirletir (kural 9).
            float fadeStart = p.lifetime * (1f - fadeRatio);

            if (p.life >= fadeStart)
            {
                float t = fadeRatio > 0.0001f ? (p.life - fadeStart) / (p.lifetime * fadeRatio) : 1f;

                Color c = p.image.color;
                c.a = 1f - Mathf.Clamp01(t);
                p.image.color = c;
            }

            // Ekranın altından çıkan parçayı erken serbest bırak — ömrü beklemenin
            // anlamı yok, slot başka parçaya lazım.
            if (p.pos.y < -halfHeight - size) Release(i);
        }
    }

    // ---------------------------------------------------------------- olaylar

    void HandleMaxTierMerged(FruitDefinition def, Vector2 position) => PlayBurstAtWorld(position);

    // Yeni oyun başlarken havada parça kalmasın.
    void HandleRunStarted() => ClearAll();

    void HandleStateChanged(GameState s)
    {
        if (s == GameState.Menu) ClearAll();
    }
}
