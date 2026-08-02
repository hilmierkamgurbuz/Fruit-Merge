using UnityEngine;

/// <summary>Meyve yüz ifadeleri. Sıra ÖNEMLİ — FaceSet lookup'ı indeksle çalışıyor.</summary>
public enum FaceExpression
{
    Idle,
    Happy,
    Love,
    Excited,
    Wink,
    Surprised,
    Worried,
    Scared,
    Angry,
    Dizzy,
    Sleepy,
    Squish
}

/// <summary>Meyve boyut sınıfı — art 4 ayrı çözünürlükte hazırlanmış.</summary>
public enum FaceSize
{
    Sm,
    Md,
    Lg,
    Xl
}

/// <summary>
/// Meyvenin danger line'a göre durumu. Histerezis için meyve başına saklanıyor —
/// FruitFace'te duruyor, böylece Dictionary/lookup/allocation gerekmiyor.
/// </summary>
public enum FaceDangerState
{
    None,
    Worried,
    Scared
}

/// <summary>
/// 12 ifade × 4 boyut = 48 sprite'ın tablosu.
///
/// Çalışma anında düz bir <c>Sprite[48]</c> dizisine indirgeniyor ve
/// <c>(int)expression * 4 + (int)size</c> ile indeksleniyor — Dictionary değil,
/// string değil, allocation yok (performans kuralı 11).
/// </summary>
[CreateAssetMenu(fileName = "FaceSet", menuName = "FruitMerge/Face Set")]
public class FaceSet : ScriptableObject
{
    [System.Serializable]
    public class Row
    {
        public FaceExpression expression;
        public Sprite sm;
        public Sprite md;
        public Sprite lg;
        public Sprite xl;
    }

    [Tooltip("Bileşen başlığına sağ tıkla → 'Yüzleri isimden otomatik doldur' — 48 sprite'ı elle sürüklemene gerek yok")]
    public Row[] rows = new Row[0];

    const int SizeCount = 4;

    Sprite[] _lookup;

    void OnEnable() => _lookup = null;

    void Build()
    {
        int exprCount = System.Enum.GetValues(typeof(FaceExpression)).Length;

        _lookup = new Sprite[exprCount * SizeCount];

        if (rows == null) return;

        for (int i = 0; i < rows.Length; i++)
        {
            Row r = rows[i];

            if (r == null) continue;

            int b = (int)r.expression * SizeCount;

            if (b < 0 || b + 3 >= _lookup.Length) continue;

            _lookup[b + 0] = r.sm;
            _lookup[b + 1] = r.md;
            _lookup[b + 2] = r.lg;
            _lookup[b + 3] = r.xl;
        }
    }

    public Sprite Get(FaceExpression expression, FaceSize size)
    {
        if (_lookup == null) Build();

        int i = (int)expression * SizeCount + (int)size;

        if (i < 0 || i >= _lookup.Length) return null;

        return _lookup[i];
    }

#if UNITY_EDITOR
    void OnValidate() => _lookup = null;

    /// <summary>
    /// Dosya adı kalıbından (face_&lt;ifade&gt;_&lt;boyut&gt;.png) 48 sprite'ı otomatik bağlar.
    /// </summary>
    [ContextMenu("Yüzleri isimden otomatik doldur")]
    void AutoFillFromNames()
    {
        const string folder = "Assets/FruitMerge/Art/Fruits/Faces";

        string[] names = System.Enum.GetNames(typeof(FaceExpression));
        string[] sizes = { "sm", "md", "lg", "xl" };

        var built = new Row[names.Length];
        int found = 0, missing = 0;

        for (int i = 0; i < names.Length; i++)
        {
            string key = names[i].ToLowerInvariant();

            var row = new Row();
            row.expression = (FaceExpression)i;

            var loaded = new Sprite[4];

            for (int s = 0; s < 4; s++)
            {
                string path = folder + "/face_" + key + "_" + sizes[s] + ".png";
                loaded[s] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);

                if (loaded[s] != null) found++;
                else { missing++; Debug.LogWarning("[FaceSet] bulunamadı: " + path, this); }
            }

            row.sm = loaded[0];
            row.md = loaded[1];
            row.lg = loaded[2];
            row.xl = loaded[3];

            built[i] = row;
        }

        rows = built;
        _lookup = null;

        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log($"[FaceSet] {found} sprite bağlandı, {missing} eksik.", this);
    }
#endif
}
