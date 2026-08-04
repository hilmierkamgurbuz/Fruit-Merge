using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bir boost'un HUD butonu. <b>Boost başına kopyalanmıyor</b> — hangi boost'a ait olduğunu
/// <see cref="_id"/> alanından öğreniyor, director'e <see cref="BoostGate"/> üzerinden
/// ulaşıyor. Yeni bir boost eklemek = sahnede bu objeyi çoğaltıp <c>_id</c> ile ikonu
/// değiştirmek; yeni script yazmak gerekmiyor.
///
/// Tek kaynaktan besleniyor: <see cref="GameEvents.OnBoostStateChanged"/> hem "hangi boost"
/// hem "silahlı mı" hem "kaç kullanım kaldı" bilgisini birlikte yayınlıyor, böylece buton
/// ayrı olayları birleştirmek zorunda kalmıyor (abone sırasına güvenmek olurdu).
///
/// İkonun sağ alt köşesinde iki rozetten <b>tam olarak biri</b> duruyor:
///  - kullanım varsa <see cref="_countBadge"/> ve içinde kalan sayı,
///  - bittiyse <see cref="_plusBadge"/>. O hâldeyken butona basmak boost'u değil
///    <see cref="BoostShopPanel"/>'i açıyor.
/// Buton bu yüzden kullanım bitince artık DEVRE DIŞI kalmıyor: tıklanabilir kalması
/// mağazaya girişin tek yolu.
///
/// Kural 1 gereği hiçbir abonelikte lambda yok — hepsi isimli metot, hepsinin
/// <c>OnDisable</c>'da birebir karşılığı var (kural 2).
/// </summary>
public class BoostButton : MonoBehaviour
{
    [Header("Kimlik")]
    [Tooltip("Bu buton hangi boost'u tetikliyor. Kendi id'si dışındaki olayları eliyor")]
    [SerializeField] BoostId _id = BoostId.Worms;

    [Header("Referanslar")]
    [SerializeField] Button _button;

    [Tooltip("silahlıyken beliren halka — boost_glow_ring")]
    [SerializeField] GameObject _armedGlow;

    [Tooltip("kullanım bitince ikon bu renge solar")]
    [SerializeField] Image _icon;

    [SerializeField] Color _emptyTint = new Color(1f, 1f, 1f, 0.35f);

    [Header("Rozet")]
    [Tooltip("içi boş rozet — kalan kullanım sayısı burada yazıyor")]
    [SerializeField] GameObject _countBadge;

    [SerializeField] TextMeshProUGUI _countLabel;

    [Tooltip("'+' rozeti — kullanım bitince bunun yerini alıyor, basınca mağaza açılıyor")]
    [SerializeField] GameObject _plusBadge;

    Color _fullTint = Color.white;

    CanvasGroup _group;

    // Tıklamanın boost'u mu mağazayı mı açacağını belirliyor. Olaydan geliyor,
    // her tıklamada director'e sormaya gerek kalmıyor.
    bool _hasCharge = true;

    void Awake()
    {
        if (_button == null) _button = GetComponent<Button>();

        if (_icon != null) _fullTint = _icon.color;

        // Görünürlüğü SetActive ile yönetmiyoruz: kendini kapatan bir bileşen
        // OnDisable'da aboneliğini bırakır ve bir daha asla açılamaz.
        _group = GetComponent<CanvasGroup>();

        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        GameEvents.OnBoostStateChanged += HandleBoostState;
        GameEvents.OnStateChanged      += HandleGameState;

        if (_button != null) _button.onClick.AddListener(HandleClick);
    }

    void OnDisable()
    {
        GameEvents.OnBoostStateChanged -= HandleBoostState;
        GameEvents.OnStateChanged      -= HandleGameState;

        if (_button != null) _button.onClick.RemoveListener(HandleClick);
    }

    void Start()
    {
        // Director Start'ında bir kez yayınlıyor ama sıralama garanti değil —
        // açılış durumunu buradan da bir kez okuyoruz.
        var d = BoostGate.Get(_id);

        HandleBoostState(_id, d != null && d.IsArmed, d != null ? d.Charges : 0);

        SetVisible(GameManager.Instance != null && GameManager.Instance.IsPlaying);
    }

    void HandleClick()
    {
        if (AudioService.Instance != null) AudioService.Instance.PlayUIClick();

        // Kullanım bittiyse tıklama boost'u değil mağazayı açıyor. Paneli doğrudan
        // çağırmıyoruz: buton HUDCanvas'ta, panel PanelCanvas'ta — olayla konuşuyorlar.
        if (!_hasCharge)
        {
            GameEvents.RaiseBoostShopRequested(_id);

            return;
        }

        var d = BoostGate.Get(_id);

        if (d != null) d.Toggle();
    }

    void HandleBoostState(BoostId id, bool armed, int charges)
    {
        // Bütün boost butonları aynı olayı dinliyor — başkasının haberini yut.
        if (id != _id) return;

        if (_armedGlow != null && _armedGlow.activeSelf != armed)
            _armedGlow.SetActive(armed);

        // charges == -1 sınırsız demek (test modu): rozet sayı yerine boş kalıyor
        // ama mağazaya da düşmüyor.
        _hasCharge = charges != 0;

        if (_icon != null)
        {
            Color want = _hasCharge ? _fullTint : _emptyTint;

            if (_icon.color != want) _icon.color = want;
        }

        SetBadges(charges);

        // Buton HER ZAMAN tıklanabilir: kullanım varken boost'u, yokken mağazayı açıyor.
        if (_button != null) _button.interactable = true;
    }

    /// <summary>
    /// İki rozetten birini gösterir. <c>SetActive</c> yalnızca durum DEĞİŞTİYSE
    /// çağrılıyor — aynı değeri tekrar yazmak canvas'ı boş yere yeniden kuruyor (kural 9).
    /// </summary>
    void SetBadges(int charges)
    {
        bool showCount = charges != 0;

        if (_countBadge != null && _countBadge.activeSelf != showCount)
            _countBadge.SetActive(showCount);

        if (_plusBadge != null && _plusBadge.activeSelf == showCount)
            _plusBadge.SetActive(!showCount);

        if (!showCount || _countLabel == null) return;

        // Sınırsız modda rakam anlamsız — sonsuz işareti daha dürüst.
        if (charges < 0) _countLabel.SetText("∞");
        else             _countLabel.SetText("{0}", charges);
    }

    void HandleGameState(GameState s)
    {
        // Boost sadece oynarken anlamlı; menü/pause/sonuç ekranında butonu gizle
        SetVisible(s == GameState.Playing);
    }

    void SetVisible(bool show)
    {
        if (_group == null) return;

        _group.alpha          = show ? 1f : 0f;
        _group.interactable   = show;
        _group.blocksRaycasts = show;
    }
}
