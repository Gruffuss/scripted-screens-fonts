using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ScriptedScreensFonts;

/// <summary>
/// Publishes the shipped docs and examples as StationeersLua MCP resources, the way
/// ScriptedScreens, ScriptedScreens Vector and ScriptedScreens Html do: a <c>fonts</c> search
/// scope, the guide one resource per section, the changelog, a quick start served as the index,
/// everything under <c>examples/</c>, and a live list of the font names registered right now.
/// </summary>
/// <remarks>
/// Bound by reflection so the mod loads without StationeersLua. Split by section because the
/// search returns at most two hits per resource. Files are read when a resource is read, so the
/// docs never go stale against the DLL.
/// </remarks>
internal static class FontsDocsTool
{
    private const string DocRoot = "stationeers://fonts/";

    private static bool _done;
    private static Action? _refreshAvailable;

    /// <summary>
    /// Re-registers the live font list. A resource read always calls its function, but the
    /// search index keeps the text from its last build and rebuilds only after a registration,
    /// so without this the list would be searchable only as it was when the index was built.
    /// </summary>
    internal static void RefreshAvailable() => _refreshAvailable?.Invoke();

    /// <summary>Registers once. Returns false while StationeersLua's registry is not loaded yet.</summary>
    internal static bool TryRegister()
    {
        if (_done)
            return true;

        try
        {
            var registry = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("StationeersLua.LuaMcpRegistry", throwOnError: false))
                .FirstOrDefault(t => t != null);
            if (registry == null)
                return false;

            _done = true;
            RegisterDocs(registry);
        }
        catch (Exception ex)
        {
            // Never fatal: the mod's job is fonts, and the docs are a convenience.
            _done = true;
            ScriptedScreensFontsPlugin.Log?.LogWarning($"fonts docs registration failed: {ex.Message}");
        }

        return true;
    }

    private static void RegisterDocs(Type registry)
    {
        var folder = Path.GetDirectoryName(typeof(FontsDocsTool).Assembly.Location);
        if (string.IsNullOrEmpty(folder))
            return;

        var resource = registry.GetMethod("RegisterDocumentationResource",
            new[] { typeof(string), typeof(string), typeof(string), typeof(string), typeof(Func<string>) });
        if (resource == null)
        {
            ScriptedScreensFontsPlugin.Log?.LogInfo("StationeersLua has no documentation resources; fonts docs not registered.");
            return;
        }

        registry.GetMethod("RegisterDocumentationSearchScope", new[] { typeof(string), typeof(string) })
            ?.Invoke(null, new object[] { "fonts", DocRoot });

        var index = new StringBuilder("\nSearch everything with scope `fonts`, or read these URIs:\n\n");
        var count = 0;

        void Add(string key, string name, string description, Func<string> content)
        {
            resource.Invoke(null, new object[] { DocRoot + key, name, description, "text/markdown", content });
            index.Append("- `").Append(DocRoot).Append(key).Append("` -- ").Append(name).Append('\n');
            count++;
        }

        const string AvailableName = "Fonts available now";
        const string AvailableDescription = "Every font name <font=\"...\"> resolves in this game right now, with where it came from. Game fonts keep arriving for the first minutes of a session.";
        Add("available", AvailableName, AvailableDescription, Available);

        var rebuild = registry.GetMethod("RequestDocumentationSearchIndexRefresh", Type.EmptyTypes);
        _refreshAvailable = () =>
        {
            try
            {
                resource.Invoke(null, new object[] { DocRoot + "available", AvailableName, AvailableDescription, "text/markdown", new Func<string>(Available) });
                rebuild?.Invoke(null, null);
            }
            catch (Exception ex)
            {
                _refreshAvailable = null;
                ScriptedScreensFontsPlugin.Log?.LogWarning($"fonts list refresh failed: {ex.Message}");
            }
        };

        var readme = Path.Combine(folder, "README.md");
        foreach (var (line, label, slug) in Sections(readme))
        {
            var wanted = line;
            Add($"guide/{slug}", $"Fonts Guide: {label}",
                $"ScriptedScreens Fonts guide -- section \"{label}\".", () => Section(readme, wanted));
        }

        Add("changelog", "Fonts Changelog", "ScriptedScreens Fonts release history, newest first.",
            () => Read(Path.Combine(folder, "CHANGELOG.md")));

        index.Append("\nRunnable examples, one idea each: `").Append(DocRoot).Append("examples/index`.\n");
        index.Append("A picture of a console: the `capture_scripted_screen` tool. Chip errors: `get_chip_errors`.\n");

        // The index is the quick start with the resource list appended: the one page an editor
        // needs before styling a first label, and the map to everything else.
        var list = index.ToString();
        var quickstart = Path.Combine(folder, "QUICKSTART.md");
        resource.Invoke(null, new object[] { DocRoot + "index", "Fonts quick start and documentation index",
            "Start here: how to use a font in a ScriptedScreens label, how to add font files, the rules that fail silently, and every documentation URI.",
            "text/markdown", new Func<string>(() => Read(quickstart) + list) });

        registry.GetMethod("RegisterBundledExampleDocumentation", new[] { typeof(string), typeof(string), typeof(string) })
            ?.Invoke(null, new object[] { folder!, DocRoot + "examples/", "ScriptedScreens Fonts" });

        ScriptedScreensFontsPlugin.Log?.LogInfo($"registered {count + 1} fonts documentation resources and the examples");
    }

    /// <summary>The live font list, read on every request.</summary>
    private static string Available()
    {
        var fonts = FontRegistry.Snapshot();
        var text = new StringBuilder("# Fonts available now\n\n");
        text.Append("Use a name exactly as written, case included: `<font=\"Name\">` inside a label's `text`.\n");
        text.Append("A font file's glyph coverage is its own: a character it lacks does not render.\n\n");

        if (fonts.Count == 0)
            text.Append("Nothing registered yet. Game fonts appear once a world is loading; font files once TextMeshPro's shaders are up.\n");

        foreach (var font in fonts.OrderBy(f => f.Value.StartsWith("game", StringComparison.Ordinal)).ThenBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
            text.Append("- `").Append(font.Key).Append("` -- ").Append(font.Value).Append('\n');

        return text.ToString();
    }

    private static string Read(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            return $"Could not read {path}: {ex.Message}";
        }
    }

    /// <summary>
    /// The sections of a markdown file: text before the first <c>##</c> ("introduction"), then
    /// one per <c>##</c> heading outside code fences, each with its heading line, label and slug.
    /// </summary>
    private static List<(string Line, string Label, string Slug)> Sections(string path)
    {
        var found = new List<(string, string, string)>();
        var used = new HashSet<string>(StringComparer.Ordinal) { "introduction" };
        var fence = false;

        foreach (var line in Read(path).Split('\n'))
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
                fence = !fence;

            if (fence || !line.StartsWith("## ", StringComparison.Ordinal))
            {
                if (found.Count == 0 && line.Trim().Length > 0)
                    found.Add(("", "Introduction", "introduction"));
                continue;
            }

            var label = line.Substring(3).Trim();
            var slug = new StringBuilder();
            foreach (var c in label)
            {
                if (char.IsLetterOrDigit(c))
                    slug.Append(char.ToLowerInvariant(c));
                else if (slug.Length > 0 && slug[slug.Length - 1] != '-')
                    slug.Append('-');
            }

            var baseSlug = slug.ToString().TrimEnd('-');
            var unique = baseSlug.Length == 0 ? "section" : baseSlug;
            for (var n = 2; !used.Add(unique); n++)
                unique = $"{baseSlug}-{n}";

            found.Add((line, label, unique));
        }

        return found;
    }

    /// <summary>The section starting at heading <paramref name="line"/> ("" for the introduction), up to the next <c>##</c>.</summary>
    private static string Section(string path, string line)
    {
        var text = new StringBuilder();
        var fence = false;
        var inside = line.Length == 0;

        foreach (var current in Read(path).Split('\n'))
        {
            if (current.StartsWith("```", StringComparison.Ordinal))
                fence = !fence;

            if (!fence && current.StartsWith("## ", StringComparison.Ordinal))
            {
                if (inside)
                    break;
                inside = current == line;
            }

            if (inside)
                text.Append(current).Append('\n');
        }

        return text.Length > 0 ? text.ToString() : $"That section is no longer in {Path.GetFileName(path)}.";
    }
}
