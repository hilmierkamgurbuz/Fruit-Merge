// Taptic Engine köprüsü — HapticDevice.cs'in iOS tarafı.
//
// Neden native koda ihtiyaç var: Unity'nin Handheld.Vibrate()'i iOS'ta eski tarz sistem
// titreşimini çalıyor (tüm telefonu sarsan, şiddeti/süresi ayarlanamayan tek bir buzz).
// Bir merge oyununda gereken şey "hafif tık" ile "tok darbe" arasındaki fark, o da yalnızca
// UIImpactFeedbackGenerator ile mümkün.
//
// Üç jeneratör (hafif/orta/sert) BİR KEZ yaratılıp saklanıyor: her darbede yeni jeneratör
// yaratmak hem tahsis hem de motorun ısınmasını beklemek demek — zincirleme birleşmede
// gecikme olarak hissedilir.
//
// Taptic Engine'i olmayan cihazda (iPhone 6 ve öncesi, iPad'ler) çağrılar sessizce hiçbir
// şey yapıyor. Kasıtlı: oradaki tek alternatif olan sistem titreşimi bir merge tıkı için
// çok kaba, hiç titremek daha iyi.

#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator *gLight  = nil;
static UIImpactFeedbackGenerator *gMedium = nil;
static UIImpactFeedbackGenerator *gHeavy  = nil;

static UIImpactFeedbackGenerator *GeneratorForStyle(int style)
{
    switch (style)
    {
        case 0:  return gLight;
        case 1:  return gMedium;
        default: return gHeavy;
    }
}

extern "C" {

/// Jeneratörleri kurar. Haptic API yoksa false döner ve C# tarafı titreşimi tamamen kapatır.
bool FruitMergeHapticsInit(void)
{
    if (NSClassFromString(@"UIImpactFeedbackGenerator") == nil) return false;

    if (gLight == nil)
    {
        gLight  = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
        gMedium = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium];
        gHeavy  = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];

        // Isıtma: hazırlanmamış jeneratörün ilk darbesi ~100 ms gecikiyor ve o gecikme
        // birleşmenin görüntüsünden kopuk hissediliyor.
        [gLight prepare];
        [gMedium prepare];
        [gHeavy prepare];
    }

    return true;
}

/// style: 0 hafif · 1 orta · 2 sert. intensity 0-1 (iOS 13+; öncesinde yok sayılır).
void FruitMergeHapticsImpact(int style, float intensity)
{
    UIImpactFeedbackGenerator *generator = GeneratorForStyle(style);

    if (generator == nil) return;

    if ([generator respondsToSelector:@selector(impactOccurredWithIntensity:)])
    {
        [generator impactOccurredWithIntensity:(CGFloat)intensity];
    }
    else
    {
        [generator impactOccurred];
    }

    // Ard arda gelen darbeler (deprem treni, combo zinciri) için motoru sıcak tut.
    [generator prepare];
}

void FruitMergeHapticsRelease(void)
{
    gLight  = nil;
    gMedium = nil;
    gHeavy  = nil;
}

}
