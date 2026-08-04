using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    const string SessionKey = "FruitMerge.SceneFixups.Ran.v10";

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
                      + FixFruitPhysics()
                      + FixPanelSubCanvases(scene)
                      + FixScoreSubCanvas(scene)
                      + FixFruitTicker(scene)
                      + FixRaycastTargets(scene)
                      + FixAtlasCompression();

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

    // --------------------------------------- 8) panellere iç içe Canvas (overdraw)

    /// <summary>
    /// Panellerin köküne İÇ İÇE <c>Canvas</c> ekler. <see cref="UIPanel"/> panel kapanınca
    /// <c>canvas.enabled = false</c> yapıyor.
    ///
    /// <b>Neden:</b> <c>CanvasGroup.alpha = 0</c> çizimi DURDURMUYOR — geometri yine
    /// kuruluyor, GPU şeffaf dörtgenleri yine harmanlıyor. Oynanış sırasında dört panelin
    /// dört TAM EKRAN Dimmer/Background'u + ~48 küçük graphic'i boşuna çiziliyordu ve
    /// mobilde darboğaz neredeyse her zaman fill-rate.
    ///
    /// <b>Neden <c>SetActive(false)</c> değil:</b> GameObject aktif kalmalı, yoksa panel
    /// <c>OnDisable</c>'da aboneliğini bırakır ve durum olayını bir daha duymaz.
    /// </summary>
    static readonly string[] PanelNames =
    {
        "MenuPanel", "PausePanel", "GameOverPanel", "BoostShopPanel", "SplashPanel"
    };

    static int FixPanelSubCanvases(Scene scene)
    {
        int changed = 0;

        for (int i = 0; i < PanelNames.Length; i++)
        {
            Transform panel = FindDeep(scene, PanelNames[i]);

            if (panel == null)
            {
                Debug.LogWarning($"SceneFixups: {PanelNames[i]} bulunamadı, alt canvas eklenemedi.");

                continue;
            }

            // interactive: TRUE — paneller buton içeriyor, iç içe canvas'ın KENDİ
            // GraphicRaycaster'ı olmak zorunda (bkz. EnsureSubCanvas).
            changed += EnsureSubCanvas(panel.gameObject, true, PanelNames[i]);
        }

        return changed;
    }

    // ------------------------------------- 9) skor yazısına ayrı alt canvas

    /// <summary>
    /// <c>ScoreText</c>'e kendi <c>Canvas</c>'ını verir.
    ///
    /// <b>Neden:</b> HUDCanvas'ta skor yazısıyla birlikte 11 slotluk evrim zinciri
    /// (23 graphic + HorizontalLayoutGroup) ve boost rozetleri duruyor — toplam ~34
    /// CanvasRenderer. <c>HUDView</c> skoru saydığı sürece her karede <c>SetText</c>
    /// çağırıyor ve TMP mesh'i değiştiği an ALT CANVAS'IN TAMAMI yeniden batch'leniyor.
    /// Yani üç haneli bir sayının değişmesi, hiç değişmemiş 23 meyve ikonunun geometrisini
    /// yeniden birleştirmeye sebep oluyordu.
    ///
    /// Yazıyı yeniden ebeveynlemek yerine Canvas'ı ONUN ÜSTÜNE koyuyoruz: RectTransform
    /// zinciri, anchor'lar ve serialize edilmiş referanslar hiç değişmiyor.
    ///
    /// <c>HighScoreText</c> taşınmıyor — yalnızca rekor kırılınca değişiyor, ayrı canvas
    /// maliyetine değmez.
    /// </summary>
    static int FixScoreSubCanvas(Scene scene)
    {
        Transform score = FindDeep(scene, "ScoreText");

        if (score == null)
        {
            Debug.LogWarning("SceneFixups: ScoreText bulunamadı, alt canvas eklenemedi.");

            return 0;
        }

        // interactive: FALSE — skor yazısı salt gösterim, raycastTarget'ı da kapatıldı
        // (bkz. FixRaycastTargets), yani kendi raycaster'ına ihtiyacı yok.
        return EnsureSubCanvas(score.gameObject, false, "ScoreText");
    }

    /// <summary>
    /// Objeye iç içe bir <c>Canvas</c> (ve gerekiyorsa <c>GraphicRaycaster</c>) ekler.
    ///
    /// <b>⚠️ GraphicRaycaster ŞART — bu unutulduğunda paneldeki bütün butonlar ÖLÜYOR.</b>
    /// Bir <c>Graphic</c> kendini EN YAKIN Canvas'a kaydediyor
    /// (<c>GraphicRegistry.RegisterGraphicForCanvas</c>), <c>GraphicRaycaster</c> ise yalnızca
    /// KENDİ canvas'ının graphic'lerini sınıyor. Yani bir panele iç içe Canvas eklemek,
    /// panelin butonlarını üst canvas'ın raycaster'ının görüş alanından çıkarıyor.
    /// Sahnedeki <c>MainCanvas</c> / <c>HUDCanvas</c> / <c>PanelCanvas</c> üçlüsünün
    /// her birinde ayrı bir raycaster olmasının sebebi tam olarak bu.
    ///
    /// Varsayılan <c>GraphicRaycaster</c> değerleri sahnedeki mevcut üç raycaster'la birebir
    /// aynı (<c>ignoreReversedGraphics</c> açık, <c>blockingObjects</c> None, maske tam),
    /// o yüzden ayar kopyalamaya gerek yok.
    ///
    /// <c>additionalShaderChannels</c> en yakın üst Canvas'tan KOPYALANIYOR: TextMeshPro'nun
    /// SDF shader'ı TexCoord1 + Normal + Tangent kanallarına ihtiyaç duyuyor ve yeni bir
    /// Canvas bunları varsayılan olarak KAPALI getiriyor — kopyalanmazsa yazılar bozuk
    /// çiziliyor. Sahnedeki üç alt canvas'ta bu değer 25.
    ///
    /// <b>Etkileşimli</b> alt canvas'larda <c>overrideSorting</c> AÇILIP üst canvas'ın
    /// <c>sortingOrder</c>'ı devralınıyor — kapalı bırakmak sırayı 0'a düşürüyor ve
    /// panelleri HUD'un arkasına atabiliyor (hem çizimde hem raycast'te). Salt gösterim
    /// alt canvas'larında (skor yazısı) kapalı kalıyor: orada sıra hiyerarşiden gelmeli.
    ///
    /// Fikirsiz: üç ayar (Canvas, sıralama, raycaster) AYRI AYRI kontrol ediliyor, böylece
    /// Canvas'ı zaten eklenmiş bir objeye sonradan eksikleri tamamlamak da çalışıyor.
    /// </summary>
    /// <returns>eklenen bileşen sayısı</returns>
    static int EnsureSubCanvas(GameObject go, bool interactive, string label)
    {
        int changed = 0;

        Canvas parent = go.transform.parent != null
            ? go.transform.parent.GetComponentInParent<Canvas>()
            : null;

        Canvas canvas = go.GetComponent<Canvas>();

        if (canvas == null)
        {
            canvas = Undo.AddComponent<Canvas>(go);

            canvas.additionalShaderChannels =
                parent != null && parent.additionalShaderChannels != AdditionalCanvasShaderChannels.None
                    ? parent.additionalShaderChannels
                    : AdditionalCanvasShaderChannels.TexCoord1
                      | AdditionalCanvasShaderChannels.Normal
                      | AdditionalCanvasShaderChannels.Tangent;

            EditorUtility.SetDirty(canvas);

            Debug.Log($"SceneFixups: {label}'a iç içe Canvas eklendi — kapalıyken tuvalden " +
                      "tamamen çıkıyor (alpha 0 çizimi durdurmuyordu).");

            changed++;
        }

        // ⚠️ SIRALAMAYI KORU. Etkileşimli alt canvas'ta overrideSorting KAPALI kalırsa
        // sortingOrder 0 olarak okunuyor — hem çizimde hem RAYCAST'te. GraphicRaycaster'ın
        // sortOrderPriority'si doğrudan canvas.sortingOrder'dan geliyor, yani PanelCanvas'ın
        // 2'si kaybolup HUDCanvas'ın 0'ıyla eşitleniyor ve panel butonları HUD'un arkasına
        // düşebiliyor. Üst canvas'ın sırasını devralarak eski öncelik birebir korunuyor.
        if (interactive && parent != null &&
            (!canvas.overrideSorting || canvas.sortingOrder != parent.sortingOrder))
        {
            Undo.RecordObject(canvas, "alt canvas sıralaması");

            canvas.overrideSorting = true;
            canvas.sortingOrder    = parent.sortingOrder;

            EditorUtility.SetDirty(canvas);

            Debug.Log($"SceneFixups: {label} alt canvas'ı sortingOrder {parent.sortingOrder} " +
                      "olarak sabitlendi (üst canvas'ın sırası devralındı).");

            changed++;
        }

        if (interactive && go.GetComponent<GraphicRaycaster>() == null)
        {
            Undo.AddComponent<GraphicRaycaster>(go);

            Debug.Log($"SceneFixups: {label}'a GraphicRaycaster eklendi — iç içe canvas'ın " +
                      "graphic'leri üst canvas'ın raycaster'ı tarafından görülmüyor, " +
                      "bu olmadan paneldeki butonlar tıklanamıyor.");

            changed++;
        }

        return changed;
    }

    // ------------------------------------------------- 10) FruitTicker

    /// <summary>
    /// Sahneye <see cref="FruitTicker"/> ekler — <see cref="FruitPool"/>'un objesine.
    ///
    /// <see cref="Fruit"/>'in kendi <c>Update</c>/<c>FixedUpdate</c>'i kaldırıldı (60 meyve
    /// = kare başına 60, fizik adımı başına 60 managed↔native geçişi). Tick'i artık tek bir
    /// döngü sürüyor; bu bileşen sahnede yoksa meyveler pop/squash animasyonunu ve
    /// Continuous→Discrete geçişini HİÇ yapmaz, o yüzden eklenmesi zorunlu.
    /// </summary>
    static int FixFruitTicker(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<FruitTicker>(true) != null) return 0;

        FruitPool pool = null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            pool = root.GetComponentInChildren<FruitPool>(true);

            if (pool != null) break;
        }

        if (pool == null)
        {
            Debug.LogWarning("SceneFixups: FruitPool bulunamadı, FruitTicker eklenemedi — " +
                             "meyvelerin pop/squash animasyonu çalışmaz!");

            return 0;
        }

        Undo.AddComponent<FruitTicker>(pool.gameObject);

        Debug.Log($"SceneFixups: {pool.name} objesine FruitTicker eklendi " +
                  "(Fruit'in kendi Update/FixedUpdate'i kaldırıldı).");

        return 1;
    }

    // --------------------------------------------- 11) gereksiz raycast target

    /// <summary>
    /// Tıklanması hiç gerekmeyen elemanlarda <c>Raycast Target</c>'ı kapatır.
    ///
    /// İki kural:
    ///  1. <b>Buton İÇİNDEKİ</b> graphic'ler — butonun kendi <c>targetGraphic</c>'i
    ///     tıklamayı zaten yakalıyor, çocuk yazı/ikonun ayrıca hedef olması her pointer
    ///     olayında fazladan sınama demek.
    ///  2. Salt gösterim <b>etiketleri</b> (skor, rekor, sonuç ekranı yazıları).
    ///
    /// <b>DOKUNULMAYANLAR</b> — bunlar davranışsal:
    ///  - <c>HudPanel</c>: <see cref="DropController"/> <c>PointerInput.IsOverUI()</c> ile
    ///    HUD'un üstündeki dokunuşu BİLEREK eliyor ("meyve görünmeyen bir yere
    ///    bırakılmasın"). Kapatmak HUD alanına dokunulduğunda meyve düşürür.
    ///  - Panellerin <c>Dimmer</c> / <c>Background</c> / <c>Box</c> görselleri: arkadaki
    ///    tıklamayı yutmaları gerekiyor.
    ///  - Butonların KENDİ görselleri.
    /// </summary>
    static readonly string[] DisplayOnlyLabels =
    {
        "ScoreText", "HighScoreText", "ScoreLabel", "ScoreCaption", "BestLabel", "BestCaption"
    };

    static int FixRaycastTargets(Scene scene)
    {
        int changed = 0;

        // 1) buton içindeki graphic'ler
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                foreach (Graphic g in button.GetComponentsInChildren<Graphic>(true))
                {
                    // butonun KENDİ görseli ve hedef görseli hedef kalmalı
                    if (g.gameObject == button.gameObject) continue;

                    if (g == button.targetGraphic) continue;

                    // araya başka bir buton girmişse (iç içe buton) ona karışma
                    if (g.GetComponentInParent<Button>() != button) continue;

                    if (!g.raycastTarget) continue;

                    Undo.RecordObject(g, "raycastTarget kapat");

                    g.raycastTarget = false;

                    EditorUtility.SetDirty(g);

                    Debug.Log($"SceneFixups: {button.name}/{g.name} raycastTarget kapatıldı " +
                              "(butonun kendi görseli tıklamayı zaten yakalıyor).");

                    changed++;
                }
            }
        }

        // 2) salt gösterim etiketleri
        for (int i = 0; i < DisplayOnlyLabels.Length; i++)
        {
            Transform t = FindDeep(scene, DisplayOnlyLabels[i]);

            var g = t != null ? t.GetComponent<Graphic>() : null;

            if (g == null || !g.raycastTarget) continue;

            Undo.RecordObject(g, "raycastTarget kapat");

            g.raycastTarget = false;

            EditorUtility.SetDirty(g);

            Debug.Log($"SceneFixups: {DisplayOnlyLabels[i]} raycastTarget kapatıldı (salt gösterim).");

            changed++;
        }

        return changed;
    }

    // ------------------------------------------ 12) atlas mobil sıkıştırması

    /// <summary>
    /// Sprite atlas'larına Android/iOS için <b>ASTC 4×4</b> override'ı koyar.
    ///
    /// <b>Sorun:</b> iki atlas da <c>textureCompression: 0</c> (Uncompressed) ile import
    /// ediliyordu ve <c>platformSettings</c> boştu — yani çalışma anındaki atlas dokusu
    /// piksel başına 4 bayt. FruitAtlas'ın kapsadığı alan (11 meyve gövdesi + 48 yüz
    /// sprite'ı) 2048² sayfalara sığdığında ~2 sayfa, yani ~33 MB; UIAtlas da en az bir
    /// sayfa (+16 MB). Bir merge oyunu için gereğinin kat kat üstü, ve aynı zamanda her
    /// karede örneklenen bant genişliği.
    ///
    /// <b>Neden ASTC 4×4:</b> piksel başına 8 bit, alfa dahil — RGBA32'ye göre 4 kat
    /// tasarruf, pratikte ayırt edilemeyecek kalite. Daha agresif olan 6×6, keskin kenarlı
    /// düz renk alanlı bu çizim tarzında (özellikle yüzlerin ince çizgilerinde) blok
    /// artefaktı gösterebiliyor — bilinçli olarak 4×4'te kalıyoruz.
    ///
    /// Mipmap KAPALI kalıyor (ortografik 2D'de gereksiz) ve maxTextureSize 2048'de kalıyor.
    /// </summary>
    static readonly string[] AtlasPaths =
    {
        "Assets/FruitMerge/Art/Fruits/FruitAtlas.spriteatlasv2",
        "Assets/FruitMerge/Art/UI/UIAtlas.spriteatlasv2"
    };

    // iOS'un import platformu adı "iPhone" (BuildTarget.iOS değil).
    static readonly string[] MobileTargets = { "Android", "iPhone" };

    static int FixAtlasCompression()
    {
        int changed = 0;

        for (int i = 0; i < AtlasPaths.Length; i++) changed += SetAtlasCompression(AtlasPaths[i]);

        return changed;
    }

    static int SetAtlasCompression(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as SpriteAtlasImporter;

        if (importer == null)
        {
            Debug.LogWarning($"SceneFixups: {path} bir SpriteAtlasImporter değil (ya da yok) — atlandı.");

            return 0;
        }

        int changed = 0;

        for (int i = 0; i < MobileTargets.Length; i++)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformSettings(MobileTargets[i]);

            bool ok = settings.overridden
                      && settings.format == TextureImporterFormat.ASTC_4x4
                      && settings.maxTextureSize == 2048;

            if (ok) continue;

            settings.overridden     = true;
            settings.format         = TextureImporterFormat.ASTC_4x4;
            settings.maxTextureSize = 2048;
            settings.textureCompression = TextureImporterCompression.Compressed;
            settings.compressionQuality = 50;

            importer.SetPlatformSettings(settings);

            changed++;
        }

        if (changed == 0) return 0;

        importer.SaveAndReimport();

        Debug.Log($"SceneFixups: {System.IO.Path.GetFileName(path)} → Android/iOS ASTC 4×4 " +
                  "(eskiden Uncompressed RGBA32, ~4 kat doku belleği).");

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
