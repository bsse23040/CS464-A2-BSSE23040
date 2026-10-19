using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Plays all five levels in a row with RouteAutopilot and reports whether each one reached its goal.
/// Menu: CS464 > Test All Levels. The same run is used to record the video (CS464 > Record Walkthrough),
/// which also maximises the Game view and writes the start and end time of each level to Logs/walkthrough_times.txt.
/// You can also start it by creating the file Temp/run_levels or Temp/record_levels.
/// </summary>
[InitializeOnLoad]
public static class LevelRunner
{
    const string KActive = "LevelRunner.active", KIndex = "LevelRunner.index", KRecord = "LevelRunner.record",
                 KStart = "LevelRunner.start", KFinishedAt = "LevelRunner.finishedAt", KLogged = "LevelRunner.logged";

    static readonly string[] Scenes = { "Level01", "Level02", "Level03", "Level04", "Level05" };

    // (x, z, jump). The player's own x and z are measured from the spawn; a jump is pressed when the point is reached,
    // about 0.6 m before the edge of a gap.
    static readonly float[][][] Routes = {
        new[] { P(0, 8), P(0, 18), P(0, 25.5f), P(2.5f, 30), P(2.5f, 36), P(2.5f, 42), P(0, 43.4f, 1), P(0, 48), P(0, 51) },
        new[] { P(5, 7), P(5, 12), P(5, 19.4f, 1), P(5, 27), P(5, 34), P(5, 36), P(2, 41), P(0, 46), P(0, 50), P(0, 56), P(0, 62), P(0, 66) },
        new[] { P(0, 12), P(0, 24), P(0, 31), P(0, 45), P(0, 53), P(0, 60), P(0, 66) },
        new[] { P(0, 12), P(0, 19), P(0, 24), P(0, 29.4f, 1), P(0, 36), P(0, 43), P(0, 48), P(0, 53.4f, 1), P(0, 58), P(0, 60) },
        new[] { P(0, 12), P(0, 22), P(0, 33), P(0, 45), P(0, 55), P(0, 63), P(0, 70), P(0, 78), P(0, 79.4f, 1), P(0, 84), P(0, 86) },
    };
    static float[] P(float x, float z, float jump = 0f) { return new[] { x, z, jump }; }

    static bool Active { get { return SessionState.GetBool(KActive, false); } }

    static LevelRunner()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.update += WatchTriggers;
        if (Active && EditorApplication.isPlaying) EditorApplication.update += Poll;
    }

    [MenuItem("CS464/Test All Levels")] public static void TestAll() { Begin(false); }
    [MenuItem("CS464/Record Walkthrough")] public static void Record() { Begin(true); }

    static void WatchTriggers()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists("Temp/run_levels")) { File.Delete("Temp/run_levels"); Begin(false); }
        else if (File.Exists("Temp/record_levels")) { File.Delete("Temp/record_levels"); Begin(true); }
    }

    static void Begin(bool record)
    {
        SessionState.SetBool(KActive, true);
        SessionState.SetBool(KRecord, record);
        SessionState.SetInt(KIndex, 0);
        Directory.CreateDirectory("Logs");
        if (record) File.WriteAllText("Logs/walkthrough_times.txt", "");
        Debug.Log("[Runner] START " + (record ? "(recording)" : "(test)"));
        StartLevel();
    }

    static void StartLevel()
    {
        int i = SessionState.GetInt(KIndex, 0);
        SessionState.SetBool(KLogged, false);
        SessionState.SetFloat(KFinishedAt, -1f);
        EditorSceneManager.OpenScene("Assets/Scenes/" + Scenes[i] + ".unity", OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        Debug.Log("[Runner] play mode: " + state + " (active=" + Active + ", index=" + SessionState.GetInt(KIndex, 0) + ")");
        if (!Active) return;
        var gameView = typeof(Editor).Assembly.GetType("UnityEditor.GameView");

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            SessionState.SetFloat(KStart, (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Poll; EditorApplication.update += Poll;
            if (SessionState.GetBool(KRecord, false))
            {
                EditorApplication.delayCall += () => { var w = EditorWindow.GetWindow(gameView); w.Focus(); w.maximized = true; };
                Stamp(Scenes[SessionState.GetInt(KIndex, 0)] + " start");
            }
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Poll;
            if (SessionState.GetBool(KRecord, false)) { var w = EditorWindow.GetWindow(gameView); if (w.maximized) w.maximized = false; }
            int next = SessionState.GetInt(KIndex, 0) + 1;
            SessionState.SetInt(KIndex, next);
            if (next < Scenes.Length) EditorApplication.delayCall += StartLevel;
            else { SessionState.SetBool(KActive, false); Debug.Log("[Runner] ALL DONE"); }
        }
    }

    static void Stamp(string what)
    {
        File.AppendAllText("Logs/walkthrough_times.txt", what + " " + DateTime.UtcNow.ToString("O") + "\n");
    }

    static void Poll()
    {
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        if (player == null) return;
        int i = SessionState.GetInt(KIndex, 0);
        float now = (float)EditorApplication.timeSinceStartup;
        var auto = player.GetComponent<RouteAutopilot>();

        if (auto == null)
        {
            var route = Routes[i];
            var pts = new Vector2[route.Length]; var jumps = new bool[route.Length];
            for (int k = 0; k < route.Length; k++) { pts[k] = new Vector2(route[k][0], route[k][1]); jumps[k] = route[k][2] > 0f; }
            auto = player.gameObject.AddComponent<RouteAutopilot>();
            auto.Setup(pts, jumps);
            return;
        }

        float elapsed = now - SessionState.GetFloat(KStart, now);
        bool fell = player.transform.position.y < -5f;

        if (SessionState.GetFloat(KFinishedAt, -1f) < 0f)
        {
            if (auto.Finished || auto.Stuck || fell || elapsed > 70f)
            {
                string result = auto.Finished ? "REACHED THE GOAL in " + elapsed.ToString("0.0") + " s"
                    : fell ? "FELL at waypoint " + auto.Index + " position " + player.transform.position
                    : auto.Stuck ? "STUCK at waypoint " + auto.Index + " position " + player.transform.position
                    : "TIMED OUT at waypoint " + auto.Index + " position " + player.transform.position;
                Debug.Log("[Runner] " + Scenes[i] + ": " + result);
                if (SessionState.GetBool(KRecord, false)) Stamp(Scenes[i] + " end");
                SessionState.SetFloat(KFinishedAt, now);
            }
        }
        else if (now - SessionState.GetFloat(KFinishedAt, now) > 1.5f)      // hold on the goal for a moment
        {
            EditorApplication.update -= Poll;
            EditorApplication.isPlaying = false;
        }
    }
}
