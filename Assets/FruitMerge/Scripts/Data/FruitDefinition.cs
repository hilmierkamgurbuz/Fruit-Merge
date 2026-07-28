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

    [Header("fizik")] 
    
    public float mass = 1f;

    [Header("oyun")] 
    [Tooltip("bir meyve oluşturduğunda verilecek puan")]
    public int score = 4;
    
    [Tooltip("zincirin bir sonrakşi halkası")]
    public FruitDefinition nextTier;
    
    public bool countForGameOver = true;
    
    [Header("ses")]
    
    [Tooltip("birleşme sesi")]
    public AudioClip mergeSfx;
    
}
