using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Tek bir combo popup'ı: birleşme noktasında beliren, rastgele sağa/sola yatık
/// "x3 / Delicious!" yazısı. Üretilen meyvenin renginde, kademesine göre büyüyor.
///
/// <b>Neden biraz yükseliyor:</b> yazı üretilen meyvenin <c>displayColor</c>'ında ve tam
/// o meyvenin üstünde doğuyor — yerinde dursaydı aynı renk üstünde aynı renk kalır ve
/// okunmazdı. Az bir yükselme (yavaşlayarak) yazıyı gövdeden ayırıyor; kontur da
/// arkada başka bir meyve olduğunda kurtarıyor.
///
/// <b>Update'i YOK.</b> ComboPopupDirector tek Update'te <see cref="Tick"/> çağırıyor —
/// aynı anda birkaç popup olsa da tek managed↔native geçişi (performans kuralı 7).
/// </summary>
[RequireComponent(typeof(TextMeshPro))]
public class ComboPopupItem : MonoBehaviour
{
    TextMeshPro _text;
    GameConfig  _config;

    float   _baseFontSize;
    Vector3 _from;
    Vector3 _to;
    float   _lifetime;
    float   _hold;
    float   _elapsed;

    public bool IsDone { get; private set; }

    void Awake()
    {
        _text = GetComponent<TextMeshPro>();

        // prefab'daki punto referans; kademe çarpanı bunun üstüne biniyor
        _baseFontSize = _text.fontSize;
    }

    /// <summary>Havuz yaratırken bir kez — ayarları her Play'de parametre olarak taşımayalım.</summary>
    public void Bind(GameConfig config) => _config = config;

    /// <param name="tier">0 düşük · 1 orta · 2 yüksek · 3 efsane</param>
    public void Play(Vector2 mergePoint, StringBuilder text, Color color, int tier, float tiltDegrees)
    {
        if (_config == null) { IsDone = true; return; }

        _lifetime = Mathf.Max(0.01f, _config.comboPopupLifetime
                                     + tier * _config.comboPopupTierLifetimeStep);
        _hold     = Mathf.Clamp01(_config.comboPopupHoldRatio);
        _elapsed  = 0f;
        IsDone    = false;

        _text.fontSize = _baseFontSize * (1f + tier * _config.comboPopupTierScaleStep);

        _text.SetText(text);

        Color c = color;
        c.a = 1f;
        _text.color = c;

        transform.localRotation = Quaternion.Euler(0f, 0f, tiltDegrees);

        // Kademe yükseldikçe popup birleşme noktasının daha üstünde doğuyor —
        // büyük combo ekranın daha görünür bir yerinde patlasın.
        Vector2 spawn = mergePoint + Vector2.up * (tier * _config.comboPopupTierOffsetY);

        _from = ClampToView(spawn, tiltDegrees);
        _to   = _from + Vector3.up * _config.comboPopupRiseDistance;

        transform.position = _from;
    }

    /// <summary>
    /// Yazıyı ekranın içinde tutar. Genişliği tahmin etmiyoruz: <c>ForceMeshUpdate</c>
    /// ile gerçek mesh sınırlarını ölçüp yatıklığı da hesaba katıyoruz — "Mouthwatering!"
    /// efsane kademede 20° yatıkken tahmini bir payla kenardan taşıyordu.
    /// </summary>
    Vector3 ClampToView(Vector2 wanted, float tiltDegrees)
    {
        _text.ForceMeshUpdate();

        Vector3 ext = _text.textBounds.extents;

        float rad = tiltDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Abs(Mathf.Cos(rad));
        float sin = Mathf.Abs(Mathf.Sin(rad));

        float halfW = ext.x * cos + ext.y * sin;
        float halfH = ext.x * sin + ext.y * cos;

        float limitX = Mathf.Max(0f, _config.comboPopupClampX - halfW);

        float x = Mathf.Clamp(wanted.x, -limitX, limitX);

        // yükselme payını da bırak, tavana yapışıp orada bitmesin
        float limitY = _config.comboPopupMaxY - halfH - _config.comboPopupRiseDistance;

        float y = Mathf.Min(wanted.y, limitY);

        return new Vector3(x, y, 0f);
    }

    public void Tick(float dt)
    {
        _elapsed += dt;

        float t = Mathf.Clamp01(_elapsed / _lifetime);

        // hızlı çıkıp yavaşlayarak duruyor
        transform.position = Vector3.LerpUnclamped(_from, _to, 1f - (1f - t) * (1f - t));

        // ömrün ilk _hold'luk kısmında tam opak, sonrasında söner
        float a = t <= _hold
            ? 1f
            : 1f - (t - _hold) / Mathf.Max(0.0001f, 1f - _hold);

        Color c = _text.color;

        if (!Mathf.Approximately(c.a, a))
        {
            c.a = a;
            _text.color = c;
        }

        if (t >= 1f) IsDone = true;
    }
}
