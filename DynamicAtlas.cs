using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ScriptedScreensFonts;

/// <summary>
/// Lets a font built from a file grow on demand, so every character the file contains can be
/// drawn rather than a set chosen at load.
/// </summary>
/// <remarks>
/// <para>
/// TextMeshPro already does all of this: in <c>AtlasPopulationMode.Dynamic</c> it rasterises a
/// glyph the first time something draws it, spills into further atlas textures when one fills,
/// and keeps its own tables straight. Exactly one line stands in the way.
/// <c>TMP_FontAsset.TryAddCharacterInternal</c> begins by re-opening the face with
/// <c>FontEngine.LoadFontFace(sourceFontFile, pointSize)</c> — a Unity <c>Font</c> object, which
/// a face built from a `.ttf` on disk does not have and cannot be given: no API builds a
/// <c>Font</c> from bytes or a path.
/// </para>
/// <para>
/// So that one call is redirected. A prefix on <c>TryAddCharacterInternal</c> notes which of our
/// assets is being grown, and a prefix on <c>FontEngine.LoadFontFace(Font, int)</c> sees that note
/// and loads the face from the file's bytes instead. Everything after that line in TMP's method is
/// already generic and needs no help. No IL is rewritten, so this survives anything but a change
/// to those two signatures.
/// </para>
/// <para>
/// The engine patch is global, but does nothing unless the note is set, which happens only inside
/// our own assets' growth. The game's fonts are untouched.
/// </para>
/// </remarks>
internal static class DynamicAtlas
{
    private sealed class Source
    {
        public string Path = string.Empty;
        public int FaceIndex;
    }

    private static readonly Dictionary<TMP_FontAsset, Source> Sources = new();

    /// <summary>The asset currently being grown, and so the face the engine should be holding.</summary>
    private static Source? _growing;

    /// <summary>
    /// The last file read, kept because TMP re-opens the face once per glyph: a page drawing
    /// ninety new characters would otherwise read the same few hundred kilobytes ninety times.
    /// One entry is enough, since the glyphs of one frame are nearly always one face.
    /// </summary>
    private static string? _cachedPath;
    private static byte[]? _cachedBytes;

    private static bool _installed;

    /// <summary>Held for the session: the patches must outlive this call, so it is never disposed.</summary>
    private static Harmony? _harmony;

    /// <summary>Remembers where a built face came from, so it can be re-opened to grow.</summary>
    internal static void Register(TMP_FontAsset asset, string path, int faceIndex)
    {
        Sources[asset] = new Source { Path = path, FaceIndex = faceIndex };
    }

    /// <summary>Whether a face can grow; false means it is stuck with the glyphs it has.</summary>
    internal static bool Available => _installed;

    internal static void Install()
    {
        if (_installed)
            return;

        try
        {
            var grow = AccessTools.Method(typeof(TMP_FontAsset), "TryAddCharacterInternal");
            var load = AccessTools.Method(typeof(FontEngine), "LoadFontFace", new[] { typeof(Font), typeof(int) });
            if (grow == null || load == null)
            {
                ScriptedScreensFontsPlugin.Log?.LogWarning(
                    "Could not find the TextMeshPro methods that grow a font; loaded fonts will keep only the characters built at load.");
                return;
            }

            _harmony = new Harmony("gruffuss.stationeers.scriptedscreens.fonts");
            _harmony.Patch(grow,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(DynamicAtlas), nameof(BeforeGrow))),
                finalizer: new HarmonyMethod(AccessTools.Method(typeof(DynamicAtlas), nameof(AfterGrow))));
            _harmony.Patch(load,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(DynamicAtlas), nameof(BeforeLoadFace))));

            _installed = true;
            ScriptedScreensFontsPlugin.Log?.LogInfo("Fonts can grow on demand: every character a font file contains is available.");
        }
        catch (Exception ex)
        {
            ScriptedScreensFontsPlugin.Log?.LogWarning($"Could not enable on-demand characters: {ex}");
        }
    }

    private static void BeforeGrow(TMP_FontAsset __instance)
    {
        Sources.TryGetValue(__instance, out _growing);
    }

    // A finalizer rather than a postfix: it runs even when the method throws, so a fault inside
    // TMP cannot leave the note set and send an unrelated face's glyphs to our file.
    private static void AfterGrow()
    {
        _growing = null;
    }

    private static bool BeforeLoadFace(Font font, ref FontEngineError __result, int pointSize)
    {
        var source = _growing;
        if (source == null)
            return true;

        // Belt as well as braces: our own assets have no source Font, which is the whole reason
        // this redirect exists. A call that DOES carry one is somebody else's -- the game's own
        // dynamic fonts, or whatever draws the mod settings panel -- and must reach the real
        // method untouched. Without this, a note left set by any path I have not thought of
        // would feed our font's bytes to another caller, and the engine holds one face at a
        // time: that caller would then lay its text out with our glyph indices and metrics.
        if (font != null)
            return true;

        try
        {
            if (!string.Equals(_cachedPath, source.Path, StringComparison.Ordinal))
            {
                _cachedBytes = File.ReadAllBytes(source.Path);
                _cachedPath = source.Path;
            }

            __result = FontEngine.LoadFontFace(_cachedBytes, pointSize, source.FaceIndex);
        }
        catch (Exception ex)
        {
            // The file moved or is unreadable: report nothing loaded, exactly as a failed
            // original would, and let the character fall through to whatever draws it now.
            ScriptedScreensFontsPlugin.Log?.LogWarning($"Could not re-open \"{source.Path}\" to add a character: {ex.Message}");
            _cachedPath = null;
            _cachedBytes = null;
            __result = FontEngineError.Invalid_Face;
        }

        return false;
    }
}
