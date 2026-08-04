using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class GameEvents
{
    public static event Action<FruitDefinition, Vector2> OnMerged;
    
    public static event Action<FruitDefinition, Vector2> OnMaxTierMerged;
    
    public static event Action<FruitDefinition> OnFruitDropped;
    
    public static event Action<FruitDefinition> OnNextFruitChanged;
    
    public static event Action<int> OnScoreChanged;
    
    public static event Action<int> OnHighScoreChanged;
    
    public static event Action<int> OnComboChanged;

    /// <summary>
    /// Nitelikli bir birleşme oldu: üretilen meyve, birleşme noktası VE o anki combo
    /// sayısı bir arada. OnMerged + OnComboChanged'i ayrı ayrı dinleyip birleştirmek
    /// abone sırasına güvenmek anlamına gelirdi (OnNewRecord'daki gibi garanti değil) —
    /// ScoreSystem üçünü de aynı anda, kesin doğru haliyle burada yayınlıyor.
    /// </summary>
    public static event Action<FruitDefinition, Vector2, int> OnComboMerge;

    public static event Action<GameState> OnStateChanged;
    
    public static event Action<int> OnGameOver;

    // ses/müzik/titreşim ayarlarından biri değişti — abone, değeri SaveService'ten okur
    public static event Action OnSettingsChanged;

    /// <summary>
    /// YENİ bir oyun başladı. Pause'dan dönüş bunu tetiklemez — skor sıfırlama gibi
    /// "oyuna sıfırdan başla" işleri OnStateChanged(Playing) yerine buna bağlanmalı,
    /// çünkü Resume() de Playing'e geçiyor.
    /// </summary>
    public static event Action OnRunStarted;

    /// <summary>
    /// Bu oyunda rekor kırıldı. OnGameOver'ın abone sırası garanti olmadığı için
    /// sonuç ekranı "skorum rekoru geçti mi" karşılaştırmasını kendi yapamaz —
    /// SaveService kesin bilgiyi buradan yayınlıyor.
    /// </summary>
    public static event Action<int> OnNewRecord;

    /// <summary>
    /// Bir boost'un durumu değişti: HANGİ boost, silahlandı mı (hedef bekleniyor) ve kaç
    /// kullanım kaldı. HUD butonu tek olaydan beslensin diye üçü bir arada — ayrı ayrı
    /// yayınlamak abone sırasına güvenmek olurdu.
    ///
    /// <see cref="BoostId"/> imzada olduğu için TEK <see cref="BoostButton"/> script'i
    /// bütün boost butonlarına hizmet ediyor: her buton kendi id'si dışındakini eliyor.
    /// </summary>
    public static event Action<BoostId, bool, int> OnBoostStateChanged;

    /// <summary>
    /// Bir boost'un kullanımı bitmişken butonuna basıldı — mağaza paneli açılmalı.
    /// <see cref="BoostButton"/> paneli doğrudan çağırmıyor: buton HUD'da, panel
    /// PanelCanvas'ta; ikisini birbirine bağlamak yerine olayla konuşuyorlar.
    /// </summary>
    public static event Action<BoostId> OnBoostShopRequested;

    /// <summary>
    /// Boost mağazası açıldı / kapandı. Cüzdan HUD'ı oynanış sırasında gizli duruyor
    /// ama mağaza açıkken görünmesi ŞART — oyuncu neyle ödeyeceğini görmeli. Panelin
    /// kendisi yerine olay dinleniyor, böylece HUD panelin varlığını bilmiyor.
    /// </summary>
    public static event Action<bool> OnBoostShopToggled;

    /// <summary>Cüzdandaki toplam coin değişti. HUD sayacı bunu dinliyor.</summary>
    public static event Action<int> OnCoinsChanged;

    /// <summary>
    /// Sonuç ekranındaki yıldız gösterimi BİTTİ — kaç yıldız kazanıldığı kesinleşti.
    /// Coin ödülü buna bağlanıyor: <see cref="OnGameOver"/>'a bağlansaydı coin'ler
    /// yıldızlar daha yerine oturmadan uçmaya başlardı.
    ///
    /// Yıldız kazanılmasa da (0) yayınlanıyor — meyve ödülü yine de verilecek.
    /// </summary>
    public static event Action<int> OnStarsRevealed;

    /// <summary>Bir meyve kurtçuklar tarafından yendi: yenen tanım + konumu.</summary>
    public static event Action<FruitDefinition, Vector2> OnFruitEaten;

    /// <summary>
    /// Deprem boost'u başladı. Ses, titreşim ve telemetri kancası — director'ün kendisi
    /// <see cref="AudioService"/>'i doğrudan çağırmak yerine bunu yayınlıyor, böylece
    /// depremi duyan başka sistemler (ileride görev/başarım) director'e bağlanmıyor.
    /// </summary>
    public static event Action OnQuakeStarted;

    /// <summary>
    /// Kurtçuklar çiğnemeye başladı / bitirdi. <see cref="OnFruitEaten"/> tek bir AN
    /// (meyve yok oldu), bu ise SÜREÇ: kemirme titreşimi meyve eriyene kadar sürüyor.
    /// İkisi ayrı olmak zorunda — biri trenin motoru, öteki finalindeki tek vuruş.
    /// </summary>
    public static event Action<bool> OnWormsChewingChanged;

    public static void RaiseMerged(FruitDefinition yeni_uretilen, Vector2 konum) => OnMerged?.Invoke(yeni_uretilen,konum);

    public static void RaiseMaxTierMerged(FruitDefinition fruit, Vector2 konum)  => OnMaxTierMerged?.Invoke(fruit, konum);
    
    public static void RaiseFruitDropped(FruitDefinition fruit) => OnFruitDropped?.Invoke(fruit);
    
    public static void RaiseNextFruitChanged(FruitDefinition fruit) => OnNextFruitChanged?.Invoke(fruit);
    
    public static void RaiseScoreChanged(int score) => OnScoreChanged?.Invoke(score);
    
    public static void RaiseHighScoreChanged(int score) => OnHighScoreChanged?.Invoke(score);
    
    public static void RaiseComboChanged(int combo) => OnComboChanged?.Invoke(combo);

    public static void RaiseComboMerge(FruitDefinition produced, Vector2 position, int combo) =>
        OnComboMerge?.Invoke(produced, position, combo);
    
    public static void RaiseStateChanged(GameState state) => OnStateChanged?.Invoke(state);
    
    public static void RaiseGameOver(int score) => OnGameOver?.Invoke(score);

    public static void RaiseSettingsChanged() => OnSettingsChanged?.Invoke();

    public static void RaiseRunStarted() => OnRunStarted?.Invoke();

    public static void RaiseNewRecord(int score) => OnNewRecord?.Invoke(score);

    public static void RaiseBoostStateChanged(BoostId id, bool armed, int charges) =>
        OnBoostStateChanged?.Invoke(id, armed, charges);

    public static void RaiseBoostShopRequested(BoostId id) => OnBoostShopRequested?.Invoke(id);

    public static void RaiseBoostShopToggled(bool open) => OnBoostShopToggled?.Invoke(open);

    public static void RaiseCoinsChanged(int total) => OnCoinsChanged?.Invoke(total);

    public static void RaiseStarsRevealed(int starCount) => OnStarsRevealed?.Invoke(starCount);

    public static void RaiseFruitEaten(FruitDefinition def, Vector2 position) =>
        OnFruitEaten?.Invoke(def, position);

    public static void RaiseQuakeStarted() => OnQuakeStarted?.Invoke();

    public static void RaiseWormsChewingChanged(bool chewing) => OnWormsChewingChanged?.Invoke(chewing);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]

    static void ResetStatics()
    {
        OnMerged = null;
        OnMaxTierMerged = null;
        OnFruitDropped = null;
        OnNextFruitChanged = null;
        OnScoreChanged = null;
        OnHighScoreChanged = null;
        OnComboChanged = null;
        OnComboMerge = null;
        OnStateChanged = null;
        OnGameOver = null;
        OnSettingsChanged = null;
        OnRunStarted = null;
        OnNewRecord = null;
        OnBoostStateChanged = null;
        OnBoostShopRequested = null;
        OnBoostShopToggled = null;
        OnCoinsChanged = null;
        OnStarsRevealed = null;
        OnFruitEaten = null;
        OnQuakeStarted = null;
        OnWormsChewingChanged = null;
    }



}
