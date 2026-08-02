using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class SpawnQueue : MonoBehaviour
{
    [SerializeField] FruitDatabase _database;
    [SerializeField] GameConfig _config;

    [Tooltip("Kaç adım ileriyi göstereceğiz. 1 = sadece 'sıradaki'.")]
    [SerializeField] int _previewDepth = 1;

    readonly List<FruitDefinition> _bag = new List<FruitDefinition>(32);

    readonly Queue<FruitDefinition> _preview = new Queue<FruitDefinition>(4);

    void Awake()
    {
        RefillBag();

        for (int i = 0; i < _previewDepth + 1; i++)
            _preview.Enqueue(DrawFromBag());
    }

    public FruitDefinition Peek() => _preview.Peek();

    public FruitDefinition Next()
    {
        var result = _preview.Dequeue();

        _preview.Enqueue(DrawFromBag());

        GameEvents.RaiseNextFruitChanged(Peek());

        return result;
    }

    FruitDefinition DrawFromBag()
    {
        if (_bag.Count == 0) RefillBag();

        int last = _bag.Count - 1;
        var item = _bag[last];
        _bag.RemoveAt(last);
        return item;
    }

    void RefillBag()
    {
        _bag.Clear();

        int count = Mathf.Min(_database.spawnableCount, _database.fruits.Count);

        for (int tier = 0; tier < count; tier++)
        {
            var def = _database.GetByTier(tier);

            if (def == null) continue;

            for (int c = 0; c < _config.bagCopiesPerFruit; c++)
                _bag.Add(def);
        }

        Shuffle(_bag);
    }

    static void Shuffle(List<FruitDefinition> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}