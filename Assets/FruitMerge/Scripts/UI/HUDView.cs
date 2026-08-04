using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]
public class HUDView : MonoBehaviour
{
    [Header("Metinler")]
    [SerializeField] TextMeshProUGUI _scoreText;
    [SerializeField] TextMeshProUGUI _highScoreText;

    [Header("Önizleme")]
    [SerializeField] Image _nextFruitImage;
    [SerializeField] float _previewMaxSize = 70f;

    [Header("Butonlar")]
    [SerializeField] Button _pauseButton;

    [Header("Ayar")]
    [SerializeField] GameConfig _config;

    [Header("Skor Animasyonu")]
    [Tooltip("Gösterilen skorun gerçek skora yaklaşma hızı (birim/saniye).")]
    [SerializeField] float _countSpeed = 400f;

    int _targetScore;
    float _shownScore;
    bool _dirty;

    void OnEnable()
    {
        GameEvents.OnScoreChanged     += HandleScore;
        GameEvents.OnHighScoreChanged += HandleHighScore;
        GameEvents.OnNextFruitChanged += HandleNextFruit;
        GameEvents.OnRunStarted       += HandleRunStarted;
    }

    void OnDisable()
    {
        GameEvents.OnRunStarted       -= HandleRunStarted;
        GameEvents.OnScoreChanged     -= HandleScore;
        GameEvents.OnHighScoreChanged -= HandleHighScore;
        GameEvents.OnNextFruitChanged -= HandleNextFruit;
    }

    void Start()
    {
        if (_pauseButton != null)
            _pauseButton.onClick.AddListener(HandlePauseClicked);
    }

    void OnDestroy()
    {
        if (_pauseButton != null)
            _pauseButton.onClick.RemoveListener(HandlePauseClicked);
    }

    void HandlePauseClicked()
    {
        if (AudioService.Instance != null) AudioService.Instance.PlayUIClick();

        GameManager.Instance.Pause();
    }

    void HandleScore(int score) => _targetScore = score;

    /// <summary>
    /// Yeni oyunda skoru ANINDA sıfıra çek. Sayaç animasyonu yalnızca yukarı doğru
    /// anlamlı; sıfırlamayı da yumuşatınca ekranda eski skordan 0'a doğru saniyede
    /// _countSpeed hızında geri sayım görünüyordu (3956'dan 0'a ~10 saniye).
    /// </summary>
    void HandleRunStarted()
    {
        _targetScore = 0;
        _shownScore = 0f;

        if (_scoreText != null) _scoreText.SetText("{0}", 0);
    }

    // Null kontrolü şart: rekor olayı SaveService.Start'ta yayınlanıyor, yani alan
    // sahnede boş kalırsa açılışta NRE. _scoreText için zaten kontrol var, bunda yoktu.
    void HandleHighScore(int hs)
    {
        if (_highScoreText != null) _highScoreText.SetText("{0}", hs);
    }

    void HandleNextFruit(FruitDefinition def)
    {
        if (def == null || _nextFruitImage == null) return;

        _nextFruitImage.sprite = def.sprite;

        float s = Mathf.Lerp(0.55f, 1f, def.scale / 4f);
        _nextFruitImage.rectTransform.sizeDelta = Vector2.one * (_previewMaxSize * s);
    }

    void Update()
    {
        if (_scoreText != null && !Mathf.Approximately(_shownScore, _targetScore))
        {
            _shownScore = Mathf.MoveTowards(_shownScore, _targetScore, _countSpeed * Time.unscaledDeltaTime);
            _scoreText.SetText("{0}", Mathf.RoundToInt(_shownScore));
        }
    }
}
