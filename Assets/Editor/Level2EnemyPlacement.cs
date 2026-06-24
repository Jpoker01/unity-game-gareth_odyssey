using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Loads LevelTwoScene, surveys SolidTilemap for its real ground profile (so every
/// spawn lands on actual solid ground — never floating or buried), and places the
/// Level 2 roster built by Level2EnemySetup at positions/widths that follow the
/// per-enemy "Level placement" guidance in Level2_Enemy_Design_Gortyna.md: an
/// early olive-grove band (wide, easy encounters), a mid band of ruined courtyards
/// and corridors (vipers, a narrow-platform sentry, archers on real elevated
/// platforms), and a late marble-interior band (an overlapping sentry pair, the
/// rare eagle swoop, the final corridor viper). Zone fractions below are read off
/// the doc's "first third / mid-level / late / near the end" language mapped onto
/// the tilemap's actual surveyed X-span (Player spawn ≈ x=-4.9, VictoryArea ≈ x=870).
/// </summary>
public static class Level2EnemyPlacement
{
    const string ScenePath = "Assets/Scenes/LevelTwoScene.unity";
    const string PrefabDir = "Assets/Prefabs/Enemies";

    class Segment
    {
        public int xStart, xEnd;
        public float topY;
        public float riseFromPrev; // topY minus the previous (more-leftward) segment's topY — how far Gareth must climb to reach this one from the approach side
        public int Length => xEnd - xStart + 1;
        public float CenterX => (xStart + xEnd) * 0.5f;
    }

    static List<Segment> segs;
    static float mainY, minX, maxX, spanLen;
    static HashSet<Segment> used;
    static StringBuilder report;
    static GameObject root;
    static Scene scene;

    [MenuItem("Gareth Odyssey/8 - Place Level 2 Enemies")]
    public static void Run()
    {
        scene  = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        report = new StringBuilder();
        report.AppendLine("===== Level 2 Enemy Placement =====");

        if (!Survey()) return;

        var stale = GameObject.Find("Level 2 Enemies");
        if (stale != null) Object.DestroyImmediate(stale); // re-running replaces rather than duplicates

        used = new HashSet<Segment>();
        root = new GameObject("Level 2 Enemies");
        SceneManager.MoveGameObjectToScene(root, scene);

        report.AppendLine("\n-- Olive Grove exterior (first third — wide, easy to spot) --");
        PlaceSentry(0.02f, 0.16f, "Olive Grove A", wide: true);
        PlaceSentry(0.10f, 0.22f, "Olive Grove B", wide: true);
        PlaceBoar  (0.04f, 0.16f, "Olive Grove A", wide: true);
        PlaceBoar  (0.14f, 0.26f, "Olive Grove B", wide: true);

        report.AppendLine("\n-- Transition: narrow ruined lane / first courtyard / gate arch --");
        PlaceBoar  (0.26f, 0.36f, "Narrow Ruined Lane",       wide: false);
        PlaceSentry(0.30f, 0.40f, "First Ruined Courtyard",   wide: false);
        PlaceArcher(0.34f, 0.46f, "Gate Arch over Ruined Road");

        report.AppendLine("\n-- Overgrown ruined courtyard (mid-level — vipers in the undergrowth) --");
        PlaceViperPair(0.30f, 0.40f, "Overgrown Ruined Courtyard");

        report.AppendLine("\n-- Late mid-level: ruined upper floor archer + rare eagle rooftop swoop --");
        PlaceArcher(0.56f, 0.68f, "Ruined Upper Floor over Switch");
        PlaceEagle (0.64f, 0.78f, "Open Rooftop Crossing");

        report.AppendLine("\n-- Deep marble interior (late — overlapping gauntlet pair + final viper) --");
        PlaceSentryPair(0.80f, 0.95f, "Deep Marble Interior");
        PlaceViper(0.88f, 0.98f, "Narrow Corridor near Level End");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        report.AppendLine("\n===== Saved LevelTwoScene — 14 enemies + 1 trigger zone under 'Level 2 Enemies' =====");
        report.AppendLine("(5 RomanSentry, 2 RomanArcher, 3 WildBoar, 3 CretanViper, 1 WarEagleEncounter, 1 EagleTriggerZone)");
        Debug.Log(report.ToString());
    }

    // ── Survey: read SolidTilemap's real occupancy into per-column ground heights,
    //    then collapse those into contiguous "platform" segments (≈constant height runs) ──
    static bool Survey()
    {
        var tilemapGO = GameObject.Find("SolidTilemap");
        if (tilemapGO == null) { Debug.LogError("Level2EnemyPlacement: 'SolidTilemap' not found in LevelTwoScene."); return false; }

        var tilemap = tilemapGO.GetComponent<Tilemap>();
        var grid    = tilemapGO.GetComponentInParent<Grid>();
        if (tilemap == null || grid == null) { Debug.LogError("Level2EnemyPlacement: SolidTilemap is missing its Tilemap/Grid."); return false; }

        tilemap.CompressBounds();
        BoundsInt cb = tilemap.cellBounds;

        var topY = new SortedDictionary<int, float>();
        for (int x = cb.xMin; x < cb.xMax; x++)
            for (int y = cb.yMax - 1; y >= cb.yMin; y--)
                if (tilemap.GetTile(new Vector3Int(x, y, 0)) != null)
                {
                    topY[x] = grid.CellToWorld(new Vector3Int(x, y + 1, 0)).y; // top edge of the topmost solid cell
                    break;
                }

        if (topY.Count == 0) { Debug.LogError("Level2EnemyPlacement: SolidTilemap has no painted tiles to survey."); return false; }

        minX    = topY.Keys.Min();
        maxX    = topY.Keys.Max();
        spanLen = maxX - minX;

        segs = new List<Segment>();
        Segment cur = null;
        int prevX = int.MinValue; float prevY = 0f;
        foreach (var kv in topY)
        {
            bool extend = cur != null && kv.Key == prevX + 1 && Mathf.Abs(kv.Value - prevY) < 0.51f;
            if (extend) cur.xEnd = kv.Key;
            else
            {
                float fromY = cur?.topY ?? kv.Value;
                cur = new Segment { xStart = kv.Key, xEnd = kv.Key, topY = kv.Value, riseFromPrev = kv.Value - fromY };
                segs.Add(cur);
            }
            prevX = kv.Key; prevY = kv.Value;
        }

        mainY = segs.GroupBy(s => Mathf.Round(s.topY * 2f) / 2f)
                    .OrderByDescending(g => g.Sum(s => s.Length))
                    .First().Key;

        report.AppendLine($"Surveyed SolidTilemap: x=[{minX:0}..{maxX:0}] ({topY.Count}/{(int)spanLen + 1} columns occupied), " +
                          $"{segs.Count} ground segments, main walking level y≈{mainY:0.##}");
        return true;
    }

    static float ZoneX(float frac01) => minX + spanLen * frac01;

    static string SegInfo(Segment s) =>
        s == null ? "(no segment matched zone — used zone-centre fallback)"
                  : $"[platform x={s.xStart}..{s.xEnd}, {s.Length} tiles wide, y={s.topY:0.0}" +
                    (Mathf.Abs(s.topY - mainY) > 1f ? $", {(s.topY - mainY):+0.0;-0.0} vs main" : "") +
                    (s.riseFromPrev > 1f ? $", steps up {s.riseFromPrev:0.0} from the approach" : "") + "]";

    /// <summary>Best unused ground segment whose centre falls in [from01,to01] of the level span,
    /// long enough for minLen, and at the requested relative height band; degrades gracefully.</summary>
    static Segment Pick(float from01, float to01, float minLen, bool elevated)
    {
        float xa = ZoneX(from01), xb = ZoneX(to01);

        // This terrain is a continuous hillside that climbs from y≈0 to y≈55 and back across
        // the level — there is no single "main height" an elevated platform sits above. A
        // ground encounter (sentry/boar/viper patrol/territory) needs to stay near ITS LOCAL
        // walking surface, which the global mainY only approximates near the start/end baseline
        // bands these zones happen to target. A "ledge / upper floor / gate arch", by contrast,
        // is best read simply as the highest standable point within its own encounter zone —
        // that's what makes Gareth look up at it regardless of the absolute world height.
        System.Func<Segment, bool>           plausible = elevated ? (s => true) : (s => Mathf.Abs(s.topY - mainY) < 1.6f);
        System.Func<IEnumerable<Segment>, Segment> rank = elevated
            ? (cands => cands.OrderByDescending(s => s.topY).ThenByDescending(s => s.Length).FirstOrDefault())
            : (cands => cands.OrderByDescending(s => s.Length).FirstOrDefault());

        var pick = rank(segs.Where(s => !used.Contains(s) && plausible(s) && s.Length >= minLen && s.CenterX >= xa && s.CenterX <= xb))
            ?? segs.Where(s => !used.Contains(s) && plausible(s) && s.Length >= Mathf.Max(2f, minLen - 1f))
                   .OrderBy(s => Mathf.Abs(s.CenterX - (xa + xb) * 0.5f)).FirstOrDefault()
            ?? segs.Where(s => !used.Contains(s))
                   .OrderBy(s => Mathf.Abs(s.CenterX - (xa + xb) * 0.5f)).FirstOrDefault();

        if (pick != null) used.Add(pick);
        return pick;
    }

    static GameObject Spawn(string prefabName, Segment seg, string label)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{prefabName}.prefab");
        if (prefab == null) { Debug.LogError($"Level2EnemyPlacement: prefab '{prefabName}' missing from {PrefabDir} — run 'Gareth Odyssey/7 - Setup Level 2 Enemies' first."); return null; }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        go.name = $"{prefabName} ({label})";
        go.transform.SetParent(root.transform, true);

        float x         = seg?.CenterX ?? ZoneX(0.5f);
        float groundTop = seg?.topY    ?? mainY;
        var   sr        = go.GetComponentInChildren<SpriteRenderer>();
        float halfH     = (sr != null && sr.sprite != null) ? sr.sprite.bounds.extents.y : 0.5f;
        go.transform.position = new Vector3(x, groundTop + halfH, 0f);
        return go;
    }

    static void PlaceSentry(float from01, float to01, string label, bool wide)
    {
        var seg = Pick(from01, to01, wide ? 6f : 3f, elevated: false);
        var go  = Spawn("RomanSentry", seg, label);
        if (go == null) return;

        var s = go.GetComponent<RomanSentry>();
        if (seg != null) s.patrolWidth = Mathf.Clamp(seg.Length - 1f, 3f, wide ? 8f : 5f);
        report.AppendLine($"  Sentry  [{label,-30}] x={go.transform.position.x,7:0.0}  patrolWidth={s.patrolWidth:0.0}  {SegInfo(seg)}");
    }

    static void PlaceBoar(float from01, float to01, string label, bool wide)
    {
        var seg = Pick(from01, to01, wide ? 6f : 3f, elevated: false);
        var go  = Spawn("WildBoar", seg, label);
        if (go == null) return;

        var b = go.GetComponent<WildBoar>();
        if (seg != null) b.territoryWidth = Mathf.Clamp(seg.Length - 1.5f, 3f, wide ? 7f : 4.5f);
        report.AppendLine($"  Boar    [{label,-30}] x={go.transform.position.x,7:0.0}  territoryWidth={b.territoryWidth:0.0}  {SegInfo(seg)}");
    }

    static void PlaceViper(float from01, float to01, string label)
    {
        var seg = Pick(from01, to01, 2f, elevated: false);
        var go  = Spawn("CretanViper", seg, label);
        if (go == null) return;

        var v = go.GetComponent<CretanViper>();
        if (seg != null) v.patrolWidth = Mathf.Clamp(seg.Length - 0.5f, 1.5f, 3f);
        report.AppendLine($"  Viper   [{label,-30}] x={go.transform.position.x,7:0.0}  patrolWidth={v.patrolWidth:0.0}  {SegInfo(seg)}");
    }

    /// <summary>Doc: "Overgrown ruined courtyard (mid-level): 2 vipers... The first one...
    /// should be in an open enough area that the rearing animation is clearly visible." Both
    /// belong to ONE shared courtyard, so — unlike the spread-out solo placements — this picks
    /// a single open anchor (the longest near-baseline ground in the zone, favouring visibility
    /// of the rear-up telegraph) and then seats the second viper on the nearest other unused
    /// near-baseline platform, wherever the real terrain offers one, so they read as occupying
    /// the same broken-ground area rather than two unrelated encounters.</summary>
    static void PlaceViperPair(float from01, float to01, string label)
    {
        float xa = ZoneX(from01), xb = ZoneX(to01);
        System.Func<Segment, bool> nearBaseline = s => Mathf.Abs(s.topY - mainY) < 1.6f;

        var anchor = segs.Where(s => !used.Contains(s) && nearBaseline(s) && s.CenterX >= xa && s.CenterX <= xb)
                         .OrderByDescending(s => s.Length).FirstOrDefault()
                  ?? segs.Where(s => !used.Contains(s) && nearBaseline(s))
                         .OrderBy(s => Mathf.Abs(s.CenterX - (xa + xb) * 0.5f)).FirstOrDefault();
        if (anchor == null) { report.AppendLine($"  Viper pair [{label}]: no open near-baseline ground available — skipped."); return; }
        used.Add(anchor);

        var partner = segs.Where(s => !used.Contains(s) && nearBaseline(s))
                          .OrderBy(s => Mathf.Abs(s.CenterX - anchor.CenterX)).FirstOrDefault();
        if (partner != null) used.Add(partner);

        var goA = Spawn("CretanViper", anchor, label + " A (open — rearing telegraph reads clearly)");
        var vA  = goA.GetComponent<CretanViper>();
        vA.patrolWidth = Mathf.Clamp(anchor.Length - 0.5f, 1.5f, 3f);
        report.AppendLine($"  Viper   [{label} A] x={goA.transform.position.x,7:0.0}  patrolWidth={vA.patrolWidth:0.0}  {SegInfo(anchor)}");

        if (partner == null) { report.AppendLine($"          (no second near-baseline platform left for the partner viper — pair placed solo)"); return; }

        var goB = Spawn("CretanViper", partner, label + " B");
        var vB  = goB.GetComponent<CretanViper>();
        vB.patrolWidth = Mathf.Clamp(partner.Length - 0.5f, 1.5f, 3f);
        report.AppendLine($"  Viper   [{label} B] x={goB.transform.position.x,7:0.0}  patrolWidth={vB.patrolWidth:0.0}  " +
                          $"({Mathf.Abs(goB.transform.position.x - goA.transform.position.x):0.0} tiles from its partner)  {SegInfo(partner)}");
    }

    static void PlaceArcher(float from01, float to01, string label)
    {
        var seg = Pick(from01, to01, 3f, elevated: true);
        var go  = Spawn("RomanArcher", seg, label);
        if (go == null) return;

        report.AppendLine($"  Archer  [{label,-30}] x={go.transform.position.x,7:0.0}  faces player  {SegInfo(seg)}");
    }

    /// <summary>
    /// Wrapper Y is solved from the doc + the prefab's baked path-marker offsets so the
    /// whole encounter is self-consistent: pathEntry sits +2.5 above the wrapper (doc wants
    /// the spawn ~5-6 tiles over the ground, so wrapper = ground+3 -> entry = ground+5.5),
    /// and the shadow offset is then derived so it lands exactly on the ground at pathPeak
    /// (the lowest point of the dive, local y -1.5) rather than using its generic default.
    /// </summary>
    static void PlaceEagle(float from01, float to01, string label)
    {
        var seg     = Pick(from01, to01, 8f, elevated: false);
        float groundY = seg?.topY ?? mainY;

        var encGo = Spawn("WarEagleEncounter", seg, label);
        if (encGo == null) return;

        float wrapperY = groundY + 3f; // -> pathEntry/pathExit ≈ ground+5.5 (doc: "5-6 tiles above the ground")
        encGo.transform.position = new Vector3(encGo.transform.position.x, wrapperY, 0f);

        var eagle = encGo.GetComponentInChildren<WarEagle>();
        if (eagle != null)
        {
            float peakLocalY = eagle.pathPeak != null ? eagle.pathPeak.localPosition.y : -1.5f;
            eagle.shadowGroundOffset = Mathf.Max(0.5f, (wrapperY + peakLocalY) - groundY);
        }

        // Trigger sits a few tiles before the wrapper so the swoop is mid-arc as the player arrives.
        var zone = Spawn("EagleTriggerZone", null, label + " Trigger");
        GameObject zoneGo = zone;
        if (zoneGo != null)
        {
            zoneGo.transform.position = new Vector3(encGo.transform.position.x - 7f, groundY + 1.5f, 0f);
            var ez = zoneGo.GetComponent<EagleTriggerZone>();
            ez.eagle = eagle;
        }

        report.AppendLine($"  Eagle   [{label,-30}] encounter x={encGo.transform.position.x,7:0.0} y={wrapperY:0.0}" +
                          (eagle != null ? $"  shadowOffset={eagle.shadowGroundOffset:0.0} (lands on ground at the dive's lowest point)" : "  *** Eagle child not found — eagle left unwired ***"));
        report.AppendLine($"          trigger zone x={(zoneGo != null ? zoneGo.transform.position.x : 0f),7:0.0}  (fires ~7 tiles before the swoop centre)  {SegInfo(seg)}");
    }

    /// <summary>Doc: "2 sentries on adjacent platforms whose patrol paths overlap briefly,
    /// creating a timed gap." Picks two neighbouring ground segments and gives each a patrol
    /// width that uses nearly its whole platform — narrow enough to stay on solid ground,
    /// wide enough that the two ranges read as a connected gauntlet across the gap between them.</summary>
    static void PlaceSentryPair(float from01, float to01, string label)
    {
        float xa = ZoneX(from01), xb = ZoneX(to01);
        var zoneSegs = segs.Where(s => !used.Contains(s) && Mathf.Abs(s.topY - mainY) < 3f
                                     && s.CenterX >= xa && s.CenterX <= xb && s.Length >= 3f)
                           .OrderBy(s => s.xStart).ToList();

        Segment a = null, b = null;
        for (int i = 0; i + 1 < zoneSegs.Count; i++)
            if (zoneSegs[i + 1].xStart - zoneSegs[i].xEnd <= 10) { a = zoneSegs[i]; b = zoneSegs[i + 1]; break; }
        if (a == null && zoneSegs.Count >= 2) { a = zoneSegs[zoneSegs.Count - 2]; b = zoneSegs[zoneSegs.Count - 1]; }
        if (a == null)
        {
            var any = segs.Where(s => !used.Contains(s) && Mathf.Abs(s.topY - mainY) < 3f && s.Length >= 3f)
                          .OrderBy(s => Mathf.Abs(s.CenterX - (xa + xb) * 0.5f)).Take(2).OrderBy(s => s.xStart).ToList();
            if (any.Count >= 2) { a = any[0]; b = any[1]; }
        }
        if (a == null) { report.AppendLine($"  Sentry pair [{label}]: fewer than two distinct ground platforms available — skipped."); return; }
        used.Add(a); used.Add(b);

        var goA = Spawn("RomanSentry", a, label + " — nearer platform");
        var goB = Spawn("RomanSentry", b, label + " — farther platform");
        if (goA == null || goB == null) return;

        var sa = goA.GetComponent<RomanSentry>();
        var sb = goB.GetComponent<RomanSentry>();
        sa.patrolWidth = Mathf.Clamp(a.Length - 0.5f, 3f, 8f);
        sb.patrolWidth = Mathf.Clamp(b.Length - 0.5f, 3f, 8f);

        float aRight = goA.transform.position.x + sa.patrolWidth * 0.5f;
        float bLeft  = goB.transform.position.x - sb.patrolWidth * 0.5f;
        report.AppendLine($"  Sentry  [{label} A] x={goA.transform.position.x,7:0.0}  patrolWidth={sa.patrolWidth:0.0} (range right edge {aRight:0.0})  {SegInfo(a)}");
        report.AppendLine($"  Sentry  [{label} B] x={goB.transform.position.x,7:0.0}  patrolWidth={sb.patrolWidth:0.0} (range left edge {bLeft:0.0})  {SegInfo(b)}");
        report.AppendLine($"          gap between platforms ≈ {(b.xStart - a.xEnd):0.0} tiles — patrol-edge gap ≈ {(bLeft - aRight):0.0} tiles for the player to slip through");
    }
}
