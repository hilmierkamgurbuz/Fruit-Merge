using UnityEngine;

/// <summary>
/// Oyun sonu coin ödülünü sipariş eder: kazanılan her yıldız için sabit bir miktar,
/// tahtada kalan her <b>ödüllü</b> meyve için o meyvenin kendi değeri.
///
/// Paralar artık EKRANIN ORTASINDAN kalkan iki ayrı patlama olarak akıyor (bkz.
/// <see cref="CoinFlyDirector.SpawnBurst"/>) — yıldızların/meyvelerin üstünden kalkmıyor,
/// çünkü tek katmanlı merkez patlaması hem daha okunaklı hem de panel/HUD hiyerarşisine
/// bağımlı değil. Meyve ödülü bu değişiklikte KAYBOLMADI: sadece kalkış noktası değişti,
/// <see cref="FruitDefinition.coinReward"/> toplamı aynen ikinci patlamanın değerine giriyor.
///
/// Hangi meyvenin kaç para ettiği <see cref="FruitDefinition.coinReward"/>'da duruyor,
/// burada tier listesi YOK: zincire yeni bir meyve eklenince bu script'e dokunulmuyor.
///
/// Neden <see cref="GameEvents.OnGameOver"/> değil de
/// <see cref="GameEvents.OnStarsRevealed"/>: paralar yıldızlar yerine oturduktan sonra
/// akmaya başlamalı, yoksa oyuncu henüz dolmamış yıldızların üstüne binen bir para
/// akışı görür ve "az önce ne oldu" ayrımını kaybeder.
///
/// Kendi <c>Update</c>'i yok — kalkış gecikmelerini <see cref="CoinFlyDirector"/>
/// zaten kendi döngüsünde sayıyor, burası sadece siparişi veriyor (kural 7, 8).
/// </summary>
public class CoinRewardDirector : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] GameConfig _config;

    [SerializeField] CoinFlyDirector _fly;

    void OnEnable()
    {
        GameEvents.OnStarsRevealed += HandleStarsRevealed;
    }

    void OnDisable()
    {
        GameEvents.OnStarsRevealed -= HandleStarsRevealed;
    }

    void HandleStarsRevealed(int starCount)
    {
        if (_fly == null) return;

        float delay   = _config != null ? _config.coinPayoutDelay   : 0.35f;
        float stagger = _config != null ? _config.coinPayoutStagger : 0.09f;

        int perStar = _config != null ? _config.coinPerStar : 10;

        int starTotal = Mathf.Max(0, starCount) * perStar;

        // Meyve patlaması, yıldız patlamasının son parası kalkana kadar beklemeli —
        // yoksa iki ödül tek bir kalabalığa karışır ve oyuncu "neyi kazandım" ayrımını
        // kaybeder. Kaç para kalktığı (coinBurstCount ile totalValue'nun küçüğü) ve
        // dolayısıyla son paranın ne zaman kalktığı SpawnBurst'ün kendi kırpma kuralına
        // bağlı — o kuralı burada tekrarlamak (ör. starTotal == 0 iken hâlâ tam süre
        // beklemek) eski hatanın kaynağıydı. SpawnBurst'ün döndürdüğü kuyruk anını
        // olduğu gibi kullanıyoruz.
        float starTail = _fly.SpawnBurst(starTotal, delay);

        int fruitTotal = FruitCoinTotal();

        _fly.SpawnBurst(fruitTotal, starTail + stagger);
    }

    /// <summary>
    /// Tahtada kalan aktif meyvelerin ödül toplamı. LINQ yok (kural 11) — basit bir
    /// <c>for</c>, <c>null</c> kontrolleri korunuyor.
    /// </summary>
    int FruitCoinTotal()
    {
        var pool = FruitPool.Instance;

        if (pool == null) return 0;

        var active = pool.Active;

        int total = 0;

        for (int i = 0; i < active.Count; i++)
        {
            var fruit = active[i];

            if (fruit == null || fruit.Definition == null) continue;

            // Havuzun aktif listesi DALDAKİ bekleyen meyveyi de içeriyor (DropController
            // onu _pool.Spawn ile alıyor). Oyuncunun hiç bırakmadığı meyve ödül vermemeli.
            // GameOverDetector, QuakeBoostDirector ve WormBoostDirector aynı ayrımı
            // yapıyor; tek atlayan yer burasıydı.
            if (!fruit.IsDropped) continue;

            total += fruit.Definition.coinReward;
        }

        return total;
    }
}
