using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Sonuç ekranındaki başlığı harf harf meyve renklerine boyar. Başlık artık bir görsel
/// değil TMP metni: "OVERFLOWING" 11 harf, oyunda da tam 11 meyve var (tier 0 Cherry →
/// tier 10 Watermelon) — birebir eşleşiyor. Harf <c>i</c>, <see cref="FruitDatabase.GetByTier"/>
/// ile tier <c>i</c>'nin <see cref="FruitDefinition.displayColor"/>'ını alıyor.
///
/// Renkler sahneye elle <c>&lt;color=#HEX&gt;</c> olarak GÖMÜLMEZ: tek doğru kaynak
/// <see cref="FruitDefinition.displayColor"/>. Sahnedeki metne rengi sabit yazsaydık,
/// bir meyvenin rengi değiştiği gün başlık sessizce bayatlardı ve kimse fark etmezdi.
/// Aynı gerekçe bu kod tabanında başka yerde de uygulanmış — <see cref="CoinRewardDirector"/>
/// da tier listesi tutmuyor, ödülü <see cref="FruitDefinition.coinReward"/>'dan okuyor.
///
/// Kontur (outline) rengi harf renklerinden ETKİLENMEZ: TMP'nin SDF shader'ında vertex
/// rengi yalnızca YÜZÜ (face) çarpar, kontur kendi <c>_OutlineColor</c>'ından gelir ve
/// yalnızca vertex ALFASIYLA çarpılır — bu yüzden beyaz kenarlık harf renklerine rağmen
/// beyaz kalır. "Renk verdim kontur neden değişmedi" sorusunun cevabı budur.
/// </summary>
[DefaultExecutionOrder(100)]   // TMP'nin kendi kurulumundan sonra
public class FruitColorTitle : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] TextMeshProUGUI _label;
    [SerializeField] FruitDatabase _database;

    [Header("Metin")]
    [Tooltip("BÜYÜK harflerle yazılmalı — renklendirme harf harf yapıldığı için TMP'nin " +
             "UpperCase stiline bırakmak yerine kelime doğrudan büyük yazılıyor.\n\n" +
             "Kelime meyve sayısından uzunsa renkler başa dönerek tekrar eder; boşluklar " +
             "renk harcamaz")]
    [SerializeField] string _word = "OVERFLOWING";

    // Her çağrıda yeni string birleştirme yapmamak için paylaşımlı (kural 11) —
    // SetText(StringBuilder) aşırı yüklemesiyle yazılıyor, ToString() ile ekstra
    // tahsis yapılmıyor.
    readonly StringBuilder _sb = new StringBuilder(160);

    void Awake()
    {
        Apply();
    }

    /// <summary>
    /// <see cref="_word"/>'ü harf harf meyve renklerine boyayıp <see cref="_label"/>'a yazar.
    /// </summary>
    public void Apply()
    {
        if (_label == null || _database == null)
        {
            Debug.LogWarning("[FruitColorTitle] _label veya _database atanmamış, mevcut metin dokunulmadan bırakıldı.", this);
            return;
        }

        int fruitCount = _database.MaxTier + 1;

        if (fruitCount <= 0)
        {
            Debug.LogWarning("[FruitColorTitle] Veritabanında hiç meyve yok, mevcut metin dokunulmadan bırakıldı.", this);
            return;
        }

        _sb.Clear();

        int colorIndex = 0;

        for (int i = 0; i < _word.Length; i++)
        {
            char ch = _word[i];

            if (char.IsWhiteSpace(ch))
            {
                // boşluk bir meyve harcamasın — renk sayacı artmıyor
                _sb.Append(ch);
                continue;
            }

            var def = _database.GetByTier(colorIndex % fruitCount);

            if (def != null)
            {
                // RGBA değil RGB: alfayı 1'de tutuyoruz, meyve renginin alfası düşükse
                // başlık yarı saydam olurdu — burada sadece harf renginin göstergesi lazım
                _sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(def.displayColor))
                   .Append('>').Append(ch).Append("</color>");
            }
            else
            {
                // veritabanında boş slot olabilir (bkz. FruitDatabase.OnValidate)
                // — çökmek yerine harfi düz, renksiz ekle
                _sb.Append(ch);
            }

            colorIndex++;
        }

        _label.SetText(_sb);
    }

    // Play Mode'a girmeden Inspector'dan sağ tıkla önizleme için: renkli hali edit
    // mode'da görülebilmeli, yoksa punto/kontur ayarı yaparken her denemede oyunu
    // başlatmak gerekirdi.
    [ContextMenu("Renkleri Uygula")]
    void ApplyFromContextMenu() => Apply();
}
