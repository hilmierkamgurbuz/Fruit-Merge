using UnityEngine;

public class ScoreSystem : MonoBehaviour
{
    [SerializeField] GameConfig _config;

    public static ScoreSystem Instance { get; private set; }

    public int Score { get; private set; }
    public int Combo { get; private set; }

    float _lastMergeTime = -999f;

    // Kopya koruması: kod tabanındaki bütün diğer singleton'lar (GameManager, FaceDirector,
    // AudioService, EffectDirector, CoinFlyDirector…) bunu yapıyor. Bu sınıf atlamıştı ve
    // sahnede kopya kalırsa sessizce ikincisi kazanıyordu — iki ScoreSystem OnMerged'e
    // ayrı ayrı abone olduğu için skor da iki kat artardı.
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("ScoreSystem: sahnede ikinci kopya var, bu obje yok ediliyor.", this);

            Destroy(gameObject);

            return;
        }

        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void OnEnable()
    {
        // Awake'te yok edilmeye işaretlenen kopya abone olmasın — yoksa skor iki kat artar.
        // (AudioService / HapticService / EffectDirector ile aynı desen.)
        if (Instance != this) return;

        GameEvents.OnMerged       += HandleMerged;
        GameEvents.OnMaxTierMerged += HandleMaxTier;
        GameEvents.OnRunStarted   += HandleRunStarted;
    }

    void OnDisable()
    {
        if (Instance != this) return;

        GameEvents.OnMerged       -= HandleMerged;
        GameEvents.OnMaxTierMerged -= HandleMaxTier;
        GameEvents.OnRunStarted   -= HandleRunStarted;
    }

    void HandleMerged(FruitDefinition produced, Vector2 pos)
    {
        if (_config == null || produced == null) return;

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
        if (def == null) return;

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