using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "FruitMerge/Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("Bırakma (Drop)")] 
    
    [Tooltip("iki bırakama arasın min süre (0 olursa 5 meyve üst üste düşer)")]
    public float dropCooldown = 0.45f;
    
    [Tooltip("coodown biterken oyuncunun erken dokunuşu kaç saniye hafızada tutulsun")]
    public float inputBufferTime = 0.25f;

    [Tooltip("bırakma x alt sınırı")] 
    public float dropMinX = -2.2f;
    
    [Tooltip("bırakma x üst sınırı")]
    public float dropMaxX = 2.2f;

    [Tooltip("bekleyen meyvenin yüksekliği")]
    public float dropY = 4.2f;
    
    [Header("game over")]
    
    [Tooltip("ihlal kaç saniye sürerse oyun biter")]
    public float gameOverDelay = 2f;
    
    [Tooltip("yeni bırakılan meyveye dokunulmazlık")]
    public float dropGracePeriod = 1f;

    [Tooltip("'durgun' sayılma hızı eşiği")]
    public float setVelocityThreshold = 0.3f;

    [Tooltip("kaç saniyede bir kontrol edilsin")]
    public float gameOverCheckInterval = 0.1f;

    [Header("fizik")] 
    
    [Tooltip("continuous-discrete geçiş hızı eşiği")]
    public float continuousExitSpeed = 0.5f;
    
    [Tooltip("kaç kere üst üste yavaş olmalı")]
    public int continuousEnterFrames = 5;
    
    [Header("Spawn (Bag Randomizer")]
    [Tooltip("torbada her meyveden kaç kopya")]
    public int bagCopiesPerFruit = 2;

    [Header("combo")]
    [Tooltip("combo zincirinin devam süresi")]
    public float comboWindow = 1.2f;
    
    [Tooltip("her combo adımının çaroan artışı")]
    public float comboMultiplierStep = 0.25f;
    
    [Header("his,cila")]
    [Tooltip("pop animasyonu süresi")]
    public float popDuration = 0.15f;

    [Tooltip("ne kadar şişip geri dönecek")]
    public float popOverShot = 1.12f;

    [Tooltip("hangi boyuttan başlayacak")] 
    public float popStartScale = 0.7f;

    [Header("ses")] [Tooltip("aynı ses kaç saniye içinde tekrar çalmasın")]
    public float sfxRetriggerGuard = 0.06f;

    [Tooltip("kaç ses kanalı yaratılacak")]
    public int audioSourceCount = 6;
}
