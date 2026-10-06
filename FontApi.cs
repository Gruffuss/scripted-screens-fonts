using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using BepInEx.Configuration;

namespace ScriptedScreensFonts;

/// <summary>
/// The fonts mod's public entry point for other mods: load the fonts behind a link at runtime,
/// for example from a page's <c>@font-face { src: url(...) }</c>. Call it by reflection so
/// neither mod needs the other to build:
/// <code>
/// var api = Type.GetType("ScriptedScreensFonts.FontApi, ScriptedScreensFonts");
/// api?.GetMethod("RequestFont")?.Invoke(null, new object[] { link, (Action&lt;string[]&gt;)OnFonts });
/// </code>
/// </summary>
/// <remarks>
/// <para>
/// A page is written by whoever built the console, and in multiplayer its source reaches every
/// player. A font file is parsed by FreeType, native code, and crafted font files are a classic
/// way into font parsers. So a page may only download from the hosts in the
/// <c>PageFontHosts</c> setting, and the default is Google Fonts alone: it serves only Google's
/// own curated files, where a general CDN such as jsDelivr serves anything from any GitHub repo.
/// A stylesheet's font links must pass the same check. Links in <c>FontUrls</c> are the
/// player's own choice and are not limited.
/// </para>
/// <para>
/// Files land in the same <c>fonts/downloaded</c> cache as <c>FontUrls</c>, so a page's font is
/// downloaded once ever, not once per session. A font the player has switched off in the mod
/// settings stays off. Each loaded face costs about 1 MB of texture memory for the session, so
/// page requests are capped at <see cref="MaxPageFaces"/> faces per session.
/// </para>
/// </remarks>
public static class FontApi
{
    private const int MaxPageFaces = 48;

    /// <summary>How long a request waits for the font loader before reporting nothing.</summary>
    private const float StartupWaitSeconds = 60f;

    private static readonly char[] HostSeparators = { ',', ' ', ';', '\n', '\r', '\t' };
    private static readonly Dictionary<string, Pending> Requests = new(StringComparer.Ordinal);
    private static ConfigEntry<string>? _hosts;
    private static int _faces;

    private sealed class Pending
    {
        public readonly List<Action<string[]>> Callbacks = new();
        public string[]? Names;
    }

    /// <summary>The <c>PageFontHosts</c> setting, read on every request so a change applies at once.</summary>
    internal static void Configure(ConfigEntry<string> hosts) => _hosts = hosts;

    /// <summary>
    /// Loads the fonts behind <paramref name="link"/> and reports the names they can be used
    /// under, as <c>&lt;font="Name"&gt;</c> or a CSS <c>font-family</c>.
    /// </summary>
    /// <param name="link">An http(s) link to a <c>.ttf</c>/<c>.otf</c> file or a Google Fonts
    /// stylesheet, on a host listed in the <c>PageFontHosts</c> setting.</param>
    /// <param name="done">Called once, on the main thread, with the registered names ("Manrope",
    /// "Manrope Bold"); empty when nothing could be loaded, in which case a later request tries
    /// again. Called immediately when the link was already loaded this session.</param>
    /// <returns>False when the link is refused — malformed, not http(s), or its host not
    /// allowed — and <paramref name="done"/> is not called. The reason is in the log.</returns>
    public static bool RequestFont(string link, Action<string[]>? done)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var url) || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            ScriptedScreensFontsPlugin.Log?.LogWarning($"Font request refused, not an http(s) link: {link}");
            return false;
        }

        if (!Allowed(url))
        {
            ScriptedScreensFontsPlugin.Log?.LogWarning($"Font request refused: {url.Host} is not in PageFontHosts ({link}).");
            return false;
        }

        var host = FontRegistryLoader.Instance;
        var folder = FontLoader.UserFontsFolder();
        if (host == null || folder == null)
        {
            ScriptedScreensFontsPlugin.Log?.LogWarning($"Font request refused, the loader is not running: {link}");
            return false;
        }

        var key = url.AbsoluteUri;
        if (Requests.TryGetValue(key, out var pending))
        {
            if (pending.Names != null)
                Deliver(done, pending.Names);
            else if (done != null)
                pending.Callbacks.Add(done);

            return true;
        }

        pending = new Pending();
        if (done != null)
            pending.Callbacks.Add(done);

        Requests[key] = pending;
        host.StartCoroutine(Load(url, Path.Combine(folder, "downloaded"), pending));
        return true;
    }

    private static bool Allowed(Uri url)
    {
        foreach (var entry in (_hosts?.Value ?? string.Empty).Split(HostSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            var allowed = entry.Trim().TrimStart('.');
            if (url.Host.Equals(allowed, StringComparison.OrdinalIgnoreCase)
                || url.Host.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static IEnumerator Load(Uri url, string folder, Pending pending)
    {
        var names = new List<string>();
        try
        {
            if (!FontDownloader.TryCached(url, folder, out var paths))
            {
                paths = new List<string>();
                yield return FontDownloader.Fetch(url, folder, Allowed, paths);
            }

            // The launch's own fonts first; a cached file may be among them already. Bounded,
            // because this waits on conditions a request cannot influence: if TMP's shader never
            // materialises, an unbounded wait would leave the caller's callback never arriving
            // at all, and a page waiting for ever is worse than one told it got nothing.
            var deadline = Time.realtimeSinceStartup + StartupWaitSeconds;
            while (FontLoader.StartupPending || !FontLoader.ShaderReady)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    ScriptedScreensFontsPlugin.Log?.LogWarning(
                        $"Font request {url}: the font loader was still not ready after {StartupWaitSeconds:0} s; giving up on this request.");
                    yield break;
                }

                yield return null;
            }

            foreach (var path in paths)
            {
                var name = FontLoader.LoadedName(path);
                if (name == null && !FontLoader.IsDisabled(path))
                {
                    if (_faces >= MaxPageFaces)
                    {
                        ScriptedScreensFontsPlugin.Log?.LogWarning($"Font request {url}: {MaxPageFaces} page font faces loaded this session, no more are built.");
                        break;
                    }

                    name = FontLoader.LoadNow(path);
                    if (name != null)
                        _faces++;
                }

                if (name != null && !names.Contains(name))
                    names.Add(name);
            }
        }
        finally
        {
            var result = names.ToArray();
            pending.Names = result;
            foreach (var callback in pending.Callbacks)
                Deliver(callback, result);

            pending.Callbacks.Clear();

            // Nothing loaded: forget it, so the next request tries again.
            if (result.Length == 0)
                Requests.Remove(url.AbsoluteUri);
            else
                FontsDocsTool.RefreshAvailable();
        }
    }

    private static void Deliver(Action<string[]>? done, string[] names)
    {
        try
        {
            done?.Invoke(names);
        }
        catch (Exception ex)
        {
            ScriptedScreensFontsPlugin.Log?.LogWarning($"A font request callback threw: {ex}");
        }
    }
}
