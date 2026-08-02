using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Birleşmede o anki combo'yu ("x3") üretilen meyvenin renginde, birleşme noktasında
/// gösterir. HUD'da değil, dünya uzayında — meyvenin kendi rengiyle yerinde durup söner.
///
/// Havuzlu (kural 13): zincirleme birleşmede aynı anda birden çok popup havada olabilir.
/// Tek Update (kural 7): her popup'ın <see cref="ComboPopupItem.Tick"/>'i burada çağrılıyor.
/// </summary>
[DefaultExecutionOrder(-40)]
public class ComboPopupDirector : MonoBehaviour, IPrewarmSource
{
    [Header("Referanslar")]
    [SerializeField] ComboPopupItem _prefab;
    [SerializeField] Transform _parent;
    [SerializeField] GameConfig _config;

    [Header("Havuz")]
    [SerializeField] int _prewarmCount = 6;
    [SerializeField] int _maxSize = 16;

    [Header("Teşvik kelimeleri (kademeye göre)")]
    [Tooltip("düşük combo — x2 ile comboTierMidMin arası")]
    [SerializeField] string[] _wordsLow =
        { "Nice!", "Yummy!", "Tasty!", "Sweet!" };

    [Tooltip("orta combo — comboTierMidMin ile comboTierHighMin arası")]
    [SerializeField] string[] _wordsMid =
        { "Delicious!", "Juicy!", "So Good!", "Fruity!" };

    [Tooltip("yüksek combo — comboTierHighMin ile comboTierLegendaryMin arası")]
    [SerializeField] string[] _wordsHigh =
        { "Delightful!", "Mouthwatering!", "Fruit Feast!", "Unstoppable!" };

    [Tooltip("efsane combo — comboTierLegendaryMin ve üstü")]
    [SerializeField] string[] _wordsLegendary =
        { "Legendary!", "Fruit Master!", "Godly Combo!", "Perfection!" };

    ObjectPool<ComboPopupItem> _pool;

    readonly List<ComboPopupItem> _active = new List<ComboPopupItem>(8);

    // Metin her seferinde yeniden kuruluyor ama string birleştirme YOK (kural 11):
    // paylaşımlı StringBuilder + TMP'nin SetText(StringBuilder) aşırı yüklemesi.
    readonly StringBuilder _sb = new StringBuilder(48);

    // aynı kelimenin arka arkaya çıkmaması için kademe başına son seçilen indeks
    readonly int[] _lastWord = { -1, -1, -1, -1 };

    int _prewarmDone;

    public int PrewarmTotal => _prewarmCount;

    public int PrewarmDone => _prewarmDone;

    void Awake()
    {
        _pool = new ObjectPool<ComboPopupItem>(
            createFunc:      CreateItem,
            actionOnGet:     OnGet,
            actionOnRelease: OnRelease,
            actionOnDestroy: OnItemDestroy,
            collectionCheck: true,
            defaultCapacity: _prewarmCount,
            maxSize:         _maxSize
        );

        // Isıtma artık burada değil, açılış ekranı karelere yayarak yapıyor (PrewarmQueue).
        PrewarmQueue.Register(this);
    }

    void OnEnable()
    {
        GameEvents.OnComboMerge += HandleComboMerge;
        GameEvents.OnRunStarted += HandleRunStarted;
    }

    void OnDisable()
    {
        GameEvents.OnComboMerge -= HandleComboMerge;
        GameEvents.OnRunStarted -= HandleRunStarted;
    }

    void OnDestroy()
    {
        PrewarmQueue.Unregister(this);
        _pool?.Dispose();
    }

    ComboPopupItem CreateItem()
    {
        var item = Instantiate(_prefab, _parent);
        item.Bind(_config);
        item.gameObject.SetActive(false);
        return item;
    }

    void OnGet(ComboPopupItem item)
    {
        item.gameObject.SetActive(true);
        _active.Add(item);
    }

    void OnRelease(ComboPopupItem item)
    {
        _active.Remove(item);
        item.gameObject.SetActive(false);
    }

    void OnItemDestroy(ComboPopupItem item)
    {
        if (item != null) Destroy(item.gameObject);
    }

    /// <summary>Bu karede en fazla <paramref name="budget"/> popup yarat (bkz. <see cref="PrewarmQueue"/>).</summary>
    public void PrewarmStep(int budget)
    {
        if (budget <= 0) return;

        int end = Mathf.Min(_prewarmDone + budget, _prewarmCount);

        for (int i = _prewarmDone; i < end; i++) _pool.Release(CreateItem());

        _prewarmDone = end;
    }

    void HandleComboMerge(FruitDefinition produced, Vector2 position, int combo)
    {
        if (produced == null || _config == null) return;
        if (combo < _config.comboPopupMinCombo) return;

        // Yatıklık her popup'ta yeniden çekiliyor: aynı açıyla üst üste binen yazılar
        // tek bir kalın leke gibi görünüyordu.
        float tilt = Random.Range(_config.comboPopupTiltMin, _config.comboPopupTiltMax);

        if (Random.value < 0.5f) tilt = -tilt;

        int tier = TierOf(combo);

        BuildText(combo, tier);

        ComboPopupItem item = _pool.Get();

        item.Play(position, _sb, produced.displayColor, tier, tilt);
    }

    /// <summary>0 düşük · 1 orta · 2 yüksek · 3 efsane</summary>
    int TierOf(int combo)
    {
        if (combo >= _config.comboTierLegendaryMin) return 3;
        if (combo >= _config.comboTierHighMin)      return 2;
        if (combo >= _config.comboTierMidMin)       return 1;

        return 0;
    }

    /// <summary>
    /// "x3" üstte, teşvik kelimesi altta ve daha küçük — tek TMP, tek draw call.
    /// İki ayrı obje yerine rich text <c>&lt;size=%&gt;</c> kullanılıyor.
    /// </summary>
    void BuildText(int combo, int tier)
    {
        _sb.Clear();

        _sb.Append('x').Append(combo);

        string word = PickWord(tier);

        if (string.IsNullOrEmpty(word)) return;

        int percent = Mathf.RoundToInt(Mathf.Clamp(_config.comboPopupWordScale, 0.1f, 2f) * 100f);

        _sb.Append("\n<size=").Append(percent).Append("%>").Append(word).Append("</size>");
    }

    string PickWord(int tier)
    {
        string[] list = WordsFor(tier);

        if (list == null || list.Length == 0) return null;

        if (list.Length == 1) return list[0];

        // aynı kelime arka arkaya çıkmasın — "Nice! Nice! Nice!" tekdüze duruyordu
        int i = Random.Range(0, list.Length);

        if (i == _lastWord[tier]) i = (i + 1) % list.Length;

        _lastWord[tier] = i;

        return list[i];
    }

    string[] WordsFor(int tier)
    {
        switch (tier)
        {
            case 3:  return _wordsLegendary;
            case 2:  return _wordsHigh;
            case 1:  return _wordsMid;
            default: return _wordsLow;
        }
    }

    // Yeni oyun başlarken havada kalan popup olmasın.
    void HandleRunStarted()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
            _pool.Release(_active[i]);
    }

    void Update()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            ComboPopupItem item = _active[i];

            item.Tick(Time.deltaTime);

            if (item.IsDone) _pool.Release(item);
        }
    }
}
