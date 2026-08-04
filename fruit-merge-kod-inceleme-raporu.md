# Fruit Merge — Kod & Performans İnceleme Raporu

**İnceleme kapsamı:** `Assets/FruitMerge/Scripts` altındaki 56 script'in tamamı, `Assets/FruitMerge/Editor` (4 dosya), `Assets/FruitMerge/Scenes/Game.unity`, `Prefabs/Fruit.prefab`, `Art/Fruits/FruitAtlas.spriteatlasv2`, `Art/UI/UIAtlas.spriteatlasv2`, `ProjectSettings/{Physics2DSettings, TimeManager, QualitySettings, EditorSettings}.asset`, `Assets/Settings/{UniversalRP, Renderer2D}.asset`, ses import ayarları.

**Tespit edilen ortam:** Unity **6000.0.80f1**, **URP + Renderer2D**, `Fixed Timestep 0.02` (50 Hz fizik), `Physics2D` velocity/position iterations 8/3, `Reuse Collision Callbacks` **açık**, `Auto Sync Transforms` **kapalı**, `Layer Collision Matrix` Fruit↔Fruit / Fruit↔Wall dışında temizlenmiş. Fruit prefab: `Interpolate = Interpolate`, `Sleeping Mode = Start Awake`, prefab'da `Collision Detection = Continuous` (kod zaten `Initialize`/`ResetState`'te `Discrete`'e çekiyor).

> **Not:** Bu bir tek-parti incelemedir; bütün script'ler aynı turda okundu. Bulgu numaraları F-01'den başlıyor, sonraki turlarda kaldığı yerden devam edecek.

---

## 0. Uygulama durumu

**23 bulgunun 22'si uygulandı. F-10 uygulandı, Play Mode'u kırdı ve GERİ ALINDI.**
Uygulama sırasında bir bulgu daha çıktı
([F-24](#f-24-atlasa-paketlenen-kaynak-dokular-sıkıştırılmış--çifte-kayıp)) — o, kapsamı
gereği (~100 doku asset'i) senin kararına bırakıldı.

> ### ⚠️ F-10 regresyonu — sebep ve alınan ders
>
> **Ne oldu:** F-10 için `ConfettiDirector`, `CoinFlyDirector` ve `WormBoostDirector`'ün
> ısıtmasını `PrewarmQueue`'ya taşıdım. Sonuç: `WormBoostDirector.PrewarmStep` her karede
> `IndexOutOfRangeException` attı → `PrewarmQueue.Done` hiç `Total`'a ulaşmadı →
> `SplashPanel`'in yükleme çubuğu hiç dolmadı → **oyun açılış ekranında kilitlendi.**
> Konsolda görünmüyordu çünkü exception `Editor.log`'a düşüyordu.
>
> **Neyi kaçırdım:** Bu proje Play Mode'da **Reload Domain VE Reload Scene kapalı**
> çalışıyor (`Editor.log`: *"Entering Playmode with Reload Domain disabled"*). Bu
> konfigürasyonda serialize EDİLMEYEN instance alanları (`_prewarmDone`, `_worms`,
> `_cursors`) oturumlar arasında yaşıyor, ama runtime'da yaratılan GameObject'ler yok
> ediliyor — yani sayaç ile gerçek durum ayrışıyor. Kod tabanı bu tuzağı biliyor ve
> `GameEvents`, `BoostGate`, `PointerInput`'ta `[RuntimeInitializeOnLoadMethod]` +
> `ResetStatics` deseniyle koruyor. **`PrewarmQueue` bu korumaya sahip olmayan tek statik**
> ve ben tam oraya yeni state ekledim.
>
> **Neden geri aldım, düzeltmedim:** F-10 set içindeki en düşük değerli bulgu — kazancı
> yalnızca açılıştaki tek karelik takılma (oynanış performansına etkisi sıfır). Play Mode'u
> riske atmaya değmez. Doğru yapmak `PrewarmQueue`'ya `ResetStatics` eklemek + her kaynağın
> ısıtma durumunu oturum başında sıfırlamak demek; bu, ayrı ve dikkatli bir iş.
>
> **Korunanlar:** Aynı dosyalarda yaptığım F-01 (`RestoreScale`), F-12 (`_cursorRefUnit`) ve
> F-18 (null kontrolleri) değişiklikleri yerinde duruyor — onlar bu mekanizmadan bağımsız.
>
> **Kalıcı iz:** `WormBoostDirector.BuildCursors`'ın üstüne, `ConfettiDirector.BuildPool` ve
> `CoinFlyDirector.BuildPool`'un üstüne ve `GameConfig.splashPrewarmPerFrame`'in tooltip'ine
> "bu bilerek Awake/Start'ta, PrewarmQueue'da değil — sebebi şu" notu yazıldı. Aynı hatanın
> tekrar yapılmaması için.
>
> ### ⚠️ F-02 regresyonu — iç içe Canvas'ın iki gizli şartı
>
> **Ne oldu:** F-10 geri alındıktan sonra oyun açıldı ve menü geldi, ama **PLAY butonu
> tıklanamıyordu** (aynı şekilde bütün panel butonları: pause, restart, menu, mağaza).
>
> **Sebep — iç içe Canvas'ın iki şartı var, ikisini de atlamıştım:**
>
> 1. **Kendi `GraphicRaycaster`'ı olmak zorunda.** Bir `Graphic` kendini EN YAKIN Canvas'a
>    kaydediyor (`GraphicRegistry.RegisterGraphicForCanvas`), `GraphicRaycaster` ise yalnızca
>    KENDİ canvas'ının graphic'lerini sınıyor. Panele iç içe Canvas eklemek, panelin
>    butonlarını `PanelCanvas`'ın raycaster'ının görüş alanından çıkardı. Sahnedeki
>    `MainCanvas` / `HUDCanvas` / `PanelCanvas` üçlüsünün her birinde ayrı raycaster
>    olmasının sebebi tam olarak buydu — deseni görüp anlamı çıkarmam gerekirdi.
> 2. **`overrideSorting` + `sortingOrder` devredilmek zorunda.** Kapalı bırakılan alt
>    canvas `sortingOrder`'ı **0** olarak bildiriyor — hem çizimde hem raycast'te
>    (`GraphicRaycaster.sortOrderPriority` doğrudan `canvas.sortingOrder`'dan geliyor).
>    Yani `PanelCanvas`'ın 2'si kaybolup `HUDCanvas`'ın 0'ıyla eşitleniyor ve paneller
>    HUD'un arkasına düşebiliyordu. Bu ikincisini oyun daha ilk hatada patladığı için
>    kullanıcı hiç görmedi, ama düzeltmeye dahil edildi.
>
> **Düzeltme:** `SceneFixups.EnsureSubCanvas` artık üç şeyi ayrı ayrı garanti ediyor —
> Canvas, sıralama devri (yalnızca etkileşimli panellerde), `GraphicRaycaster` (yalnızca
> etkileşimli panellerde). Skor yazısının alt canvas'ı salt gösterim olduğu için ikisini
> de almıyor: `raycastTarget`'ı kapalı ve sırası hiyerarşiden gelmeli.
>
> **Alınan ders:** "görsel katmanlama aynı kalıyor" diye yazdığım yerde, iç içe Canvas'ın
> raycast ve sıralama semantiğini değiştirdiğini doğrulamadım. Sahnede zaten üç ayrı
> raycaster duruyordu — o desen sorunun cevabıydı.

Aşağıdaki bölümler (1-8) incelemenin kendisi olarak duruyor — yani "sorun neydi, neden
sorundu, nasıl çözüldü" kaydı. Bu bölüm ne yapıldığını ve senin neyi doğrulaman gerektiğini
özetliyor.

**Derleme doğrulandı:** `Assembly-CSharp` ve `Assembly-CSharp-Editor` → **0 hata**
(`dotnet build`, Unity 6000.0.80f1 referans assembly'leriyle).

### Değişen dosyalar

| Dosya | Bulgular |
|---|---|
| `Scripts/Gameplay/Fruit.cs` | F-01 (`RestoreScale`), F-05 (`TickVisual`/`TickPhysics`), F-06, F-07 |
| `Scripts/Gameplay/FruitTicker.cs` | **YENİ** — F-05 |
| `Scripts/Gameplay/FruitFace.cs` | F-08 |
| `Scripts/Gameplay/Worm.cs` | F-22 |
| `Scripts/Gameplay/MergeHandler.cs` | F-17 |
| `Scripts/Gameplay/DropController.cs` | F-18 |
| `Scripts/Gameplay/DropIndicatorController.cs` | F-14 |
| `Scripts/Services/WormBoostDirector.cs` | F-01, F-10, F-12, F-18 |
| `Scripts/Services/ConfettiDirector.cs` | F-10 |
| `Scripts/Services/CoinFlyDirector.cs` | F-10 |
| `Scripts/Services/GameOverDetector.cs` | F-09, F-13 |
| `Scripts/Services/CoinRewardDirector.cs` | F-16 |
| `Scripts/Services/ScoreSystem.cs` | F-18 |
| `Scripts/Services/AudioService.cs` | F-19 |
| `Scripts/Services/HapticService.cs` | F-23 |
| `Scripts/UI/UIPanel.cs` | F-02 |
| `Scripts/UI/GameOverPanel.cs` | F-21 |
| `Scripts/UI/HUDView.cs` | F-18 |
| `Scripts/Data/GameConfig.cs` | F-13 (4 alan silindi), F-23 (varsayılan) |
| `Data/GameConfig.asset` | F-13 (4 ölü satır silindi), F-23 (`hapticEditorLog: 0`), F-10 (`splashPrewarmPerFrame: 2 → 6`) |
| `Editor/SceneFixups.cs` | F-02, F-03, F-04, F-05, F-15 (5 yeni düzeltme) |
| `ProjectSettings/Physics2DSettings.asset` | F-11 |

> **`GameConfig.asset` neden de değişti:** serialize edilmiş değer kod varsayılanını EZİYOR.
> `hapticEditorLog`'u kodda `false` yapmak tek başına işe yaramazdı — asset'te `1` yazıyordu.

### Bulgu bazında durum

| ID | Durum | Ne yapıldı |
|----|-------|-----------|
| F-01 | ✅ Kod | `Fruit.RestoreScale()` eklendi, `WormBoostDirector.Abort` çağırıyor. |
| F-02 | ⚠️ Kod + sahne, **2. denemede düzeltildi** | `UIPanel` iç içe `Canvas`'ı kapanınca `enabled = false` yapıyor; Canvas'ları `SceneFixups.FixPanelSubCanvases` ekliyor (5 panel). **İlk denemede panel butonları tıklanamaz oldu** — iç içe canvas'a `GraphicRaycaster` ve sıralama devri eklendi, bkz. aşağıdaki not. |
| F-03 | ✅ Sahne | `SceneFixups.FixScoreSubCanvas` — `ScoreText`'e kendi Canvas'ı. Yeniden ebeveynleme yok, anchor'lar ve referanslar değişmiyor. |
| F-04 | ✅ Asset | `SceneFixups.FixAtlasCompression` — iki atlasa Android/iOS **ASTC 4×4** override'ı, `SpriteAtlasImporter` API'siyle (elle YAML/enum tahmini yok). |
| F-05 | ✅ Kod + sahne | `Fruit.Update`/`FixedUpdate` → `TickVisual`/`TickPhysics`; yeni `FruitTicker` (`[DefaultExecutionOrder(0)]`, eski sırayı koruyor) sahneye `SceneFixups.FixFruitTicker` ile ekleniyor. |
| F-06 | ✅ Kod | `TickPhysics` başında `!_rb.simulated` ve `!_rb.IsAwake()` çıkışları. |
| F-07 | ✅ Kod | `_physicsStep` / `_rearmStep` sayacı; `TryRearmContinuous` fizik adımı başına bir kez. |
| F-08 | ✅ Kod | `LookSnapSqr` eşiği — hedefteyken transform'a hiç dokunulmuyor. |
| F-09 | ✅ Kod | `ComputeFillRatio` artık önbellekli `FloorY`'yi kullanıyor. |
| F-10 | ❌ **GERİ ALINDI** | Uygulandı, Play Mode'u kırdı, geri alındı. Sebep aşağıda — [F-10'un kendi bölümünde](#f-10-açılışta-250-gameobject-awakestartta-yaratılıyor-prewarmqueue-atlanmış) de not düşüldü. `_cursorRefUnit` (F-12) ve null kontrolleri (F-18) korundu. |
| F-11 | ⚠️ Ayar | `useMultithreading: 1` **+** `useConsistencySorting: 1` (ikisi birlikte — determinizmi koruyan şey bu). **Cihazda his kontrolü gerekiyor**, aşağıya bak. |
| F-12 | ✅ Kod | `_cursorRefUnit` bir kez hesaplanıyor; `PlaceCursors`'a `_pool` null kontrolü. |
| F-13 | ✅ Kod | `countForGameOver` **canlandırıldı** (`HasViolation`'da okunuyor, varsayılan `true` olduğu için davranış değişmiyor). `continuousEnterFrames`, `maxConcurrentEffects`, `effectPrewarmCount`, `newRecordDelay` **silindi**. |
| F-14 | ✅ Kod | `_floorY` önbelleği, koşullu `enabled` yazımı, `_config`/`_floor` null korumaları. |
| F-15 | ✅ Sahne | `SceneFixups.FixRaycastTargets` — buton içi graphic'ler (genel kural) + 6 salt-gösterim etiketi; toplam 10 kapandı. **`HudPanel`'e dokunulmadı** (davranışsal). |
| F-16 | ✅ Kod | `FruitCoinTotal`'a `if (!fruit.IsDropped) continue;`. |
| F-17 | ✅ Kod | `_queuedPairs.Clear()` koşulsuz; gereksiz `if (_queue.Count > 0)` de kalktı. |
| F-18 | ✅ Kod | `HUDView` (2 yer), `DropController` (2 yer), `ScoreSystem` (kopya koruması + abonelik koruması + 2 null kontrolü), `WormBoostDirector` (`_pool`, `_config` ×2, `_pulse`). |
| F-19 | ✅ Kod | `ClipReferenceComparer` — .NET'in `ReferenceEqualityComparer`'ı Unity'nin .NET Standard 2.1 profilinde olmadığı için üç satırla kendimiz yazdık. |
| F-20 | ➖ Gerekmiyor | İncelendi: `fontSize` → `SetText` → `ForceMeshUpdate` sırası hâlihazırda **tek** mesh kurulumu yapıyor. Ölçüm yerine tablo koymak, kelime listesi değişince bayatlayan bir önbellek demek olurdu — mevcut çözümün bilinçli olarak kaçındığı hata. Yapısal düzeltme yok; Profiler'da bu sivrilmeyi görürsen sebebi budur. |
| F-21 | ✅ Kod | `GameOverPanel.OnTick` başına `if (!IsOpen && !_revealing) return;`. |
| F-22 | ✅ Kod | Halka ölçeği `Configure`'da bir kez; `ApplySegment` artık yazmıyor. |
| F-23 | ✅ Kod | `hapticEditorLog` varsayılanı `false` (kodda **ve** asset'te); deprem/kemirme trenleri günlükten muaf. |
| F-24 | ⚠️ Uygulanmadı | İnceleme sonrası, F-04'ün atlas reimport'u sırasında çıktı: kaynak PNG'ler sıkıştırılmış import ediliyor, atlas çifte sıkıştırmadan besleniyor. ~100 doku asset'ini yeniden import etmek gerektiği için **kararı sana bıraktım** — [detay](#f-24-atlasa-paketlenen-kaynak-dokular-sıkıştırılmış--çifte-kayıp). |
| F-25 | ✅ Kod | İnceleme sonrası, senin testinde çıktı: **pause boost'u iptal ediyordu ve kullanım boşa gidiyordu.** Artık pause donduruyor, Continue kaldığı yerden devam ettiriyor; iptal yalnızca menü/oyun sonunda. `AudioService`'e gürültü duraklat/sürdür, `HapticService`'e `ResumeQuake` eklendi — [detay](#f-25-pause-boostu-iptal-ediyor--kullanım-boşa-gidiyor). |

### Senin yapman gerekenler

**1. Play Mode'dan ÇIK (Cmd+P).** `SceneFixups` (`SessionKey` v10) Edit Mode'a dönüşte
kendiliğinden çalışıp eksik `GraphicRaycaster`'ları ve sıralama devrini uygulayacak, sonra
sahneyi kaydedecek. Konsolda şunu göreceksin:

```
SceneFixups: MenuPanel'a GraphicRaycaster eklendi — …          (×5 panel)
SceneFixups: MenuPanel alt canvas'ı sortingOrder 2 …           (×5 panel)
SceneFixups: N düzeltme uygulandı, sahne kaydedildi.
```

Çalışmazsa menüden: `FruitMerge → Sahne Düzeltmelerini Uygula`. **Sonra Play'e bas** —
PLAY butonu çalışmalı.

Bir önceki turda uygulanıp diskten doğrulanmış olanlar (bunlar yerinde):

| Ne | Beklenen | Diskte |
|---|---|---|
| Panel alt canvas'ları | 5 panel | ✅ `MenuPanel`, `PausePanel`, `GameOverPanel`, `BoostShopPanel`, `SplashPanel` |
| Skor alt canvas'ı | 1 | ✅ `HUDCanvas/HudPanel/ScoreText` |
| Yeni canvas ayarları | `overrideSorting: 0`, shaderChannels 25 | ✅ altısında da (25 = TexCoord1\|Normal\|Tangent — TMP'nin ihtiyacı) |
| `FruitTicker` | `FruitPool`'un objesinde | ✅ sahnede 1 kopya |
| Raycast target | 32 → 22 | ✅ 10 kapandı; **kalan 22'nin tamamı** buton görselleri + Dimmer/Background/Box + `HudPanel` (yani kapanmaması gerekenler) |
| Atlas sıkıştırması | Android + iOS ASTC 4×4 | ✅ ikisinde de `textureFormat: 48`, `overridden: 1` (Unity "iPhone"u "iOS" olarak yazdı) |

Bir şey ters giderse menüden tekrar tetiklenebilir: `FruitMerge → Sahne Düzeltmelerini Uygula`
(fikirsiz — zaten uygulanmışsa hiçbir şey yapmaz).

**2. Play Mode'da şunları doğrula** (hepsi görsel/davranışsal, hiçbiri ölçüm gerektirmiyor):

| Ne | Beklenen | Hangi bulgu |
|---|---|---|
| Meyve düşür, üst üste bindir | Pop ve squash animasyonları çalışıyor | F-05 — `FruitTicker` eklenmediyse animasyonlar hiç oynamaz, **ilk kontrol bu** |
| Skor artışı | Sayı yukarı sayıyor, yerinde ve doğru puntoda | F-03 — Canvas eklenmesi yazıyı bozmamalı |
| Menü / pause / sonuç / mağaza panellerini aç-kapat | Hepsi normal açılıp kapanıyor, fade akıcı | F-02 |
| Sonuç ekranı yazıları ve yıldızlar | TMP yazılar bozuk değil (kontur, kalınlık normal) | F-02/F-03 — shader kanalları kopyalandı, bozulmamalı |
| Butonlara bas | Hepsi tepki veriyor | F-15 |
| Kurtçuk boost'u başlat, "Eat" fazında **pause'a bas**, devam et | Meyve **eski boyutunda** kalıyor (eskiden küçülüp öyle kalıyordu) | **F-01** |
| Deprem boost'u | Sarsıntı, toz, moloz normal | F-11 |
| Kurtçuk boost'unu birkaç kez üst üste kullan | Nişangâhlar her seferinde çıkıyor, kurtlar geliyor | F-10 geri alındı — eski davranış |
| Karpuz birleştir / rekor kır | Konfeti tam sayıda çıkıyor | F-10 geri alındı — eski davranış |
| **Açılış ekranı** | **Çubuk doluyor ve menüye geçiyor** | **F-10 regresyonunun testi — kilitlenme buradaydı** |

**3. Açılış ayarına dokunmaya gerek yok.** F-10 geri alındığı için ısıtma yükü eski hâline
(46 birim: meyve 40 + popup 6) döndü ve `splashPrewarmPerFrame` **2**'de bırakıldı — yani
açılış davranışı incelemeden önceki gibi.

**4. F-11'i cihazda hissederek kontrol et.** Bu, rapordaki "ölçmeden dokunma" kalemiydi ve
senin talebin üzerine uygulandı. `useConsistencySorting` de açık olduğu için simülasyon
deterministik kalıyor, yani yığının oturma davranışı değişmemeli. Yine de 55-60 meyvelik
dolu bir tahtada oyna: **yığın "lastikli" hissediyorsa ya da meyveler farklı oturuyorsa**
`ProjectSettings/Physics2DSettings.asset` içinde ikisini de `0`'a geri çevir — bu ayar
oyunun geri kalanından tamamen bağımsız, tek satırlık bir geri alma. Kazancı
`Physics2D.Simulate` marker'ında ölç; 60 gövde iş dağıtımının kârlı olduğu eşiğin
sınırında olduğu için kazanç sıfıra yakın çıkabilir.

**5. Atlas kalitesini gözle kontrol et (F-04).** ASTC 4×4 pratikte kayıpsız ama yine de bir
sıkıştırma: **cihazda** (editörde değil) kirazı, yüzlerin `sm` boyutunu ve nişangâh
halkalarını bir kez incele. Memory Profiler'da `Texture2D` toplamının ~4 kat düştüğünü
göreceksin.

### Sahnede ne DEĞİŞTİ

`SceneFixups` sahneyi kendi kaydediyor, ama ne yaptığını bilmek isteyeceksin:

- **5 panele** (`MenuPanel`, `PausePanel`, `GameOverPanel`, `BoostShopPanel`, `SplashPanel`)
  üç bileşen/ayar: `Canvas` + `GraphicRaycaster` + `overrideSorting` açık ve
  `sortingOrder = 2` (üst canvas'tan devralındı). Raycaster ve sıralama devri
  **zorunlu** — ikisi olmadan panel butonları tıklanamıyor (bkz. F-02 regresyon notu).
  `additionalShaderChannels` üst canvas'tan kopyalandı (25 = TexCoord1 | Normal | Tangent) —
  TMP'nin SDF shader'ı bu kanallara ihtiyaç duyuyor, kopyalanmasa yazılar bozuk çizilirdi.
- **`ScoreText`'e** yalnızca `Canvas` eklendi — raycaster ve sıralama devri YOK (salt
  gösterim, `raycastTarget`'ı kapalı, sırası hiyerarşiden gelmeli). Obje yeniden
  ebeveynlenmedi.
- **`FruitPool`'un objesine** `FruitTicker` eklendi.
- **10 elemanda** `Raycast Target` kapatıldı: 4 buton içi `Text (TMP)` + `ScoreText`,
  `HighScoreText`, `ScoreLabel`, `ScoreCaption`, `BestLabel`, `BestCaption`. (Raporun
  ilk halinde 11 yazıyordu; `GameOverPanel/Box/MenuButton/Text` sahnede zaten kapalıydı.)
- **Atlas'lar** yeniden import edildi (Android/iOS ASTC 4×4).

Hepsi fikirsiz (idempotent): tekrar çalıştırmak hiçbir şey yapmıyor.

---

## 1. Özet tablo

| ID | Dosya | Kategori | Önem | Oynanışa risk | Tek cümlelik özet |
|----|-------|----------|------|---------------|-------------------|
| [F-01](#f-01-boost-yarıda-kesilirse-meyve-küçülmüş-halde-kalıyor) | WormBoostDirector.cs : 768-777, 859-888 | Bug | **Kritik** | Yok (düzeltme) | Kurtçuk boost'u yemenin ortasında iptal edilirse meyve hem görsel hem fiziksel olarak kalıcı küçük kalıyor. |
| [F-02](#f-02-kapanan-paneller-tuvalden-çıkmıyor--tam-ekran-şeffaf-overdraw) | UIPanel.cs : 45-83 + Game.unity/PanelCanvas | UI / Render | **Yüksek** | Yok | Kapanan paneller `alpha = 0` ile duruyor ama çizilmeye devam ediyor: oynanış boyunca 4 tam ekran şeffaf katman + ~50 CanvasRenderer. |
| [F-03](#f-03-skor-yazısı-ile-11-slotluk-evrim-zinciri-aynı-alt-canvasta) | Game.unity/HUDCanvas + HUDView.cs : 92-99 | UI | **Yüksek** | Yok | Her skor değişimi, 11 slotlu evrim zincirini ve boost rozetlerini içeren ~40 elemanlı alt canvas'ı yeniden kuruyor. |
| [F-04](#f-04-sprite-atlasları-sıkıştırılmamış-rgba32) | FruitAtlas / UIAtlas .spriteatlasv2.meta | Render / bellek | **Yüksek** | Yok | İki atlas da `textureCompression: 0` (RGBA32) ve platform override'ı yok → ~40 MB gereksiz doku belleği ve bant genişliği. |
| [F-05](#f-05-fruit-kendi-update-ve-fixedupdatesini-taşıyor-kural-7-ihlali) | Fruit.cs : 195-271 | Update maliyeti | Orta | Yok | 60 meyve = kare başına 60 `Update` + 50 Hz'de 60 `FixedUpdate` managed↔native geçişi; projenin kendi "tek Update" kuralının tek istisnası. |
| [F-06](#f-06-fruitfixedupdate-uyuyan-gövdelerde-de-boşa-dönüyor) | Fruit.cs : 244-271 | Fizik / Update | Orta | Yok | Yerleşip uyuyan meyvelerde `FixedUpdate` gövdesi tamamen no-op ama yine de her adım çalışıyor; tek satırlık `IsAwake()` çıkışı var. |
| [F-07](#f-07-oncollisionstay2d-içinde-temas-başına-tekrarlanan-native-okuma) | Fruit.cs : 280-298 | Fizik | Orta | Yok | `TryRearmContinuous` temas parametresine hiç bakmıyor ama temas başına çağrılıyor; fizik adımı başına bir kez yeterli. |
| [F-08](#f-08-fruitfaceticklook-hedef-yokken-de-her-karede-transform-yazıyor) | FruitFace.cs : 251-274 | Update maliyeti | Orta | Yok | Bakış hedefi olmayan meyvelerde bile her karede `localPosition` yazılıyor; 60 meyve × kare başına 60 gereksiz transform kirletme. |
| [F-09](#f-09-computefillratio-önbelleklenmiş-floory-yerine-collider-boundsu-okuyor) | GameOverDetector.cs : 144-146 | Update maliyeti | Orta | Yok | Aynı dosyada zaten önbelleğe alınmış `FloorY` varken `ComputeFillRatio` native `bounds` çağrısını tekrar yapıyor. |
| [F-10](#f-10-açılışta-250-gameobject-awakestartta-yaratılıyor-prewarmqueue-atlanmış) | ConfettiDirector.cs : 124-162 · WormBoostDirector.cs : 185-248 · CoinFlyDirector.cs : 115-156 | Lifecycle / açılış | Orta | **Yüksek (uygulanınca çıktı)** | FruitPool ve ComboPopupDirector `PrewarmQueue`'ya taşınmış ama konfeti (140), nişangâh (44), kurt halkaları (30) ve para (32) hâlâ tek karede yaratılıyor. ❌ **Denendi, Play Mode'u kırdı, geri alındı** — [sebep](#0-uygulama-durumu). |
| [F-11](#f-11-physics2d-job-multithreading-kapalı) | ProjectSettings/Physics2DSettings.asset : 30 | Fizik | Orta | Düşük | 60 gövdelik sahnede `useMultithreading: 0`; açmak çözücüyü çekirdeklere dağıtır, `useConsistencySorting` ile birlikte açılmalı. |
| [F-12](#f-12-placecursors-sabit-değeri-meyve-başına-her-karede-yeniden-hesaplıyor) | WormBoostDirector.cs : 358-397 | Update maliyeti | Orta | Yok | Nişangâh sprite'ının dünya birimi her meyve için her karede yeniden okunuyor; tek bir sprite, bir kez hesaplanabilir. |
| [F-13](#f-13-ölü-ayar-alanları--inspectordan-çevirince-hiçbir-şey-olmuyor) | GameConfig.cs : 69, 189, 192, 275 · FruitDefinition.cs : 51 | Bug / bakım | Orta | Yok | `countForGameOver`, `continuousEnterFrames`, `maxConcurrentEffects`, `effectPrewarmCount`, `newRecordDelay` hiçbir yerde okunmuyor. |
| [F-14](#f-14-dropindicatorcontrollerupdate-her-karede-gereksiz-native-iş-yapıyor) | DropIndicatorController.cs : 40-64 | Update maliyeti | Orta | Yok | Her karede `bounds` okuma + `renderer.enabled` yazımı + null kontrolsüz `_config`/`_floor` erişimi. |
| [F-15](#f-15-gereksiz-raycast-target-işaretli-ui-elemanları) | Game.unity (12 eleman) | UI | Düşük | Yok* | Buton içi `Text (TMP)`'ler ve salt-okunur skor/rekor etiketleri raycast hedefi; `HudPanel` bilinçli, ona dokunulmamalı. |
| [F-16](#f-16-daldaki-bırakılmamış-meyve-de-coin-ödülüne-sayılıyor) | CoinRewardDirector.cs : 70-90 | Bug | Düşük | Düşük | `FruitCoinTotal` `IsDropped` kontrolü yapmıyor; dalda asılı duran meyve de ödül veriyor. |
| [F-17](#f-17-mergehandler-kuyruk-guardına-takılırsa-_queuedpairs-kalıntı-bırakıyor) | MergeHandler.cs : 67-85 | Bug | Düşük | Düşük | 100'lük guard dolarsa kalan anahtarlar temizlenmiyor ve o çift bir süre daha birleşemiyor. |
| [F-18](#f-18-null-referans-riski-taşıyan-noktalar) | HUDView.cs : 80 · DropController.cs : 60, 291 · ScoreSystem.cs : 14, 34 | Bug | Düşük | Yok | Aynı kod tabanında tutarlı olan null kontrolleri birkaç yerde eksik; sahnede bir alan boş kalırsa sessiz çökme. |
| [F-19](#f-19-audioservice-audioclip-anahtarlı-dictionary-kullanıyor) | AudioService.cs : 116, 363 | GC / mikro | Düşük | Yok | `Dictionary<AudioClip, float>` her aramada `UnityEngine.Object.Equals` üzerinden geçiyor; `ReferenceEqualityComparer` yeter. |
| [F-20](#f-20-her-combo-popupında-forcemeshupdate) | ComboPopupItem.cs : 79-102 | UI | Düşük | Yok | Zincirleme birleşmede aynı karede birkaç kez TMP mesh'i zorla yeniden kuruluyor. |
| [F-21](#f-21-kapalı-panellerde-de-ontick--tickpunch-dönüyor) | UIPanel.cs : 67-83 · GameOverPanel.cs : 248-275 | Update maliyeti | Düşük | Yok | 5 panelin `Update`'i oyun boyunca çalışıyor; sonuç ekranı kapalıyken de 3 yıldızlık döngü dönüyor. |
| [F-22](#f-22-wormapplysegment-değişmeyen-ölçeği-her-karede-yazıyor) | Worm.cs : 333-357 | Update maliyeti | Düşük | Yok | `localScale` bir seferde belirlenen sabit bir değer ama halka başına her karede yazılıyor. |
| [F-23](#f-23-hapticservice-editör-günlüğü-deprem-boyunca-saniyede-14-string-üretiyor) | HapticService.cs : 554-560 | GC (editör) | Düşük | Yok | Cihazda derlenmiyor ama editörde profil ölçümünü ve konsolu kirletiyor. |
| [F-24](#f-24-atlasa-paketlenen-kaynak-dokular-sıkıştırılmış--çifte-kayıp) | Art/UI/**, Art/Fruits/** (~100 doku) | Render / kalite | Orta | Yok | Atlas'a paketlenen kaynak PNG'ler sıkıştırılmış import ediliyor → atlas çifte sıkıştırmadan besleniyor. **Uygulanmadı — kararı sende.** |
| [F-25](#f-25-pause-boostu-iptal-ediyor--kullanım-boşa-gidiyor) | WormBoostDirector.cs : 261-265 · QuakeBoostDirector.cs : 148-152 | Bug | **Yüksek** | Yok (düzeltme) | Pause boost'u dondurmak yerine İPTAL ediyor; kullanım `Begin`'de harcandığı için oyuncu bir boost'u pause'a bastığı an kaybediyor. |

\* F-15'te yalnızca `HudPanel` davranışsal; gerisi risksiz.

---

## 2. İçindekiler

**Kritik**
- [F-01 Boost yarıda kesilirse meyve küçülmüş halde kalıyor](#f-01-boost-yarıda-kesilirse-meyve-küçülmüş-halde-kalıyor)

**Yüksek**
- [F-02 Kapanan paneller tuvalden çıkmıyor — tam ekran şeffaf overdraw](#f-02-kapanan-paneller-tuvalden-çıkmıyor--tam-ekran-şeffaf-overdraw)
- [F-03 Skor yazısı ile 11 slotluk evrim zinciri aynı alt canvas'ta](#f-03-skor-yazısı-ile-11-slotluk-evrim-zinciri-aynı-alt-canvasta)
- [F-04 Sprite atlasları sıkıştırılmamış (RGBA32)](#f-04-sprite-atlasları-sıkıştırılmamış-rgba32)

**Orta**
- [F-05 `Fruit` kendi `Update` ve `FixedUpdate`'sini taşıyor (kural 7 ihlali)](#f-05-fruit-kendi-update-ve-fixedupdatesini-taşıyor-kural-7-ihlali)
- [F-06 `Fruit.FixedUpdate` uyuyan gövdelerde de boşa dönüyor](#f-06-fruitfixedupdate-uyuyan-gövdelerde-de-boşa-dönüyor)
- [F-07 `OnCollisionStay2D` içinde temas başına tekrarlanan native okuma](#f-07-oncollisionstay2d-içinde-temas-başına-tekrarlanan-native-okuma)
- [F-08 `FruitFace.TickLook` hedef yokken de her karede transform yazıyor](#f-08-fruitfaceticklook-hedef-yokken-de-her-karede-transform-yazıyor)
- [F-09 `ComputeFillRatio` önbelleklenmiş `FloorY` yerine collider bounds'u okuyor](#f-09-computefillratio-önbelleklenmiş-floory-yerine-collider-boundsu-okuyor)
- [F-10 Açılışta ~250 GameObject Awake/Start'ta yaratılıyor (PrewarmQueue atlanmış)](#f-10-açılışta-250-gameobject-awakestartta-yaratılıyor-prewarmqueue-atlanmış)
- [F-11 Physics2D job multithreading kapalı](#f-11-physics2d-job-multithreading-kapalı)
- [F-12 `PlaceCursors` sabit değeri meyve başına her karede yeniden hesaplıyor](#f-12-placecursors-sabit-değeri-meyve-başına-her-karede-yeniden-hesaplıyor)
- [F-13 Ölü ayar alanları — Inspector'dan çevirince hiçbir şey olmuyor](#f-13-ölü-ayar-alanları--inspectordan-çevirince-hiçbir-şey-olmuyor)
- [F-14 `DropIndicatorController.Update` her karede gereksiz native iş yapıyor](#f-14-dropindicatorcontrollerupdate-her-karede-gereksiz-native-iş-yapıyor)

**Düşük**
- [F-15 Gereksiz Raycast Target işaretli UI elemanları](#f-15-gereksiz-raycast-target-işaretli-ui-elemanları)
- [F-16 Daldaki bırakılmamış meyve de coin ödülüne sayılıyor](#f-16-daldaki-bırakılmamış-meyve-de-coin-ödülüne-sayılıyor)
- [F-17 MergeHandler kuyruk guard'ına takılırsa `_queuedPairs` kalıntı bırakıyor](#f-17-mergehandler-kuyruk-guardına-takılırsa-_queuedpairs-kalıntı-bırakıyor)
- [F-18 Null referans riski taşıyan noktalar](#f-18-null-referans-riski-taşıyan-noktalar)
- [F-19 AudioService `AudioClip` anahtarlı Dictionary kullanıyor](#f-19-audioservice-audioclip-anahtarlı-dictionary-kullanıyor)
- [F-20 Her combo popup'ında `ForceMeshUpdate`](#f-20-her-combo-popupında-forcemeshupdate)
- [F-21 Kapalı panellerde de `OnTick` + `TickPunch` dönüyor](#f-21-kapalı-panellerde-de-ontick--tickpunch-dönüyor)
- [F-22 `Worm.ApplySegment` değişmeyen ölçeği her karede yazıyor](#f-22-wormapplysegment-değişmeyen-ölçeği-her-karede-yazıyor)
- [F-23 HapticService editör günlüğü deprem boyunca saniyede ~14 string üretiyor](#f-23-hapticservice-editör-günlüğü-deprem-boyunca-saniyede-14-string-üretiyor)

**İnceleme sonrası eklenen**
- [F-24 Atlas'a paketlenen kaynak dokular sıkıştırılmış — çifte kayıp](#f-24-atlasa-paketlenen-kaynak-dokular-sıkıştırılmış--çifte-kayıp)
- [F-25 Pause boost'u iptal ediyor — kullanım boşa gidiyor](#f-25-pause-boostu-iptal-ediyor--kullanım-boşa-gidiyor)

**Kapanış**
- [4. Öncelik sıralı aksiyon planı](#4-öncelik-sıralı-aksiyon-planı)
- [5. Hızlı kazanımlar (quick wins)](#5-hızlı-kazanımlar-quick-wins)
- [6. Önerilmeyen optimizasyonlar](#6-önerilmeyen-optimizasyonlar)
- [7. Profiling rehberi](#7-profiling-rehberi)
- [8. Temiz olan kısımlar](#8-temiz-olan-kısımlar)

---

## 3. Detaylı bulgular

### F-01 Boost yarıda kesilirse meyve küçülmüş halde kalıyor

- **Dosya / satır:** `Assets/FruitMerge/Scripts/Services/WormBoostDirector.cs : 768-777` (`ShrinkFruit`) ve `: 859-888` (`Abort`)
- **Kategori:** Bug
- **Önem:** Kritik
- **Sorun:** `TickEat` her karede `ShrinkFruit()` çağırıp hedef meyvenin `transform.localScale`'ini `eatFruitMinScale`'e (0.35) doğru küçültüyor. Ölçek normalde meyve yok olduğunda (`VanishFruit` → `Despawn` → `ResetState`/`Initialize`) sıfırlanıyor. Ama **`Abort()` ölçeği geri almıyor.** `HandleStateChanged` `s != GameState.Playing` olduğu her durumda `Abort()` çağırıyor — yani **yemenin ortasında pause'a basmak** (ya da menüye dönmek, ya da o anda oyunun bitmesi) meyveyi yarı küçülmüş halde tahtada bırakıyor.

  İki katmanlı sonuç doğuruyor:
  1. **Görsel:** yığının içinde tek bir meyve kalıcı olarak küçük duruyor.
  2. **Fizik ve mantık:** `CircleCollider2D`'nin efektif yarıçapı `transform.localScale` ile ölçekleniyor, yani meyve fiziksel olarak da küçülüyor ve yığın onun etrafında çöküyor. Buna karşılık `Fruit.Radius` (`Fruit.cs : 43`) ve `Fruit.TopY` (`: 44`) hâlâ `_targetScale`'i kullanıyor — yani `GameOverDetector.ComputeFillRatio`, `DropController.DropLimitX` ve kurt boost'unun `_targetRadius`'u o meyve için **yanlış** değer üretiyor.

  `Fruit.Update` yalnızca pop/squash sayacı çalışırken `localScale` yazdığı için ölçek kendiliğinden de düzelmiyor; meyve rastgele bir çarpma ile squash tetikleyene kadar (belki hiç) küçük kalıyor.
- **Beklenen etki:** Performans etkisi yok; oynanış hatası. Tekrar üretme: kurtçuk boost'unu başlat, "Eat" fazında (hedef seçiminden ~2 sn sonra) pause'a bas, devam et.
- **Oynanışa risk:** Düzeltmenin riski **Yok** — mevcut hatalı durumu ortadan kaldırıyor.
- **Çözüm:** Ölçeği geri yazma sorumluluğunu `Fruit`'e ver (hedef ölçeği bilen tek yer o) ve `Abort` içinde çağır. Böylece `_targetBaseScale`'i director'ün doğru tutmasına da bağımlı kalmıyoruz.

Önce — `Fruit.cs` (ölçeği geri alacak bir API yok):

```csharp
public void PlaySquash(float intensity)
{
    if (_config == null) return;
    ...
}
```

Sonra — `Fruit.cs`, `PlaySquash`'ın hemen üstüne:

```csharp
/// <summary>
/// Görsel ölçeği hedef ölçeğe geri oturtur. Kurtçuk boost'u yeme sırasında meyveyi
/// küçültüyor; boost yarıda kesilirse (pause / menü / oyun sonu) meyve o ölçekte
/// kalıyordu — collider da transform ölçeğiyle küçüldüğü için yığın onun etrafında
/// çöküyor ve Radius/TopY yanlış değer veriyordu.
/// </summary>
public void RestoreScale()
{
    _popTimer    = -1f;
    _squashTimer = -1f;

    transform.localScale = Vector3.one * _targetScale;
}

public void PlaySquash(float intensity)
{
    if (_config == null) return;
    ...
}
```

Önce — `WormBoostDirector.cs : 859-869`:

```csharp
void Abort()
{
    if (_state == State.Idle && _wormsActive == 0) return;

    // hedef hâlâ tahtadaysa merge kilidini geri al
    if (_target != null && _target.gameObject.activeSelf) _target.IsMerging = false;
```

Sonra:

```csharp
void Abort()
{
    if (_state == State.Idle && _wormsActive == 0) return;

    // hedef hâlâ tahtadaysa merge kilidini VE ShrinkFruit'in küçülttüğü ölçeği geri al
    if (_target != null && _target.gameObject.activeSelf)
    {
        _target.IsMerging = false;
        _target.RestoreScale();
    }
```

---

### F-02 Kapanan paneller tuvalden çıkmıyor — tam ekran şeffaf overdraw

- **Dosya / satır:** `Assets/FruitMerge/Scripts/UI/UIPanel.cs : 45-83` + sahne yapısı `Game.unity` → `MainCanvas/PanelCanvas`
- **Kategori:** UI / Render (overdraw)
- **Önem:** Yüksek
- **Sorun:** `UIPanel.Hide()` yalnızca `CanvasGroup.alpha`'yı 0'a indiriyor, `interactable`/`blocksRaycasts`'i kapatıyor. GameObject **aktif kalıyor** — ve bu bilinçli bir karar (`CoinHudView.cs : 35-37` ve `BoostButton.cs : 65-66` aynı gerekçeyi açıkça yazıyor: kendini kapatan bileşen `OnDisable`'da aboneliğini bırakıp bir daha haber alamaz).

  Fakat `alpha = 0` **çizimi durdurmuyor**: `CanvasRenderer` geometriyi yine kuruyor ve GPU yine tam ekran şeffaf dörtgenleri harmanlıyor. Sahnedeki `PanelCanvas` içeriği (hiyerarşiyi doğruladım):

  | Panel | Tam ekran eleman | Toplam graphic |
  |---|---|---|
  | `PausePanel` | `Dimmer` | 18 (Image + TMP) |
  | `MenuPanel` | `Background` | 8 (4 bulut + FruitPile + PlayButton + Text) |
  | `GameOverPanel` | `Dimmer` | 15 (3 yıldız + 6 TMP + butonlar) |
  | `BoostShopPanel` | `Dimmer` | 7 |

  Yani oynanış sırasında **4 tam ekran şeffaf katman + ~48 küçük graphic** hiç görünmeden çiziliyor. Mobil GPU'da darboğaz neredeyse her zaman fill-rate; 4 tam ekran blend, oyunun kendi arka planı + yığın + parçacıkların üstüne binen ölü maliyet.

  `SplashPanel` bunu **doğru** yapıyor (`SplashPanel.cs : 102`, "Splash bir daha açılmıyor: tuvalden tamamen çıksın, boşuna batch/overdraw olmasın") — ama bu yol diğer panellere uygulanamıyor çünkü onlar tekrar açılmak zorunda.
- **Beklenen etki:** 1080×2400 ekranda 4 tam ekran blend ≈ 10.4 M fragment/kare. Orta segment Android'de bu tek başına ölçülebilir bir GPU süresi (tipik olarak 1–3 ms) demek. CPU tarafında da ~50 CanvasRenderer batch'e giriyor.
- **Oynanışa risk:** Yok — görünmeyen geometri çizilmiyor, görüntü birebir aynı.
- **Çözüm:** Her panel kökünde bir **iç içe `Canvas`** bileşeni olsun (Override Sorting **kapalı** kalsın, hiyerarşi sırası korunur) ve panel kapanma animasyonu bittiğinde `canvas.enabled = false` yapılsın. `Canvas.enabled = false` alt ağacı tamamen tuvalden çıkarıyor ama **GameObject aktif kaldığı için `OnEnable`/`OnDisable` abonelikleri bozulmuyor** — F-02'nin varlık sebebi olan kısıt korunuyor.

  Sahne tarafında yapılacak: `PausePanel`, `MenuPanel`, `GameOverPanel`, `BoostShopPanel` köklerine `Canvas` bileşeni ekle (Override Sorting kapalı). Kod tarafı tek dosya:

Önce — `UIPanel.cs : 1-83`:

```csharp
[RequireComponent(typeof(CanvasGroup))]
public abstract class UIPanel : MonoBehaviour
{
    [SerializeField] protected float _fadeDuration = 0.18f;

    protected CanvasGroup _group;

    float _target;
    bool  _animating;

    public bool IsOpen { get; private set; }
    ...
    protected virtual void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        SetInstant(false);
    }

    public virtual void Show()
    {
        IsOpen = true;
        gameObject.SetActive(true);
        _target = 1f;
        _animating = true;
        ...
    }
    ...
    void SetInstant(bool open)
    {
        IsOpen = open;
        _group.alpha = open ? 1f : 0f;
        _group.interactable = open;
        _group.blocksRaycasts = open;
    }

    void Update()
    {
        OnTick(Time.unscaledDeltaTime);

        if (!_animating) return;

        _group.alpha = Mathf.MoveTowards(_group.alpha, _target,
            Time.unscaledDeltaTime / _fadeDuration);

        if (!Mathf.Approximately(_group.alpha, _target)) return;

        _animating = false;

        if (IsOpen) OnShown(); else OnHidden();
    }
```

Sonra:

```csharp
[RequireComponent(typeof(CanvasGroup))]
public abstract class UIPanel : MonoBehaviour
{
    [SerializeField] protected float _fadeDuration = 0.18f;

    protected CanvasGroup _group;

    /// <summary>
    /// Panel kökündeki İÇ İÇE Canvas (Override Sorting kapalı — hiyerarşi sırası korunur).
    ///
    /// Kapanınca <c>enabled = false</c> yapılıyor, <c>SetActive(false)</c> DEĞİL:
    /// GameObject aktif kaldığı için OnEnable/OnDisable abonelikleri bozulmuyor
    /// (kapanan panel bir daha haber alamaz sorunu), ama alt ağaç tuvalden tamamen
    /// çıkıyor — alpha 0 geometriyi ÇİZMEYİ durdurmuyordu, sadece görünmez yapıyordu.
    /// Oynanış sırasında dört panelin dört tam ekran Dimmer/Background'u boşa
    /// harmanlanıyordu.
    ///
    /// Bileşen yoksa (sahnede eklenmediyse) davranış eskisi gibi kalıyor.
    /// </summary>
    Canvas _canvas;

    float _target;
    bool  _animating;

    public bool IsOpen { get; private set; }
    ...
    protected virtual void Awake()
    {
        _group  = GetComponent<CanvasGroup>();
        _canvas = GetComponent<Canvas>();
        SetInstant(false);
    }

    public virtual void Show()
    {
        IsOpen = true;
        gameObject.SetActive(true);

        // Fade'in İLK karesinden önce tuvale geri gir, yoksa panel bir kare gecikir.
        if (_canvas != null) _canvas.enabled = true;

        _target = 1f;
        _animating = true;
        ...
    }
    ...
    void SetInstant(bool open)
    {
        IsOpen = open;
        _group.alpha = open ? 1f : 0f;
        _group.interactable = open;
        _group.blocksRaycasts = open;

        if (_canvas != null) _canvas.enabled = open;
    }

    void Update()
    {
        OnTick(Time.unscaledDeltaTime);

        if (!_animating) return;

        _group.alpha = Mathf.MoveTowards(_group.alpha, _target,
            Time.unscaledDeltaTime / _fadeDuration);

        if (!Mathf.Approximately(_group.alpha, _target)) return;

        _animating = false;

        if (IsOpen)
        {
            OnShown();
        }
        else
        {
            // Fade BİTTİKTEN sonra tuvalden çık — yarı saydam kareler hâlâ çizilmeli.
            if (_canvas != null) _canvas.enabled = false;

            OnHidden();
        }
    }
```

> **Doğrulama:** Değişiklikten önce ve sonra Frame Debugger'ı oynanış sırasında aç; `PanelCanvas` altındaki draw çağrılarının tamamen kaybolduğunu gör. `SplashPanel`'in `OnHidden`'ındaki `SetActive(false)` olduğu gibi kalabilir, çakışmıyor.

---

### F-03 Skor yazısı ile 11 slotluk evrim zinciri aynı alt canvas'ta

- **Dosya / satır:** `Game.unity` → `MainCanvas/HUDCanvas` hiyerarşisi + `HUDView.cs : 92-99`
- **Kategori:** UI
- **Önem:** Yüksek
- **Sorun:** Sahnedeki `HUDCanvas` alt canvas'ının içeriği:

  ```
  HUDCanvas  (Canvas, order 0, override sorting)
    PauseButton + Icon                          →  2 graphic
    BoostSlot_Quake + CountBadge + Label + ...   →  3 aktif graphic
    HudPanel + HighScoreText + ScoreText         →  3 graphic
    BoostSlot + CountBadge + Label + ...         →  3 aktif graphic
    FruitChainPanel (HorizontalLayoutGroup)      →  1 + 11 slot × (Icon + Face) = 23 graphic
  ```

  Toplam **~34 CanvasRenderer, bir de `HorizontalLayoutGroup`**. `HUDView.Update` (`: 92-99`) skoru `_countSpeed = 400`/sn hızıyla yukarı sayıyor ve saydığı sürece **her karede** `_scoreText.SetText("{0}", ...)` çağırıyor. Sayma, tipik bir birleşmeden sonra 0.1–0.5 sn sürüyor; combo zincirinde neredeyse kesintisiz.

  TMP mesh'i her değiştiğinde canvas'ın o alt ağacı "dirty" işaretleniyor ve **alt canvas'ın tamamı** (34 renderer + layout grubu) yeniden batch'leniyor. Yani üç haneli bir sayının değişmesi, hiç değişmemiş 23 meyve ikonunun geometrisini yeniden birleştirmeye sebep oluyor. Mobilde "gizli maliyet" listesinin başındaki kalem tam olarak bu.
- **Beklenen etki:** `Canvas.SendWillRenderCanvases` + `Canvas.BuildBatch` marker'larında ölçülür; 34 elemanlı bir alt canvas için tipik olarak 0.3–1.0 ms/kare, ve bu skor saydığı sürece süren bir maliyet. Ayrılınca yeniden kurulan eleman sayısı 34 → 1'e düşüyor.
- **Oynanışa risk:** Yok — tamamen sahne yapısı değişikliği, görüntü aynı.
- **Çözüm:** Sık değişen yazıyı **kendi alt canvas'ına** al. Kod değişikliği gerekmiyor.

  Sahnede yapılacak:
  1. `HUDCanvas/HudPanel` altına `ScoreGroup` adında bir GameObject ekle.
  2. `ScoreGroup`'un `RectTransform`'unu tam gerdir (`anchorMin 0,0` · `anchorMax 1,1` · `offsetMin/Max 0`) — böylece `ScoreText`'in mevcut anchor'ları birebir aynı sonucu verir.
  3. `ScoreGroup`'a bir **`Canvas`** bileşeni ekle (Override Sorting **kapalı**). `GraphicRaycaster` **eklemeyin** — gerek yok.
  4. `ScoreText`'i `ScoreGroup`'un altına taşı. `HUDView._scoreText` referansı taşımada korunur, yine de Inspector'dan doğrula.

  `HighScoreText` taşınmasın: yalnızca rekor kırılınca değişiyor (`HUDView.cs : 80`), ayrı canvas maliyetine değmez.

  Aynı mantıkla `CoinHudView`'in `Label`'ı (`OverlayCanvas/CoinHud/Label`) da oyun sonunda birer birer sayıyor — ama `OverlayCanvas`'ta yalnızca 3 graphic var (`CoinAnchor`, `Badge`, `Label`), yani orada ayırmanın kazancı yok. **Dokunma.**

> Ölçmeden uygulamak isterseniz risk sıfır; ama kazancı görmek için Profiler'da `Canvas.BuildBatch` marker'ını değişiklik öncesi/sonrası karşılaştırın.

---

### F-04 Sprite atlasları sıkıştırılmamış (RGBA32)

- **Dosya / satır:** `Assets/FruitMerge/Art/Fruits/FruitAtlas.spriteatlasv2.meta : 5-19` ve `Assets/FruitMerge/Art/UI/UIAtlas.spriteatlasv2.meta` (aynı blok)
- **Kategori:** Render / bellek
- **Önem:** Yüksek
- **Sorun:** İki atlas da şu ayarlarla import ediliyor:

  ```yaml
  textureSettings:
    maxTextureSize: 2048
    textureCompression: 0      # ← Uncompressed (RGBA32)
    generateMipMaps: 0         # ✔ doğru
    readable: 0                # ✔ doğru
    crunchedCompression: 0
  platformSettings: []         # ← Android/iOS override YOK
  ```

  `textureCompression: 0` **Uncompressed** demek, yani çalışma anındaki atlas dokusu piksel başına 4 bayt. `platformSettings` boş olduğu için Android ve iOS de bu varsayılanı miras alıyor.

  `FruitAtlas`'ın kapsadığı alan: 11 meyve gövdesi (~470 px) + 48 yüz sprite'ı (12 ifade × 512/256/128/64). Kaba hesap ≈ 6.6 M piksel, yani 2048² sayfalara sığdırıldığında **2 sayfa**. 2 × 2048² × 4 bayt = **~33 MB**. `UIAtlas` da (4 packable) en az bir 2048² sayfa, +16 MB. Bu, bir merge oyununun tamamı için ayrılmış doku belleğinin kat kat üstünde ve aynı zamanda her karede örneklenen bant genişliği.

  Bu ayrıca kök dizindeki `fm.apk`'nin 92 MB olmasının en olası ana sebebi.
- **Beklenen etki:** ASTC 4×4'e geçiş: 4 bayt/px → 1 bayt/px, yani **~49 MB → ~12 MB** doku belleği ve aynı oranda doku okuma bant genişliği. Orta segment Android'de bellek baskısı ve GPU doku cache miss'i doğrudan düşer.
- **Oynanışa risk:** Yok. **Görsel risk: çok düşük** — ASTC 4×4, RGBA32'ye göre pratikte ayırt edilemeyecek kalitede bir blok sıkıştırma (piksel başına 8 bit, alfa dahil). Yine de bu bir *sıkıştırma*, dolayısıyla teorik olarak kayıplı: uygulamadan sonra en küçük meyveyi (kiraz) ve yüzlerin `sm` boyutunu cihazda gözle kontrol edin. Daha agresif olan ASTC 6×6 [bölüm 6'da](#6-önerilmeyen-optimizasyonlar) ayrıca değerlendirildi.
- **Çözüm:** Her iki atlas asset'ini seç → Inspector → **Android** ve **iOS** sekmelerinde `Override` işaretle:

  | Ayar | Değer |
  |---|---|
  | Max Texture Size | 2048 |
  | Format | **ASTC 4×4** (hem Android hem iOS) |
  | Compressor Quality | Normal |

  `Generate Mip Maps` kapalı kalsın (ortografik 2D'de mipmap gereksiz — şu an doğru). `Read/Write` kapalı kalsın (doğru).

  Ayrıca meta'da doğrulanabilir bir ikinci nokta: `compressionQuality: 50` alanı yalnızca sıkıştırma açıkken anlamlı; şu an hiçbir etkisi yok.

> **Not:** Bu bir asset ayarı, kod değişikliği değil. Uygulamadan sonra Memory Profiler'da `Texture2D` toplamını karşılaştırın — beklenen düşüş ~4 katı.

---

### F-05 `Fruit` kendi `Update` ve `FixedUpdate`'sini taşıyor (kural 7 ihlali)

- **Dosya / satır:** `Assets/FruitMerge/Scripts/Gameplay/Fruit.cs : 195-242` (`Update`), `: 244-271` (`FixedUpdate`)
- **Kategori:** Update maliyeti
- **Önem:** Orta
- **Sorun:** Proje "tek Update" desenini tutarlı biçimde uyguluyor: `FruitFace` (`: 5-7` "Update'i YOK"), `Worm` (`: 14-15`), `ComboPopupItem` (`: 14-16`) hepsi director'ün tek `Update`'inden `Tick`'leniyor. **Tek istisna `Fruit`'in kendisi** — ve sahnede en çok kopyası olan bileşen tam olarak o.

  60 aktif meyvede:
  - `Fruit.Update` → kare başına 60 managed↔native geçişi (çoğu `: 197`'deki erken çıkışla bitiyor, ama çağrının kendisi ücretsiz değil),
  - `Fruit.FixedUpdate` → saniyede 50 adım × 60 = **3000 çağrı/sn**.

  Ayrıca bu iki metodun içeriği zaten havuzun elindeki listeden sürülebilir: `FruitPool._active` (`FruitPool.cs : 23`) hazır, index'lenebilir bir `List<Fruit>`.
- **Beklenen etki:** Boş/erken-çıkan bir `Update` çağrısı Unity'de ~0.1–0.3 µs. 60 meyve için kare başına ~10–20 µs, fizik adımı başına aynı mertebe. Tek başına küçük ama 60 meyvelik sahnenin toplam bütçesinde ölçülebilir ve **tamamen bedava** bir kazanç.
- **Oynanışa risk:** Yok — aynı iş, aynı sırada, tek çağrıdan.
- **Çözüm:** `Fruit`'in iki döngüsünü public `Tick` metotlarına çevir, sürmeyi ayrı bir küçük bileşene ver. **Neden `FruitPool`'un kendisine değil:** `FruitPool` `[DefaultExecutionOrder(-90)]`, `Fruit` ise varsayılan 0. `QuakeBoostDirector` (-30) `FixedUpdate`'te itme uyguluyor ve şu an `Fruit.FixedUpdate` ondan **sonra** çalışıyor. Sırayı korumak için tick'i varsayılan sıralı ayrı bir bileşene koyuyoruz.

Önce — `Fruit.cs : 195-271`:

```csharp
    void Update()
    {
        if (_popTimer < 0f && _squashTimer < 0f) return;
        ...
    }

    private void FixedUpdate()
    {
        if (_config == null) return;
        ...
    }
```

Sonra — `Fruit.cs` (gövdeler birebir aynı, yalnızca imza ve dt kaynağı değişiyor):

```csharp
    /// <summary>
    /// Pop / squash animasyonu. Kendi Update'i YOK (kural 7): 60 meyve için 60
    /// managed↔native geçişi yerine FruitTicker tek döngüden çağırıyor.
    /// </summary>
    public void TickVisual(float dt)
    {
        if (_popTimer < 0f && _squashTimer < 0f) return;

        float popScale = 1f;

        if (_popTimer >= 0f)
        {
            _popTimer += dt;
            ...
        }
        ...
    }

    /// <summary>
    /// Dönüş söndürme + Continuous→Discrete geçişi. Fizik adımına ait olduğu için
    /// FruitTicker'ın FixedUpdate'inden çağrılıyor.
    /// </summary>
    public void TickPhysics()
    {
        if (_config == null) return;
        ...
    }
```

Yeni dosya — `Assets/FruitMerge/Scripts/Gameplay/FruitTicker.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tahtadaki bütün meyveleri TEK döngüden sürer (kural 7). Meyvelerin kendi
/// Update/FixedUpdate'i yok; en çok kopyası olan bileşen için 60 ayrı managed↔native
/// geçişi ödemenin karşılığı yok.
///
/// <b>Execution order neden 0:</b> QuakeBoostDirector (-30) itmeleri FixedUpdate'te
/// uyguluyor ve meyvenin dönüş söndürmesi ondan SONRA çalışmalı — eski
/// <c>Fruit.FixedUpdate</c> varsayılan sırada (0) olduğu için zaten öyleydi.
/// FruitPool (-90) üzerine koysaydık sıra ters dönerdi.
/// </summary>
[DefaultExecutionOrder(0)]
public class FruitTicker : MonoBehaviour
{
    void Update()
    {
        FruitPool pool = FruitPool.Instance;

        if (pool == null) return;

        IReadOnlyList<Fruit> active = pool.Active;

        float dt = Time.deltaTime;

        for (int i = 0; i < active.Count; i++)
        {
            Fruit f = active[i];

            if (f != null) f.TickVisual(dt);
        }
    }

    void FixedUpdate()
    {
        FruitPool pool = FruitPool.Instance;

        if (pool == null) return;

        IReadOnlyList<Fruit> active = pool.Active;

        for (int i = 0; i < active.Count; i++)
        {
            Fruit f = active[i];

            if (f != null) f.TickPhysics();
        }
    }
}
```

Sahnede: `FruitPool`'un durduğu objeye (ya da yeni bir boş objeye) `FruitTicker` ekle. Havuzdan çıkmış (`SetActive(false)`) meyveler `_active` listesinde olmadığı için tick almazlar — eski davranışla birebir aynı.

---

### F-06 `Fruit.FixedUpdate` uyuyan gövdelerde de boşa dönüyor

- **Dosya / satır:** `Assets/FruitMerge/Scripts/Gameplay/Fruit.cs : 244-271`
- **Kategori:** Fizik / Update maliyeti
- **Önem:** Orta
- **Sorun:** Metodun tamamı, yerleşip uyuyan bir meyve için **hiçbir şey yapmıyor**:

  - `_rb.linearVelocity.sqrMagnitude` okunuyor (native get) → uyuyan gövdede 0,
  - `isSlow == true` olduğu için `_rb.angularVelocity = Mathf.MoveTowards(0, 0, ...)` yazılıyor → 0 üstüne 0,
  - `_rb.collisionDetectionMode` okunuyor (native get) → uyumadan önce zaten `Discrete`'e düşmüş olduğu için `return`.

  `m_TimeToSleep: 0.5` ve `spinSettleRate = 180 °/sn²` ile bir meyve uykuya geçtiğinde açısal hızı zaten tam 0 olmuş oluyor (0.02 sn'de 3.6 °/sn sönüyor, uyku eşiği 2 °/sn), yani yazılan değer gerçekten no-op. Ama **iki native property okuması + bir yazma, 60 meyve × 50 adım = saniyede 9000 native geçiş** olarak duruyor. Suika tipi bir oyunda yığının büyük kısmı zamanın çoğunda uyuyor — yani bu maliyet tam olarak "en kalabalık anda en gereksiz".
- **Beklenen etki:** Uyuyan meyve oranı %70 varsayımıyla saniyede ~6000 native geçişin elenmesi. `Physics2D` marker'ında değil, `FruitTicker.FixedUpdate` / `Fruit.FixedUpdate` marker'ında görülür.
- **Oynanışa risk:** Yok. Uyuyan gövdenin hızı zaten uyku toleransının altında, açısal hızı 0 ve modu `Discrete`; erken çıkış hiçbir durum değiştirmiyor. Gövde bir temasla uyandığı anda tick geri devreye giriyor.
- **Çözüm:** F-05'teki `TickPhysics`'in başına iki satırlık kapı. (F-05'i uygulamıyorsanız aynı iki satır `FixedUpdate`'in başına konur.)

Önce:

```csharp
    public void TickPhysics()
    {
        if (_config == null) return;

        float limitSqr = _config.continuousExitSpeed * _config.continuousExitSpeed;
```

Sonra:

```csharp
    public void TickPhysics()
    {
        if (_config == null) return;

        // Daldaki bekleyen meyve simülasyonda değil: hızı da, çarpışma modu da
        // ResetState'te sabitlendi, burada yapılacak iş yok.
        if (!_rb.simulated) return;

        // Uyuyan gövdede bu metodun TAMAMI no-op: hız 0 (uyku toleransının altında),
        // açısal hız uykuya geçmeden önce zaten 0'a sönmüş, mod da Discrete'e düşmüş.
        // Yığının büyük kısmı zamanın çoğunda uyuyor — 60 meyve × 50 adımda saniyede
        // binlerce native property erişimi buradan eleniyor. Temas gövdeyi uyandırdığı
        // anda tick kendiliğinden geri geliyor.
        if (!_rb.IsAwake()) return;

        float limitSqr = _config.continuousExitSpeed * _config.continuousExitSpeed;
```

---

### F-07 `OnCollisionStay2D` içinde temas başına tekrarlanan native okuma

- **Dosya / satır:** `Assets/FruitMerge/Scripts/Gameplay/Fruit.cs : 280-298`
- **Kategori:** Fizik
- **Önem:** Orta
- **Sorun:**

  ```csharp
  void OnCollisionStay2D(Collision2D c)
  {
      TryRequestMerge(c);
      TryRearmContinuous();   // ← c'yi hiç kullanmıyor
  }
  ```

  `TryRearmContinuous()` yalnızca meyvenin **kendi** durumuna bakıyor (`_rb.collisionDetectionMode`, `_rb.linearVelocity`) — temas parametresine hiç dokunmuyor. Ama `OnCollisionStay2D` **temas başına** çağrılıyor: yığının içindeki bir meyvenin 3-5 komşusu var, yani aynı fizik adımında aynı hesap 3-5 kez yapılıyor.

  Üstelik yerleşmiş bir meyve `Discrete` modda olduğu için ilk `if` erken çıkmıyor; `linearVelocity` de okunuyor. Yani temas başına **2 native property get**.

  `TryRequestMerge(c)` temas başına çağrılmak **zorunda** (her komşuyu ayrı ayrı sınaması gerekiyor) — ona dokunulmamalı. Bu bulgu yalnızca `TryRearmContinuous` hakkında.
- **Beklenen etki:** 60 meyve × ortalama 4 temas × 50 adım = saniyede ~12.000 çağrı; fizik adımı başına bire indirilince ~3.000'e düşüyor. Yaklaşık 18.000 native property erişimi/sn eleniyor.
- **Oynanışa risk:** Yok — aynı fizik adımı içinde sonuç birebir aynı (adım içinde hız değişmiyor, mod da yalnızca bu metot tarafından yükseltiliyor). Tünelleme koruması olan "anında Continuous'a dön" davranışı **aynı adımda** korunuyor; bu yüzden `FixedUpdate`'e taşımak yerine adım-başına-bir-kez guard'ı kullanıyoruz (taşımak bir adım gecikme demek olurdu).
- **Çözüm:** Fizik adımını sayıp adım başına bir kez çalıştır. `Time.frameCount` **kullanılmamalı** — bir render karesinde birden fazla fizik adımı olabiliyor.

Önce — `Fruit.cs : 273-298`:

```csharp
    void OnCollisionEnter2D(Collision2D c)
    {
        TryRequestMerge(c);
        TryRequestSquash(c);
        TryRearmContinuous();
    }

    void OnCollisionStay2D(Collision2D c)
    {
        TryRequestMerge(c);
        TryRearmContinuous();
    }
```

Sonra:

```csharp
    void OnCollisionEnter2D(Collision2D c)
    {
        TryRequestMerge(c);
        TryRequestSquash(c);

        // Enter temas başına en fazla bir kez geldiği için burada guard'a gerek yok.
        TryRearmContinuous();
    }

    void OnCollisionStay2D(Collision2D c)
    {
        TryRequestMerge(c);

        // TryRearmContinuous temas parametresine BAKMIYOR, sadece kendi hızımıza bakıyor —
        // yığının içindeki meyvenin 4 komşusu varsa aynı hesap aynı fizik adımında 4 kez
        // yapılıyordu. Adım başına bir kez yeterli; aynı adım içinde sonuç değişmiyor,
        // dolayısıyla "sert çarpışmadan sonra ANINDA Continuous'a dön" garantisi bozulmuyor.
        if (_rearmStep == _physicsStep) return;

        _rearmStep = _physicsStep;

        TryRearmContinuous();
    }
```

`TickPhysics` (F-05 sonrası; yoksa `FixedUpdate`) sayacı artırıyor, alanlar `_slowFrames`'in yanına:

```csharp
    private int _slowFrames;

    /// <summary>Fizik adımı sayacı. Time.frameCount KULLANILMIYOR: bir render karesinde
    /// birden fazla fizik adımı olabiliyor ve o zaman guard yanlış eliyor olurdu.</summary>
    int _physicsStep;
    int _rearmStep = -1;
```

```csharp
    public void TickPhysics()
    {
        _physicsStep++;

        if (_config == null) return;
        ...
    }
```

> **Dikkat:** `_physicsStep`'in artışı `TickPhysics`'in erken çıkışlarından **önce** olmalı, yoksa uyuyan meyve uyandığında guard bayat kalır. `ResetState`/`Initialize` içinde sıfırlamaya gerek yok — `_rearmStep = -1` başlangıç değeri ve monoton artan sayaç yeterli; yine de havuz döngüsünde tutarlılık için `ResetState`'e `_rearmStep = -1;` eklemek zararsız.

---

### F-08 `FruitFace.TickLook` hedef yokken de her karede transform yazıyor

- **Dosya / satır:** `Assets/FruitMerge/Scripts/Gameplay/FruitFace.cs : 251-274`
- **Kategori:** Update maliyeti
- **Önem:** Orta
- **Sorun:**

  ```csharp
  _lookOffset = Vector2.Lerp(_lookOffset, want, Mathf.Clamp01(dt * _lookSpeed));

  transform.localPosition = _baseOffset + _lookOffset;
  ```

  `Lerp` hedefe asimptotik yaklaşıyor, yani `_lookOffset` **hiçbir zaman tam olarak** `want`'a eşit olmuyor. Sonuç: bakış hedefi olmayan (`ClearLook` → `want = Vector2.zero`) ve zaten yerine oturmuş bir yüz için bile her karede `localPosition` yazılıyor.

  `Transform` yazımı ücretsiz değil: dirty bayrağı, child transform hiyerarşisinin invalidasyonu ve `SpriteRenderer`'ın bounds güncellemesi tetikleniyor. 60 meyvede kare başına 60 gereksiz yazma.

  `FaceDirector.TickFaces` (`: 436-501`) bakış hedefi olmayan meyveler için `face.ClearLook()` çağırıyor — yani yığının büyük kısmı tam bu durumda.

  İkinci nokta: `_hasLook` doğruyken `_owner.InverseTransformDirection(...)` (`: 263`) native bir çağrı ve meyve başına her karede yapılıyor. Bu gerekli (yüz gövdeyle döndüğü için) — sadece hedefi olmayan meyvelerde hiç girilmediğini garanti etmek yeterli, ki zaten öyle.
- **Beklenen etki:** Yığın durgunken kare başına ~60 transform yazımı ve bağlı bounds/dirty işi eleniyor. `FaceDirector.Update` marker'ında görülür; 60 meyvede tipik olarak 0.05–0.2 ms.
- **Oynanışa risk:** Yok. Eşik `_lookRadius`'un (0.18 dünya birimi) on binde biri; bu mesafe referans çözünürlükte alt-piksel mertebesinde, gözle ayırt edilemez. Bakış hareketi başladığı anda (fark eşiğin üstüne çıkınca) tam eski davranışa dönülüyor.
- **Çözüm:**

Önce — `FruitFace.cs : 251-274`:

```csharp
    void TickLook(float dt)
    {
        Vector2 want = Vector2.zero;

        if (_hasLook && _owner != null)
        {
            ...
        }

        _lookOffset = Vector2.Lerp(_lookOffset, want, Mathf.Clamp01(dt * _lookSpeed));

        transform.localPosition = _baseOffset + _lookOffset;
    }
```

Sonra:

```csharp
    /// <summary>
    /// Bakış hedefine yumuşama eşiği (dünya birimi²). Lerp hedefe asimptotik yaklaştığı
    /// için _lookOffset asla tam olarak want'a eşitlenmiyordu — bakış hedefi olmayan
    /// durgun bir yüz için bile her karede localPosition yazılıyordu (60 meyve = kare
    /// başına 60 gereksiz transform kirletme + bounds güncellemesi).
    ///
    /// Eşik faceLookRadius'un (0.18) on binde biri; alt-piksel mertebesinde, gözle
    /// ayırt edilemiyor. Fark eşiğin üstüne çıktığı anda eski davranış birebir geri geliyor.
    /// </summary>
    const float LookSnapSqr = 1e-8f;

    void TickLook(float dt)
    {
        Vector2 want = Vector2.zero;

        if (_hasLook && _owner != null)
        {
            ...
        }

        // Zaten hedefte: transform'a dokunma.
        if ((want - _lookOffset).sqrMagnitude <= LookSnapSqr)
        {
            if (_lookOffset != want)
            {
                _lookOffset = want;
                transform.localPosition = _baseOffset + _lookOffset;
            }

            return;
        }

        _lookOffset = Vector2.Lerp(_lookOffset, want, Mathf.Clamp01(dt * _lookSpeed));

        transform.localPosition = _baseOffset + _lookOffset;
    }
```

---

### F-09 `ComputeFillRatio` önbelleklenmiş `FloorY` yerine collider bounds'u okuyor

- **Dosya / satır:** `Assets/FruitMerge/Scripts/Services/GameOverDetector.cs : 144-146` (karşılaştırın: `: 29-45`)
- **Kategori:** Update maliyeti
- **Önem:** Orta
- **Sorun:** Aynı dosya, `FloorY` property'sinde bu problemi zaten çözmüş ve gerekçesini de yazmış:

  ```csharp
  /// Zeminin üst yüzeyi. Zemin hareket etmediği için bir kez hesaplanıp saklanıyor —
  /// Collider2D.bounds native bir çağrı, her karede meyve başına istemiyoruz.
  public float FloorY { get { if (!_floorCached) { ... } return _cachedFloorY; } }
  ```

  Ama `ComputeFillRatio` bu property'yi **kullanmıyor**:

  ```csharp
  float floorY = _floor != null ? _floor.bounds.max.y : transform.position.y - 5f;
  ```

  Yani `gameOverCheckInterval` (0.1 sn = saniyede 10 kez) native `Collider2D.bounds` çağrısı yapılıyor ve bu, `FloorY`'nin tam olarak engellemek için var olduğu şey. `QuakeBoostDirector` aynı deseni doğru uygulamış (`: 116-121`, "bir kez oku").
- **Beklenen etki:** Saniyede 10 native `bounds` çağrısı eleniyor. Küçük — ama düzeltme tek satır ve aynı dosyadaki mevcut çözümü kullanmak, kodun iki farklı doğruyu aynı anda söylemesini de bitiriyor.
- **Oynanışa risk:** Yok — birebir aynı değer (zemin hareket etmiyor).
- **Çözüm:**

Önce — `GameOverDetector.cs : 144-146`:

```csharp
    float ComputeFillRatio()
    {
        float floorY = _floor != null ? _floor.bounds.max.y : transform.position.y - 5f;
        float span   = transform.position.y - floorY;
```

Sonra:

```csharp
    float ComputeFillRatio()
    {
        // Önbellekli property (bkz. FloorY): Collider2D.bounds native bir çağrı ve
        // zemin hiç hareket etmiyor. Buradaki kopya hesap onu boşa çıkarıyordu.
        float floorY = FloorY;
        float span   = transform.position.y - floorY;
```

---

### F-10 Açılışta ~250 GameObject Awake/Start'ta yaratılıyor (PrewarmQueue atlanmış)

- **Dosya / satır:** `ConfettiDirector.cs : 124-162` (`BuildPool`, Awake'ten) · `WormBoostDirector.cs : 185-207` (`BuildCursors`) ve `: 228-248` (`BuildWorms`, Start'tan) · `CoinFlyDirector.cs : 115-156` (`BuildPool`, Awake'ten) · `AudioService.cs : 207-252` (`BuildSources`, Awake'ten)
- **Kategori:** Lifecycle / açılış süresi
- **Önem:** Orta
- **Sorun:** Proje bu problemi zaten teşhis etmiş ve çözmüş — ama yalnızca iki havuz için. `PrewarmQueue.cs : 6-13`:

  > `FruitPool` ve `ComboPopupDirector` ısıtmayı `Awake` içinde TEK KAREDE yapıyordu (40 + 6 = 46 Instantiate). Bu iş sahne yüklenirken bittiği için ilk kare o kadar geç geliyordu; oyuncu boş ekrana bakıyordu.

  Ölçtüğüm sahne değerleriyle **`PrewarmQueue`'ya kaydolmayan** ısıtmalar:

  | Yer | Sayı | Nerede |
  |---|---|---|
  | `ConfettiDirector.BuildPool` | **140** GameObject (RectTransform + CanvasRenderer + Image) | `Awake` |
  | `WormBoostDirector.BuildCursors` | **44** GameObject (+ Pulse = 45) | `Start` |
  | `WormBoostDirector.BuildWorms` | 6 kurt × `wormSegmentCount` 5 halka = **30** + 6 kök = 36 | `Start` |
  | `CoinFlyDirector.BuildPool` | **32** GameObject | `Awake` |
  | `AudioService.BuildSources` | 6 + rumble + music = **8** GameObject (AudioSource) | `Awake` |

  Toplam **~260 GameObject, tek karede**. Yani `PrewarmQueue` 46 nesneyi karelere yayarken, aynı açılışta onun 5 katı hâlâ tek karede yaratılıyor. Konfetinin 140 UI `Image`'ı bunların en pahalısı (her biri `RectTransform` + `CanvasRenderer` + `Image` + canvas hiyerarşisine kayıt).

  Ayrıca `SplashPanel`'in yükleme çubuğu "gerçek işi gösteriyor" iddiasında (`SplashPanel.cs : 9-13`) ama gösterdiği iş toplam ısıtmanın yalnızca ~%18'i.
- **Beklenen etki:** İlk kareye kadarki gecikme. 260 GameObject + bileşen ekleme, orta segment Android'de tipik olarak 150–500 ms; karelere yayılınca aynı toplam iş, ama uygulama gözle görülür şekilde daha erken açılıyor ve çubuk gerçekten dolduğu işi gösteriyor.
- **Oynanışa risk:** Raporu yazarken "Yok" demiştim. **Yanlıştı — uygulandığında Play Mode kırıldı.**

> ### ❌ Bu bulgu UYGULANDI ve GERİ ALINDI
>
> Aşağıdaki çözüm birebir uygulandı ve **oyun açılış ekranında kilitlendi**:
> `WormBoostDirector.PrewarmStep` her karede `IndexOutOfRangeException` attı,
> `PrewarmQueue.Done` hiç `Total`'a ulaşmadı, `SplashPanel`'in çubuğu dolmadı.
>
> **Kök sebep:** proje Play Mode'da **Reload Domain ve Reload Scene kapalı** çalışıyor.
> Bu konfigürasyonda serialize edilmeyen instance alanları (`_prewarmDone`, `_worms`,
> `_cursors`) oturumlar arasında yaşıyor ama runtime'da yaratılan GameObject'ler yok
> ediliyor — sayaç ile gerçeklik ayrışıyor. Kod tabanı bu tuzağı `GameEvents`,
> `BoostGate` ve `PointerInput`'ta `ResetStatics` deseniyle kapatıyor;
> **`PrewarmQueue` bu korumaya sahip olmayan tek statik** ve state tam oraya eklendi.
>
> **Doğru yapmanın yolu** (isteyen olursa): `PrewarmQueue`'ya
> `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)] static void ResetStatics()`
> ekleyip `_sources.Clear()` yapmak, ARTI her kaynağın ısıtma durumunu (`_prewarmDone` +
> yaratılmış obje dizileri) oturum başında sıfırlaması. Yani bu bulgu, göründüğünden
> daha büyük bir iş; kazancı (açılışta tek karelik takılma, oynanışa etkisi sıfır) o
> işi hak etmiyor.
>
> **Kalıcı iz:** üç `Build*` metodunun üstüne ve `splashPrewarmPerFrame`'in tooltip'ine
> "bu bilerek Awake/Start'ta" notu yazıldı.

- **Çözüm (uygulanmadı — yukarıdaki uyarıyı oku):** Üç director'ü de `IPrewarmSource`'a bağla — `FruitPool.cs : 155-164` ve `ComboPopupDirector.cs : 119-128` şablonu birebir uygulanabiliyor. `AudioService`'in 8 kaynağı ısıtmaya değmez, bırakılabilir.

Örnek — `ConfettiDirector.cs`. Önce (`: 86-97`, `: 124-162`):

```csharp
[DefaultExecutionOrder(-40)]
public class ConfettiDirector : MonoBehaviour
{
    ...
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }

        Instance = this;

        BuildPool();
    }
    ...
    void BuildPool()
    {
        int count = _config != null ? Mathf.Max(1, _config.confettiPoolSize) : 140;
        float size = _config != null ? _config.confettiSize : 64f;

        int spriteCount = _sprites != null ? _sprites.Length : 0;

        _pieces = new Piece[count];

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Confetti_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ...
        }
    }
```

Sonra:

```csharp
[DefaultExecutionOrder(-40)]
public class ConfettiDirector : MonoBehaviour, IPrewarmSource
{
    ...
    int _prewarmDone;

    public int PrewarmTotal => _config != null ? Mathf.Max(1, _config.confettiPoolSize) : 140;

    public int PrewarmDone => _prewarmDone;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }

        Instance = this;

        // Dizi burada ayrılıyor (ucuz), GameObject'ler AÇILIŞ EKRANI boyunca karelere
        // yayılıyor. 140 UI Image tek karede yaratılınca ilk kare gecikiyordu — FruitPool
        // ve ComboPopupDirector'ün zaten çözdüğü sorunun aynısı (bkz. PrewarmQueue).
        _pieces = new Piece[PrewarmTotal];

        PrewarmQueue.Register(this);
    }

    void OnDestroy()
    {
        PrewarmQueue.Unregister(this);

        if (Instance == this) Instance = null;
    }
    ...
    /// <summary>Bu karede en fazla <paramref name="budget"/> parça yarat.</summary>
    public void PrewarmStep(int budget)
    {
        if (budget <= 0 || _pieces == null) return;

        int end = Mathf.Min(_prewarmDone + budget, _pieces.Length);

        for (int i = _prewarmDone; i < end; i++) CreatePiece(i);

        _prewarmDone = end;
    }

    void CreatePiece(int i)
    {
        float size = _config != null ? _config.confettiSize : 64f;

        int spriteCount = _sprites != null ? _sprites.Length : 0;

        var go = new GameObject("Confetti_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        ... // gövde birebir eski BuildPool döngüsünün içi
    }
```

Aynı desen `CoinFlyDirector.BuildPool` (32) ve `WormBoostDirector.BuildCursors`/`BuildWorms` (81) için de geçerli. `WormBoostDirector`'de dikkat: `Toggle()` çağrıldığında havuz hazır olmalı — `CanArm` zaten `GameManager.IsPlaying` şartını koşuyor ve oynanış açılış ekranından sonra başlıyor, yani ısıtma bitmiş oluyor. Yine de `PlaceCursors` (`: 370`) havuz yetmezse `CreateCursor()` ile büyütüyor, dolayısıyla yarım ısıtma bile güvenli.

Ek olarak `GameConfig.splashPrewarmPerFrame` (2) bu yeni yükle birlikte gözden geçirilmeli: 46 + 260 = ~306 nesne, kare başına 2 ile 153 kare (≈2.5 sn) sürer. `splashMinDuration` 1.2 sn olduğu için çubuk artık **işi** bekler. Kare başına 4-6'ya çıkarmak makul; ölçüp ayarlayın.

---

### F-11 Physics2D job multithreading kapalı

- **Dosya / satır:** `ProjectSettings/Physics2DSettings.asset : 30` (`m_JobOptions.useMultithreading: 0`, `useConsistencySorting: 0`)
- **Kategori:** Fizik
- **Önem:** Orta
- **Sorun:** Sahnede eşzamanlı 60 `Rigidbody2D` + `CircleCollider2D` var ve `Physics2D` iş dağıtımı tek çekirdekte çalışıyor. `m_JobOptions` altındaki eşikler (`m_IslandSolverBodiesPerJob: 50`, `m_CollideContactsPerJob: 100` vb.) zaten tanımlı ama `useMultithreading: 0` olduğu için hiç kullanılmıyor.
- **Beklenen etki:** Sıkışık bir yığında (tek büyük ada, çok temas) çözücü iş parçalarına bölünebilir. 60 gövdede beklenen kazanç ölçüme bağlı: ada başına maliyet eşiği (`m_IslandSolverCostThreshold: 100`) aşılmazsa kazanç sıfıra yakın olur, aşılırsa `Physics2D.Simulate` süresi belirgin düşer. **Bu, tahminle değil ölçümle karar verilecek tek fizik ayarı.**
- **Oynanışa risk:** **Düşük.** Çözüm sırası değiştiği için sonuç bit-bit aynı olmayabilir; `useConsistencySorting`'i **birlikte açmak** bunu düzeltir (sıralamayı deterministik yapar, karşılığında bir miktar kazançtan feragat edersiniz). Oyunda kayıt/replay determinizmi gerektiren hiçbir şey yok (fizik durumu kaydedilmiyor — `SaveService` yalnızca skor/coin/ayar tutuyor), yani determinizm kaybı oynanışı bozmuyor. Yine de "hissi değiştirmez" garantisi vermek için:
  - Önce `useConsistencySorting: 1` **ile** açın, cihazda 60 meyvelik bir yığınla oynayın.
  - Sekme/oturma davranışında fark hissederseniz geri kapatın; bu ayar oyunun geri kalanından tamamen bağımsız.
- **Çözüm:** Project Settings → Physics 2D → **Job Options**:
  - `Use Multithreading` → **açık**
  - `Use Consistency Sorting` → **açık** (davranışı sabitlemek için)
  - Diğer eşikler varsayılanda kalsın.

  Bu ayarı **`velocityIterations`/`positionIterations` düşürmekle karıştırmayın** — onlar oynanışı bozar ve [bölüm 6'da](#6-önerilmeyen-optimizasyonlar) reddedildi. Multithreading aynı çözümü daha hızlı üretiyor, çözümü *zayıflatmıyor*.

---

### F-12 `PlaceCursors` sabit değeri meyve başına her karede yeniden hesaplıyor

- **Dosya / satır:** `Assets/FruitMerge/Scripts/Services/WormBoostDirector.cs : 358-397`
- **Kategori:** Update maliyeti
- **Önem:** Orta
- **Sorun:**

  ```csharp
  float unit = sr.sprite != null
      ? sr.sprite.rect.width / sr.sprite.pixelsPerUnit
      : 1f;
  ```

  Bütün nişangâhlar **aynı** `_crosshair` sprite'ını kullanıyor (`CreateCursor`, `: 217`). Yani `unit` sabit bir sayı — ama tahtadaki her meyve için, boost silahlıyken **her karede** yeniden okunuyor. `Sprite.rect` ve `Sprite.pixelsPerUnit` ikisi de native property.

  44 meyveli bir tahtada kare başına 88 gereksiz native erişim, ve bu tam olarak oyuncunun hedef seçmeyi düşündüğü (yani ekranın hareketsiz, oyuncunun dikkatli olduğu) an.

  Aynı hesap `_pulseRefUnit` için **doğru** yapılmış (`BuildCursors`, `: 203-204`): bir kez, kurulumda. Nişangâh da aynı şekilde ele alınmalı.
- **Beklenen etki:** Silahlı fazda kare başına ~88 native property erişimi eleniyor; `WormBoostDirector.Update` marker'ında görülür.
- **Oynanışa risk:** Yok — sabit bir değeri bir kez hesaplamak.
- **Çözüm:**

Önce — `WormBoostDirector.cs : 185-207` ve `: 380-390`:

```csharp
    void BuildCursors()
    {
        var parent = new GameObject("Cursors");
        ...
        if (_pulseFrames != null && _pulseFrames.Length > 0 && _pulseFrames[0] != null)
            _pulseRefUnit = _pulseFrames[0].rect.width / _pulseFrames[0].pixelsPerUnit;

        pulseGo.SetActive(false);
    }
```

```csharp
            // nişangâhın sprite'ı 1 dünya biriminden farklı — meyveye göre ölçekle
            float world = f.Radius * 2f * _config.boostCrosshairScale;

            float unit = sr.sprite != null
                ? sr.sprite.rect.width / sr.sprite.pixelsPerUnit
                : 1f;

            float k = world / Mathf.Max(0.0001f, unit);
```

Sonra — alan tanımı `_pulseRefUnit`'in yanına:

```csharp
    /// <summary>
    /// Nişangâh sprite'ının dünya genişliği. Bütün nişangâhlar AYNI sprite'ı kullanıyor,
    /// yani bu sabit — eskiden meyve başına her karede sprite.rect + pixelsPerUnit
    /// (iki native property) okunuyordu. _pulseRefUnit ile aynı desen.
    /// </summary>
    float _cursorRefUnit = 1f;
```

```csharp
    void BuildCursors()
    {
        var parent = new GameObject("Cursors");
        ...
        if (_crosshair != null)
            _cursorRefUnit = _crosshair.rect.width / _crosshair.pixelsPerUnit;

        if (_pulseFrames != null && _pulseFrames.Length > 0 && _pulseFrames[0] != null)
            _pulseRefUnit = _pulseFrames[0].rect.width / _pulseFrames[0].pixelsPerUnit;

        pulseGo.SetActive(false);
    }
```

```csharp
            // nişangâhın sprite'ı 1 dünya biriminden farklı — meyveye göre ölçekle
            float world = f.Radius * 2f * _config.boostCrosshairScale;

            float k = world / Mathf.Max(0.0001f, _cursorRefUnit);
```

Ayrıca aynı metotta `var fruits = _pool.Active;` (`: 360`) `_pool` null kontrolü yapmıyor — `ArmFruitsForShaking` (`: 238`) ve `FindFruitAt` bunu farklı şekilde ele alıyor. Tutarlılık için `if (_pool == null) return;` eklenmeli (bkz. F-18).

---

### F-13 Ölü ayar alanları — Inspector'dan çevirince hiçbir şey olmuyor

- **Dosya / satır:** `GameConfig.cs : 69` (`continuousEnterFrames`), `: 189` (`maxConcurrentEffects`), `: 192` (`effectPrewarmCount`), `: 275` (`newRecordDelay`) · `FruitDefinition.cs : 51` (`countForGameOver`)
- **Kategori:** Bug / bakım riski
- **Önem:** Orta
- **Sorun:** Beş serialize alanı hiçbir yerde okunmuyor — `Assets/FruitMerge/Scripts` altında referans sayısı **sıfır**:

  | Alan | Tooltip'in verdiği izlenim | Gerçek |
  |---|---|---|
  | `FruitDefinition.countForGameOver` | Bu meyve oyun sonu kontrolüne sayılmasın | `GameOverDetector.HasViolation` (`: 122-142`) bu alana **hiç bakmıyor**; her `IsDropped` meyve sayılıyor |
  | `GameConfig.continuousEnterFrames` | Continuous'a girme eşiği | Girme `continuousRearmSpeed` ile hızdan karar veriliyor, kare sayısı kullanılmıyor |
  | `GameConfig.maxConcurrentEffects` | "mobilde overdraw sınırı… en eski efekt geri dönüştürülür" | `EffectDirector` paylaşımlı `ParticleSystem` kullanıyor, böyle bir sınır yok |
  | `GameConfig.effectPrewarmCount` | Efekt havuzu ısıtması | Efekt havuzu yok |
  | `GameConfig.newRecordDelay` | Son yıldızdan rekor şeridine bekleme | `GameOverPanel.OnTick` (`: 186-191`) şeridi **gecikmesiz** gösteriyor |

  Bunlar performans bulgusu değil ama **hata kaynağı**: `countForGameOver`'ı kapatan biri "bu meyve oyunu bitirmez" sanacak, `newRecordDelay`'i 1 sn'ye çeken biri şeridin gecikeceğini sanacak. `maxConcurrentEffects`'in tooltip'i ("Sınıra gelince en eski efekt geri dönüştürülür") var olmayan bir mekanizmayı anlatıyor.
- **Beklenen etki:** Performans etkisi yok; yanlış ayar yapma riski yüksek.
- **Oynanışa risk:** Yok (silmek) / Var (davranışı gerçekten uygulamak — o zaman bilinçli bir tasarım kararı gerekir).
- **Çözüm:** Her alan için **bilinçli** bir karar:

  1. **`countForGameOver`** — istenen davranış buysa `HasViolation`'a bir satır ekle. İstenmiyorsa alanı sil.

     ```csharp
     bool HasViolation()
     {
         float lineY = transform.position.y;
         var fruits = _pool.Active;

         for (int i = 0; i < fruits.Count; i++)
         {
             Fruit f = fruits[i];
             if (f == null) continue;
             if (!f.IsDropped) continue;
             if (f.IsMerging) continue;

             // FruitDefinition.countForGameOver şimdiye kadar hiç okunmuyordu: alanı
             // kapatan biri "bu meyve oyunu bitirmez" sanıyordu.
             if (f.Definition != null && !f.Definition.countForGameOver) continue;

             if (Time.time - f.DropTime < _config.dropGracePeriod) continue;
             ...
         }
     ```

  2. **`newRecordDelay`** — `GameOverPanel.OnTick`'e gecikmeyi ekle ya da alanı sil. Şerit şu an son yıldızla aynı karede çıkıyor; tooltip'in tarif ettiği 0.3 sn'lik ayrım kutlamanın okunurluğunu artırır, ama bu **his değişikliği** olduğu için ürün kararı — bana göre alanı silmek daha dürüst.

  3. **`continuousEnterFrames`, `maxConcurrentEffects`, `effectPrewarmCount`** — sil. Üçü de mimari değişince (hız tabanlı rearm, paylaşımlı ParticleSystem) geride kalmış artıklar. `maxConcurrentEffects`'in yanıltıcı tooltip'i özellikle silinmeli.

---

### F-14 `DropIndicatorController.Update` her karede gereksiz native iş yapıyor

- **Dosya / satır:** `Assets/FruitMerge/Scripts/Gameplay/DropIndicatorController.cs : 40-64`
- **Kategori:** Update maliyeti / Bug
- **Önem:** Orta
- **Sorun:** Metot dört ayrı konuda düzeltmeye açık:

  1. **`_renderer.enabled` her karede yazılıyor** (`: 43`) — `enabled` setter'ı native bir çağrı ve değer değişmediğinde de yapılıyor. Proje bu deseni başka yerlerde doğru uyguluyor (`BoostButton.SetBadges`, `: 146-154`: "SetActive yalnızca durum DEĞİŞTİYSE çağrılıyor").
  2. **`_floor.bounds.max.y` her karede okunuyor** (`: 49`) — `GameOverDetector.FloorY` (`: 33-45`) ve `QuakeBoostDirector.Start` (`: 116-121`) aynı değeri bir kez okuyup saklıyor; burada üçüncü ve önbelleksiz bir kopya var.
  3. **Null kontrolü yok** (`: 46, 49`) — `_config` ya da `_floor` sahnede boş kalırsa her karede `NullReferenceException`. Aynı dosyanın çağıranı (`DropController`) `_dropIndicator` için null kontrolü yapıyor (`: 268`), yani kod tabanının geri kalanı bu konuda titiz.
  4. `Physics2D.Raycast`'in bu aşırı yüklemesi **allocation yapmıyor** (tek `RaycastHit2D` döndürüyor, dizi versiyonu değil) — burada sorun yok, doğru seçilmiş.
- **Beklenen etki:** Kare başına 1 native `bounds` çağrısı + 1 gereksiz `enabled` yazımı. Küçük; asıl kazanç 3. maddedeki çökme riskinin kapanması.
- **Oynanışa risk:** Yok.
- **Çözüm:**

Önce — `DropIndicatorController.cs : 14-64`:

```csharp
    void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _mpb = new MaterialPropertyBlock();
    }
    ...
    void Update()
    {
        bool playing = GameManager.Instance != null && GameManager.Instance.IsPlaying;
        _renderer.enabled = playing && _hasPending;
        if (!playing || !_hasPending) return;

        float topWorldY = _fruitBottomWorldY - _config.dropIndicatorSkin;
        Vector2 origin = new Vector2(transform.position.x, topWorldY);

        float floorY = _floor.bounds.max.y;
        float maxDist = Mathf.Max(0.01f, topWorldY - floorY + 1f);
        ...
    }
```

Sonra:

```csharp
    /// <summary>
    /// Zeminin üst yüzeyi. Collider2D.bounds native bir çağrı ve zemin hiç hareket
    /// etmiyor — GameOverDetector.FloorY ve QuakeBoostDirector.Start ile aynı desen.
    /// </summary>
    float _floorY;

    void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _mpb = new MaterialPropertyBlock();
    }

    void Start()
    {
        _floorY = _floor != null ? _floor.bounds.max.y : transform.position.y - 5f;

        if (_floor == null)
            Debug.LogWarning("DropIndicatorController: _floor bağlı değil — gösterge " +
                             "zemine kadar uzamayacak.", this);
    }
    ...
    void Update()
    {
        bool playing = GameManager.Instance != null && GameManager.Instance.IsPlaying;

        bool visible = playing && _hasPending;

        // enabled setter'ı native bir çağrı: yalnızca durum DEĞİŞTİYSE yaz (kural 9).
        if (_renderer.enabled != visible) _renderer.enabled = visible;

        if (!visible || _config == null) return;

        float topWorldY = _fruitBottomWorldY - _config.dropIndicatorSkin;
        Vector2 origin = new Vector2(transform.position.x, topWorldY);

        float maxDist = Mathf.Max(0.01f, topWorldY - _floorY + 1f);

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, maxDist, _mask);
        float endWorldY = hit.collider != null ? hit.point.y : _floorY;
        ...
    }
```

`_floorY`'nin geri kalan kullanımları (`: 53`) de `floorY` → `_floorY` olarak güncellenmeli.

---

### F-15 Gereksiz Raycast Target işaretli UI elemanları

- **Dosya / satır:** `Game.unity` — aşağıdaki 12 eleman `m_RaycastTarget: 1`
- **Kategori:** UI
- **Önem:** Düşük
- **Sorun:** Sahnede 32 eleman raycast hedefi. Bunların bir kısmı **hiçbir zaman tıklanmıyor**; her pointer olayında `GraphicRaycaster` bunları da sınıyor:

  Kapatılabilir (salt gösterim):
  - `HUDCanvas/HudPanel/ScoreText`, `HUDCanvas/HudPanel/HighScoreText`
  - `PanelCanvas/GameOverPanel/Box/ScoreLabel`, `ScoreCaption`, `BestCaption`, `BestLabel`
  - Buton **içindeki** yazılar (butonun kendi `Image`'i zaten hedef): `PausePanel/Box/ResumeButton/Text (TMP)`, `PausePanel/Box/RestartButton/Text (TMP)`, `PausePanel/Box/MenuButton/Text (TMP)`, `GameOverPanel/Box/RestartButton/Text (TMP)`, `GameOverPanel/Box/MenuButton/Text (TMP)`

  **Kapatılmaması gerekenler** (davranışsal):
  - `HUDCanvas/HudPanel` — `DropController.HandleInput` (`: 200, 217`) `PointerInput.IsOverUI()` ile HUD'un üstündeki dokunuşu bilinçli olarak eliyor ("Tahtada başlayıp parmağını HUD'un üstüne kaydıranı da tutuyoruz"). Bunu kapatmak HUD alanına dokunulduğunda meyve düşürülmesine yol açar.
  - `HUDCanvas/BoostSlot`, `BoostSlot_Quake`, `PauseButton` — butonlar.
  - Panellerin `Dimmer` / `Background` / `Box` `Image`'leri — arkadaki tıklamayı yutmaları gerekiyor.
- **Beklenen etki:** Kare başına değil, **pointer olayı başına** kazanç. Dokunma sırasında `EventSystem.Update` içindeki raycast listesi kısalıyor. Çok küçük — ama riski sıfır ve 5 dakikalık iş.
- **Oynanışa risk:** Yok (yukarıdaki "kapatılmaması gerekenler" listesine uyulduğu sürece).
- **Çözüm:** Inspector'da listelenen 11 elemanın `Raycast Target`'ını kapat. TMP bileşenlerinde bu kutu "Extra Settings" altında.

  İleriye dönük: TMP prefab varsayılanlarınızda `Raycast Target`'ı kapalı tutmak, her yeni etikette bu adımı tekrarlamaktan kurtarır.

---

### F-16 Daldaki bırakılmamış meyve de coin ödülüne sayılıyor

- **Dosya / satır:** `Assets/FruitMerge/Scripts/Services/CoinRewardDirector.cs : 70-90`
- **Kategori:** Bug
- **Önem:** Düşük
- **Sorun:**

  ```csharp
  for (int i = 0; i < active.Count; i++)
  {
      var fruit = active[i];
      if (fruit == null || fruit.Definition == null) continue;
      total += fruit.Definition.coinReward;
  }
  ```

  `FruitPool.Active` **bekleyen meyveyi de içeriyor**: `DropController.PreparePending` (`: 274-278`) onu `_pool.Spawn` ile alıyor, `Spawn` → `Get` → `OnGetFruit` → `_active.Add(f)` (`FruitPool.cs : 98`). Yani oyun bittiğinde dalda asılı duran, oyuncunun hiç bırakmadığı meyve de ödül veriyor.

  Kod tabanının geri kalanı bu ayrımı **tutarlı biçimde** yapıyor — `GameOverDetector.HasViolation` (`: 130`), `ComputeFillRatio` (`: 155`), `QuakeBoostDirector.ApplyKicks` (`: 381`), `WormBoostDirector.FindFruitAt` (`: 464`) ve `PlaceCursors` (`: 368`) hepsi `!f.IsDropped` kontrolü yapıyor. Yalnızca burası atlamış.
- **Beklenen etki:** Şu anki içerik yapılandırmasında pratikte 0 coin: `spawnableCount = 5` olduğu için bekleyen meyve daima tier 0-4 ve `coinReward` yalnızca zincirin tepesindeki meyvelere verilmiş (`FruitDefinition.cs : 42-45` tooltip'i bunu açıkça söylüyor). Yani **latent** bir hata — `spawnableCount` artırıldığı ya da küçük meyvelere ödül verildiği gün sessizce yanlış ödül dağıtır.
- **Oynanışa risk:** Düzeltmenin riski Yok (mevcut değerlerde ödül miktarı değişmiyor).
- **Çözüm:**

Önce:

```csharp
            var fruit = active[i];

            if (fruit == null || fruit.Definition == null) continue;

            total += fruit.Definition.coinReward;
```

Sonra:

```csharp
            var fruit = active[i];

            if (fruit == null || fruit.Definition == null) continue;

            // Havuzun aktif listesi DALDAKİ bekleyen meyveyi de içeriyor (DropController
            // onu Spawn ile alıyor). Oyuncunun hiç bırakmadığı meyve ödül vermemeli —
            // GameOverDetector, QuakeBoostDirector ve WormBoostDirector aynı ayrımı
            // yapıyor, tek atlayan yer burasıydı.
            if (!fruit.IsDropped) continue;

            total += fruit.Definition.coinReward;
```

---

### F-17 MergeHandler kuyruk guard'ına takılırsa `_queuedPairs` kalıntı bırakıyor

- **Dosya / satır:** `Assets/FruitMerge/Scripts/Gameplay/MergeHandler.cs : 67-85`
- **Kategori:** Bug
- **Önem:** Düşük
- **Sorun:**

  ```csharp
  int guard = 0;
  while (_queue.Count > 0 && guard++ < 100)
  {
      ...
  }

  if (_queue.Count == 0) _queuedPairs.Clear();
  ```

  Guard 100'e takılırsa (`_queue.Count > 0` kalır) `_queuedPairs` **temizlenmiyor**. İşlenmiş çiftlerin anahtarları sette kalıyor ve `Request` (`: 38`) o çiftler için yeni istek kabul etmiyor:

  ```csharp
  if (!_queuedPairs.Add(key)) return;
  ```

  Anahtar `GetInstanceID()` tabanlı ve **havuz instance'ları yeniden kullanıyor** — yani aynı iki `Fruit` instance'ı tekrar aynı tier'da karşılaşırsa (havuz döngüsünde bu tamamen normal) birleşme, kuyruk bir kez boşalana kadar reddedilir. `OnCollisionStay2D` her adımda tekrar denediği için genelde bir sonraki karede düzeliyor, ama "kuyruk hiç boşalmazsa" durumu teorik olarak kilitlenebilir.

  Ayrıca `if (_queue.Count > 0) _queue.Clear();` (`: 60`) satırındaki `if` gereksiz — `Clear()` boş kuyrukta da zararsız ve `Count` okuması bedava değil (küçük ama gereksiz).
- **Beklenen etki:** Pratikte 100'lük guard'a ulaşmak çok zor (aynı karede 100 birleşme). Ama düzeltme tek satır ve mantığı kesinleştiriyor.
- **Oynanışa risk:** Yok.
- **Çözüm:** `_queuedPairs` yalnızca "kuyrukta bekleyen" çiftleri temsil etmeli. Her tur sonunda koşulsuz temizlemek en basit ve doğru yol: kuyrukta kalan istekler bir sonraki turda yeniden işlenecek ve o istekler zaten kuyrukta (set yalnızca *yeni* istek eklemeyi filtreliyor).

Önce — `MergeHandler.cs : 58-85`:

```csharp
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying)
        {
            if (_queue.Count > 0) _queue.Clear();

            _queuedPairs.Clear();

            return;
        }

        int guard = 0;
        while (_queue.Count > 0 && guard++ < 100)
        {
            ...
        }

        if (_queue.Count == 0) _queuedPairs.Clear();
```

Sonra:

```csharp
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying)
        {
            _queue.Clear();
            _queuedPairs.Clear();

            return;
        }

        int guard = 0;
        while (_queue.Count > 0 && guard++ < 100)
        {
            ...
        }

        // KOŞULSUZ temizle. Eskiden yalnızca kuyruk boşalınca temizleniyordu; guard 100'e
        // takıldığında işlenmiş çiftlerin anahtarları sette kalıyor ve Request onları
        // reddediyordu. Anahtar GetInstanceID tabanlı, havuz da instance'ları yeniden
        // kullandığı için bu "aynı iki meyve bir daha birleşemiyor" demekti.
        //
        // Kalıntı bırakmamak güvenli: kuyrukta bekleyen istekler zaten kuyrukta, set
        // yalnızca YENİ istek eklemeyi filtreliyor. Aynı çift için ikinci bir istek
        // gelirse LateUpdate'in IsMerging / Definition kontrolleri onu eliyor.
        _queuedPairs.Clear();
```

---

### F-18 Null referans riski taşıyan noktalar

- **Dosya / satır:** `HUDView.cs : 80` · `DropController.cs : 60, 291` · `ScoreSystem.cs : 14, 34` · `WormBoostDirector.cs : 360` · `FaceDirector.cs : 206`
- **Kategori:** Bug
- **Önem:** Düşük
- **Sorun:** Kod tabanı genel olarak null kontrollerinde çok titiz (`if (_config != null)`, `AudioService.Instance != null`, `_dropIndicator != null`…). Aşağıdaki noktalar bu standardın dışında kalmış — hepsi "sahnede bir alan boş kalırsa" senaryosu, yani asıl risk refactor / yeni sahne kurulumu:

  | Yer | Kod | Risk |
  |---|---|---|
  | `HUDView.cs : 80` | `_highScoreText.SetText("{0}", hs)` | `_scoreText` için (`: 77`) null kontrolü var, `_highScoreText` için yok. Rekor olayı açılışta geliyor → açılışta NRE. |
  | `DropController.cs : 60` | `_config.dropY` (`Start`) | Aynı sınıf `_pool`, `_dropIndicator`, `_nextDisplay` için null kontrolü yapıyor; `_config` için hiç yapmıyor. |
  | `DropController.cs : 291` | `_dropIndicator.SetPending(...)` | `: 268` ve `: 101`'de `if (_dropIndicator != null)` var, burada yok — aynı alan iki farklı standartla ele alınıyor. |
  | `ScoreSystem.cs : 14` | `void Awake() => Instance = this;` | Diğer bütün singleton'lar kopya koruması yapıyor (`GameManager`, `FaceDirector`, `AudioService`, `EffectDirector`…). Sahnede kopya kalırsa sessizce ikincisi kazanır. |
  | `ScoreSystem.cs : 34, 39` | `_config.comboWindow`, `_config.comboMultiplierStep` | `_config` null kontrolü yok; her birleşmede NRE. |
  | `WormBoostDirector.cs : 360` | `var fruits = _pool.Active;` (`PlaceCursors`) | `ArmFruitsForShaking` (`: 238`) `if (_pool == null) return;` yapıyor, burası yapmıyor. |
  | `FaceDirector.cs : 206` | `_detector != null ? _detector.LineY : 0f` | Bu **doğru** — karşılaştırma için burada. |
- **Beklenen etki:** Performans yok. Sahne kurulumu hatalarında sessiz çökme yerine anlaşılır uyarı.
- **Oynanışa risk:** Yok.
- **Çözüm:** Kod tabanının kendi desenini uygula. Örnekler:

```csharp
// HUDView.cs : 80
void HandleHighScore(int hs)
{
    if (_highScoreText != null) _highScoreText.SetText("{0}", hs);
}
```

```csharp
// DropController.cs : 56-64
void Start()
{
    if (_camera == null) _camera = Camera.main;

    if (_config == null)
    {
        Debug.LogError("DropController: GameConfig bağlı değil — bırakma yüksekliği " +
                       "okunamıyor, bileşen kapatılıyor.", this);
        enabled = false;
        return;
    }

    transform.position = new Vector3(transform.position.x, _config.dropY, 0f);
}
```

```csharp
// DropController.cs : 291 — aynı dosyadaki diğer iki kullanımla aynı desen
if (_dropIndicator != null)
    _dropIndicator.SetPending(bottomWorldY, _pending.Definition.displayColor);
```

```csharp
// ScoreSystem.cs : 14 — diğer bütün singleton'larla aynı desen
void Awake()
{
    if (Instance != null && Instance != this)
    {
        Debug.LogWarning("ScoreSystem: sahnede ikinci kopya var, bu obje yok ediliyor.", this);
        Destroy(gameObject);
        return;
    }

    Instance = this;
}
```

`ScoreSystem.HandleMerged` için `_config` null kontrolü: `if (_config == null || produced == null) return;` yeterli.

---

### F-19 AudioService `AudioClip` anahtarlı Dictionary kullanıyor

- **Dosya / satır:** `Assets/FruitMerge/Scripts/Services/AudioService.cs : 116, 363-365`
- **Kategori:** GC / mikro-optimizasyon
- **Önem:** Düşük
- **Sorun:**

  ```csharp
  readonly Dictionary<AudioClip, float> _lastPlayTime = new Dictionary<AudioClip, float>(16);
  ...
  if (_lastPlayTime.TryGetValue(clip, out float last) && now - last < guardSeconds) return;
  _lastPlayTime[clip] = now;
  ```

  `AudioClip` bir `UnityEngine.Object` ve `Object` hem `Equals(object)` hem `GetHashCode()`'u **override ediyor**. `EqualityComparer<AudioClip>.Default` bu override'ları çağırıyor, yani her arama managed↔native sınırına yakın bir karşılaştırmaya dönüyor (ve `Equals` içinde "yok edilmiş obje" kontrolü var).

  Sözlük 16 girdiyi geçmiyor (klip sayısı sabit) ve `Play` saniyede en fazla birkaç kez çağrılıyor — bu yüzden **düşük** önemde. Zincirleme birleşmede aynı karede 5-6 çağrı olabiliyor.

  Allocation yok (sözlük yeniden boyutlanmıyor, `float` boxing olmuyor) — yani bu bir GC bulgusu değil, saf CPU mikro-maliyeti.
- **Beklenen etki:** Ölçülebilir sınırın altında; teorik listenin sonuna ait. Tek gerekçe: düzeltme bir satır ve semantiği de doğrultuyor (referans kimliği zaten istenen şey).
- **Oynanışa risk:** Yok.
- **Çözüm:** Referans karşılaştırıcı ver — `UnityEngine.Object.Equals`'ı tamamen atlar.

Önce:

```csharp
    readonly Dictionary<AudioClip, float> _lastPlayTime = new Dictionary<AudioClip, float>(16);
```

Sonra:

```csharp
    /// <summary>
    /// Guard kayıtları. Karşılaştırıcı AÇIKÇA referans kimliği: varsayılan karşılaştırıcı
    /// UnityEngine.Object'in override ettiği Equals/GetHashCode'una gidiyor (yok edilmiş
    /// obje kontrolü dahil). İstediğimiz şey zaten "aynı klip mi", yani referans eşitliği.
    /// </summary>
    readonly Dictionary<AudioClip, float> _lastPlayTime =
        new Dictionary<AudioClip, float>(16, ReferenceEqualityComparer.Instance);
```

> `ReferenceEqualityComparer` .NET 5+ / `System.Collections.Generic` altında; Unity 6'nın .NET Standard 2.1 profilinde mevcut. Erişilemezse üç satırlık kendi `IEqualityComparer<AudioClip>` implementasyonu (`RuntimeHelpers.GetHashCode`) aynı işi yapar.

---

### F-20 Her combo popup'ında `ForceMeshUpdate`

- **Dosya / satır:** `Assets/FruitMerge/Scripts/Gameplay/ComboPopupItem.cs : 79-102`
- **Kategori:** UI
- **Önem:** Düşük
- **Sorun:** `ClampToView` yazının gerçek genişliğini ölçmek için `_text.ForceMeshUpdate()` çağırıyor — TMP mesh'ini o anda, senkron olarak yeniden kuruyor. Bu **bilinçli** bir karar ve gerekçesi de yazılmış (`: 74-78`: tahmini payla "Mouthwatering!" kenardan taşıyordu). Doğru karar.

  Maliyet noktası: zincirleme birleşmede aynı karede birden fazla popup doğabiliyor (`ComboPopupDirector.HandleComboMerge`, her `OnComboMerge` için bir `_pool.Get()` + `Play`). Her biri ayrı bir `ForceMeshUpdate`. Ayrıca `Play` içinde `SetText` **`ForceMeshUpdate`'ten önce** çağrıldığı için mesh iki kez kuruluyor (bir `SetText`'in kendi geç güncellemesi iptal olmuş sayılsa da, `ForceMeshUpdate` tam bir yeniden kurma).
- **Beklenen etki:** Popup başına bir TMP mesh kurulumu (yazı kısa: "x7\nLegendary!"). 5'lik bir zincirde aynı karede 5 kurulum — tipik olarak toplam 0.2-0.5 ms'lik bir sivrilme. Combo anı zaten en yoğun kare (merge sesi + parçacık + haptic + popup), yani sivrilmenin en kötü zamanda geldiği doğru.
- **Oynanışa risk:** Yok.
- **Çözüm:** İki seçenek, ikisi de davranışı korur:

  1. **En basit (önerilen):** ölçümü mevcut haliyle bırak, ama `_hold`/`fontSize`/`color` atamalarının `SetText`'ten **sonra** ve `ForceMeshUpdate`'in tek çağrı olduğundan emin ol — şu an `_text.fontSize` (`: 54`) `SetText`'ten (`: 56`) önce yazılıyor, bu doğru sıra. Yani kod hâlihazırda minimum sayıda kurulum yapıyor: **bu maddede yapısal bir düzeltme yok, sadece profil ederken bu sivrilmenin sebebini bilin.**

  2. Ölçüm yerine **kademe başına önceden hesaplanmış genişlik tablosu** kullanmak mümkün (4 kademe × 4 kelime = 16 sabit), ama bu, kelime listesi değiştiğinde bayatlayan bir önbellek demek — mevcut çözümün tam olarak kaçındığı hata. **Önerilmiyor.**

  Kayda değer tek gerçek iyileştirme: `_pool` maksimum 16 (`ComboPopupDirector : 23`); aynı karede 16'dan fazla popup istenirse `ObjectPool` yeni nesne `Instantiate` eder. `comboPopupMinCombo = 2` ve combo penceresi 1.2 sn olduğu için pratikte imkânsız — sorun yok, sadece bilin.

---

### F-21 Kapalı panellerde de `OnTick` + `TickPunch` dönüyor

- **Dosya / satır:** `UIPanel.cs : 67-83` · `GameOverPanel.cs : 165-199, 248-275`
- **Kategori:** Update maliyeti
- **Önem:** Düşük
- **Sorun:** `UIPanel.Update` koşulsuz olarak `OnTick(Time.unscaledDeltaTime)` çağırıyor. Sahnede 5 panel var (`SplashPanel` açılıştan sonra `SetActive(false)` ile çıkıyor, geriye 4 kalıyor), yani oynanış sırasında kare başına 4 `Update` + 4 `OnTick`.

  `GameOverPanel.OnTick` (`: 165-199`) her karede `TickPunch(dt)` çağırıyor, o da 3 yıldızlık döngüyü dönüyor — panel kapalıyken de. Döngü `if (_punch[i] <= 0f) continue;` ile hemen çıkıyor, yani gerçek iş yok; maliyet çağrı zincirinin kendisi.

  `UIPanel.Update`'in mimari gerekçesi sağlam (`: 68-70`: alt sınıflar kendi `Update`'ini tanımlayınca base'in fade'i sessizce durur) — bu bulgu o tasarımı değiştirmeyi değil, boşta çalışan kısmı kısaltmayı öneriyor.
- **Beklenen etki:** Kare başına ~8 managed çağrı. Ölçülebilir sınırın altında; listenin sonuna ait. F-02 uygulanırsa panellerin görsel maliyeti çözülmüş olacağı için bu madde büyük ölçüde akademik kalıyor.
- **Oynanışa risk:** Yok.
- **Çözüm:** `GameOverPanel`'de en dıştaki kapıyı öne al:

Önce — `GameOverPanel.cs : 165-173`:

```csharp
    protected override void OnTick(float dt)
    {
        TickPunch(dt);

        if (!_revealing) return;
```

Sonra:

```csharp
    protected override void OnTick(float dt)
    {
        // Panel kapalıyken yapacak iş yok: yıldız punch'ı da gösterim sayacı da yalnızca
        // panel açıkken anlamlı. Oyunun %95'i bu satırda bitiyor.
        if (!IsOpen && !_revealing) return;

        TickPunch(dt);

        if (!_revealing) return;
```

`UIPanel.Update`'in kendisine dokunulmamalı — `_animating` kontrolü zaten fade bittiğinde çıkıyor ve `OnTick`'in koşulsuz olması `SplashPanel`'in yükleme döngüsü için **gerekli** (`SplashPanel.OnTick`, `: 57-88`, panel açıkken çalışıyor ama `Show()` çağrısından önce de bir kare geçebiliyor).

---

### F-22 `Worm.ApplySegment` değişmeyen ölçeği her karede yazıyor

- **Dosya / satır:** `Assets/FruitMerge/Scripts/Gameplay/Worm.cs : 333-357`
- **Kategori:** Update maliyeti
- **Önem:** Düşük
- **Sorun:**

  ```csharp
  float taper = 1f - 0.05f * i;

  t.localScale = new Vector3(_diameter * taper, _diameter * taper, 1f);
  ```

  `_diameter` `Configure`'da bir kez hesaplanıyor (`: 126-128`) ve kurdun ömrü boyunca sabit; `taper` de yalnızca halka indeksine bağlı. Yani bu satır **her karede aynı değeri** yazıyor. `localPosition` ve `localRotation` gerçekten değişiyor (yol fonksiyonu), onlara dokunulmamalı.

  6 kurt × 5 halka = 30 transform, kare başına 30 gereksiz `localScale` yazımı — ve bu yalnızca boost oynarken (~5.5 sn) geçerli.

  Aynı dosya `sr.flipX` için doğru deseni uygulamış (`: 350`: `if (sr.flipX != flip) sr.flipX = flip;`) ve `ApplyHeadSprite` de öyle (`: 397`, "kural 9: sadece değişince ata"). `localScale` bu titizliğin dışında kalmış.
- **Beklenen etki:** Boost süresince kare başına 30 transform yazımı. Küçük ve süreli; teorik listenin üstünde, gerçek darboğazın altında.
- **Oynanışa risk:** Yok.
- **Çözüm:** Ölçeği `Configure`'da bir kez yaz.

Önce — `Worm.cs : 333-342`:

```csharp
    void ApplySegment(int i, Vector2 pos, Vector2 forward, int n)
    {
        Transform t = _segments[i];

        t.localPosition = pos;

        // kuyruğa doğru hafif incelme — zincire hacim veriyor
        float taper = 1f - 0.05f * i;

        t.localScale = new Vector3(_diameter * taper, _diameter * taper, 1f);
```

Sonra — `Configure`'un sonuna (`: 171` civarı, `ApplyHeadSprite` çağrısından önce):

```csharp
        // Halka ölçeği kurdun ömrü boyunca SABİT (_diameter Configure'da hesaplandı,
        // taper yalnızca indekse bağlı) — her karede yazmanın karşılığı yok (kural 9).
        for (int i = 0; i < _segments.Length; i++)
        {
            float taper = 1f - 0.05f * i;

            _segments[i].localScale = new Vector3(_diameter * taper, _diameter * taper, 1f);
        }

        ApplyHeadSprite(_spHeadIdle);
```

ve `ApplySegment` sadeleşiyor:

```csharp
    void ApplySegment(int i, Vector2 pos, Vector2 forward, int n)
    {
        Transform t = _segments[i];

        t.localPosition = pos;

        // localScale burada YAZILMIYOR: Configure'da bir kez ayarlandı.

        if (forward.sqrMagnitude < 1e-6f) return;
        ...
```

---

### F-23 HapticService editör günlüğü deprem boyunca saniyede ~14 string üretiyor

- **Dosya / satır:** `Assets/FruitMerge/Scripts/Services/HapticService.cs : 554-560`
- **Kategori:** GC (yalnızca editör)
- **Önem:** Düşük
- **Sorun:**

  ```csharp
  #if UNITY_EDITOR
      if (_config != null && _config.hapticEditorLog)
          Debug.Log($"[Haptic] {reason ?? "pulse"} · şiddet {intensity01:0.00} · " +
                    $"{Mathf.RoundToInt(duration * 1000f)} ms");
  #endif
  ```

  `#if UNITY_EDITOR` doğru kullanılmış — **cihaz derlemesinde bu satır hiç yok**, yani oyuncuya maliyeti sıfır. Bulgu tamamen editörde profil almakla ilgili:

  - `hapticQuakePulseInterval = 0.07f` → deprem treni saniyede ~14 kez `Fire` çağırıyor,
  - `hapticChewInterval = 0.11f` → kemirme treni saniyede ~9 kez,
  - her çağrı iki string interpolation + `Debug.Log` (stack trace toplama dahil, ki `Debug.Log`'un editördeki asıl maliyeti bu).

  `GameConfig.hapticEditorLog` varsayılanı **`true`** (`: 711`). Yani Editor'de her deprem/boost, Profiler'ın GC Alloc grafiğinde gerçek olmayan bir sivrilme üretiyor ve konsolu dolduruyor.
- **Beklenen etki:** Cihazda 0. Editörde deprem sırasında saniyede ~14 log; `Debug.Log` editörde çağrı başına 1-10 µs + string allocation. **Yanlış teşhis riski:** boost'u profillerken bu allocation'ı oyun kodunun sanmak.
- **Oynanışa risk:** Yok.
- **Çözüm:** İki adım:

  1. `GameConfig.hapticEditorLog` varsayılanını **`false`** yap; kanca doğrulaması gerektiğinde elle açılır. Tooltip zaten "Editör'de her titreşim isteğini konsola yaz" diyor, varsayılan olarak kapalı olması bu amaca aykırı değil.
  2. Süreklilik trenlerini (deprem, kemirme) günlükten muaf tut — tek darbeler ve diziler zaten doğrulanması gereken şeyler; tren saniyede 14 kez aynı satırı basıyor.

Önce — `GameConfig.cs : 707-711`:

```csharp
    [Tooltip("Editör'de her titreşim isteğini konsola yaz.\n\n" + ...)]
    public bool hapticEditorLog = true;
```

Sonra:

```csharp
    [Tooltip("Editör'de her titreşim isteğini konsola yaz.\n\n" +
             "Masaüstünde motor YOK — Editör'de titreşimi hissedemezsin. Kancaların doğru " +
             "yerde ve doğru şiddette tetiklendiğini ancak böyle görebilirsin. Cihaz " +
             "derlemesinde bu satır hiç derlenmiyor.\n\n" +
             "⚠️ Varsayılan KAPALI: deprem ve kemirme trenleri saniyede 14-9 darbe " +
             "üretiyor, açık bırakılınca Profiler'ın GC Alloc grafiğinde oyun kodunun " +
             "üretmediği bir sivrilme görünüyor ve konsol doluyor")]
    public bool hapticEditorLog = false;
```

Önce — `HapticService.cs : 554-560`:

```csharp
#if UNITY_EDITOR
        if (_config != null && _config.hapticEditorLog)
            Debug.Log($"[Haptic] {reason ?? "pulse"} · şiddet {intensity01:0.00} · " +
                      $"{Mathf.RoundToInt(duration * 1000f)} ms");
#endif
```

Sonra:

```csharp
#if UNITY_EDITOR
        // Süreklilik trenleri günlüğe girmiyor: saniyede 14 (deprem) / 9 (kemirme) kez
        // aynı satırı basıyorlar ve doğrulanacak bir şey söylemiyorlar. Tek darbeler ve
        // diziler — yani kancanın doğru yerde tetiklendiğini gösteren istekler — kalıyor.
        if (_config != null && _config.hapticEditorLog && reason != "quake" && reason != "chew")
            Debug.Log($"[Haptic] {reason ?? "pulse"} · şiddet {intensity01:0.00} · " +
                      $"{Mathf.RoundToInt(duration * 1000f)} ms");
#endif
```

---

### F-24 Atlas'a paketlenen kaynak dokular sıkıştırılmış — çifte kayıp

- **Dosya / satır:** `Assets/FruitMerge/Art/UI/**` ve `Assets/FruitMerge/Art/Fruits/**` altındaki
  doku `.meta`'ları (ör. `Art/Fruits/Base/fruit_00_cherry.png.meta` — platform override'larında
  `textureCompression: 1`)
- **Kategori:** Render / doku kalitesi
- **Önem:** Orta
- **Durum:** ⚠️ **Uygulanmadı** — bkz. aşağıdaki gerekçe.
- **Sorun:** [F-04](#f-04-sprite-atlasları-sıkıştırılmamış-rgba32)'ü uygulayıp atlas'ları
  yeniden import ettirdiğimde Unity konsolu ~40 uyarı bastı (sadece ilk sayfası; muhtemelen
  daha fazlası var):

  ```
  Source Texture (Assets/FruitMerge/Art/UI/Buttons/btn_square_orange_normal.png)
  of Sprite (btn_square_orange_normal_0) is using compressed format.
  To ensure no loss in source pixel details when packing to SpriteAtlas,
  please use uncompressed format in TextureImporter.
  ```

  Doğru boru hattı **kaynak sıkıştırılmamış → atlas sıkıştırılmış**. Şu an tersi kurulu:
  kaynak PNG'ler sıkıştırılmış import ediliyor, atlas onları açıp yeniden paketliyor. Yani
  atlas'a giren pikseller **zaten bir kez kayıp yaşamış**.

  Bu uyarı benim değişikliğimden ÖNCE de geçerliydi (atlas her paketlendiğinde aynı kayıp
  oluyordu); atlas o zaman sıkıştırılmamış olduğu için toplam kayıp bir kademeydi. F-04 ile
  ASTC 4×4 eklenince **iki kademe** oluyor: kaynak sıkıştırma + atlas sıkıştırma. ASTC 4×4
  tek başına pratikte kayıpsız ama çifte sıkıştırma özellikle keskin kenarlarda ve alfa
  geçişlerinde görünür hâle gelebilir.
- **Beklenen etki:** Çalışma anı performansına etkisi **yok** — kaynak dokular atlas'a
  paketlendiği için derlemeye hiç girmiyorlar, yalnızca atlas'ın girdi kalitesini
  belirliyorlar. Kazanç tamamen **görsel kalite**: F-04'ün getirdiği tek kademeli kaybı
  olduğu gibi bırakıp ikinci kademeyi ortadan kaldırıyor.
- **Oynanışa risk:** Yok.
- **Neden kendiliğinden uygulanmadı:** ~100 doku asset'inin import ayarını değiştirmek
  demek — uzun bir yeniden import, büyük bir git diff'i ve Editor tarafında artan
  `Library` boyutu. İnceleme kapsamındaki 23 bulgunun dışında, sen istemeden yapılmasını
  doğru bulmadığım büyüklükte bir sweep. Karar sende.
- **Çözüm:** Atlas'a paketlenen dokuların **Default** platform ayarını `Uncompressed`'e
  çek. `SceneFixups`'a şu deseni ekleyebilirim (tek menü tıkı, fikirsiz):

```csharp
    /// <summary>
    /// Atlas'a paketlenen kaynak dokuları SIKIŞTIRILMAMIŞ'a çeker.
    ///
    /// Doğru boru hattı: kaynak sıkıştırılmamış → atlas sıkıştırılmış (ASTC). Kaynak da
    /// sıkıştırılmışsa atlas çifte sıkıştırmadan besleniyor ve keskin kenarlarda kayıp
    /// birikiyor — Unity bunu her paketlemede uyarı olarak basıyor.
    ///
    /// Derleme boyutuna etkisi YOK: atlas'a paketlenen kaynak dokular derlemeye girmiyor,
    /// yalnızca atlas'ın girdi kalitesini belirliyorlar.
    /// </summary>
    static readonly string[] PackedTextureFolders =
    {
        "Assets/FruitMerge/Art/Fruits",
        "Assets/FruitMerge/Art/UI"
    };

    static int FixPackedSourceTextures()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", PackedTextureFolders);

        int changed = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null) continue;

            TextureImporterPlatformSettings def = importer.GetDefaultPlatformTextureSettings();

            if (def.textureCompression == TextureImporterCompression.Uncompressed) continue;

            def.textureCompression = TextureImporterCompression.Uncompressed;

            importer.SetPlatformTextureSettings(def);
            importer.SaveAndReimport();

            changed++;
        }

        if (changed > 0)
            Debug.Log($"SceneFixups: {changed} kaynak doku Uncompressed'e çekildi — atlas " +
                      "artık çifte sıkıştırmadan beslenmiyor.");

        return changed;
    }
```

> **Uygulamak istersen söyle**, `SceneFixups`'a ekleyip çalıştırırım. Uygulamazsan da F-04
> geçerli kalıyor: bellek kazancı aynı, sadece kalite teorik olarak bir kademe daha düşük.

---

### F-25 Pause boost'u iptal ediyor — kullanım boşa gidiyor

- **Dosya / satır:** `WormBoostDirector.cs : 261-265` ve `QuakeBoostDirector.cs : 148-152`
  (ikisinde de `HandleStateChanged`)
- **Kategori:** Bug (oynanış)
- **Önem:** Yüksek
- **Durum:** ✅ Düzeltildi.
- **Sorun:** İki director de aynı satırı taşıyordu:

  ```csharp
  void HandleStateChanged(GameState s)
  {
      // Pause / menü / oyun sonu: yarım kalmış bir boost ekranda asılı kalmasın
      if (s != GameState.Playing) Abort();
  }
  ```

  `GameState.Paused` de bu koşula giriyor, yani **pause boost'u iptal ediyor.** Kullanım
  ise iptalden ÖNCE harcanmış oluyor (`BeginBoost` / `Begin` içinde `_charges--`), dolayısıyla:

  - Kurtlar meyveyi yerken pause'a basmak → kurtlar gidiyor, meyve kurtuluyor, **kullanım
    boşa.** Oyuncu tek kullanımlık bir kurtarma aracını hiçbir şey karşılığında kaybediyor.
  - Depremin ortasında pause'a basmak → sarsıntı kesiliyor, yığın olduğu gibi kalıyor,
    **kullanım boşa.**

  Doğru davranış: pause **dondurur**, Continue kaldığı yerden devam ettirir. Menü ve oyun
  sonu ise gerçekten iptal (orada boost'un bir anlamı kalmıyor).

  Bu bulgu ilk incelemede kaçtı: [F-01](#f-01-boost-yarıda-kesilirse-meyve-küçülmüş-halde-kalıyor)'de
  "pause → `Abort`" zincirini doğru tespit etmiş ama `Abort`'un o durumda **yapılmaması
  gerektiğini** sorgulamak yerine yalnızca `Abort`'un eksik temizliğini (ölçek geri alma)
  düzeltmiştim. Kullanıcı test ederken buldu.
- **Beklenen etki:** Performans yok — oynanış/denge hatası. Tek kullanımlık boost'larda
  (`wormsChargesPerRun = 1`, `quakeChargesPerRun = 1`) doğrudan oyuncu kaybı.
- **Oynanışa risk:** Düzeltmenin riski **Yok** — kullanıcının açıkça istediği davranış.
- **Çözüm:** Üç ayrı durum ayrıldı: **Paused → dondur**, **Playing → sürdür**,
  **diğerleri → iptal**.

  Dondurma işinin büyük kısmını `Time.timeScale = 0` zaten yapıyor (`GameManager`'ın
  `EnterState(Paused)`'ı): bütün faz sayaçları `Time.deltaTime` ile ilerliyor, `FixedUpdate`
  hiç çağrılmıyor (deprem itmeleri durur), parçacıklar donuyor, `Time.time` tabanlı
  uyuklama pencereleri de donuyor. Kodda ele alınması gereken üç şey kaldı — hepsi
  `timeScale`'e **bağlı olmayan** kanallar:

  | Kanal | Neden özel | Ne yapıldı |
  |---|---|---|
  | **Girdi** | `PointerInput` ham `Input`'tan okuyor | `Update` pause'da erken çıkıyor — yoksa `TryReadTap` pause panelindeki dokunuşu hedef seçimi sanıyordu |
  | **Ses** | `AudioSource`'lar `ignoreListenerPause = true` ile kurulu | `AudioService.PauseQuakeRumble()` / `ResumeQuakeRumble()` — `Stop` değil `Pause`, klip baştan başlamasın |
  | **Titreşim** | `HapticService` `unscaledDeltaTime` ile dönüyor | Pause'da susturmayı o servis kendi `OnStateChanged`'inde zaten yapıyordu; eksik olan **sürdürme**: `HapticService.ResumeQuake()` eklendi, kemirme treni için `RaiseWormsChewingChanged(true)` yeniden yayınlanıyor |

  Ayrıca `FaceDirector.HandleStateChanged(Playing)` yeni oyun için **her şeyi sıfırlıyor**
  (`_boostFocus = null`, `_quakeMood = false`) ve bu pause'dan dönüşte de çalışıyor —
  o yüzden iki director dönüşte yüz durumunu geri yazıyor.

Önce — her iki director'de:

```csharp
    void HandleStateChanged(GameState s)
    {
        // Pause / menü / oyun sonu: yarım kalmış bir boost ekranda asılı kalmasın
        if (s != GameState.Playing) Abort();
    }
```

Sonra — `WormBoostDirector`:

```csharp
    void HandleStateChanged(GameState s)
    {
        // PAUSE = DONDUR, iptal etme. Eskiden burada Abort() çağrılıyordu: oyuncu kurtlar
        // meyveyi yerken pause'a bastığı an kurtlar gidiyor, meyve kurtuluyor ve kullanım
        // boşa gidiyordu (kullanım BeginBoost'ta zaten harcanmış oluyor).
        //
        // Dondurmayı timeScale = 0 hallediyor. Update aşağıda pause'da erken çıkıyor —
        // asıl sebebi zaman değil GİRDİ: TryReadTap pause panelindeki dokunuşu hedef
        // seçimi sanardı.
        if (s == GameState.Paused) return;

        if (s == GameState.Playing) { ResumeFromPause(); return; }

        // Menü / oyun sonu: burada gerçekten iptal.
        Abort();
    }

    void ResumeFromPause()
    {
        if (_state == State.Idle) return;

        // Continue'ye basan dokunuşun bırakılması hedef seçimi sayılmasın.
        if (_state == State.Armed) _gestureBlocked = true;

        // FaceDirector OnStateChanged(Playing)'de her şeyi sıfırlıyor — geri yaz.
        if (FaceDirector.Instance != null)
        {
            FaceDirector.Instance.SetBoostFocus(
                _target != null && _target.gameObject.activeSelf ? _target.transform : null);

            FaceDirector.Instance.SuppressSleepFor(_config.wormApproachDuration +
                                                   _config.wormEatDuration +
                                                   _config.wormLeaveDuration);
        }

        // Kemirme titreşimini HapticService pause'da susturdu; yeme sürüyorsa yeniden başlat.
        if (_state == State.Eat && !_fruitVanished) GameEvents.RaiseWormsChewingChanged(true);
    }
```

`QuakeBoostDirector` aynı yapıda, farkı gürültü sesi:

```csharp
        if (s == GameState.Paused)
        {
            if (_state == State.Idle) return;

            // Ses timeScale'e bağlı DEĞİL ve kanallar ignoreListenerPause ile kurulu —
            // pause'da gürültü sabit sesle uğuldamaya devam ediyordu.
            if (AudioService.Instance != null) AudioService.Instance.PauseQuakeRumble();

            return;
        }
```

```csharp
    void ResumeFromPause()
    {
        if (_state == State.Idle) return;

        if (AudioService.Instance != null) AudioService.Instance.ResumeQuakeRumble();

        if (HapticService.Instance != null) HapticService.Instance.ResumeQuake();

        if (FaceDirector.Instance != null)
        {
            FaceDirector.Instance.SetQuakeMood(true);
            FaceDirector.Instance.SuppressSleepFor(TotalDuration);
        }
    }
```

> **F-01 hâlâ gerekli:** `Abort` artık pause'da çağrılmıyor ama menü yolunda hâlâ çalışıyor
> (pause → MENU). O yolda küçülmüş meyveyi `RestoreScale()` toparlıyor.

---

## 4. Öncelik sıralı aksiyon planı

Sıra **etki / risk** oranına göre. Her adımdan sonra Profiler'da ölçün, sonra bir sonrakine geçin — toplu değiştirirseniz neyin işe yaradığını göremezsiniz.

| # | Bulgu | Tahmini kazanç | Zorluk | Not |
|---|---|---|---|---|
| 1 | **[F-01](#f-01-boost-yarıda-kesilirse-meyve-küçülmüş-halde-kalıyor)** meyve küçük kalıyor | Performans yok — **oynanış hatası** | Kolay | Tek `if` bloğu + 8 satırlık `RestoreScale`. Sıranın başında çünkü hata, optimizasyon değil. |
| 2 | **[F-04](#f-04-sprite-atlasları-sıkıştırılmamış-rgba32)** atlas sıkıştırma | ~49 MB → ~12 MB doku belleği; aynı oranda doku bant genişliği | Kolay | Sadece iki asset'te Android/iOS override. Ölçümü Memory Profiler'da anında görünüyor. |
| 3 | **[F-02](#f-02-kapanan-paneller-tuvalden-çıkmıyor--tam-ekran-şeffaf-overdraw)** panelleri tuvalden çıkar | 4 tam ekran blend + ~48 graphic; mobilde tipik 1-3 ms GPU | Orta | 4 panele `Canvas` ekleme + `UIPanel`'de 5 satır. Frame Debugger ile doğrula. |
| 4 | **[F-03](#f-03-skor-yazısı-ile-11-slotluk-evrim-zinciri-aynı-alt-canvasta)** skoru ayrı alt canvas'a | Rebuild edilen eleman 34 → 1; `Canvas.BuildBatch` marker'ında 0.3-1.0 ms | Kolay | Kod değişikliği yok, sadece sahne. Anchor'ları doğrula. |
| 5 | **[F-06](#f-06-fruitfixedupdate-uyuyan-gövdelerde-de-boşa-dönüyor)** uyuyan meyvede erken çıkış | Saniyede ~6000 native property erişimi | Kolay | İki satır. F-05'ten bağımsız uygulanabilir ve kazancın büyük kısmını o veriyor. |
| 6 | **[F-07](#f-07-oncollisionstay2d-içinde-temas-başına-tekrarlanan-native-okuma)** rearm'ı adım başına bir kez | ~18.000 native erişim/sn | Kolay | İki alan + bir guard. Fizik adımı sayacını `Time.frameCount` ile karıştırmayın. |
| 7 | **[F-08](#f-08-fruitfaceticklook-hedef-yokken-de-her-karede-transform-yazıyor)** bakış snap eşiği | Kare başına ~60 transform yazımı + bounds işi | Kolay | `FaceDirector.Update` marker'ında ölçülür. |
| 8 | **[F-10](#f-10-açılışta-250-gameobject-awakestartta-yaratılıyor-prewarmqueue-atlanmış)** ısıtmayı PrewarmQueue'ya taşı | İlk kareye kadar 150-500 ms | Orta | Konfeti (140) tek başına en büyük payı. `splashPrewarmPerFrame`'i de birlikte ayarla. |
| 9 | **[F-11](#f-11-physics2d-job-multithreading-kapalı)** Physics2D multithreading | **Ölç** — 60 gövdede 0 ile belirgin arası | Kolay | `useConsistencySorting` ile birlikte aç. Cihazda his kontrolü şart. |
| 10 | **[F-05](#f-05-fruit-kendi-update-ve-fixedupdatesini-taşıyor-kural-7-ihlali)** Fruit tick'ini merkezileştir | Kare başına ~10-20 µs + fizik adımı başına aynı | Orta | F-06 uygulandıysa kazancın kalanı; execution order'a dikkat (`FruitTicker` = 0). |
| 11 | **[F-12](#f-12-placecursors-sabit-değeri-meyve-başına-her-karede-yeniden-hesaplıyor)** nişangâh birimi önbelleği | Silahlı fazda kare başına ~88 native erişim | Kolay | |
| 12 | **[F-09](#f-09-computefillratio-önbelleklenmiş-floory-yerine-collider-boundsu-okuyor)** + **[F-14](#f-14-dropindicatorcontrollerupdate-her-karede-gereksiz-native-iş-yapıyor)** bounds önbellekleri | Küçük; F-14 ayrıca bir çökme riskini kapatıyor | Kolay | İkisi bir oturumda. |
| 13 | **[F-13](#f-13-ölü-ayar-alanları--inspectordan-çevirince-hiçbir-şey-olmuyor)** ölü ayar alanları | Performans yok — **yanlış ayar riski** | Kolay | `countForGameOver` ve `newRecordDelay` için ürün kararı gerekiyor. |
| 14 | **[F-16](#f-16-daldaki-bırakılmamış-meyve-de-coin-ödülüne-sayılıyor)** + **[F-17](#f-17-mergehandler-kuyruk-guardına-takılırsa-_queuedpairs-kalıntı-bırakıyor)** + **[F-18](#f-18-null-referans-riski-taşıyan-noktalar)** doğruluk | Performans yok — latent hatalar | Kolay | Hepsi tek satırlık. |
| 15 | **[F-15](#f-15-gereksiz-raycast-target-işaretli-ui-elemanları)** + **[F-21](#f-21-kapalı-panellerde-de-ontick--tickpunch-dönüyor)** + **[F-22](#f-22-wormapplysegment-değişmeyen-ölçeği-her-karede-yazıyor)** + **[F-19](#f-19-audioservice-audioclip-anahtarlı-dictionary-kullanıyor)** + **[F-20](#f-20-her-combo-popupında-forcemeshupdate)** | Ölçüm sınırının altında | Kolay | Teorik kalemler; ilk 14 bitmeden buraya gelmeyin. |
| 16 | **[F-23](#f-23-hapticservice-editör-günlüğü-deprem-boyunca-saniyede-14-string-üretiyor)** editör günlüğü | Cihazda 0 — **profil ölçümünü temizler** | Kolay | Aslında **9. adımdan önce** yapın: profil almaya başlamadan önce gürültüyü kesin. |

---

## 5. Hızlı kazanımlar (quick wins)

10 dakikada uygulanabilen, riski **sıfır** olanlar:

1. **[F-23](#f-23-hapticservice-editör-günlüğü-deprem-boyunca-saniyede-14-string-üretiyor)** — `hapticEditorLog` varsayılanını `false` yap. *Profil almaya başlamadan önce bunu yapın*, yoksa deprem ölçümlerinde oyun kodunun üretmediği GC Alloc göreceksiniz.
2. **[F-06](#f-06-fruitfixedupdate-uyuyan-gövdelerde-de-boşa-dönüyor)** — `Fruit.FixedUpdate` başına iki satır (`!_rb.simulated` + `!_rb.IsAwake()`). *(Bu, F-05 uygulanmadan da tek başına geçerli.)*
3. **[F-09](#f-09-computefillratio-önbelleklenmiş-floory-yerine-collider-boundsu-okuyor)** — `ComputeFillRatio` içindeki bir satırı `FloorY` yap.
4. **[F-12](#f-12-placecursors-sabit-değeri-meyve-başına-her-karede-yeniden-hesaplıyor)** — nişangâh birimini `BuildCursors`'ta bir kez hesapla.
5. **[F-16](#f-16-daldaki-bırakılmamış-meyve-de-coin-ödülüne-sayılıyor)** — `FruitCoinTotal`'a `if (!fruit.IsDropped) continue;`.
6. **[F-17](#f-17-mergehandler-kuyruk-guardına-takılırsa-_queuedpairs-kalıntı-bırakıyor)** — `_queuedPairs.Clear()`'ı koşulsuz yap.
7. **[F-18](#f-18-null-referans-riski-taşıyan-noktalar)** — `HUDView.HandleHighScore`, `DropController.PreparePending`, `ScoreSystem.Awake` null/kopya kontrolleri.
8. **[F-15](#f-15-gereksiz-raycast-target-işaretli-ui-elemanları)** — 11 elemanda `Raycast Target` kapat (`HudPanel`'e **dokunma**).
9. **[F-03](#f-03-skor-yazısı-ile-11-slotluk-evrim-zinciri-aynı-alt-canvasta)** — `ScoreGroup` + `Canvas` ekle, `ScoreText`'i altına taşı. Kod değişikliği yok ve listedeki en yüksek kazanç/emek oranı.
10. **[F-04](#f-04-sprite-atlasları-sıkıştırılmamış-rgba32)** — iki atlas asset'ine Android/iOS ASTC 4×4 override. Tek Inspector işi, en büyük bellek kazancı.

---

## 6. Önerilmeyen optimizasyonlar

Bunlar performans kazandırır ama oyunun hissini, dengesini veya görselliğini bozar. Bilerek elendiler.

### ❌ `Time.fixedDeltaTime`'ı 0.02'den 0.033'e çıkarmak (50 Hz → 30 Hz fizik)
**Kazanç:** Physics2D maliyeti neredeyse yarıya iner — listedeki en büyük tek kazanç.
**Neden elendi:** Bu oyunun tamamı temas hassasiyeti üstüne kurulu. Üç ayrı yerde kırılır:
- `Fruit.TryRequestMerge` `OnCollisionEnter2D`/`Stay2D`'ye bağlı; adım süresi uzayınca hızlı meyvenin ince duvarları (0.3 birim) tünelleme olasılığı artıyor. `continuousRearmSpeed` mekanizması bunu 50 Hz varsayımıyla kalibre edilmiş.
- `mergeRetriggerGuard = 0.012` **açıkça** fizik adımına göre ayarlanmış (`GameConfig.cs : 177-183`: "halkalar arası mesafe fizik adımı yüzünden sadece ~0.017-0.04 sn"). Adım süresi değişince combo zincirinin sesi susar.
- Düşen meyvenin hareketi 30 Hz'de gözle görülür şekilde daha az akıcı olur (interpolation yumuşatır ama çarpışma anı kabalaşır).

### ❌ `velocityIterations` 8 → 4 / `positionIterations` 3 → 2
**Kazanç:** Çözücü maliyetinde doğrudan ~%40.
**Neden elendi:** Suika tipi oyunda sıkışık yığın çözücünün en zor senaryosu. İterasyon düşürmek iç içe geçme (overlap), yığının "yumuşak/lastikli" hissetmesi ve yerleşme süresinin uzaması demek. `GameConfig.quakeMaxRiseSpeed`'in tooltip'i (`: 506-514`) çözücünün zorlanmasının nasıl iç içe geçmeye yol açtığını zaten yaşanmış bir hata olarak anlatıyor — iterasyon düşürmek tam o kapıyı açar. Yığının oturması oyunun temel his öğesi.

### ❌ `m_TimeToSleep`'i 0.5'ten düşürmek
**Kazanç:** Meyveler daha erken uyur, F-06'nın kazancı büyür.
**Neden elendi:** Erken uyuma, henüz tam oturmamış yığının donması demek. Daha kötüsü: `GameOverDetector.HasViolation` "durgun + çizginin üstünde" meyveyi ihlal sayıyor (`: 135-137`); uyuma eşiği düşerse meyveler daha çabuk "durgun" sayılır ve oyun **haksız yere** bitmeye başlar. Denge değişkeni.

### ❌ Nişangâh/parçacık/konfeti sayılarını kesmek
**Kazanç:** `confettiPoolSize` 140 → 60, `confettiRainCount` 110 → 50, `quakeDustRate` 40 → 20 vb.
**Neden elendi:** Bunların her biri **açıkça belgelenmiş görsel kararlar** ve gerekçeleri de yaşanmış hatalara dayanıyor: `confettiPoolSize`'ın tooltip'i (`: 838-843`) "küçük havuzda yağmurun kuyruğu hiç doğmadan sessizce düşüyordu" diyor; `quakeDustWallShare` (`: 584-588`) "sadece zeminden çıkınca deprem hissi olmuyor" diyor. Görsel yoğunluğu kesmek prompt'un açık kısıtı ve zaten bu değerler ölçülerek bulunmuş.

### ❌ Meyve/yüz sprite'larını daha küçük çözünürlüğe indirmek
**Kazanç:** F-04'ün ötesinde ek bellek.
**Neden elendi:** `FruitFace.Bind` (`: 74-91`) yüzü gövdenin tuval genişliğine normalize ediyor ve `FaceSize` (sm/md/lg/xl) sistemi tam olarak "render boyutuyla çözünürlüğü eşleştirmek" için var. Çözünürlük düşürmek büyük meyvelerde (karpuz, hindistan cevizi) gözle görülür bulanıklık demek. Sistem zaten doğru boyutu seçiyor.

### ⚠️ ASTC **6×6** (4×4 yerine) — sınırda
**Kazanç:** F-04'ün üstüne bir ~2.25 kat daha (12 MB → 5 MB).
**Neden ayrı tutuldu:** 6×6, piksel başına 3.56 bit — keskin kenarlı, düz renk alanlı çizim tarzı sprite'larda (bu oyunun tarzı) blok artefaktı **görünebilir**, özellikle yüzlerin ince çizgilerinde ve nişangâhın halkalarında. Önce 4×4 ile git; bellek hâlâ sıkıntılıysa 6×6'yı **yalnızca `UIAtlas`** için dene (UI elemanları daha büyük ve daha az ince detaylı) ve cihazda gözle karşılaştır. Meyve/yüz atlası 4×4'te kalsın.

### ⚠️ URP `Supports HDR` kapatmak / Renderer2D `Use Depth/Stencil Buffer` kapatmak — ölçmeden yapmayın
`UniversalRP.asset : 26` → `m_SupportsHDR: 1`, kamera `m_HDR: 1`, ama `m_RenderPostProcessing: 0` ve `Renderer2D.asset : 26` → `m_RendererFeatures: []`. Sahnede **hiç `Light2D` ve `SpriteMask` yok** (grep ile doğruladım). Bu kombinasyonda URP muhtemelen doğrudan backbuffer'a çiziyor, yani HDR bayrağının pratikte hiçbir maliyeti olmayabilir. `m_UseDepthStencilBuffer: 1` (`Renderer2D.asset : 46`) ise bir depth buffer ayırıp temizliyor olabilir — 2D sprite sıralaması `sortingOrder` ile yapıldığı için işlevsel olarak gereksiz, ama `SpriteMask` eklendiği gün sessizce bozar.

**Bu yüzden bu ikisini bulgu listesine koymadım:** kazançları ölçülmeden bilinemez ve ikisi de ileride bir özellik eklendiğinde geri tepebilir. [Profiling rehberinde](#7-profiling-rehberi) nasıl doğrulanacağı yazıyor; kazanç çıkarsa uygulayın, çıkmazsa dokunmayın.

---

## 7. Profiling rehberi

### Ölçüm düzeni

1. **Cihazda ölç, editörde değil.** Editörde `Fruit.FixedUpdate` maliyeti gerçeğin kat kat üstünde görünür (domain reload, editör overhead). Android'de `adb` üzerinden Profiler'a bağlanın, **Development Build** + **Autoconnect Profiler** ile.
2. **Ölçüm senaryosunu sabitleyin.** Bu oyun için üç ayrı senaryo gerekiyor, çünkü darboğaz her birinde farklı:
   - **A — dolu tahta:** 55-60 meyve, yığın oturmuş, oyuncu bekliyor. Fizik + `FaceDirector` + `Fruit` tick'lerinin baskın olduğu durum.
   - **B — combo zinciri:** 5+ halkalı zincir. `MergeHandler.LateUpdate`, `EffectDirector.Emit`, `ComboPopupItem`, `AudioService`, `HapticService` aynı karede.
   - **C — deprem boost'u:** 60 meyveye 0.06 sn'de bir itme + toz + moloz + kamera + gürültü.
3. **F-23'ü ilk yapın.** `hapticEditorLog` açık kalırsa senaryo C'de gerçek olmayan GC Alloc göreceksiniz.

### Bakılacak marker'lar

| Marker | Ne aranıyor | İlgili bulgu |
|---|---|---|
| `Physics2D.Simulate` (ve altındaki `Physics2D.Solve`) | Senaryo A'da toplam süre. 60 gövdede orta segment Android'de 1-4 ms bekleyin. | [F-11](#f-11-physics2d-job-multithreading-kapalı) |
| `FruitTicker.FixedUpdate` / `Fruit.FixedUpdate` | Uyuyan meyve oranı arttıkça düşmeli. F-06 öncesi/sonrası karşılaştırın. | [F-05](#f-05-fruit-kendi-update-ve-fixedupdatesini-taşıyor-kural-7-ihlali), [F-06](#f-06-fruitfixedupdate-uyuyan-gövdelerde-de-boşa-dönüyor) |
| `Fruit.OnCollisionStay2D` | Temas sayısıyla doğrusal artıyor mu. | [F-07](#f-07-oncollisionstay2d-içinde-temas-başına-tekrarlanan-native-okuma) |
| `FaceDirector.Update` | Senaryo A'da 60 meyve için süre. İçindeki `TickFaces` payı `EvaluateAndAssign`'dan çok daha büyükse F-08 devrede. | [F-08](#f-08-fruitfaceticklook-hedef-yokken-de-her-karede-transform-yazıyor) |
| **`Canvas.SendWillRenderCanvases`** | **En kritik UI marker'ı.** Skor sayarken sivriliyorsa F-03 doğrulanmış olur. | [F-03](#f-03-skor-yazısı-ile-11-slotluk-evrim-zinciri-aynı-alt-canvasta) |
| `Canvas.BuildBatch` | F-03 öncesi/sonrası. Rebuild edilen eleman sayısı düştüğü için süre de düşmeli. | [F-03](#f-03-skor-yazısı-ile-11-slotluk-evrim-zinciri-aynı-alt-canvasta) |
| `TextMeshPro.GenerateText` / `TMP_Text.ForceMeshUpdate` | Senaryo B'de popup başına bir kurulum. | [F-20](#f-20-her-combo-popupında-forcemeshupdate) |
| `WormBoostDirector.Update` | Silahlı fazda `PlaceCursors` payı. | [F-12](#f-12-placecursors-sabit-değeri-meyve-başına-her-karede-yeniden-hesaplıyor) |
| `ParticleSystem.Update` (native) | Senaryo C'de toz + moloz + sis toplamı. | — |
| `GC.Alloc` sütunu | Senaryo A'da **0 B/kare** olmalı. Değilse Deep Profile ile kaynağı bulun. | — |

### Deep Profile ne zaman

Sadece **kaynağı bulamadığınız bir `GC.Alloc` veya açıklanamayan bir `Scripts` süresi** varsa. Deep Profile her managed çağrıyı sarmaladığı için mutlak süreleri 5-10 kat şişirir; **hiçbir zaman "önce/sonra" karşılaştırması için kullanılmaz**. Bu kod tabanında sıcak yol allocation'sız yazılmış (struct `EmitParams`, paylaşımlı `StringBuilder`, `SetText("{0}", int)`, `for` + index), yani senaryo A'da Deep Profile'a ihtiyaç duymanız beklenmiyor — duyuyorsanız beklenmedik bir şey var.

### Memory Profiler'da

- **`Texture2D` toplamı** — F-04 öncesi/sonrası. `FruitAtlas` ve `UIAtlas` sayfalarını tek tek görün; beklenen düşüş ~4 kat. Sayfa sayısı beklediğinizden fazlaysa atlas doluluk oranını (`maxTextureSize` 2048) gözden geçirin.
- **`AudioClip` toplamı** — `Forest.wav` (18 MB kaynak) **Streaming + Vorbis** olarak import edilmiş (`loadType: 2`, `compressionFormat: 1`, `preloadAudioData: 0`), yani RAM'de olmaması gerekiyor. Doğrulayın: Memory Profiler'da bu klip için birkaç yüz KB'dan fazla görürseniz import ayarı build'de uygulanmamış demektir.
- **Sahne objesi sayısı** — F-10 sonrası açılış sonunda ~306 havuz objesi olmalı. Beklenenden fazlaysa bir havuz iki kez ısınıyor.

### Frame Debugger'da

- **F-02 doğrulaması (en önemlisi):** oynanış sırasında Frame Debugger'ı aç, draw çağrılarını gez. `PanelCanvas` altındaki hiçbir şey listede **görünmemeli**. Şu an görünüyorsa (bekleniyor) F-02 doğrulanmış olur; düzeltmeden sonra tamamen kaybolmalı.
- **Batch sayısı:** meyveler + yüzler tek atlas ve tek materyal paylaştığı için 60 meyve **tek batch'te** çizilmeli. Birden fazla batch görürseniz sorting order aralıklarına bakın (`Fruit.Initialize : 82` gövde `100 - tier`, yüz `+1`) — araya farklı materyalli bir renderer girmiş olabilir.
- **HDR/depth doğrulaması (bkz. [bölüm 6](#6-önerilmeyen-optimizasyonlar)):** Frame Debugger'ın en üstünde render target formatını okuyun. `R8G8B8A8_UNORM` görüyorsanız HDR bayrağının maliyeti yok, dokunmayın. `B10G11R11` ya da `R16G16B16A16` görüyorsanız `Supports HDR`'ı kapatıp aynı yerden tekrar bakın ve GPU süresini karşılaştırın. Aynı ekranda depth attachment olup olmadığını da görürsünüz — yoksa `Use Depth/Stencil Buffer` zaten etkisiz.

### Ölçmeden değiştirmeyin

- **[F-11](#f-11-physics2d-job-multithreading-kapalı) (Physics2D multithreading)** — 60 gövde, iş dağıtımının kârlı olduğu eşiğin sınırında. `m_IslandSolverCostThreshold: 100` aşılmazsa hiç kazanç olmaz, hatta iş kurulum maliyeti yüzünden gerileyebilir. **Ve mutlaka cihazda his kontrolü yapın.**
- **HDR / depth buffer** — yukarıda anlatıldı.
- **`splashPrewarmPerFrame`** — F-10 sonrası ısıtma yükü 6 katına çıkıyor. Değeri cihazda açılış süresine bakarak ayarlayın; kare başına çok yüksek değer açılış ekranında takılma yapar (zaten çözülmüş olan sorunun geri gelmesi).
- **[F-05](#f-05-fruit-kendi-update-ve-fixedupdatesini-taşıyor-kural-7-ihlali)** — kazanç mütevazı, değişiklik ise execution order'a dokunuyor. F-06'yı uygulayıp ölçün; kalan `Fruit.FixedUpdate` payı bütçede görünmüyorsa F-05'i hiç yapmayın.

---

## 8. Temiz olan kısımlar

Bunlar doğru yapılmış; **koruyun** ve yeni kod yazarken aynı desenleri sürdürün.

**Statik event bus — leak yok.** `GameEvents.ResetStatics` (`: 148-173`) `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` ile **21 event'in tamamını** sıfırlıyor. Aynı desen `BoostGate.ResetStatics` (`: 82-86`) ve `PointerInput.ResetStatics` (`: 98-107`) için de uygulanmış. Domain reload kapalıyken statik state'in bir sonraki oturuma taşınması bu tür projelerde en sık görülen leak kaynağı ve burada kapatılmış. **Yeni bir static event eklerken `ResetStatics`'e satır eklemeyi unutmayın.**

**Abonelik simetrisi.** `OnEnable`'da abone olup `OnDisable`'da çıkan **her** bileşeni kontrol ettim: eksik çıkış yok, lambda ile abone olan yer yok (hepsi isimli metot, yani `-=` gerçekten çalışıyor). `AudioService`, `HapticService`, `EffectDirector`, `FaceDirector`, `CoinFlyDirector`, `ConfettiDirector`'de ayrıca `if (Instance != this) return;` koruması var — `Awake`'te yok edilmeye işaretlenen kopyanın abone olup çift efekt üretmesi engellenmiş. Bu ince bir detay ve doğru yakalanmış.

**Coroutine hiç kullanılmamış.** Bütün zamanlama float sayaçla yapılıyor (`QuakeBoostDirector`, `WormBoostDirector`, `HapticService`, `Worm`, `ComboPopupItem`, `CoinFlyDirector`, `ConfettiDirector`). Bu, "obje pool'a dönerken coroutine durduruluyor mu", "devre dışı objede `StartCoroutine`", "`new WaitForSeconds` her çağrıda yeniden yaratılıyor mu" sorularının **tamamını** ortadan kaldırıyor. `StopAllCoroutines` eksikliği gibi bir risk yok çünkü coroutine yok.

**Havuz doğruluğu.** `FruitPool.OnGetFruit` sırası doğru: `SetParent` → `SetActive(true)` → `ResetState()`, ardından `Spawn` içinde `position` → `Initialize`. Meyve `simulated = false` ile aktifleştiği için "aktif ama eski konumda ve eski collider'la" penceresinde hayalet çarpışma üretmiyor. `ResetState` (`: 105-123`) hız, açısal hız, `rotation`, `transform.rotation`, `IsMerging`, `IsDropped`, `DropTime`, `WasPlayerDropped`, `_slowFrames`, pop/squash sayaçları, `simulated`, `collisionDetectionMode` ve yüzü (`_face.ResetFace()`) sıfırlıyor — kontrol ettiğim kadarıyla eksik state yok. `collectionCheck: true` çift iade riskini yakalıyor. `Despawn`'daki `if (!_active.Contains(f)) return;` ikinci bir kalkan.

**`FruitPool` oyun sonunda tahtayı donduruyor.** `FreezeAll` + `Fruit.Freeze` (`: 152-172`) `Time.timeScale = 0` yerine seçilmiş ve **gerekçesi de yazılmış**: timeScale yüz geçişlerini ve parçacıkları da dondurup yarı yolda bırakıyordu. Doğru karar, doğru belgelenmiş.

**Merge kuyruğu.** Çift-merge koruması üç katmanlı ve her katmanın ayrı bir işi var: `Fruit.TryRequestMerge`'deki `GetInstanceID()` karşılaştırması (`: 327`) aynı çiftin iki taraftan istek göndermesini eliyor, `_queuedPairs` (`MergeHandler : 26`) kuyruk içinde tekrarı eliyor, `LateUpdate`'deki `IsMerging`/`activeSelf`/`Definition` kontrolleri (`: 72-78`) işlenme anındaki bayatlığı eliyor. `MergeHandler.LateUpdate`'in en başındaki "oyun oynanmıyorsa kuyruğu boşalt" bloğu (`: 46-65`) gerçek bir hatayı (oyun bittikten sonra tek başına düşen meyve + skor artışı) kapatıyor ve gerekçesi yorumda duruyor.

**`Physics2D` ayarları.** `Reuse Collision Callbacks` **açık** — `Collision2D` nesnesi yeniden kullanılıyor, yani çarpışma callback'lerinde allocation yok. Yığın yoğun bir 2D oyunda bu, kapatılırsa doğrudan GC baskısına dönen tek ayardır ve doğru. `Auto Sync Transforms` **kapalı** (doğru: `transform.position` yazımının gizli fizik senkronizasyonu tetiklemesini engelliyor). `Simulation Mode = FixedUpdate` doğru.

**Layer Collision Matrix temizlenmiş.** Ham matrisi çözdüm: `Fruit`(6) yalnızca `Fruit` ve `Wall`(7) ile çarpışıyor; `Default`, `UI`, `TransparentFX`, `Water` ile çiftleri **kapatılmış**. Prompt'un sorduğu "gereksiz çarpışma çiftleri açık mı" sorusunun cevabı: hayır, doğru yapılmış.

**Fruit prefab ayarları.** `Interpolation = Interpolate` (50 Hz fizikte 60/120 fps render için doğru — bu olmadan düşen meyve titrer), `Sleeping Mode = Start Awake` (uyumaya izin veriyor), `Use Auto Mass = false` + `Fruit.Initialize`'da `_rb.mass = def.mass` (tier başına kütle kontrolü). `Collision Detection` prefab'da Continuous ama kod `Initialize`/`ResetState`'te `Discrete`'e çekiyor ve yalnızca gerektiğinde (`Drop`, `TryRearmContinuous`, `ArmFruitsForShaking`) yükseltiyor — sweep taramasının maliyetini yalnızca hızlı meyveler için ödüyorsunuz. Bu, "gerekli yerde açık, gereksiz yerde kapalı" tam olarak.

**Sıcak yolda allocation yok.** `EffectDirector` `ParticleSystem.EmitParams` **struct**'ını kullanıyor ve paylaşımlı `ParticleSystem`'e `Emit` ediyor (efekt başına sistem yaratmıyor). `CoinFlyDirector.FlyingCoin` ve `ConfettiDirector.Piece` `struct` dizilerinde, `ref` ile erişiliyor. `ComboPopupDirector` paylaşımlı `StringBuilder` + `TMP.SetText(StringBuilder)` kullanıyor. `HUDView`/`CoinHudView`/`BoostButton`/`GameOverPanel` `SetText("{0}", int)` aşırı yüklemesini kullanıyor — string birleştirme yok. Hiçbir `Update`'te LINQ, `new List<>`, `FindObjectOfType`, `GameObject.Find`, `Camera.main` ya da `GetComponent` yok (kameralar `[SerializeField]`, meyve listesi `FruitPool.Active`).

**"Sadece değişince yaz" disiplini.** `GameOverDetector.SetLineAlpha` (`: 198-205`), `FruitFace.SetAlpha` (`: 239-247`), `FruitChainView.SetAlpha` (`: 194-200`), `WormBoostDirector.TickCursorFade` (`: 416-420`), `Worm.ApplyHeadSprite` (`: 397`), `BoostButton.SetBadges` (`: 146-161`), `PausePanel.SetIcon` (`: 168-177`), `ConfettiDirector`'ün "sönme sadece ömrün son diliminde" kuralı (`: 410-421`) — hepsi `Mathf.Approximately` ya da eşitlik kontrolüyle gereksiz yazmayı eliyor. F-08, F-14 ve F-22 bu disiplinin **atlandığı** üç yeri gösteriyor, yani kural doğru, uygulaması üç noktada eksik.

**Boşta erken çıkış disiplini.** `QuakeBoostDirector.Update` (`: 256`), `WormBoostDirector.Update` (`: 312`), `CoinFlyDirector.Update` (`: 319`), `ConfettiDirector.Update` (`: 351`), `CoinHudView.Update` (`: 134-137`), `HUDView.Update` (`: 94`), `Fruit.Update` (`: 197`), `CameraShaker.LateUpdate` (`: 140-150`) — hepsi ilk satırlarda tek bir karşılaştırmayla çıkıyor. Oyunun %99'unda bu sistemlerin maliyeti sıfıra yakın.

**Alt canvas ayrımı zaten var.** `MainCanvas` altında `HUDCanvas` / `PanelCanvas` / `OverlayCanvas` iç içe canvas olarak ayrılmış (Override Sorting ile). Bu, rebuild izolasyonunun **doğru** yolu ve zaten kurulmuş — F-03 bu ayrımı bir kademe daha ileriye götürmeyi öneriyor, sıfırdan kurmayı değil.

**`CanvasGroup` ile görünürlük yönetimi.** `CoinHudView` (`: 35-37`) ve `BoostButton` (`: 65-66`) `SetActive` yerine `CanvasGroup` kullanıyor ve **neden** kullandığını yazıyor: kendini kapatan bileşen `OnDisable`'da aboneliğini bırakıp bir daha haber alamaz. Bu tuzağı bilerek atlamak deneyim işaretidir. (F-02 aynı kısıtı bozmadan görsel maliyeti çözmeyi öneriyor.)

**`UIPanel`'in "kendi Update'ini tanımlama" uyarısı.** `: 68-70`'deki yorum ve `OnTick` deseni gerçek bir Unity tuzağını (Unity yalnızca en türemiş `Update`'i çağırır) hem çözmüş hem belgelemiş. `GameOverPanel : 18-20` bunu tekrar hatırlatıyor. Bu tür "neden böyle" yorumları kod tabanının en değerli kısmı.

**Ses import ayarları doğru.** SFX'ler (`drop.wav`, `merge.wav` ve diğerleri): `loadType: 0` (Decompress On Load) + `compressionFormat: 0` (PCM) + `forceToMono: 1` — kısa efektler için tam olarak doğru kombinasyon. Müzik (`Forest.wav`): `loadType: 2` (Streaming) + `compressionFormat: 1` (Vorbis, quality 0.7) + `preloadAudioData: 0` + `loadInBackground: 1`. `AudioService.cs : 64-66`'daki tooltip bu ayrımı uyarı olarak yazmış ve **uygulama da uyarıya uygun** — belgelenen kural ile asset'in gerçek hali örtüşüyor, ki bu nadirdir.

**Ses kanal yönetimi.** 6 kanallı round-robin SFX havuzu + gürültü ve müzik için **ayrı** kanallar. Gürültünün ayrı olma gerekçesi (`: 94-98`) doğru: round-robin bir sonraki efekt onun üstüne yazardı. Retrigger guard'ı iki kademeli (`sfxRetriggerGuard` 0.06 genel, `mergeRetriggerGuard` 0.012 birleşme için) ve ikincisinin varlık sebebi fizik adımı süresine dayandırılarak açıklanmış.

**`FrameRateSetup`.** `Application.targetFrameRate`'in Android'de -1 bırakılmasının ne demek olduğu (30 FPS), `vSyncCount`'un mobilde etkisiz olduğu, ekranın gerçek tazeleme hızına uyum (judder önleme) ve Swappy ile ilişkisi — hepsi doğru ve `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` ile doğru yerde. Prompt'un 10. bölümündeki sorunun cevabı: **evet, ayarlanmış ve doğru ayarlanmış.**

**Accelerometer/gyro maliyeti yok.** Telefon sallama boost'u `Input.acceleration` okumuyor; `QuakeBoostDirector` butonla tetiklenip kendi zarfını (`Envelope`) sürüyor. "Sadece boost aktifken mi okunuyor" sorusu geçersiz — hiç okunmuyor, dolayısıyla sürekli sensör maliyeti de yok.

**`PointerInput` kare başına bir kez örnekliyor** (`: 50-76`) ve iki input backend'i arasındaki pointer numarası kaymasını (`IsOverUI`, `: 79-94`) belgelenmiş bir şekilde çözüyor. `DropController` ve `WormBoostDirector` aynı kaynaktan besleniyor, kod kopyası yok. Input polling'de allocation yok.

**`CameraFit` / `BackgroundCover` en-boy oranı işi.** `orthographicSize = max(designHalfHeight, designHalfWidth / aspect)` formülü tahtayı dünya koordinatında sabit tutup kamerayı uyarlıyor — 9:16, 9:20, 9:21 ve tablet oranlarında **aynı oyunu** veriyor. Bu, prompt'un 11. bölümündeki "farklı en-boy oranlarında bozulan hesaplamalar" sorusunun doğru cevabı ve adalet gerekçesi (`: 6-10`) de yazılmış. `BackgroundCover` açılan boşluğu pivot etrafında ölçekleyerek kapatıyor, `Update` kullanmıyor (`CameraFit.FrameChanged` olayına abone). `CameraShaker.SetRest` ile ikisi arasındaki bayat-dinlenme-konumu tuzağı da kapatılmış.

**`SaveService` dayanıklılığı.** `File.Exists` → `try/catch` → `JsonUtility.FromJson(...) ?? new SaveData()` → `Migrate()` zinciri bozuk dosya, eksik dosya ve eski şema durumlarının **üçünü de** çökmeden karşılıyor. `Migrate` sürüm bazlı ve açık ("JsonUtility'nin eksik alanları nasıl doldurduğuna güvenmiyoruz"). Yazma politikası da doğru ayrılmış: `AddCoins` diske yazmıyor (oyun sonunda onlarca kez çağrılabiliyor), `TrySpendCoins` **anında** yazıyor (geri alınamaz işlem), gerisi `OnApplicationPause`/`Focus`/`Quit` + oyun sonunda. `_isDirty` guard'ı gereksiz yazmayı eliyor.

**`CameraShaker`'ın kendini toparlaması.** `SetRumble`'ın yazıldığı kareyi takip edip (`_rumbleFrame`, `: 40`) çağıran unutursa sarsıntıyı söndürmesi — director çökse ya da `Abort`'u atlasa bile kameranın sonsuza kadar titrer kalmasını engelliyor. Savunmacı tasarımın iyi bir örneği. Dinlenme konumuna **birebir** (lerp değil) geri yazma gerekçesi de doğru.

**Editör kodu ayrılmış.** `Assets/FruitMerge/Editor` altındaki 4 dosya build'e girmiyor. `SceneFixups` `InitializeOnLoadMethod` + `EditorApplication.delayCall` kullanıyor ve `isPlayingOrWillChangePlaymode` ile play mode'da çalışmayacak şekilde korunmuş. `FaceSet.AutoFillFromNames` ve `FruitDatabase.OnValidate` `#if UNITY_EDITOR` içinde. `FruitDatabase.OnValidate` (`: 26-59`) 11 elemanlı bir liste üzerinde tier/zincir tutarlılığı kontrol ediyor — editörü kilitleyecek bir ağırlıkta değil, ve zincir kopukluğunu erken yakalaması değerli.

**`FaceSet`'in düz dizi lookup'ı.** 12 ifade × 4 boyut, `(int)expression * 4 + (int)size` ile indeksleniyor: Dictionary yok, string yok, allocation yok. `_lookup` lazy build ve `OnEnable`/`OnValidate`'te geçersiz kılınıyor. `FruitFace.DangerState`'in histerezis state'ini meyvenin **kendisinde** tutması da aynı fikrin devamı — Dictionary aramasından ve allocation'dan kurtarıyor.

**`BoostGate` / `BoostButton` / `BoostShopPanel` genelleştirmesi.** `BoostId` ile indekslenen sabit boyutlu dizi (LINQ ve Dictionary yok), `Register`/`Unregister` simetrisi, "sadece kendi kaydını sil" koruması (`: 59`), ve tek `BoostButton` script'inin bütün boost butonlarına hizmet etmesi. Yeni boost eklemek = bir enum değeri + bir director + sahnede bir buton kopyası. `IsAnyBusy`'nin gövdesi kare başına iki abone tarafından çağrıldığı için sadece bir `for` + null kontrolü.

**Olay tasarımında abone sırasına güvenilmemesi.** `OnComboMerge` (meyve + konum + combo bir arada), `OnNewRecord`, `OnStarsRevealed`, `OnBoostStateChanged` (id + armed + charges bir arada) — hepsi "iki ayrı olayı dinleyip birleştirmek abone sırasına güvenmek olurdu" gerekçesiyle tek olayda birleştirilmiş. `CoinHudView.Apply` (`: 119-130`) aynı fikrin bir adım ötesi: iki bağımsız girdiyi alanlara yazıp kararı tek yerde veriyor, böylece olay sırası sonucu değiştirmiyor. Bu, prompt'un sorduğu "race condition ve sıralama bağımlılıkları" riskini sistematik olarak kapatan bir yaklaşım.

**`SaveService.Start` / `AudioService.Start` / `HapticService.Start` gerekçeleri.** Üçü de "`Awake`'te değil `Start`'ta, çünkü …" diye açıklanmış ve gerekçe doğru: `Awake` sırasında yayınlanan olay, execution order'ı daha büyük dinleyicilerin (`HUDView` 100) `OnEnable`'ı çalışmadan gittiği için boşa düşüyordu. "Referans alma `Awake`'de, başka script'e bağımlı init `Start`'ta" kuralı bu kod tabanında bilinçli uygulanmış.

---

*Rapor sonu. Bir sonraki parti geldiğinde bulgu ID'leri F-24'ten devam edecek ve özet tablo güncellenecek.*
