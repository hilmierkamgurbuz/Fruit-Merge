using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kurtçuk boost'unun HUD butonu. Tek kaynaktan besleniyor:
/// <see cref="GameEvents.OnWormsBoostStateChanged"/> hem "silahlı mı" hem "kaç kullanım
/// kaldı" bilgisini birlikte yayınlıyor, böylece buton iki ayrı olayı birleştirmek
/// zorunda kalmıyor (abone sırasına güvenmek olurdu).
///
/// Kural 1 gereği hiçbir abonelikte lambda yok — hepsi isimli metot, hepsinin
/// <c>OnDisable</c>'da birebir karşılığı var (kural 2).
/// </summary>
public class BoostButton : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField] Button _button;

    [Tooltip("silahlıyken beliren halka — boost_glow_ring")]
    [SerializeField] GameObject _armedGlow;

    [Tooltip("kalan kullanım sayısı. Sınırsızsa (-1) gizlenir")]
    [SerializeField] TextMeshProUGUI _countText;

    [Tooltip("kullanım bitince ikon bu renge solar")]
    [SerializeField] Image _icon;

    [SerializeField] Color _emptyTint = new Color(1f, 1f, 1f, 0.35f);

    Color _fullTint = Color.white;

    CanvasGroup _group;

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
        GameEvents.OnWormsBoostStateChanged += HandleBoostState;
        GameEvents.OnStateChanged           += HandleGameState;

        if (_button != null) _button.onClick.AddListener(HandleClick);
    }

    void OnDisable()
    {
        GameEvents.OnWormsBoostStateChanged -= HandleBoostState;
        GameEvents.OnStateChanged           -= HandleGameState;

        if (_button != null) _button.onClick.RemoveListener(HandleClick);
    }

    void Start()
    {
        // Director Start'ında bir kez yayınlıyor ama sıralama garanti değil —
        // açılış durumunu buradan da bir kez okuyoruz.
        var d = WormBoostDirector.Instance;

        HandleBoostState(d != null && d.IsArmed, d != null ? d.Charges : 0);

        SetVisible(GameManager.Instance != null && GameManager.Instance.IsPlaying);
    }

    void HandleClick()
    {
        if (AudioService.Instance != null) AudioService.Instance.PlayUIClick();

        if (WormBoostDirector.Instance != null) WormBoostDirector.Instance.Toggle();
    }

    void HandleBoostState(bool armed, int charges)
    {
        if (_armedGlow != null && _armedGlow.activeSelf != armed)
            _armedGlow.SetActive(armed);

        if (_countText != null)
        {
            bool show = charges >= 0;

            if (_countText.gameObject.activeSelf != show) _countText.gameObject.SetActive(show);

            if (show) _countText.SetText("{0}", charges);
        }

        bool usable = charges != 0;

        if (_icon != null)
        {
            Color want = usable ? _fullTint : _emptyTint;

            if (_icon.color != want) _icon.color = want;
        }

        if (_button != null) _button.interactable = usable || armed;
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
