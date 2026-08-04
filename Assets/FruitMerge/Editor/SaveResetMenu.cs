using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Kaydı sıfırlama / inceleme menüsü.
///
/// <b>Neden gerekli:</b> bu proje <c>PlayerPrefs</c> KULLANMIYOR — <see cref="SaveService"/>
/// her şeyi (rekor, coin, ayarlar, istatistik) <c>Application.persistentDataPath/save.json</c>
/// dosyasına yazıyor. Unity'nin <i>Edit ▸ Clear All PlayerPrefs</i> komutu bu yüzden hiçbir
/// şeyi sıfırlamıyor ve "neden hâlâ 190 coin var" sorusuna saatler kaybettiriyor. Menü
/// doğru dosyayı hedefliyor.
/// </summary>
static class SaveResetMenu
{
    const string Menu = "FruitMerge/Kayıt/";

    static string Path => System.IO.Path.Combine(Application.persistentDataPath, "save.json");

    [MenuItem(Menu + "Kaydı Sıfırla (rekor + coin + ayarlar)", false, 0)]
    static void ResetSave()
    {
        string path = Path;

        if (!File.Exists(path))
        {
            EditorUtility.DisplayDialog("Kayıt yok",
                "Sıfırlanacak bir kayıt bulunamadı:\n\n" + path, "Tamam");
            return;
        }

        // Play Mode'da SaveService kaydı BELLEKTE tutuyor ve OnApplicationPause /
        // OnApplicationQuit / OnDestroy'da diske geri yazıyor — dosyayı şimdi silsek
        // oyundan çıkarken aynı değerlerle yeniden doğar. Bu yüzden uyarı sadece
        // bilgilendirme değil, gerçek bir tuzak.
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Önce Play Mode'dan çık",
                "Oyun çalışırken kayıt bellekte duruyor ve Play Mode'dan çıkarken diske " +
                "geri yazılıyor — şimdi silmek işe yaramaz.\n\nPlay Mode'dan çık, sonra " +
                "tekrar dene.", "Tamam");
            return;
        }

        string preview = File.ReadAllText(path);

        if (!EditorUtility.DisplayDialog("Kaydı sıfırla?",
                "Bu dosya SİLİNECEK:\n" + path + "\n\nİçeriği:\n" + preview +
                "\nRekor ve coin geri gelmez.", "Sil", "Vazgeç"))
        {
            return;
        }

        File.Delete(path);

        Debug.Log("[SaveResetMenu] Kayıt silindi: " + path +
                  " — oyun bir sonraki açılışta sıfırdan başlar.");
    }

    [MenuItem(Menu + "Kaydı Konsola Yaz", false, 1)]
    static void PrintSave()
    {
        string path = Path;

        Debug.Log(File.Exists(path)
            ? "[SaveResetMenu] " + path + "\n" + File.ReadAllText(path)
            : "[SaveResetMenu] Kayıt yok: " + path);
    }

    /// <summary>Elle tek bir alanı düzeltmek (ör. sadece coin) için klasörü açar.</summary>
    [MenuItem(Menu + "Kayıt Klasörünü Aç", false, 2)]
    static void RevealSave() => EditorUtility.RevealInFinder(Path);
}
