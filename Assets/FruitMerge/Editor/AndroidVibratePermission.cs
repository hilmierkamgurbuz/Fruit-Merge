using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// Android derlemesine <c>android.permission.VIBRATE</c> iznini ekler.
///
/// <b>Neden gerekli:</b> Unity'nin ürettiği manifest'te bu izin YOK (Editor'ün
/// <c>PlaybackEngines/AndroidPlayer/Apk/UnityManifest.xml</c> dosyası kontrol edildi).
/// İzin olmadan <c>Vibrator.vibrate()</c> çağrısı SecurityException atıyor —
/// <see cref="HapticDevice"/> onu yakalayıp titreşimi sessizce kapatır, yani oyun
/// çalışır ama cihazda hiç titremez. Hata mesajı da sadece logcat'te görünür.
///
/// <b>Neden elle AndroidManifest.xml koymuyoruz:</b> <c>Assets/Plugins/Android/AndroidManifest.xml</c>
/// koymak ANA manifest'i tamamen DEVRALMAK demek — activity/theme/GameActivity bloklarını
/// da bizim taşımamız ve Unity sürümüyle güncel tutmamız gerekirdi. Tek bir izin için
/// Unity'nin ürettiği manifest'e dokunmak daha güvenli.
///
/// <c>#if UNITY_ANDROID</c> ile sarılmadı BİLEREK: <c>IPostGenerateGradleAndroidProject</c>
/// UnityEditor.CoreModule'de yaşıyor, yani hedef platform ne olursa olsun derleniyor.
/// Sarsaydık hata ancak Android'e geçildiği anda ortaya çıkardı.
/// </summary>
class AndroidVibratePermission : IPostGenerateGradleAndroidProject
{
    const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
    const string Permission       = "android.permission.VIBRATE";

    public int callbackOrder => 0;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");

        if (!File.Exists(manifestPath))
        {
            Debug.LogWarning($"[AndroidVibratePermission] Manifest bulunamadı: {manifestPath} — " +
                             "titreşim izni eklenemedi, cihazda titreşim çalışmayacak.");
            return;
        }

        var document = new XmlDocument();

        document.Load(manifestPath);

        XmlElement root = document.DocumentElement;

        if (root == null) return;

        XmlNodeList existing = root.SelectNodes("uses-permission");

        if (existing != null)
        {
            foreach (XmlNode node in existing)
            {
                if (node is XmlElement element &&
                    element.GetAttribute("name", AndroidNamespace) == Permission)
                {
                    return;   // zaten var (ör. başka bir eklenti eklemiş)
                }
            }
        }

        XmlElement added = document.CreateElement("uses-permission");

        added.SetAttribute("name", AndroidNamespace, Permission);

        root.AppendChild(added);

        document.Save(manifestPath);

        Debug.Log("[AndroidVibratePermission] android.permission.VIBRATE manifest'e eklendi.");
    }
}
