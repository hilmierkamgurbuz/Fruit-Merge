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
    /// Kurtçuk boost'unun durumu değişti: silahlandı mı (hedef bekleniyor) ve kaç
    /// kullanım kaldı. HUD butonu tek olaydan beslensin diye ikisi bir arada —
    /// ayrı ayrı yayınlamak abone sırasına güvenmek olurdu.
    /// </summary>
    public static event Action<bool, int> OnWormsBoostStateChanged;

    /// <summary>Bir meyve kurtçuklar tarafından yendi: yenen tanım + konumu.</summary>
    public static event Action<FruitDefinition, Vector2> OnFruitEaten;

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

    public static void RaiseWormsBoostStateChanged(bool armed, int charges) =>
        OnWormsBoostStateChanged?.Invoke(armed, charges);

    public static void RaiseFruitEaten(FruitDefinition def, Vector2 position) =>
        OnFruitEaten?.Invoke(def, position);

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
        OnWormsBoostStateChanged = null;
        OnFruitEaten = null;
        
        
    }



}
