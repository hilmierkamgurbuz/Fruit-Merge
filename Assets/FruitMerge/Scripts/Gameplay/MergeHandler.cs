using System;
using UnityEngine;
using System.Collections.Generic;


[DefaultExecutionOrder(0)]
public class MergeHandler : MonoBehaviour
{
    [SerializeField] private GameConfig _config;
    
    [SerializeField] FruitPool _pool;
    
    public readonly struct MergeRequest
    {

        public readonly Fruit A;
        public readonly Fruit B;
        
        public MergeRequest(Fruit a, Fruit b) { A = a; B = b; }
 
        
    }
    
    readonly Queue<MergeRequest> _queue = new Queue<MergeRequest>(32);
    
    readonly HashSet<long> _queuedPairs = new HashSet<long>();



    public void Request(Fruit a, Fruit b)
    {
        if (a == null || b == null) return;
        
        if ( a.IsMerging || b.IsMerging) return;

        long key = PairKey(a, b);
        
        if (!_queuedPairs.Add(key)) return;

        _queue.Enqueue(new MergeRequest(a, b));



    }
    
    void LateUpdate()
    {
        // Oyun oynanmıyorsa kuyruk işlenmez ve boşaltılır.
        //
        // Oyun sonu karesinde şu sıra oluyordu: GameOverDetector Update'te oyunu bitiriyor,
        // FruitPool tahtayı donduruyor, ardından BU LateUpdate sıraya girmiş birleşmeyi
        // işliyordu. Üretilen meyve Drop() çağırdığı için simülasyona geri dönüyor ve
        // sonuç ekranı açıkken tek başına düşüyordu — üstelik skor da oyun bittikten
        // sonra artıyordu.
        //
        // Kuyruğu boşaltmak kayıp değil: pause'dan dönüşte temas hâlâ sürüyorsa
        // OnCollisionStay2D isteği yeniden koyuyor.
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying)
        {
            _queue.Clear();

            _queuedPairs.Clear();

            return;
        }

        int guard = 0;
        while (_queue.Count > 0 && guard++ < 100)
        {
            var req = _queue.Dequeue();

            if (req.A == null || req.B == null) continue;

            if (!req.A.gameObject.activeSelf || !req.B.gameObject.activeSelf) continue;

            if (req.A.IsMerging || req.B.IsMerging) continue;

            if (req.A.Definition != req.B.Definition) continue;

            Execute(req.A, req.B);
            
        }

        // KOŞULSUZ temizle. Eskiden yalnızca kuyruk boşalınca temizleniyordu; guard 100'e
        // takıldığında işlenmiş çiftlerin anahtarları sette kalıyor ve Request onları
        // reddediyordu. Anahtar GetInstanceID tabanlı, havuz da instance'ları yeniden
        // kullandığı için bu "aynı iki meyve bir daha birleşemiyor" demekti.
        //
        // Kalıntı bırakmamak güvenli: kuyrukta bekleyen istekler zaten kuyrukta, set
        // yalnızca YENİ istek eklemeyi filtreliyor. Aynı çift için ikinci bir istek
        // gelirse yukarıdaki IsMerging / activeSelf / Definition kontrolleri onu eliyor.
        _queuedPairs.Clear();
    }
    
    void Execute(Fruit a, Fruit b)
    {
        a.IsMerging = true;
        b.IsMerging = true;

        FruitDefinition current = a.Definition;
        FruitDefinition next = current.nextTier;
            
        Vector2 spawnPos = (Vector2)(a.transform.position + b.transform.position)*0.5f;

        _pool.Despawn(a);
        _pool.Despawn(b);

        if (next == null)
        {
            GameEvents.RaiseMaxTierMerged(current, spawnPos);

            return;
        }
        Fruit spawned = _pool.Spawn(next,  spawnPos);
            
        // byPlayer: false — bu meyve birleşmeden doğdu. Yüzlerin bakış hedefi
        // oyuncunun bıraktığı meyveyi hızlanmasını beklemeden takip ediyor; birleşme
        // ürünü de aynı ayrıcalığı alsaydı her birleşme bakışı kendine çekerdi.
        spawned.Drop(false);
        spawned.PlayPop();

        // Üretilen meyve aşık olur. Olay imzası meyve instance'ı taşımadığı için burada
        // yapıyoruz — 'spawned' referansı elimizde. Kilit, global modu 2 sn ezer.
        if (spawned.Face != null && _config != null)
            spawned.Face.Express(FaceExpression.Love, _config.faceMergeReactionTime);

        GameEvents.RaiseMerged(next, spawnPos);
            
    }

    static long PairKey(Fruit a, Fruit b)
    {
        int ia = a.GetInstanceID();
        int ib = b.GetInstanceID();
        int lo = ia < ib ? ia : ib;
        int hi = ia < ib ? ib : ia;
        unchecked { return ((long)lo << 32) | (uint)hi; }
    }
    
    
}
