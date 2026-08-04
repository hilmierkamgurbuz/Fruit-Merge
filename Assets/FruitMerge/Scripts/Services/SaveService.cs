using System;
using System.IO;
using UnityEngine;

public class SaveService : MonoBehaviour
{
    /// <summary>Kayıt şemasının güncel sürümü. Alan eklendiğinde artır ve Migrate'e adım yaz.</summary>
    public const int CurrentVersion = 3;

    [Serializable]
    public class SaveData
    {
        public int version = CurrentVersion;
        public int highScore;
        public int totalMerges;
        public int gamesPlayed;

        // --- ayarlar (v2'de eklendi) ---
        public bool sfxOn = true;
        public bool musicOn = true;
        public bool vibrationOn = true;

        // --- cüzdan (v3'te eklendi) ---
        public int coins;
    }

    public static SaveService Instance { get; private set; }

    SaveData _data;
    bool _isDirty;
    string _path;

    public int  HighScore   => _data.highScore;
    public bool SfxOn       => _data.sfxOn;
    public bool MusicOn     => _data.musicOn;
    public bool VibrationOn => _data.vibrationOn;
    public int  Coins       => _data.coins;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _path = Path.Combine(Application.persistentDataPath, "save.json");
        Load();
    }

    void OnEnable()
    {
        GameEvents.OnGameOver += HandleGameOver;
        GameEvents.OnMerged   += HandleMerged;
    }

    void OnDisable()
    {
        GameEvents.OnGameOver -= HandleGameOver;
        GameEvents.OnMerged   -= HandleMerged;
    }

    void Load()
    {
        if (!File.Exists(_path))
        {
            _data = new SaveData();
        }
        else
        {
            try
            {
                string json = File.ReadAllText(_path);
                _data = JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] Kayıt okunamadı, sıfırdan başlanıyor: {e.Message}");
                _data = new SaveData();
            }
        }

        Migrate();
    }

    /// <summary>
    /// Kayıttaki değerleri yayınlar.
    ///
    /// Awake'te DEĞİL Start'ta: Awake sırasında yayınlarsak, execution order'ı bizden
    /// büyük olan dinleyicilerin (HUDView 100, biz 0) OnEnable'ı henüz çalışmamış olur
    /// ve olay boşa gider. Rekor bu yüzden HUD'da hep 0 görünüyordu. Tüm OnEnable'lar
    /// Start'lardan önce bittiği için burada yayınlamak güvenli.
    /// </summary>
    void Start()
    {
        GameEvents.RaiseHighScoreChanged(_data.highScore);
        GameEvents.RaiseCoinsChanged(_data.coins);
    }

    /// <summary>
    /// Eski kayıtları güncel şemaya taşır. JsonUtility'nin eksik alanları nasıl
    /// doldurduğuna güvenmiyoruz — sürüm bazlı açıkça yazıyoruz.
    /// </summary>
    void Migrate()
    {
        if (_data.version >= CurrentVersion) return;

        if (_data.version < 2)
        {
            // v1'de ayar alanı yoktu — mevcut oyuncu her şeyi açık bulsun
            _data.sfxOn = true;
            _data.musicOn = true;
            _data.vibrationOn = true;
        }

        if (_data.version < 3)
        {
            // v2'de cüzdan yoktu. Mevcut oyuncu sıfır coin ile başlıyor — ilk oyun
            // sonunda zaten yıldız/meyve ödülü geliyor, ayrıca hediye vermiyoruz.
            _data.coins = 0;
        }

        _data.version = CurrentVersion;
        _isDirty = true;
        Save();
    }

    public void SetSfxOn(bool value)
    {
        if (_data.sfxOn == value) return;

        _data.sfxOn = value;
        _isDirty = true;
        Save();

        GameEvents.RaiseSettingsChanged();
    }

    public void SetMusicOn(bool value)
    {
        if (_data.musicOn == value) return;

        _data.musicOn = value;
        _isDirty = true;
        Save();

        GameEvents.RaiseSettingsChanged();
    }

    public void SetVibrationOn(bool value)
    {
        if (_data.vibrationOn == value) return;

        _data.vibrationOn = value;
        _isDirty = true;
        Save();

        GameEvents.RaiseSettingsChanged();
    }

    // ---------------------------------------------------------------- cüzdan

    /// <summary>
    /// Coin ekler ve yeni toplamı yayınlar. Oyun sonu ödülü coin coin damladığı için
    /// (uçan her para ayrı ayrı iniyor) bu metot bir oyunda onlarca kez çağrılabiliyor —
    /// bu yüzden diske YAZMIYOR, sadece kirli işaretliyor. Gerçek yazma
    /// <see cref="OnApplicationPause"/> / <see cref="OnApplicationQuit"/> ve oyun sonunda
    /// zaten olan <see cref="HandleGameOver"/> kaydında oluyor.
    /// </summary>
    public void AddCoins(int amount)
    {
        if (amount <= 0) return;

        _data.coins += amount;
        _isDirty = true;

        GameEvents.RaiseCoinsChanged(_data.coins);
    }

    /// <summary>
    /// Para yeterse harcar. Satın alma nadir ve geri alınamaz olduğu için burada
    /// <see cref="AddCoins"/>'in aksine ANINDA diske yazıyoruz: oyuncu boost'u alıp
    /// uygulamayı öldürürse parası geri gelmemeli.
    /// </summary>
    /// <returns>harcanabildiyse <c>true</c></returns>
    public bool TrySpendCoins(int amount)
    {
        if (amount < 0 || _data.coins < amount) return false;

        _data.coins -= amount;
        _isDirty = true;
        Save();

        GameEvents.RaiseCoinsChanged(_data.coins);

        return true;
    }

    void HandleMerged(FruitDefinition def, Vector2 pos)
    {
        _data.totalMerges++;
        _isDirty = true;
    }

    void HandleGameOver(int finalScore)
    {
        _data.gamesPlayed++;

        if (finalScore > _data.highScore)
        {
            _data.highScore = finalScore;
            GameEvents.RaiseHighScoreChanged(finalScore);
            GameEvents.RaiseNewRecord(finalScore);
        }

        _isDirty = true;
        Save();
    }

    public void Save()
    {
        if (!_isDirty) return;

        try
        {
            File.WriteAllText(_path, JsonUtility.ToJson(_data, true));
            _isDirty = false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveService] Kayıt yazılamadı: {e.Message}");
        }
    }

    void OnApplicationPause(bool paused) { if (paused) Save(); }
    void OnApplicationFocus(bool focused) { if (!focused) Save(); }
    void OnApplicationQuit() => Save();

    void OnDestroy() { Save(); if (Instance == this) Instance = null; }
}
