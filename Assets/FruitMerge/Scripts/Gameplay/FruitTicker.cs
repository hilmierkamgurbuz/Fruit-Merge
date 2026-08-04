using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tahtadaki bütün meyveleri TEK döngüden sürer (kural 7).
///
/// <b>Neden gerekli:</b> <see cref="Fruit"/> sahnede en çok kopyası olan bileşen. Kendi
/// <c>Update</c> ve <c>FixedUpdate</c>'i varken 60 meyve kare başına 60, fizik adımı
/// başına da 60 ayrı managed↔native geçişi demekti — saniyede 3000'den fazla çağrı.
/// <see cref="FruitFace"/>, <see cref="Worm"/> ve <see cref="ComboPopupItem"/> zaten bu
/// desende; <see cref="Fruit"/> tek istisnaydı.
///
/// Meyve listesi <see cref="FruitPool.Active"/> — havuzun zaten tuttuğu, index'lenebilir
/// bir <c>List&lt;Fruit&gt;</c>. Arama yok, allocation yok. Havuza dönmüş meyveler listede
/// olmadığı için tick de almıyorlar (eski davranışla birebir aynı: pasif objenin
/// <c>Update</c>'i de çalışmıyordu).
///
/// <b>Execution order neden 0:</b> <see cref="QuakeBoostDirector"/> (-30) itmeleri
/// <c>FixedUpdate</c>'te uyguluyor ve meyvenin dönüş söndürmesi ondan SONRA çalışmalı —
/// eski <c>Fruit.FixedUpdate</c> varsayılan sırada (0) olduğu için zaten öyleydi.
/// <see cref="FruitPool"/>'un (-90) üzerine koysaydık sıra ters dönerdi.
/// </summary>
[DefaultExecutionOrder(0)]
public class FruitTicker : MonoBehaviour
{
    void Update()
    {
        FruitPool pool = FruitPool.Instance;

        if (pool == null) return;

        IReadOnlyList<Fruit> active = pool.Active;

        float dt = Time.deltaTime;

        for (int i = 0; i < active.Count; i++)
        {
            Fruit f = active[i];

            if (f != null) f.TickVisual(dt);
        }
    }

    void FixedUpdate()
    {
        FruitPool pool = FruitPool.Instance;

        if (pool == null) return;

        IReadOnlyList<Fruit> active = pool.Active;

        for (int i = 0; i < active.Count; i++)
        {
            Fruit f = active[i];

            if (f != null) f.TickPhysics();
        }
    }
}
