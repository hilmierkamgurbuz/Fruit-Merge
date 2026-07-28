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

    public static event Action<GameState> OnStateChanged;
    
    public static event Action<int> OnGameOver;

    public static void RaiseMerged(FruitDefinition yeni_uretilen, Vector2 konum) => OnMerged?.Invoke(yeni_uretilen,konum);

    public static void RaiseMaxTierMerged(FruitDefinition fruit, Vector2 konum)  => OnMaxTierMerged?.Invoke(fruit, konum);
    
    public static void RaiseFruitDropped(FruitDefinition fruit) => OnFruitDropped?.Invoke(fruit);
    
    public static void RaiseNextFruitChanged(FruitDefinition fruit) => OnNextFruitChanged?.Invoke(fruit);
    
    public static void RaiseScoreChanged(int score) => OnScoreChanged?.Invoke(score);
    
    public static void RaiseHighScoreChanged(int score) => OnHighScoreChanged?.Invoke(score);
    
    public static void RaiseComboChanged(int combo) => OnComboChanged?.Invoke(combo);
    
    public static void RaiseStateChanged(GameState state) => OnStateChanged?.Invoke(state);
    
    public static void RaiseGameOver(int score) => OnGameOver?.Invoke(score);

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
        OnStateChanged = null;
        OnGameOver = null;
        
        
    }



}
