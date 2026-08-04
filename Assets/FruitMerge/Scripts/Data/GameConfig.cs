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

    // continuousEnterFrames KALDIRILDI: hiçbir yerde okunmuyordu. Continuous'a GİRİŞ
    // kare sayısıyla değil hızla karar veriliyor (continuousRearmSpeed) — alan, mimari
    // değişince geride kalmış bir artıktı ve Inspector'da çalışıyormuş gibi duruyordu.

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

    // maxConcurrentEffects ve effectPrewarmCount KALDIRILDI: ikisi de hiçbir yerde
    // okunmuyordu. EffectDirector efekt başına obje yaratmıyor — tek bir paylaşımlı
    // ParticleSystem'e Emit ediyor, parçacık havuzunu Unity native tarafta yönetiyor.
    // Yani ne bir "eşzamanlı efekt sınırı" ne de bir "efekt havuzu ısıtması" var.
    // maxConcurrentEffects'in tooltip'i ("sınıra gelince en eski efekt geri dönüştürülür")
    // var olmayan bir mekanizmayı anlatıyordu, o yüzden özellikle silindi.

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

    [Tooltip("OYUNCUNUN bıraktığı meyvede, bırakmanın ilk bu kadar saniyesinde " +
             "faceFallSpeedThreshold kapısı uygulanmaz (birleşmeden doğan meyveler bu " +
             "muafiyeti almaz).\n\n" +
             "Meyve duruyorken bırakılıyor: yerçekimi hızı 0'dan başlatıyor ve eşiğe " +
             "ulaşması ~0.15 sn sürüyor. O pencerede meyve 'düşüyor' sayılmadığı ve yeni " +
             "bekleyen meyve de henüz doğmadığı için bakış hedefi boşa düşüyor, bütün " +
             "yüzler bir an merkeze kayıp sonra takibe geri dönüyordu.\n\n" +
             "Yerçekimi 9.81 ve eşik 1.5 iken gereken süre 0.153 sn — payla birlikte 0.3")]
    public float faceFallGrace = 0.3f;

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

    // newRecordDelay KALDIRILDI: hiçbir yerde okunmuyordu. Rekor şeridi GameOverPanel'de
    // son yıldızla AYNI karede çıkıyor. Alanı canlandırmak (şeridi 0.3 sn geciktirmek)
    // kutlamanın ritmini değiştiren bir HİS kararı olurdu — mevcut zamanlama korunuyor,
    // yanıltıcı alan siliniyor. Gecikme gerçekten istenirse GameOverPanel.OnTick'e
    // ayrı bir sayaç olarak, bilinçli bir tasarım kararıyla eklenmeli.

    [Header("danger line")]
    [Tooltip("çizgi bu doluluk oranından sonra YANIP SÖNMEYE başlar (0-1). Altında " +
             "kaybolmuyor, dangerIdleAlpha'da sabit durmaya devam ediyor")]
    public float dangerShowRatio = 0.75f;

    [Tooltip("tehlike yokken çizginin sabit alpha'sı. Çizgi oyun boyunca GÖRÜNÜR " +
             "kalıyor — oyuncu nereye kadar yığabileceğini baştan bilmeli. Yeterince " +
             "düşük tut ki dikkat çekmesin; yanıp sönme dibi bu değerin altına inmiyor")]
    [Range(0f, 1f)]
    public float dangerIdleAlpha = 0.15f;

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
             "biter ama kare başına daha çok Instantiate düşer.\n\n" +
             "Konfeti/para/nişangâh havuzları BİLEREK bu listede değil (bkz. " +
             "WormBoostDirector.BuildCursors üstündeki not): Reload Domain/Scene kapalı " +
             "olduğu için ısıtma sayacı oturumlar arasında yaşıyor ve açılış ekranı " +
             "kilitleniyordu")]
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
    [Tooltip("her yeni oyunda oyuncuya kaç kullanım verilsin. -1 = sınırsız (test).\n\n" +
             "1 = oyun başına tek kullanım. HUD'da sayı gösterilmiyor: 1 veya 0 zaten ikonun " +
             "parlak/soluk olmasından okunuyor")]
    public int wormsChargesPerRun = 1;

    // ---------------------------------------------------------------------- boost: deprem

    [Header("boost — deprem / zamanlama")]
    [Tooltip("SARSINTI fazı (sn). İtmeler bu faz boyunca uygulanıyor. " +
             "quakeKickDirectionSlots ile birlikte yön dilimlerinin uzunluğunu belirliyor: " +
             "2.5 sn / 4 dilim = 0.625 sn'lik dilimler")]
    public float quakeShakeDuration = 2.5f;

    [Tooltip("YATIŞMA fazı (sn). İtme ve kamera bitmiş; toz dağılıyor, meyveler yerine oturuyor. " +
             "Boost bu faz boyunca hâlâ 'meşgul' sayılıyor: bırakma girdisi kilitli ve oyun sonu " +
             "sayacı donuk kalıyor, yoksa henüz oturmamış yığın haksızca kaybettirir. " +
             "Sönümleme kalktığı için meyveler ASIL bu fazda boşluklara düşüyor — kısaltma")]
    public float quakeSettleDuration = 0.8f;

    [Header("boost — deprem / itme")]
    [Tooltip("kaç saniyede bir itme uygulanacak. Fizik adımı 0.02 sn, yani 0.06 = 3 adımda bir. " +
             "Sık olması titreşim hissi için şart; büyütürsen sarsıntı 'tık tık' kesik oluyor")]
    public float quakeKickInterval = 0.06f;

    [Tooltip("aralığa eklenen rastgelelik (sn). Sabit aralık makine gibi tık tık atıyor; " +
             "biraz sapma organik bir sarsıntı hissi veriyor")]
    public float quakeKickIntervalJitter = 0.015f;

    [Tooltip("her itmede eklenen hızın büyüklüğü (birim/sn). Yön MEYVE BAŞINA bağımsız ve " +
             "zamanla gezinen bir açıdan geliyor (bkz. quakeKickTurnRate).\n\n" +
             "İlerleme mesafesini belirleyen ana parametre bu. Tek itmenin dikey hoplaması " +
             "v²/2g; asıl iş yatay kaymada")]
    public float quakeKickStrength = 3.2f;

    [Tooltip("itmeye eklenen tamamen rastgele bileşenin oranı. Gezinen yön 'düzgün' bir " +
             "hareket veriyor, bu da üstüne pürüz katıyor. 0 = kusursuz pürüzsüz yön, " +
             "1 = yön kaybolur, saf gürültü kalır")]
    [Range(0f, 1f)]
    public float quakeKickJitterRatio = 0.35f;

    [Tooltip("⭐ İTMENİN BİRİKMESİNİ ENGELLEYEN ŞEY. Sadece hızı bu değerin ALTINDA olan " +
             "meyveler itiliyor; hâlâ hareket halinde olan atlanıyor.\n\n" +
             "Neden gerekli: ilk sürümde her meyve her turda itiliyordu. Yığının içindeki " +
             "sıkışık meyvelerde temas çözücü hızı anında yutuyor (hiç kıpırdamıyorlar), ama " +
             "HAVADAKİ bir meyve serbest olduğu için her itmede hızlanıyor ve ekranın " +
             "tepesine tırmanıyordu. Bu kapı ikisini birden çözüyor: sıkışık meyve her turda " +
             "dürtülüp titreşiyor, hoplayan meyve inip yavaşlayana kadar rahat bırakılıyor.\n\n" +
             "Büyütürsen hareket halindeki meyveler de itilir (tırmanma riski geri gelir), " +
             "küçültürsen titreşim seyrekleşir")]
    public float quakeKickRestSpeed = 1f;

    [Tooltip("⭐ Sarsıntı süresi kaç YÖN DİLİMİNE bölünsün. Her meyve her dilimde kendine özgü " +
             "yeni bir rastgele yön seçiyor; dilim boyunca o yöne itiliyor.\n\n" +
             "4 = 2.5 sn'lik depremde 0.625 sn'lik dört dilim, dilim başına ~10 itme aynı " +
             "yönde. Mesafe buradan geliyor: kare başına rastgele yön verilirse hareketler " +
             "birbirini götürüyor ve meyve olduğu yerde titriyor.\n\n" +
             "Büyütürsen yön daha sık değişir (yerinde titremeye yaklaşır), küçültürsen meyve " +
             "daha uzun süre aynı yöne gider")]
    public int quakeKickDirectionSlots = 4;

    [Tooltip("itmenin AŞAĞI bileşeni bu oranla kısılıyor. Yön hiçbir zaman yukarı bakmıyor " +
             "(bkz. quakeMaxRiseSpeed), sadece sağa/sola/aşağı — bu değer o aşağı bileşenin " +
             "ne kadar güçlü olacağını belirliyor. 1 = tam aşağı itebilir, 0 = tamamen yatay")]
    [Range(0f, 1f)]
    public float quakeKickVerticalScale = 0.7f;

    [Tooltip("⚠️ YUKARI HIZ TAVANI (birim/sn) — meyvelerin havaya kalkmasını engelleyen şey.\n\n" +
             "İki yerde uygulanıyor: (1) itme sonrası, çünkü rastgele sapma yukarı bakabiliyor; " +
             "(2) HAREKET HALİNDEKİ meyvelerde de, her turda. İkincisi kritik: sıkışık yığın " +
             "bir yay gibi davranıp meyveyi fırlatabiliyor ve quakeKickRestSpeed kapısı hızlı " +
             "meyveyi itmediği için onu frenleyecek hiçbir şey kalmıyordu — meyveler böyle " +
             "duvarın üstünden kaçıyordu.\n\n" +
             "0.6 → en fazla 0.018 birimlik yükselme (v²/2g). Tam 0 yapmak fizik çözücüsünü " +
             "zorlayıp iç içe geçmeye yol açabiliyor, o yüzden küçük bir pay bırakılıyor")]
    public float quakeMaxRiseSpeed = 0.6f;

    [Tooltip("deprem sırasında meyvenin 'gittiği yöne bakması' için gereken en küçük hız " +
             "(birim/sn). Bunun altında kalanlar düz bakıyor — yoksa neredeyse durgun bir " +
             "meyvenin gözleri gürültüyle titriyor")]
    public float quakeLookMinSpeed = 0.35f;

    [Tooltip("EN KÜÇÜK meyvedeki (kiraz) itme çarpanı")]
    public float quakeKickScaleSmall = 1f;

    [Tooltip("EN BÜYÜK meyvedeki (karpuz) itme çarpanı. Küçük meyveler daha çok zıplasın — " +
             "boşluklara giren ve eşini bulan onlar")]
    public float quakeKickScaleBig = 0.75f;

    [Tooltip("⚠️ HIZ TAVANI (birim/sn) — emniyet ağı. Kutunun TAVANI YOK: duvar tepeleri " +
             "y=+4.60'ta, danger line y=2.12'de. Bir meyve duvarın 0.3 birimlik tepesine " +
             "oturursa durgun + çizginin üstünde olur ve 3 sn sonra oyun biter.\n\n" +
             "Yukarı yönde asıl freni quakeMaxRiseSpeed yapıyor; bu clamp toplam hızı " +
             "(ağırlıklı yatay) sınırlıyor, meyveler duvara çok sert vurup sekmesin")]
    public float quakeMaxSpeed = 4.5f;

    [Tooltip("zarfın (envelope) ana sarsıntı başında uyarı seviyesinden 1'e çıkma süresi (sn)")]
    public float quakeAttackTime = 0.15f;

    [Tooltip("zarfın ana sarsıntı SONUNDA 1'den 0'a inme süresi (sn). Ani duruş 'oyun dondu' " +
             "gibi duruyor; yumuşak sönme 'deprem yatıştı' gibi")]
    public float quakeReleaseTime = 0.4f;

    [Header("boost — deprem / kamera")]
    [Tooltip("kamera ötelemesinin EN BÜYÜK değeri (dünya birimi). Arka plan sprite'ı " +
             "8.64 × 18.21 birim, görünür alan ~6.19 × 11 — bol pay var, bu değerde kenar " +
             "asla açığa çıkmıyor")]
    public float quakeShakeAmplitude = 0.12f;

    [Tooltip("Perlin gürültüsünün hızı (Hz). Perlin kullanılıyor çünkü rastgele ofset her " +
             "karede zıplayıp epileptik görünüyor; Perlin yumuşak sarsıyor")]
    public float quakeShakeFrequency = 14f;

    [Tooltip("depremin ilk anındaki tek seferlik kamera darbesi (0-1). Düz bir rampa yerine " +
             "'deprem başladı' vuruşu veriyor, quakeAttackTime süresince sönüyor")]
    [Range(0f, 1f)]
    public float quakeStartPunch = 0.7f;

    [Header("boost — deprem / efekt")]
    [Tooltip("saniyede kaç toz parçacığı çıksın (zarf tam değerdeyken)")]
    public float quakeDustRate = 40f;

    [Tooltip("toz parçacığının ömrü (sn). Toz üretimi ana sarsıntının SONUNDA duruyor " +
             "(zarf 0'a inince), yani son parçacık boost bittikten ~0.3 sn sonra ölüyor — " +
             "deprem geçtikten sonra tozun bir süre daha dağılması BİLEREK böyle. " +
             "Havada asılı kalmasını istemezsen quakeSettleDuration'ın altına indir")]
    public float quakeDustLifetime = 0.9f;

    [Tooltip("toz parçacığının çapı (dünya birimi)")]
    public float quakeDustSize = 0.55f;

    [Tooltip("tozun en yoğun anındaki alfası")]
    public float quakeDustAlpha = 0.55f;

    [Tooltip("toz parçacığının tint'i. BEYAZ bırak: Mat_QuakeDust artık amaca üretilmiş " +
             "quake_dust_puff dokusunu kullanıyor ve o doku zaten sıcak bej (#CCB28C). " +
             "Üstüne bir de #D9C7A8 boyarsan iki tint çarpılıp toz çamur rengine (#AD8A5C) " +
             "düşüyor. Sadece tozun ruh halini değiştirmek istersen (ör. daha kırmızı bir " +
             "toprak) buradan boya. Alfa kanalı koddan yazılıyor, buradaki alfa yok sayılır")]
    public Color quakeDustColor = Color.white;

    [Tooltip("tozun çıktığı şeridin zemin yüzeyine göre yüksekliği (dünya birimi) — " +
             "parçacıklar zeminin tam üstünden doğsun")]
    public float quakeDustSpawnLift = 0.12f;

    [Tooltip("tozun yüzde kaçı DUVARLARDAN çıksın (kalanı zeminden). Sadece zeminden çıkınca " +
             "'yerden toz kalkıyor' oluyor; deprem hissi için tozun yanlardan da gelmesi " +
             "gerekiyor. 0.45 = neredeyse yarısı duvarlardan, ikiye bölünüp sağ ve sola")]
    [Range(0f, 1f)]
    public float quakeDustWallShare = 0.45f;

    [Tooltip("duvar toz şeridinin yüksekliği (dünya birimi), zeminden yukarı. Duvarlar " +
             "zeminden y=+4.60'a kadar; 5.0 tamamını kapsıyor")]
    public float quakeDustWallHeight = 5f;

    [Tooltip("duvar toz şeridi duvarın iç yüzünden ne kadar içeride doğsun (dünya birimi)")]
    public float quakeDustWallInset = 0.15f;

    [Header("boost — deprem / düşen moloz")]
    [Tooltip("saniyede kaç moloz parçası düşsün (zarf tam değerdeyken). Ekranın SAĞ ve SOL " +
             "kenarından, meyvelerin ARKASINDAN düşüyorlar — 'deprem oluyor' hissinin " +
             "büyük kısmı bundan geliyor")]
    public float quakeRubbleRate = 14f;

    [Tooltip("moloz parçasının ömrü (sn). Ekranı yukarıdan aşağıya geçmesine yetecek kadar")]
    public float quakeRubbleLifetime = 1.6f;

    [Tooltip("moloz parçasının çapı (dünya birimi). Kiraz yarıçapı 0.19 — moloz ondan küçük olmalı")]
    public float quakeRubbleSize = 0.14f;

    [Tooltip("moloz tint'i. BEYAZ bırak: quake_pebble dokusu zaten toprak renginde. " +
             "Daha koyu/açık moloz istersen buradan boya")]
    public Color quakeRubbleColor = Color.white;

    [Tooltip("moloz duvarın iç yüzünden ne kadar İÇERİDE doğsun (dünya birimi). 0 = tam " +
             "duvar dibinde")]
    public float quakeRubbleEdgeInset = 0.25f;

    [Tooltip("doğduğu şeridin dikey yayılımı (dünya birimi) — hepsi aynı yükseklikten " +
             "düşmesin, sıra sıra görünmesin")]
    public float quakeRubbleSpawnSpread = 1.8f;

    [Tooltip("moloz şeridinin merkezi, bırakma yüksekliğine (dropY) göre ofset. Pozitif = " +
             "daha yukarı, ekran dışından düşmeye başlar")]
    public float quakeRubbleSpawnYOffset = 1.6f;

    [Tooltip("molozun sıralama katmanı. Meyveler 90-100 — moloz onların ARKASINDAN düşmeli " +
             "(-4), yoksa yığının önünden geçip dikkat dağıtıyor. Background -10")]
    public int quakeRubbleSortingOrder = -4;

    // Uyarı levhası (ünlem işareti) ve zemindeki çakan yıldızlar KALDIRILDI — ikisi de
    // istenmedi. Deprem artık tamamen kamera + itme + toz + moloz ile anlatılıyor, ekranda
    // duran hiçbir sprite yok. quake_warning_sign.png ve quake_ground_crack.png Assets
    // altında duruyor ama kullanılmıyor.

    [Header("boost — deprem / envanter")]
    [Tooltip("her yeni oyunda oyuncuya kaç kullanım verilsin. -1 = sınırsız (test).\n\n" +
             "1 = oyun başına tek kullanım. Kalan sayı ikonun sağ altındaki rozette " +
             "yazıyor; 0'a düşünce rozet '+' olup mağazayı açıyor")]
    public int quakeChargesPerRun = 1;

    [Header("boost — mağaza")]
    [Tooltip("kurtçuk boost'unun tek kullanımlık fiyatı (coin)")]
    public int wormsBoostPrice = 20;

    [Tooltip("deprem boost'unun tek kullanımlık fiyatı (coin)")]
    public int quakeBoostPrice = 50;

    /// <summary>
    /// Bir boost'un fiyatı. <see cref="BoostShopPanel"/> tek script olarak bütün
    /// boost'lara hizmet ettiği için fiyatı buradan soruyor — panelde boost başına
    /// alan tutulmuyor.
    /// </summary>
    public int PriceFor(BoostId id)
    {
        switch (id)
        {
            case BoostId.Quake: return quakeBoostPrice;
            default:            return wormsBoostPrice;
        }
    }

    [Header("coin — ödül")]
    [Tooltip("sonuç ekranında kazanılan her yıldız için verilecek coin")]
    public int coinPerStar = 10;

    [Tooltip("yıldızlar yerine oturduktan sonra ilk paranın uçmaya başlamasına kadar " +
             "bekleme (sn). Yıldız punch animasyonu bitsin, sonra para aksın")]
    public float coinPayoutDelay = 0.35f;

    [Tooltip("arka arkaya uçan paralar arası gecikme (sn). Hepsi aynı anda kalkarsa " +
             "tek bir sıçrama gibi görünüyor, sırayla kalkınca akış hissi oluyor")]
    public float coinPayoutStagger = 0.09f;

    [Header("coin — uçuş")]
    [Tooltip("bir paranın kaynaktan HUD'a varış süresi (sn)")]
    public float coinFlyDuration = 0.75f;

    [Tooltip("uçuş yayının yanal sapması (referans çözünürlükte piksel). Düz çizgi " +
             "yerine kavis çizsin diye — her para rastgele sağa ya da sola sapıyor")]
    public float coinFlyArc = 260f;

    [Tooltip("paranın referans çözünürlükteki boyutu (piksel)")]
    public float coinFlySize = 96f;

    [Tooltip("para HUD'a varırken bu orana kadar küçülüyor (1 = küçülme yok)")]
    public float coinFlyEndScale = 0.55f;

    [Header("coin — HUD sayacı")]
    [Tooltip("HUD'daki sayının saniyede kaç birim artacağı. Yüksek tut: para 'birer birer " +
             "ama hızlıca' saysın, oyuncu sonucu beklemek zorunda kalmasın")]
    public float coinCountSpeed = 45f;

    // ---------------------------------------------------------------------- titreşim

    [Header("titreşim — genel")]
    [Tooltip("bütün titreşimlerin ortak çarpanı. Aynı 0.7 genlik telefondan telefona çok " +
             "farklı hissediliyor — test cihazın fazla sertse tek yerden kıs.\n\n" +
             "0 = titreşim tamamen kapalı (oyuncunun ayarından bağımsız, geliştirici anahtarı)")]
    [Range(0f, 1f)] public float hapticStrength = 1f;

    [Tooltip("iki AYRI darbe arasında en az bu kadar süre geçsin (sn).\n\n" +
             "Zincirleme birleşmenin halkaları arası sadece ~0.02-0.04 sn: her halka motoru " +
             "yeniden tetiklerse ayrı ayrı darbeler yerine tek bir sürekli mırıldanma " +
             "hissediliyor. Guard içinde gelen istek ATILMIYOR — en güçlüsü saklanıp guard " +
             "bitince çalıyor, yani x7'lik halka x1'lik tıkın gölgesinde kalmıyor")]
    public float hapticGuard = 0.05f;

    [Tooltip("Editör'de her titreşim isteğini konsola yaz.\n\n" +
             "Masaüstünde motor YOK — Editör'de titreşimi hissedemezsin. Kancaların doğru " +
             "yerde ve doğru şiddette tetiklendiğini ancak böyle görebilirsin. Cihaz " +
             "derlemesinde bu satır hiç derlenmiyor.\n\n" +
             "⚠️ Varsayılan KAPALI: deprem ve kemirme trenleri saniyede 14/9 darbe üretiyor. " +
             "Trenler artık günlüğe hiç girmiyor ama kalan istekler de Profiler'da GC Alloc " +
             "olarak görünüyor — kanca doğrulaması yapacağın zaman elle aç")]
    public bool hapticEditorLog = false;

    [Header("titreşim — bırakma / birleşme")]
    [Tooltip("meyve bırakınca çok hafif bir tık")]
    public bool hapticOnDrop = true;

    [Tooltip("bırakma tıkının şiddeti (0-1). Saniyede bir olabilen bir olay — hafif tut")]
    [Range(0f, 1f)] public float hapticDropIntensity = 0.22f;

    [Tooltip("her birleşmede tık (combo olmasa da). Kapatırsan sadece x2+ combo'lar titrer")]
    public bool hapticOnMerge = true;

    [Tooltip("tier 0 (en küçük meyve) birleşmesinin şiddeti")]
    [Range(0f, 1f)] public float hapticMergeIntensityLow = 0.3f;

    [Tooltip("en yüksek tier birleşmesinin şiddeti — büyük meyve daha tok vursun. " +
             "Ses tarafındaki mergePitchLow/HighTier ile aynı fikir")]
    [Range(0f, 1f)] public float hapticMergeIntensityHigh = 0.6f;

    [Tooltip("birleşme darbesinin süresi (sn).\n\n" +
             "⚠️ 0.015'in altına inme: eski Android telefonlarındaki ERM motorları dönmeye " +
             "başlayamadan darbe bitiyor ve HİÇBİR ŞEY hissedilmiyor")]
    public float hapticMergeDuration = 0.025f;

    [Header("titreşim — combo")]
    [Tooltip("bu combodan itibaren kademe titreşimi devreye girer " +
             "(2 = zincirin ikinci halkası, comboPopupMinCombo ile aynı)")]
    public int hapticComboMinCombo = 2;

    [Tooltip("combo KADEMESİ başına şiddet artışı. Kademeler comboTierMidMin / " +
             "comboTierHighMin / comboTierLegendaryMin ile birebir aynı — parmağın " +
             "hissettiği kademe ekranda okunan kelimeyle ('Nice!' / 'Legendary!') aynı olsun")]
    [Range(0f, 0.5f)] public float hapticComboIntensityStep = 0.15f;

    [Tooltip("combo kademesi başına süre artışı (sn). Büyük combo daha TOK, sadece daha " +
             "güçlü değil")]
    public float hapticComboDurationStep = 0.012f;

    [Tooltip("EFSANE kademede (x10+) tek darbe yerine çift darbe. Şiddet zaten tavana " +
             "dayandığı için farkı ancak ritim taşıyor")]
    public bool hapticComboLegendaryDouble = true;

    [Tooltip("karpuz birleşmesinin kutlama dizisi (iki sert vuruş + uzun kuyruk) çarpanı. " +
             "0 = kapalı")]
    [Range(0f, 1f)] public float hapticMaxTierStrength = 1f;

    [Header("titreşim — kurtçuklar")]
    [Tooltip("kurtlar çiğnerken hissedilen kemirme treni. 0 = kapalı.\n\n" +
             "Tek bir darbe yerine tren: yeme 1+ saniye süren, ekranda sis ve kırıntıyla " +
             "görülen bir SÜREÇ — parmağın da o süre boyunca bir şey hissetmesi gerekiyor")]
    [Range(0f, 1f)] public float hapticChewIntensity = 0.28f;

    [Tooltip("kemirme darbeleri arası (sn). Küçültürsen kemirme hızlanır — 0.06'nın altında " +
             "kemirme değil sürekli titreşim gibi hissediliyor")]
    public float hapticChewInterval = 0.11f;

    [Tooltip("bir kemirme darbesinin süresi (sn)")]
    public float hapticChewDuration = 0.016f;

    [Tooltip("meyve yok olduğu andaki 'yutma' darbesi. Kemirme treninden belirgin şekilde " +
             "güçlü olmalı — süreci o bitiriyor. 0 = kapalı")]
    [Range(0f, 1f)] public float hapticEatenIntensity = 0.75f;

    [Tooltip("yutma darbesinin süresi (sn)")]
    public float hapticEatenDuration = 0.06f;

    [Header("titreşim — deprem")]
    [Tooltip("depremin EN ŞİDDETLİ anındaki titreşim. Aradaki her an, kameranın ve gürültünün " +
             "beslendiği aynı zarfla (Envelope) ölçekleniyor: hissedilen şiddet, görülen ve " +
             "duyulan şiddetle birebir aynı. 0 = kapalı")]
    [Range(0f, 1f)] public float hapticQuakeMaxIntensity = 1f;

    [Tooltip("deprem darbeleri arası (sn). Darbe süresi bunun ~1.6 katı, yani darbeler ÜST " +
             "ÜSTE biniyor — bindirmezsen sürekli sarsıntı yerine 'tık tık tık' hissediliyor")]
    public float hapticQuakePulseInterval = 0.07f;

    [Tooltip("zarf bunun altına inince titreşim kesilir. Yatışmanın son kırıntısında " +
             "hissedilmeyen ama motoru meşgul eden ölü darbeler kalmasın")]
    [Range(0f, 0.5f)] public float hapticQuakeMinLevel = 0.08f;

    [Header("titreşim — oyun sonu / sonuç")]
    [Tooltip("oyun sonu dizisi (iki kısa + bir uzun darbe) çarpanı. Oyunda hissedilen EN " +
             "güçlü titreşim bu olmalı — kaybetmenin ağırlığı buradan geliyor. 0 = kapalı")]
    [Range(0f, 1f)] public float hapticGameOverStrength = 1f;

    [Tooltip("sonuç ekranında dolan her yıldızın tıkı. Sesle (star.wav) aynı karede. 0 = kapalı")]
    [Range(0f, 1f)] public float hapticStarIntensity = 0.45f;

    [Tooltip("rekor kırıldığında yükselen üçlü dizinin çarpanı. 0 = kapalı")]
    [Range(0f, 1f)] public float hapticNewRecordStrength = 0.85f;

    // ---------------------------------------------------------------------- coin patlaması

    [Header("coin — patlama (ekran ortası)")]
    [Tooltip("oyun sonunda kazanılan coin artık yıldızların/meyvelerin üstünden değil " +
             "EKRANIN ORTASINDAN kalkıp sağ üstteki cüzdan HUD'ına uçuyor — paralar UI " +
             "Image olarak uçuyor, dünya uzayı ParticleSystem Screen Space Overlay " +
             "canvas'ların ARKASINDA kalıp cüzdana asla varamazdı.\n\n" +
             "toplam ödül kaç paraya bölünüp uçacak. Değer paranın görselinde yazmıyor " +
             "(hepsi aynı particle_coin), miktar para SAYISINDAN okunuyor. Havuz " +
             "boyutundan büyük olması zararsız, taşan para uçmadan doğrudan hesaba geçer")]
    public int coinBurstCount = 14;

    [Tooltip("kalkış noktasının ekran ortasından ne kadar YUKARI kaydığı (ekran " +
             "yüksekliğinin oranı). 0 = tam orta. Sonuç ekranında yıldızlar ortanın " +
             "hemen üstünde, paralar oradan çıkıyormuş gibi görünsün")]
    public float coinBurstOriginYRatio = 0.06f;

    [Tooltip("kalkış noktalarının saçılma yarıçapı (referans çözünürlükte piksel, canvas " +
             "1080x1920). Hepsi tek noktadan kalkarsa tek bir kalın çizgi gibi görünüyor")]
    public float coinBurstSpread = 170f;

    [Tooltip("paralar arası kalkış gecikmesi (sn). Hepsi aynı karede kalkarsa akış değil " +
             "tek sıçrama oluyor; cüzdan sayacı da paraların ritmini takip ettiği için " +
             "(birer birer sayıyor) bu değer sayma hissini belirliyor")]
    public float coinBurstStagger = 0.045f;

    [Tooltip("uçarken dönme hızı (derece/sn), yön her parada rastgele. Madeni paranın " +
             "havada takla atması")]
    public float coinBurstSpinSpeed = 220f;

    [Tooltip("boyut sapması (0.25 = ±%25). Hepsi aynı boyutta olursa kopyalanmış gibi duruyor")]
    public float coinBurstSizeJitter = 0.25f;

    // ---------------------------------------------------------------------- konfeti

    [Header("konfeti — genel")]
    [Tooltip("havuz Awake'te kuruluyor, oynanış sırasında Instantiate yok. Karpuz " +
             "patlaması (26) ile rekor yağmuru (110) üst üste binebiliyor — karpuzla " +
             "biten bir oyunda patlamanın parçaları hâlâ havadayken sonuç ekranı " +
             "açılıp yağmur da başlıyor. Küçük havuzda yağmurun kuyruğu hiç " +
             "doğmadan sessizce düşüyordu. 26 + 110 + yedek")]
    public int confettiPoolSize = 140;

    [Tooltip("parçanın referans çözünürlükteki boyutu (piksel)")]
    public float confettiSize = 64f;

    [Tooltip("boyut sapması (0.3 = ±%30). Hepsi aynı boyutta olursa kopyalanmış gibi duruyor")]
    public float confettiSizeJitter = 0.3f;

    [Tooltip("parçanın ekranı BAŞTAN AŞAĞI geçmesine yetmeli — yetmezse konfeti ortada " +
             "havada sönüp yok oluyor ve yağmur seyrek görünüyor. Terminal hız kabaca " +
             "confettiGravity / confettiDrag")]
    public float confettiLifetime = 3.2f;

    [Tooltip("ömrün son yüzde kaçında sönme başlıyor (0.3 = son %30). Parçaların çoğu " +
             "artık ekranın altından çıkarak ölüyor, sönme sadece emniyet ağı — bu " +
             "yüzden küçük tut. Baştan solmaya başlarsa konfeti hep yarı saydam görünüyor")]
    public float confettiFadeRatio = 0.18f;

    [Tooltip("yerçekimi (piksel/sn²). Yavaş düşen konfeti aynı anda ekranda DAHA ÇOK " +
             "parça bırakıyor, yani aynı sayıyla daha dolu görünüyor")]
    public float confettiGravity = 1600f;

    [Tooltip("hava sürtünmesi (1/sn), parça BAŞINA confettiDragJitter ile sapıyor. " +
             "Konfeti taş gibi düşmemeli; sürtünme terminal hızı yerçekimi/sürtünme " +
             "olarak sınırlıyor. Yavaş düşen konfeti aynı anda ekranda daha çok parça " +
             "bırakıyor, yani aynı sayıyla daha dolu görünüyor")]
    public float confettiDrag = 2.2f;

    [Tooltip("parça BAŞINA sürtünme sapması (0.45 = ±%45). ⭐ Konfetinin \"tek blok " +
             "halinde iniyor\" görünmesinin ASIL çaresi: sürtünme herkeste aynıysa " +
             "bütün parçalar ~0.4 sn içinde aynı terminal hıza oturuyor ve aralarındaki " +
             "mesafe donuyor. Farklı sürtünme = farklı terminal hız = parçalar " +
             "birbirinden ayrılarak iner")]
    public float confettiDragJitter = 0.45f;

    [Tooltip("kendi ekseninde dönme hızı üst sınırı (derece/sn), her parçada rastgele " +
             "işaret ve büyüklük")]
    public float confettiSpinSpeed = 540f;

    [Tooltip("yatay salınım genliği (piksel/sn) — kağıt parçasının havada sağa sola " +
             "savrulması, parça BAŞINA confettiFlutterJitter ile sapıyor. Salınım " +
             "olmadan konfeti dümdüz düşüyor ve kağıt gibi durmuyor")]
    public float confettiFlutterSpeed = 230f;

    [Tooltip("yatay salınımın frekansı (Hz), parça BAŞINA confettiFlutterJitter ile sapıyor")]
    public float confettiFlutterFrequency = 3.2f;

    [Tooltip("parça başına salınım sapması (0.5 = ±%50), genliğe VE frekansa birlikte " +
             "uygulanıyor. Sadece faz rastgeleyken hepsi aynı ritimde sallanıyor ve göz " +
             "bunu koro gibi okuyor")]
    public float confettiFlutterJitter = 0.5f;

    [Tooltip("takla frekansı (Hz) — parça kendi dikey ekseni etrafında dönüyormuş gibi " +
             "X ölçeği sinüsle daralıp genişliyor. Sadece Z dönüşü olan kağıt hep aynı " +
             "genişlikte kalıyor ve \"sticker\" gibi duruyor; takla ucuz ama en çok " +
             "hayat katan detay. Parça BAŞINA confettiFlutterJitter ile sapıyor")]
    public float confettiTumbleSpeed = 2.1f;

    [Tooltip("taklanın en dar anındaki X ölçeği (0 = tamamen profilden, görünmez olur). " +
             "0.15 = neredeyse kenarına dönüyor ama kayboluyormuş gibi görünmüyor")]
    public float confettiTumbleMinScale = 0.15f;

    [Header("konfeti — karpuz patlaması")]
    [Tooltip("karpuz birleşme noktasında patlayan parça sayısı")]
    public int confettiBurstCount = 26;

    [Tooltip("kalkış hızının alt sınırı (piksel/sn)")]
    public float confettiBurstSpeedMin = 1100f;

    [Tooltip("kalkış hızının üst sınırı (piksel/sn)")]
    public float confettiBurstSpeedMax = 2000f;

    [Tooltip("yönün yukarı eğilimi (0 = tam daire, 1 = sadece yukarı). Patlama aşağı " +
             "doğru saçılırsa parçalar anında ekranın altına iniyor")]
    public float confettiBurstUpBias = 0.55f;

    [Header("konfeti — rekor yağmuru")]
    [Tooltip("yeni rekorda ekranın üstünden yağan parça sayısı")]
    public int confettiRainCount = 110;

    [Tooltip("parçaların bırakılma süresi (sn) — hepsi aynı anda değil, bu süreye " +
             "confettiRainDelayJitter ile dağılarak")]
    public float confettiRainDuration = 1.8f;

    [Tooltip("başlangıç aşağı hızının alt sınırı (piksel/sn)")]
    public float confettiRainSpeedMin = 120f;

    [Tooltip("başlangıç aşağı hızının üst sınırı (piksel/sn)")]
    public float confettiRainSpeedMax = 520f;

    [Tooltip("ekranın üst kenarının kaç piksel ÜSTÜNDE doğduğu — kenarda 'pat' diye " +
             "belirmesinler diye")]
    public float confettiRainTopMargin = 60f;

    [Tooltip("yağmur parçalarının üst kenarın kaç piksellik ARALIĞINA rastgele " +
             "dağılarak doğduğu. Hepsi tam aynı Y'de doğarsa ekrana kusursuz bir " +
             "yatay HAT halinde giriyorlar — dağınık görünmesinin şartı bu aralık")]
    public float confettiRainTopSpread = 450f;

    [Tooltip("bırakma takvimindeki rastgelelik (aralığın oranı, 0.7 = ±%70). Tam eşit " +
             "aralıklı bırakma ritmik bir dalga üretiyor; göz o düzenliliği " +
             "\"toplu hareket\" olarak okuyor")]
    public float confettiRainDelayJitter = 0.7f;

    [Tooltip("yağmurun yatay saçılma hızı (piksel/sn, ±). Dümdüz aşağı inen parçalar " +
             "paralel çizgiler gibi duruyor")]
    public float confettiRainDrift = 260f;
}
