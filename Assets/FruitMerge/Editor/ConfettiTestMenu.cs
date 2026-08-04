using UnityEditor;
using UnityEngine;

/// <summary>
/// Konfetiyi Play Mode'da anında tetikleyen test menüsü.
///
/// <b>Neden gerekli:</b> konfetinin iki tetikleyicisi de zor tekrarlanıyor — rekor yağmuru
/// için oyunu bitirip REKOR KIRMAK, patlama için karpuza kadar birleştirmek gerekiyor.
/// Yerçekimi/sürtünme/salınım gibi his ayarlarını bir kez görüp karar veremezsin, arka arkaya
/// birkaç kez izlemen gerekiyor. Bu menü o döngüyü saniyeye indiriyor.
///
/// Editor klasöründe olduğu için derlemeye/APK'ya girmiyor.
/// </summary>
static class ConfettiTestMenu
{
    const string Menu = "FruitMerge/Konfeti/";

    [MenuItem(Menu + "Rekor Yağmuru Test", false, 0)]
    static void TestRain()
    {
        ConfettiDirector director = Resolve();

        if (director == null) return;

        director.PlayRain();
    }

    [MenuItem(Menu + "Karpuz Patlaması Test (ekran ortası)", false, 1)]
    static void TestBurst()
    {
        ConfettiDirector director = Resolve();

        if (director == null) return;

        // Karpuz normalde tahtanın ortalarında birleşiyor; test için ekranın ortası yeterince
        // temsili — patlamanın yayılımı doğuş noktasından değil parça başına rastgelelikten geliyor.
        director.PlayBurstAtScreen(new Vector2(Screen.width * 0.5f, Screen.height * 0.45f));
    }

    [MenuItem(Menu + "Havadakileri Temizle", false, 2)]
    static void ClearAll()
    {
        ConfettiDirector director = Resolve();

        if (director == null) return;

        director.ClearAll();
    }

    /// <summary>
    /// Çalışan sahnedeki director. Havuz <c>Awake</c>'te kurulduğu için Play Mode ŞART —
    /// edit mode'da <c>Instance</c> yok ve parça objeleri de yok.
    /// </summary>
    static ConfettiDirector Resolve()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Play Mode gerekiyor",
                "Konfeti havuzu oyun başlarken (Awake) kuruluyor — test için Play Mode'da ol.",
                "Tamam");
            return null;
        }

        if (ConfettiDirector.Instance == null)
        {
            Debug.LogWarning("[ConfettiTestMenu] ConfettiDirector.Instance yok — sahnede " +
                             "ConfettiDirector objesi kapalı ya da silinmiş olabilir.");
            return null;
        }

        return ConfettiDirector.Instance;
    }
}
