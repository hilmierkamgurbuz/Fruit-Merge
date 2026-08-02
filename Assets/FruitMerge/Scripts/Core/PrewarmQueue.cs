using System.Collections.Generic;

/// <summary>
/// Karelere yayılarak yapılan açılış ısıtması. Havuzlar <c>Awake</c>'te kendini
/// kaydeder, <see cref="SplashPanel"/> yükleme çubuğunu doldururken adım adım tüketir.
///
/// Neden: <see cref="FruitPool"/> ve <see cref="ComboPopupDirector"/> ısıtmayı
/// <c>Awake</c> içinde TEK KAREDE yapıyordu (40 + 6 = 46 Instantiate). Bu iş sahne
/// yüklenirken bittiği için ilk kare o kadar geç geliyordu; oyuncu boş ekrana bakıyordu.
/// Aynı iş açılış ekranı boyunca 2'şer 2'şer yapılınca toplam maliyet değişmiyor ama
/// ilk kare erken geliyor ve çubuk GERÇEK bir işi gösteriyor.
///
/// <c>GameEvents</c> gibi statik — sahnede ek bir obje ya da referans bağlama yok.
/// </summary>
public static class PrewarmQueue
{
    static readonly List<IPrewarmSource> _sources = new List<IPrewarmSource>(4);

    public static void Register(IPrewarmSource source)
    {
        if (source == null) return;
        if (_sources.Contains(source)) return;

        _sources.Add(source);
    }

    public static void Unregister(IPrewarmSource source)
    {
        if (source == null) return;

        _sources.Remove(source);
    }

    /// <summary>Toplam yaratılacak obje sayısı. Kaynak yoksa 0.</summary>
    public static int Total
    {
        get
        {
            int total = 0;
            for (int i = 0; i < _sources.Count; i++) total += _sources[i].PrewarmTotal;
            return total;
        }
    }

    /// <summary>Şimdiye kadar yaratılan obje sayısı.</summary>
    public static int Done
    {
        get
        {
            int done = 0;
            for (int i = 0; i < _sources.Count; i++) done += _sources[i].PrewarmDone;
            return done;
        }
    }

    public static bool IsComplete => Done >= Total;

    /// <summary>
    /// Bu karede EN FAZLA <paramref name="budget"/> obje yarat. Bütçe kaynaklar arasında
    /// sırayla paylaştırılır: ilk kaynak bitmeden ikinciye geçilmez.
    /// </summary>
    public static void Step(int budget)
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            if (budget <= 0) return;

            IPrewarmSource source = _sources[i];

            int before = source.PrewarmDone;

            source.PrewarmStep(budget);

            budget -= source.PrewarmDone - before;
        }
    }
}

/// <summary>Açılışta karelere yayılarak ısıtılabilen havuz.</summary>
public interface IPrewarmSource
{
    int PrewarmTotal { get; }

    int PrewarmDone { get; }

    /// <summary>En fazla <paramref name="budget"/> obje yarat.</summary>
    void PrewarmStep(int budget);
}
