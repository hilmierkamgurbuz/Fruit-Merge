using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Açılış (Splash) ve ana menü ekranının ORTAK krem zemini.
///
/// İki ekranın rengi sahnede ayrı ayrı elle girilseydi biri değiştirilince diğeri
/// unutulur, geçişte "iki farklı ekran" hissi doğardı — kullanıcının şikayeti tam
/// olarak buydu. Renk tek yerde: <see cref="GameConfig.screenBackgroundColor"/>.
///
/// Sahnedeki <c>Image.color</c> de aynı değere ayarlı (Editor'de doğru görünsün diye);
/// bu bileşen sadece çalışma anında bir kez üzerine yazıp ikisini senkron tutuyor.
/// <c>Update</c> yok, tek <c>Awake</c> — performans maliyeti yok sayılır.
/// </summary>
[RequireComponent(typeof(Image))]
[DefaultExecutionOrder(-95)]
public class ScreenBackground : MonoBehaviour
{
    [Tooltip("zemin rengini okuyacağı config")]
    [SerializeField] GameConfig _config;

    void Awake()
    {
        if (_config == null) return;

        Image image = GetComponent<Image>();

        if (image == null) return;

        image.color = _config.screenBackgroundColor;
    }
}
