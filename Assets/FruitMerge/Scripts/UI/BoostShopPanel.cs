using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kullanımı biten bir boost'a basılınca açılan satın alma penceresi.
///
/// <b>Boost başına kopyalanmıyor.</b> <see cref="BoostButton"/> ile aynı desen: hangi
/// boost'un istendiği <see cref="GameEvents.OnBoostShopRequested"/> ile geliyor, panel
/// görselini/metnini <see cref="_entries"/> tablosundan, fiyatını
/// <see cref="GameConfig.PriceFor"/>'dan okuyor. Yeni boost eklemek = tabloya bir satır.
///
/// Fiyat neden panelde değil <see cref="GameConfig"/>'te: fiyat dengeleme değeri,
/// görsel ve açıklama ise sunum. Dengeleme değerleri tek dosyada toplanıyor (kural 6).
///
/// Kendi <c>Update</c>'ini tanımlamıyor — <see cref="UIPanel"/>'in fade'ini kesmemek
/// için (bkz. UIPanel açıklaması). Burada zamana bağlı hiçbir iş de yok.
/// </summary>
[DefaultExecutionOrder(100)]
public class BoostShopPanel : UIPanel
{
    /// <summary>Bir boost'un mağazadaki sunumu. Fiyat burada YOK, GameConfig'te.</summary>
    [Serializable]
    struct Entry
    {
        public BoostId id;

        [Tooltip("panelde büyük gösterilecek boost ikonu")]
        public Sprite icon;

        [Tooltip("tek cümlelik İngilizce açıklama")]
        public string description;
    }

    [Header("Boost tablosu")]
    [SerializeField] Entry[] _entries;

    [Header("Referanslar")]
    [SerializeField] GameConfig _config;

    [SerializeField] Image _boostIcon;

    [SerializeField] TextMeshProUGUI _descriptionLabel;

    [SerializeField] TextMeshProUGUI _priceLabel;

    [SerializeField] Button _buyButton;

    [SerializeField] Button _closeButton;

    BoostId _current;
    int     _price;

    protected override void Awake()
    {
        base.Awake();

        if (_buyButton != null)   _buyButton.onClick.AddListener(HandleBuyClicked);
        if (_closeButton != null) _closeButton.onClick.AddListener(HandleCloseClicked);
    }

    void OnDestroy()
    {
        if (_buyButton != null)   _buyButton.onClick.RemoveListener(HandleBuyClicked);
        if (_closeButton != null) _closeButton.onClick.RemoveListener(HandleCloseClicked);
    }

    void OnEnable()
    {
        GameEvents.OnBoostShopRequested += HandleShopRequested;
        GameEvents.OnCoinsChanged       += HandleCoinsChanged;
        GameEvents.OnStateChanged       += HandleStateChanged;
    }

    void OnDisable()
    {
        GameEvents.OnBoostShopRequested -= HandleShopRequested;
        GameEvents.OnCoinsChanged       -= HandleCoinsChanged;
        GameEvents.OnStateChanged       -= HandleStateChanged;
    }

    // ---------------------------------------------------------------- olaylar

    void HandleShopRequested(BoostId id)
    {
        _current = id;
        _price   = _config != null ? _config.PriceFor(id) : 0;

        Fill(id);

        if (_priceLabel != null) _priceLabel.SetText("{0}", _price);

        RefreshAffordability();

        if (!IsOpen) Show();
    }

    /// <summary>
    /// Panel açıkken para değişirse (ödül inmesi ya da satın alma) SATIN AL butonu
    /// hemen doğru hâle geçsin.
    /// </summary>
    void HandleCoinsChanged(int total)
    {
        if (IsOpen) RefreshAffordability();
    }

    /// <summary>
    /// Oyun durumu değişince kapan. Panel oynanış sırasında açılıyor; oyuncu bu arada
    /// pause'a ya da menüye giderse pencere ortada asılı kalmamalı.
    /// </summary>
    void HandleStateChanged(GameState s)
    {
        if (s != GameState.Playing && IsOpen) Hide();
    }

    // Cüzdan HUD'ı oynanış sırasında gizli; mağaza açıkken görünmesi gerekiyor.
    // Show/Hide'ı çağıran her yol (buton, ESC, durum değişimi) buradan geçtiği için
    // tek çift kanca yetiyor — çağrı yerlerine tek tek olay serpmeye gerek yok.
    protected override void OnShow() => GameEvents.RaiseBoostShopToggled(true);

    protected override void OnHide() => GameEvents.RaiseBoostShopToggled(false);

    // ---------------------------------------------------------------- içerik

    void Fill(BoostId id)
    {
        if (_entries == null) return;

        for (int i = 0; i < _entries.Length; i++)
        {
            if (_entries[i].id != id) continue;

            if (_boostIcon != null && _entries[i].icon != null)
                _boostIcon.sprite = _entries[i].icon;

            if (_descriptionLabel != null)
                _descriptionLabel.SetText(_entries[i].description);

            return;
        }

        Debug.LogWarning($"[BoostShopPanel] {id} için tabloda satır yok — panel eski " +
                         "içerikle açılıyor.", this);
    }

    void RefreshAffordability()
    {
        if (_buyButton == null) return;

        bool canAfford = SaveService.Instance != null && SaveService.Instance.Coins >= _price;

        _buyButton.interactable = canAfford;
    }

    // ---------------------------------------------------------------- butonlar

    void HandleBuyClicked()
    {
        if (SaveService.Instance == null) return;

        if (!SaveService.Instance.TrySpendCoins(_price)) return;

        var director = BoostGate.Get(_current);

        if (director != null) director.AddCharge(1);

        if (AudioService.Instance != null) AudioService.Instance.PlayUIClick();

        Hide();
    }

    // ui_click çalmıyoruz: kapanışta panel_close zaten çalıyor, ikisi üst üste
    // tek bulanık tık oluyor (GameOverPanel'deki MENU butonuyla aynı gerekçe)
    void HandleCloseClicked()
    {
        if (IsOpen) Hide();
    }
}
