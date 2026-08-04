/// <summary>
/// Boost kimlikleri. Tek amacı <see cref="BoostGate"/>'in director'leri indeksleyebilmesi ve
/// <see cref="BoostButton"/>'ın "ben hangi boost'un butonuyum" diyebilmesi.
///
/// Sıra ÖNEMLİ değil ama değerler sahnede <c>BoostButton._id</c> alanında serialize ediliyor —
/// aradan bir eleman SİLMEYİN, sonuna ekleyin. Yoksa mevcut butonlar başka bir boost'a bağlanır.
/// </summary>
public enum BoostId
{
    /// <summary>Tatlı kurtçuklar — seçilen tek meyveyi yer.</summary>
    Worms = 0,

    /// <summary>Deprem — tahtanın tamamını sarsar, hiçbir meyveyi silmez.</summary>
    Quake = 1
}
