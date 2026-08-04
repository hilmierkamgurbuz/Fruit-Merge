using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sahnede elle yapılması gereken iki düzeltmeyi Unity'nin KENDİ API'siyle uygular.
///
/// Neden script: sahne dosyasını (Game.unity) dışarıdan YAML olarak düzenlemek, Editor
/// sahneyi bellekte tuttuğu için kaydedildiğinde üzerine yazılıyor — ayrıca elle
/// MonoBehaviour bloğu yazıp fileID uydurmak sahneyi bozma riski taşıyor. Bu yol
/// değişikliği Editor'ün içinde yaptırıyor.
///
/// Yaptıkları:
///  1. <b>Main Camera → CameraFit</b>. Referans kadraj (orthographicSize / kamera Y)
///     bileşen EKLENMEDEN ÖNCE okunuyor: <c>CameraFit</c> <c>ExecuteAlways</c> olduğu
///     için eklenir eklenmez kamerayı yazmaya başlıyor, sonradan okusaydık kendi
///     yazdığımız değeri referans sanardık.
///  2. <b>GameOverPanel/Dimmer</b> RectTransform'u tam ekran stretch'e çeker. Şu an
///     anchor'lar (0,0)-(1,1) ama offset'ler küçük bir elemandan kalmış
///     (sizeDelta -980 × -1820): referans çözünürlükte 100×100'lük bir kare,
///     1080×2400 telefonda ise negatif genişlik — hiç çizilmiyor.
///
/// İkisi de FİKİRSİZ (idempotent): zaten uygulanmışsa hiçbir şey yapmıyor. Unity
/// oturumda bir kez kendiliğinden çalıştırıyor; menüden tekrar tetiklenebilir.
///
/// <b>İş bittiğinde bu dosya silinebilir</b> — tek seferlik bir tamir aracı.
/// </summary>
public static class SceneFixups
{
    const string ScenePath  = "Assets/FruitMerge/Scenes/Game.unity";
    // Sürüm eki: bu oturumda bir önceki sürüm zaten çalışmış olabilir. Anahtarı
    // değiştirmek, güncellenmiş düzeltmelerin Unity'yi yeniden başlatmadan bir kez
    // daha uygulanmasını sağlıyor.
    const string SessionKey = "FruitMerge.SceneFixups.Ran.v8";

    /// <summary>
    /// Oturumda bir kez, sahne yüklendikten sonra çalışır. <see cref="SessionState"/>
    /// domain reload'ları aşıyor ama Unity kapanınca sıfırlanıyor.
    ///
    /// <b>Bayrak ancak BAŞARILI çalıştırmadan SONRA konuyor.</b> Play mode'a girmek de
    /// domain reload tetikliyor; bayrağı baştan koysaydık (önceki sürüm bunu yapıyordu)
    /// çalıştırma aşağıdaki Play mode guard'ına takılıp sessizce iptal olur ve o oturumda
    /// bir daha hiç denenmezdi. Bu yüzden ayrıca Play mode'dan çıkışı da dinliyoruz.
    /// </summary>
    [InitializeOnLoadMethod]
    static void Bootstrap()
    {
        if (SessionState.GetBool(SessionKey, false)) return;

        // Sahne henüz yüklenmemiş olabilir; bir kare bekle.
        EditorApplication.delayCall            += TryApplyAuto;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
    }

    static void HandlePlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += TryApplyAuto;
    }

    static void TryApplyAuto()
    {
        if (SessionState.GetBool(SessionKey, false)) return;

        // Play mode'dayken sahneye dokunmuyoruz — değişiklikler çıkışta zaten geri alınırdı.
        // EnteredEditMode'da tekrar denenecek.
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        if (!Apply(false)) return;

        SessionState.SetBool(SessionKey, true);

        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
    }

    [MenuItem("FruitMerge/Sahne Düzeltmelerini Uygula")]
    static void ApplyFromMenu() => Apply(true);

    /// <returns>Çalıştırılabildi mi. Değişiklik olup olmaması ayrı — engellendiyse false.</returns>
    static bool Apply(bool verbose)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (verbose)
                Debug.LogWarning("SceneFixups: Play mode'dayken sahne düzenlenmiyor. " +
                                 "Play'den çık, düzeltmeler kendiliğinden uygulanacak.");

            return false;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (scene.path != ScenePath)
        {
            if (verbose)
                Debug.LogWarning($"SceneFixups: aktif sahne {ScenePath} değil ({scene.path}) — atlandı.");

            return false;
        }

        int changed = FixCameraFit(scene)
                      + FixBackgroundCover(scene)
                      + FixCanvasScaler(scene)
                      + RemoveDesignFrame(scene, "HUDCanvas")
                      + RemoveDesignFrame(scene, "OverlayCanvas")
                      + FixBoostSize(scene)
                      + FixGameOverDimmer(scene)
                      + FixBoardLayout(scene)
                      + FixFruitPhysics();

        if (changed == 0)
        {
            if (verbose) Debug.Log("SceneFixups: her şey zaten yerinde, değişiklik yok.");

            return true;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"SceneFixups: {changed} düzeltme uygulandı, sahne kaydedildi.");

        return true;
    }

    // ------------------------------------------------------------ 1) CameraFit

    const string ConfigPath = "Assets/FruitMerge/Data/GameConfig.asset";

    static int FixCameraFit(Scene scene)
    {
        Camera cam = FindMainCamera(scene);

        if (cam == null)
        {
            Debug.LogWarning("SceneFixups: Main Camera bulunamadı, CameraFit eklenemedi.");

            return 0;
        }

        int changed = 0;

        CameraFit fit = cam.GetComponent<CameraFit>();

        if (fit == null)
        {
            // ÖNCE oku — bkz. sınıf açıklaması.
            float baseSize = cam.orthographicSize;
            float baseY    = cam.transform.localPosition.y;

            fit = Undo.AddComponent<CameraFit>(cam.gameObject);

            var so = new SerializedObject(fit);

            so.FindProperty("_baseOrthoSize").floatValue = baseSize;
            so.FindProperty("_baseCameraY").floatValue   = baseY;

            // ApplyModifiedProperties OnValidate'i tetikliyor, CameraFit da orada
            // hesabı geçersiz kılıp doğru referansla yeniden çalışıyor.
            so.ApplyModifiedProperties();

            Debug.Log($"SceneFixups: Main Camera'ya CameraFit eklendi " +
                      $"(referans kadraj: orthographicSize {baseSize}, kamera Y {baseY}).");

            changed++;
        }

        // Açılan fazla alan eşit bölünsün: üstteki HUD ve alttaki boost/zincir şeridi
        // birlikte pay alsın. Eski varsayılan 0'dı (hepsi yukarı) ve alt şerit uzun
        // ekranda dar kalıyordu. Sadece o eski değeri taşıyoruz — kullanıcı elle
        // ayarladıysa dokunmuyoruz.
        var biasSo = new SerializedObject(fit);

        SerializedProperty biasProp = biasSo.FindProperty("_verticalBias");

        if (biasProp != null && Mathf.Approximately(biasProp.floatValue, 0f))
        {
            biasProp.floatValue = 0.5f;

            biasSo.ApplyModifiedProperties();

            Debug.Log("SceneFixups: CameraFit dikey dağılımı 0 → 0.5 (fazla alan alta ve üste eşit).");

            changed++;
        }

        // Hedef genişlik GameConfig.wallInnerX'ten okunuyor; referans bağlı değilse
        // bileşen kadraja hiç dokunmuyor.
        var fitSo = new SerializedObject(fit);

        SerializedProperty configProp = fitSo.FindProperty("_config");

        if (configProp != null && configProp.objectReferenceValue == null)
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);

            if (config == null)
            {
                Debug.LogWarning($"SceneFixups: {ConfigPath} bulunamadı, CameraFit'in " +
                                 "GameConfig alanı boş kaldı.");
            }
            else
            {
                configProp.objectReferenceValue = config;

                fitSo.ApplyModifiedProperties();

                Debug.Log($"SceneFixups: CameraFit'e GameConfig bağlandı " +
                          $"(hedef yarı-genişlik wallInnerX = {config.wallInnerX}).");

                changed++;
            }
        }

        return changed;
    }

    static Camera FindMainCamera(Scene scene)
    {
        Camera named = null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera c in root.GetComponentsInChildren<Camera>(true))
            {
                if (c.CompareTag("MainCamera")) return c;

                if (named == null && c.name == "Main Camera") named = c;
            }
        }

        return named;
    }

    // ------------------------------------ 6) UI ölçeği + tasarım çerçevesi

    /// <summary>
    /// CanvasScaler'ı SADECE YÜKSEKLİĞE göre ölçeklemeye çeker (Match = 1).
    ///
    /// <b>Bilinçli bir ayrım:</b> oyun alanı ile HUD farklı kurallara bağlı.
    ///  - <b>Dünya</b> genişliğe bağlı (<see cref="CameraFit"/>): tahta her cihazda
    ///    aynı, oynanış değişmiyor.
    ///  - <b>HUD</b> yüksekliğe bağlı: uzun ekranda açılan fazla dikey alan HUD'un
    ///    payına düşüyor, ikonlar o alana göre büyüyor. Match 0 iken HUD ekran
    ///    genişliğine kilitliydi; 20:9'da ekran %25 uzayınca köşelerdeki ikonlar
    ///    olduğu boyutta kalıp küçücük görünüyordu.
    ///
    /// Ölçek çarpanı <c>ekranYüksekliği / 1920</c>, yani 1080×2400'de 1.25 — pause,
    /// skor ve boost ikonları 9:16'daki hâllerinden %25 büyük çiziliyor.
    ///
    /// Referans GENİŞLİK bunun sonucu olarak daralıyor (1920 × aspect = 20:9'da 864).
    /// Köşe-noktası anchor'lı elemanlar bundan etkilenmiyor; genişliğe yayılan tek şey
    /// meyve zinciri şeridi ve onu <see cref="FruitChainView"/> zaten orantılı
    /// bölüştürüp sığdırıyor.
    /// </summary>
    static int FixCanvasScaler(Scene scene)
    {
        Transform canvas = FindDeep(scene, "MainCanvas");

        var scaler = canvas != null ? canvas.GetComponent<UnityEngine.UI.CanvasScaler>() : null;

        if (scaler == null)
        {
            Debug.LogWarning("SceneFixups: MainCanvas/CanvasScaler bulunamadı.");

            return 0;
        }

        if (Mathf.Approximately(scaler.matchWidthOrHeight, 1f)) return 0;

        Undo.RecordObject(scaler, "CanvasScaler match");

        float old = scaler.matchWidthOrHeight;

        scaler.matchWidthOrHeight = 1f;

        EditorUtility.SetDirty(scaler);

        Debug.Log($"SceneFixups: CanvasScaler Match {old} → 1 (sadece yükseklik).");

        return 1;
    }

    /// <summary>
    /// <c>DesignFrame</c> denemesini geri alır: çocukları canvas'a geri taşıyıp ara
    /// objeyi siler.
    ///
    /// O yaklaşım HUD'u 9:16 çerçevesine hapsediyordu — oranlar birebir korunuyordu ama
    /// köşelerdeki ikonlar uzun ekranda ekrana göre küçük kalıyordu. İstenen davranış
    /// bunun tersi: HUD ekran köşelerinde kalsın ve ekranla birlikte BÜYÜSÜN
    /// (bkz. <see cref="FixCanvasScaler"/>).
    /// </summary>
    static int RemoveDesignFrame(Scene scene, string canvasName)
    {
        Transform canvas = FindDeep(scene, canvasName);

        if (canvas == null) return 0;

        Transform frame = canvas.Find("DesignFrame");

        if (frame == null) return 0;

        var moving = new System.Collections.Generic.List<RectTransform>();

        foreach (Transform child in frame)
            if (child is RectTransform rt) moving.Add(rt);

        int baseIndex = frame.GetSiblingIndex();

        for (int i = 0; i < moving.Count; i++)
        {
            RectTransform rt = moving[i];

            Vector2 aMin = rt.anchorMin, aMax = rt.anchorMax;
            Vector2 aPos = rt.anchoredPosition, sDelta = rt.sizeDelta;
            Vector2 pivot = rt.pivot;

            Undo.SetTransformParent(rt, canvas, "DesignFrame'den geri taşı");

            rt.anchorMin        = aMin;
            rt.anchorMax        = aMax;
            rt.pivot            = pivot;
            rt.anchoredPosition = aPos;
            rt.sizeDelta        = sDelta;

            rt.SetSiblingIndex(baseIndex + i);
        }

        Undo.DestroyObjectImmediate(frame.gameObject);

        Debug.Log($"SceneFixups: {canvasName} altındaki DesignFrame kaldırıldı, " +
                  $"{moving.Count} çocuk geri taşındı.");

        return 1;
    }

    // -------------------------------------------- 4) arka plan kaplaması

    /// <summary>
    /// <see cref="BackgroundCover"/>'ı Background objesine takar ve kadraj kaynağını bağlar.
    /// Uzun ekranda açılan alanın altında boyanmamış şerit kalmasın diye.
    /// </summary>
    static int FixBackgroundCover(Scene scene)
    {
        Transform bg = FindDeep(scene, "Background");

        if (bg == null)
        {
            Debug.LogWarning("SceneFixups: Environment/Background bulunamadı.");

            return 0;
        }

        Camera cam = FindMainCamera(scene);

        CameraFit fit = cam != null ? cam.GetComponent<CameraFit>() : null;

        int changed = 0;

        BackgroundCover cover = bg.GetComponent<BackgroundCover>();

        if (cover == null)
        {
            cover = Undo.AddComponent<BackgroundCover>(bg.gameObject);

            // Taban konum/ölçek: bileşen henüz hiçbir şey yazmadan yakalanmalı.
            var so = new SerializedObject(cover);

            so.FindProperty("_basePosition").vector3Value = bg.localPosition;
            so.FindProperty("_baseScale").vector3Value    = bg.localScale;

            so.ApplyModifiedProperties();

            Debug.Log($"SceneFixups: Background'a BackgroundCover eklendi " +
                      $"(taban konum {bg.localPosition}, ölçek {bg.localScale}).");

            changed++;
        }

        var coverSo = new SerializedObject(cover);

        SerializedProperty fitProp = coverSo.FindProperty("_cameraFit");

        if (fitProp != null && fitProp.objectReferenceValue == null && fit != null)
        {
            fitProp.objectReferenceValue = fit;

            coverSo.ApplyModifiedProperties();

            Debug.Log("SceneFixups: BackgroundCover'a CameraFit bağlandı.");

            changed++;
        }

        return changed;
    }

    // ------------------------------------------------ 7) boost ikon boyutu

    /// <summary>
    /// Boost ikonlarını büyütür.
    ///
    /// <b>Neden <c>localScale</c>, <c>sizeDelta</c> değil:</b> BoostSlot'un çocukları
    /// (Glow 206, CountBadge/PlusBadge 64) sabit boyutlu ve NOKTA anchor'lı — parent'ın
    /// boyutunu takip etmiyorlar. <c>sizeDelta</c>'yı büyütmek sadece ikonun kendisini
    /// büyütür, halka ve rozetler olduğu yerde kalır ve oranlar dağılır. <c>localScale</c>
    /// üçünü birden ölçekliyor.
    ///
    /// Slot'lar sahnede 0.823 ölçekteydi — yani 160 birimlik ikon aslında 131.7 birim
    /// çiziliyordu. Küçük görünmelerinin asıl sebebi buydu. Birinin z'si 0.809'du
    /// (Inspector'da tek eksen kaydırılmış), o da düzeliyor.
    ///
    /// Yeni efektif boyut 180 referans birim (160 × 1.125), yani <b>%37 büyük</b>.
    /// Konumlar sol kenar payı ve zincir şeridiyle ilişki AYNI kalacak şekilde
    /// yeniden hesaplandı; 9:16'da zemin çizgisine 18.6 birim pay kalıyor.
    /// </summary>
    const float BoostScale = 1.125f;

    static readonly Vector2 BoostWormsPos = new Vector2(124f, 279f);
    static readonly Vector2 BoostQuakePos = new Vector2(322f, 279f);

    static int FixBoostSize(Scene scene)
    {
        return ResizeBoost(scene, "BoostSlot",       BoostWormsPos)
               + ResizeBoost(scene, "BoostSlot_Quake", BoostQuakePos);
    }

    static int ResizeBoost(Scene scene, string name, Vector2 position)
    {
        Transform slot = FindDeep(scene, name);

        if (slot == null)
        {
            Debug.LogWarning($"SceneFixups: {name} bulunamadı.");

            return 0;
        }

        var rt = slot as RectTransform;

        if (rt == null) return 0;

        var scale = new Vector3(BoostScale, BoostScale, BoostScale);

        bool ok = rt.localScale == scale && rt.anchoredPosition == position;

        if (ok) return 0;

        Undo.RecordObject(rt, $"{name} boyutu");

        Vector3 oldScale = rt.localScale;

        rt.localScale       = scale;
        rt.anchoredPosition = position;

        EditorUtility.SetDirty(rt);

        Debug.Log($"SceneFixups: {name} ölçek {oldScale.x:0.###} → {BoostScale} " +
                  $"(efektif {160f * BoostScale:0.#} birim), konum {position}.");

        return 1;
    }

    // ------------------------------------------------- 5) meyve fiziği

    const string FruitPrefabPath = "Assets/FruitMerge/Prefabs/Fruit.prefab";

    /// <summary>
    /// Meyvelerin çarpışma algısını Continuous'a çeker.
    ///
    /// Discrete algı, gövdeyi kare kare ışınlıyor: hızlı düşen bir meyve iki fizik adımı
    /// arasında duvarın öte tarafına geçebiliyor. Zeminden 6.58 birim yükseklikte
    /// bırakılan meyve çarpma anında 11.4 birim/sn'ye ulaşıyor, 50 Hz'de adım başına
    /// 0.23 birim — duvar 0.5 kalınlığında, yani pay dar. Sıkışan bir yığın çok daha
    /// yüksek anlık hızlar üretebiliyor ve orada Discrete kesin olarak kaçırıyor.
    ///
    /// Continuous, uyanık gövdeler için biraz daha pahalı; ama uyuyanları hiç
    /// ilgilendirmiyor ve tahtadaki meyvelerin çoğu duruyor. Doğru takas.
    /// </summary>
    static int FixFruitPhysics()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(FruitPrefabPath);

        if (contents == null)
        {
            Debug.LogWarning($"SceneFixups: {FruitPrefabPath} açılamadı.");

            return 0;
        }

        try
        {
            var body = contents.GetComponentInChildren<Rigidbody2D>(true);

            if (body == null)
            {
                Debug.LogWarning("SceneFixups: Fruit prefab'ında Rigidbody2D yok.");

                return 0;
            }

            if (body.collisionDetectionMode == CollisionDetectionMode2D.Continuous) return 0;

            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            PrefabUtility.SaveAsPrefabAsset(contents, FruitPrefabPath);

            Debug.Log("SceneFixups: Fruit prefab çarpışma algısı Discrete → Continuous.");

            return 1;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // ------------------------------------------------------ 3) tahta düzeni

    /// <summary>
    /// Tahtayı TASARIM ÇERÇEVESİNİN içinde tutar — 9:16'da yazılmış hâli.
    ///
    /// <b>Neden büyütmüyoruz:</b> tahtayı uzun ekrana göre yukarı doğru açmak, oyunu
    /// cihaza göre değiştirir — havuz daha çok meyve alır, skor tavanı yükselir,
    /// 9:16'da ise dal kadrajın dışına taşar. Tahta artık her cihazda AYNI; ekranın
    /// artakalan kısmı oynanışa değil UI'a gidiyor (bkz. <see cref="CameraFit"/>).
    ///
    /// Üçü birbirine bağlı, biri değişirse hepsi gözden geçirilmeli:
    ///  - <b>DangerLine</b> — kaybetme eşiği, oyun alanının tavanı.
    ///  - <b>DropZone / dropY</b> — dalda asılı meyvenin ALTI danger line'ın üstünde
    ///    kalmalı, yoksa oyun daha meyve bırakılmadan kaybedilmiş sayılır. Ayrıca
    ///    yükseldikçe düşüş hızı artar: 3.8'den zemine düşen meyve 11.4 birim/sn'ye
    ///    çıkıyor, 6.0'dan düşen 13.1'e.
    ///  - <b>Duvarların üst kenarı</b> — danger line ile arasında en az bir karpuz
    ///    çapı (2.45) pay olmalı, yoksa meyve duvarın üstünden yanlara kaçar.
    ///    5.38 - 2.12 = 3.26, yeterli.
    /// </summary>
    const float DropY       = 3.8f;
    const float DangerLineY = 2.12f;
    const float WallTopY    = 5.38f;

    static int FixBoardLayout(Scene scene)
    {
        int changed = 0;

        // --- dropY: dalın yüksekliği. DropController Start'ta buradan okuyup DropZone'u
        // oraya taşıyor, yani asıl kaynak config.
        var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);

        if (config == null)
        {
            Debug.LogWarning($"SceneFixups: {ConfigPath} bulunamadı, dropY güncellenemedi.");
        }
        else if (!Mathf.Approximately(config.dropY, DropY))
        {
            Undo.RecordObject(config, "dropY");

            float old = config.dropY;

            config.dropY = DropY;

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Debug.Log($"SceneFixups: GameConfig.dropY {old} → {DropY}.");

            changed++;
        }

        // --- DropZone: sahnedeki konum da eşleşsin ki edit mode'da doğru görünsün.
        Transform dropZone = FindDeep(scene, "DropZone");

        if (dropZone != null && !Mathf.Approximately(dropZone.position.y, DropY))
        {
            Undo.RecordObject(dropZone, "DropZone yüksekliği");

            Vector3 p = dropZone.position;
            p.y = DropY;
            dropZone.position = p;

            changed++;
        }

        // --- DangerLine: GameOverDetector eşiği objenin KENDİ Y'sinden okuyor
        // (LineY => transform.position.y), görsel de aynı objedeki SpriteRenderer.
        Transform danger = FindDeep(scene, "DangerLine");

        if (danger == null)
        {
            Debug.LogWarning("SceneFixups: DangerLine bulunamadı.");
        }
        else if (!Mathf.Approximately(danger.position.y, DangerLineY))
        {
            Undo.RecordObject(danger, "DangerLine yüksekliği");

            float old = danger.position.y;

            Vector3 p = danger.position;
            p.y = DangerLineY;
            danger.position = p;

            Debug.Log($"SceneFixups: DangerLine {old} → {DangerLineY}.");

            changed++;
        }

        // --- Duvarlar: ALT kenar olduğu yerde kalsın, üst kenar WallTopY'ye çıksın.
        changed += RaiseWall(scene, "Wall_Left");
        changed += RaiseWall(scene, "Wall_Right");

        return changed;
    }

    /// <summary>
    /// Duvarın alt kenarını sabit tutup üst kenarını <see cref="WallTopY"/>'ye taşır.
    /// Alt kenar korunduğu için tekrar tekrar çalıştırmak aynı sonucu veriyor.
    /// </summary>
    static int RaiseWall(Scene scene, string name)
    {
        Transform wall = FindDeep(scene, name);

        if (wall == null)
        {
            Debug.LogWarning($"SceneFixups: {name} bulunamadı.");

            return 0;
        }

        var box = wall.GetComponent<BoxCollider2D>();

        if (box == null)
        {
            Debug.LogWarning($"SceneFixups: {name} üzerinde BoxCollider2D yok.");

            return 0;
        }

        float halfH  = box.size.y * 0.5f;
        float bottom = wall.position.y + box.offset.y - halfH;
        float top    = wall.position.y + box.offset.y + halfH;

        if (Mathf.Abs(top - WallTopY) < 0.001f) return 0;

        float newHalf   = (WallTopY - bottom) * 0.5f;
        float newCentre = bottom + newHalf;

        Undo.RecordObject(wall, $"{name} yüksekliği");
        Undo.RecordObject(box, $"{name} collider");

        Vector3 p = wall.position;
        p.y = newCentre - box.offset.y;
        wall.position = p;

        box.size = new Vector2(box.size.x, newHalf * 2f);

        EditorUtility.SetDirty(wall);
        EditorUtility.SetDirty(box);

        Debug.Log($"SceneFixups: {name} üst kenarı {top:0.##} → {WallTopY} " +
                  $"(alt kenar {bottom:0.##} korundu, yükseklik {newHalf * 2f:0.##}).");

        return 1;
    }

    // --------------------------------------------------- 2) GameOverPanel dimmer

    static int FixGameOverDimmer(Scene scene)
    {
        Transform panel = FindDeep(scene, "GameOverPanel");

        if (panel == null)
        {
            Debug.LogWarning("SceneFixups: GameOverPanel bulunamadı.");

            return 0;
        }

        var dim = panel.Find("Dimmer") as RectTransform;

        if (dim == null)
        {
            Debug.LogWarning("SceneFixups: GameOverPanel/Dimmer bulunamadı.");

            return 0;
        }

        bool ok = dim.anchorMin == Vector2.zero
                  && dim.anchorMax == Vector2.one
                  && dim.offsetMin == Vector2.zero
                  && dim.offsetMax == Vector2.zero;

        if (ok) return 0;

        Undo.RecordObject(dim, "GameOverPanel dimmer tam ekran");

        // Anchor'lar da yazılıyor: offset'leri sıfırlamak ancak tam stretch'te
        // "ekranı kapla" anlamına geliyor.
        dim.anchorMin = Vector2.zero;
        dim.anchorMax = Vector2.one;
        dim.offsetMin = Vector2.zero;
        dim.offsetMax = Vector2.zero;

        EditorUtility.SetDirty(dim);

        Debug.Log("SceneFixups: GameOverPanel/Dimmer tam ekran stretch'e çekildi.");

        return 1;
    }

    // ------------------------------------------------------------------ yardımcı

    /// <summary>Pasif objeler dahil, isme göre ilk eşleşen Transform.</summary>
    static Transform FindDeep(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
        }

        return null;
    }
}
