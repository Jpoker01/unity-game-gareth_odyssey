// Assets/Editor/Level2EnemySetup.cs
// Menu: Gareth Odyssey > 7 - Setup Level 2 Enemies
//
// One-shot automation for the Level 2 "Ancient Gortyna" enemies
// (Level2_Enemy_Design_Gortyna.md): slices the supplied sprite sheets into
// individual frames and assembles + saves all seven enemy prefabs, ready to
// be dragged into LevelTwoScene and positioned per the design doc.

using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static class Level2EnemySetup
{
    const string SpriteDir = "Assets/Sprites/Enemies";
    const string PrefabDir = "Assets/Prefabs/Enemies";

    [MenuItem("Gareth Odyssey/7 - Setup Level 2 Enemies")]
    public static void Run()
    {
        EnsureFolder(PrefabDir);

        // EnemyBirds.png uses an opaque magenta chroma-key instead of alpha
        // transparency — strip it before slicing or every bird gets an ugly box.
        StripChromaKey($"{SpriteDir}/EnemyBirds.png", new Color32(255, 0, 255, 255));

        SliceGrid($"{SpriteDir}/BoarIdle.png",        64, 40, "BoarIdle");
        SliceGrid($"{SpriteDir}/BoarWalk.png",        64, 40, "BoarWalk");
        SliceGrid($"{SpriteDir}/BoarRun.png",         64, 40, "BoarRun");
        SliceGrid($"{SpriteDir}/RomanCharacters.png", 32, 32, "Roman");
        SliceGrid($"{SpriteDir}/EnemyBirds.png",      32, 32, "Bird");

        // Snake sheets hold irregular hand-drawn frames, not a uniform grid —
        // slice with explicit per-frame rects measured in image-analysis
        // (top-left origin) coordinates; SliceRectsTopLeft flips them into
        // Unity's bottom-left-origin Rects.
        SliceRectsTopLeft($"{SpriteDir}/SnakeMoveSheet.png", "SnakeMove", new[]
        {
            new RectInt(7,  3,  25, 23),
            new RectInt(48, 3,  23, 22),
            new RectInt(10, 32, 20, 22),
            new RectInt(48, 32, 23, 22),
            new RectInt(7,  62, 26, 22),
            new RectInt(45, 61, 29, 23),
            new RectInt(4,  90, 32, 23),
        });

        SliceRectsTopLeft($"{SpriteDir}/SnakeDieSheet.png", "SnakeDie", new[]
        {
            new RectInt(7,  3,  25, 23),
            new RectInt(48, 5,  23, 20),
            new RectInt(7,  37, 25, 19),
            new RectInt(47, 42, 29, 14),
            new RectInt(7,  74, 28, 11),
        });

        AssetDatabase.Refresh();

        Sprite[] boarIdle  = LoadSlicedSprites($"{SpriteDir}/BoarIdle.png");
        Sprite[] boarWalk  = LoadSlicedSprites($"{SpriteDir}/BoarWalk.png");
        Sprite[] boarRun   = LoadSlicedSprites($"{SpriteDir}/BoarRun.png");
        Sprite[] roman     = LoadSlicedSprites($"{SpriteDir}/RomanCharacters.png");
        Sprite[] birds     = LoadSlicedSprites($"{SpriteDir}/EnemyBirds.png");
        Sprite[] snakeMove = LoadSlicedSprites($"{SpriteDir}/SnakeMoveSheet.png");
        Sprite[] snakeDie  = LoadSlicedSprites($"{SpriteDir}/SnakeDieSheet.png");

        if (boarIdle.Length < 8 || boarWalk.Length < 8 || boarRun.Length < 6 ||
            roman.Length < 12 || birds.Length < 12 ||
            snakeMove.Length < 7 || snakeDie.Length < 5)
        {
            Debug.LogError($"Level2EnemySetup: a sheet sliced into fewer frames than expected — aborting prefab assembly. Check the sheets in {SpriteDir} and the console above for import warnings.");
            return;
        }

        GameObject arrowPrefab = BuildArrowPrefab();

        BuildRomanSentryPrefab(roman);
        BuildRomanArcherPrefab(roman, arrowPrefab.GetComponent<Arrow>());
        BuildWildBoarPrefab(boarIdle, boarWalk, boarRun);
        BuildCretanViperPrefab(snakeMove, snakeDie);
        BuildWarEaglePrefab(SubArray(birds, 6, 5));
        BuildEagleTriggerZonePrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Level2EnemySetup: enemy prefabs ready in {PrefabDir} — drag them into LevelTwoScene and position per the design doc.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Sprite import & slicing
    // ═══════════════════════════════════════════════════════════════════════

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    /// <summary>Replaces every exact-match pixel of `keyColor` with full transparency, in place on disk.</summary>
    static void StripChromaKey(string assetPath, Color32 keyColor)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(assetPath)); // works without importer.isReadable

        Color32[] px = tex.GetPixels32();
        bool changed = false;
        for (int i = 0; i < px.Length; i++)
        {
            if (px[i].r == keyColor.r && px[i].g == keyColor.g && px[i].b == keyColor.b && px[i].a == keyColor.a)
            {
                px[i] = new Color32(0, 0, 0, 0);
                changed = true;
            }
        }

        if (changed)
        {
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
        }
        Object.DestroyImmediate(tex);

        if (changed) AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }

    /// <summary>Slices a uniform cell grid in reading order — top-left = index 0, then left→right, top→bottom.</summary>
    static void SliceGrid(string assetPath, int cellWidth, int cellHeight, string baseName)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) { Debug.LogWarning($"Level2EnemySetup: no TextureImporter at {assetPath}"); return; }

        importer.GetSourceTextureWidthAndHeight(out int texW, out int texH);
        int cols = Mathf.Max(1, texW / cellWidth);
        int rows = Mathf.Max(1, texH / cellHeight);

        var metas = new SpriteMetaData[cols * rows];
        int idx = 0;
        for (int r = rows - 1; r >= 0; r--)       // top grid row first …
            for (int c = 0; c < cols; c++)         // … left to right
            {
                metas[idx] = new SpriteMetaData
                {
                    name      = $"{baseName}_{idx}",
                    rect      = new Rect(c * cellWidth, r * cellHeight, cellWidth, cellHeight),
                    pivot     = new Vector2(0.5f, 0.5f),
                    alignment = (int)SpriteAlignment.Center
                };
                idx++;
            }

        ApplySpriteImport(importer, metas);
    }

    /// <summary>
    /// Slices explicit per-frame rects supplied in image-analysis (top-left
    /// origin, Y grows downward) coordinates, converting each to Unity's
    /// bottom-left-origin Rect via unityY = textureHeight - topLeftY - height.
    /// </summary>
    static void SliceRectsTopLeft(string assetPath, string baseName, RectInt[] topLeftRects)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) { Debug.LogWarning($"Level2EnemySetup: no TextureImporter at {assetPath}"); return; }

        importer.GetSourceTextureWidthAndHeight(out int texW, out int texH);

        var metas = new SpriteMetaData[topLeftRects.Length];
        for (int i = 0; i < topLeftRects.Length; i++)
        {
            RectInt r = topLeftRects[i];
            float unityY = texH - r.y - r.height;
            metas[i] = new SpriteMetaData
            {
                name      = $"{baseName}_{i}",
                rect      = new Rect(r.x, unityY, r.width, r.height),
                pivot     = new Vector2(0.5f, 0.5f),
                alignment = (int)SpriteAlignment.Center
            };
        }

        ApplySpriteImport(importer, metas);
    }

    static void ApplySpriteImport(TextureImporter importer, SpriteMetaData[] metas)
    {
        importer.textureType         = TextureImporterType.Sprite;
        importer.spriteImportMode    = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 32f;
        importer.filterMode          = FilterMode.Point;
        importer.textureCompression  = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled       = false;
        importer.spritesheet         = metas;
        importer.SaveAndReimport();
    }

    static Sprite[] LoadSlicedSprites(string assetPath)
    {
        return AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath)
            .OfType<Sprite>()
            .OrderBy(s => TrailingIndex(s.name))
            .ToArray();
    }

    static int TrailingIndex(string name)
    {
        int i = name.LastIndexOf('_');
        return (i >= 0 && int.TryParse(name.Substring(i + 1), out int idx)) ? idx : 0;
    }

    static Sprite[] SubArray(Sprite[] src, int start, int count) => src.Skip(start).Take(count).ToArray();

    // ═══════════════════════════════════════════════════════════════════════
    // Prefab assembly
    // ═══════════════════════════════════════════════════════════════════════

    static GameObject SaveAsPrefab(GameObject go, string path)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    static Transform MakeMarker(Transform parent, string name, Vector3 localPosition)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        return go.transform;
    }

    // ── Enemy 2 support — Arrow projectile ──────────────────────────────────
    static GameObject BuildArrowPrefab()
    {
        var go = new GameObject("Arrow");
        go.AddComponent<Arrow>(); // RequireComponent chain adds Rigidbody2D / BoxCollider2D / SpriteRenderer; Awake sizes the trigger to the sprite
        go.GetComponent<Rigidbody2D>().collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        return SaveAsPrefab(go, $"{PrefabDir}/Arrow.prefab");
    }

    // ── Enemy 1 — Legionarius / Roman Sentry ────────────────────────────────
    static void BuildRomanSentryPrefab(Sprite[] roman)
    {
        var go = new GameObject("RomanSentry");
        var sentry = go.AddComponent<RomanSentry>();
        go.GetComponent<SpriteRenderer>().sprite = roman[0];

        // Roster sheet = static character poses, not a walk-cycle: idx 0/1 are
        // near-identical legionary stances (a subtle marching shimmer), idx 5
        // is a visually distinct ornate-armoured pose for the alert telegraph.
        sentry.walkFrames      = new[] { roman[0], roman[1] };
        sentry.idleRestFrames  = new[] { roman[0] };
        sentry.idleAlertFrames = new[] { roman[5] };

        SaveAsPrefab(go, $"{PrefabDir}/RomanSentry.prefab");
    }

    // ── Enemy 2 — Sagittarius / Roman Archer ────────────────────────────────
    static void BuildRomanArcherPrefab(Sprite[] roman, Arrow arrowPrefab)
    {
        var go = new GameObject("RomanArcher");
        var archer = go.AddComponent<RomanArcher>();
        go.GetComponent<SpriteRenderer>().sprite = roman[2];

        // The roster has exactly one archer pose we can reuse without an
        // identity-swap (idx 8 is a visibly different archer); the Idle → Draw
        // → Release telegraph reads through TIMING rather than a frame change —
        // a real limitation of the supplied art, not a bug in the state machine.
        archer.idleFrames    = new[] { roman[2] };
        archer.drawFrames    = new[] { roman[2] };
        archer.releaseFrames = new[] { roman[2] };
        archer.arrowPrefab   = arrowPrefab;

        SaveAsPrefab(go, $"{PrefabDir}/RomanArcher.prefab");
    }

    // ── Enemy 3 — Agrios Xoiros / Cretan Wild Boar ───────────────────────────
    static void BuildWildBoarPrefab(Sprite[] idle, Sprite[] walk, Sprite[] run)
    {
        var go = new GameObject("WildBoar");
        var boar = go.AddComponent<WildBoar>();
        go.GetComponent<SpriteRenderer>().sprite = idle[0];

        // 1:1 thematic mapping straight from the three supplied sheets —
        // wandering = walking, frozen telegraph = standing, charging = running.
        boar.grazeFrames   = walk;
        boar.alertFrames   = idle;
        boar.chargeFrames  = run;
        boar.recoverFrames = idle;

        SaveAsPrefab(go, $"{PrefabDir}/WildBoar.prefab");
    }

    // ── Enemy 4 — Macrovipera / Cretan Viper ─────────────────────────────────
    static void BuildCretanViperPrefab(Sprite[] move, Sprite[] die)
    {
        var go = new GameObject("CretanViper");
        var viper = go.AddComponent<CretanViper>();
        go.GetComponent<SpriteRenderer>().sprite = move[0];

        // Neither snake sheet has a dedicated rear/strike/recoil clip, so the
        // 5-frame death progression (alert → collapsed) is reused in reverse as
        // a "rising to alert" telegraph: collapsed → bright-eyed (REAR), hold
        // the peak pose (STRIKE), settle back down (RECOIL). Frame continuity
        // is exact across both the REAR/STRIKE and STRIKE/RECOIL seams.
        viper.slitherFrames = move;
        viper.rearFrames    = new[] { die[4], die[3], die[2], die[1], die[0] };
        viper.strikeFrames  = new[] { die[0] };
        viper.recoilFrames  = new[] { die[1], die[2], die[3] };
        viper.rearFps       = 12f;
        viper.recoilFps     = 6f;

        SaveAsPrefab(go, $"{PrefabDir}/CretanViper.prefab");
    }

    // ── Enemy 5 — Aquila / Roman War Eagle (optional) ────────────────────────
    static void BuildWarEaglePrefab(Sprite[] eagleFrames)
    {
        // Wrapper root holds the eagle and its three path markers as SIBLINGS —
        // never as children of the eagle itself, or moving the eagle would drag
        // its own path references along (a feedback loop caught during design).
        var root = new GameObject("WarEagleEncounter");

        var eagleGO = new GameObject("Eagle");
        eagleGO.transform.SetParent(root.transform, false);
        var eagle = eagleGO.AddComponent<WarEagle>();
        eagleGO.GetComponent<SpriteRenderer>().sprite = eagleFrames[0];
        eagle.flyFrames = eagleFrames;

        // Span derived from the scene camera's orthographic size (5 → roughly
        // ±9 units of visible half-width at 16:9): entry/exit sit just past
        // either edge, the peak pulls the arc into a gentle dip-then-rise.
        eagle.pathEntry = MakeMarker(root.transform, "PathEntry", new Vector3( 9f,  2.5f, 0f));
        eagle.pathPeak  = MakeMarker(root.transform, "PathPeak",  new Vector3( 0f, -1.5f, 0f));
        eagle.pathExit  = MakeMarker(root.transform, "PathExit",  new Vector3(-9f,  2.5f, 0f));

        SaveAsPrefab(root, $"{PrefabDir}/WarEagleEncounter.prefab");
    }

    static void BuildEagleTriggerZonePrefab()
    {
        var go = new GameObject("EagleTriggerZone");
        go.AddComponent<EagleTriggerZone>();
        go.GetComponent<BoxCollider2D>().size = new Vector2(0.5f, 3f);

        SaveAsPrefab(go, $"{PrefabDir}/EagleTriggerZone.prefab");
    }
}
