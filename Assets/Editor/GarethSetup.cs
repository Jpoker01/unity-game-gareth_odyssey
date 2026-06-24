// Assets/Editor/GarethSetup.cs
// Menu: Gareth Odyssey > …
// Steps 1 & 4 also run automatically on every recompile.

using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine.Tilemaps;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

[InitializeOnLoad]
public static class GarethSetup
{
    static GarethSetup()
    {
        EditorApplication.delayCall += AutoFixGroundCollision;
        EditorApplication.delayCall += AutoPopulateAnimation;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // AUTO-RUN on every compile
    // ═══════════════════════════════════════════════════════════════════════════

    static void AutoFixGroundCollision()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        const string layerName = "Ground";
        int groundLayer = LayerMask.NameToLayer(layerName);
        if (groundLayer == -1) return;

        bool changed = false;
        foreach (var tm in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            var go = tm.gameObject;
            string ln = go.name.ToLower();
            if (!ln.Contains("solid") && !ln.Contains("ground") &&
                !ln.Contains("floor") && !ln.Contains("platform"))
                continue;

            if (go.GetComponent<TilemapCollider2D>() == null)
            {
                go.AddComponent<TilemapCollider2D>();
                EditorUtility.SetDirty(go);
                changed = true;
            }
            if (go.layer != groundLayer)
            {
                go.layer = groundLayer;
                EditorUtility.SetDirty(go);
                changed = true;
            }
        }

        var player = Object.FindFirstObjectByType<PlayerController>();
        if (player != null && player.groundLayer.value != (1 << groundLayer))
        {
            player.groundLayer = 1 << groundLayer;
            EditorUtility.SetDirty(player);
            changed = true;
        }

        if (changed)
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    static void AutoPopulateAnimation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        
        const string sheetPath = "Assets/Sprites/Archaeologist Sprite Sheet.png";
        var sprites = AssetDatabase.LoadAllAssetsAtPath(sheetPath)
            .OfType<Sprite>().OrderBy(s => ParseSpriteIndex(s.name)).ToArray();
        if (sprites.Length < 42) return;

        var anim = Object.FindFirstObjectByType<PlayerSpriteAnimation>();
        if (anim == null) return;
        bool anyEmpty = anim.idle == null || anim.idle.Length == 0
                     || anim.walk == null || anim.walk.Length == 0;
        if (!anyEmpty) return;

        AssignFrames(anim, sprites);
        EditorUtility.SetDirty(anim);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MENU ITEMS
    // ═══════════════════════════════════════════════════════════════════════════

    // ── Step 1 ──────────────────────────────────────────────────────────────
    [MenuItem("Gareth Odyssey/1 - Fix Ground Collision")]
    static void FixGroundCollision()
    {
        const string layerName = "Ground";
        int groundLayer = LayerMask.NameToLayer(layerName);
        if (groundLayer == -1)
        {
            EditorUtility.DisplayDialog("Missing Layer",
                $"Layer '{layerName}' not found.\n\nEdit > Project Settings > Tags and Layers — add 'Ground' as a User Layer.", "OK");
            return;
        }

        int count = 0;
        foreach (var tm in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            var go = tm.gameObject;
            string ln = go.name.ToLower();
            if (!ln.Contains("solid") && !ln.Contains("ground") &&
                !ln.Contains("floor") && !ln.Contains("platform")) continue;
            if (go.GetComponent<TilemapCollider2D>() == null)
                go.AddComponent<TilemapCollider2D>();
            go.layer = groundLayer;
            EditorUtility.SetDirty(go);
            count++;
        }

        var player = Object.FindFirstObjectByType<PlayerController>();
        if (player != null) { player.groundLayer = 1 << groundLayer; EditorUtility.SetDirty(player); }
        else Debug.LogWarning("[GarethSetup] No PlayerController in scene.");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Ground Collision",
            count > 0 ? $"Fixed {count} tilemap(s). Press Ctrl+S to save."
                      : "No solid tilemaps found.", "OK");
    }

    // ── Step 2 ──────────────────────────────────────────────────────────────
    [MenuItem("Gareth Odyssey/2 - Create Hazard Tilemap")]
    static void CreateHazardTilemap()
    {
        bool tagExists = InternalEditorUtility.tags.Contains("Hazard");
        if (!tagExists)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset");
            var so = new SerializedObject(asset);
            var tags = so.FindProperty("tags");
            int idx = tags.arraySize;
            tags.InsertArrayElementAtIndex(idx);
            tags.GetArrayElementAtIndex(idx).stringValue = "Hazard";
            so.ApplyModifiedProperties();
        }

        var grid = Object.FindFirstObjectByType<Grid>();
        if (grid == null) { EditorUtility.DisplayDialog("No Grid", "Open the level scene first.", "OK"); return; }
        if (grid.transform.Find("HazardTilemap") != null) { EditorUtility.DisplayDialog("Already Exists", "HazardTilemap already exists.", "OK"); return; }

        var go = new GameObject("HazardTilemap");
        Undo.RegisterCreatedObjectUndo(go, "Create HazardTilemap");
        go.transform.SetParent(grid.transform, false);
        go.tag = "Hazard";
        go.AddComponent<Tilemap>();
        go.AddComponent<TilemapRenderer>();
        go.AddComponent<TilemapCollider2D>().isTrigger = true;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("HazardTilemap Created",
            "HazardTilemap added under the Grid.\n\nPaint spike tiles on it via the Tile Palette.\nDO NOT paint ground tiles on it — hazards only.", "OK");
    }

    // ── Step 3 ──────────────────────────────────────────────────────────────
    [MenuItem("Gareth Odyssey/3 - Add Lives HUD")]
    static void AddLivesHUD()
    {
        var player = Object.FindFirstObjectByType<PlayerController>();
        if (player == null) { EditorUtility.DisplayDialog("No Player", "Drag Player.prefab into the scene first.", "OK"); return; }
        if (player.GetComponent<LivesHUD>() != null) { EditorUtility.DisplayDialog("Already Added", "LivesHUD is already on the Player.", "OK"); return; }
        player.gameObject.AddComponent<LivesHUD>();
        EditorUtility.SetDirty(player.gameObject);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Lives HUD Added", "LivesHUD added. Note: step 6 creates a Canvas-based HeartsUI too.", "OK");
    }

    // ── Step 4 ──────────────────────────────────────────────────────────────
    [MenuItem("Gareth Odyssey/4 - Setup Animation Sprites")]
    static void SetupAnimationSprites()
    {
        const string sheetPath = "Assets/Sprites/Archaeologist Sprite Sheet.png";
        var sprites = AssetDatabase.LoadAllAssetsAtPath(sheetPath)
            .OfType<Sprite>().OrderBy(s => ParseSpriteIndex(s.name)).ToArray();
        if (sprites.Length < 42)
        {
            EditorUtility.DisplayDialog("Sprite Sheet Not Found",
                $"Could not load sprites from:\n{sheetPath}\n\nMake sure it is imported as Sprite Mode: Multiple, 64×32 cells.", "OK");
            return;
        }

        var player = Object.FindFirstObjectByType<PlayerController>();
        if (player == null) { EditorUtility.DisplayDialog("No Player", "Drag Player.prefab into the scene first.", "OK"); return; }

        var anim = player.GetComponent<PlayerSpriteAnimation>();
        if (anim == null) anim = player.gameObject.AddComponent<PlayerSpriteAnimation>();

        AssignFrames(anim, sprites);
        EditorUtility.SetDirty(player.gameObject);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Animation Sprites Assigned",
            $"Populated {sprites.Length} sprites (Idle/Walk/Tool/Jump/Fall/Hurt).\n\n" +
            "NEXT: Add CameraFollow to the Main Camera and set Target = Player.\nPress Ctrl+S to save.", "OK");
    }

    // ── Step 5 ──────────────────────────────────────────────────────────────
    [MenuItem("Gareth Odyssey/5 - Fix Hazard Tags")]
    static void FixHazardTags()
    {
        int cleared = 0;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go.CompareTag("Hazard") && go.name != "HazardTilemap")
            {
                go.tag = "Untagged";
                EditorUtility.SetDirty(go);
                cleared++;
            }
        }
        var grid = Object.FindFirstObjectByType<Grid>();
        if (grid != null)
        {
            var ht = grid.transform.Find("HazardTilemap");
            if (ht != null)
            {
                var tc = ht.GetComponent<TilemapCollider2D>();
                if (tc != null && !tc.isTrigger) { tc.isTrigger = true; EditorUtility.SetDirty(ht.gameObject); }
            }
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Hazard Tags",
            cleared > 0 ? $"Cleared 'Hazard' tag from {cleared} wrong object(s)."
                        : "No wrongly-tagged objects found.\nIf ground still damages: repaint ground tiles on SolidTilemap, not HazardTilemap.", "OK");
    }

    // ── Step 6 ──────────────────────────────────────────────────────────────
    [MenuItem("Gareth Odyssey/6 - Create Level UI")]
    static void CreateLevelUI()
    {
        if (Object.FindFirstObjectByType<LevelManager>() != null)
        {
            EditorUtility.DisplayDialog("Already Exists",
                "LevelManager found in scene. Delete 'LevelManager' and 'LevelUI Canvas' GameObjects first to re-run.", "OK");
            return;
        }

        Font font = GetFont();
        Color boxBg  = new Color(0.10f, 0.10f, 0.14f, 0.97f);
        Color cGreen = new Color(0.18f, 0.42f, 0.31f);
        Color cBlue  = new Color(0.11f, 0.31f, 0.54f);
        Color cOrange= new Color(0.62f, 0.29f, 0.00f);
        Color cRed   = new Color(0.48f, 0.06f, 0.06f);
        Color cGold  = new Color(0.72f, 0.52f, 0.07f);
        Color cSlate = new Color(0.17f, 0.29f, 0.49f);
        Color cYellow= new Color(1.00f, 0.93f, 0.53f);
        Color cGoldTxt = new Color(1.00f, 0.84f, 0.00f);

        // ── EventSystem ──────────────────────────────────────────────────────
        EnsureEventSystem();

        // ── Canvas ───────────────────────────────────────────────────────────
        var canvasGO = new GameObject("LevelUI Canvas");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Level UI");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();
        var ct = canvasGO.transform;

        // ── LevelManager ─────────────────────────────────────────────────────
        var lmGO = new GameObject("LevelManager");
        Undo.RegisterCreatedObjectUndo(lmGO, "Create LevelManager");
        var lm = lmGO.AddComponent<LevelManager>();

        // ── Hearts (top-left) ─────────────────────────────────────────────────
        var heartsGO = new GameObject("HeartsDisplay");
        heartsGO.transform.SetParent(ct, false);
        var hrt = heartsGO.AddComponent<RectTransform>();
        hrt.anchorMin = hrt.anchorMax = new Vector2(0f, 1f);
        hrt.pivot     = new Vector2(0f, 1f);
        hrt.anchoredPosition = new Vector2(18f, -18f);
        hrt.sizeDelta = new Vector2(280f, 42f);
        var heartsText = heartsGO.AddComponent<Text>();
        heartsText.text = "♥ ♥ ♥";
        heartsText.font = font;
        heartsText.fontSize = 30;
        heartsText.color = new Color(1f, 0.27f, 0.27f);
        heartsText.alignment = TextAnchor.MiddleLeft;
        heartsText.supportRichText = true;
        var hui = heartsGO.AddComponent<HeartsUI>();
        // player will be auto-found in HeartsUI.Start()

        // ── Pickup Prompt (bottom-centre, hidden) ────────────────────────────
        var promptGO = new GameObject("PickupPrompt");
        promptGO.transform.SetParent(ct, false);
        var prt = promptGO.AddComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f);
        prt.pivot = new Vector2(0.5f, 0f);
        prt.anchoredPosition = new Vector2(0f, 60f);
        prt.sizeDelta = new Vector2(420f, 44f);
        var promptText = promptGO.AddComponent<Text>();
        promptText.text = "Press E to pick up artefact";
        promptText.font = font;
        promptText.fontSize = 20;
        promptText.color = cYellow;
        promptText.alignment = TextAnchor.MiddleCenter;
        promptText.supportRichText = true;
        promptGO.SetActive(false);

        // ── Intro Panel ───────────────────────────────────────────────────────
        var introOverlay = MakeOverlay(ct, "IntroPanel");
        var introBox     = MakeBox(introOverlay.transform, "IntroBox", 700f, 500f, boxBg);
        var introBoxT    = introBox.transform;
        var ititle = MakeLabel(introBoxT, "IntroTitle", lm.levelTitle,
            font, 26, cYellow, TextAnchor.MiddleCenter, new Vector2(0f, 202f), new Vector2(660f, 60f));
        var ictx = MakeLabel(introBoxT, "IntroContext", lm.levelContext,
            font, 16, Color.white, TextAnchor.UpperCenter, new Vector2(0f, 30f), new Vector2(640f, 230f), true);
        var iBtn = MakeButton(introBoxT, "IntroContinueBtn", "Choose Difficulty  →",
            font, cGreen, 18, new Vector2(0f, -210f), new Vector2(260f, 52f));

        // ── Difficulty Panel ──────────────────────────────────────────────────
        var diffOverlay = MakeOverlay(ct, "DifficultyPanel");
        var diffBox     = MakeBox(diffOverlay.transform, "DiffBox", 540f, 420f, boxBg);
        var dbt         = diffBox.transform;
        MakeLabel(dbt, "DiffTitle", "Select Difficulty", font, 24, Color.white,
            TextAnchor.MiddleCenter, new Vector2(0f, 163f), new Vector2(500f, 55f));
        var dEasy = MakeButton(dbt, "EasyBtn", "Easy  —  Unlimited Lives  (Exploration)",
            font, cGreen, 15, new Vector2(0f, 90f), new Vector2(480f, 48f));
        var dMed  = MakeButton(dbt, "MedBtn",  "Medium  —  5 Lives  (Balanced Challenge)",
            font, cBlue,  15, new Vector2(0f, 30f), new Vector2(480f, 48f));
        var dHard = MakeButton(dbt, "HardBtn", "Hard  —  3 Lives  (Demanding Precision)",
            font, cOrange,15, new Vector2(0f, -30f), new Vector2(480f, 48f));
        var dImp  = MakeButton(dbt, "ImpossibleBtn","Impossible  —  1 Life  (Single Attempt)",
            font, cRed,   15, new Vector2(0f, -90f), new Vector2(480f, 48f));

        // ── Pause Panel ───────────────────────────────────────────────────────
        var pauseOverlay = MakeOverlay(ct, "PausePanel");
        var pauseBox     = MakeBox(pauseOverlay.transform, "PauseBox", 560f, 560f, boxBg);
        var pb           = pauseBox.transform;
        MakeLabel(pb, "PauseTitle", "PAUSED", font, 30, Color.white,
            TextAnchor.MiddleCenter, new Vector2(0f, 243f), new Vector2(500f, 58f));
        var pResume = MakeButton(pb, "ResumeBtn", "Resume",
            font, cGreen, 18, new Vector2(0f, 177f), new Vector2(320f, 48f));
        MakeLabel(pb, "DiffLabel", "Change Difficulty:", font, 15,
            new Color(0.7f,0.7f,0.7f), TextAnchor.MiddleCenter,
            new Vector2(0f, 118f), new Vector2(500f, 32f));
        // 4 small difficulty buttons in a row (centres at x: -185, -62, +62, +185)
        var pDE = MakeButton(pb, "PauseDiffEasyBtn",  "Easy",
            font, cGreen,  12, new Vector2(-185f, 66f), new Vector2(115f, 38f));
        var pDM = MakeButton(pb, "PauseDiffMedBtn",   "Medium",
            font, cBlue,   12, new Vector2(-62f,  66f), new Vector2(115f, 38f));
        var pDH = MakeButton(pb, "PauseDiffHardBtn",  "Hard",
            font, cOrange, 12, new Vector2( 62f,  66f), new Vector2(115f, 38f));
        var pDI = MakeButton(pb, "PauseDiffImpBtn",   "Impossible",
            font, cRed,    11, new Vector2( 185f, 66f), new Vector2(115f, 38f));
        var pRestart = MakeButton(pb, "RestartBtn", "Restart Level",
            font, cSlate, 18, new Vector2(0f, -5f), new Vector2(320f, 48f));
        var pExit  = MakeButton(pb, "ExitLevelBtn", "Exit Level  (Lose Artefact)",
            font, cGold,  16, new Vector2(0f, -65f), new Vector2(320f, 48f));
        var pQuit  = MakeButton(pb, "ExitGameBtn", "Quit to Desktop",
            font, cRed,   18, new Vector2(0f, -135f), new Vector2(320f, 48f));

        // ── Complete Panel ────────────────────────────────────────────────────
        var compOverlay = MakeOverlay(ct, "CompletePanel", 0.92f);
        var compBox     = MakeBox(compOverlay.transform, "CompleteBox", 640f, 430f, boxBg);
        var cb          = compBox.transform;
        var compTitle = MakeLabel(cb, "CompleteTitle", "Artefact Recovered!",
            font, 28, cGoldTxt, TextAnchor.MiddleCenter, new Vector2(0f, 178f), new Vector2(600f, 60f));
        var compArt = MakeLabel(cb, "CompleteArtifact", lm.artifactName,
            font, 20, Color.white, TextAnchor.MiddleCenter, new Vector2(0f, 112f), new Vector2(600f, 50f));
        var compDesc = MakeLabel(cb, "CompleteDesc", lm.artifactDescription,
            font, 15, new Color(0.8f, 0.8f, 0.8f), TextAnchor.UpperCenter,
            new Vector2(0f, 15f), new Vector2(580f, 150f), true);
        var compBtn = MakeButton(cb, "CompleteContinueBtn", "Return to Museum  →",
            font, cGold, 18, new Vector2(0f, -183f), new Vector2(280f, 52f));

        // ── Game Over Panel ───────────────────────────────────────────────────
        var gameOverOverlay = MakeOverlay(ct, "GameOverPanel");
        var gameOverBox     = MakeBox(gameOverOverlay.transform, "GameOverBox", 480f, 280f, boxBg);
        var gb              = gameOverBox.transform;
        MakeLabel(gb, "GameOverTitle", "GAME OVER",
            font, 34, cRed, TextAnchor.MiddleCenter, new Vector2(0f, 100f), new Vector2(440f, 70f));
        MakeLabel(gb, "GameOverSub", "All lives lost.",
            font, 17, new Color(0.65f, 0.65f, 0.65f), TextAnchor.MiddleCenter,
            new Vector2(0f, 48f), new Vector2(440f, 38f));
        var goRestart = MakeButton(gb, "GameOverRestartBtn", "Restart Level",
            font, cSlate, 18, new Vector2(0f, -20f), new Vector2(320f, 52f));
        var goExit    = MakeButton(gb, "GameOverExitBtn", "Exit Game",
            font, cRed, 18, new Vector2(0f, -85f), new Vector2(280f, 52f));

        // ── Wire LevelManager ─────────────────────────────────────────────────
        lm.introPanel      = introOverlay;
        lm.difficultyPanel = diffOverlay;
        lm.pausePanel      = pauseOverlay;
        lm.completePanel   = compOverlay;

        lm.introTitleText    = ititle;
        lm.introContextText  = ictx;
        lm.completeTitleText    = compTitle;
        lm.completeArtifactText = compArt;
        lm.completeDescText     = compDesc;

        lm.btnIntroContinue   = iBtn;
        lm.btnDiffEasy        = dEasy;
        lm.btnDiffMedium      = dMed;
        lm.btnDiffHard        = dHard;
        lm.btnDiffImpossible  = dImp;

        lm.btnResume           = pResume;
        lm.btnPauseDiffEasy    = pDE;
        lm.btnPauseDiffMedium  = pDM;
        lm.btnPauseDiffHard    = pDH;
        lm.btnPauseDiffImpossible = pDI;
        lm.btnRestart          = pRestart;
        lm.btnExitLevel        = pExit;
        lm.btnExitGame         = pQuit;

        lm.btnCompleteContinue = compBtn;

        lm.gameOverPanel      = gameOverOverlay;
        lm.btnGameOverRestart = goRestart;
        lm.btnGameOverExit    = goExit;

        // ── Wire ArtifactPickup (if already in scene) ─────────────────────────
        var ap = Object.FindFirstObjectByType<ArtifactPickup>();
        if (ap != null) ap.promptText = promptText;

        EditorUtility.SetDirty(lmGO);
        EditorUtility.SetDirty(canvasGO);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Level UI Created",
            "Canvas + LevelManager created.\n\n" +
            "What to do now:\n" +
            "1. Select LevelManager in Hierarchy and fill in Level Title, Context, Artifact Name & Description.\n" +
            "2. Create an empty GameObject at the end of the level, add ArtifactPickup component.\n" +
            "   Then drag the 'PickupPrompt' Text (inside LevelUI Canvas) into ArtifactPickup > Prompt Text.\n" +
            "3. Run step 7 to place all checkpoint triggers in the scene.\n" +
            "4. Press Ctrl+S to save.\n\n" +
            "Flow: Level loads → frozen → intro popup → difficulty select → play.\n" +
            "ESC opens pause menu (also has difficulty change).\n" +
            "Losing all lives shows the Game Over popup (restart from last checkpoint or exit).\n" +
            "Picking up the artefact shows the complete popup.", "OK");
    }

    // ── Step 7 ──────────────────────────────────────────────────────────────
    [MenuItem("Gareth Odyssey/7 - Place Checkpoints")]
    static void PlaceCheckpoints()
    {
        // Create CheckpointManager singleton if not already in scene
        if (Object.FindFirstObjectByType<CheckpointManager>() == null)
        {
            var cmGO = new GameObject("CheckpointManager");
            Undo.RegisterCreatedObjectUndo(cmGO, "Create CheckpointManager");
            cmGO.AddComponent<CheckpointManager>();
            EditorUtility.SetDirty(cmGO);
        }

        var positions = new Vector3[]
        {
            new Vector3(  59f,   2f, 0f),
            new Vector3(  79f,   0f, 0f),
            new Vector3(  99f,  -2f, 0f),
            new Vector3( 119f, -33f, 0f),
            new Vector3( 145f, -33f, 0f),
            new Vector3( 157f, -33f, 0f),
            new Vector3( 176f, -27f, 0f),
            new Vector3( 180f, -11f, 0f),
            new Vector3( 220f,  -1f, 0f),
            new Vector3( 271f, -13f, 0f),
            new Vector3( 320f,  14f, 0f),
            new Vector3( 330f,   9f, 0f),
            new Vector3( 351f,  -5f, 0f),
            new Vector3( 390f,   6f, 0f),
            new Vector3( 502f,  24f, 0f),
            new Vector3( 736f, -89f, 0f),
            new Vector3( 773f, -89f, 0f),
            new Vector3( 820f, -29f, 0f),
            new Vector3( 871f, -28f, 0f),
            new Vector3( 944f, -31f, 0f),
            new Vector3(1021f, -36f, 0f),
        };

        var existing = GameObject.Find("Checkpoints");
        if (existing != null)
        {
            bool replace = EditorUtility.DisplayDialog("Checkpoints Already Exist",
                "A 'Checkpoints' GameObject already exists.\n\nReplace all checkpoints?",
                "Replace", "Cancel");
            if (!replace) return;
            Undo.DestroyObjectImmediate(existing);
        }

        var parent = new GameObject("Checkpoints");
        Undo.RegisterCreatedObjectUndo(parent, "Place Checkpoints");

        for (int i = 0; i < positions.Length; i++)
        {
            var cp = new GameObject($"Checkpoint_{i + 1:D2}");
            Undo.RegisterCreatedObjectUndo(cp, "Create Checkpoint");
            cp.transform.SetParent(parent.transform, false);
            cp.transform.position = positions[i];
            var col = cp.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size      = new Vector2(2f, 3f);
            cp.AddComponent<Checkpoint>();
            EditorUtility.SetDirty(cp);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Checkpoints Placed",
            $"{positions.Length} checkpoint triggers placed under 'Checkpoints' in the Hierarchy.\n\n" +
            "CheckpointManager singleton is also in the scene.\n\n" +
            "Each checkpoint is a 2×3 trigger zone — yellow gizmos in Scene view,\nturning green at runtime when Gareth passes through.\n\n" +
            "Press Ctrl+S to save.", "OK");
    }

    // ── Step 8 ──────────────────────────────────────────────────────────────
    [MenuItem("Gareth Odyssey/8 - Setup Death Zone Tilemap")]
    static void CreateDeathZoneTilemap()
    {
        // Ensure the "DeathZone" tag exists in Project Settings
        if (!InternalEditorUtility.tags.Contains("DeathZone"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset");
            var so    = new SerializedObject(asset);
            var tags  = so.FindProperty("tags");
            int idx   = tags.arraySize;
            tags.InsertArrayElementAtIndex(idx);
            tags.GetArrayElementAtIndex(idx).stringValue = "DeathZone";
            so.ApplyModifiedProperties();
        }

        var grid = Object.FindFirstObjectByType<Grid>();
        if (grid == null) { EditorUtility.DisplayDialog("No Grid", "Open the level scene first.", "OK"); return; }

        // Use the existing tilemap if present, otherwise create one.
        var existing = grid.transform.Find("DeathZoneTileMap");
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject("DeathZoneTileMap");
            Undo.RegisterCreatedObjectUndo(go, "Create DeathZoneTileMap");
            go.transform.SetParent(grid.transform, false);
            go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>();
        }

        // Apply tag
        go.tag = "DeathZone";

        // Ensure a trigger TilemapCollider2D is present
        var tc = go.GetComponent<TilemapCollider2D>();
        if (tc == null) tc = go.AddComponent<TilemapCollider2D>();
        tc.isTrigger = true;

        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Death Zone Ready",
            $"'{go.name}' is now tagged 'DeathZone' with a trigger TilemapCollider2D.\n\n" +
            "Behaviour: touching it costs one life and teleports Gareth\n" +
            "back to the last checkpoint he passed (or the closest one\n" +
            "behind him if he hasn't touched any yet).\n\n" +
            "Press Ctrl+S to save.", "OK");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    static void AssignFrames(PlayerSpriteAnimation anim, Sprite[] s)
    {
        anim.idle    = s.Skip(0).Take(8).ToArray();
        anim.walk    = s.Skip(8).Take(8).ToArray();
        anim.toolUse = s.Skip(16).Take(20).ToArray();
        anim.jump    = s.Skip(36).Take(2).ToArray();
        anim.fall    = s.Skip(38).Take(3).ToArray();
        anim.hurt    = s.Skip(41).Take(5).ToArray();
    }

    static int ParseSpriteIndex(string name)
    {
        int i = name.LastIndexOf('_');
        return (i >= 0 && int.TryParse(name.Substring(i + 1), out int idx)) ? idx : 999;
    }

    static Font GetFont()
    {
        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return f != null ? f : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        var esGO = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
        esGO.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        esGO.AddComponent<InputSystemUIInputModule>();
#else
        esGO.AddComponent<StandaloneInputModule>();
#endif
    }

    // Full-screen semi-transparent overlay
    static GameObject MakeOverlay(Transform canvas, string name, float alpha = 0.88f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(canvas, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero;
        go.AddComponent<Image>().color = new Color(0f, 0f, 0f, alpha);
        return go;
    }

    // Centred box (the card inside an overlay)
    static GameObject MakeBox(Transform parent, string name, float w, float h, Color bg)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        go.AddComponent<Image>().color = bg;
        return go;
    }

    // Text at a fixed anchored position inside a box
    static Text MakeLabel(Transform parent, string name, string txt, Font font,
                          int sz, Color col, TextAnchor align,
                          Vector2 pos, Vector2 size, bool wrap = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var t = go.AddComponent<Text>();
        t.text = txt; t.font = font; t.fontSize = sz; t.color = col;
        t.alignment = align; t.supportRichText = true;
        t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    // Button with a centred text label
    static Button MakeButton(Transform parent, string name, string label, Font font,
                             Color bg, int fontSize, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        go.AddComponent<Image>().color = bg;
        var btn = go.AddComponent<Button>();
        var bc = btn.colors;
        bc.normalColor     = bg;
        bc.highlightedColor = new Color(Mathf.Min(bg.r+0.15f,1f), Mathf.Min(bg.g+0.15f,1f), Mathf.Min(bg.b+0.15f,1f));
        bc.pressedColor    = new Color(bg.r*0.7f, bg.g*0.7f, bg.b*0.7f);
        btn.colors = bc;

        var lbl = new GameObject("Label");
        lbl.transform.SetParent(go.transform, false);
        var lrt = lbl.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.sizeDelta = Vector2.zero;
        var lt = lbl.AddComponent<Text>();
        lt.text = label; lt.font = font; lt.fontSize = fontSize;
        lt.color = Color.white; lt.alignment = TextAnchor.MiddleCenter;
        return btn;
    }
}
