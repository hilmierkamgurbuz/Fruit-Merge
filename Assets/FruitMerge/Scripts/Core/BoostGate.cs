using System;
using UnityEngine;

/// <summary>
/// Boost'ların ortak kapısı. İki iş yapıyor:
///
///  1. <see cref="IsAnyBusy"/> — "şu an bir boost oynuyor mu?". <see cref="DropController"/>
///     bırakma girdisini, <see cref="GameOverDetector"/> kayıp sayacını buna göre donduruyor.
///     Eskiden ikisi de doğrudan <c>WormBoostDirector.Instance.IsBusy</c> yazıyordu; her yeni
///     boost o iki satırı çoğaltıyordu.
///  2. <see cref="Get"/> — <see cref="BoostId"/>'den director'e. <see cref="BoostButton"/> böylece
///     TEK script olarak bütün boost butonlarına hizmet ediyor, boost başına kopyalanmıyor.
///
/// Sözlük yok, LINQ yok: <see cref="BoostId"/> ile indekslenen sabit boyutlu bir dizi (kural 11).
/// <see cref="IsAnyBusy"/> her karede iki abone tarafından çağrılıyor, o yüzden gövdesi sadece
/// bir <c>for</c> + null kontrolü.
///
/// Statikler domain reload kapalıyken bir sonraki oturuma taşınmasın diye
/// <see cref="GameEvents.ResetStatics"/> ile aynı desende sıfırlanıyor.
/// </summary>
public static class BoostGate
{
    // Enum.GetValues allocation yapıyor ama bu satır uygulama ömründe BİR KEZ, tip ilk
    // kullanıldığında çalışıyor — sıcak döngüde değil. Elle bir "Count" sabiti tutmaktansa
    // bu, BoostId'ye yeni bir eleman eklendiğinde kendiliğinden doğru kalıyor.
    static readonly IBoostDirector[] Directors =
        new IBoostDirector[Enum.GetValues(typeof(BoostId)).Length];

    /// <summary>
    /// Director <c>OnEnable</c>'ında kendini kaydeder. Aynı id'yi iki director alırsa
    /// sonuncusu kazanır ve uyarı basılır — sahnede kopya obje kalmışsa sessizce yanlış
    /// davranmak yerine haber vermesi daha iyi.
    /// </summary>
    public static void Register(IBoostDirector director)
    {
        if (director == null) return;

        int i = (int)director.Id;

        if (i < 0 || i >= Directors.Length) return;

        if (Directors[i] != null && !ReferenceEquals(Directors[i], director))
            Debug.LogWarning($"BoostGate: {director.Id} zaten kayıtlı, üzerine yazılıyor. " +
                             "Sahnede iki director objesi mi var?");

        Directors[i] = director;
    }

    /// <summary>Director <c>OnDisable</c>'ında kaydını siler (kural 2 — her Register'ın karşılığı).</summary>
    public static void Unregister(IBoostDirector director)
    {
        if (director == null) return;

        int i = (int)director.Id;

        if (i < 0 || i >= Directors.Length) return;

        // Sadece KENDİ kaydını silsin: araya başka bir director girmişse onu düşürmesin.
        if (ReferenceEquals(Directors[i], director)) Directors[i] = null;
    }

    /// <summary>Verilen boost'un director'ü, yoksa <c>null</c>.</summary>
    public static IBoostDirector Get(BoostId id)
    {
        int i = (int)id;

        return i >= 0 && i < Directors.Length ? Directors[i] : null;
    }

    /// <summary>Herhangi bir boost oynuyor mu. Bırakma girdisi ve oyun sonu sayacı bunu okuyor.</summary>
    public static bool IsAnyBusy
    {
        get
        {
            for (int i = 0; i < Directors.Length; i++)
                if (Directors[i] != null && Directors[i].IsBusy) return true;

            return false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        for (int i = 0; i < Directors.Length; i++) Directors[i] = null;
    }
}
