using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "FruitMerge/Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("Bırakma (Drop)")] 
    
    [Tooltip("bırakma anında yatay mikro sapma — kusursuz kule kurulmasını önler")]                                                                                                    
    public float dropJitterX = 0.04f;                                                                                                                                                  
                                                                                                                                                                                     
    [Tooltip("bırakma anında rastgele dönüş (derece/sn)")]                                                                                                                             
    public float dropSpin = 30f;
    
    [Tooltip("iki bırakama arasın min süre (0 olursa 5 meyve üst üste düşer)")]
    public float dropCooldown = 0.45f;
    
    [Tooltip("coodown biterken oyuncunun erken dokunuşu kaç saniye hafızada tutulsun")]
    public float inputBufferTime = 0.25f;

    [Tooltip("duvarların iç yüzü: |Wall_Right.x| - size.x/2")]                                                                                                                         
    public float wallInnerX = 3.08f;                                                                                                                                                   
                                                                                                                                                                                     
    [Tooltip("duvara bırakılan temas payı")]                                                                                                                                           
    public float dropEdgePadding = 0.02f;        

    [Tooltip("bekleyen meyvenin yüksekliği")]
    public float dropY = 4.2f;

    [Tooltip("dalın sapının ucunun dropY'ye göre local yüksekliği. Bekleyen meyve TEPESİ " +
             "buraya değecek şekilde asılır — küçük meyve yukarıda, büyük meyve aşağıda durur. " +
             "Sabit merkez kullanılsa kiraz daldan kopuk görünüyordu")]
    public float dropperTwigTipY = 0.25f;

    [Tooltip("bekleyen meyvenin alt kenarı ile göstergenin başladığı nokta arasındaki pay")]
    public float dropIndicatorSkin = 0.02f;

    [Tooltip("bırakılan meyve ile yeni bekleyen meyve arasında kalacak boşluk (dünya birimi). " +
             "Gereken düşüş = iki meyvenin yarıçapları + bu pay. Yeni meyve düşenin " +
             "tepesinde belirmesin diye")]
    public float pendingSpawnPadding = 0.15f;

    [Tooltip("yeni bekleyen meyve en fazla bu kadar geciksin (sn). Düşen meyve yığına " +
             "çarpıp hemen durursa mesafe şartı hiç sağlanmaz — bu emniyet ağı devreye girer. " +
             "dropCooldown'dan küçük tut, yoksa oyuncu bekler")]
    public float pendingSpawnMaxWait = 0.5f;
    
    [Header("game over")]
    
    [Tooltip("ihlal kaç saniye sürerse oyun biter")]
    public float gameOverDelay = 3f;
    
    [Tooltip("yeni bırakılan meyveye dokunulmazlık")]
    public float dropGracePeriod = 1f;

    [Tooltip("'durgun' sayılma hızı eşiği")]
    public float settleVelocityThreshold = 0.3f;
    
    [Tooltip("kaç saniyede bir kontrol edilsin")]
    public float gameOverCheckInterval = 0.1f;

    [Header("fizik")] 
    
    [Tooltip("continuous-discrete geçiş hızı eşiği")]
    public float continuousExitSpeed = 0.5f;
    
    public int continuousExitFrames = 5;
    
    [Tooltip("kaç kere üst üste yavaş olmalı")]
    public int continuousEnterFrames = 5;

    [Tooltip("Discrete moddaki meyve bir çarpışmadan bu hızın üstünde çıkarsa, tünellemeyi önlemek için anında Continuous'a geri alınır")]
    public float continuousRearmSpeed = 4f;

    [Tooltip("meyve durgunlaşınca dönüşün söndürülme hızı (derece/sn²)")]
    public float spinSettleRate = 180f;
    
    [Header("Spawn (Bag Randomizer")]
    [Tooltip("torbada her meyveden kaç kopya")]
    public int bagCopiesPerFruit = 2;

    [Header("combo")]
    [Tooltip("combo zincirinin devam süresi")]
    public float comboWindow = 1.2f;
    
    [Tooltip("her combo adımının çaroan artışı")]
    public float comboMultiplierStep = 0.25f;

    [Tooltip("bu değerden düşük combo'da popup çıkmaz (2 = zincirin ikinci halkasından itibaren)")]
    public int comboPopupMinCombo = 2;

    [Tooltip("combo popup'ının birleşme noktasında ekranda kalma süresi (sn)")]
    public float comboPopupLifetime = 0.9f;

    [Tooltip("combo popup'ı ömrünün yüzde kaçında TAM opak dursun. Kalan sürede söner. " +
             "0.55 = yarıdan fazlası tam görünür, sonra solar. Eskiden ilk kareden " +
             "itibaren soluyordu ve yazı hep yarı saydam görünüyordu")]
    public float comboPopupHoldRatio = 0.55f;

    [Tooltip("combo popup'ının rastgele yatıklığının alt sınırı (derece)")]
    public float comboPopupTiltMin = 10f;

    [Tooltip("combo popup'ının rastgele yatıklığının üst sınırı (derece). Yön (sağa/sola) " +
             "her seferinde yazı tura")]
    public float comboPopupTiltMax = 20f;

    [Tooltip("popup ömrü boyunca kaç birim yükselsin. Yazı üretilen meyvenin renginde " +
             "olduğu için tam o meyvenin üstünde durunca kayboluyordu; birazcık " +
             "yükselmek onu gövdeden ayırıyor. 0 = yerinde dursun")]
    public float comboPopupRiseDistance = 0.45f;

    [Header("combo — kademeler")]
    [Tooltip("ORTA combo kademesinin başladığı sayı (Delicious/Juicy/So Good/Fruity)")]
    public int comboTierMidMin = 4;

    [Tooltip("YÜKSEK combo kademesinin başladığı sayı (Delightful/Mouthwatering/...)")]
    public int comboTierHighMin = 7;

    [Tooltip("EFSANE combo kademesinin başladığı sayı (Legendary/Fruit Master/...)")]
    public int comboTierLegendaryMin = 10;

    [Tooltip("her kademede punto ne kadar büyüsün. 0.3 = düşük 1.0×, orta 1.3×, " +
             "yüksek 1.6×, efsane 1.9×")]
    public float comboPopupTierScaleStep = 0.3f;

    [Tooltip("her kademede popup birleşme noktasının kaç birim ÜSTÜNDE doğsun. " +
             "0.6 = düşük 0, orta 0.6, yüksek 1.2, efsane 1.8 — büyük combo " +
             "ekranın daha üstünde, daha görünür bir yerde patlar")]
    public float comboPopupTierOffsetY = 0.6f;

    [Tooltip("her kademede ömür ne kadar uzasın (sn). Efsane combo daha uzun kalsın")]
    public float comboPopupTierLifetimeStep = 0.15f;

    [Tooltip("teşvik kelimesinin 'xN' satırına göre punto oranı (rich text <size=%>). " +
             "'Mouthwatering!' gibi uzun kelimeler xN kadar büyük olursa ekrana sığmıyor")]
    public float comboPopupWordScale = 0.55f;

    [Tooltip("popup'ın merkezi bu X'i geçmesin (dünya birimi). Yazının GERÇEK genişliği " +
             "ölçülüp yatıklığıyla birlikte hesaba katılıyor, kenardan taşmasın diye")]
    public float comboPopupClampX = 2.9f;

    [Tooltip("yazının TEPESİ bu Y'yi geçmesin. Kademe kaydırması büyük combo'da yazıyı " +
             "dalın içine sokmasın — dal 4.2'de, danger line 2.12'de, arası boş")]
    public float comboPopupMaxY = 3.8f;

    [Header("his,cila")]
    [Tooltip("pop animasyonu süresi")]
    public float popDuration = 0.15f;

    [Tooltip("ne kadar şişip geri dönecek")]
    public float popOverShot = 1.12f;

    [Tooltip("hangi boyuttan başlayacak")]
    public float popStartScale = 0.7f;

    [Header("çarpma ezilmesi (squash)")]
    [Tooltip("bu hızın altındaki çarpmalar ezilme yaratmaz")]
    public float squashMinImpactSpeed = 2f;

    [Tooltip("çarpma hızı bu değere ulaşınca ezilme maksimuma çıkar")]
    public float squashMaxImpactSpeed = 8f;

    [Tooltip("maksimum ezilmede dikey ölçek çarpanı (1 = ezilme yok)")]
    public float squashMinScale = 0.7f;

    [Tooltip("ezilip eski haline dönme süresi (sn)")]
    public float squashDuration = 0.2f;

    [Tooltip("geri dönerken ne kadar taşıp geri gelsin")]
    public float squashOverShot = 1.12f;

    [Header("ses")] [Tooltip("aynı ses kaç saniye içinde tekrar çalmasın")]
    public float sfxRetriggerGuard = 0.06f;

    [Tooltip("kaç ses kanalı yaratılacak")]
    public int audioSourceCount = 6;

    [Tooltip("birleşme sesi için AYRI ve çok daha kısa guard.\n\n" +
             "11 meyve aynı merge.wav'ı paylaşıyor. Genel guard (0.06 sn) zincirleme " +
             "birleşmenin ikinci halkasını susturuyordu — halkalar arası mesafe fizik " +
             "adımı yüzünden sadece ~0.017-0.04 sn. Her halkanın kendi tier pitch'iyle " +
             "duyulması gerekiyor.\n\n" +
             "0.012 = 60 fps'te ard arda KAREler geçer (16.7 ms), aynı KAREde çözülen " +
             "iki birleşme engellenir (0 ms). Üstüne çıkarma, zincir yine susar")]
    public float mergeRetriggerGuard = 0.012f;

    [Header("efektler")]
    [Tooltip("aynı anda en fazla kaç efekt katmanı görünsün — mobilde overdraw sınırı. " +
             "Sınıra gelince en eski efekt geri dönüştürülür")]
    public int maxConcurrentEffects = 12;

    [Tooltip("başlangıçta havuza kaç efekt objesi hazırlanacak")]
    public int effectPrewarmCount = 16;

    [Header("yüz ifadeleri")]
    [Tooltip("karar turunun sıklığı (sn). 0.1 = 10 Hz, yeter. Görsel yumuşatma her karede döner")]
    public float faceMoodInterval = 0.1f;

    [Tooltip("birleşmede love/happy kaç saniye sürsün")]
    public float faceMergeReactionTime = 2f;

    [Tooltip("üretilen meyvenin tier'ı bu değere eşit/büyükse DİĞER meyveler de sevinir. " +
             "6 = şeftali, yani elma+elma birleşmesi (elma tier 5)")]
    public int faceCrowdReactionMinTier = 6;

    [Tooltip("son bırakmadan kaç saniye sonra meyveler uykuya geçsin")]
    public float faceIdleToSleepy = 5f;

    [Tooltip("Danger yakınlığı MEYVE BAŞINA ölçülüyor: (meyvenin tepesi - zemin) / " +
             "(danger line - zemin). 0 = tabanda, 1.0 = tam çizgide.\n\n" +
             "Bu orandan yukarıdaki meyveler 'worried' olur. 0.85 = çizgiye %15 kalmış")]
    public float faceWorriedRatio = 0.85f;

    [Tooltip("Bu orandan yukarıdaki meyveler 'scared' olur. 1.0 = tepesi çizgiyi geçmiş")]
    public float faceScaredRatio = 1f;

    [Tooltip("Bir duruma girdikten sonra çıkmak için eşiğin bu kadar ALTINA düşmek gerekir. " +
             "Histerezis — sınırda titreşen meyvenin yüzü sürekli değişmesin")]
    public float faceDangerHysteresis = 0.03f;

    [Tooltip("bakış kaymasının yarıçapı (meyvenin local birimi). Gövde tuvali 0.92 birim " +
             "geniş, yani 0.18 ≈ gövde genişliğinin %20'si. Büyütürsen yüz daha çok gezinir, " +
             "fazla büyütürsen meyvenin kenarından taşar")]
    public float faceLookRadius = 0.18f;

    [Tooltip("bakışın hedefe yaklaşma hızı — büyük değer daha çabuk çevirir")]
    public float faceLookSpeed = 8f;

    [Tooltip("meyvenin 'düşüyor' sayılması için AŞAĞI doğru en az bu hızda olması gerekir. " +
             "Sadece 'hızı yüksek' demek yetmiyor: tahta dolunca büyük meyveler birbirini " +
             "itip sürekli hareket ediyor ve bakış hedefini çalıyorlar")]
    public float faceFallSpeedThreshold = 1.5f;

    [Tooltip("bırakıldıktan kaç saniye boyunca 'düşen meyve' sayılsın. Bu pencere geçince " +
             "bakış tekrar parmaktaki meyveye döner — yerleşmiş ama sallanan meyveler " +
             "dikkati sonsuza kadar üstlerinde tutmasın")]
    public float faceFallFollowTime = 1.2f;

    [Tooltip("ifade değişiminin yumuşama süresi (sn). Eski yüz söner, ortada sprite değişir, " +
             "yeni yüz dolar. 0'a yakın = ani geçiş")]
    public float faceTransitionDuration = 0.14f;
    
    
    [Header("sonuç ekranı")]
    [Tooltip("1., 2. ve 3. yıldız için gereken skor")]
    public int star1Score = 1000;

    public int star2Score = 2500;

    public int star3Score = 5000;

    [Tooltip("panel açıldıktan ilk yıldıza kadar bekleme (sn). game_over.wav 480 ms — " +
             "yıldız sesleri onun üstüne binmesin")]
    public float starRevealDelay = 0.7f;

    [Tooltip("yıldızlar arası aralık (sn)")]
    public float starRevealInterval = 0.35f;

    [Tooltip("yıldız belirirken şişip geri dönme süresi")]
    public float starPunchDuration = 0.22f;

    [Tooltip("yıldız hangi ölçekten başlayıp 1'e insin")]
    public float starPunchScale = 1.7f;

    [Tooltip("son yıldızdan rekor şeridine kadar bekleme (sn)")]
    public float newRecordDelay = 0.3f;

    [Header("danger line")]
    [Tooltip("çizgi bu doluluk oranından sonra görünür (0-1)")]                                                                                                                        
    public float dangerShowRatio = 0.75f;                                                                                                                                              
                                                                                                                                                                                     
    [Tooltip("eşikteki alpha")]                                                                                                                                                        
    public float dangerMinAlpha = 0.25f;                                                                                                                                               
                                                                                                                                                                                     
    [Tooltip("çizgiye dayandığındaki alpha")]                                                                                                                                          
    public float dangerMaxAlpha = 0.9f;                                                                                                                                                
                                                                                                                                                                                     
    [Tooltip("yanıp sönme hızı (Hz) — eşikte")]                                                                                                                                        
    public float dangerBlinkHzMin = 1.5f;                                                                                                                                              
                                                                                                                                                                                     
    [Tooltip("yanıp sönme hızı (Hz) — tam dolu")]                                                                                                                                      
    public float dangerBlinkHzMax = 5f;

    [Header("evrim zinciri")]
    [Tooltip("henüz ulaşılmamış meyvelerin alpha'sı (0-1). Krem şerit hep tam görünür, sadece meyve ikonu silikleşir. 0.55 civarı: silik ama hangi meyve olduğu hâlâ okunuyor")]
    [Range(0f, 1f)]
    public float fruitChainDimAlpha = 0.55f;

    [Header("ekran zemini")]
    [Tooltip("açılış (Splash) ve ana menü ekranının ORTAK krem zemini. İki ekranda da " +
             "ScreenBackground bileşeni bu değeri yazıyor — sahnede iki ayrı renk elle " +
             "girilince zamanla birbirinden ayrılıyor ve geçiş 'iki farklı ekran' gibi " +
             "duruyordu. #FEEEB4 civarı")]
    public Color screenBackgroundColor = new Color(0.9959f, 0.9354f, 0.7050f, 1f);

    [Header("splash")]
    [Tooltip("yükleme çubuğunun EN AZ bu kadar sürmesi garanti (sn). Çubuk gerçek ısıtma " +
             "işini gösteriyor; iş bundan önce biterse çubuk yine de bu sürede dolar, " +
             "yoksa 0'dan 1'e göz kırpması gibi sıçrardı. Gereğinden uzun tutma")]
    public float splashMinDuration = 1.2f;

    [Tooltip("açılışta KARE BAŞINA kaç havuz objesi yaratılsın. Isıtma (FruitPool 40 + " +
             "ComboPopupDirector 6) artık Awake'te tek karede değil, açılış ekranı boyunca " +
             "karelere yayılıyor — ilk kare daha erken geliyor. Büyütürsen ısıtma çabuk " +
             "biter ama kare başına daha çok Instantiate düşer")]
    public int splashPrewarmPerFrame = 2;

    // ------------------------------------------------------------------ boost: kurtçuklar

    [Header("boost — kurtçuklar / hedefleme")]
    [Tooltip("boost silahlandığında HER meyvenin üstünde beliren nişangâhın dönüş hızı " +
             "(derece/sn). Pozitif = saat yönü")]
    public float boostCrosshairSpinSpeed = -90f;

    [Tooltip("nişangâh çapı = meyve çapı × bu. 1'in biraz altı, meyvenin içinde kalsın")]
    public float boostCrosshairScale = 0.9f;

    [Tooltip("nişangâhın belirme/sönme süresi (sn)")]
    public float boostCrosshairFade = 0.15f;

    [Tooltip("hedef seçilince meyvede çakan pulse halkalarının TOPLAM süresi (sn). " +
             "Dört kare bu süre içinde bir kez oynar, büyüyerek söner — bir 'ping'. " +
             "Kurtların gelişi boyunca sürmez. 0.2'de kareler arası geçiş seçilmiyordu " +
             "(60 fps'te kare başına 3 kare); 0.4 = kare başına ~6 kare, adımlar okunuyor")]
    public float boostPulseDuration = 0.4f;

    [Tooltip("pulse halkasının çapı = meyve çapı × bu")]
    public float boostPulseScale = 1.15f;

    [Header("boost — kurtçuklar / kurt")]
    [Tooltip("bir kurdun kaç halkası olsun: kafa + (n-2) gövde + kuyruk")]
    public int wormSegmentCount = 5;

    [Tooltip("halka çapı = hedef meyvenin YARIÇAPI × bu")]
    public float wormSizeFactor = 0.55f;

    [Tooltip("halka çapının alt sınırı (dünya birimi) — kirazda kurt yok olmasın")]
    public float wormSizeMin = 0.17f;

    [Tooltip("halka çapının üst sınırı — karpuzda 6 kurt sığsın")]
    public float wormSizeMax = 0.40f;

    [Tooltip("iki halka merkezi arası mesafe = halka çapı × bu. 1'in altı = halkalar " +
             "birbirine biner, zincirde boşluk görünmez")]
    public float wormSegmentSpacing = 0.62f;

    [Tooltip("sürünme dalgası: halka aralığının yüzde kaçı sıkışıp açılsın. " +
             "0 = dümdüz kayan zincir")]
    public float wormWaveAmplitude = 0.3f;

    [Tooltip("sürünme dalgasının hızı (rad/sn)")]
    public float wormWaveSpeed = 9f;

    [Tooltip("dalganın halkadan halkaya faz farkı (rad). Büyük değer = daha kısa dalga")]
    public float wormWavePhasePerSegment = 1.1f;

    [Tooltip("kurdun geliş/gidiş yolundaki dikey salınımın genliği (dünya birimi)")]
    public float wormPathWobble = 0.12f;

    [Tooltip("kurt ekranın kenarından bu kadar DIŞARIDA doğar / burada yok olur (dünya birimi)")]
    public float wormSpawnMarginX = 1.2f;

    [Tooltip("aynı taraftan gelen kurtların dikey olarak birbirinden ayrılma payı")]
    public float wormLaneSpread = 0.55f;

    [Tooltip("kurtların meyveye yapıştığı yay yarım açısı (derece). 50 = sol kurtlar " +
             "180°±50 arasına dizilir")]
    public float wormSlotArcHalfAngle = 55f;

    [Tooltip("kurdun sıralama katmanı. Meyveler 100-tier (90..100), yüzler +1, " +
             "parçacıklar 200 — kurtlar SİSİN DE ÜSTÜNDE olmalı, yoksa yerken " +
             "bulutun arkasında kaybolurlar")]
    public int wormSortingOrder = 220;

    [Tooltip("nişangâh/pulse sıralama katmanı — meyvelerin üstünde, kurtların altında")]
    public int boostCursorSortingOrder = 112;

    [Header("boost — kurtçuklar / zamanlama")]
    [Tooltip("hedef seçildikten sonra kurtların meyveye varması (sn). Pulse dizisi " +
             "bu süre boyunca oynar")]
    public float wormApproachDuration = 2f;

    [Tooltip("yeme süresi (sn). Sis bu sürenin başında belirir, sonunda tamamen dağılır")]
    public float wormEatDuration = 2f;

    [Tooltip("yemenin kaçıncı saniyesinde meyve yok olsun. Sis en yoğun anda olmalı ki " +
             "göz geçişi görmesin")]
    public float wormFruitVanishAt = 1f;

    [Tooltip("kurtların geldikleri yönde devam edip ekrandan çıkması (sn)")]
    public float wormLeaveDuration = 1.5f;

    [Header("boost — kurtçuklar / efekt")]
    [Tooltip("sis bulutunun yarıçapı = meyve yarıçapı × bu")]
    public float eatSmokeRadiusFactor = 1.5f;

    [Tooltip("saniyede kaç sis parçacığı çıksın (yeme süresinin tepe noktasında)")]
    public float eatSmokeRate = 55f;

    [Tooltip("sis parçacığının çapı = meyve yarıçapı × bu")]
    public float eatSmokeParticleSize = 1.25f;

    [Tooltip("sis parçacığının ömrü (sn). wormEatDuration'dan çıkarılınca kalan süre " +
             "parçacıkların çıkabileceği son andır — sis tam zamanında dağılsın")]
    public float eatSmokeLifetime = 0.8f;

    [Tooltip("sisin en yoğun anındaki alfası")]
    public float eatSmokeMaxAlpha = 0.9f;

    [Tooltip("yeme sırasında kaç kez kırıntı saçılsın (merge'ün meyve suyu parçacıkları)")]
    public int eatCrumbBursts = 7;

    [Tooltip("her kırıntı saçılmasının merge'e göre yoğunluğu")]
    public float eatCrumbIntensity = 0.45f;

    [Tooltip("meyve yok olurken küçülme oranı — 1 = hiç küçülmez")]
    public float eatFruitMinScale = 0.35f;

    [Tooltip("yenen meyve kaç puan versin. 0 = puan yok (boost bir kurtarma aracı, " +
             "skor kaynağı değil)")]
    public int wormsScoreOnEat = 0;

    [Header("boost — kurtçuklar / envanter")]
    [Tooltip("her yeni oyunda oyuncuya kaç kullanım verilsin. -1 = sınırsız (test)")]
    public int wormsChargesPerRun = 3;
}
