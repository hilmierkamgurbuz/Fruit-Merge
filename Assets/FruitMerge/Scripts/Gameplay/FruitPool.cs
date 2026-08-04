using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

[DefaultExecutionOrder(-90)]
public class FruitPool : MonoBehaviour, IPrewarmSource
{
    [Header("Referanslar")]
    [SerializeField] Fruit _prefab;
    [SerializeField] Transform _activeParent;
    [SerializeField] Transform _pooledParent;
    [SerializeField] MergeHandler _mergeHandler;
    [SerializeField] GameConfig _config;

    [Header("Havuz Ayarları")]
    [SerializeField] int _prewarmCount = 40;
    [SerializeField] int _maxSize = 120;

    public static FruitPool Instance { get; private set; }

    ObjectPool<Fruit> _pool;

    readonly List<Fruit> _active = new List<Fruit>(64);

    public IReadOnlyList<Fruit> Active => _active;

    public Transform ActiveParent => _activeParent;

    int _counter;

    int _prewarmDone;

    public int PrewarmTotal => _prewarmCount;

    public int PrewarmDone => _prewarmDone;

    void Awake()
    {
        Instance = this;

        _pool = new ObjectPool<Fruit>(
            createFunc:       CreateFruit,
            actionOnGet:      OnGetFruit,
            actionOnRelease:  OnReleaseFruit,
            actionOnDestroy:  OnDestroyFruit,
            collectionCheck:  true,
            defaultCapacity:  _prewarmCount,
            maxSize:          _maxSize
        );

        // Isıtma ARTIK BURADA DEĞİL. 40 Instantiate tek karede yapılınca ilk kare
        // (açılış ekranı) o kadar geç geliyordu. Aynı iş SplashPanel yükleme çubuğunu
        // doldururken karelere yayılıyor — bkz. PrewarmQueue.
        PrewarmQueue.Register(this);
    }

    void OnEnable()
    {
        GameEvents.OnGameOver += HandleGameOver;
    }

    void OnDisable()
    {
        GameEvents.OnGameOver -= HandleGameOver;
    }

    void OnDestroy()
    {
        PrewarmQueue.Unregister(this);
        if (Instance == this) Instance = null;
        _pool?.Dispose();
    }

    /// <summary>
    /// Oyun bitti: tahta olduğu gibi kalsın. Eskiden fizik çalışmaya devam ediyordu ve
    /// sonuç ekranı açıkken yığın arkada kaymaya, yerleşmeye, hatta birleşmeye devam
    /// ediyordu.
    ///
    /// Aboneliği tahtanın sahibi olan havuz taşıyor: aktif meyve listesi burada, başka
    /// bir sistemin bu listeyi dolaşması için ödünç alması gerekirdi.
    /// </summary>
    void HandleGameOver(int finalScore) => FreezeAll();

    Fruit CreateFruit()
    {
        var f = Instantiate(_prefab, _pooledParent);
        f.name = $"Fruit_{_counter++:D3}";
        f.Bind(_mergeHandler, _config);
        f.gameObject.SetActive(false);
        return f;
    }

    void OnGetFruit(Fruit f)
    {
        f.transform.SetParent(_activeParent, false);
        f.gameObject.SetActive(true);
        f.ResetState();
        _active.Add(f);
    }

    void OnReleaseFruit(Fruit f)
    {
        _active.Remove(f);
        f.Body.simulated = false;
        f.transform.SetParent(_pooledParent, false);
        f.gameObject.SetActive(false);
    }

    void OnDestroyFruit(Fruit f)
    {
        if (f) Destroy(f.gameObject);
    }

    public Fruit Spawn(FruitDefinition def, Vector2 position)
    {
        var f = _pool.Get();
        f.transform.position = position;
        f.Initialize(def);
        return f;
    }

    public void Despawn(Fruit f)
    {
        if (f == null) return;
        if (!_active.Contains(f)) return;
        _pool.Release(f);
    }

    public void DespawnAll()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
            _pool.Release(_active[i]);
    }

    /// <summary>
    /// Tahtadaki bütün meyveleri oldukları yerde dondurur (bkz. <see cref="Fruit.Freeze"/>).
    /// Geri alma yolu yok — çözülme havuz döngüsünde kendiliğinden oluyor: yeni oyunda
    /// meyveler havuza dönüp yeniden doğuyor.
    /// </summary>
    public void FreezeAll()
    {
        for (int i = 0; i < _active.Count; i++)
            if (_active[i] != null) _active[i].Freeze();
    }

    /// <summary>
    /// Bu karede en fazla <paramref name="budget"/> meyve yarat (bkz. <see cref="PrewarmQueue"/>).
    ///
    /// <c>Get()</c> + geçici dizi + toplu <c>Release()</c> yerine doğrudan
    /// <c>Release(CreateFruit())</c>: eskiden ısıtma tek karede bittiği için aradan
    /// fizik adımı geçmiyordu, şimdi geçiyor — <c>Get()</c> meyveleri AKTİF hale
    /// getirdiği için ekranda birkaç kare boyunca görünür/uyanır olurlardı. Ayrıca
    /// geçici diziyi de ortadan kaldırıyor (allocation yok).
    /// </summary>
    public void PrewarmStep(int budget)
    {
        if (budget <= 0) return;

        int end = Mathf.Min(_prewarmDone + budget, _prewarmCount);

        for (int i = _prewarmDone; i < end; i++) _pool.Release(CreateFruit());

        _prewarmDone = end;
    }
}