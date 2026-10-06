using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScriptedScreensFonts;

/// <summary>
/// Drives <see cref="FontRegistry"/> rescans. Bundled fonts do not exist yet when
/// LaunchPad loads mods, so a single scan at startup finds almost nothing.
/// </summary>
/// <remarks>
/// <c>Resources.FindObjectsOfTypeAll</c> walks every loaded object, so the burst after each
/// scene load stays short. It is followed by a slow heartbeat that never stops, because
/// fonts keep arriving after the burst closes: signage faces load with the prefabs that use
/// them, so they appear whenever the player first comes near one. A burst-only scan silently
/// misses those, and the miss looks like a broken mod rather than a timing gap. How long that
/// tail actually runs has never been measured; <see cref="Scan"/> logs the arrival time of
/// every registration so that a long session can settle it.
/// </remarks>
internal sealed class FontRegistryLoader : MonoBehaviour
{
    private const int RescanCount = 15;
    private const float RescanIntervalSeconds = 2f;
    private const float HeartbeatIntervalSeconds = 20f;
    private const float HeartbeatMaxSeconds = 320f;

    private static IEnumerator? _download;
    private Coroutine? _rescan;

    /// <summary>The running loader, which also hosts runtime font requests (<see cref="FontApi"/>).</summary>
    internal static FontRegistryLoader? Instance { get; private set; }

    /// <param name="download">Font downloads to run once, on this component, so a scene load
    /// that restarts the rescan window cannot cut one off halfway.</param>
    internal static void Install(IEnumerator? download)
    {
        _download = download;
        var host = new GameObject(nameof(ScriptedScreensFonts))
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        DontDestroyOnLoad(host);
        host.AddComponent<FontRegistryLoader>();
    }

    private void OnEnable()
    {
        Instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (_download != null)
        {
            StartCoroutine(_download);
            _download = null;
        }

        _rescan = StartCoroutine(RescanWindow());
    }

    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Only the rescan: StopAllCoroutines would also kill a download in flight and leave
        // FontDownloader.Busy set for ever, so no file font would ever load.
        if (_rescan != null)
            StopCoroutine(_rescan);

        _rescan = StartCoroutine(RescanWindow());
    }

    private static IEnumerator RescanWindow()
    {
        var burst = new WaitForSeconds(RescanIntervalSeconds);

        // Docs register from here rather than at mod load, so it does not matter whether
        // StationeersLua loaded before or after this mod; by the end of the burst it has or never will.
        var docs = false;
        for (var i = 0; i < RescanCount; i++)
        {
            docs = docs || FontsDocsTool.TryRegister();
            Scan();
            yield return burst;
        }

        if (!docs)
            ScriptedScreensFontsPlugin.Log?.LogInfo("StationeersLua MCP registry not present; fonts docs not registered.");

        // Slow tail: catches fonts that arrive with prefabs streamed in later. Only newly
        // seen names log anything, so a quiet session stays quiet.
        //
        // It backs off rather than stopping. Each quiet scan doubles the wait up to
        // HeartbeatMaxSeconds, and the first scan that finds a font drops it back to the floor,
        // so a session that has settled costs a sixteenth of what it did while still answering
        // within minutes when a late prefab brings a face in. A stop condition was the other
        // option and was rejected: there is no font set to expect -- what loads depends on the
        // save, the language and which prefabs the player comes near -- so any threshold that
        // ends the scan can go permanently blind, which reads as a broken mod.
        var interval = HeartbeatIntervalSeconds;
        var heartbeat = new WaitForSeconds(interval);

        while (true)
        {
            var found = Scan();
            var next = found ? HeartbeatIntervalSeconds : Mathf.Min(interval * 2f, HeartbeatMaxSeconds);
            if (next != interval)
            {
                interval = next;
                heartbeat = new WaitForSeconds(interval);
            }

            yield return heartbeat;
        }
    }

    /// <returns><see langword="true"/> if this scan registered anything new.</returns>
    private static bool Scan()
    {
        var before = FontRegistry.Count;
        FontLoader.TryLoadPending();
        FontRegistry.ScanAndRegister();
        if (FontRegistry.Count == before)
            return false;

        FontsDocsTool.RefreshAvailable();

        // When the last font arrived, in seconds since this component started. The tail of the
        // heartbeat exists because fonts keep coming after the burst, but how long that tail
        // really is has never been measured -- this line is what would measure it.
        ScriptedScreensFontsPlugin.Log?.LogInfo(
            $"{FontRegistry.Count - before} font(s) registered at {Time.realtimeSinceStartup:0} s; {FontRegistry.Count} in total.");
        return true;
    }
}
