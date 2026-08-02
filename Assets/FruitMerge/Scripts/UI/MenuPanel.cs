using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Açılış / ana menü ekranı. K4 kararı gereği ayrı sahne değil, aynı sahnede panel.
///
/// <c>GameState.Menu</c>'yü dinliyor: uygulama açılışında <c>GameManager</c> Boot'tan
/// Menu'ye geçiyor, PLAY basılınca Playing'e. Pause ve sonuç ekranındaki MENU butonları
/// buraya geri döndürüyor.
/// </summary>
[DefaultExecutionOrder(100)]
public class MenuPanel : UIPanel
{
    [SerializeField] Button _playButton;

    // Uygulama açılışında panel_open çalmasın — menü bir açılır pencere değil, ekranın
    // kendisi. Ayrıca açılışta AudioService henüz kayıttaki ses ayarını uygulamamış
    // olabiliyor (execution order), yani ses kapalıyken bile bir kez duyulurdu.
    protected override bool PlaysOpenSfx => false;

    protected override void Awake()
    {
        base.Awake();

        if (_playButton != null) _playButton.onClick.AddListener(HandlePlayClicked);
    }

    void OnDestroy()
    {
        if (_playButton != null) _playButton.onClick.RemoveListener(HandlePlayClicked);
    }

    void OnEnable()  { GameEvents.OnStateChanged += HandleStateChanged; }
    void OnDisable() { GameEvents.OnStateChanged -= HandleStateChanged; }

    void HandleStateChanged(GameState s)
    {
        if (s == GameState.Menu)
        {
            if (!IsOpen) Show();
        }
        else
        {
            if (IsOpen) Hide();
        }
    }

    void HandlePlayClicked()
    {
        if (AudioService.Instance != null) AudioService.Instance.PlayUIClick();

        if (GameManager.Instance != null) GameManager.Instance.Play();
    }
}
