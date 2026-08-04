/// <summary>
/// Bir boost director'ünün dışarıya açtığı yüz. <see cref="BoostGate"/> bunu tutuyor,
/// <see cref="BoostButton"/> ve <see cref="GameOverDetector"/> bunun üzerinden konuşuyor —
/// böylece kimse somut bir director tipini (örn. <c>WormBoostDirector</c>) bilmek zorunda kalmıyor.
///
/// Kasıtlı olarak küçük: burada olan her üye en az iki farklı yerden çağrılıyor. Boost'a özel
/// her şey (hedefleme, faz süreleri, efektler) somut director'de kalıyor.
/// </summary>
public interface IBoostDirector
{
    /// <summary>Bu director hangi boost. <see cref="BoostGate"/>'in indeks anahtarı.</summary>
    BoostId Id { get; }

    /// <summary>Boost şu an oynuyor mu (hedef bekleme dahil). Bırakma girdisi ve oyun sonu bunu okuyor.</summary>
    bool IsBusy { get; }

    /// <summary>Hedef bekleniyor mu. Hedefsiz boost'larda her zaman <c>false</c>.</summary>
    bool IsArmed { get; }

    /// <summary>Kalan kullanım. <c>-1</c> = sınırsız, <c>0</c> = bitti.</summary>
    int Charges { get; }

    /// <summary>HUD butonunun girişi. Zaten silahlıysa iptal eder.</summary>
    void Toggle();

    /// <summary>
    /// Kullanım ekler ve yeni durumu yayınlar. Mağazadan satın alma bunu çağırıyor —
    /// <see cref="BoostShopPanel"/> tek script olarak bütün boost'lara hizmet ettiği
    /// için somut director tipini bilmemeli.
    ///
    /// Sınırsız (<c>Charges == -1</c>) bir director'de sessizce yok sayılır.
    /// </summary>
    void AddCharge(int amount);

    // Bilerek YOK: CanArm ve Abort. İkisi de her director'ün KENDİ içinde kullanılıyor
    // (Toggle'ın guard'ı ve OnRunStarted/OnStateChanged temizliği) — arayüzden hiç
    // çağrılmıyorlar. Buraya koymak spekülatif soyutlama olurdu; üçüncü boost gerçekten
    // ihtiyaç duyarsa o zaman eklenir.
}
