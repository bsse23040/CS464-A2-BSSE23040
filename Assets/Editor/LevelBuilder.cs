using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the greybox prefab kit and the five blockout levels.
// Menu: CS464 > Build Kit + Levels. Batch: -executeMethod LevelBuilder.BuildAll
// Units are metres. Player capsule 2 x 1, doorways 2 x 2.5, walls 3 tall, corridors ~3 wide,
// low cover 1.1, steps 0.3, slopes < 20 deg, jump gaps <= 2.8.
public static class LevelBuilder
{
    const string PrefabDir = "Assets/Prefabs";
    const string MatDir = "Assets/Materials";
    static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
    static readonly float[] None = new float[0];
    static Transform root, group;

    // ---------------------------------------------------------------- kit
    static Material Mat(string name, Color c, float emission = 0f)
    {
        string p = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, p); }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0f);
        m.SetFloat("_Metallic", 0f);
        if (emission > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }

    static GameObject Prim(PrimitiveType t, string name, Vector3 pos, Vector3 scale, Material m, Transform parent = null, bool collider = true)
    {
        var g = GameObject.CreatePrimitive(t);
        g.name = name;
        if (parent != null) g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    const int GroundLayer = 6;   // the "Ground" layer in ProjectSettings/TagManager.asset
    static void SetLayerAll(GameObject g, int layer) { g.layer = layer; foreach (Transform t in g.transform) SetLayerAll(t.gameObject, layer); }

    // everything the player can stand on goes on the Ground layer, so the player's ground check can ignore itself
    static void Save(GameObject g, string name, bool ground = true)
    {
        if (ground) SetLayerAll(g, GroundLayer);
        prefabs[name] = PrefabUtility.SaveAsPrefabAsset(g, PrefabDir + "/" + name + ".prefab");
        Object.DestroyImmediate(g);
    }

    // zero friction with Friction Combine = Minimum, so the player does not stick to walls in mid-air
    static PhysicsMaterial NoFriction()
    {
        const string path = "Assets/Materials/PlayerNoFriction.asset";
        var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (m == null) { m = new PhysicsMaterial("PlayerNoFriction"); AssetDatabase.CreateAsset(m, path); }
        m.dynamicFriction = 0f; m.staticFriction = 0f; m.bounciness = 0f;
        m.frictionCombine = PhysicsMaterialCombine.Minimum;
        EditorUtility.SetDirty(m);
        return m;
    }

    // The Player: an empty root with a Rigidbody, a capsule collider (height 2, radius 0.5) and PlayerController.
    // The mesh is a child, Body, with no collider. The pivot is at the player's feet.
    static void BuildPlayer(Material mat)
    {
        var root = new GameObject("Player");
        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        var col = root.AddComponent<CapsuleCollider>();
        col.height = 2f; col.radius = 0.5f; col.center = new Vector3(0, 1, 0);
        col.sharedMaterial = NoFriction();

        Prim(PrimitiveType.Capsule, "Body", new Vector3(0, 1, 0), Vector3.one, mat, root.transform, false);

        var feet = new GameObject("Feet");
        feet.transform.SetParent(root.transform, false);
        feet.transform.localPosition = new Vector3(0, 0.35f, 0);     // a 0.4 m sphere here reaches 5 cm below the feet

        var pc = root.AddComponent<PlayerController>();
        var so = new SerializedObject(pc);
        so.FindProperty("feet").objectReferenceValue = feet.transform;
        so.FindProperty("groundMask").intValue = 1 << GroundLayer;
        so.ApplyModifiedPropertiesWithoutUndo();

        Save(root, "Player", false);
    }

    static void BuildKit()
    {
        EnsureFolder("Assets", "Prefabs"); EnsureFolder("Assets", "Materials"); EnsureFolder("Assets", "Scenes");
        var floor = Mat("Floor", new Color(.55f, .57f, .60f));
        var dark = Mat("FloorDark", new Color(.20f, .21f, .24f));
        var wall = Mat("Wall", new Color(.82f, .82f, .85f));
        var cover = Mat("Cover", new Color(.95f, .60f, .20f));
        var lmk = Mat("Landmark", new Color(1f, .42f, .08f), .5f);
        var goal = Mat("Goal", new Color(.15f, .85f, .3f), .8f);
        var spawn = Mat("Spawn", new Color(.2f, .45f, .95f));
        var coin = Mat("Coin", new Color(1f, .85f, .1f), .6f);
        var strip = Mat("Strip", new Color(.97f, .97f, .75f), .4f);
        var player = Mat("Player", new Color(.9f, .2f, .2f));

        Save(Prim(PrimitiveType.Cube, "Floor", Vector3.zero, new Vector3(4, .4f, 4), floor), "Floor");
        Save(Prim(PrimitiveType.Cube, "FloorDark", Vector3.zero, new Vector3(4, .4f, 4), dark), "FloorDark");
        Save(Prim(PrimitiveType.Cube, "Wall", Vector3.zero, new Vector3(4, 3, .4f), wall), "Wall");
        Save(Prim(PrimitiveType.Cube, "LowCover", Vector3.zero, new Vector3(2, 1.1f, .5f), cover), "LowCover");
        Save(Prim(PrimitiveType.Cube, "Crate", Vector3.zero, new Vector3(1.2f, 1.2f, 1.2f), cover), "Crate");
        Save(Prim(PrimitiveType.Cube, "Pillar", Vector3.zero, new Vector3(1, 3, 1), wall), "Pillar");
        Save(Prim(PrimitiveType.Cube, "Step", Vector3.zero, new Vector3(3, .3f, .6f), floor), "Step");
        Save(Prim(PrimitiveType.Cube, "Ramp", Vector3.zero, new Vector3(4, .3f, 7), floor), "Ramp");
        Save(Prim(PrimitiveType.Cube, "Landmark", Vector3.zero, new Vector3(2, 14, 2), lmk), "Landmark");
        Save(Prim(PrimitiveType.Cube, "Strip", Vector3.zero, new Vector3(1, .05f, 1), strip, null, false), "Strip", false);
        Save(Prim(PrimitiveType.Sphere, "Coin", Vector3.zero, new Vector3(.4f, .4f, .4f), coin, null, false), "Coin", false);
        Save(Prim(PrimitiveType.Cube, "SpawnPad", Vector3.zero, new Vector3(3, .1f, 3), spawn), "SpawnPad");
        BuildPlayer(player);

        var door = new GameObject("WallDoor"); // 4 m module, 2 m x 2.5 m opening
        Prim(PrimitiveType.Cube, "JambL", new Vector3(-1.5f, 1.5f, 0), new Vector3(1, 3, .4f), wall, door.transform);
        Prim(PrimitiveType.Cube, "JambR", new Vector3(1.5f, 1.5f, 0), new Vector3(1, 3, .4f), wall, door.transform);
        Prim(PrimitiveType.Cube, "Lintel", new Vector3(0, 2.75f, 0), new Vector3(2, .5f, .4f), wall, door.transform);
        Save(door, "WallDoor");

        var g = new GameObject("Goal");
        Prim(PrimitiveType.Cube, "Pad", new Vector3(0, .05f, 0), new Vector3(3, .1f, 3), goal, g.transform);
        Prim(PrimitiveType.Cube, "Pole", new Vector3(0, 2.1f, 0), new Vector3(.2f, 4, .2f), goal, g.transform);
        Prim(PrimitiveType.Cube, "Flag", new Vector3(.7f, 3.5f, 0), new Vector3(1.2f, .8f, .05f), goal, g.transform);
        Prim(PrimitiveType.Cube, "Beam", new Vector3(0, 8, 0), new Vector3(.5f, 12, .5f), goal, g.transform, false);
        Save(g, "Goal");

        var l = new GameObject("LightPoint");
        var lt = l.AddComponent<Light>();
        lt.type = LightType.Point; lt.color = new Color(1f, .85f, .6f); lt.intensity = 25f; lt.range = 14f;
        Save(l, "LightPoint", false);
        AssetDatabase.SaveAssets();
    }

    // ---------------------------------------------------------------- placing
    static GameObject Put(string n, Vector3 pos, Vector3 size, float yaw = 0)
    {
        var g = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[n]);
        g.transform.SetParent(group != null ? group : root, false);
        g.transform.position = pos;
        g.transform.rotation = Quaternion.Euler(0, yaw, 0);
        g.transform.localScale = size;
        return g;
    }
    static GameObject Raw(string n, Vector3 pos, float yaw = 0) { return Put(n, pos, Vector3.one, yaw); }
    static void Group(string n) { var g = new GameObject(n); g.transform.SetParent(root, false); group = g.transform; }

    // floor slab whose top face is at 'top'
    static void FloorR(float x0, float z0, float x1, float z1, float top = 0f, float th = .4f, string n = "Floor")
    {
        Put(n, new Vector3((x0 + x1) / 2, top - th / 2, (z0 + z1) / 2), new Vector3(x1 - x0, th, z1 - z0));
    }
    // solid block from y=0 up to 'top'
    static void Block(float x0, float z0, float x1, float z1, float top, string n = "Floor") { FloorR(x0, z0, x1, z1, top, top, n); }

    static void Seg(float a, float b, float fixd, bool alongX, float y0)
    {
        if (b - a < .05f) return;
        float c = (a + b) / 2;
        if (alongX) Put("Wall", new Vector3(c, y0 + 1.5f, fixd), new Vector3(b - a, 3, .4f));
        else Put("Wall", new Vector3(fixd, y0 + 1.5f, c), new Vector3(.4f, 3, b - a));
    }
    static void WallRun(bool alongX, float fixd, float a0, float a1, float y0, float[] doors)
    {
        var d = new List<float>(doors); d.Sort(); float cur = a0;
        foreach (var c in d)
        {
            Seg(cur, c - 2, fixd, alongX, y0);
            Raw("WallDoor", alongX ? new Vector3(c, y0, fixd) : new Vector3(fixd, y0, c), alongX ? 0 : 90);
            cur = c + 2;
        }
        Seg(cur, a1, fixd, alongX, y0);
    }
    static void WX(float z, float x0, float x1, params float[] doors) { WallRun(true, z, x0, x1, 0, doors); }   // wall running along X at z
    static void WZ(float x, float z0, float z1, params float[] doors) { WallRun(false, x, z0, z1, 0, doors); }  // wall running along Z at x
    static void Room(float x0, float z0, float x1, float z1, float[] south = null, float[] north = null, float[] west = null, float[] east = null)
    {
        WX(z0, x0 - .2f, x1 + .2f, south ?? None); WX(z1, x0 - .2f, x1 + .2f, north ?? None);
        WZ(x0, z0 + .2f, z1 - .2f, west ?? None); WZ(x1, z0 + .2f, z1 - .2f, east ?? None);
    }
    static void Corridor(float x0, float x1, float z0, float z1, string floor = "Floor")
    {
        FloorR(x0, z0, x1, z1, 0, .4f, floor); WZ(x0, z0 - .2f, z1 + .2f); WZ(x1, z0 - .2f, z1 + .2f);
    }
    // solid steps climbing +Z: 0.3 m rise, 0.6 m run
    static void Stairs(float cx, float zStart, float y0, int steps, float width)
    {
        for (int i = 0; i < steps; i++)
        {
            float h = (i + 1) * .3f;
            Put("Step", new Vector3(cx, y0 + h / 2, zStart + i * .6f + .3f), new Vector3(width, h, .6f));
        }
    }
    // ramp climbing +Z from (z0,y0) to (z1,y1)
    static void RampZ(float cx, float z0, float y0, float z1, float y1, float width)
    {
        float dz = z1 - z0, dy = y1 - y0, len = Mathf.Sqrt(dz * dz + dy * dy), a = Mathf.Atan2(dy, dz);
        var up = new Vector3(0, Mathf.Cos(a), -Mathf.Sin(a));
        var mid = new Vector3(cx, (y0 + y1) / 2, (z0 + z1) / 2) - up * .15f;
        var g = Put("Ramp", mid, new Vector3(width, .3f, len));
        g.transform.rotation = Quaternion.Euler(-a * Mathf.Rad2Deg, 0, 0);
    }
    static void Cover(float x, float z, float yaw = 0, float y = 0) { Put("LowCover", new Vector3(x, y + .55f, z), new Vector3(2, 1.1f, .5f), yaw); }
    static void CoinAt(float x, float z, float y = 0) { Put("Coin", new Vector3(x, y + 1f, z), new Vector3(.4f, .4f, .4f)); }
    static void Strip(float x, float z0, float z1, float y = 0, float w = .8f)
    {
        Put("Strip", new Vector3(x, y + .03f, (z0 + z1) / 2), new Vector3(w, .05f, z1 - z0));
    }
    static void Light(float x, float y, float z, float intensity = 25f)
    {
        var g = Raw("LightPoint", new Vector3(x, y, z)); g.GetComponent<Light>().intensity = intensity;
    }

    // ---------------------------------------------------------------- scene plumbing
    static void Begin()
    {
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        root = new GameObject("Level").transform; group = null;
    }
    static void Spawn(float x, float y, float z)
    {
        var p = Raw("SpawnPad", new Vector3(x, y + .05f, z)); p.name = "Spawn";
        var pl = Raw("Player", new Vector3(x, y + 1f, z)); pl.name = "Player";
        var cam = Camera.main;
        if (cam != null) { cam.transform.position = new Vector3(x, y + 4f, z - 8f); cam.transform.rotation = Quaternion.Euler(18, 0, 0); }
    }
    static void Goal(float x, float y, float z) { var g = Raw("Goal", new Vector3(x, y, z)); g.name = "Goal"; }

    static void Shot(string file)
    {
        var b = new Bounds(); bool first = true;
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        var go = new GameObject("ShotCam");
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(.88f, .90f, .94f);
        go.transform.rotation = Quaternion.Euler(72, -90, 0); // route runs left (spawn) to right (goal)
        go.transform.position = b.center - go.transform.forward * 200f;
        cam.nearClipPlane = 1; cam.farClipPlane = 500;
        const int W = 1600, H = 900;
        float hx = 0, hy = 0;
        for (int i = 0; i < 8; i++)
        {
            var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            var l = go.transform.InverseTransformPoint(c);
            hx = Mathf.Max(hx, Mathf.Abs(l.x)); hy = Mathf.Max(hy, Mathf.Abs(l.y));
        }
        cam.orthographicSize = Mathf.Max(hy, hx / ((float)W / H)) * 1.1f;
        var rt = new RenderTexture(W, H, 24);
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/levels"));
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
    }

    static void Finish(int n)
    {
        string name = "Level0" + n;
        foreach (var lt in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            if (lt.type == LightType.Directional) lt.transform.rotation = Quaternion.Euler(50, -30, 0);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/Scenes/" + name + ".unity");
        Shot("level0" + n + ".png");
        Debug.Log("[LevelBuilder] " + name + " built: " + root.GetComponentsInChildren<Transform>().Length + " objects");
    }

    // ---------------------------------------------------------------- levels
    // Level01  idea: the basics. Room -> corridor -> yard with cover -> one 2.5 m jump. Tool: landmark.
    static void L1()
    {
        Begin();
        Group("SpawnRoom"); FloorR(-6, -2, 6, 10); Room(-6, -2, 6, 10, null, new[] { 0f }); Spawn(0, 0, 3);
        Group("Corridor"); Corridor(-1.5f, 1.5f, 10, 22);
        Group("Yard"); FloorR(-10, 22, 10, 44);
        WX(22, -10.2f, 10.2f, 0); WZ(-10, 22.2f, 43.8f); WZ(10, 22.2f, 43.8f);
        Seg(-10.2f, -4, 44, true, 0); Seg(4, 10.2f, 44, true, 0);
        Cover(-5, 28); Cover(5, 28); Cover(0, 32); Cover(-6, 36); Cover(6, 36); Cover(0, 40);
        CoinAt(-8.5f, 42); CoinAt(8.5f, 42);
        Group("Goal"); FloorR(-4, 46.5f, 4, 54); Goal(0, 0, 51);
        Group("Landmark"); Put("Landmark", new Vector3(0, 7, 64), new Vector3(2, 14, 2));
        Finish(1);
    }

    // Level02  idea: a split. Safe corridor vs. corridor with a gap, then a narrow bridge. Tool: breadcrumbs (coin trail).
    static void L2()
    {
        Begin();
        Group("SpawnPlaza"); FloorR(-8, -2, 8, 10); Room(-8, -2, 8, 10, null, new[] { -5f, 5f }); Spawn(0, 0, 3);
        Group("LeftRoute_Safe"); Corridor(-6.5f, -3.5f, 10, 34);
        Group("RightRoute_Gap");
        FloorR(3.5f, 10, 6.5f, 20); FloorR(3.5f, 22.5f, 6.5f, 34);
        WZ(3.5f, 9.8f, 34.2f); WZ(6.5f, 9.8f, 34.2f);
        Group("Hall"); FloorR(-10, 34, 10, 50); Room(-10, 34, 10, 50, new[] { -5f, 5f }, new[] { 0f }, null, new[] { 42f });
        Cover(-6, 41); Cover(6, 45, 90);
        Group("RewardRoom"); FloorR(10, 38, 16, 46); WX(38, 9.8f, 16.2f); WX(46, 9.8f, 16.2f); WZ(16, 38.2f, 45.8f);
        CoinAt(13, 40); CoinAt(14.5f, 42); CoinAt(13, 44); Put("Crate", new Vector3(14.7f, .6f, 44.7f), new Vector3(1.2f, 1.2f, 1.2f));
        Group("Bridge"); FloorR(-.75f, 50, .75f, 62, 0, .3f);
        Group("Goal"); FloorR(-5, 62, 5, 70); Goal(0, 0, 66);
        Group("Breadcrumbs");
        for (float z = 12; z <= 33; z += 3) CoinAt(-5, z);
        for (float z = 12; z <= 18; z += 3) CoinAt(5, z);
        CoinAt(5, 26); CoinAt(5, 29); CoinAt(5, 32);
        CoinAt(-3, 37); CoinAt(-1.5f, 40); CoinAt(0, 43); CoinAt(0, 46); CoinAt(0, 49);
        for (float z = 52; z <= 60; z += 2) CoinAt(0, z);
        Finish(2);
    }

    // Level03  idea: climb a ramp to the goal. A pinch, then a release into a big hall. Tool: pinch and release.
    static void L3()
    {
        Begin();
        Group("SpawnRoom"); FloorR(-5, -2, 5, 8); Room(-5, -2, 5, 8, null, new[] { 0f }); Spawn(0, 0, 2);
        Group("Corridor"); Corridor(-1.5f, 1.5f, 8, 22); Corridor(-1f, 1f, 22, 30);
        WX(22, -1.5f, -1f); WX(22, 1f, 1.5f);
        Group("Hall"); FloorR(-18, 30, 18, 70); Room(-18, 30, 18, 70, new[] { 0f });
        foreach (var p in new[] { new Vector2(-8, 40), new Vector2(8, 40), new Vector2(-14, 48), new Vector2(14, 48), new Vector2(-8, 52), new Vector2(8, 52) })
            Put("Pillar", new Vector3(p.x, 1.5f, p.y), new Vector3(1, 3, 1));
        Cover(-3, 38); Cover(3, 38); Cover(-12, 56); Cover(12, 56, 90); CoinAt(-15, 36); CoinAt(15, 36); CoinAt(-16, 66); CoinAt(16, 66);
        Group("ClimbAndGoal"); Block(-7, 60, 7, 70, 2.4f); RampZ(0, 53, 0, 60, 2.4f, 4); Goal(0, 2.4f, 66);
        Finish(3);
    }

    // Level04  idea: height. Stairs up, two 2.5 m jumps across a dark void. Tool: light and contrast.
    static void L4()
    {
        Begin();
        Group("GroundRoom"); FloorR(-8, -2, 8, 14, 0, .4f, "FloorDark"); Room(-8, -2, 8, 14, null, new[] { 0f }); Spawn(0, 0, 3);
        Strip(0, 4, 13); Light(0, 2.8f, 8);
        Group("Stair1"); WZ(-1.5f, 13.8f, 20.2f); WZ(1.5f, 13.8f, 20.2f); Stairs(0, 14, 0, 10, 3);
        Group("Platform1"); FloorR(-6, 20, 6, 30, 3, .6f, "FloorDark"); Strip(0, 20, 30, 3); Light(0, 6.5f, 25);
        Group("Platform2"); FloorR(-3, 32.5f, 3, 40, 3, .6f, "FloorDark"); Strip(0, 32.5f, 40, 3); Light(0, 6.5f, 36);
        Group("Stair2"); Stairs(0, 40, 3, 10, 3);
        Group("Platform3"); FloorR(-5, 46, 5, 54, 6, .6f, "FloorDark"); Strip(0, 46, 54, 6); Light(0, 9.5f, 50);
        Group("Goal"); FloorR(-4, 56.5f, 4, 64, 6, .6f, "Floor"); Goal(0, 6, 60); Light(0, 10, 60, 60);
        Finish(4);
    }

    // Level05  idea: everything together. Chain of doorway frames, a split (cover maze vs. narrow bridge), a ramp, and a 2.8 m jump.
    // Tool: framing (aligned doorways steer the eye down the axis).
    static void L5()
    {
        Begin();
        Group("SpawnRoom"); FloorR(-6, -2, 6, 10); Room(-6, -2, 6, 10, null, new[] { 0f }); Spawn(0, 0, 3);
        Group("FrameHall"); Corridor(-3, 3, 10, 34);
        foreach (float z in new[] { 16f, 22f, 28f }) { Raw("WallDoor", new Vector3(0, 0, z)); Seg(-3, -2, z, true, 0); Seg(2, 3, z, true, 0); }
        Group("Courtyard");
        FloorR(-14, 34, 2, 62); FloorR(2, 34, 14, 40); FloorR(2, 56, 14, 62); FloorR(12, 40, 14, 56);
        Room(-14, 34, 14, 62, new[] { 0f }, new[] { 0f }, new[] { 44f });
        Cover(-9, 40); Cover(-4, 46, 90); Cover(-10, 50); Cover(-3, 53); Cover(-8, 58);
        Group("Bridge"); FloorR(6.25f, 40, 7.75f, 56, 0, .3f);
        Group("RewardRoom"); FloorR(-20, 40, -14, 48); WX(40, -20.2f, -13.8f); WX(48, -20.2f, -13.8f); WZ(-20, 40.2f, 47.8f);
        CoinAt(-18, 42); CoinAt(-16.5f, 45); CoinAt(-18, 46.5f); Put("Crate", new Vector3(-18.7f, .6f, 41.2f), new Vector3(1.2f, 1.2f, 1.2f));
        Group("RampRoom"); Corridor(-3, 3, 62, 69); WZ(-3, 69, 80.2f); WZ(3, 69, 80.2f);
        RampZ(0, 69, 0, 76, 2.4f, 4); Block(-3, 76, 3, 80, 2.4f);
        Group("Goal"); Block(-4, 82.8f, 4, 90, 2.4f); Goal(0, 2.4f, 86);
        Finish(5);
    }

    [MenuItem("CS464/Build Kit Only")]
    public static void BuildKitOnly()
    {
        BuildKit();
        AssetDatabase.SaveAssets();
        Debug.Log("[LevelBuilder] kit built");
    }

    [MenuItem("CS464/Build Kit + Levels")]
    public static void BuildAll()
    {
        BuildKit();
        L1(); L1(); L2(); L3(); L4(); L5(); // first render warms up shaders, so L1 runs twice
        var list = new List<EditorBuildSettingsScene>();
        for (int i = 1; i <= 5; i++) list.Add(new EditorBuildSettingsScene("Assets/Scenes/Level0" + i + ".unity", true));
        EditorBuildSettings.scenes = list.ToArray();
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("[LevelBuilder] done");
    }
}
