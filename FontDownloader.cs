using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine.Networking;
using UnityEngine.TextCore.LowLevel;

namespace ScriptedScreensFonts;

/// <summary>
/// Downloads the font URLs listed in the config into <c>&lt;save folder&gt;/fonts/downloaded</c>,
/// once, and hands new files to <see cref="FontLoader"/>. After that they are ordinary player
/// fonts: the next launch finds them on disk, with their own on/off switches, and needs no
/// network.
/// </summary>
/// <remarks>
/// <para>
/// A URL is either a font file (<c>.ttf</c>/<c>.otf</c>) or a Google Fonts CSS link
/// (<c>fonts.googleapis.com/css2?family=Manrope:wght@400;700</c>). Google answers a
/// user agent it does not recognise as a browser, such as Unity's, with plain TrueType files
/// rather than WOFF2, and serves a variable font as one static file per requested weight, so
/// every <c>url(...)</c> in the stylesheet is loadable as it stands.
/// </para>
/// <para>
/// Files are named from the font's own metadata (<c>Manrope-Bold.ttf</c>), not the URL: CDN
/// file names are hashes, and the name becomes the config switch's label.
/// <c>sources.txt</c> maps each configured URL to the files it produced, which is how a URL is
/// known to be done without asking the network.
/// </para>
/// </remarks>
internal static class FontDownloader
{
    private const string IndexFile = "sources.txt";
    private const int TimeoutSeconds = 30;

    // Whitespace only. A Google Fonts CSS link carries both ';' and ',' inside it
    // (`family=Inter:wght@400;700`, `family=Inter:ital,wght@0,400;1,700`), so either as a
    // separator cuts the link in half and silently downloads only its first weight.
    private static readonly char[] UrlSeparators = { ' ', '\n', '\r', '\t' };
    private static readonly char[] FileSeparator = { '|' };
    private static readonly Regex CssUrl = new(@"url\(\s*['""]?([^'"")\s]+)['""]?\s*\)", RegexOptions.Compiled);

    /// <summary>True while downloads are outstanding; <see cref="FontLoader"/> waits for them.</summary>
    internal static bool Busy { get; private set; }

    /// <summary>Configured URLs, split on commas, spaces and new lines; anything not http(s) is refused.</summary>
    internal static List<Uri> ParseUrls(string value)
    {
        var urls = new List<Uri>();
        foreach (var part in (value ?? string.Empty).Split(UrlSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (Uri.TryCreate(part, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                urls.Add(uri);
            else
                ScriptedScreensFontsPlugin.Log?.LogWarning($"Font URL ignored, not an http(s) link: {part}");
        }

        return urls;
    }

    /// <summary>Marks downloads as outstanding before the coroutine gets its first frame.</summary>
    internal static void Prepare(List<Uri> urls) => Busy = urls.Count > 0;

    internal static IEnumerator Run(List<Uri> urls, string folder)
    {
        try
        {
            if (urls.Count == 0)
                yield break;

            foreach (var url in urls)
            {
                if (TryCached(url, folder, out _))
                    continue;

                yield return Fetch(url, folder, null, new List<string>());
            }
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>
    /// The files a URL produced on an earlier download, when every one is still on disk.
    /// One index for the whole session, so two downloads running at once cannot overwrite each
    /// other's entries (both run on the main thread, interleaved at their yields).
    /// </summary>
    internal static bool TryCached(Uri url, string folder, out List<string> paths)
    {
        paths = new List<string>();
        if (!Index(folder).TryGetValue(url.AbsoluteUri, out var files))
            return false;

        foreach (var file in files)
        {
            var path = Path.Combine(folder, file);
            if (!File.Exists(path))
                return false;

            paths.Add(path);
        }

        return paths.Count > 0;
    }

    private static Dictionary<string, List<string>>? _index;

    private static Dictionary<string, List<string>> Index(string folder) => _index ??= ReadIndex(folder);

    /// <summary>
    /// Downloads a URL (following a stylesheet to its font files) into <paramref name="folder"/>,
    /// adds the saved files' full paths to <paramref name="produced"/> and records them in the
    /// index. <paramref name="allowed"/>, when given, must accept every URL fetched, the
    /// stylesheet's font links included.
    /// </summary>
    internal static IEnumerator Fetch(Uri url, string folder, Func<Uri, bool>? allowed, List<string> produced)
    {
        Directory.CreateDirectory(folder);
        yield return FetchFiles(url, folder, allowed, produced);

        if (produced.Count == 0)
            yield break;

        var index = Index(folder);
        index[url.AbsoluteUri] = produced.ConvertAll(Path.GetFileName);
        WriteIndex(folder, index);
    }

    private static IEnumerator FetchFiles(Uri url, string folder, Func<Uri, bool>? allowed, List<string> produced)
    {
        using var request = UnityWebRequest.Get(url);
        request.timeout = TimeoutSeconds;
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            ScriptedScreensFontsPlugin.Log?.LogWarning($"Could not download font URL {url}: {request.error}");
            yield break;
        }

        var bytes = request.downloadHandler.data;
        if (!IsStylesheet(url, request.GetResponseHeader("Content-Type"), bytes))
        {
            Save(url, bytes, folder, produced);
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in CssUrl.Matches(request.downloadHandler.text))
        {
            // Relative url() entries resolve against the stylesheet, as in a browser.
            if (!Uri.TryCreate(match.Groups[1].Value, UriKind.RelativeOrAbsolute, out var reference)
                || !Uri.TryCreate(url, reference, out var fontUrl) || !seen.Add(fontUrl.AbsoluteUri))
                continue;

            // An allowed stylesheet must not be a way to reach a host that is not allowed.
            if (allowed != null && !allowed(fontUrl))
            {
                ScriptedScreensFontsPlugin.Log?.LogWarning($"Font link {fontUrl} in {url} refused: its host is not in PageFontHosts.");
                continue;
            }

            using var font = UnityWebRequest.Get(fontUrl);
            font.timeout = TimeoutSeconds;
            yield return font.SendWebRequest();

            if (font.result == UnityWebRequest.Result.Success)
                Save(fontUrl, font.downloadHandler.data, folder, produced);
            else
                ScriptedScreensFontsPlugin.Log?.LogWarning($"Could not download {fontUrl} (from {url}): {font.error}");
        }

        if (seen.Count == 0)
            ScriptedScreensFontsPlugin.Log?.LogWarning($"Font URL {url} is a stylesheet with no font files in it.");
    }

    private static bool IsStylesheet(Uri url, string? contentType, byte[] bytes)
    {
        if (contentType != null && contentType.Contains("text/css", StringComparison.OrdinalIgnoreCase))
            return true;

        if (url.AbsoluteUri.Contains("fonts.googleapis.com/css", StringComparison.OrdinalIgnoreCase))
            return true;

        // A font file starts with a binary tag; a stylesheet with text.
        return bytes.Length > 0 && (bytes[0] == (byte)'@' || bytes[0] == (byte)'/');
    }

    /// <summary>Checks the bytes are a font the engine reads, then names and writes the file.</summary>
    private static void Save(Uri url, byte[] bytes, string folder, List<string> produced)
    {
        try
        {
            FontEngine.InitializeFontEngine();
            if (FontEngine.LoadFontFace(bytes, 48) != FontEngineError.Success)
            {
                ScriptedScreensFontsPlugin.Log?.LogWarning($"{url} is not a font file the engine can read (use a .ttf or .otf link; WOFF2 is not read).");
                return;
            }

            var face = FontEngine.GetFaceInfo();
            var family = string.IsNullOrEmpty(face.familyName) ? Path.GetFileNameWithoutExtension(url.AbsolutePath) : face.familyName;
            var style = string.IsNullOrEmpty(face.styleName) ? "Regular" : face.styleName;
            var extension = url.AbsolutePath.EndsWith(".otf", StringComparison.OrdinalIgnoreCase) ? ".otf" : ".ttf";
            var file = SafeFileName(family + "-" + style) + extension;
            var path = Path.Combine(folder, file);

            File.WriteAllBytes(path, bytes);
            produced.Add(path);
            FontLoader.AddDownloaded(path);
            ScriptedScreensFontsPlugin.Log?.LogInfo($"Downloaded {family} {style} as {file} ({bytes.Length / 1024} KB) from {url}");
        }
        catch (Exception ex)
        {
            ScriptedScreensFontsPlugin.Log?.LogWarning($"Could not save font from {url}: {ex.Message}");
        }
    }

    private static string SafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        return name;
    }

    private static Dictionary<string, List<string>> ReadIndex(string folder)
    {
        var index = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var path = Path.Combine(folder, IndexFile);
        if (!File.Exists(path))
            return index;

        foreach (var line in File.ReadAllLines(path))
        {
            var tab = line.IndexOf('\t', StringComparison.Ordinal);
            if (tab > 0)
                index[line.Substring(0, tab)] = new List<string>(line.Substring(tab + 1).Split(FileSeparator, StringSplitOptions.RemoveEmptyEntries));
        }

        return index;
    }

    private static void WriteIndex(string folder, Dictionary<string, List<string>> index)
    {
        var lines = new List<string>();
        foreach (var entry in index)
            lines.Add(entry.Key + "\t" + string.Join("|", entry.Value));

        File.WriteAllLines(Path.Combine(folder, IndexFile), lines);
    }
}
