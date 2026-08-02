# Boost: "Tatlı Kurtçuklar" (`worms`)

> Faz 8'in **dikey dilimi**. `plan.md` "hammer + bomb ile dikey dilim" diyordu — bu boost
> mekanik olarak `hammer` ile birebir aynı (tek meyve sil), sadece sunumu çok daha zengin.
> Yani altyapıyı bununla kurarsak `hammer`/`bomb`/`remove` sonradan sadece efekt değiştirmek olur.

---

## ✅ UYGULANDI — ne yapıldı, plandan ne değişti

Art üretildi, kod yazıldı, sahne kuruldu, konsol temiz. **Play Mode'da elle test edilmedi.**

**Senin revize ettiğin akış uygulandı** (aşağıdaki §1'deki ilk taslak değil):

| | Plandaki ilk taslak | Uygulanan |
|---|---|---|
| Hedefleme | `target_dim_overlay` + parmağı takip eden tek nişangâh | **HER meyvenin üstünde saat yönünde dönen nişangâh**, dim overlay yok |
| Seçim geri bildirimi | — | seçilen meyvede **`target_crosshair_pulse_01..04` bir kez, 0.4 sn'de** — büyüyerek söner, kısa bir "ping". Kurtların gelişi boyunca ekranda kalmıyor. Kare başına 6 ekran karesi düşüyor, adımlar okunuyor; sönme sadece son karede |
| Diğer meyvelerin tepkisi | — | hedef **`scared`** olurken tahtadaki diğer TÜM meyveler **`surprised`** olup gözleriyle onu izliyor. `FaceDirector`'e `SetBoostFocus(Transform)` eklendi; öncelik sırasında oyun sonu ve `Express` kilidinin altında, kutlama/danger/sleepy'nin üstünde. Meyve yenince odak düşüyor |
| Kurt sayısı | sabit 3 | **meyveye göre 1–6**: `tier/2 + 1` → kiraz/böğürtlen 1 · lime/üzüm 2 · portakal/yeşil elma 3 · şeftali/hindistan cevizi 4 · ejder/ananas 5 · karpuz 6 |
| Kurt tarafı | 2 sol 1 sağ (sabit) | **yarı yarıya, tek sayıda fazlalık rastgele bir tarafa** |
| Isırık ritmi | 6 ayrı ısırık, her ısırıkta meyve kademeli küçülür | sürekli çiğneme + **7 kırıntı saçılması**, meyve düzgün küçülür |
| Meyvenin yok olması | yeme süresinin sonunda | **yemenin 1. saniyesinde**, sis en yoğunken — yığın oradan itibaren çöküyor |
| Kurtların gidişi | geldikleri yönden geri | **geldikleri yönde DEVAM edip karşı taraftan çıkıyor**, 1.5 sn |

**Toplam süre:** seçimden sonra 2 sn geliş + 2 sn yeme + 1.5 sn gidiş = **5.5 sn**.

**Doğrulanmış zaman çizelgesi** (t = hedef seçildiği an):

| t | Olan |
|---|---|
| 0.00 | pulse çakar |
| 0.20 | pulse biter |
| 0.00–2.00 | kurtlar geliyor |
| 2.00 | yeme başlar · sis + kırıntı |
| **3.00** | **meyve yok olur** · yığın çöker · kırıntı durur · **kurtlar çiğnemeyi bırakır** (tok yüz + şişmiş gövde) |
| 3.20 | sis üretimi durur |
| 4.00 | sisin son parçacığı ölür (3.20 + 0.80 ömür) → kurtlar yola devam eder |
| 5.50 | kurtlar ekrandan çıktı, boost biter |

Sisin son parçacığının öldüğü kare, yeme fazının bittiği kareyle **birebir aynı** —
`eatSmokeLifetime` emisyon penceresinden çıkarılarak hesaplanıyor, elle ayarlanmıyor.

**Yazılan/değişen dosyalar**

| Dosya | Durum |
|---|---|
| `Scripts/Gameplay/Worm.cs` | **yeni** — segment zinciri, tek yol fonksiyonundan besleniyor |
| `Scripts/Services/WormBoostDirector.cs` | **yeni** — hedefleme + tüm sekans + kurt/nişangâh havuzu |
| `Scripts/UI/BoostButton.cs` | **yeni** — HUD butonu |
| `Scripts/Services/EffectDirector.cs` | `EmitEatSmoke(...)` eklendi, `ClearAll` sisi de temizliyor |
| `Scripts/Data/GameConfig.cs` | 30 yeni ayar (hepsi Tooltip'li) |
| `Scripts/Core/GameEvents.cs` | `OnWormsBoostStateChanged`, `OnFruitEaten` |
| `Scripts/Gameplay/DropController.cs` | boost çalışırken bırakma girdisi kilitli |
| `Scripts/Services/GameOverDetector.cs` | boost çalışırken oyun bitmiyor |
| `Scripts/Services/FaceDirector.cs` | `SetBoostFocus` (hedef scared, diğerleri surprised + bakış), `NotifyActivity`, `SuppressSleepFor` |
| `Art/Effects/Mat_EatSmoke.mat` | **yeni** — `particle_smoke_02` dokulu URP particle materyali |
| Sahne | `WormBoostDirector` objesi · `EffectDirector/EatSmoke` parçacık sistemi · `HUDCanvas/BoostSlot` butonu |

**Sıralama katmanları:** meyve 90–100 · yüz 91–101 · nişangâh 112 · pulse 113 · sis+kırıntı 200 · **kurtlar 220–250** (kurtlar sisin ÜSTÜNDE, yoksa bulutun arkasında kaybolurlardı).

**Uyuklama:** `faceIdleToSleepy` "son BIRAKMADAN beri geçen süre"ye bakıyor (5 sn) ve boost
5.5 sn sürüyor — meyveler kurtlar hâlâ ekrandayken uyuklamaya başlıyordu. Hedef seçilirken
`FaceDirector.SuppressSleepFor(5.5)` **tek çağrı** ile sayaç ileri atılıyor; geri sayım
kurtlar çıktıktan sonra başlıyor (t=10.5). Süresi belirsiz tek faz olan *Armed* için
kare başına `NotifyActivity()` var — tek float ataması.

**Update maliyeti:** `WormBoostDirector.Update` boost boştayken **tek bir enum
karşılaştırmasıyla** çıkıyor (`_state == Idle && _cursorAlpha <= 0`). Nişangâh sönme
döngüsü havuzun tamamını (44) değil o an kullanımdaki sayıyı geziyor. `FaceDirector`
boost odağının `transform.position`'ını meyve döngüsünün dışında bir kez okuyor.
Hiçbir yerde `FindObjectOfType` yok; `GetComponent` sadece `Awake`'lerde.

**Bilinçli olarak yapılmayanlar**
- **Ses yok.** `worms_arrive/chew/leave` klipleri üretilmedi, kod hiçbir ses çağırmıyor.
- **Envanter kaydedilmiyor.** Kullanım hakkı her oyunda `GameConfig.wormsChargesPerRun`'a (3) sıfırlanıyor; `SaveService` v3 migrasyonu yapılmadı.
- **`BoostDefinition` / `BoostDatabase` SO'ları yok.** Tek boost için gereksiz soyutlamaydı; ikinci boost gelince çıkarılır.
- **`worms_bite_hole.png`** import edildi ama kullanılmıyor.
- **Puan verilmiyor** (`wormsScoreOnEat = 0`, `ScoreSystem`'in dışarıdan puan ekleyen API'si de yok).

**Art notu:** `worms_tail.png` hizalanırken ucu kırpıldı, taper koddan yeniden çizildi — orijinaline göre ucu bir tık daha küt. Kusursuz istersen §4 PROMPT 4 ile o tek dosyayı yeniden üret, `Assets/FruitMerge/Art/Boosts/Effects/` üstüne kopyala; pivot ölçümü otomatik değil, söyle yeniden ölçeyim.

---

## 1. Ne oluyor (oyuncunun gördüğü)

1. Oyuncu HUD tepsisinden kurtçuk boost'una basar.
2. Ekran kararır (`target_dim_overlay`), parmağın altındaki meyve halkayla işaretlenir.
3. Meyveye dokununca: **ekranın sol ve sağ kenarından 3 tombul kurtçuk sürünerek girer.**
4. Hedef meyve korkar (`face_scared`), kurtlara bakar.
5. Kurtlar meyvenin kenarına yapışır, sırayla ısırır. Her ısırıkta meyve biraz küçülür,
   meyve renginde kırıntılar sıçrar.
6. Meyve renginde **duman** giderek meyveyi kaplar. 2 saniye içinde meyve dumanın altında kaybolur.
7. Duman dağılırken kurtçuklar **tombullaşmış ve mutlu** halde geldikleri yönden sürünerek çıkar.
8. Üstteki yığın boşluğa çöker.

**Toplam süre ≈ 4.4 sn.** Bu sürede bırakma girdisi kilitli, fizik çalışmaya devam ediyor.

### Zaman çizelgesi

| t (sn) | Olan |
|---|---|
| 0.0 | Hedef onaylandı, dim overlay sönüyor · meyve `Scared` + kurda bakıyor |
| 0.0 – 1.0 | Kurtlar ekran dışından süzülüp meyvenin çevresine yerleşiyor (`worms_arrive`) |
| 1.0 – 3.2 | Isırık döngüsü: 6 ısırık, kurtlara dağıtılmış · her ısırıkta kırıntı + küçülme + `PlaySquash` |
| 1.0 – 3.2 | Duman yoğunluğu 0 → tam · yarıçap meyve yarıçapının 1.6 katı |
| ~2.3 | Meyve yüzü `Scared` → `Dizzy` |
| 3.2 – 3.6 | Duman patlaması zirvede — **meyve tam bu anda, dumanın arkasında** `Despawn` ediliyor |
| 3.4 – 4.4 | Kurtlar `worms_head_full` + `worms_body_fat` ile geri sürünüyor, ekran dışında havuza dönüyor |
| 3.6 – 4.8 | Duman sönüyor |

> **Numara:** meyveyi dumanın en yoğun olduğu karede despawn etmek. Dissolve shader'a,
> ısırık maskesine, hiçbir şeye gerek kalmıyor — göz geçişi görmüyor.

---

## 2. Görsel envanteri

### 2A. Zaten var — **tek dosya üretmeye gerek yok** ✅

| İş | Kullanılacak mevcut asset | Not |
|---|---|---|
| Hedefleme karartması | `Boosts/Cursors/target_dim_overlay.png` | hazır |
| Nişangâh | `target_crosshair_pulse_01..04.png` | 4 kare, koddan döndürülür |
| Seçili meyve halkası | `target_highlight_circle.png` | meyvenin `displayColor`'ıyla boyanır |
| Geçersiz hedef | `target_crosshair_invalid.png` | hazır |
| **Meyve renginde duman — yumuşak katman** | `Effects/Particles/particle_circle_soft.png` | neredeyse beyaz radyal gradyan → `displayColor` ile boyandığında tam istenen sisi veriyor |
| **Meyve renginde duman — çizgi film katmanı** | `Effects/Particles/particle_smoke_01..04.png` | konturlu karikatür bulut, boyandığında konturu da koyulaşıyor (istediğimiz şey) |
| Isırık kırıntıları | `Effects/Particles/particle_shard_01..04.png` | `displayColor` ile boyanır |
| Bitiş parıltısı | `Effects/Particles/particle_sparkle.png` | kurtlar giderken |
| Kurt gölgesi | `particle_circle_soft.png` (siyah, %25 alfa) | ayrı dosya gereksiz |
| Boost yuvası / çerçeve / rozet | `Boosts/Bar/*`, `Boosts/States/boost_badge_count.png` | hazır |
| İkon durumları (aktif/cooldown/kilitli) | `boost_glow_ring.png`, `boost_cooldown_mask.png`, `boost_lock.png` | **paylaşımlı** — aşağıya bak |
| Meyve yüz ifadeleri | `face_scared_*`, `face_dizzy_*` | `FruitFace.Express()` hazır |

> **Duman için hiçbir yeni art gerekmiyor.** Bu, işin en büyük kısmını sıfırlıyor.
> `EffectDirector` zaten "renk `displayColor`'dan geliyor — her tier için ayrı asset üretmeye
> gerek yok" mantığıyla kurulu; duman da aynı yoldan gidiyor.

### 2B. Üretilecek — kurtçuk karakteri (**4 zorunlu + 2 opsiyonel**)

Hepsi `Assets/FruitMerge/Art/Boosts/Effects/` altına (mevcut `bomb_*`, `hammer_*` deseni),
**512×512, PPU 512, Sprite Mode: Single, Pivot: Center.**

| # | Dosya | Ne | Zorunlu |
|---|---|---|---|
| 1 | `worms_head.png` | Kafa — ağzı kapalı, gülümsüyor, sağa bakıyor | ✅ |
| 2 | `worms_head_open.png` | Kafa — ağzı kocaman açık (ısırık karesi) | ✅ |
| 3 | `worms_body.png` | Tek gövde halkası (yüzsüz top) | ✅ |
| 4 | `worms_tail.png` | Kuyruk — sola doğru sivrilen son halka | ✅ |
| 5 | `worms_head_full.png` | Kafa — gözler mutlu kavis, tok/memnun | ⬜ opsiyonel |
| 6 | `worms_body_fat.png` | Şişmiş gövde halkası (yemek sonrası) | ⬜ opsiyonel |

**Toplam 4–6 dosya.** Flipbook yapsaydık 30+ olurdu (bkz. §3).

### 2C. Üretilecek — UI (**2 zorunlu**)

| # | Dosya | Boyut | Ne |
|---|---|---|---|
| 7 | `Boosts/Icons/boost_worms.png` | 640×640 | Krem rozetli boost ikonu (tepsi/mağaza) |
| 8 | `Boosts/States/boost_worms_available.png` | 640×640 | Rozetsiz, çıplak obje |

`_active` / `_cooldown` / `_disabled` / `_locked` **üretilmeyecek** — mevcut `boost_glow_ring`,
`boost_cooldown_mask`, `boost_lock` paylaşımlı asset'leri `_available`'ın üstüne runtime'da
bindirilecek. Böylece 5 yerine 1 AI üretimi, ve tutarlılık garantili.

> Bu kararı `hammer`/`bomb`'a da geriye dönük uygulayabiliriz — 15 boost × 4 durum = **60 dosya**
> ölü ağırlıktan kurtulur. Ama şart değil, mevcut dosyalar duruyor.

### 2D. Opsiyonel — 1 dosya

| # | Dosya | Ne | Neden opsiyonel |
|---|---|---|---|
| 9 | `worms_bite_hole.png` | 256×256, yumuşak koyu hilal (%35 siyah, konturuz) | Meyvenin üstüne bindirilip "ısırık deliği" yanılsaması verir. Küçülme + duman zaten işi görüyor; bu sadece ekstra cila |

### 2E. Ses — **3 klip yok, üretilmeli**

| Dosya | Ne |
|---|---|
| `worms_arrive.wav` | Kısa, yumuşak "tıpış tıpış" sürünme (0.4 sn) |
| `worms_chew.wav` | Tek ısırık — sulu "nyam" (0.15 sn). Pitch koddan ±%12 rastgele |
| `worms_leave.wav` | Tok/memnun küçük geğirme veya "iyy" (0.3 sn) — opsiyonel |

`AudioService` deseni hazır. Import ayarı: SFX (Decompress On Load + PCM), müzik ayarı **değil**.

---

## 3. Neden "segment zinciri", flipbook değil

Kurt bir **karakter**, kıvılcım değil. Üç yol var:

| Yol | Dosya | Sorun |
|---|---|---|
| **Flipbook** (kare kare sürünme) | 8–12 kare × 2 yön × çiğneme = **30+** | AI kare kare tutarlı sprite sheet üretemez — her karede kafa boyutu/rengi kayar. Faz 2'de merge flipbook'u zaten bu yüzden reddedildi |
| **2D Animation kemik rig'i** | **1** | Paket projede kurulu (`com.unity.2d.animation`). Organik sonuç verir ama Editor'de elle rigging gerekir, MCP ile scriptlenemez. **Plan B** |
| **Segment zinciri** ✅ | **4** | Kafa + N gövde + kuyruk, koddan "takip et" zinciriyle dizilir |

Segment zinciri neden bu projeye tam oturuyor:

- **Sürünme bedava.** Zincir aralığına yürüyen bir sinüs dalgası uygulayınca gövde
  sıkışıp açılıyor — tırtıl yürüyüşünün tam kendisi. Kare yok, allocation yok.
- **Tek `Update` kuralına uyuyor** (kural 7). `WormDirector` tek `Update`, her kurt `Tick(dt)`.
- Kurt uzunluğu, boyu, hızı `GameConfig`'ten ayarlanabilir (kural 6).
- Yön değişimi `flipX` — soldan gelen kurt için ayrı asset yok.
- **En önemlisi:** AI'dan istediğimiz şey "aynı topun 4 farklı yüzeyi" — bu, AI'ın
  gerçekten tutarlı üretebildiği tek şey.

**Karar: bacak yok.** Sevimli tırtıllar (Caterpie tipi) bacaksızdır; ayrıca yürüyüş döngüsü
olmadan bacak kaydırmak kötü görünür. Salınan zincir tek başına "sürünüyor" okunuyor.

### Ölçüler

| Değer | Öneri | Not |
|---|---|---|
| Segment çapı | 0.30 dünya birimi | Kiraz yarıçapı 0.19, karpuz 1.23 |
| Segment aralığı | çapın %62'si = 0.186 | Bindirme olsun ki zincirde boşluk görünmesin |
| Segment sayısı | 5 (kafa + 3 gövde + kuyruk) | |
| Kurt uzunluğu | ≈ 1.04 birim | |
| Hedefe göre ölçek | `Lerp(0.75, 1.35, tierT)` | Karpuza büyük kurt, kiraza küçük kurt |
| Sürünme hızı | 4.5 birim/sn | Kenardan (x≈±3.9) merkeze ≈ 0.87 sn |
| Kurt sayısı | 3 | Yakın kenara 2, uzağa 1 |

Kurtlar meyve yığınının **önünde** çiziliyor (sorting order meyvelerin ve yüzlerin üstünde),
bu yüzden yığının içinden geçme problemi yok — kartuncu mantığı, sorun değil.

Hedef meyve fizikte serbest kalmaya devam ediyor (yığın çökerse yuvarlanabilir);
kurtlar her karede meyvenin `transform`'una göre yeniden konumlanıyor, yani meyveyle
birlikte hareket ediyorlar. `Rigidbody2D`'ye hiç dokunulmuyor.

---

## 4. AI görsel prompt'ları

> Prompt'lar **İngilizce** — görsel modelleri İngilizce'de belirgin şekilde daha iyi.
> Renkler mevcut art'tan **piksel örneklenerek** çıkarıldı, tahmin değil.

### 4.0 Tutarlılık taktiği (bunu atlama)

1. **Önce `worms_head.png`'i üret**, beğenene kadar tekrarla. Bu senin *karakter referansın*.
2. Diğer tüm kurt dosyalarında o görseli **reference image olarak ekle** (Nano Banana / GPT-Image:
   dosyayı yükle · Midjourney: `--cref` · Flux: Redux) ve prompt'a şunu ekle:
   `Same character, same colors, same outline weight, same ball size and position as the reference.`
3. **Alternatif ve daha güvenli yol:** gövde + kuyruk + kafayı **tek bir görselde yan yana** üret
   (1536×512, üç top eşit boyutta bir sırada), sonra 3 dosyaya böl. AI tek görsel *içinde*
   tutarlılığı, görseller *arasında* olduğundan çok daha iyi tutturuyor. Bölme + hizalama
   scriptini ben yazarım.

### 4.1 Ortak stil bloğu (her prompt'un sonuna yapıştır)

```
STYLE: 2D mobile casual game sprite, flat vector cartoon, thick uniform dark brown
outline #48270E about 3% of the canvas width, simple cel shading with exactly two tones
per surface (a base tone plus one darker shade), one soft white elliptical gloss highlight
in the upper-left, no texture, no noise, crisp clean edges, fully transparent background
with real alpha, object centered, straight-on orthographic side view, bright cheerful
children's mobile game art in the style of Suika / Watermelon Game fruit sprites.
```

### 4.2 Ortak negatif prompt

```
NEGATIVE: background, sky, ground, drop shadow, cast shadow, photorealism, 3D render,
pixel art, text, letters, watermark, signature, frame, border, multiple characters,
blurry soft edges, color fringing, realistic insect anatomy, hairy legs, many legs,
sharp menacing teeth, creepy, scary, slimy, gore, dirt, mold, rot
```

### 4.3 Renk paleti (mevcut art'tan örneklendi)

| Rol | Hex | Kaynak |
|---|---|---|
| Kontur (kurt + meyve + ikon) | `#48270E` | `fruit_04_orange` konturu |
| Kurt gövde ana ton | `#B9E27C` | yeni — yeşil elmadan (`#94C23B`) bilinçli olarak **daha açık** |
| Kurt gövde gölge tonu | `#8FC04F` | |
| Kurt karın / alt hilal | `#FFF3D4` | |
| Yanak allığı | `#FF9C9C` | |
| Ağız içi | `#6B2E2E` | |
| Dil | `#FF8C8C` | |
| Rozet krem dolgu | `#FEFAEE` | `boost_bomb` örneklendi |
| Rozet iç altın halka | `#D7B273` | `boost_bomb` örneklendi |
| Duvar arka planı (kontrast referansı) | `#FEEFB4` | `bg_game` 9768 piksel ortalaması |

> **Kontrast notu:** oyunda lime, yeşil elma (`#94C23B`) ve karpuz da yeşil. Kurdun
> `#B9E27C`'si bunlardan belirgin şekilde **daha açık**, üstelik kalın koyu kontur +
> kocaman beyaz gözler siluet kontrastını her meyvede kurtarıyor. Yine de beğenmezsen
> yedek palet: nane-turkuaz `#7FD8C4` gövde / `#4FB39D` gölge (hiçbir meyveyle çakışmıyor).

---

### PROMPT 1 — `worms_head.png` ⭐ önce bunu üret

```
A single cute cartoon grub head, side view facing RIGHT, isolated on transparent background.

SHAPE: one plump perfectly round ball, centered in the canvas, its diameter exactly 70%
of the image width. This ball is the character's head AND its first body segment - it must
be a simple sphere, nothing sticking out of it except the antennae described below.

SURFACE: base color soft yellow-green #B9E27C, with one darker shade #8FC04F occupying
the lower-right third of the ball. A small cream #FFF3D4 crescent along the very bottom
of the ball, like a pale belly. Thick dark brown #48270E outline around the whole ball.
One soft white elliptical gloss highlight on the upper-left of the ball.

FACE (on the right half of the ball): two large white oval eyes with round black pupils
and a tiny white catchlight dot in each; a small closed happy smile below and between
them; soft rosy #FF9C9C oval blush on the cheek. Friendly, harmless, baby-like -
big eyes, small mouth.

ANTENNAE: two short thin curved brown #48270E antennae rising from the top of the ball,
each ending in a tiny filled round tip. They may extend above the ball outline.

Nothing else in the image. No body, no legs, no tail, no leaf, no apple.

[STYLE BLOCK]
[NEGATIVE]
```
Üret: 1024×1024 → teslim: **512×512**

---

### PROMPT 2 — `worms_head_open.png`

> `worms_head.png`'i reference image olarak ekle.

```
Same cute cartoon grub head as the reference image - identical character, identical
colors, identical outline weight, ball diameter still exactly 70% of the image width
and centered in exactly the same position. Side view facing RIGHT.

ONLY CHANGE: the mouth is now WIDE OPEN in a big happy chomp. The mouth is a large
rounded oval opening on the right side of the ball, filled with dark #6B2E2E, with a
small soft pink #FF8C8C tongue at the bottom and two tiny rounded white triangles as
front teeth on the upper edge - blunt and cute, not sharp or scary. The eyes are open
wide and excited (larger white ovals, pupils slightly smaller). The cheek blush is
slightly bigger and rounder, as if puffed.

Everything else - ball size, ball position, antennae, colors, shading, highlight -
must be pixel-for-pixel the same as the reference.

[STYLE BLOCK]
[NEGATIVE]
```
Üret: 1024×1024 → teslim: **512×512**

---

### PROMPT 3 — `worms_body.png`

> `worms_head.png`'i reference image olarak ekle.

```
A single body segment of the cute cartoon grub from the reference image, isolated on
transparent background.

One plump perfectly round ball, centered in the canvas, diameter exactly 70% of the
image width - the SAME size and SAME position as the head ball in the reference image.

Base color soft yellow-green #B9E27C, one darker shade #8FC04F on the lower-right third,
a small cream #FFF3D4 crescent along the very bottom, thick dark brown #48270E outline,
one soft white elliptical gloss highlight on the upper-left.

Absolutely nothing else: NO face, NO eyes, NO mouth, NO antennae, NO legs. Just the
plain segment ball.

[STYLE BLOCK]
[NEGATIVE]
```
Üret: 1024×1024 → teslim: **512×512**

---

### PROMPT 4 — `worms_tail.png`

> `worms_body.png`'i reference image olarak ekle.

```
The tail end segment of the cute cartoon grub from the reference image, isolated on
transparent background.

A plump ball identical in color, shading and size to the reference body segment
(diameter 70% of the image width, centered), but tapering smoothly toward the LEFT
into a short, soft, rounded tip - like the last segment of a caterpillar. The taper
extends to the left of the ball and ends in a small rounded point; the ball part
itself must stay the same size and in the same centered position as the reference.

Base color #B9E27C, darker shade #8FC04F on the lower-right, cream #FFF3D4 crescent
along the bottom, thick dark brown #48270E outline, one white gloss highlight upper-left.

No face, no eyes, no legs, no stinger, no spike.

[STYLE BLOCK]
[NEGATIVE]
```
Üret: 1024×1024 → teslim: **512×512**

---

### PROMPT 5 — `worms_head_full.png` (opsiyonel)

> `worms_head.png`'i reference image olarak ekle.

```
Same cute cartoon grub head as the reference image - identical character, colors,
outline, ball size (70% of image width) and centered position. Side view facing RIGHT.

ONLY CHANGE: the grub is now full and satisfied after a big meal. Both eyes are closed
into happy upward arcs (^ ^) drawn as thick dark brown #48270E curves. A small contented
closed smile. The rosy #FF9C9C cheek blush is larger and rounder. One tiny cream #FFF3D4
crumb stuck at the corner of the mouth. One small four-point white sparkle floating just
above the antennae.

[STYLE BLOCK]
[NEGATIVE]
```
Üret: 1024×1024 → teslim: **512×512**

---

### PROMPT 6 — `worms_body_fat.png` (opsiyonel)

> `worms_body.png`'i reference image olarak ekle.

```
The same plain body segment as the reference image, but plumper after a big meal:
the ball diameter is now 80% of the image width instead of 70%, still perfectly
centered, slightly rounder and fuller. Identical colors, identical shading logic,
identical outline weight, identical cream belly crescent, identical gloss highlight.
No face, no eyes, no legs.

[STYLE BLOCK]
[NEGATIVE]
```
Üret: 1024×1024 → teslim: **512×512**

---

### PROMPT 7 — `boost_worms.png` (rozetli boost ikonu) ⭐

> Rozet geometrisi `boost_bomb.png`'den **ölçülerek** çıkarıldı: rozet çapı tuvalin %90.8'i,
> dış kahverengi halka kalınlığı %1.9, altın saç halka %0.9 kalınlıkta ve dış kenardan
> %4.2 içeride.

```
A mobile game boost icon on a circular badge, isolated on transparent background.

BADGE (draw this exactly):
- A circular disc centered in the canvas, diameter exactly 91% of the canvas width.
- Disc fill: warm cream #FEFAEE.
- A solid dark brown #48270E ring on the outer edge of the disc, ring thickness
  exactly 2% of the canvas width.
- A thin golden-tan #D7B273 hairline ring, thickness 1% of the canvas width, drawn
  concentrically 4.2% of the canvas width inside the outer edge of the disc.
- A very subtle warm inner shadow just inside the brown ring.
- Absolutely nothing outside the disc. Fully transparent outside.

SUBJECT INSIDE THE BADGE:
A chubby, adorable yellow-green cartoon grub curled over the top-left of a glossy red
apple. The grub has a round segmented body (4 visible round segments), base color
#B9E27C with #8FC04F shading, a cream #FFF3D4 belly, two short curved antennae, one
large white eye with a black pupil and a white catchlight, and a rosy #FF9C9C cheek.
Its head is biting into the apple: a crescent-shaped chunk is missing from the apple's
upper-right side, and the bite hole shows pale cream #FBEFD0 apple flesh.

The apple is bright red #E8402E with a #B62D20 shade on the lower-right, a short brown
#48270E stem and one small green leaf, one white gloss highlight upper-left.

Three tiny cream crumb specks and one bright yellow #FFC93C four-point sparkle fly off
the bite, up and to the right.

The grub sits upper-left, the apple lower-right. Both are fully inside the cream area
with a comfortable margin - nothing touches or crosses the golden ring.

Everything drawn with the same thick dark brown #48270E outline.

[STYLE BLOCK]
[NEGATIVE]
```
Üret: 1280×1280 → teslim: **640×640**

---

### PROMPT 8 — `boost_worms_available.png` (rozetsiz)

> `boost_worms.png`'i reference image olarak ekle.

```
The exact same grub-biting-an-apple subject as the reference image, but WITHOUT the
circular badge: no cream disc, no brown ring, no golden ring. Only the grub and the
apple with their crumbs and sparkle, floating on a fully transparent background,
centered, scaled to fill about 78% of the canvas.

Identical character, identical apple, identical colors, identical outline weight,
identical shading and highlights as the reference.

[STYLE BLOCK]
[NEGATIVE]
```
Üret: 1280×1280 → teslim: **640×640**

---

### PROMPT 9 — `worms_bite_hole.png` (opsiyonel)

```
A soft crescent-shaped shadow, isolated on transparent background. It is a simple
half-moon / bite-shaped dark area: solid black at 35% opacity in the middle, fading
smoothly to fully transparent at all edges. No outline, no stroke, no texture, no
color - pure soft dark crescent. Centered, occupying about 60% of the canvas.
Flat, blurry-edged, like a soft airbrushed mask.

NEGATIVE: outline, stroke, hard edges, color, teeth, apple, fruit, character, text
```
Üret: 512×512 → teslim: **256×256**

---

## 5. Import ayarları (tekrar eden dersleri atlama)

Bu projede **dört kez** aynı tuzağa düşüldü. Yeni dosyalar için baştan:

| Ayar | Değer | Neden |
|---|---|---|
| **Sprite Mode** | **Single** | ⚠️ Unity'nin otomatik dilimleyicisi kopuk şekilleri (antenler, kırıntılar, parıltı) ayrı sprite'lara böler. `merge_burst_06` **52 parçaya** bölünmüştü. **İçe aktardıktan sonra dosya sayısı = sprite sayısı mı, kontrol et** |
| **Pixels Per Unit** | Kurt: **512** · İkonlar: varsayılan | 512×512 sprite = 1.0 dünya birimi → kodda tek `wormSegmentDiameter` ile ölçekleniyor |
| **Pivot** | Center | Zincir merkezden merkeze diziliyor |
| **Mesh Type** | Full Rect | |
| **Compression** | **Uncompressed** | Atlasa paketlenen kaynakta çift sıkıştırma olmasın (Faz 0.5) |
| **Filter Mode** | Bilinear | |

**Atlas:** Şu an projede sadece `UIAtlas` ve `FruitAtlas` var — `plan.md`'nin Faz 0'da
oluşturduğunu söylediği `EffectsAtlas` **dosya sisteminde yok**, silinmiş görünüyor.
Boost art'ının (250 dosya) da hiçbir atlası yok. Bu faz kapsamında:

- `BoostAtlas.spriteatlasv2` → `Boosts/Icons` + `Boosts/States` + `Boosts/Bar` + `Boosts/Cursors`
  · **Tight Packing = kapalı, Rotation = kapalı** (UI'da `Image` kullanılıyor, bu şart)
- Kurt sprite'ları dünya uzayında `SpriteRenderer` → `Boosts/Effects` ile birlikte aynı atlasa
  ya da yeniden kurulacak `EffectsAtlas`'a. Bu ayrı bir temizlik işi, boost'u bloklamıyor.

**Normalizasyon adımı:** AI dört topu birebir aynı boyut/konumda üretmeyecek. `tree-branch`'te
yaptığımızın aynısını yaparız — alfa kanalından her sprite'ın top sınırlarını ölçen tek
seferlik bir editor pass'i yazıp `worms_head/body/tail`'i ortak bir referansa hizalarız.
Bu scripti ben yazarım, senin elle piksel hizalaman gerekmez.

---

## 6. Kod planı

### 6.1 Yeni dosyalar

| Dosya | Sorumluluk |
|---|---|
| `Scripts/Data/BoostDefinition.cs` | SO: `id`, `displayName`, `icon`, `targetMode` (None/SingleFruit/Area), `cooldown`, `chargesOnStart` |
| `Scripts/Data/BoostDatabase.cs` | `BoostDefinition[]` + id ile arama (`FruitDatabase` deseni) |
| `Scripts/Services/BoostService.cs` | Envanter, cooldown sayaçları, `Arm(def)` / `Cancel()` / `Execute(def, target)`. Tek `Update` |
| `Scripts/Gameplay/BoostTargeting.cs` | Dim overlay + nişangâh + `FruitPool.Active` üzerinden en yakın meyveyi bulma. Tek `Update`, sadece armed durumdayken |
| `Scripts/UI/BoostTrayView.cs` | `hud_boost_tray` + yuvalar, `boost_glow_ring`/`boost_cooldown_mask`/`boost_lock` bindirmeleri |
| `Scripts/Gameplay/Worm.cs` | Tek kurt: segment zinciri, yol takibi, durum makinesi (Approach → Settle → Eat → Leave). `Tick(dt)`, **`Update` yok** |
| `Scripts/Services/WormDirector.cs` | Kurt havuzu (`UnityEngine.Pool.ObjectPool`, `FruitPool`/`ComboPopupDirector` deseni), tek `Update`, tüm sekansın zamanlaması |

### 6.2 Değişecek dosyalar

| Dosya | Değişiklik |
|---|---|
| `Services/EffectDirector.cs` | Yeni public `PlayEatSmoke(Vector2, Color, float radius, float intensity01)` + `PlayCrumbs(...)`. Mevcut paylaşımlı-ParticleSystem deseni aynen kullanılıyor; iki yeni `ParticleSystem` alanı (haze + puff) |
| `Core/GameEvents.cs` | `OnBoostArmed(BoostDefinition)`, `OnBoostCancelled()`, `OnBoostUsed(BoostDefinition)`, `OnFruitEaten(FruitDefinition, Vector2)`. `ResetStatics()`'e ekle |
| `Services/SaveService.cs` | `SaveData` **v3**: `int[] boostCharges`. v2→v3 migrasyonu. *(Faz 5 `tutorialDone` de v3'e binebilir — tek migrasyonda ikisini birden yapmak mantıklı)* |
| `Services/GameOverDetector.cs` | Boost oynarken kontrolü askıya al — yoksa yığın çökerken oyun bitebilir |
| `Gameplay/DropController.cs` | `BoostService.IsBusy` iken bırakma girdisini yut |
| `Data/GameConfig.cs` | Aşağıdaki alanlar |

### 6.3 Yeni `GameConfig` alanları (sihirli sayı yok — kural 6)

```
[Header("Boost — kurtçuklar")]
wormCount              = 3      kaç kurt gelsin
wormSegmentCount       = 5      kafa + gövde + kuyruk
wormSegmentDiameter    = 0.30   dünya birimi
wormSegmentSpacing     = 0.62   çapın oranı — bindirme olsun
wormScaleMin           = 0.75   kiraz hedefinde ölçek
wormScaleMax           = 1.35   karpuz hedefinde ölçek
wormCrawlSpeed         = 4.5    birim/sn
wormWaveAmplitude      = 0.22   segment aralığındaki sinüs genliği (sürünme dalgası)
wormWaveFrequency      = 6.0    dalganın hızı
wormSpawnMarginX       = 0.8    ekran kenarından ne kadar dışarıda doğsun
wormApproachDuration   = 1.0    sn
wormEatDuration        = 2.2    sn
wormBiteCount          = 6      toplam ısırık (kurtlara dağıtılır)
wormBiteLunge          = 0.35   hamlenin segment çapına oranı
wormLeaveDuration      = 1.0    sn
wormSortingOrder       = 60     meyvelerin ve yüzlerin üstünde
eatSmokeRadiusFactor   = 1.6    meyve yarıçapının katı
eatSmokeMaxAlpha       = 0.85
eatFruitMinScale       = 0.15   despawn anındaki küçülme oranı
eatCrumbsPerBite       = 6
wormsScoreOnEat        = 0      yenen meyve puan versin mi (0 = vermesin)
```

### 6.4 Kurallara uyum kontrolü

| Kural | Nasıl uyuluyor |
|---|---|
| 1 Lambda yasağı | Tüm abonelikler isimli metot |
| 2 `+=`/`-=` simetrisi | `WormDirector`, `BoostService`, `BoostTrayView` `OnEnable`/`OnDisable` |
| 4 UI `unscaledTime` | Hedefleme modu **oyunu durdurmuyor** (timeScale 1 kalıyor) → normal `deltaTime`. Ama boost tepsisi pause panelinde de görünürse orada `unscaledDeltaTime` |
| 5 Null guard | `EffectDirector.Instance`, `FruitPool.Instance`, `AudioService.Instance` |
| 7 Tek `Update` | `WormDirector` tek `Update`, kurtlar `Tick(dt)`, segmentler saf `Transform` |
| 8 Coroutine yok | Tüm zamanlama `float` sayaç |
| 9 `sprite` sadece değişince | Kafa `head ⇄ head_open ⇄ head_full` geçişinde `if (_current == next) return;` |
| 11 Allocation yok | Segment `Transform[]` `Awake`'te cache, `ParticleSystem.EmitParams` struct |
| 13 Havuz | `ObjectPool<Worm>`, `wormCount × 2` ön ısıtma |

### 6.5 Uygulama sırası

1. **Art gelir** (§4 prompt'ları) → import + Single kontrolü + normalizasyon pass'i
2. `BoostDefinition` + `BoostDatabase` + `Worms` SO asset'i
3. `BoostService` (envanter + save v3 + cooldown) — henüz efekt yok, `Execute` sadece `Despawn` etsin
4. `BoostTargeting` + `BoostTrayView` → **burada oynanabilir bir dikey dilim var**, test et
5. `EffectDirector.PlayEatSmoke` + `PlayCrumbs` — meyve dumanla yok olsun, kurt hâlâ yok. Test et
6. `Worm` + `WormDirector` — kurtlar gelsin, yesin, gitsin
7. Ses klipleri + `AudioService` bağlantısı
8. `GameOverDetector` askıya alma + `DropController` girdi kilidi
9. Cila: yüz ifadeleri, `worms_head_full`/`worms_body_fat`, `worms_bite_hole`

> 4. ve 5. adımların sonunda ayrı ayrı test edilebilir durum var — kurt kodu yazılmadan
> boost zaten çalışıyor olacak. Art gecikirse 2–5 arası yine de ilerleyebiliriz.

---

## 7. Senin kararına kalan 4 şey

| # | Soru | Benim önerim |
|---|---|---|
| S1 | Yenen meyve puan versin mi? | **Hayır** (`wormsScoreOnEat = 0`). Bu bir kurtarma aracı, puan kaynağı değil — yoksa oyuncu boost'u skor farmlamak için kullanır |
| S2 | Kurt rengi: açık yeşil `#B9E27C` mi, nane-turkuaz `#7FD8C4` mü? | **Açık yeşil.** "Meyveyi yiyen kurt" ikonografisi bu; turkuaz yedek |
| S3 | Boost oynarken oyun dursun mu (`timeScale = 0`)? | **Hayır.** Fizik sürsün — yığının çökmesini görmek ödülün yarısı. Sadece bırakma girdisi kilitli |
| S4 | 4 durum ikonunu (`active`/`cooldown`/`disabled`/`locked`) üretelim mi, runtime'da mı bindirelim? | **Runtime'da bindir.** 1 üretim yeter, `boost_glow_ring`/`boost_cooldown_mask`/`boost_lock` zaten var |

---

## 8. Özet: kaç dosya üreteceksin

| | Dosya |
|---|---|
| Kurt karakteri (zorunlu) | **4** |
| Kurt karakteri (opsiyonel cila) | 2 |
| UI ikonu | **2** |
| Isırık deliği (opsiyonel) | 1 |
| Ses | 2–3 klip |
| **Duman / kırıntı / hedefleme / rozet / yuva** | **0 — hepsi mevcut art'tan** |

**Minimum 6 PNG ile boost tamamen çalışır.**
