using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Evrim zinciri şeridi: FruitDatabase'deki 11 meyveyi tier sırasıyla gösterir.
/// Ulaşılan en yüksek tier'a kadar meyve ikonu tam görünür, gerisi silik.
/// Sadece merge olaylarında günceller — Update yok.
/// Yerleşim tamamen HorizontalLayoutGroup'a ait; bu script sadece alpha'ları sürer.
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

    void Start() => BuildInitialState();

    void HandleRunStarted() => BuildInitialState();

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
