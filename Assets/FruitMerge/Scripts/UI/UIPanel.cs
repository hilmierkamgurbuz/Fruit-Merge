using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public abstract class UIPanel : MonoBehaviour
{
    [SerializeField] protected float _fadeDuration = 0.18f;

    protected CanvasGroup _group;

    float _target;
    bool  _animating;

    public bool IsOpen { get; private set; }

    /// <summary>
    /// Panel açılırken panel_open sesi çalsın mı. GameOverPanel'de KAPALI:
    /// panel_open (520 Hz) ile game_over (220 Hz + 300-800 Hz harmonikleri) aynı
    /// anda çalınca birbirini bulandırıyor, o işi game_over.wav görüyor.
    /// </summary>
    protected virtual bool PlaysOpenSfx => true;

    protected virtual bool PlaysCloseSfx => true;

    protected virtual void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        SetInstant(false);
    }

    public virtual void Show()
    {
        IsOpen = true;
        gameObject.SetActive(true);
        _target = 1f;
        _animating = true;

        _group.interactable = true;
        _group.blocksRaycasts = true;

        if (PlaysOpenSfx && AudioService.Instance != null) AudioService.Instance.PlayPanelOpen();

        OnShow();
    }

    public virtual void Hide()
    {
        IsOpen = false;
        _target = 0f;
        _animating = true;

        _group.interactable = false;
        _group.blocksRaycasts = false;

        if (PlaysCloseSfx && AudioService.Instance != null) AudioService.Instance.PlayPanelClose();

        OnHide();
    }

    void SetInstant(bool open)
    {
        IsOpen = open;
        _group.alpha = open ? 1f : 0f;
        _group.interactable = open;
        _group.blocksRaycasts = open;
    }

    void Update()
    {
        // Alt sınıflar kendi Update'ini TANIMLAMAMALI — Unity yalnızca en türemiş
        // Update'i çağırır ve bu fade'i sessizce durdurur. Bunun yerine OnTick override et.
        OnTick(Time.unscaledDeltaTime);

        if (!_animating) return;

        _group.alpha = Mathf.MoveTowards(_group.alpha, _target,
            Time.unscaledDeltaTime / _fadeDuration);

        if (!Mathf.Approximately(_group.alpha, _target)) return;

        _animating = false;

        if (IsOpen) OnShown(); else OnHidden();
    }

    protected virtual void OnShow() { }
    protected virtual void OnHide() { }

    /// <summary>
    /// Açılma animasyonu BİTTİ (alpha 1'e ulaştı). <see cref="OnShow"/> animasyonun
    /// BAŞINDA çağrılır, bu ise sonunda.
    /// </summary>
    protected virtual void OnShown() { }

    /// <summary>
    /// Kapanma animasyonu BİTTİ (alpha 0'a ulaştı) — panel artık gerçekten görünmez.
    ///
    /// Bir ekrandan diğerine geçerken zincirleme burada kurulmalı: <see cref="OnHide"/>
    /// fade'in BAŞINDA çağrıldığı için oradan bir sonraki paneli açmak iki panelin
    /// 0.18 sn boyunca üst üste görünmesine (çapraz geçiş) yol açıyor.
    /// </summary>
    protected virtual void OnHidden() { }

    /// <summary>
    /// Her karede çağrılır. Panel açıkken timeScale 0 olabildiği için süre
    /// <c>unscaledDeltaTime</c> — kendi Update'ini yazmak yerine bunu override et.
    /// </summary>
    protected virtual void OnTick(float unscaledDeltaTime) { }
}