# Fruit Merge — Yol Haritası

> Sen uygulayacaksın, ben her fazda adım adım anlatacağım.
> Bir faz bitmeden sonrakine geçme; her fazın sonunda "Test" bölümü var.
>
> **Script:** 18 → **30 dosya** · **Art:** 478 PNG, referanslı 25 → **100**, bekleyen **378**

---

# YAPILANLAR

| Faz | Ne yapıldı | Detay |
|---|---|---|
| **0** Temel + atlas | Pause kilidi tespit edildi, 48 yüz + 35 efekt + 15 ikon `Single`'a çevrildi, `FruitAtlas`'a `Faces` eklendi, 94 dosyada çift sıkıştırma kapatıldı, `SampleScene` silindi | [Faz 0](#faz-0--temel--atlas-hazırlığı) |
| **1** Pause panel + ayarlar | `PausePanel` + ses/müzik/titreşim toggle'ları, kayıt migrasyonu v1→v2. **Oyunu kilitleyen `Resume()` bug'ı kapandı** | [Faz 1](#faz-1--pause-panel--ayarlar) |
| **2** Merge efektleri | Flipbook denendi, **reddedildi**. Yerine meyve suyu fışkırması: 2 paylaşımlı `ParticleSystem`, tier'a göre damla sayısı/hız/boyut, renk `displayColor`'dan | [Faz 2](#faz-2--merge-efektleri-particle) |
| **3** Meyve yüzleri | `FaceSet` (12 ifade × 4 boyut), `FruitFace`, `FaceDirector`. Birleşmede love/happy, düşen+sürüklenen meyveyi takip, sleepy, meyve başına danger sınıflandırması, oyun sonu dizzy/squish, yumuşak ifade geçişi | [Faz 3](#faz-3--meyve-yüzleri) |
| **3.5** Dropper dalı | *Planda yoktu.* `tree-branch` üretildi, sapın ucu/yuvası alfa kanalından ölçüldü. Meyve tepesi sapa değecek şekilde asılıyor. `NextFruitDisplay` — next meyve yüzüyle yuvada durur, bırakınca aşağı kayıp büyüyerek bekleyen meyveye dönüşür | — |
| **3.6** HUD yerleşimi | *Planda Faz 6'daydı.* Skor `panel_hud_score` içine alındı, sağ üste taşındı, eski next önizleme kaldırıldı, `dropY` 4.2→3.8 | — |
| **4** Result ekranı | Yıldızlar sırayla dolar + `star.wav`, rekor kuşağı + `new_record.wav`, `GameEvents.OnNewRecord`, MENU + RESTART, metinler İngilizce, 9 yerleşim hatası düzeltildi | [Faz 4](#faz-4--result-ekranı) |
| **7a** Landing/menü | *Öne alındı.* `GameState.Menu` artık kullanılıyor: `Boot → Menu → Playing`. `MenuPanel` (logo, meyve yığını, bulutlar, PLAY), iki panelde MENU butonu aktif, `Restart()` sahne yüklemiyor | [Faz 7](#faz-7--müzik--ekran-akışı) |
| **6** HUD cilası | Evrim zinciri (11 meyve + idle yüz, tek sıra, kaymadan), combo popup (HUD'dan kaldırıldı, dünya-uzayında meyve renginde). Danger vinyeti ve bitmap skor fontu **yapılmayacak** (K5/K6) | [Faz 6](#faz-6--hud-cilası) |
| **7c** Splash | *Öne alındı.* `SplashPanel`, `Boot`'ta yükleme çubuğunu **gerçek havuz ısıtmasıyla** dolduruyor, sonra `GoToMenu()`. Menü ile aynı zemin, çakışmasız geçiş. İlk sürüm reddedildi | [Faz 7c](#faz-7c--splash) |
| **—** Coin patlaması + konfeti | *Faz değil.* Ödül coin'leri artık yıldız/meyve üstünden değil **ekranın ortasından** kalkan, dönen, kademeli iki patlama olarak cüzdana akıyor (`SpawnBurst`, toplam değer birebir korunuyor). Bütün coin görselleri `particle_coin`'de birleşti (uçan para, mağaza satın alma ikonu, cüzdan rozeti). `ConfettiDirector`: karpuzda birleşme noktasında patlama, rekorda yukarıdan yağmur. İkisi de **UI uzayında** particle sistemi — Screen Space Overlay canvas'lar dünya parçacıklarının üstüne kompozit edildiği için gerçek `ParticleSystem` panelin arkasında kalıyordu | — |
| **—** Titreşim | *Faz değil, borç kapandı.* `HapticService` (yönetmen) + `HapticDevice` (Android `Vibrator`/`VibrationEffect`, iOS `UIImpactFeedbackGenerator` native eklentisi). `Handheld.Vibrate` KULLANILMADI — şiddet/süre kademesi yok. Kancalar: bırakma, birleşme (tier'a göre), combo (popup'la aynı 4 kademe, efsanede çift vuruş), karpuz, deprem (zarfa bağlı darbe treni), kurt kemirmesi + yutma, oyun sonu, yıldız, rekor. Android izni derleme sonrası manifest'e enjekte ediliyor | — |

---

# YAPILACAKLAR

## Faz 6 — HUD cilası

> **Evrim zinciri ✅ BİTTİ.** Ekranın altındaki boş krem bantta (tahta zemin ~382 px, şerit 50–182 px)
> 11 meyve tek sırada, **kaymadan** duruyor. `FruitChainView.cs` olay güdümlü — **`Update` yok**:
> `OnRunStarted`'da en yüksek tier `spawnableCount-1`'e sıfırlanır, `OnMerged`/`OnMaxTierMerged`'de
> üretilen tier daha yüksekse güncellenir. Ulaşılana kadar ikon tam opak, sonrası
> `GameConfig.fruitChainDimAlpha` (0.55) ile silikleşir — silik ama hangi meyve olduğu hâlâ okunuyor.
> Play mode'da merge simüle edilip ilk durum, güncelleme ve `Restart()` sıfırlaması doğrulandı.
>
> **İlk sürüm reddedildi, yeniden yapıldı (Danger vinyeti dersinin aynısı).** İlk deneme
> `panel_fruit_chain` + `hud_fruit_chain_slot` halkaları + `hud_chain_arrow` okları + yatay
> `ScrollRect` idi; "berbat" bulundu. Üç ayrı sebep vardı:
> 1. **`panel_fruit_chain` düz bir dikdörtgen değil** — solda alçak/ince bir hap, x≈1420'de yukarı
>    çıkan bir çıkıntı (sol y 65–291, sağ y 65–375). Tek tip 24 px'lik dikdörtgen maske o şekle
>    uymadığı için daireler kahverengi çerçevenin dışına taşıyordu.
> 2. **11 slot ekrana sığmıyordu**, `ScrollRect` gerekiyordu — oyuncunun kaydırması gereken bir
>    "neye dönüşür" tablosu işe yaramaz, kenardan yarım kesilmiş slotlar da kötü duruyordu.
> 3. **Atlas sızması (yeni bulunan, önceden de vardı):** `FruitAtlas`/`UIAtlas` **Tight Packing +
>    Rotation** ile paketleniyordu. `SpriteRenderer` tight mesh kullandığı için sahnede sorun yok,
>    ama **UI `Image` her zaman sprite'ın tam bounding box'ını çizer** → her meyve ikonunun
>    köşelerinde komşu sprite parçaları (yeşil/mor şeritler) görünüyordu. İkisi de
>    `enableTightPacking = 0`, `enableRotation = 0` yapıldı (padding 4 kaldı). **UI'da kullanılan
>    her atlas için kural: Tight Packing ve Rotation kapalı olmalı.**
>
> **Şipilen sürüm:** `panel_chain_strip.png` (224×234) — `panel_fruit_chain`'in **sağlam sol hap
> kısmı** koddan kırpılıp aynalanarak üretilmiş, gerçek 9-slice bir kapsül (border 110/44/110/40,
> PPU 190, `FullRect` mesh). `FruitChainPanel` yatayda **stretch** (kenardan 36 px), yüksekliği
> 132 px; içinde `HorizontalLayoutGroup` (padding 38 — yuvarlak uçları es geçer,
> `childForceExpandWidth`) 11 `Slot_XX/Icon`'u eşit dağıtıyor. İkonlar tier'la 48→72 px büyüyor,
> böylece evrim yönü boyuttan da okunuyor. Sabit genişlik yerine stretch: 20:9 telefonda canvas
> 1080 değil ~978 px oluyor, sabit 990 px'lik şerit taşıyordu.
>
> Artık kullanılmayan artlar: `panel_fruit_chain` (bozuk şekil), `hud_fruit_chain_slot` (86 px'te
> saç teli inceliğinde kalıyordu), `hud_chain_arrow` (11 slot varken ok için yer yok — soldan sağa
> sıralama zaten yönü anlatıyor).
>
> **Ek: idle yüzler.** Her `Slot_XX/Icon`'un içine, gövdeyi tam kaplayan bir `Face` child'ı
> eklendi — `FaceSet.Get(FaceExpression.Idle, def.faceSize)` ile her meyvenin kendi boyut
> sınıfına (`sm/md/lg/xl`) uygun yüzü, tek seferlik statik atama (gameplay'deki `FruitFace` gibi
> canlı geçiş/bakış mantığı yok, buna gerek de yok). `FruitChainView._faceIcons` dizisi
> `_fruitIcons` ile birebir aynı alpha'yı paylaşıyor, yani silik meyvenin yüzü de silik.

> **Combo banner ✅ BİTTİ — sonra tamamen değiştirildi.** İlk sürüm (`hud_combo_banner` +
> `HUDCanvas/ComboBanner`) HUD'a sabitti; senin kararınla **HUD'dan tamamen kaldırıldı**,
> yerine **dünya uzayında, birleşme noktasında** beliren bir combo yazısı geldi:
>
> - Banner/panel yok artık, sadece "x3" yazısı — üretilen meyvenin `displayColor`'ında,
>   tam birleşme noktasında beliriyor.
> - **Sonradan revize edildi:** font `Baloo2-ExtraBold` → **`PermanentMarker-Regular`**,
>   punto 3 → 4, her popup'ta rastgele **10–20° sağa/sola yatıklık**, alfa ömrün ilk
>   %55'inde tam opak kalıp sonra sönüyor (eskiden ilk kareden itibaren soluyordu).
>   PermanentMarker atlası **Dynamic** geldiği için `x0123456789` elle pişirildi ve
>   `Clear Dynamic Data On Build` kapatıldı — yoksa ilk combo'da runtime rasterizasyon
>   tırtığı olurdu ve build'de glyph'ler silinirdi.
> - **Sonra bir daha revize edildi — kademeler + teşvik kelimeleri.** Yazı üretilen
>   meyvenin renginde ve tam o meyvenin üstünde doğduğu için aynı renk üstünde aynı renk
>   kalıp okunmuyordu. Üç şeyle çözüldü: (1) sabit `comboPopupRiseDistance` kadar
>   (0.45 birim) yavaşlayarak yükseliyor, (2) `PermanentMarker-Regular SDF Outline.mat`
>   ile krem kontur + hafif alt gölge (Baloo2'deki kontur materyali deseninin aynısı),
>   (3) combo kademesine göre büyüyor.
>   Kademeler: **düşük x2–3 · orta x4–6 · yüksek x7–9 · efsane x10+**. Her kademede
>   punto ×1.3, doğduğu nokta 0.6 birim daha yukarı, ömür 0.15 sn daha uzun.
>   Altına kademeye uygun bir **teşvik kelimesi** geliyor (`Nice!` … `Godly Combo!`) —
>   tek TMP içinde rich text `<size=55%>` ile, ikinci bir obje yok. Kelime listeleri
>   `ComboPopupDirector` inspector'ında, aynı kelime arka arkaya çıkmıyor.
>   Yazı `ForceMeshUpdate` ile gerçek mesh sınırından ölçülüp yatıklığıyla birlikte
>   ekran içine sıkıştırılıyor (`comboPopupClampX/MaxY`) — "Mouthwatering!" efsane
>   puntoda 20° yatıkken tahmini bir payla kenardan taşıyordu.
> - **Yeni olay:** `GameEvents.OnComboMerge(FruitDefinition, Vector2, int combo)`.
>   `OnMerged` + `OnComboChanged`'i ayrı ayrı dinleyip birleştirmek abone sırasına güvenmek
>   olurdu (OnNewRecord'daki gibi garanti değil) — bunun yerine `ScoreSystem.HandleMerged`
>   üçünü de aynı anda, kesin doğru haliyle tek olayda yayınlıyor.
> - **`ComboPopupItem.cs`** (Gameplay) — dünya-uzayı `TextMeshPro` (UGUI değil), `Tick(dt)`
>   var `Update()` yok (kural 7). **`ComboPopupDirector.cs`** (Services) — `UnityEngine.Pool.
>   ObjectPool` ile havuzlu (kural 13, `FruitPool` deseniyle aynı), tek `Update()` tüm aktif
>   popup'ları gezip `Tick` çağırıyor, süresi dolanı havuza iade ediyor.
> - Yeni `GameConfig` alanları: `comboPopupMinCombo` (2 — zincirin ilk halkasında çıkmaz),
>   `comboPopupLifetime` (0.9 sn), `comboPopupRiseSpeed` (1 birim/sn — *sonradan kaldırıldı*). Eski `comboTextDuration`,
>   `comboPunchDuration`, `comboPunchScale` kullanılmadığı için kaldırıldı.
> - `HUDView.cs`'den tüm combo kodu (metin, banner referansı, hide/punch sayaçları) silindi —
>   artık combo ile hiç ilgisi yok, sadece skor + next-fruit önizlemesi kaldı.
> - Play mode'da gerçek `ScoreSystem` akışından (`GameEvents.RaiseMerged` art arda) test edildi:
>   combo doğru sayılıyor, popup doğru renk/konum/metinle beliriyor, birden fazla popup aynı
>   anda havuzdan sorunsuz besleniyor, eski popup'lar solup havuza dönüyor, `Restart()` hepsini
>   temizliyor. `hud_combo_banner` artık kullanılmıyor (ileride başka bir yerde değerlendirilebilir).

> **Faz 6 kapandı.** Kalan iki iş **yapılmayacak** — karar K5/K6'da (bkz. Alınan kararlar).
> Danger vinyeti için mevcut danger line yeterli görüldü, bitmap skor fontu için mevcut TMP
> font yeterli görüldü. `hud_warning_overlay`, `hud_score_digits` kullanılmayacak.

## Faz 5 — Tutorial

| İş | Bu ne | Ne yapılacak | Art |
|---|---|---|---|
| **İlk oyun öğreticisi** | Yeni oyuncuya 2–3 adımda oynanışı gösterme | Adım 1 parmağını kaydır, 2 bırak, 3 aynı meyveleri birleştir. `spotlight_mask` ile gerisi kararır, el ikonu animasyonlu. `SaveService`'e `tutorialDone` (kayıt v3 + migrasyon) | `tutorial_hand_tap/drag`, `arrow_curved`, `spotlight_mask`, `ripple_01..04`, `callout_bubble` |

## Faz 7c — Splash

> **✅ BİTTİ (ikinci sürüm).** *Öne alındı* — Boost/Tutorial/Müzik ile bağımlılığı yoktu.
>
> **Değişmeyen iskelet:** `GameManager.Start()`'taki `SetState(GameState.Menu)` kaldırıldı — oyun
> artık `Boot`'ta (zaten varsayılan state) sessizce bekliyor, o state'e ayrı bir geçiş olayı hiç
> ateşlenmiyor. `SplashPanel.cs` (`MenuPanel` deseninde `UIPanel` alt sınıfı) kendi `Start()`'ında
> `Show()` çağırıyor, yükleme çubuğu dolunca `GameManager.GoToMenu()` ile Boot → Menu geçişini
> yapıyor. `MenuPanel` o geçişi zaten dinliyordu, orada değişiklik gerekmedi.
>
> ### İlk sürüm reddedildi: "splash menüyle karışıyor"
>
> Kullanıcı ilk sürümü "hiç olmamış, splash menüyle iç içe geçiyor" diye reddetti. **İki ayrı
> sebep vardı, ikisi de gerçek:**
>
> 1. **Splash'ın zemini yoktu.** `MenuPanel`'in `Background`'u tam ekran, `sprite = null`, düz
>    `RGBA(0.9959, 0.9354, 0.7050, 1)` (≈ `#FEEEB4`) krem bir dikdörtgen. Splash'ta böyle bir
>    çocuk hiç yoktu; maskotun etrafındaki her boşluktan **dünya uzayındaki oyun tahtası**
>    (`Environment/Background`, `bg_game.png`, sorting order -10, state'ten bağımsız hep açık)
>    görünüyordu. Menüye geçince zemin bir anda kreme dönüyordu — "iki farklı ekran" hissi.
> 2. **Çapraz geçiş (asıl "iç içe geçme").** `OnTick` çubuk dolunca `Hide()` **ve hemen ardından**
>    `GoToMenu()` çağırıyordu. `GoToMenu` → `RaiseStateChanged(Menu)` → `MenuPanel.Show()`
>    zinciri **senkron**, yani aynı karede. `UIPanel.Hide()`/`Show()` ise 0.18 sn'lik bir alfa
>    animasyonu başlatıyor → **0.18 sn boyunca splash sönerken menü açılıyordu**, ikisi de tam
>    ekran, ikisi de ortada büyük görsel taşıyor. Ekranda gerçekten üst üste iki ekran vardı.
>
> ### Ne yapıldı
>
> **`UIPanel`'e `OnShown()` / `OnHidden()` eklendi.** Mevcut `OnShow`/`OnHide` fade'in
> **başında** çağrılıyor; yenileri alfa hedefe **ulaştığında**. Ekrandan ekrana zincirleme artık
> `OnHidden`'da kuruluyor: `SplashPanel.OnHidden()` → `GoToMenu()` → sonra kendini
> `SetActive(false)`. Splash **tam olarak görünmez olduktan sonra** menü açılmaya başlıyor, ikisi
> asla aynı anda görünmüyor. Kapanınca obje tamamen kapatılıyor — bir daha açılmıyor, boşuna
> batch/overdraw taşımasın. Açılışta içeri fade de kaldırıldı (`Start()`'ta `_group.alpha = 1`):
> 0.18 sn yarı saydam splash'ın altından oyun tahtası görünüyordu. `PlaysCloseSfx` da `false`
> yapıldı (`PlaysOpenSfx` zaten öyleydi) — splash bir açılır pencere değil, ekranın kendisi.
>
> **Ortak zemin, `ScreenBackground.cs`.** Renk `GameConfig.screenBackgroundColor`'da; hem
> `SplashPanel/Background` hem `MenuPanel/Background` bu küçük bileşeni taşıyor ve `Awake`'te bir
> kez rengi yazıyor (`Update` yok). Sahnedeki `Image.color` de aynı değere ayarlı, Editor'de doğru
> görünsün diye. İki ekrana ayrı ayrı elle renk girilseydi biri değişince diğeri unutulurdu.
>
> **Yeni kompozisyon** (kullanıcının verdiği sıra; sibling sırası = çizim sırası, tuval 1080×1920):
>
> | # | Obje | Sprite (native) | Anchor / boyut / konum |
> |---|---|---|---|
> | 0 | `Background` | yok, düz renk | tam ekran, `#FEEEB4`, `raycastTarget` açık |
> | 1 | `Decor` | `bg_splash_decor_only` 1057×848 | üst-orta, pivot (0.5, 1), **1080 × 866.42**, aPos (0, 0) → y 960 … 93.6 |
> | 2 | `Logo` | `landing_logo` 1024×521 | orta, **880 × 447.72**, aPos (0, −230) → y −6.1 … −453.9 |
> | 3 | `LoadingBar` | — | orta, **700 × 111**, aPos (0, **−664**) → y −608.5 … −719.5 |
> | 3a | ↳ `Track` | `splash_loading_track` 1741×276 | `LoadingBar` içinde tam gerilmiş |
> | 3b | ↳ `Fill` | `splash_loading_fill` 1853×214 | ortalanmış **675 × 78**, Type=Filled/Horizontal/Left |
>
> Ölçüler tahmin değil, `.meta`'daki sprite rect'lerinden hesaplandı:
> - `Decor`: 1057/848 = 1.2465 → 1080 / 1.2465 = **866.42**. Doku 1440×900, içerik rect
>   (191, 26, 1057, 848) yani **iki yanda 191/192 px, altta/üstte 26 px simetrik boşluk** —
>   rect doğru, bayat değil, dokuya taşmıyor. Balonlar ekranın tepesinden sarkıyor.
> - `Logo`: 1024/521 = 1.9655 → 880 / 1.9655 = **447.72**.
> - `Track`: 1741/276 = 6.308, kutusu 700/111 = 6.306 → **en-boy doğru**, çubuk ezilmiyor.
> - `Fill`: 1853/214 = 8.659. Eski sürümde `Fill` de `Track` gibi tam geriliyordu, yani
>   **8.66 en-boylu sprite 6.31'lik kutuya sokuluyordu** → sarı hap dikeyde %37 şişiyor, uçları
>   oval oluyor ve kahverengi çerçeveyi tamamen örtüyordu. Şimdi 675 × 78 (= 8.654) ile
>   oluğun içine oturuyor; üstte/altta ~16 px koyu oluk görünür kalıyor.
> - `LoadingBar` merkezi **−664**: `MenuPanel/PlayButton`'ın merkeziyle aynı. Çubuk sönerken
>   PLAY butonu tam aynı yerde beliriyor, geçiş "yerinde" duruyor.
>
> **`splash_mascot` kaldırıldı** (obje silindi, deaktif bırakılmadı) — yeni tasarımda yeri yok.
>
> **Dilimleme kontrolü (tekrar eden ders):** `bg_splash_decor_only.png` `spriteMode: 2`
> (Multiple) ama **tek** alt-sprite var (`bg_splash_decor_only_0`) — yani otomatik dilimleyici
> bölmemiş, projedeki genel durumun aynısı (`splash_loading_track_0`, `btn_pill_*_0` … hepsi
> böyle). Rect dokunun içinde ve içerikle bire bir örtüşüyor, dokunulmadı. `landing_logo.png`
> zaten `spriteMode: 1` (Single). Atlas ayarlarına dokunulmadı.
>
> ### Yükleme çubuğu artık sahte değil
>
> Kullanıcının isteği: "yükleme prewarm de yapsın, ama performans önceliğimiz".
>
> **Tespit:** gerçek ısıtma işi `FruitPool.Awake()` (40 meyve) ve `ComboPopupDirector.Awake()`
> (6 popup) idi — toplam **46 `Instantiate`, hepsi tek karede**. `Awake` her objede `Start`'lardan
> önce çalıştığı için (`GameManager` −100, `FruitPool` −90, `ComboPopupDirector` −40,
> `SplashPanel` +100) bu iş **splash daha ekrana gelmeden** bitiyordu. Yani çubuk gerçek bir işi
> gösteremezdi. (`EffectDirector` ısıtma yapmıyor: tek paylaşımlı `ParticleSystem`, havuz yok —
> `GameConfig.effectPrewarmCount` kullanılmıyor.)
>
> **Çözüm — `PrewarmQueue` (statik, `GameEvents` gibi) + `IPrewarmSource`.** Havuzlar `Awake`'te
> ısıtmayı **yapmıyor**, kendilerini kuyruğa **kaydediyor**; `SplashPanel.OnTick` her karede
> `PrewarmQueue.Step(GameConfig.splashPrewarmPerFrame)` çağırıyor (varsayılan **2/kare**, yani
> 46 obje ≈ 23 kare ≈ 0.4 sn @60fps). Sahnede bağlanacak referans yok.
>
> Bu **gerçek bir kazanç**, uydurma iş değil: 46 `Instantiate` ilk kareden çıkarıldığı için
> **açılış ekranı daha erken görünüyor**; toplam maliyet aynı, sadece oyuncunun boş ekrana baktığı
> süre, animasyonlu çubuğa baktığı süreye dönüşüyor. Kural 7'ye (çok kareli iş = director
> `Update`'i) ve kural 11'e (allocation yok) uygun: eski `Prewarm()` geçici bir dizi ayırıyordu,
> yenisi ayırmıyor.
>
> **Ayrıntı:** `PrewarmStep` artık `_pool.Get()` + toplu `Release()` yerine doğrudan
> `_pool.Release(CreateFruit())` yapıyor. Sebep: eskiden ısıtma tek karede bittiği için araya
> fizik adımı girmiyordu; karelere yayılınca giriyor ve `Get()` meyveleri **aktif** hale
> getirdiği için 40 meyve birkaç kare boyunca sahnede duracaktı. `CreateFruit()` objeyi zaten
> kapalı ve `_pooledParent` altında üretiyor. (`ObjectPool.CountAll` bu yüzden 0 kalıyor —
> projede hiçbir yer okumuyor.)
>
> **Çubuğun değeri = `min(iş oranı, geçen süre / splashMinDuration)`.** İş erken biterse çubuk
> 0'dan 1'e sıçramasın diye alt sınır var: `splashMinDuration` **1.2 sn** (eski sahte
> `splashLoadDuration` 1.5 sn'nin yerine — gereksiz yere yavaşlatmamak için kısaltıldı).
> Dürüst özet: **ilk ~0.4 sn'de çubuğu gerçek ısıtma sürüyor, kalan ~0.8 sn kozmetik alt sınır.**
>
> ⚠️ Isıtmanın tek sürücüsü `SplashPanel`. Splash sahneden kaldırılırsa havuzlar ısınmaz
> (yanlış çalışmaz — `ObjectPool` istendiğinde tek tek üretir). Zaten splash olmadan oyun `Boot`'ta
> takılı kalıyor, yani panel şu an da açılış için zorunlu.
>
> **`GameConfig` yeni alanları:** `screenBackgroundColor`, `splashMinDuration`,
> `splashPrewarmPerFrame`. `splashLoadDuration` silindi (asset'te hiç serialize edilmemişti,
> C# varsayılanından geliyordu — kayıp değer yok).

## Faz 7b — Müzik (en son)

| İş | Bu ne | Ne yapılacak | Art |
|---|---|---|---|
| **Müzik** | Arka plan müziği. **Hiç üretilmedi, uygun klip henüz bulunamadı.** Pause'daki toggle ayarı kaydediyor ama çalacak bir şey yok | Önce klip bulunur/üretilir. Sonra `AudioService`'e ayrı `AudioSource` — SFX kanallarından bağımsız, loop açık. ⚠️ **Import ayarı SFX'ten FARKLI:** Streaming + Vorbis. SFX ayarını verirsen 60 sn'lik klip RAM'de ~10 MB olur | müzik dosyası **yok** |

## Faz 8 — Boost sistemi (en son)

| İş | Bu ne | Ne yapılacak | Art |
|---|---|---|---|
| **Boost altyapısı** | 15 güçlendirme: bomba, çekiç, dondurma, mıknatıs, gökkuşağı, karıştır, takas, geri al, yükselt, küçült, sil, satır temizle, yavaşlat, çift puan, ekstra can | `BoostDefinition` SO, envanter + kayıt, hedefleme modu (`target_crosshair`, `target_dim_overlay`), cooldown, HUD tepsisi. **15'ini birden yapma** — dikey dilim tek bir boost'la, mimari oturunca gerisi tekrar | ~250 dosya hazır |
| **`worms` boost'u** ⭐ | *Yeni.* Tatlı kurtçuklar ekran kenarlarından sürünüp seçilen meyveyi yiyor, meyve renginde sis kaplıyor | **✅ KODLANDI, Play Mode testi bekliyor.** Faz 8'in dikey dilimi. 3 yeni script + EffectDirector'e sis + HUD butonu + sahne kurulumu. Ses ve envanter kaydı bilerek dışarıda. Detay: **[`plan_boost_worms.md`](plan_boost_worms.md)** | 9 PNG üretildi ve import edildi; sis/kırıntı/hedefleme mevcut art'tan |
| **`quake` boost'u** ⭐ | *Yeni.* Deprem: ekran sarsılır, tüm meyvelere küçük itmeler uygulanır, yığın yeniden yerleşir ve sıkışık meyveler kendiliğinden birleşir. **Hiçbir meyve silinmiyor** | **✅ KODLANDI + ART/SES İMPORT EDİLDİ, Play Mode testi bekliyor.** Faz 8'in ikinci dikey dilimi — `worms` "sil", bu "karıştır" deseni. `BoostGate` + `IBoostDirector` + `BoostId` ile altyapı tekilleştirildi (`DropController`/`GameOverDetector`'daki hard-coded kontroller ve `BoostButton` artık boost başına çoğalmıyor). Yeni `CameraShaker` (projede ilk kamera sarsıntısı) ve `SaveService.VibrationOn`'un ilk tüketicisi. Kutu/collider/yerçekimi **dokunulmadı**. Detay: **[`plan_boost_quake.md`](plan_boost_quake.md)** | **6 PNG + 2 ses üretildi ve import edildi.** `quake_ground_crack` iki üretici artefaktı (pembe kayma + gri tül) için ton düzeltmesinden geçti; `quake_pebble` henüz kullanılmıyor. Çakma/buton durumları/yüzler mevcut art'tan |
| **`undo` boost'u** | Son hamleyi geri alma | En zoru: tüm meyvelerin pozisyon/hız/tier'ı + skorun snapshot'ı. Havuz mimarisi kolaylaştırıyor ama ayrı tasarım işi | `boost_undo*`, `undo_ghost`, `undo_trail_arrow` |
| **Devam teklifi** | Kaybedince boost harcayıp devam etme | Sonuç ekranında `continue_boost_prompt`. Panelde yeri şu an boş | `continue_boost_prompt` |

## Önerilen sıra

1. **Boost** — Tutorial'dan önce
2. **Tutorial** — Boost'tan sonra
3. **Müzik** — en son, uygun klip henüz bulunamadı

*(Evrim zinciri, Combo popup, Splash ✅ bitti; Danger vinyeti ve Bitmap skor fontu yapılmayacak —
yukarıdaki notlara ve K5/K6'ya bak.)*

---

## Mevcut durum

| | Başlangıç | Şimdi |
|---|---|---|
| Script | 18 dosya | **40 dosya** |
| Prefab | 1 (`Fruit.prefab`, child'ı yok) | 1 (`Fruit.prefab` + `Face` child'ı) |
| Sahne | `Game.unity` + gereksiz `SampleScene` | `Game.unity` (SampleScene silindi) |
| Art | 477 PNG — **25**'i bağlı | 478 PNG — **100**'ü bağlı, 378 bekliyor |
| Ses | 12 klip, müzik yok | 11 klip bağlı (`combo.wav` çıkarıldı), müzik hâlâ yok |

**Çalışan sistemler:** state machine (Menu/Playing/Paused/GameOver), drop + input buffer + gecikmeli pending, merge kuyruğu, obje havuzu, bag randomizer, skor + combo, save + migrasyon, game over algılama, danger line, pop/squash, drop indicator, ses, meyve suyu efektleri, meyve yüzleri, dropper dalı + next göstergesi, pause paneli + ayarlar, sonuç ekranı, ana menü, evrim zinciri şeridi, combo popup (dünya uzayında), splash ekranı.

**Kapatılan bug'lar:** `Resume()` çağrılmıyordu (oyun kilitleniyordu) · `SaveService.cs:40` stray `3` (derleme hatası) · pause'dan dönüşte skor sıfırlanıyordu · rekor HUD'da hep 0 görünüyordu (init sırası) · `GameOverPanel` hiç kapanmıyordu · restart menüye atıyordu · skor 0'a geri sayıyordu · zincirleme birleşmede kendi sesi guard'a takılıyordu.

---

## Her fazda geçerli kurallar

Bunlar ses entegrasyonunda kullandığımız kurallar; devam ediyoruz.

1. **Lambda yasağı** — olay/buton aboneliklerinde lambda yok. İsimli metot kullan, yoksa `-=` / `RemoveListener` yapamazsın.
2. **`+=` / `-=` simetrisi** — `OnEnable`'daki her `+=` için `OnDisable`'da birebir `-=`.
3. **`volume` clamp** — `AudioSource.volume` 0–1. Fark istiyorsan klip seviyesinden ver, koddan 1'in üstüne çıkma.
4. **UI zamanı `unscaledTime`** — panel açıkken `timeScale = 0`. Panel animasyonu, guard, ses zamanlaması `unscaledDeltaTime` / `unscaledTime` kullanmalı. Oynanış zamanı `deltaTime` kalır.
5. **Null guard** — singleton'lara (`AudioService.Instance`, `GameManager.Instance`) erişimde null kontrolü.
6. **Sihirli sayı yok** — ayarlanabilir her değer `GameConfig`'e gider, Tooltip'iyle.

### Performans kuralları (bu plan boyunca kritik)

7. **Tek `Update` kuralı** — 60 meyvenin her birinde `Update()` çalıştırma. Unity her `Update` çağrısında managed↔native sınırı geçer; 60 obje = 60 geçiş/kare. Yerine **merkezî bir director** tek `Update`'te döner ve bileşenlerin `Tick(dt)` metodunu çağırır. Yüz sistemi ve efekt sistemi bu deseni kullanacak.
8. **Coroutine yok** — her coroutine allocation yapar. Süreli davranışlar `float` sayaç + director döngüsü ile.
9. **`sprite` sadece değişince atanır** — `SpriteRenderer.sprite`'a atama mesh rebuild tetikler. `if (_current == next) return;` şart.
10. **Atlas disiplini** — aynı karede birlikte görünen şeyler aynı atlasta olmalı, yoksa her materyal değişimi bir draw call. (Faz 0'da düzeltiyoruz.)
11. **Sıcak döngüde allocation yok** — LINQ yok, `foreach` yerine `for` + index, string birleştirme yok, `GetComponent` runtime'da yok (hepsi `Awake`'te cache'lenir).
12. **Karar sıklığını ayır** — "ne yapılacak" kararı 10 Hz yeter (`GameConfig`'ten ayarlı). Sadece görsel yumuşatma (lerp) her karede döner.
13. **Havuz** — hiçbir şey oynanış sırasında `Instantiate` edilmez. `FruitPool` deseni kopyalanır.

---

> Aşağısı **uygulama günlüğü**: her fazın nasıl yapıldığı, alınan kararlar ve ölçümler.
> Güncel iş listesi için yukarıdaki YAPILACAKLAR bölümüne bak.

---

## Faz 0 — Temel + atlas hazırlığı

**Amaç:** bug'ı kapat, batching'i düzelt. Yarım gün.

### 0.1 Pause kilidini kır
`GameManager.Resume()` çağıran kimse yok. Faz 1'de pause panelin Resume butonu bunu çağıracak — ama o panel gelene kadar pause butonu oyunu kilitliyor. Faz 1'i hemen arkasına yapacağımız için ayrı bir geçici çözüm yazmıyoruz, sadece bilerek bırakıyoruz.

> **Not:** Faz 1 bitmeden pause butonuna basma.

### 0.2 Yüz dosyalarını `Single`'a çevir ⚠️ **en kritik adım**

Ölçüm sonucu: **48 yüz dosyasının hiçbiri tek sprite değil.** Unity'nin otomatik dilimleyicisi her yüzü kopuk parçalarına ayırmış.

`face_happy_xl.png` → 5 parça: sol göz (71×41), sağ göz (71×41), sol yanak (63×64), ağız (90×59), sağ yanak (63×64).
`face_love_sm.png` → 3 parça: sol göz, ağız, sağ göz.

Dağılım: 26 dosya 5 parça · 8 dosya 3 parça · 5 dosya 7 parça · 3 dosya 8 parça · 3 dosya 4 parça · 1 dosya 6 parça · 2 dosya 2 parça. **Tek parça olan: 0/48.**

Bu haliyle bir yüzü göstermek için 5 ayrı `SpriteRenderer` konumlandırmak gerekir — hem saçma hem performans katliamı.

**Yapılacak:** `Art/Fruits/Faces` içindeki 48 PNG'yi **hep birlikte seç** → Inspector → **Sprite Mode: Single** → Apply. Artık her dosya = tam kare tek sprite, pivot ortada. Gövde de 512×512 tuvale ortalı çizildiği için yüz tam yerine oturur.

> ⛔ **`Base/` klasörüne DOKUNMA.** 11 gövde şu an `Multiple` modda ve tek alt-sprite'a **470×470 kırpılmış** (512 değil). `Single`'a çevirirsen sprite 512'ye çıkar, tüm meyveler ~%9 büyür ve `colliderRadius` / `colliderOffset` ayarların bozulur. Fizik ayarlıysa öyle kalsın.

### 0.3 `FruitAtlas` — `Faces/` ekle + boyutu büyüt

Şu an `FruitAtlas.spriteatlasv2` sadece `Base/`'i paketliyor. Yüzler atlas dışında = her yüz ayrı materyal.

- `FruitAtlas`'ı seç → **Objects for Packing** listesine `Art/Fruits/Faces` klasörünü ekle
- **Max Texture Size: 4096** — üst sınır olarak. Paketleyici sığdığı en küçük boyutu seçer.
- Compression: Normal Quality. Mobilde ASTC tercih et.

**Ölçüm sonucu:** tek sayfa **2048×2048**, 59 sprite. Ham tuval alanı 7.06M px² (2048 kapasitesinin %168'i) olmasına rağmen sığdı, çünkü **paketleyici saydam kenarları kırpıyor** — yüzler 512×512 tuvalde duruyor ama mürekkep alanı çok küçük (`face_happy_xl`'in özellikleri ~326×126'lık bir kutuda).

Üst sınırı 4096'da bırakmanın anlamı: ileride art büyürse atlas **sayfaya bölünmek yerine** tek sayfada büyür. Sayfa bölünmesi kötü, çünkü her sayfa ayrı materyal olur ve gövde/yüz sorting order'da iç içe geçtiği için batch'ler **her sprite'ta** kırılır — tam kaçındığımız senaryo.

*İleride bellek sıkışırsa:* `face_*_xl` dosyalarını Max Size 256'ya indirmek yer kazandırır. Şimdilik gerek yok; karpuzun yüzü ekranda ~430 px kaplıyor, 512 doğru çözünürlük.

### 0.4 `EffectsAtlas` oluştur + efektlerde de dilimleme var ⚠️

`Art/Effects/` altında `EffectsAtlas.spriteatlasv2` → `Effects/Merge` + `Effects/Particles` klasörleri. Faz 2 bunu kullanacak.

**İlk paketlemede 35 dosyadan 262 sprite çıktı** — dilimleme sorunu efektlerde de vardı. Bir patlama karesi zaten dağılan kıvılcımlardan oluşuyor, otomatik dilimleyici her kıvılcımı ayrı sprite yapmış:

| Dosya | Parça |
|---|---|
| `merge_burst_06` | **52** |
| `merge_burst_05` | 49 |
| `merge_burst_04` | 40 |
| `merge_burst_07` | 38 |
| `particle_smoke_04` | 18 |
| `merge_burst_08` | 16 |
| `merge_ring` | 6 |
| `particle_smoke_03` | 7 |
| `merge_burst_02/03`, `particle_smoke_01/02` | 2–4 |

`merge_burst_01` tek parçaydı (patlamanın çekirdeği hâlâ birleşik), 02'den sonra kıvılcımlar ayrıldıkça parça sayısı arttı.

**Çözüm:** 35 efekt dosyasının hepsi `Single`'a çevrildi → 35 dosya = 35 sprite. Atlas tek sayfa **1024×2048**.

### 0.4b UI ikonlarında da dilimleme vardı ⚠️

Faz 1'e geçerken çıktı: `Art/UI` altında **17 dosya** dilimlenmişti.

- **Icons (15):** `icon_sound_on_white`(3), `icon_vibrate_on_white`(5), `icon_vibrate_off_white`(3), `icon_music_on_dark`(3), `icon_pause_white`(2), `icon_pause_dark`(2), `icon_swap_white/dark`(2), `icon_settings_slider_white/dark`(3), `icon_sound_on_dark`(3), `icon_vibrate_on_dark`(3), `icon_vibrate_off_dark`(4), `icon_leaderboard_dark`(4), `icon_upgrade_white`(3) → **hepsi `Single`'a çevrildi**
- **HUD (2):** `hud_score_digits`(10) → **dokunulmadı**, o gerçekten 0–9 rakam sayfası, dilimli olması doğru. `hud_dropper_guide`(15) → şimdilik dokunulmadı, Faz 6'da bakılacak.
- **Buttons (44) ve Panels (15):** hepsi zaten tek parça, sorun yok.

UIAtlas 208 → 178 sprite.

**Ders:** yeni bir art klasörünü ilk kez kullanmadan önce alt-sprite sayısını kontrol et. Üç ayrı yerde (yüzler, efektler, ikonlar) aynı otomatik dilimleme hatası çıktı.

**Sprite metadata'sı hakkında not:** `panel_modal_md`'nin importer rect'i `(501, 481, 1077, 958)` görünüyor ve 1024×945'lik dokuda sınır dışı duruyor — **bozuk değil.** Kaynak PNG daha büyük, `maxTextureSize` küçültmüş, dilim koordinatları kaynak uzayında saklanıyor. Gerçek imported sprite **530×471**. Ölçü alırken importer metadata'sına değil `Sprite.rect`'e bak.

### 0.5 Kaynak doku sıkıştırmasını kapat

Paketleme 94 uyarı üretti: *"Source Texture is using compressed format... please use uncompressed format"*. Sıkıştırılmış kaynağı atlasa paketlemek **çift sıkıştırma** demek — kalite kaybı.

`Base` + `Faces` + `Effects/Merge` + `Effects/Particles` = **94 dosyanın hepsinde Compression → Uncompressed.** Nihai sıkıştırmayı atlas yapıyor; kaynaklar atlasa paketlendiği için build boyutuna girmiyorlar, sadece Library önbelleği büyüyor. 94 uyarı sıfırlandı.

### 0.6 Temizlik
- `Assets/FruitMerge/Scenes/SampleScene.unity` silindi (git'te takipli, geri alınabilir)
- Build Settings → sadece `Game.unity` (Faz 7'de menü paneli aynı sahnede olacak, yeni sahne gerekmiyor)

### ✅ Faz 0 sonuç — tamamlandı

| Kontrol | Sonuç |
|---|---|
| Yüzler `Single` | **48/48** |
| Gövdeler dokunulmamış (`Multiple`, 470×470) | **11/11** |
| Efektler `Single` | **35/35** |
| `FruitAtlas` | 59 sprite, tek sayfa 2048×2048 |
| `EffectsAtlas` | 35 sprite, tek sayfa 1024×2048 |
| Kaynak sıkıştırma kapatıldı | 94/94 dosya |
| Runtime'da atlas kullanılıyor | ✔ `sactx-0-2048x2048-DXT5-FruitAtlas` |
| Meyve yarıçapları | kiraz 0.1921 · karpuz 1.2250 (değişmedi) |
| Konsol | **0 hata, 0 uyarı** |

---

## Faz 1 — Pause panel + ayarlar

**Amaç:** pause kilidini kır, ayarları pause panelin içine koy.

Senin kararın: ayrı ayarlar ekranı yok, her şey pause panelde.

### İçerik
| Eleman | Art | Davranış |
|---|---|---|
| Modal kutu | `panel_modal_md` | `UIPanel`'den türeyen `PausePanel` |
| Başlık şeridi | `panel_header_ribbon` | "Duraklatıldı" |
| Devam | `btn_pill_green_*` + `icon_play_white` | `GameManager.Resume()` ← **bug burada kapanıyor** |
| Yeniden başlat | `btn_pill_yellow_*` + `icon_restart_white` | `GameManager.Restart()` |
| Ana menü | `btn_pill_gray_*` + `icon_home_white` | Faz 7'ye kadar devre dışı/gizli |
| Kapat | `btn_circle_close_*` | Resume ile aynı |
| Ses aç/kapa | `icon_sound_on/off_*` | `AudioService.SetMasterVolume(0/1)` + `PlayToggle(bool)` |
| Müzik aç/kapa | `icon_music_on/off_*` | Faz 7'ye kadar devre dışı |
| Titreşim aç/kapa | `icon_vibrate_on/off_*` | Ayar saklanır, kullanımı sonra |

### Yapılacaklar
1. **`SaveService`'e ayar alanları** — `SaveData`'ya `sfxOn`, `musicOn`, `vibrationOn` (hepsi default `true`) + `version` 2'ye çık ve eski kaydı migrate et.
2. **`PausePanel.cs`** (`Scripts/UI/`) — `UIPanel`'den türet. Kural 4 gereği `UIPanel` zaten `unscaledDeltaTime` kullanıyor, dokunma.
3. **`GameManager`** — `Pause()`/`Resume()` zaten var, `EnterState` `timeScale`'i yönetiyor. Panelin `Show()`/`Hide()`'ı `OnStateChanged` olayına bağlanmalı, butondan doğrudan değil — state machine tek doğru kaynak kalsın.
4. **HUD pause butonu** — zaten `PlayUIClick()` çalıyor, dokunmaya gerek yok.
5. **Sahne** — `MainCanvas` altına PausePanel, `CanvasGroup` şart (`UIPanel` `[RequireComponent]`).

### Dikkat
- `PausePanel` panel açılış/kapanış sesini **çalacak** (`GameOverPanel`'in aksine). `UIPanel.PlaysOpenSfx` default `true`, ekstra iş yok. `panel_open.wav` ve `panel_close.wav` ilk kez burada duyulacak.
- Toggle'lar `AudioService.PlayToggle(bool)` çağırır — klipler bağlı, hazır.
- Panel açıkken `timeScale = 0`; toggle animasyonu varsa `unscaledDeltaTime`.

### Test
1. Pause → panel açılıyor, `panel_open.wav` duyuluyor, oyun donuyor
2. Devam → panel kapanıyor, `panel_close.wav`, oyun kaldığı yerden sürüyor **(bug kapandı)**
3. Ses kapat → hiçbir SFX duyulmuyor; aç → geri geliyor
4. Ses kapalıyken oyunu kapat/aç → ayar hatırlanıyor
5. Pause → Yeniden başlat → yeni oyun, `timeScale` 1
6. Pause açıkken meyve bırakılamıyor (`DropController` `IsPlaying` kontrol ediyor — doğrulayın)

---

## Faz 2 — Merge efektleri (particle)

**Amaç:** birleşmede görsel patlama. Ses zaten aynı karede çalıyor, görsel eşleşince his tamamlanıyor.

### Eldeki art
- `merge_burst_01..08` — 8 kareli patlama (flipbook)
- `merge_sparkle_01..06` — 6 kareli parıltı
- `merge_flash`, `merge_ring` — tek kare, ölçek+fade animasyonu için
- `Effects/Particles/` — `particle_shard_01..04`, `particle_smoke_01..04`, `particle_confetti_01..06`, `particle_star`, `particle_sparkle`, `particle_circle_soft`

### Mimari kararı — **meyve suyu fışkırması** (flipbook DENENDİ, REDDEDİLDİ)

İlk denemede el çizimi kareleri flipbook olarak oynattık (`merge_burst_01..08` + sparkle + ring). **Görsel olarak beğenilmedi ve kaldırıldı.**

Yerine gelen: **meyvenin her yanından kendi renginde meyve suyu fışkırıyor.**

**Tek paylaşımlı `ParticleSystem` + `Emit()`** — her birleşmede yeni sistem yaratmak yerine iki sabit sistem tutuluyor ve `Emit(EmitParams, count)` ile o noktadan patlatılıyor. Parçacık havuzunu Unity native tarafta zaten yönetiyor, bu yüzden:

- **obje havuzu YOK** — gereksiz
- **`Update` döngüsü YOK** — `EffectDirector`'da hiç `Update` yok, parçacıklar kendini native tarafta günceller. Kural 7'nin en iyi hali: sıfır managed döngü.
- `EmitParams` struct → çağrı başına allocation yok (kural 11)
- `simulationSpace = World` → damlalar emitter'ı takip etmez, havada kalır ve düşer

İki katman:

| Sistem | Ömür | Yerçekimi | Rol |
|---|---|---|---|
| `JuiceDroplets` | 0.45–0.85 sn | 1.30 | ana damlalar, ağır, yere düşer |
| `JuiceMist` | 0.22–0.45 sn | 0.45 | ince serpinti, hızlı, geniş yayılır |

Damla sayısı/boyutu/hızı **tier'a göre** ölçekleniyor: `startSpeedMultiplier` ve `startSizeMultiplier` her `Emit` öncesi yazılıyor, `shape.radius` meyvenin yarıçapına ayarlanıyor ki su gövdenin **kenarından** çıksın.

> ⚠️ `startSpeed`/`startSize` **TwoCurves** modunda kurulu, TwoConstants değil. TwoConstants modunda `startSpeedMultiplier` çalışmıyor — koddan ölçekleme sessizce etkisiz kalırdı.

Renk `FruitDefinition.displayColor`'dan: kiraz R0.91/G0.00/B0.24, limon R0.88/G0.89/B0.04, üzüm R0.59/G0.07/B0.71. Tier başına asset yok.

**Ölçülen sonuç:**

| Meyve | Damla | Çıkış yarıçapı | Damla boyutu | Hız |
|---|---|---|---|---|
| Blueberry (1) | 12 | 0.23 | 0.038–0.088 | 1.7–3.2 |
| Grape (3) | 17 | 0.32 | 0.053–0.125 | 2.0–3.8 |
| Peach (6) | 24 | 0.62 | 0.102–0.238 | 2.5–4.7 |
| Watermelon (10) | 34 | 0.98 | 0.162–0.377 | 3.2–5.9 |
| karpuz + karpuz | **61** | — | — | — |

**Materyal:** `Mat_Juice` — `Universal Render Pipeline/Particles/Unlit`, saydam alpha blend, doku `particle_circle_soft`. Additive değil: meyve suyu sıvı, ışık değil.

### ✅ Faz 2 sonuç — tamamlandı ve onaylandı

Sahnede: `EffectDirector` + `JuiceDroplets` + `JuiceMist`, üçü de bağlı. Konsol temiz.

**Silinen (flipbook denemesinden kalan):** `SpriteEffect.cs`, `EffectDefinition.cs`, `EffectSprite.prefab`, 5 × `Effect_*.asset`, `Data/Effects` klasörü.

### Faz 2'den kalan temizlik borcu (küçük, sonra)

Flipbook kalkınca `EffectsAtlas` büyük ölçüde işlevsiz kaldı:

1. **`Effects/Merge` (16 dosya) artık hiç kullanılmıyor** — `merge_burst_*`, `merge_sparkle_*`, `merge_flash`, `merge_ring` atlasta yer kaplıyor ama hiçbir kod onlara dokunmuyor.
2. **`particle_circle_soft` iki kez build'e giriyor** — `Mat_Juice` doğrudan `Texture2D`'yi referans ediyor; doğrudan referans varsa Unity orijinal dokuyu da build'e katıyor, üstüne atlas kopyası da var.
3. **Sıkıştırma yanlış yönde** — Faz 0'da atlas kaynağı oldukları için `Uncompressed` yapılmıştı. Artık materyalde doğrudan kullanıldığı için RAM'e sıkıştırılmamış giriyor (256×256 RGBA = 256 KB, DXT5 = 64 KB).

**Çözüm:** `EffectsAtlas`'tan `Effects/Particles`'ı çıkar (particle materyalleri sprite değil doku kullanıyor), `particle_circle_soft`'u `Compressed`'a al. `Effects/Merge` Faz 8'de yeniden değerlendirilir.

### Yapılacaklar
1. **`EffectDirector.cs`** (`Scripts/Services/`) — tek `Update`, aktif efekt listesi, `FruitPool` deseninde havuz.
2. **`MergeEffect.cs`** — `Tick(dt)` metodu var, `Update` **yok**. Kare ilerletme + fade + ölçek.
3. **`EffectDefinition`** ScriptableObject — kare dizisi, kare süresi, ölçek eğrisi, renk. Farklı efektler (merge / max tier / boost) aynı koddan beslenir.
4. **Bağlantı** — `EffectDirector`, `GameEvents.OnMerged` ve `OnMaxTierMerged`'e abone (ikisi de `Vector2 konum` taşıyor, hazır).
5. **Tier rengi** — `FruitDefinition.displayColor` var. Efekti meyve rengiyle tint'le, her tier'a ayrı asset üretmeye gerek yok.
6. **Max tier** — karpuz+karpuz için daha büyük/uzun varyant + `confetti` emit.

### Dikkat
- **URP kullanıyorsun.** Efekt materyali `Universal Render Pipeline/2D/Sprite-Unlit` (veya particle için URP Unlit) olmalı, yoksa pembe render.
- **Overdraw** — mobilde en büyük risk. Additive blend'li büyük saydam quad'lar üst üste binince fill rate patlar. Efekt sprite'larını küçük tut, eşzamanlı efekt sayısını `GameConfig`'ten sınırla (`maxConcurrentEffects`, öneri 8), sınıra gelince en eskiyi geri dönüştür.
- Efekt sorting order'ı meyvelerin üstünde ama UI'ın altında olmalı.
- Efektler `EffectsAtlas`'ta (Faz 0.3).

### Test
1. Birleşme → efekt tam birleşme noktasında, sesle aynı karede
2. 5 zincirleme birleşme → 5 efekt, hepsi düzgün, kare atlamıyor
3. Karpuz+karpuz → belirgin şekilde daha büyük efekt
4. Profiler: birleşme anında GC allocation **0 B** (havuz çalışıyorsa öyle olmalı)
5. Stats: birleşme sırasında batch artışı 1–2'yi geçmiyor

---

## Faz 3 — Meyve yüzleri

En detaylı sistem. Alt fazlara böldüm; her birini ayrı test et.

### Eldeki art
12 ifade × 4 boyut = 48 dosya: `idle`, `happy`, `love`, `excited`, `wink`, `surprised`, `worried`, `scared`, `angry`, `dizzy`, `sleepy`, `squish` × `sm`/`md`/`lg`/`xl`.

Senin spec'in 8 tanesini kullanıyor: **love, happy, idle, sleepy, scared, worried, dizzy, squish**. Kalan 4 (`excited`, `wink`, `surprised`, `angry`) opsiyonel — combo ≥ 3'te `excited`, yeni rekorda `wink` gibi sonra eklenebilir.

### Davranış spec'i (senin anlattığın)

| Durum | Kim | İfade | Süre |
|---|---|---|---|
| Birleşme | üretilen meyve | `love` | 2 sn |
| Birleşme, elma+elma ve üstü | **diğer tüm** meyveler | `happy` | 2 sn (love ile eşzamanlı) |
| Meyve düşerken | diğer meyveler | `idle` + düşen meyveyi **takip** | düşme boyunca |
| Oyuncu 5 sn'den fazla beklerse | diğer meyveler | `sleepy` | bekleme boyunca |
| Danger line'a %10 yaklaşınca | meyveler | `scared` 1.5 sn ↔ `worried` 1.5 sn, **danger line'a bakar** | %10 uzaklaşınca eski hale |
| Oyun sonu | line'ın **üstündeki** meyveler | `dizzy` | kalıcı |
| Oyun sonu | line'ın **altındaki** meyveler | `squish` | kalıcı |

### Öncelik sırası (çakışma çözümü) — **K1 kararı: kutlama önce**

Birden fazla durum aynı anda doğru olabilir. Sıralama:

```
1. Oyun sonu     (dizzy / squish)   ← terminal, bir kez uygulanır, director durur
2. Meyve ifade kilidi (love 2 sn)   ← MergeHandler'ın Express() ile koyduğu kilit
3. Kalabalık kutlaması (happy 2 sn) ← tier >= 6 birleşmede
4. Danger        (scared ↔ worried)
5. Sleepy        (5 sn beklemede)
6. Düşme takibi  (idle + bakış)
7. Idle          (varsayılan)
```

**Kutlama danger'ı bastırır.** Senin kuralın: kutlama oynar, bittiğinde yeni birleşme yoksa danger devralır. Uygulaması:

- Nitelikli her birleşme (tier ≥ 6) 2 sn'lik sayacı **sıfırdan başlatır** — yani zincir birleşmede kutlama kesintisiz sürer, her halka sayacı tazeler.
- Sayaç bitince `FaceDirector` bir sonraki karar turunda (10 Hz) doluluk oranına bakar; hâlâ ≥ %90 ise yüzler `scared`/`worried`'a düşer.
- Nitelikli olmayan birleşme (tier < 6) kalabalık sayacını tazelemez — zaten başlatmıyor. Ama üretilen meyvenin kendi `love` kilidi 2 sn boyunca onun danger yüzünü de bastırır. Yani tehlike anında küçük bir birleşme yaparsan: yeni meyve aşık, geri kalan herkes korkmuş. İstenen davranış bu.
- **Meyve bazlı kilit, global moddan güçlüdür.** `Express(ifade, süre)` çağrılan meyve, süre bitene kadar global mod tarafından ezilmez. Oyun sonu tek istisna — o her kilidi kırar.

### 3A — Altyapı

**Yeni dosyalar**
- `Scripts/Data/FaceSet.cs` — `FaceExpression` (12) ve `FaceSize` (4) enum'ları + `FaceSet` ScriptableObject.
  - Inspector'da 12 satır × 4 sprite.
  - `OnEnable`'da düz bir `Sprite[48]` dizisine flatten et, indeks `(int)expression * 4 + (int)size`. **Dictionary değil** — O(1), allocation yok, GC yok (Kural 11).
- `Scripts/Gameplay/FruitFace.cs` — meyvenin `Face` child'ında.
  - `Update` **yok**, `Tick(float dt)` var (Kural 7).
  - `SetExpression(FaceExpression)` — sadece değişince `sprite` atar (Kural 9).
  - `SetLookTarget(Transform)` / `ClearLook()`.
  - `ResetFace()` — havuzdan çıkarken çağrılır.
- `Scripts/Services/FaceDirector.cs` — **sistemin tek `Update`'i.**

**Değişecek dosyalar**
- `Fruit.prefab` → `Face` adında child GameObject + `SpriteRenderer` + `FruitFace`
- `Fruit.cs` → `public FruitFace Face { get; private set; }`, `Awake`'te cache; `Initialize`'da `Face.ResetFace()` + `sortingOrder = _sr.sortingOrder + 1`; `ResetState`'te de reset
- `FruitDefinition.cs` → `public FaceSize faceSize;` + `public Vector2 faceOffset;`
  - `faceOffset` neden gerekli: gövde sprite'ı 512 tuvalden **470×470'e kırpılmış**, yüz ise `Single` olarak tam 512. İkisi de ortalı olduğu için çoğu meyvede hizalanır, ama sapı/yaprağı üstte olan meyvelerde (`colliderOffset`'i sıfır olmayanlar) yüz birkaç piksel kaymış görünebilir. Meyve başına ince ayar için.
- `GameOverDetector.cs` → `public float FillRatio => _fillRatio;` ve `public float LineY => transform.position.y;` (iki satır — doluluk oranı **zaten** 10 Hz'te hesaplanıyor, yeniden hesaplama yok)
- `GameConfig.cs` → yeni `[Header("yüz ifadeleri")]` bloğu

**`GameConfig`'e eklenecekler**
```
faceMoodInterval        = 0.1f   // karar sıklığı — 10 Hz yeter (Kural 12)
faceMergeReactionTime   = 2f     // love/happy süresi
faceCrowdReactionMinTier= 6      // üretilen tier >= bu ise diğerleri happy
faceIdleToSleepy        = 5f     // son bırakmadan kaç sn sonra sleepy
faceDangerRatio         = 0.90f  // bu doluluktan sonra scared/worried
faceDangerExitRatio     = 0.88f  // histerezis — eşikte titremeyi önler
faceScaredDuration      = 1.5f
faceWorriedDuration     = 1.5f
faceLookRadius          = 0.08f  // bakış kaymasının yarıçapı (local birim)
faceLookSpeed           = 8f     // bakışın hedefe yaklaşma hızı
faceFallSpeedThreshold  = 1.5f   // bu hızın üstü "düşüyor" sayılır
```

**`FaceSize` önerisi (11 meyve)**
| Tier | Meyve | Boyut |
|---|---|---|
| 0–1 | Cherry, Blueberry | `Sm` |
| 2–3 | Lime, Grape | `Md` |
| 4–6 | Orange, GreenApple, Peach | `Lg` |
| 7–10 | Coconut, Dragonfruit, Pineapple, Watermelon | `Xl` |

**Çözülmesi gereken iki teknik detay**

1. **Meyveler dönüyor** — `Fruit.Drop()` rastgele `angularVelocity` veriyor. Yüz child olduğu için gövdeyle birlikte döner, baş aşağı kalır. Çözüm: `FaceDirector` her tick'te yüzün **world rotation**'ını `Quaternion.identity`'ye sabitler. Bakış kayması da world uzayında verilir, yoksa dönen parent yüzünden yön şaşar.
2. **Yüz gövdenin üstünde çizilmeli** — gövde `sortingOrder = 100 - tier`. Yüz `= gövde + 1`. Aynı atlasta oldukları için (Faz 0.2) sorting order farkı batch'i bozmaz.

### 3B — Birleşme tepkisi (love / happy)

- **`love` (üretilen meyve):** `MergeHandler.Execute` içinde `spawned` referansı **zaten var**. Bir satır: `spawned.Face.Express(FaceExpression.Love, _config.faceMergeReactionTime)`. Bonus: `MergeHandler._config` şu an tanımlı ama kullanılmıyor — burada işe yarıyor.
- **`happy` (kalabalık):** `FaceDirector`, `GameEvents.OnMerged`'e abone. `produced.tier >= faceCrowdReactionMinTier` ise 2 sn'lik "kalabalık neşesi" modu başlar. Yeni olay eklemeye gerek yok, mevcut `OnMerged(FruitDefinition, Vector2)` yetiyor.
- **Karpuz+karpuz:** `OnMaxTierMerged` — iki karpuz da yok oluyor, `love` verilecek meyve kalmıyor. Sadece kalabalık `happy` olur.

> **K2 kararı:** eşik **üretilen** tier üzerinden. Elma tier 5; elma+elma → şeftali (tier 6) üretir. `faceCrowdReactionMinTier = 6`. `GameConfig`'ten oynatılabilir.

**Test:** iki kiraz birleştir → sadece yeni meyve `love`, diğerleri değişmez. İki elma birleştir → yeni şeftali `love` **ve** ekrandaki tüm meyveler `happy`, 2 sn sonra hepsi normale döner.

### 3C — Düşme takibi + sleepy

- **Düşen meyveyi bulma:** `GameEvents.OnFruitDropped` sadece `FruitDefinition` taşıyor, **instance taşımıyor**. Olayın imzasını değiştirmeyeceğiz (`AudioService`, `SaveService`, `HUDView` ona bağlı). Yerine `FaceDirector`, 10 Hz'lik karar turunda `FruitPool.Active` içinde `DropTime`'ı en büyük **ve** hızı `faceFallSpeedThreshold` üstünde olan meyveyi bulur — düşen o. `Fruit` zaten `IsDropped`, `DropTime`, `Body` açıyor. Sıfır coupling, sıfır allocation.
- **Takip:** diğer meyveler `idle` + yüz o hedefe doğru `faceLookRadius` kadar kayar, `faceLookSpeed` ile lerp. Lerp her karede döner (ucuz matematik), karar 10 Hz'te.
- **Sleepy:** `FaceDirector` `OnFruitDropped`'ta `_lastDropTime = Time.time` kaydeder. `Time.time - _lastDropTime > faceIdleToSleepy` → hepsi `sleepy`.

**Test:** meyve bırak → yerleşik meyveler ona bakıyor, düşerken izliyor. Yere değince bakış merkeze dönüyor. 5 sn dokunma → hepsi uykuya geçiyor. Bırak → anında uyanıyor.

### 3D — Danger tepkisi

- Eşik: `GameOverDetector.FillRatio >= faceDangerRatio` (0.90 = line'a %10 yaklaşmış). Çıkış `faceDangerExitRatio` (0.88) — histerezis, yoksa eşikte yüzler titrer.
- Aktifken meyveler `scared` 1.5 sn ↔ `worried` 1.5 sn arasında geçiş yapar ve **danger line'a bakar** (`GameOverDetector.LineY` → hedef `(kendi x, lineY)`).
- Alt eşiğe düşünce normale döner.
- **Bonus:** `hud_warning_overlay.png` kullanılmıyor. Danger aktifken tam ekran vinyet ekle — yüzlerle birlikte tehlikeyi çok net anlatır.

> **K3 kararı: tüm meyveler.** `GameOverDetector` doluluk oranını zaten 10 Hz'te global hesaplıyor — ek maliyet sıfır, görsel etki daha güçlü.

**Test:** meyveleri yığ → %90'a gelince tüm yüzler korkuyor ve yukarı, çizgiye bakıyor. Birleştirip yığını düşür → normale dönüyor. Eşikte gezinirken yüzler titremiyor (histerezis).

### 3E — Oyun sonu

- `GameEvents.OnGameOver` → tek geçiş: merkezi `LineY`'nin üstündeki meyveler `dizzy`, altındakiler `squish`.
- Bir kez uygulanır, `FaceDirector` tick'i durur (Kural 7 — bitmiş bir sistem her kare dönmesin).

**Test:** oyunu kaybet → çizgi üstündeki meyveler sersem, altındakiler ezilmiş. Restart → hepsi `idle`.

### Faz 3 performans testi
- Profiler: 50+ meyve ekranda, `FaceDirector.Update` **< 0.3 ms**
- GC allocation sürekli **0 B** (ne coroutine ne LINQ ne string)
- Stats: yüzler eklendikten sonra batch artışı **≤ 1** (atlas doğruysa)
- Havuz testi: 100 birleşme sonrası yanlış yüzle kalan meyve yok (reset çalışıyor)

---

## Faz 4 — Result ekranı

> **✅ BİTTİ.** Yıldızlar 0.70 sn sonra sırayla dolar (`star.wav`, pitch indeksle yükselir),
> rekorda 0.30 sn sonra kuşak (`new_record.wav`). Rekor tespiti için `GameEvents.OnNewRecord`
> eklendi — `OnGameOver`'ın abone sırası garanti olmadığı için panel karşılaştırmayı kendi
> yapamıyordu. `UIPanel`'e `OnTick` eklendi: alt sınıfta `Update()` tanımlamak Unity'de
> tabanın fade'ini sessizce öldürüyor.
>
> İncelemede **9 yerleşim hatası** bulunup düzeltildi: panel objesi kapalıydı (hiç açılmıyordu),
> 4 alan bağlı değildi (`_config` boş olduğu için hiç yıldız çıkmazdı), banner yatay ezilmişti,
> `ScoreCaption` metni "0"du, `ScoreLabel` beyazdı (krem panelde okunmuyordu), `Star2` kaymıştı,
> `RestartButton` `localScale 0.51` ile boyutlandırılmıştı, kuşak banner'la çakışıyordu.
> Metinler İngilizce: `SCORE` · `BEST` · `RESTART` · `MENU`.
>
> Aşağısı orijinal plan, referans için duruyor.

`GameOverPanel` şu an minimal: skor + rekor + restart. Eldeki art tam bir sonuç ekranı istiyor.

| Eleman | Art | Ses |
|---|---|---|
| Banner | `gameover_banner` | — |
| Üzgün maskot | `gameover_mascot_sad` | (sahnede `MascotSad` objesi zaten var) |
| Yıldızlar | `result_star_empty` / `result_star_filled` | `star.wav` → `AudioService.PlayStar(index)` **hazır** |
| Rekor şeridi | `newrecord_ribbon` | `new_record.wav` → `PlayNewRecord()` **hazır** |
| Devam teklifi | `continue_boost_prompt` | Faz 8'e kadar gizli |

**Yapılacaklar**
- Yıldız eşikleri `GameConfig`'e (skora göre 1/2/3 yıldız)
- Yıldızlar sırayla dolar, her biri `PlayStar(index)` — pitch zaten indeksle yükseliyor
- `SaveService.HandleGameOver` yeni rekoru **zaten** algılıyor; oradan bir olay yayınla, panel `newrecord_ribbon` + `PlayNewRecord()` göstersin
- `game_over.wav` ile `new_record.wav` üst üste binmesin — rekor sesini yıldız animasyonundan sonra çal
- `GameOverPanel.PlaysOpenSfx` `false` kalacak (`game_over.wav` o işi görüyor) — dokunma

---

## Faz 5 — Tutorial

`tutorial_hand_tap`, `hand_tap_pressed`, `hand_drag`, `arrow_curved`, `spotlight_mask`, `ripple_01..04`, `callout_bubble`.

İlk oyunda 2–3 adım: (1) parmağını kaydır, (2) bırak, (3) aynı meyveleri birleştir. `SaveService`'e `tutorialDone` bayrağı. `spotlight_mask` ile ekranın gerisini karart.

---

## Faz 6 — HUD cilası

Kozmetik, sıra esnek.
- `hud_combo_banner` — şu an düz TMP metni
- `panel_fruit_chain` + `hud_fruit_chain_slot` + `hud_chain_arrow` — evrim zinciri göstergesi (bu tür oyunlarda standart)
- `hud_score_digits` — bitmap skor fontu
- `hud_dropper_branch` / `hud_dropper_guide` — dropper görseli

---

## Faz 7 — Müzik + ekran akışı

> **Menü kısmı (7a) ÖNE ALINDI ve bitti.** `GameManager.Start()` artık `Playing` yerine
> `Menu`'ye giriyor; `Play()` ve `GoToMenu()` eklendi; `MenuPanel` kuruldu; pause ve sonuç
> ekranındaki MENU butonları aktifleşti.
>
> Bu sırada iki yan etki çıktı ve düzeltildi: (1) `Restart()` sahneyi yeniden yüklüyordu,
> `Start()` Menu'ye girdiği için restart oyuncuyu menüye atıyordu → **yumuşak sıfırlamaya**
> çevrildi, `OnRunStarted` `SetState`'in içinden çıkarıldı. (2) `GameOverPanel` hiç `Hide()`
> çağırmıyordu; MENU'ye basınca panel açık kalıyor, arkada HUD skoru 0'a doğru geri sayıyordu.
>
> **Kalan (7b): müzik + splash.**

Senin isteğin: sona.

**Müzik** — hiç üretilmedi. `AudioService` SFX için kurulu; müzik **ayrı** bir `AudioSource` ister ve import ayarları farklı: **Streaming + Vorbis**. SFX ayarlarını (Decompress On Load + PCM) müziğe verirsen 60 sn'lik klip RAM'de ~10 MB olur. Faz 1'deki müzik toggle'ı burada gerçekten çalışmaya başlar.

**Splash** — `splash_loading_track/fill`, `bg_splash_decor_only`, `landing_logo` (`splash_mascot` kullanılmıyor, bkz. Faz 7c)
**Landing/menü** — `landing_fruit_pile`, `landing_cloud_01..03` (`landing_logo` menüde **kullanılmıyor**, sadece splash'ta)

`GameState.Menu` enum'da **var** ama `GameManager` onu hiç kullanmıyor; `Start()` doğrudan `Playing`'e giriyor. Burada state machine'e gerçekten bağlanacak.

> **K4 kararı: tek sahne + panel.** Ek sahne yok; splash/landing/menü `Game.unity` içinde `UIPanel` olarak yaşayacak. `GameState.Menu` state machine'e bağlanır, `Playing`'e geçiş paneli kapatır. Sahne geçişi yükleme duraklaması ve `GameEvents` static abonelik riski getiriyordu — ikisinden de kaçınıyoruz. `GameOverPanel`'in "Ana menü" butonu da Faz 7'de burada anlam kazanıyor.

---

## Faz 8 — Boost sistemi

**Art'ın yarısından fazlası, kodun sıfırı.** 15 boost × 5 durum + efekt setleri + bar/slot/cursor ≈ 250 dosya.

Boostlar: `bomb`, `hammer`, `freeze`, `magnet`, `rainbow`, `shuffle`, `swap`, `undo`, `upgrade`, `shrink`, `remove`, `line_clear`, `slow_time`, `double_score`, `extra_life`.

**Gerekenler:** `BoostDefinition` ScriptableObject · envanter + save · hedefleme modu (`target_crosshair`, `target_dim_overlay`, `target_area_row/square`) · cooldown · her boost'un oyun etkisi · HUD tepsisi (`hud_boost_tray`).

**En zor:** `undo` — oyun durumunun snapshot'ı gerekiyor (tüm meyvelerin pozisyon/hız/tier'ı + skor). Havuz mimarisi bunu kolaylaştırıyor ama yine de ayrı tasarım işi.

**Yaklaşım:** 15'ini birden yapma. `hammer` (tek meyve sil) + `bomb` (alan sil) ile **dikey dilim** tamamla — definition, envanter, hedefleme, cooldown, efekt, ses, HUD. Mimari oturunca gerisi çoğunlukla tekrar.

`_ShopExcluded/` klasörü bilinçli dışarıda: reklam/IAP ayrı bir karar.

---

## Alınan kararlar

| # | Konu | Karar |
|---|---|---|
| **K1** | Danger mı birleşme mi öncelikli? | **Kutlama önce.** 2 sn oynar, zincirde tazelenir, bitince yeni birleşme yoksa danger devralır |
| **K2** | "elma+elma ve üstü" eşiği | **Üretilen tier ≥ 6** (şeftali) |
| **K3** | Danger'da tüm meyveler mi, yakın olanlar mı? | **Tüm meyveler** |
| **K4** | Menü: ayrı sahne mi, panel mi? | **Tek sahne + panel** |
| **K5** | Danger vinyeti yapılacak mı? | **Hayır.** İlk deneme reddedildi (bkz. Faz 6 notu), mevcut danger line yeterli görüldü — ek bir görsel uyarıya gerek yok |
| **K6** | Bitmap skor fontu yapılacak mı? | **Hayır.** Mevcut TMP font yeterli görüldü, `hud_score_digits` kullanılmayacak |

Hepsi `GameConfig`'ten ayarlanabilir, sonra da değiştirebilirsin.

---

## Ölçülmüş gerçekler (tahmin değil)

Bu plan boyunca dayandığımız, projede doğrulanmış veriler:

- Yüz dosyaları: 48 PNG, **0'ı tek sprite** — hepsi otomatik dilimlenmişti (Faz 0.2'de düzeltildi)
- Efekt dosyaları: 35 PNG → **262 sprite**, aynı dilimleme sorunu (Faz 0.4'te düzeltildi)
- Atlas: ham tuval alanı 7.06M px² ama paketleyici saydam kenarları kırptığı için **2048×2048 tek sayfaya sığdı**. Üst sınır 4096, büyüme payı için
- Gövdeler `Multiple` modda **470×470'e kırpılı**, `Single`'a çevirmek meyveleri ~%9 büyütür → dokunulmayacak
- `GameOverDetector` doluluk oranını **zaten** `gameOverCheckInterval` (0.1 sn) periyoduyla hesaplıyor → danger yüz tepkisi bedava
- `MergeHandler._config` tanımlı ama **kullanılmıyor** → `love` tetiklemesi için hazır yer
- `GameEvents.OnFruitDropped` meyve **instance'ı taşımıyor** → düşen meyve `DropTime` + hız taramasıyla bulunacak, olay imzası değişmeyecek
- `FruitPool.Active` → `IReadOnlyList<Fruit>`, index'le allocation'sız gezilir
- `Fruit` açıkta tutuyor: `IsDropped`, `DropTime`, `Body`, `TopY`, `Radius`, `Definition`
- Sahnede `AudioListener` **Main Camera**'da, tek kopya
- URP kullanılıyor (`UniversalAdditionalCameraData`) → efekt materyalleri URP shader'ı olmalı
- Sahnede **hiç `Light2D` yok**, renk uzayı **Linear** → `Sprite-Lit` materyaller doku rengiyle çiziliyor, tint uygulanmıyor
- `bg_game` duvar rengi **#FEEFB4** (9768 piksel ortalaması), raf y=1475/1844 yani **%80**'de başlıyor
- `tree-branch` sprite'ı ppu 512'de tam **2.00×2.00** dünya birimi; yuva merkezden **+0.119**, sap ucu **−0.352**
- Efekt sprite'larının `pixelsPerUnit`'i **40/50/100 arasında değişiyor** → efekt boyutu koddan normalize ediliyor
- `dropCooldown` asset'te **0.6** (C# varsayılanı 0.45 değil)

---

# FAZ OLMAYAN BORÇLAR

Bir faza ait olmayan, biriken küçük işler. Hiçbiri acil değil.

| Borç | Sorun | Çözüm |
|---|---|---|
| **Müzik toggle'ı** | Aynı durum: ayar kaydediliyor, çalacak müzik yok | Faz 7b ile birlikte çözülür |
| **Menü bulutları** | `landing_cloud_01..03` eski art. Yeni logo ve meyve yığını yanında stil olarak sırıtıyor | Yenile ya da kaldır |
| **`gameover_mascot_sad`** | Faz 4'te opsiyoneldi, eklenmedi | Sonuç ekranının sol altına eklenebilir |
| **`splash_mascot`** | Splash'ın ikinci sürümünde tasarımdan çıktı, artık hiçbir yerde kullanılmıyor | Bırak ya da sil |
| **Menüde logo yok** | `landing_logo` sadece splash'ta; `MenuPanel`'in 7 çocuğu `Background`, `Cloud1..4`, `FruitPile`, `PlayButton` — logo hiç eklenmemiş | Menüye de eklenecekse splash'taki ölçüyü (880 × 447.72) kullan |
| **`MenuPanel` layer 0** | `GameOverPanel`/`PausePanel`/`SplashPanel` UI layer'ında (5), `MenuPanel` ve tüm çocukları Default (0)'da | Overlay canvas'ta çizimi etkilemiyor, ama tutarsız — 5'e alınabilir |
| **`hud_dropper_guide`** | 15 parçaya dilimlenmiş, düzeltilmedi. Kullanılan `hud_dropper_guide_dash_tile`, o sağlam | Kullanılacaksa `Single`'a çevir |
| **`Baloo2-* SDF Outline` materyalleri** | Üçünde de `_OutlineWidth 0.25` + kahverengi `_OutlineColor` ayarlı ama shader'ın **`OUTLINE_ON` keyword'ü kapalı** → kontur HİÇ render edilmiyor. `Baloo2-Bold SDF Outline` HUD'daki `ScoreText` tarafından kullanılıyor, yani skorun kahverengi konturu bugüne kadar hiç görünmedi. (`PermanentMarker-Regular SDF Outline` doğru kurulmuş, combo popup'ın konturu çalışıyor) | `mat.EnableKeyword("OUTLINE_ON")` — tek satır, ama HUD'ın görünümünü değiştirir, karar gerekiyor |
| **`gameover_banner`** | Sonuç ekranının başlığı artık TMP metni ("OVERFLOWING", harf harf meyve renkleri + beyaz kontur), iki satırlık kırmızı "GAME OVER" görseli kullanılmıyor | Bırak ya da sil |
| **Eski coin artları** | `hud_coin_reward_10/20/30` ve `icon_coin_gold` artık hiçbir yerde kullanılmıyor — hepsi `particle_coin` ile değişti. Parlak/3B stilleri zaten oyunun konturlu çizgi stiline uymuyordu | Bırak ya da sil |
| **`panel_coin_badge` pişmiş coin** | Rozetin sol ucundaki coin sanata gömülü ve rozet 800×320'den 320×103'e çekildiği için yatayda 1.24× eziliyor. Üstüne oturan `particle_coin` aynı elipsle (97×80) kapatıldı | Kalıcı çözüm: rozet PNG'sinden coin'i silmek |
| **`combo.wav`** | Projede duruyor, artık çalmıyor — zincir sesi istenmedi, her halka kendi `merge.wav`'ını çalıyor | Bırak ya da sil |
| **`Effects/Merge` 16 dosya** | Flipbook yaklaşımı reddedildiği için hiçbiri kullanılmıyor | Faz 8'de boost efektleri için değerlendirilebilir |
| **Dal / HUD teması** | Dalı en uca sürükleyince next meyve köşedeki skor panelinin altından geçiyor | Bilinçli kabul edildi. Tam kurtulmak için `dropY` 2.9'a inmeli, oynama alanı yarıya düşerdi |
| **Test yok** | Hiç otomatik test yazılmadı | Merge kuyruğu, bag randomizer, skor/combo hesabı, kayıt migrasyonu birim testine uygun |

---

## Tekrar eden ders: otomatik dilimleme

Bu projede **dört ayrı yerde** aynı tuzağa düşüldü — Unity'nin otomatik dilimleyicisi kopuk şekilleri ayrı sprite'lara bölmüş:

| Klasör | Bulunan | Düzeltildi mi |
|---|---|---|
| `Fruits/Faces` | 48 dosya → 0'ı tek parça (`face_happy_xl` 5 parça) | ✅ |
| `Effects/Merge` + `Particles` | 35 dosya → 262 sprite (`merge_burst_06` **52 parça**) | ✅ |
| `UI/Icons` | 15 dosya bölünmüş (`icon_vibrate_on_white` 5 parça) | ✅ |
| `Screens/Menu` | yeni logo/yığın, eski `Multiple` rect'i kırpılmış dokuda dışarı taşıyordu | ✅ |

**Yeni bir art klasörünü ilk kez kullanmadan önce alt-sprite sayısını kontrol et.** Dosya sayısı = sprite sayısı olmalı; değilse `Single`'a çevir. İstisna: `hud_score_digits` (10 rakam) gerçekten çok parçalı olmalı.

## Tekrar eden ders: atlas Tight Packing + UI Image

`FruitAtlas` ve `UIAtlas` `enableTightPacking = 1` ile paketleniyordu. Sahnedeki `SpriteRenderer`'lar
tight mesh çizdiği için orada sorun görünmüyor, ama **UI `Image` her zaman sprite'ın tam bounding
box'ını çizer** — tight paketlemede o kutunun içine komşu sprite'ların pikselleri girdiği için
UI'daki her meyve ikonunun köşesinde alakasız renk parçaları belirdi (evrim zincirinde fark edildi).
`enableRotation` da UI için risklidir (`Image` döndürülmüş atlas UV'sini doğru çizmez).

**Kural: UI'da kullanılan sprite'ları içeren her atlasta Tight Packing ve Rotation kapalı olacak.**
İkisi de `.spriteatlasv2.meta` içindeki `packingSettings` altında; `SpriteAtlasImporter.packingSettings`
ile koddan değiştirilir (`SpriteAtlasExtensions.SetPackingSettings` v2'de kalıcı olmuyor).
