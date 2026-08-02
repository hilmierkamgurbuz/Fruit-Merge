using UnityEngine;

[CreateAssetMenu(fileName = "Fruit_00", menuName = "FruitMerge/Fruit Definition")]
public class FruitDefinition : ScriptableObject
{
    [Header("kimlik")] 
    
    [Tooltip("zincirin kaçıncı halkası")]
    public int tier;

    public string displayName = "Cherry";
    
    [Header("görsel")]
    
    [Tooltip("SpriteRenderer.sprite a atanacak")]
    public Sprite sprite = null;
    
    public float scale = 1f;
        
    [Tooltip("SpriteRenderer.color a atanacak")]
    public Color tint = Color.white;

    [Tooltip("meyvenin temsili rengi — sprite beyaz tint ile çalıştığı için UI/gösterge elemanlarında (örn. drop indicator) kullanılır")]
    public Color displayColor = Color.white;

    [Header("fizik")] 
    
    public float mass = 1f;

    [Tooltip("collider yarıçapı, local (prefab) birim. Sprite'ın yarıçapı 0.5; " +
             "sapı/yaprağı dışarıda bırakmak için küçült. Dünya yarıçapı = bu × scale")]
    public float colliderRadius = 0.5f;

    [Tooltip("collider merkezinin sprite merkezine göre kayması, local birim. " +
             "Sap üstteyse gövde merkezi aşağıdadır, yani y negatif olur")]
    public Vector2 colliderOffset = Vector2.zero;

    [Header("oyun")] 
    [Tooltip("bir meyve oluşturduğunda verilecek puan")]
    public int score = 4;
    
    [Tooltip("zincirin bir sonrakşi halkası")]
    public FruitDefinition nextTier;
    
    public bool countForGameOver = true;
    
    [Header("ses")]

    [Tooltip("birleşme sesi")]
    public AudioClip mergeSfx;

    [Header("yüz")]

    [Tooltip("hangi çözünürlük sınıfındaki yüz kullanılacak. Öneri: kiraz/böğürtlen Sm, " +
             "misket/üzüm Md, portakal/elma/şeftali Lg, hindistan cevizi ve üstü Xl")]
    public FaceSize faceSize = FaceSize.Md;

    [Tooltip("yüzün gövdeye göre ince ayarı (local birim). Gövde sprite'ı 470 px'e kırpılı, " +
             "yüz tam tuval — sapı/yaprağı üstte olan meyvelerde yüz birkaç piksel kaymış " +
             "görünürse buradan düzelt")]
    public Vector2 faceOffset = Vector2.zero;
    
}
