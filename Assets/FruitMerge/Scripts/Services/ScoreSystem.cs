using UnityEngine;

public class ScoreSystem : MonoBehaviour
{
    [SerializeField] GameConfig _config;

    public static ScoreSystem Instance { get; private set; }

    public int Score { get; private set; }
    public int Combo { get; private set; }

    float _lastMergeTime = -999f;

    void Awake() => Instance = this;

    void OnDestroy() { if (Instance == this) Instance = null; }

    void OnEnable()
    {
        GameEvents.OnMerged       += HandleMerged;
        GameEvents.OnMaxTierMerged += HandleMaxTier;
        GameEvents.OnRunStarted   += HandleRunStarted;
    }

    void OnDisable()
    {
        GameEvents.OnMerged       -= HandleMerged;
        GameEvents.OnMaxTierMerged -= HandleMaxTier;
        GameEvents.OnRunStarted   -= HandleRunStarted;
    }

    void HandleMerged(FruitDefinition produced, Vector2 pos)
    {
        if (Time.time - _lastMergeTime <= _config.comboWindow) Combo++;
        else                                                    Combo = 1;

        _lastMergeTime = Time.time;

        float multiplier = 1f + (Combo - 1) * _config.comboMultiplierStep;

        Score += Mathf.RoundToInt(produced.score * multiplier);

        GameEvents.RaiseScoreChanged(Score);
        GameEvents.RaiseComboChanged(Combo);
        GameEvents.RaiseComboMerge(produced, pos, Combo);
    }

    void HandleMaxTier(FruitDefinition def, Vector2 pos)
    {
        Score += def.score * 5;
        Combo = 0;

        GameEvents.RaiseScoreChanged(Score);
        GameEvents.RaiseComboChanged(Combo);
    }

    /// <summary>
    /// Sadece YENİ oyunda çalışır. Eskiden OnStateChanged(Playing)'e bağlıydı ve
    /// Resume() da Playing'e geçtiği için pause'dan dönüşte skoru sıfırlıyordu.
    /// </summary>
    void HandleRunStarted()
    {
        Score = 0;
        Combo = 0;
        _lastMergeTime = -999f;

        GameEvents.RaiseScoreChanged(0);
        GameEvents.RaiseComboChanged(0);
    }
}