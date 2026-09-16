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
/// fonts keep arriving long after the burst closes: signage faces load with the prefabs
/// that use them, which can be an hour into a session. A burst-only scan silently misses
/// those, and the miss looks like a broken mod rather than a timing gap.
/// </remarks>
internal sealed class FontRegistryLoader : MonoBehaviour
{
    private const int RescanCount = 15;
    private const float RescanIntervalSeconds = 2f;
    private const float HeartbeatIntervalSeconds = 20f;

    internal static void Install()
    {
        var host = new GameObject(nameof(ScriptedScreensFonts))
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        DontDestroyOnLoad(host);
        host.AddComponent<FontRegistryLoader>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(RescanWindow());
    }

    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StopAllCoroutines();
        StartCoroutine(RescanWindow());
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
        var heartbeat = new WaitForSeconds(HeartbeatIntervalSeconds);

        while (true)
        {
            Scan();
            yield return heartbeat;
        }
    }

    private static void Scan()
    {
        var before = FontRegistry.Count;
        FontLoader.TryLoadPending();
        FontRegistry.ScanAndRegister();
        if (FontRegistry.Count != before)
            FontsDocsTool.RefreshAvailable();
    }
}
