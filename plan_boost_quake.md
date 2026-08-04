# Boost: "Deprem" (`quake`)

> Faz 8'in **ikinci dikey dilimi**. `worms` hedefli (tek meyve seçilir, silinir);
> `quake` **hedefsiz/global** ve **hiçbir meyveyi silmiyor** — sadece yığını sarsıp yeniden
> yerleştiriyor. İlk boost bir "sil" aracıydı, bu bir "karıştır" aracı; ikisi mekanik olarak
> tamamen farklı, yani Faz 8'in iki uç desenini de artık elimizde var.

---

## ✅ UYGULANDI — ne yapıldı, plandan ne değişti

Kod yazıldı, sahne kuruldu, **art ve ses üretildi + import edildi + bağlandı**, derleme temiz.
**Play Mode 1. turu yapıldı** → 5 sorun bulundu, hepsi düzeltildi (aşağıya bak). 2. tur bekliyor.

### 🔧 Play Mode 6. tur — yukarı hareket YASAK + 4 yön dilimi

Meyveler hâlâ o kadar yükseğe çıkıyordu ki **duvar collider'larının üstünden kaçıyorlardı.**
Kök nedeni itme yönü değildi — `quakeKickRestSpeed` kapısıydı:

> Kapı "hızlı meyveyi itme" diyordu, ama hızlı meyveyi **hiç ellemiyordu**. Sıkışık yığın bir
> yay/kama gibi davranıp bir meyveyi fırlattığında onu frenleyecek hiçbir şey kalmıyordu.
> İtme yönünü kısmak bu yolu kapatmıyor, çünkü fırlatan şey itme değil **temas çözücüsü**.

**Üç katmanlı çözüm:**

1. **İtme yönü asla yukarı bakmıyor.** Açı yalnız **alt yarım daireden** seçiliyor:
   `angle = -h * π` → `0° = sağ · -90° = aşağı · -180° = sol`. Sağa/sola/aşağı öteleme.
2. **`quakeMaxRiseSpeed` (0.6) itme sonrası kırpılıyor** — rastgele sapma yukarı bakabiliyor.
3. ⭐ **Aynı clamp HAREKET HALİNDEKİ meyvelere de uygulanıyor.** Kapı artık meyveyi atlamıyor;
   itmiyor ama yukarı hızını kırpıyor. Böylece bir meyve en fazla bir itme aralığı (0.06 sn)
   boyunca yükselebiliyor.

Sonuç: en fazla yükselme **0.018 birim** (v²/2g). Duvar tepesi zeminden 7.41 birim yukarıda —
kaçış artık matematiksel olarak imkânsız, "denedim olmadı" değil.

**Yön artık dilimlerle seçiliyor** (Perlin ile gezinme yerine): sarsıntı süresi
`quakeKickDirectionSlots` (4) dilime bölünüyor, her meyve her dilimde kendine özgü yeni bir
rastgele yön alıyor.

| Ölçüm | Değer |
|---|---|
| Sarsıntı süresi | **2.5 sn** (yarıya indirildi) |
| Dilim süresi | 2.5 / 4 = **0.625 sn** |
| Dilim başına itme | **10 kez aynı yönde** → mesafe buradan |
| Toplam boost süresi | 3.3 sn |
| En fazla yükselme | **0.018 birim** |

Yön `Hash01(seed, slot)` ile üretiliyor — klasik `frac(sin(x)*büyük sayı)` karması. Deterministik
olması şart: aynı meyve aynı dilimde her çağrıda aynı yönü almalı, yoksa yön kare başına zıplar
ve hareket yerinde titremeye döner. Dilime her depremde değişen bir tuz (`_slotSalt`) ekleniyor
ki aynı meyve her deprem aynı diziyi almasın.

`quakeKickTurnRate` **silindi** · yeni `quakeKickDirectionSlots` (4) ve `quakeMaxRiseSpeed` (0.6).

---

### 🔧 Play Mode 5. tur — yön MEYVE BAŞINA, Perlin ile gezinen

4. turdaki ortak dalga mesafeyi çözdü ama yığın **blok halinde** kayıyordu. İstenen: her meyve
kendi yönüne gitsin, deprem boyunca birçok yön değiştirsin, ve **kendi gittiği yöne baksın**.

Buradaki tuzak: kare başına tamamen rastgele yön verirsen hareketler birbirini götürür ve meyve
olduğu yerde titrer (3. turun sorunu). Gerekli olan şey **meyve başına bağımsız ama zaman
içinde SÜREKLİ** bir yön. Çözüm Perlin gürültüsü:

```
seed  = Mathf.Repeat(f.DropTime * 7.13f, 1f)          // meyveye özgü, ömrü boyunca sabit
angle = PerlinNoise(phase + seed*53.7, seed*17.3) * 2π   // phase = _stateTime * quakeKickTurnRate
dir   = (cos θ, sin θ * quakeKickVerticalScale).normalized
v    += (dir * strength + Random.insideUnitCircle * jitter) * tierScale
```

Perlin sürekli olduğu için yön **ani zıplamıyor**: meyve bir süre aynı tarafa gidip yavaşça
başka yöne dönüyor. Tohum gürültü eksenini kaydırdığından **her meyve bağımsız**.

| Ölçüm | Değer |
|---|---|
| Meyve başına belirgin yön değişimi | **~6** (4.5 sn × 1.3 turnRate) |
| Bir yönde kalma süresi | ~0.77 sn = **~13 itme aynı yönde** → mesafe buradan |
| Dikey bileşen tavanı | 1.92 → hoplama 0.188 birim |
| Tohum maliyeti | `Repeat(DropTime, 1)` — **native çağrı yok** (`GetInstanceID()` olurdu) |

**Bakış artık meyvenin GERÇEK hızından okunuyor**, director'den gelen bir yönden değil:
`FaceDirector` kuake dalında `f.Body.linearVelocity`'ye bakıyor, `quakeLookMinSpeed` (0.35)
altındakiler düz bakıyor. Böylece her meyve kendi hareketine bakıyor ve
`SetQuakeLookDirection` API'si ile director→FaceDirector kare-başı bağı tamamen kalktı.

`quakeWaveInterval` / `quakeWaveIntervalJitter` / `quakeWaveReverseSpread` **silindi** ·
`quakeWaveVerticalScale` → `quakeKickVerticalScale` · yeni `quakeKickTurnRate` (1.3) ve
`quakeLookMinSpeed` (0.35).

---

### 🔧 Play Mode 4. tur — ORTAK sarsıntı dalgası (salınım)

3. tur hareketi geri getirdi ama mesafe azdı: her meyveye **bağımsız rastgele yön** verildiği
için hareketler birbirini götürüyordu — yığın olduğu yerde titriyor, hiçbir yere gitmiyordu.

**Çözüm: yön artık meyve başına değil, tahtanın tamamı için ORTAK — ve periyodik olarak
ters dönüyor.** Gerçek deprem tek yöne sürüklenme değil salınımdır.

```
// her quakeWaveInterval (0.8 sn ±0.25) saniyede bir:
_waveDir = (-_waveDir) döndürülmüş ±quakeWaveReverseSpread (75°)   // TERSİ + sapma = salınım
_waveDir.y *= quakeWaveVerticalScale (0.6)                          // ağırlıklı yatay

// her itmede:
v += (_waveDir * strength + Random.insideUnitCircle * jitter) * scale
```

Tersini almak salınımı **garantiliyor** (tamamen rastgele seçilse yığın bir yöne sürüklenirdi),
±75° sapma metronom tekdüzeliğini kırıyor, `quakeKickJitterRatio` (0.35) de meyvelerin robot
gibi tek blok halinde gitmesini engelliyor: yığın bir bütün olarak kayarken kendi içinde de
çalkalanıyor.

| Ölçüm | Değer |
|---|---|
| Ortak itme | 3.2 birim/sn (±1.12 sapma) |
| Bir dalga boyunca aynı yöne itilme | **13 kez** (0.8 sn / 0.06 sn) — mesafe buradan geliyor |
| Yatay bileşen tavanı | 3.2 birim/sn |
| **Dikey** bileşen tavanı | 1.92 → hoplama sadece 0.188 birim (`verticalScale` 0.6 kısıyor) |
| Emniyet clamp tavanı | 1.03 birim — duvar tepesine 7.41 birim var |
| Sarsıntı süresi | 4.5 sn → **~6 yön değişimi** · toplam **5.3 sn** |

**Yüzler artık aşağı değil GİTTİKLERİ YÖNE bakıyor.** `FaceDirector.SetQuakeLookDirection(dir)`
eklendi; director her yön değişiminde bir kez çağırıyor (kare başına değil). Yön salındığı için
bakışlar da sürekli değişiyor — sabit noktaya bakmak "hepsi aşağı bakıyor" gibi donuk duruyordu.
`FaceDirector._floorY` alanı gereksiz kaldı, silindi.

`quakeKickStrength` 2 → **3.2** · `quakeMaxSpeed` 3 → 4.5 · `quakeShakeDuration` 3.5 → 4.5 ·
yeni: `quakeWaveInterval`, `quakeWaveIntervalJitter`, `quakeWaveReverseSpread`,
`quakeWaveVerticalScale`, `quakeKickJitterRatio`.

---

### 🔧 Play Mode 3. tur — sönümleme yerine "sadece durgunu it"

2. turdaki sönümleme (`v *= 0.8`) fazla kaçtı: **meyveler hiç kıpırdamıyordu.** Sebep, iki
farklı olguyu tek parametreyle çözmeye çalışmaktı:

| Meyve nerede | Ne oluyordu |
|---|---|
| Yığının **içinde** (sıkışık) | Temas çözücü verilen hızı **aynı fizik adımında yutuyor**. Sönümleme kalanı da silince tamamen taş gibi durdular |
| **Havada** (serbest) | Kısıt olmadığı için her itmede hızlanıyor → 1. turda ekranın tepesine tırmanmasının gerçek nedeni buydu, "yukarı önyargısı" değil |

**Çözüm: sönümlemeyi at, itmeye bir KAPI koy** — `quakeKickRestSpeed`. Sadece hızı bu değerin
altında olan meyveler itiliyor:

```
if (body.linearVelocity.sqrMagnitude > restSqr) continue;   // ⭐ hareket halindekine dokunma
v += Random.insideUnitCircle * (strength * scale);          // yön 4 yöne simetrik
v  = clamp(v, quakeMaxSpeed);                                // sadece emniyet ağı
```

İkisini birden çözüyor: **sıkışık meyve her turda dürtülüp titreşiyor** (hız hep ~0, kapıyı
her zaman geçiyor), **hoplayan meyve inip yavaşlayana kadar rahat bırakılıyor** — havada
tekrar itilmediği için tırmanmak imkânsız.

| Ölçüm | Değer |
|---|---|
| Tek itmenin **en büyük** hoplaması | **0.204 birim** (kiraz yarıçapı 0.19) |
| **Tipik** hoplama | **0.092 birim** — "kısa miktarlarda öteleme" |
| Karpuzda en büyük hoplama | 0.115 birim (`quakeKickScaleBig` 0.75) |
| Sıkışık meyvenin dürtülme sayısı | 3.5 sn / 0.06 sn = **~58 kez** |
| Emniyet clamp tavanı | 0.46 birim — duvar tepesine çıkmak için 7.41 birim gerekiyor |

`quakeKickDamping` silindi · `quakeKickStrength` 0.9 → **2.0** · `quakeMaxSpeed` 2 → 3 ·
yeni `quakeKickRestSpeed` = 1.0.

---

### 🔧 Play Mode 2. tur — "fırlatma değil TİTREŞİM"

1. turdaki düzeltme fazla kaçtı: meyveler **ekranın tepesine uçuyordu**. Kök nedeni matematikti —
itmeler `v += kick` ile **birikiyordu**. Yukarı önyargı 1.6, yerçekiminin 0.08 sn'de sildiği
0.78'den büyük olduğu için her itmede net enerji ekleniyor, hız `quakeMaxSpeed`'e (4) dayanıyor
ve meyve 0.82 birim hoplayıp havadayken tekrar itiliyordu → tırmanma.

**Yeni model: sönümlemeli rastgele-yön titreşimi.**

```
v *= quakeKickDamping                        // ⭐ enerjiyi bağlar, birikmeyi keser
v += Random.insideUnitCircle * strength      // yön daire içinde düzgün: yukarı/aşağı/sağa/sola simetrik
v  = clamp(v, quakeMaxSpeed)                 // emniyet ağı
// açısal hıza DOKUNULMUYOR — döndürme istenmedi
```

Kararlı durum hızı ≈ `strength / sqrt(1 - damping²)`, yani parametrelerden **hesaplanabilir**
bir üst sınır var; artık tırmanma imkânsız.

| Ölçüm | Değer |
|---|---|
| Kararlı durum hızı (RMS) | 1.50 birim/sn |
| **Tipik hoplama** | **0.115 birim** |
| **Tavan hoplama** (clamp'e dayanınca) | **0.204 birim** — kiraz yarıçapı 0.19, yani meyve kendi çapı kadar bile zıplayamıyor |
| Sarsıntı sırasında düşme hızı tavanı | 2.35 birim/sn (sönümleme viskoz bir ortam gibi davranıyor) |

| İstek | Yapılan |
|---|---|
| Meyveler tepeye uçmasın | `v *= damping` (0.8) + `quakeMaxSpeed 4→2`. `quakeKickUpBias` **tamamen silindi** — artık yukarı önyargı yok |
| Kuvvet sadece yukarı değil, karışık olsun | `Random.insideUnitCircle` — daire içinde düzgün dağılım, dört yöne simetrik. `quakeKickX`/`quakeKickY` yerine tek `quakeKickStrength` |
| Meyveler dönmesin | Açısal hız itmesi kaldırıldı, `quakeKickSpin` silindi |
| Sallanma daha uzun sürsün | `quakeShakeDuration 1.8→3.5` · `quakeSettleDuration 0.6→0.8` · toplam **4.3 sn**. Titreşim küçük dokunuşlarla çalıştığı için süre şart |
| Ünlem işareti çıkmasın | Uyarı levhası **tamamen kaldırıldı** (6 config alanı + 3 metot + renderer) |
| Zemindeki patlama saçma | `hammer_impact_star` çakmaları **tamamen kaldırıldı** (4 config alanı + 2 metot + renderer dizisi) |
| Toz sadece zeminden değil yanlardan da | `EmitQuakeDust` artık `halfExtents` alıyor; director **üç şerit** besliyor: zemin (yatay) + sol duvar (dikey) + sağ duvar (dikey). `quakeDustWallShare` 0.45 · `quakeDustRate 40→70` (üçe bölündüğü için toplam arttı) |

**Sonuç: ekranda duran hiçbir sprite yok.** Deprem tamamen kamera sarsıntısı + itmeler +
üç yönden toz + kenarlardan düşen moloz + ses ile anlatılıyor. `State.Warn` fazı da kalktı
(görsel kalmadığı için anlamı yoktu) — akış artık sadece **Shake → Settle**.

Kullanılmayan art: `quake_ground_crack.png`, `quake_warning_sign.png`.

---

### 🔧 Play Mode 1. tur — bulunan sorunlar ve düzeltmeleri

| # | Belirti | Gerçek neden | Düzeltme |
|---|---|---|---|
| 1 | **Meyveler hiç hareket etmiyor**, sadece kamera sallanıyor | İtme değerleri **çok küçüktü**. `quakeKickX/Y = 0.3` sıkışık yığında iki fizik adımında temaslar tarafından yutuluyor. Daha kötüsü `quakeKickUpBias = 0.15`: 9.81'lik yerçekimi 0.12 sn'de **1.18 birim/sn** yukarı hız siliyor, yani 0.15'lik önyargı tamamen yok sayılıyordu — meyve hiç kalkmıyordu, dolayısıyla boşluk da açılmıyordu | `quakeKickX 0.3→1.2` · `quakeKickY 0.3→0.9` · **`quakeKickUpBias 0.15→1.6`** (≈0.13 birim hoplama) · `quakeKickSpin 40→130` · `quakeKickInterval 0.12→0.08` (kesik kesik durmasın) · `quakeMaxSpeed 6→4` (daha güçlü itmeye karşı tavan riskini dengele) |
| 2 | Uyarı fazında hiçbir şey olmuyor gibi | İtmeler **sadece `Shake` fazında** uygulanıyordu; `Warn` kasten kamera-only'di | `FixedUpdate` artık **`Warn` fazında da** itiyor (zarf düşük olduğu için hafif başlıyor). `quakeWarnDuration 0.5→0.3` |
| 3 | Konsol: **"Particle Velocity curves must all be in the same mode"** (her karede) | `QuakeDust.velocityOverLifetime`'da x/y `TwoConstants`, **z `Constant`** moddaydı. Unity üç eksenin de aynı modda olmasını şart koşuyor | Üç eksen de `TwoConstants`: `z = MinMaxCurve(0f, 0f)` |
| 4 | **Oyunda hiç ses yok, tüm sesler gitti** | **Kodla ilgisi yoktu.** Editör'ün Game view'daki **"Mute Audio" düğmesi açıktı** (`EditorUtility.audioMasterMute = true`). Tüm klipler bağlı ve `Loaded` durumdaydı, `save.json`'da `sfxOn: true`, `AudioListener.volume = 1`, tek listener aktif | Düğme kapatıldı. Bir daha ses kaybolursa **önce oraya bak** |
| 5 | Zemin kırılma efekti istenmiyor | — | **Zemin yarığı tamamen kaldırıldı**: `_groundCrackSprite`, `ShowCrack/TickCrack/ApplyCrack`, `_crack`, `_crackAlpha` ve 5 `GameConfig` alanı (`quakeCrackFadeIn/FadeOut/YOffset/ScaleFrom/SortingOrder`) silindi. `quake_ground_crack.png` `Assets/` altında duruyor ama **kullanılmıyor** |

### ➕ Aynı turda eklenen: düşen moloz

"sağdan soldan küçük yer renginde parçalar düşmeli ki deprem oldu vibe'ı olsun" →
ikinci paylaşımlı parçacık sistemi **`QuakeRubble`** kuruldu (`Mat_QuakeRubble` + `quake_pebble`
dokusu). Ekranın sağ ve sol kenarından, **kamera üst kenarının üstünden** (`Y 5.90..7.70`)
doğuyor, `gravityModifier = 1` ile düşüyor, `rotationOverLifetime` ile takla atıyor,
ömrünün %78'inde sönmeye başlıyor (zemine varmadan kayboluyor).

**Sıralama katmanı -4** — meyvelerin (90-100) **arkasından** düşüyorlar. Önden geçseler
yığından dikkat çalarlardı. `EffectDirector.EmitQuakeRubble(...)` `EmitQuakeDust` ile aynı
paylaşımlı-sistem desenini kullanıyor; 8 yeni `GameConfig` alanı.

**Kullanıcının plan üstünde revize ettiği kararlar:**

| | Plandaki ilk taslak | Uygulanan |
|---|---|---|
| Kutunun eğilmesi | Kutu ±2-3° sağa sola eğilecek | **Yok.** `Environment/Container` ve üç duvar collider'ı hiç dokunulmuyor |
| Yerçekimi | `Physics2D.gravity` ±2-3° döndürülecek | **Yok.** Global yerçekimi sabit `(0, -9.81)` |
| Sarsılan şey | kutu + kamera + meyveler | **ekran (kamera) + meyveler.** Oyuncu deprem sanacak, gerçekte sadece itme + kamera var |
| Merge puanı | soruldu | **normal puan + combo** — sıfır ek kod, mevcut merge yolu zaten işliyor |

**Uygulama sırasında plandan sapmalar (hepsi bilinçli):**

| # | Plan | Uygulanan | Neden |
|---|---|---|---|
| 1 | Zaman çizelgesi: t=0'da kamera 0→0.35, t=0.15'te "tam değere çıkar" | **Tek monoton zarf:** Warn'da 0 → 0.55, Shake'te 0.55 → 1 (`quakeAttackTime` içinde), sonda 1 → 0 (`quakeReleaseTime`), Settle'da 0 | Plandaki iki satır çelişiyordu (0.35 mi tam mı?). Tek zarf hem tutarlı hem tek fonksiyon |
| 2 | `quakeWarnPunch = 0.35` (mutlak genlik) | `quakeWarnAmplitudeRatio = 0.55` (**tam genliğe oran**) | Genliği ayarlayınca uyarı fazı kendiliğinden ölçekleniyor; mutlak değerde ikisini elle senkron tutmak gerekiyordu |
| 3 | İtmeler "Shake fazı boyunca" | İtmeler **SADECE** Shake fazında; Warn **kamera-only öncü sarsıntı** | Kullanıcının spec'i "uyarı anı → sonra ana sarsıntı" diyor. Meyveler uyarı anında da oynasa "sebep" beat'i kayboluyordu |
| 4 | `quake_rumble.wav` 2.4 sn olacak, sonunda kısılacak, `FadeOutQuakeRumble(float)` | Gürültü **loop**'lanıyor, sesi **sarsıntı zarfından** sürülüyor (`SetQuakeRumbleLevel`) | Ayrı fade kodu ve ayrı `Update` gerekmiyor; duyulan şiddet görülen şiddetle birebir aynı; klip artık kısa ve döngülenebilir olması yeterli |
| 5 | `IBoostDirector`: `Id, IsBusy, IsArmed, Charges, CanArm, Toggle, Abort` | `CanArm` ve `Abort` **arayüzden çıkarıldı** | İkisi de sadece her director'ün kendi içinde kullanılıyor, arayüzden hiç çağrılmıyor. Worms planı §77'nin dersi: spekülatif soyutlama yok |
| 6 | `BoostGate`: `IsAnyBusy` + `Get` | aynısı, ama yazdığım `IsAnyArmed` / `AbortAll` **silindi** | Hiçbir çağıranı yoktu |
| 7 | — | `quakeImpactStarSortingOrder` alanı eklendi | Yıldızın katmanını `quakeCrackSortingOrder + 215` diye türetmek okunmuyordu |
| 8 | — | `quakeDustSpawnLift`, `quakeCrackScaleFrom`, `quakeWarnSignPunchTime/PunchFrom/Overshoot/FadeOut`, `quakeImpactStarLifetime/Size` alanları eklendi | Plan bu davranışları tarif ediyordu ama sayıları yoktu; kural 6 gereği hepsi `GameConfig`'e |

**Doğrulanmış zaman çizelgesi** (t = butona basıldığı an, toplam **2.9 sn**):

**Toplam 4.3 sn** (3.5 Shake + 0.8 Settle). Ekranda duran sprite yok.

| t | Faz | Olan |
|---|---|---|
| 0.00 | **Shake** | `quake_crack.wav` · tek `Handheld.Vibrate()` · kamera `quakeStartPunch` ile snap atar · tüm meyveler `Surprised` · toz üç şeritten (zemin + iki duvar) · kenarlardan moloz düşmeye başlar · **itmeler ilk kareden itibaren** |
| 0.15 | | Zarf tam değere çıkar (`quakeAttackTime`) → itme, kamera, toz, moloz ve gürültü hep birlikte zirvede |
| 0.15–3.10 | | Kesintisiz titreşim. Meyveler kayıyor, birbirine değiyor, **merge'ler kendiliğinden oluyor** |
| 3.10 | | Zarf sönmeye başlar (`quakeReleaseTime` 0.4) — hepsi birlikte yatışır |
| 3.50 | **Settle** | İtme 0, kamera dinlenme konumunda, toz/moloz üretimi durdu. **Sönümleme kalktığı için meyveler ASIL burada boşluklara oturuyor** · **`IsBusy` hâlâ true** (yığın otururken haksızca kaybettirmesin) |
| 4.30 | Idle | Yüzler serbest · bırakma girdisi açılır · `GameOverDetector` devam eder |

**Yazılan/değişen dosyalar**

| Dosya | Durum |
|---|---|
| `Scripts/Core/BoostId.cs` | **yeni** — `enum { Worms, Quake }` |
| `Scripts/Core/IBoostDirector.cs` | **yeni** — `Id, IsBusy, IsArmed, Charges, Toggle()` |
| `Scripts/Core/BoostGate.cs` | **yeni** — `Register/Unregister/Get/IsAnyBusy`, `BoostId` ile indekslenen sabit dizi |
| `Scripts/Services/CameraShaker.cs` | **yeni** — `SetRumble`, `Punch`, `StopImmediate`; Perlin ofset, `LateUpdate` |
| `Scripts/Services/QuakeBoostDirector.cs` | **yeni** — faz makinesi, zarf, itmeler, toz/moloz emisyonu, levha/yıldız |
| `Scripts/Core/GameEvents.cs` | `OnWormsBoostStateChanged(bool,int)` → **`OnBoostStateChanged(BoostId,bool,int)`** · yeni `OnQuakeStarted` |
| `Scripts/UI/BoostButton.cs` | `[SerializeField] BoostId _id` — **tek script iki butona da hizmet ediyor**, kopyalanmadı |
| `Scripts/Gameplay/DropController.cs` | `WormBoostDirector.Instance.IsBusy` → `BoostGate.IsAnyBusy` |
| `Scripts/Services/GameOverDetector.cs` | aynı tek satır |
| `Scripts/Services/WormBoostDirector.cs` | `IBoostDirector` implement · `BoostGate` kaydı · `CanArm`'a `!BoostGate.IsAnyBusy` |
| `Scripts/Services/EffectDirector.cs` | `_quakeDust` + `EmitQuakeDust(...)` + `ClearAll`'a ekleme |
| `Scripts/Services/FaceDirector.cs` | `SetQuakeMood(bool)` — hepsi `Surprised`, hepsi zemine bakıyor · `_floorY` cache |
| `Scripts/Services/AudioService.cs` | 2 klip alanı · ayrı `_rumbleSource` · `PlayQuakeCrack/StartQuakeRumble/SetQuakeRumbleLevel/StopQuakeRumble/VibrateOnce` · `OnQuakeStarted` aboneliği |
| `Scripts/Data/GameConfig.cs` | **32 yeni alan**, hepsi Türkçe Tooltip'li |
| `Art/Effects/Mat_QuakeDust.mat` | **yeni** — `Mat_EatSmoke` kopyası (URP ParticlesUnlit + `particle_smoke_02`) |
| Sahne | `Main Camera`'ya `CameraShaker` · `EffectDirector/QuakeDust` (Box shape) · `QuakeBoostDirector` kökü · `HUDCanvas/BoostSlot_Quake` |

**Sıralama katmanları:** düşen moloz **-4** (Background -10 üstü, meyveler 90-100 **altı** —
yığının arkasından düşüyor) · toz **200**. Ekranda duran sprite olmadığı için başka katman yok.

### Performans — denetlenmiş, tahmin değil

`QuakeBoostDirector`, `CameraShaker`, `BoostGate`, `EffectDirector`, `BoostButton`, `FaceDirector`
üzerinde grep denetimi:

| Kontrol | Sonuç |
|---|---|
| `FindObjectOfType` / `FindObjectsByType` / `GameObject.Find` | **hiç yok** (tek eşleşme FaceDirector'deki bir yorum satırı) |
| `GetComponent` | sadece `BoostButton.Awake`'te 2 kez — runtime'da hiç yok |
| LINQ | **hiç yok** |
| Coroutine | **hiç yok** — tüm zamanlama float sayaç |
| `foreach` | **hiç yok** — hepsi `for` + index. `IReadOnlyList<T>` üzerinde `foreach` enumerator box'lardı |
| Heap allocation (`new List`/dizi/string birleştirme) | sıcak yolda **hiç yok**. Tek string birleştirmeler `[Tooltip]` attribute'larında (derleme zamanı sabiti) ve iki `Debug.LogWarning`'de (sadece hata yolunda) |

Boştaki maliyet: `QuakeBoostDirector.Update` **tek enum karşılaştırmasıyla** çıkıyor
(`_state == State.Idle`), `FixedUpdate` da öyle (`_state != State.Shake`),
`CameraShaker.LateUpdate` `_rumble<=0 && _punchTimer<=0` ile. `EffectDirector`'ün **hiç `Update`'i
yok** (Emit ile sürülüyor), `BoostButton`'ın da yok (olay güdümlü). Meyvelere yeni `Update`
eklenmedi.

Sarsıntı sırasındaki maliyet: itme döngüsü saniyede ~16 kez, meyve başına bir
`Mathf.PerlinNoise` + bir `Mathf.Repeat` + bir `Random.insideUnitCircle` (üçü de allocation
yapmıyor). Yön tohumu için `f.DropTime` seçildi — `GetInstanceID()` native bir çağrı olurdu.
Toz/moloz emisyonu kare başına en fazla 4 `Emit` çağrısı, hepsi `EmitParams` struct'ıyla.

`SetQuakeLookDirection` API'si 5. turda kaldırıldı: bakış artık meyvenin kendi hızından
okunduğu için director ile FaceDirector arasında kare-başı bir bağ kalmadı.

> GC allocation'ın **0 B/kare** olduğunu Profiler'da doğrulamak §8/27'de duruyor — statik
> denetim allocation kaynağı göstermiyor ama ölçüm ölçümdür.

**Bilinçli olarak yapılmayanlar**
- **Moloz parçacıkları yok.** `quake_pebble.png` üretildi ve import edildi ama **kullanılmıyor** —
  ayrı bir `ParticleSystem` + materyal + `EmitParams.velocity` ile arklı fırlatma gerektiriyor.
  Uygulama sırasının 10. (opsiyonel cila) adımı. `worms_bite_hole` da aynı durumda.
  Önce itme parametreleri elle ayarlanmalı; o ayar hangi cilanın gerektiğini de değiştirir.
- **`bomb_shockwave` halkası yok** (opsiyonel cila).
- **Envanter kaydedilmiyor** — her oyunda `quakeChargesPerRun` (2) sıfırlanıyor; `SaveService` v3
  migrasyonu yapılmadı (worms da öyle bırakılmıştı).
- **`BoostDefinition`/`BoostDatabase` SO'ları ve `hud_boost_tray` tepsisi yok.** §7.1 sadece
  çoğalacak olan iki şeyi tekilleştirdi; tam Faz 8 altyapısı ayrı bir iş.
- **Cooldown yok** — worms gibi sadece kullanım sayısı.
- **Moloz parçacıkları ve `bomb_shockwave` halkası yok** (opsiyonel cila).

---

## 1. Çekirdek mekanik — itme (kick)

Her `quakeKickInterval`'de `FruitPool.Instance.Active` üzerinde tek `for` döngüsü:

```
atla: f == null · !f.IsDropped (daldaki bekleyen meyve) · f.IsMerging
scale  = Lerp(quakeKickScaleSmall, quakeKickScaleBig, tierT)
body.WakeUp();
v.x += Random(-quakeKickX, +quakeKickX) * env * scale
v.y += (Random(-quakeKickY, +quakeKickY) + quakeKickUpBias) * env * scale
if (v.sqrMagnitude > quakeMaxSpeed²) v = v.normalized * quakeMaxSpeed
body.linearVelocity   = v
body.angularVelocity += Random(-quakeKickSpin, +quakeKickSpin) * env * scale
```

Her satır projede ölçülmüş bir gerçeğe dayanıyor:

| Karar | Neden |
|---|---|
| **`body.WakeUp()` şart** | `m_TimeToSleep = 0.5` — yerleşmiş yığın yarım saniyede uyuyor, uyuyan `Rigidbody2D`'ye yazılan hız güvenilir uygulanmıyor. Projede daha önce **hiç** `WakeUp`/`AddForce` çağrısı yoktu |
| **`AddForce(Impulse)` değil, doğrudan hız artışı** | Kütleler 0.5 (kiraz) → 10 (karpuz), **20 kat**. Impulse kirazı fırlatır, karpuzu kıpırdatmaz |
| **`quakeKickUpBias`** | Yerçekimi döndürülmediği için sıkışık yığında **boşluk açan tek şey bu**. 0 yaparsan meyveler sadece titrer, birbirinin arasına giremez → merge olmaz |
| **`quakeMaxSpeed = 6`** | ⚠️ **Tavan collider'ı yok.** Duvar tepeleri `y=+4.60`, danger line `y=2.12`. Duvar tepesine oturan meyve durgun + çizgi üstünde = 3 sn sonra garantili kayıp. Zeminden (`y=-2.81`) tepeye çıkmak ~5.9 birim/sn gerektiriyor; clamp bunu imkânsız kılıyor. İtmeler de birikemiyor: 0.12 sn'de yerçekimi 1.18 birim/sn yukarı hız siliyor, tek itme en fazla 0.45 ekliyor |
| **Başta `Continuous`** | Duvarlar sadece **0.3 birim** kalın. `ArmFruitsForShaking()` bir kez yazıyor; geri `Discrete`'e düşürmeyi `Fruit.FixedUpdate` (`continuousExitFrames`) zaten kendisi yapıyor — yeni state tutulmuyor |
| **`FixedUpdate`'te** | Fizik yazması fizik adımına ait. Timestep 0.02 (50 Hz) → 0.12 sn = 6 adım |

**`Update` ↔ `FixedUpdate` ayrımı:** `Update` görsel (kamera genliği, gürültü sesi, toz, moloz,
levha alfası, faz sayaçları), `FixedUpdate` sadece itme.

### Merge'ler neden kendiliğinden oluyor

`Fruit.OnCollisionStay2D → TryRequestMerge` her fizik adımında zaten çalışıyor; tek şartı iki
meyvenin de `IsDropped` olması ve `Definition` eşitliği. `MergeHandler.LateUpdate` kuyruğu kare
başına 100'e kadar boşaltıyor. **Deprem için tek satır merge kodu yazılmadı.**

Bilinen iki keskin kenar (deprem bunları zorlayacak, ikisi de zararsız):
`MergeHandler._queuedPairs` sadece kuyruk tamamen boşalınca temizleniyor · aynı tipten 3'lü küme
kare başına tek çift çözülüyor, üçüncüsü bir sonraki `OnCollisionStay2D`'de yeniden doğuyor.

---

## 2. Kamera sarsıntısı

`Scripts/Services/CameraShaker.cs`, `Main Camera`'da. Boost'un içine gömülmedi çünkü ileride merge
darbesi ve oyun sonu da kullanacak.

- Dinlenme `localPosition` **(0, 0.5, -10)** `Awake`'te cache'leniyor, bitişte **birebir** geri
  yazılıyor. Lerp ile yaklaşmak kamerayı kalıcı olarak binde bir kaydırırdı.
- Ofset `Mathf.PerlinNoise`'dan — iki ayrı gürültü hattı (aynı hattan iki örnek alsak kamera
  köşegen bir çizgide gidip gelirdi). Perlin komşu kareler arasında sürekli;
  `Random.insideUnitCircle` her karede zıplayıp epileptik görünüyor. Allocation yok.
- `SetRumble` her karede yazılmalı; **iki kare yazılmazsa sarsıntı kendiliğinden ölüyor**
  (`_rumbleFrame` kontrolü). Bir director çökse bile kamera sonsuza kadar titrer kalmıyor.
- `LateUpdate` — bütün oynanış `Update`'leri bittikten sonra, kamera işinin geleneksel yeri.
- **Kamera dönmüyor**, yalnızca X/Y ötelemesi (kullanıcının kararı: kutu eğilmiyor).
- **HUD sarsılmaz** — `HUDCanvas` ayrı bir Canvas, kameranın child'ı değil.
- Arka plan kenarı görünmez: `Background` sprite'ı **8.64 × 18.21** birim, görünür alan ~6.19 × 11.

---

## 3. Görsel envanteri

### 3A. Zaten var — **üretmeye gerek yok** ✅

| İş | Kullanılan mevcut asset | Not |
|---|---|---|
| **Toz bulutu** | `Effects/Particles/particle_smoke_02.png` (384²) | `Mat_QuakeDust.mat` üzerinden. Art gri/beyaz, sıcak ton `quakeDustColor` (#D9C7A8) ile geliyor |
| **Çatlama anı çakması** | `Boosts/Effects/hammer_impact_star.png` (1280²) | ✅ zaten tek sprite, bağlandı |
| Buton durumları | `boost_glow_ring`, `boost_cooldown_mask`, `boost_badge_count` | `BoostButton` runtime'da bindiriyor |
| Meyve yüzleri | `face_surprised_*` | `FaceDirector.SetQuakeMood` |
| **Moloz** (opsiyonel) | `particle_shard_01..04.png` (224²) | kahverengiye boyanınca taş okunuyor |
| **Şok halkası** (opsiyonel) | `Boosts/Effects/bomb_shockwave.png` (2560²) | ✅ zaten tek sprite |
| **Duvar çatlağı** (opsiyonel) | `Boosts/Effects/hammer_crack_01..03.png` (1024²) | ⚠️ **`Multiple` modda, 4 alt-sprite'a dilimlenmiş** — kullanılacaksa önce `Single`'a çevir |

### 3B. Üretilecek — **4 zorunlu**

Teslim yolu: kök dizinde **`/Earthquake/{Icons,States,Effects,Audio}/`** (worms'ta `/Worms/` böyle
yapıldı, klasörler hazır), sonra `Assets/FruitMerge/Art/Boosts/...` altına kopyalanır.

| # | Dosya | Hedef klasör | Teslim | Ne |
|---|---|---|---|---|
| 1 ⭐ | `boost_earthquake.png` | `Boosts/Icons/` | **640×640** | Krem rozetli boost ikonu (HUD butonu bunu kullanacak) |
| 2 | `boost_earthquake_available.png` | `Boosts/States/` | **640×640** | Rozetsiz çıplak obje |
| 3 | `quake_warning_sign.png` | `Boosts/Effects/` | **512×512** | Ekran ortasında çakan uyarı levhası |
| 4 | `quake_ground_crack.png` | `Boosts/Effects/` | **2048×512** | Zemin çizgisi boyunca açılan yarık |

`_active` / `_cooldown` / `_disabled` / `_locked` **üretilmiyor** — worms kararı S4 geçerli.

### 3C. Ses — **2 klip**

| Dosya | Ne |
|---|---|
| `Audio/quake_crack.wav` | t=0'daki keskin zemin çatlaması / tok darbe (~0.3 sn) |
| `Audio/quake_rumble.wav` | **Döngülenebilir** alçak gürültü. Süresi önemli değil (1-2 sn yeter) — loop'lanıyor ve sesi sarsıntı zarfından sürülüyor |

Import: SFX ayarı (Decompress On Load + PCM), müzik ayarı **değil**.

---

## 4. AI görsel prompt'ları

> Prompt'lar **İngilizce** — görsel modelleri İngilizce'de belirgin şekilde daha iyi.
> Renkler mevcut art'tan **piksel örneklenerek** çıkarıldı, tahmin değil.

### 4.0 Tutarlılık taktiği

1. **Önce `boost_earthquake.png`'i üret** (PROMPT 1), beğenene kadar tekrarla. Bu senin referansın.
2. PROMPT 2'de o görseli **reference image olarak ekle** (Nano Banana / GPT-Image: dosyayı yükle ·
   Midjourney: `--cref` · Flux: Redux).
3. PROMPT 3 ve 4 bağımsız objeler, referans gerekmiyor.

### 4.1 Ortak stil bloğu (her prompt'un sonuna yapıştır)

```
STYLE: 2D mobile casual game sprite, flat vector cartoon, thick uniform dark brown
outline #48270E about 3% of the canvas width, simple cel shading with exactly two tones
per surface (a base tone plus one darker shade), one soft white elliptical gloss highlight
in the upper-left, no texture, no noise, crisp clean edges, fully transparent background
with real alpha, straight-on orthographic view, bright cheerful children's mobile game art
in the style of Suika / Watermelon Game fruit sprites.
```

### 4.2 Ortak negatif prompt

```
NEGATIVE: background, sky, drop shadow, cast shadow, photorealism, 3D render, glossy
airbrushed gradients, specular sheen, pixel art, text, letters, numbers, watermark,
signature, frame, border, blurry soft edges, color fringing, rubble realism, gore,
destruction horror, collapsing buildings, fire, blood, dirt smears, mold
```

> `glossy airbrushed gradients, specular sheen` negatif listede **bilerek** var: projedeki 75 adet
> eski `Boosts/States/boost_*_{active,cooldown,...}.png` dosyası tam o stilde üretilmiş ve meyvelere
> hiç uymuyor. Aynı hataya düşmemek için.

### 4.3 Renk paleti (mevcut art'tan örneklendi)

| Rol | Hex | Kaynak |
|---|---|---|
| Kontur (her şey) | `#48270E` | `fruit_04_orange` konturu |
| Rozet krem dolgu | `#FEFAEE` | `boost_bomb` örneklendi |
| Rozet iç altın halka | `#D7B273` | `boost_bomb` örneklendi |
| Duvar / ekran zemini | `#FEEFB4` | `GameConfig.screenBackgroundColor` |
| Toprak / ahşap ana ton | `#C89A5B` | `bg_game.png` süpürgeliği |
| Toprak gölge tonu | `#A87940` | |
| Yarık içi (koyu) | `#3A2410` | |
| Toz ana ton | `#D9C7A8` | `GameConfig.quakeDustColor` |
| Toz gölge tonu | `#B9A484` | |
| Uyarı amber | `#FFC93C` | worms planındaki parıltı sarısı |
| Uyarı amber gölge | `#E8A81E` | |
| Portakal | `#FF8100` / `#D96A00` | `fruit_04_orange` |
| Kiraz kırmızısı | `#E8402E` / `#B62D20` | |

**Rozet geometrisi** — `boost_bomb.png`'den ölçüldü (alt-sprite rect'i 589×606 / 640×640 = %92):
disk çapı tuvalin **%91**'i · dış kahverengi halka **%2** kalın · altın saç halka **%1** kalın ve
dış kenardan **%4.2** içeride.

---

### PROMPT 1 — `boost_earthquake.png` ⭐ önce bunu üret

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
The lower third of the cream area is a horizontal strip of cartoon ground: base color
warm tan #C89A5B with a #A87940 darker shade along its lower edge, capped by the thick
dark brown #48270E outline. A single bold jagged crack splits this ground strip open in
the middle, running from the ground surface downward in a zigzag, with two shorter
branch cracks. The inside of the crack is solid dark #3A2410. The two ground halves are
tilted very slightly apart, one a little higher than the other.

Above the crack, two round cartoon fruits are bouncing up in the air: a bright orange
#FF8100 orange with a #D96A00 shade on its lower right and one small green leaf, and a
red #E8402E cherry-apple with a #B62D20 shade and a short brown stem. Both have the same
thick dark brown outline and one white gloss highlight in the upper-left. They are
clearly airborne, not touching the ground.

Under each fruit, two short curved dark brown motion arcs show it hopping. Three small
warm dust puffs (#D9C7A8 with #B9A484 shading, thick brown outline, round cartoon cloud
shapes) rise from the crack. On the far left and far right of the cream area, three
short horizontal dark brown tremor lines each, stacked, showing the whole thing shaking
sideways.

Everything is fully inside the cream area with a comfortable margin - nothing touches or
crosses the golden ring. Everything drawn with the same thick dark brown #48270E outline.

[STYLE BLOCK]
[NEGATIVE]
```
Üret: **1280×1280** → teslim: **640×640**

---

### PROMPT 2 — `boost_earthquake_available.png`

> `boost_earthquake.png`'i reference image olarak ekle.

```
The exact same cracked-ground-with-bouncing-fruits subject as the reference image, but
WITHOUT the circular badge: no cream disc, no brown ring, no golden ring. Only the
cracked ground strip, the two airborne fruits, the motion arcs, the dust puffs and the
tremor lines, floating on a fully transparent background, centered, scaled to fill about
78% of the canvas.

Identical ground, identical fruits, identical colors, identical outline weight, identical
shading and highlights as the reference.

[STYLE BLOCK]
[NEGATIVE]
```
Üret: **1280×1280** → teslim: **640×640**

---

### PROMPT 3 — `quake_warning_sign.png`

```
A single cute cartoon warning sign, isolated on transparent background, centered.

SHAPE: an equilateral triangle standing on its base with strongly ROUNDED corners, like
a soft chunky sticker. Its width is exactly 62% of the image width and it is centered.
Fill: bright amber #FFC93C, with a #E8A81E darker shade along the lower-right inner edge.
A thick dark brown #48270E outline around the whole triangle, plus a second thin brown
inset line drawn just inside the outline, parallel to it, like a printed border on a road
sign. One soft white elliptical gloss highlight on the upper-left face of the triangle.

GLYPH INSIDE THE TRIANGLE: a bold dark brown #48270E exclamation mark, centered, its
height about 55% of the triangle's height - a thick tapered vertical stroke with rounded
ends and a separate round dot below it. Nothing else inside the triangle.

FLANKING THE TRIANGLE: on the left side, three short curved dark brown #48270E tremor
arcs, stacked vertically, curving away from the triangle; on the right side, the same
three arcs mirrored. They sit just outside the triangle outline with a small gap and do
not touch it. The arcs get shorter as they go outward.

Nothing else in the image. No ground, no fruit, no cracks, no lightning bolt.

[STYLE BLOCK]
[NEGATIVE]
```
Üret: **1024×1024** → teslim: **512×512**

---

### PROMPT 4 — `quake_ground_crack.png`

> Bu dosya **yatay bir şerit**, tuval oranı **4:1**. Kod bunu zemin yüzeyinin (`y = -2.81`) hemen
> altına, ekran genişliğinden biraz taşacak şekilde koyuyor — bu yüzden **sol ve sağ kenardan
> taşarak bitmesi gerekiyor**, uçlarında kapak/bitiş olmamalı.

```
A wide horizontal strip of cracked cartoon ground, isolated on transparent background.
The canvas is a wide 4:1 landscape banner.

LAYOUT:
- The top 8% of the canvas is the flat ground SURFACE line: a straight horizontal band of
  warm tan #C89A5B capped by a thick dark brown #48270E outline running the full width of
  the canvas, edge to edge. It must run off both the left and the right edge of the canvas
  with no end cap and no fade - it continues beyond the frame.
- Below it, the middle 60% of the canvas is the ground body: warm tan #C89A5B with a
  #A87940 darker shade, spanning the full width edge to edge.
- The bottom 25% of the canvas fades smoothly to fully transparent, so the ground has no
  hard bottom edge.

THE CRACK: one bold, dramatic jagged fissure splitting the ground open, its widest point
in the horizontal CENTER of the canvas. It starts as a wide gap at the ground surface
line and zigzags downward, narrowing, until it disappears into the transparent lower area.
Three shorter branch cracks fork off it - one to the upper left, two to the right - each a
thin sharp zigzag line in dark brown #48270E. The inside of the main fissure is solid dark
#3A2410, outlined in #48270E.

Two more, much smaller and thinner hairline zigzag cracks appear in the ground surface,
one at about 22% and one at about 78% of the canvas width, each only reaching a short way
down. They are just cracks in the surface, not open gaps.

DETAIL: four or five small chunky pebbles (rounded pentagon shapes, #C89A5B with #A87940
shading and thick brown outline) sit displaced just above the ground surface line near the
main fissure, as if they were just jolted loose. Two tiny warm #D9C7A8 dust puffs escape
from the main fissure.

Nothing else. No grass, no plants, no fruit, no buildings, no sky, no lava, no water.

[STYLE BLOCK]
[NEGATIVE]
```
Üret: **2048×512** → teslim: **2048×512** (küçültme yok)

---

### Opsiyonel PROMPT 5 — `quake_dust_puff.png` (v1'de yok)

```
A single soft cartoon dust cloud, isolated on transparent background, centered, filling
about 80% of the canvas. A rounded lumpy cloud shape made of four or five overlapping
bumps. Base color warm dusty beige #D9C7A8 with one darker #B9A484 shade along the
lower-right. Thick dark brown #48270E outline around the whole cloud silhouette only -
no internal outlines. One soft white elliptical gloss highlight upper-left. Nothing else.

[STYLE BLOCK]
[NEGATIVE]
```
Üret: 768×768 → teslim: **384×384**

### Opsiyonel PROMPT 6 — `quake_pebble.png` (v1'de yok)

```
A single small cartoon stone pebble, isolated on transparent background, centered,
filling about 65% of the canvas. A chunky rounded pentagon shape with slightly irregular
faces. Base color warm tan #C89A5B with one darker #A87940 shade on the lower-right third.
Thick dark brown #48270E outline. One soft white elliptical gloss highlight upper-left.
No face, no eyes, no cracks, no moss.

[STYLE BLOCK]
[NEGATIVE]
```
Üret: 512×512 → teslim: **256×256**

---

## 4.5 ✅ Teslim edilen art — ölçüm ve düzeltmeler

Hepsi `/Earthquake/{Icons,States,Effects,Audio}/` altına teslim edildi ve import edildi.
**Orijinaller staging klasöründe olduğu gibi duruyor**; düzeltmeler sadece `Assets/` kopyasına
uygulandı.

| Dosya | Boyut | Alfa içeriği (tuvale oran) | Spec | Durum |
|---|---|---|---|---|
| `boost_earthquake.png` | 640×640 | %89.7 × %91.4 | rozet %91 | ✅ tam |
| `boost_earthquake_available.png` | 640×640 | %77.3 × %78.1 | %78 | ✅ tam |
| `quake_warning_sign.png` | 512×512 | %79.1 × %57.4 | üçgen %62 + yan yay | ✅ tam |
| `quake_dust_puff.png` | 384×384 | %80.2 × %79.2 | %80 | ✅ tam |
| `quake_pebble.png` | 256×256 | %65.6 × %64.8 | %65 | ✅ tam (kullanılmıyor) |
| `quake_ground_crack.png` | 2048×512 | y 6..432, tam genişlik bleed | — | ⚠️ **2 düzeltme** |
| `quake_crack.wav` | 0.48 sn | — | ~0.3 sn | ✅ |
| `quake_rumble.wav` | 2.25 sn | — | döngülenebilir | ✅ |

**Import sonrası doğrulama:** 6 PNG'nin hepsi `Sprite Mode: Single` ve **sprite sayısı = 1**
(dilimleyici tuzağı yok). Dünya boyları: yarık **8.0 × 2.0**, levha **1.0 × 1.0**.
Ses: `DecompressOnLoad` + `PCM` + `forceToMono` (2D SFX'te stereo boşa bellek).

### `quake_ground_crack.png` — iki üretici artefaktı düzeltildi

Piksel taramasıyla ölçüldü, önizlemeye güvenilmedi:

1. **Pembe kayma.** Zemin gövdesi yüzeyde doğru tan (`197,141,83`) başlıyor ama aşağı doğru
   somona kayıyordu (y=341'de `202,101,111`). Palette olmayan bir renk — negatif prompt'a rağmen
   "gün batımı" sızması.
2. **Gri tül.** y≥342'de tamamen doygunluksuz gri bir katman (`~95,96,95`, ton farkı 1) alfası
   131'den 0'a inerek devam ediyordu. Zeminin altında kirli bir pus olarak görünecekti.

**Düzeltme:** her pikselin **parlaklığı (V) korunarak** ton ve doygunluğu paletin tan rampasına
(`#C58D51` → `#A87940`) çekildi, artı derine doğru %18 hafif koyulaşma. Bu yaklaşımın önemi:
koyu çatlak içi (düşük V) koyu kahve kalıyor, kenarlar/şekiller hiç bozulmuyor, gri tül de
zemin renginde bir alfa geçişine dönüşüyor. 760.098 piksel yeniden renklendirildi; şekil,
çatlak geometrisi, dallanmalar, taşlar ve toz pufları **hiç dokunulmadı**.

> Yarığın yüzey çizgisinde projenin imzası olan **kalın koyu kontur yok** — art düz bir tan
> kenarla bitiyor. Oyunda ahşap süpürgeliğe dayandığı için sorun olmayabilir; Play Mode'da
> görüp karar ver. İstersen konturu ekleyebilirim ya da o tek dosyayı §4 PROMPT 4 ile
> yeniden üretebilirsin.

### Ölçümden çıkan kod değişikliği

- **`Mat_QuakeDust` dokusu `particle_smoke_02` → `quake_dust_puff`**, ve `quakeDustColor`
  **beyaza** çekildi. Doku zaten sıcak bej (`#CCB28C`); üstüne `#D9C7A8` tint binince iki tint
  çarpılıp toz çamur rengine (`#AD8A5C`) düşüyordu.
- **`Mat_QuakeRubble`** oluşturuldu (`Mat_QuakeDust` kopyası + `quake_pebble` dokusu) — düşen
  moloz için. `quakeRubbleColor` da beyaz, aynı gerekçeyle.

> **Yarık artık kullanılmıyor.** Yukarıdaki ton düzeltmesi ve yerleşim ölçümü (`y=58` → zemine
> göre `-0.773` ofset) yapıldıktan sonra oyunda görüldü ve **istenmedi**; efekt kaldırıldı.
> Dosya `Assets/FruitMerge/Art/Boosts/Effects/quake_ground_crack.png` olarak duruyor.
> İleride "zemin çatlıyor" beat'i geri istenirse art hazır ve ölçümleri bu bölümde kayıtlı.

---

## 5. Import ayarları — projede **dört kez** aynı tuzağa düşüldü

| Ayar | `boost_earthquake*` | `quake_warning_sign` | `quake_ground_crack` |
|---|---|---|---|
| **Sprite Mode** | **Single** ⚠️ | **Single** ⚠️ | **Single** ⚠️ |
| Pixels Per Unit | 100 (varsayılan, UI) | **512** → 1.0 birim | **256** → 8.0 × 2.0 birim |
| Pivot | Center | Center | Center |
| Mesh Type | Full Rect | Full Rect | Full Rect |
| Compression | Uncompressed | Uncompressed | Uncompressed |
| Filter Mode | Bilinear | Bilinear | Bilinear |
| Max Size (Default) | 1024 | 512 | 2048 |

⚠️ **`Sprite Mode: Single` kritik.** Unity'nin otomatik dilimleyicisi kopuk şekilleri (titreme
çizgileri, toz pufları, molozlar, ünlem işaretinin noktası, dallanan çatlaklar) ayrı sprite'lara
bölüyor — `merge_burst_06` bu yüzden **52 parçaya** bölünmüştü, `hammer_crack_01` şu an 4 parça.
**İçe aktardıktan sonra kontrol et: dosya sayısı = sprite sayısı mı?**

**Bağlanan alanlar** (hepsi bağlı): `QuakeBoostDirector._warnSignSprite` ← `quake_warning_sign` ·
`._impactStarSprite` ← `hammer_impact_star` · `HUDCanvas/BoostSlot_Quake`'in `Image.sprite` ←
`boost_earthquake` · `AudioService._quakeCrackSfx/_quakeRumbleSfx` · `EffectDirector._quakeDust/_quakeRubble`.

**Kullanılmayan dosyalar:** `quake_ground_crack.png` (efekt istenmedi).

**Atlas:** `Art/Boosts/**` ve `Art/Effects/**` hiçbir atlasta değil — `BoostAtlas` planlandı ama
hâlâ yok. Bu 4 dosya da atlassız gidiyor; ayrı bir temizlik işi, depremi bloklamıyor.

---

## 6. `GameConfig` alanları (32 adet, hepsi Türkçe Tooltip'li)

```
boost — deprem / zamanlama
  quakeShakeDuration      = 3.5     titreşim — küçük dokunuşlarla çalıştığı için uzun olmalı
  quakeSettleDuration     = 0.8     yatışma — sönümleme kalkıyor, meyveler ASIL burada oturuyor

boost — deprem / itme      (6. turda son hali)
  quakeKickInterval       = 0.06    fizik adımı 0.02 → 3 adımda bir
  quakeKickIntervalJitter = 0.015   ritim makine gibi durmasın
  quakeKickStrength       = 3.20    itmenin BÜYÜKLÜĞÜ (ilerleme mesafesinin ana parametresi)
  quakeKickDirectionSlots = 4       ⭐ süre kaç yön dilimine bölünsün (2.5/4 = 0.625 sn)
  quakeKickVerticalScale  = 0.60    AŞAĞI bileşenin gücü (yön asla yukarı bakmıyor)
  quakeKickJitterRatio    = 0.35    yöne eklenen saf gürültü oranı (pürüz)
  quakeKickRestSpeed      = 1.00    bundan HIZLI olan itilmiyor (ama yukarı hızı yine kırpılıyor)
  quakeMaxRiseSpeed       = 0.60    ⭐ YUKARI TAVANI — en fazla 0.018 birim yükselme
  quakeKickScaleSmall     = 1.00    kirazdaki çarpan
  quakeKickScaleBig       = 0.75    karpuzdaki çarpan
  quakeMaxSpeed           = 4.5     toplam hız emniyet ağı (duvara çok sert vurmasın)
  quakeLookMinSpeed       = 0.35    bu hızın altındaki meyve düz bakıyor (göz titremesin)

boost — deprem / düşen moloz
  quakeRubbleRate         = 14      parça/sn
  quakeRubbleLifetime     = 1.45    zemine varmadan sönecek kadar
  quakeRubbleSize         = 0.14    kiraz yarıçapı 0.19 — molozdan küçük olmamalı
  quakeRubbleColor        = BEYAZ   quake_pebble dokusu zaten toprak renginde
  quakeRubbleEdgeInset    = 0.25    duvarın iç yüzünden ne kadar içeride
  quakeRubbleSpawnSpread  = 1.8     dikey yayılım — sıra sıra düşmesin
  quakeRubbleSpawnYOffset = 3.0     dropY'ye göre; şerit Y 5.90..7.70 = kamera üstü (6.00)
  quakeRubbleSortingOrder = -4      meyvelerin (90+) ARKASINDAN düşsün
  quakeAttackTime         = 0.15    zarf uyarı seviyesinden 1'e
  quakeReleaseTime        = 0.40    zarf 1'den 0'a (ani duruş olmasın)

boost — deprem / kamera
  quakeShakeAmplitude     = 0.12    en büyük öteleme (dünya birimi)
  quakeShakeFrequency     = 14      Perlin hızı (Hz)
  quakeStartPunch         = 0.70    ilk andaki tek seferlik darbe

boost — deprem / toz      (zemin + İKİ DUVAR = üç şerit)
  quakeDustRate           = 70      parçacık/sn, üç şeride bölünüyor
  quakeDustWallShare      = 0.45    yüzde kaçı duvarlardan (kalanı zeminden)
  quakeDustWallHeight     = 5.0     duvar şeridinin yüksekliği (zeminden yukarı)
  quakeDustWallInset      = 0.15    duvarın iç yüzünden ne kadar içeride
  quakeDustLifetime       = 0.9     < quakeSettleDuration olmalı
  quakeDustSize           = 0.55
  quakeDustAlpha          = 0.55
  quakeDustColor          = BEYAZ   doku zaten sıcak bej — tint çift boyama yapıyordu
  quakeDustSpawnLift      = 0.12    zemin yüzeyinin ne kadar üstünden doğsun

(uyarı levhası ve çakan yıldız alanları 2. turda tamamen SİLİNDİ — ikisi de istenmedi)

boost — deprem / envanter
  quakeChargesPerRun      = 2       -1 = sınırsız (test)
```

---

## 7. Kurallara uyum

| Kural | Nasıl uyuluyor |
|---|---|
| 1 Lambda yasağı | Tüm abonelikler isimli metot |
| 2 `+=`/`-=` simetrisi | `QuakeBoostDirector`, `BoostButton`, `AudioService` `OnEnable`/`OnDisable`; `BoostGate.Register`↔`Unregister` |
| 3 volume clamp | `SetQuakeRumbleLevel` üç kez `Clamp01`'den geçiyor |
| 4 UI `unscaledTime` | Deprem oyunu durdurmuyor (`timeScale` 1) → normal `deltaTime`. Pause'da donması **isteniyor** |
| 5 Null guard | `EffectDirector`, `FruitPool`, `CameraShaker`, `AudioService`, `FaceDirector`, `SaveService`, `GameManager` — hepsi `Instance != null` kontrollü |
| 6 Sihirli sayı yok | 32 alan, hepsi Tooltip'li |
| 7 Tek `Update` | `QuakeBoostDirector` tek `Update` + tek `FixedUpdate`, `CameraShaker` tek `LateUpdate`; üçü de idle'da erken çıkıyor. Meyvelerde yeni `Update` yok |
| 8 Coroutine yok | Tüm zamanlama `float` sayaç |
| 9 `sprite` sadece değişince | Yarık/levha/yıldız sprite'ı `Start`'ta bir kez; döngüde sadece `color.a` ve `localScale`. `SetAlpha` da `Mathf.Approximately` ile aynı değeri yazmıyor |
| 11 Allocation yok | `for` + index, LINQ yok, `EmitParams` struct, `Mathf.PerlinNoise` allocation yapmıyor, `GetComponent` sadece `Awake`/`Start`'ta |
| 13 Havuz | Oynanış sırasında `Instantiate` yok — director hiç obje yaratmıyor, bütün görsel iş paylaşımlı parçacık sistemlerinde ve kamerada |

---

## 8. Doğrulama — Play Mode elle test adımları

> Play Mode elle sürülüyor. Her adımın beklenen sonucu yazılı.

**A. Altyapı refactor'ü worms'u bozmadı mı**
1. Oyna → worms butonu → bir meyve seç. *Beklenen:* kurtlar gelir, yer, gider; davranış **birebir
   eskisi gibi**, sayaç 3 → 2.
2. Worms çalışırken ekrana dokun. *Beklenen:* meyve bırakılmıyor.
3. Yığını danger line üstüne çıkar, worms çalıştır. *Beklenen:* boost sürerken oyun bitmiyor.

**B. Kamera**
4. Deprem çalıştır, bitmesini bekle. *Beklenen:* Main Camera `Position` **tam olarak (0, 0.5, -10)**.
   Inspector'da kontrol et — 0.0001 sapma bile olmamalı.
5. *Beklenen:* titreme sırasında arka planın kenarı görünmüyor.
6. *Beklenen:* titreme sırasında **HUD kıpırdamıyor** (skor, pause butonu, boost butonları sabit).
   ⚠️ *Kıpırdıyorsa* `HUDCanvas` Screen Space - Overlay değil; söyle, `CameraShaker`'ı ayrı bir
   görsel köke taşırız.

**C. Deprem çekirdeği**
7. 15-20 meyve bırak, yerleşmesini bekle (**5 sn+**, uykuya dalsınlar). Deprem butonuna bas.
   *Beklenen:* **uyuyan meyveler dahil hepsi** titriyor, hiçbiri donuk kalmıyor.
   *Kalıyorsa* `body.WakeUp()` çalışmıyor.
8. *Beklenen:* meyveler **ilk kareden itibaren** oynuyor, 0.3 sn sonra sarsıntı zirveye çıkıyor.
   Hareket hafif değil, **gözle net okunuyor**.
9. *Beklenen:* meyveler kayıyor, çarpışıyor, **zıplıyor** (yığın gözle görülür şekilde çalkalanıyor).
   Kimse kutudan fırlamıyor.
9b. *Beklenen:* ekranın **sağ ve sol kenarından küçük toprak renginde molozlar** yukarıdan aşağıya
   düşüyor, meyvelerin **arkasından** geçiyor ve zemine varmadan sönüyor.
10. *Beklenen:* aynı tipten komşular **kendiliğinden birleşiyor**; merge sesi, meyve suyu ve combo
    popup'ı normal çıkıyor, skor artıyor.
11. *Beklenen:* bitiş **kademeli** — itme ve kamera birlikte yumuşak sönüyor, ani durmuyor.
12. *Beklenen:* bütün meyveler deprem boyunca **`surprised`** ve aşağı bakıyor; deprem bitince
    normale dönüyor, **deprem sürerken uyuklamıyorlar**.
13. Yığını danger line'a dayandır, deprem çalıştır. *Beklenen:* deprem sırasında ve yatışma boyunca
    oyun bitmiyor; meyveler oturduktan sonra normal 3 sn sayacı işliyor.
14. **Tavan testi (kritik):** yığını neredeyse tepeye doldur, depremi 3-4 kez çalıştır.
    *Beklenen:* **hiçbir meyve duvar tepesine (`y ≈ 4.60`) oturmuyor.** Oturursa `quakeMaxSpeed` düşür.
15. **Tünelleme testi:** aynı dolu yığında tekrarla. *Beklenen:* hiçbir meyve kutunun dışına kaçmıyor.
16. Deprem sırasında **Pause**. *Beklenen:* her şey donuyor (kamera dahil, kamera yerine oturuyor),
    Resume'da devam ediyor. Pause panelinde boost butonları gizli.
17. Deprem sırasında **Restart**. *Beklenen:* kamera yerinde, levha gizli, sayaç 2'ye sıfırlanmış.
18. Sayacı tüket (2 kullanım). *Beklenen:* ikon soluyor, buton `interactable = false`.
19. Deprem çalışırken butona tekrar bas. *Beklenen:* hiçbir şey olmuyor.
20. Deprem çalışırken **worms** butonuna bas. *Beklenen:* silahlanmıyor. Tersi de: worms silahlıyken
    deprem başlamıyor.

**D. Art + ses (üretildikten sonra)**
21. Her yeni PNG için Inspector'da **sprite sayısı = 1** mi?
22. *Beklenen:* ekranda **hiçbir sprite belirmiyor** — ünlem işareti yok, zeminde patlama yok,
    zemin yarığı yok. Deprem sadece kamera + itme + toz + moloz + ses ile anlatılıyor.
23. *Beklenen:* konsol **temiz** — özellikle "Particle Velocity curves must all be in the same mode"
    hatası YOK (üç eksen de `TwoConstants`).
24. *Beklenen:* toz zemin boyunca çıkıyor, **sıcak bej** (gri değil), yukarı süzülüyor. Üretim ana
    sarsıntının sonunda (t=2.30) duruyor, son parçacık t=3.20'de ölüyor — yani **boost bittikten
    ~0.3 sn sonra** toz hâlâ dağılıyor. Bu bilerek böyle; istemezsen `quakeDustLifetime`'ı
    `quakeSettleDuration`'ın (0.6) altına indir.
25. *Beklenen:* t=0'da çatlama sesi; gürültü sarsıntıyla birlikte yükselip alçalıyor, sert kesilmiyor.
26. Ayarlar → titreşimi kapat → deprem çalıştır. *Beklenen:* telefon titremiyor.

**E. Performans**
27. Dolu tahtada (40+ meyve) deprem çalıştır, Profiler'a bak.
    *Beklenen:* GC allocation **0 B/kare**; `QuakeBoostDirector.Update` idle'da profilde görünmüyor.

---

## 9. Özet: kaç dosya üreteceksin

| | Dosya |
|---|---|
| Dosya | Durum |
|---|---|
| `boost_earthquake.png` | ✅ kullanılıyor (HUD butonu) |
| `boost_earthquake_available.png` | ✅ import (rozetsiz sürüm, tepsi gelince kullanılacak) |
| `quake_warning_sign.png` | ⛔ **kullanılmıyor** — ünlem işareti istenmedi |
| `quake_dust_puff.png` | ✅ kullanılıyor (`Mat_QuakeDust`) |
| `quake_pebble.png` | ✅ kullanılıyor (`Mat_QuakeRubble`, düşen moloz) |
| `quake_ground_crack.png` | ⛔ **kullanılmıyor** — efekt istenmedi |
| `quake_crack.wav` + `quake_rumble.wav` | ✅ kullanılıyor |
| **Çakma / buton durumları / yüzler** | **0 yeni dosya — mevcut art'tan** |

**✅ 8 dosyadan 6'sı kullanımda.** Deprem: kamera sarsıntısı, sönümlemeli titreşim, üç şeritten
toz (zemin + iki duvar), kenarlardan düşen moloz, çatlama sesi + zarfla senkron gürültü,
titreşim, şaşkın yüzler. Kendiliğinden merge.

Kalan iş: **Play Mode 5. tur + parametrelerin zevke göre ayarı** (§8).

**Ayar rehberi — hangi şikâyet hangi parametre:**

| Şikâyet | Parametre |
|---|---|
| Meyveler yeterince ilerlemiyor | `quakeKickStrength` (3.2) büyüt · veya `quakeKickDirectionSlots` (4) **küçült** — aynı yönde daha uzun giderler |
| Yön yeterince değişmiyor | `quakeKickDirectionSlots` büyüt (yerinde titremeye yaklaşır, aşırıya kaçma) |
| Hareket fazla düzgün / mekanik | `quakeKickJitterRatio` (0.35) büyüt |
| Hâlâ yukarı çıkan meyve var | `quakeMaxRiseSpeed` (0.6) küçült — 0'a çok yaklaştırma, fizik çözücüsü iç içe geçirebilir |
| Aşağı bastırma çok/az | `quakeKickVerticalScale` (0.6) — yatay hareket etkilenmez |
| Gözler titriyor | `quakeLookMinSpeed` (0.35) büyüt |
| Yığın dağılıyor / kaos | `quakeKickStrength` küçült; `quakeKickRestSpeed` (1.0) **büyütmekten kaçın** (tırmanma riski geri gelir) |
| Sarsıntı kısa | `quakeShakeDuration` (4.5) |
| Meyveler boşluklara oturamıyor | `quakeSettleDuration` (0.8) büyüt — sarsıntı bitip fizik serbest kaldığında oturuyorlar |

⚠️ `quakeKickRestSpeed`'i 1.0'ın üstüne çıkarırsan dolu tahtada **tavan testini** (§8/14)
tekrarla: hareket halindeki meyveler de itilmeye başlar ve enerji birikebilir.
