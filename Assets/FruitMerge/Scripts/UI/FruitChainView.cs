using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Evrim zinciri şeridi: FruitDatabase'deki 11 meyveyi tier sırasıyla gösterir.
/// Ulaşılan en yüksek tier'a kadar meyve ikonu tam görünür, gerisi silik.
/// Update yok — alpha'lar merge olaylarında, yerleşim ise sadece şerit yeniden
/// boyutlandığında (yani pratikte açılışta bir kez) hesaplanıyor.
///
/// <b>Yerleşim neden artık HorizontalLayoutGroup'a bırakılmıyor:</b>
/// Grup 11 slotu EŞİT bölüştürüyordu, ama ikonlar eşit değil — kiraz 48, karpuz 72
/// birim. Referans çözünürlükte (1080×1920) slot başına 75.6 birim düşüyor ve 72
/// kıl payı sığıyordu. CanvasScaler'ın Match değeri 0.5 olduğu için uzun ekranlarda
/// (1080×2400) canvas referans genişliği 1080'den 966'ya iniyor, slot 65.3'e düşüyor
/// ve son üç ikon (67.2 / 69.6 / 72) komşusunun üstüne biniyordu. Baştaki küçük
/// meyvelerin yanında ise kullanılmayan boşluk duruyordu.
///
/// Çözüm: slot genişliği ikonun kendi boyutuyla ORANTILI. Toplam talep 660 birim;
/// 966 referans genişlikte bile 718 birim yer var, yani hiçbir şeyi küçültmeye gerek
/// kalmadan hepsi aynı nispi payla oturuyor. Daha da dar bir ekran çıkarsa
/// <see cref="ApplyLayout"/> hepsini AYNI oranda küçültüyor — zincirin kendi
/// büyüme ritmi korunuyor.
/// </summary>
public class FruitChainView : MonoBehaviour
{
    [SerializeField] FruitDatabase _database;
    [SerializeField] GameConfig _config;

    [Tooltip("tier sırasıyla 11 meyve ikonu (Slot_XX/Icon)")]
    [SerializeField] Image[] _fruitIcons;

    [Tooltip("tier sırasıyla 11 idle yüz ikonu (Slot_XX/Icon/Face). Gövdeyle aynı silikleşir")]
    [SerializeField] Image[] _faceIcons;

    int _highestTier;

    // ---- yerleşim --------------------------------------------------------
    RectTransform          _rect;
    HorizontalLayoutGroup  _layout;
    LayoutElement[]        _slots;

    /// <summary>Sahnede yazılmış ikon genişlikleri (48…72). Ölçek referansımız bunlar.</summary>
    float[] _authoredWidth;
    float   _authoredSum;

    /// <summary>En son hangi genişliğe göre hesapladık — aynı değerle tekrar çalışmasın.</summary>
    float _appliedWidth = -1f;

    void Awake()
    {
        _rect   = transform as RectTransform;
        _layout = GetComponent<HorizontalLayoutGroup>();

        CacheSlots();
    }

    void OnEnable()
    {
        GameEvents.OnRunStarted    += HandleRunStarted;
        GameEvents.OnMerged        += HandleMerged;
        GameEvents.OnMaxTierMerged += HandleMerged;
    }

    void OnDisable()
    {
        GameEvents.OnRunStarted    -= HandleRunStarted;
        GameEvents.OnMerged        -= HandleMerged;
        GameEvents.OnMaxTierMerged -= HandleMerged;
    }

    void Start()
    {
        ApplyLayout();

        BuildInitialState();
    }

    /// <summary>Çözünürlük/yönelim değişince şerit yeniden boyutlanıyor — payları tazele.</summary>
    void OnRectTransformDimensionsChange() => ApplyLayout();

    void HandleRunStarted() => BuildInitialState();

    // ------------------------------------------------------------- yerleşim

    void CacheSlots()
    {
        int n = _fruitIcons != null ? _fruitIcons.Length : 0;

        _slots         = new LayoutElement[n];
        _authoredWidth = new float[n];
        _authoredSum   = 0f;

        for (int i = 0; i < n; i++)
        {
            if (_fruitIcons[i] == null) continue;

            var iconRect = _fruitIcons[i].transform as RectTransform;

            if (iconRect == null) continue;

            _authoredWidth[i] = iconRect.sizeDelta.x;
            _authoredSum     += _authoredWidth[i];

            // ikon Slot_XX/Icon — payı taşıyan LayoutElement slotun üzerinde
            Transform slot = iconRect.parent;

            if (slot != null) _slots[i] = slot.GetComponent<LayoutElement>();
        }
    }

    void ApplyLayout()
    {
        // Awake'ten önce de tetiklenebiliyor
        if (_rect == null || _slots == null || _authoredSum <= 0f) return;

        float available = _rect.rect.width;

        if (_layout != null)
        {
            available -= _layout.padding.left + _layout.padding.right;
            available -= _layout.spacing * Mathf.Max(0, _slots.Length - 1);
        }

        if (available <= 0f) return;

        // Aynı genişlikte ikinci kez çalışma: OnRectTransformDimensionsChange
        // layout rebuild sırasında da tetikleniyor, buradan geri beslenip
        // sonsuz döngü kurmasın (kural 9).
        if (Mathf.Approximately(available, _appliedWidth)) return;

        _appliedWidth = available;

        for (int i = 0; i < _slots.Length; i++)
            if (_slots[i] != null) _slots[i].flexibleWidth = _authoredWidth[i];

        // Orantılı bölüşümde ikon_i'nin payı = available × w_i / toplam. İkonun
        // sığması için w_i ≤ o pay, yani toplam ≤ available olması yeterli —
        // dolayısıyla TEK bir ortak çarpan hepsini birden sığdırıyor.
        float k = Mathf.Min(1f, available / _authoredSum);

        var scale = new Vector3(k, k, 1f);

        for (int i = 0; i < _fruitIcons.Length; i++)
        {
            if (_fruitIcons[i] == null) continue;

            // sizeDelta yerine localScale: yüz ikonu (Slot_XX/Icon/Face) gövdenin
            // çocuğu, ölçekle birlikte kendiliğinden geliyor.
            Transform t = _fruitIcons[i].transform;

            if (t.localScale != scale) t.localScale = scale;
        }

        // Rebuild'i elle istemiyoruz: LayoutElement.flexibleWidth setter'ı değer
        // DEĞİŞTİYSE zaten SetDirty çağırıyor. Buradan tekrar istemek, bu metot
        // OnRectTransformDimensionsChange'ten geldiğinde rebuild'in içinde rebuild
        // istemek olurdu.
    }

    void BuildInitialState()
    {
        _highestTier = _database != null
            ? Mathf.Clamp(_database.spawnableCount - 1, 0, _database.MaxTier)
            : 0;

        Refresh();
    }

    void HandleMerged(FruitDefinition produced, Vector2 position)
    {
        if (produced == null || produced.tier <= _highestTier) return;

        _highestTier = produced.tier;
        Refresh();
    }

    void Refresh()
    {
        float dim = _config != null ? _config.fruitChainDimAlpha : 0.35f;

        for (int i = 0; i < _fruitIcons.Length; i++)
        {
            if (_fruitIcons[i] == null) continue;
            SetAlpha(_fruitIcons[i], i <= _highestTier ? 1f : dim);
        }

        for (int i = 0; i < _faceIcons.Length; i++)
        {
            if (_faceIcons[i] == null) continue;
            SetAlpha(_faceIcons[i], i <= _highestTier ? 1f : dim);
        }
    }

    static void SetAlpha(Image img, float a)
    {
        Color c = img.color;
        if (Mathf.Approximately(c.a, a)) return;
        c.a = a;
        img.color = c;
    }
}
