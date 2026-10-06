using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace ScriptedScreensFonts;

/// <summary>
/// Builds TMP font assets from .ttf/.otf files dropped in the mod's <c>Assets/fonts</c>
/// folder, so a font the game does not ship can be used as <c>&lt;font="Family"&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// TMP's own <c>TMP_FontAsset.CreateFontAsset</c> cannot do this. Every glyph load in this
/// TMP version routes through <c>FontEngine.LoadFontFace(Font, ...)</c>, which needs a
/// <see cref="Font"/> carrying embedded font data. A runtime <c>Font</c> has none — an OS
/// font's data lives in the OS, and there is no runtime way to build a <c>Font</c> from a
/// file — so the call fails with "Make sure Include Font Data is enabled". There is no
/// <c>DynamicOS</c> atlas mode in this TMP version to fall back on either.
/// </para>
/// <para>
/// <c>FontEngine.LoadFontFace(byte[])</c> has no such restriction, so the face is read
/// straight from the file and the asset assembled by hand, mirroring what
/// <c>TMP_FontAsset.TryAddCharacters</c> does. The consequence is a <b>static</b> atlas:
/// glyphs are rendered once at load and nothing can be added afterwards, because "afterwards"
/// would need the <c>Font</c> we do not have. Hence a fixed character set.
/// </para>
/// </remarks>
internal static class FontLoader
{
    /// <summary>Folder scanned for font files, relative to the mod's own directory.</summary>
    internal const string FontsFolder = "Assets/fonts";

    // 48 rather than TMP's default 90: the whole character set has to fit one atlas, since a
    // static atlas cannot spill into a second one. At 90pt a 1024x1024 atlas runs out at
    // roughly 100 characters and the rest would silently not render.
    private const int SamplingPointSize = 48;
    private const int AtlasPadding = 5;
    private const int AtlasSize = 1024;

    /// <summary>
    /// Symbols a console readout reaches for that Latin-1 does not carry. Written as
    /// codepoints rather than literals so the set does not depend on how this source file
    /// is encoded.
    /// </summary>
    /// <remarks>
    /// A font that lacks one of these simply does not get it — <c>TryGetGlyphIndex</c>
    /// returns 0 and the character is skipped — so an absent glyph costs nothing. Barlow,
    /// for instance, has the minus sign and both dashes but no arrows at all. Baking a
    /// broad list therefore cannot make a font render something it does not contain; it
    /// only spares the author from listing symbols in <c>ExtraCharacters</c> for the fonts
    /// that do.
    /// </remarks>
    private static readonly uint[] DefaultExtras = BuildDefaultExtras();

    private static uint[] BuildDefaultExtras()
    {
        var extras = new List<uint>
        {
            // Punctuation typography expects
            0x2013, 0x2014,                 // en dash, em dash
            0x2018, 0x2019, 0x201C, 0x201D, // curly quotes
            0x2026, 0x2022,                 // ellipsis, bullet

            // Maths and units
            0x2212,                         // true minus, width-matched to + unlike hyphen
            0x2248, 0x2260, 0x2264, 0x2265, // approx, not equal, <=, >=
            0x221E, 0x221A, 0x2211, 0x220F, // infinity, root, sigma, product
            0x2206, 0x0394, 0x2207, 0x03B4, // increment, Delta, nabla, delta
            0x03C0, 0x03A9,                 // pi, Omega

            // Arrows, including the double forms used for state transitions
            0x2190, 0x2191, 0x2192, 0x2193,
            0x2194, 0x2195,
            0x21D0, 0x21D1, 0x21D2, 0x21D3,

            // Geometric shapes: status dots, gauge needles, sort indicators
            0x25B2, 0x25BC, 0x25C0, 0x25B6,
            0x25CF, 0x25CB, 0x25C6, 0x25C7,
            0x25A0, 0x25A1, 0x2605, 0x2606,

            // Status marks
            0x2713, 0x2717, 0x26A0,
        };

        // Block elements. 2581..2588 are the eighth-height bars a text sparkline is built
        // from; 2591..2593 are the shades used for fill and disabled states.
        for (uint c = 0x2581; c <= 0x2588; c++)
            extras.Add(c);

        for (uint c = 0x2591; c <= 0x2593; c++)
            extras.Add(c);

        // Box drawing, single and double. Cheap to include and the only way to draw a
        // frame in a label rather than in the vector layer.
        extras.AddRange(new uint[]
        {
            0x2500, 0x2502, 0x250C, 0x2510, 0x2514, 0x2518,
            0x251C, 0x2524, 0x252C, 0x2534, 0x253C,
            0x2550, 0x2551, 0x2554, 0x2557, 0x255A, 0x255D,
            0x2560, 0x2563, 0x2566, 0x2569, 0x256C,
        });

        return extras.ToArray();
    }

    private static bool _engineReady;
    private static bool _kerningChecked;

    private static bool _kerningLogged;

    /// <summary>Whether to load each font's letter-pair spacing; the <c>Kerning</c> setting.</summary>
    internal static bool Kerning = true;
    private static List<uint>? _charset;

    /// <summary>File (full path) to the name it is available under, for every file loaded.</summary>
    private static readonly Dictionary<string, string> LoadedByPath = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Files switched off in the config; a runtime request does not switch them back on.</summary>
    private static readonly HashSet<string> Disabled = new(StringComparer.OrdinalIgnoreCase);

    internal static bool IsDisabled(string path) => Disabled.Contains(Path.GetFullPath(path));

    /// <summary>True until the launch's font files have been built; a late load waits for it.</summary>
    internal static bool StartupPending => _files != null;

    /// <summary>The name a loaded file is available under, or null if it has not been loaded.</summary>
    internal static string? LoadedName(string path) =>
        LoadedByPath.TryGetValue(Path.GetFullPath(path), out var name) ? name : null;

    /// <summary>
    /// Builds a font from a file now, after startup. Main thread only, and only once TMP's shaders
    /// exist (<see cref="ShaderReady"/>).
    /// </summary>
    internal static string? LoadNow(string path)
    {
        var known = LoadedName(path);
        if (known != null)
            return known;

        try
        {
            var name = Load(path, _charset ??= BuildCharacterSet(_extraCharacters));

            // A font requested at runtime may be a family's missing Bold, so re-link.
            LinkWeights();
            return name;
        }
        catch (Exception ex)
        {
            ScriptedScreensFontsPlugin.Log?.LogWarning($"Could not load \"{Path.GetFileName(path)}\": {ex}");
            return null;
        }
    }

    internal static bool ShaderReady => ShaderUtilities.ShaderRef_MobileSDF != null;
    private static List<string>? _files;
    private static string _extraCharacters = string.Empty;

    /// <summary>
    /// Finds the font files and binds one on/off toggle per file, to be loaded later by
    /// <see cref="TryLoadPending"/>.
    /// </summary>
    /// <remarks>
    /// Bound here, at mod load, rather than when the atlases are built: LaunchPad's settings
    /// UI reads <c>ModBehaviour.Config</c>, and an entry that appears minutes later is not
    /// guaranteed to show. A disabled file is never built,
    /// so it costs no atlas memory; toggling needs a restart like adding a file does.
    /// </remarks>
    internal static void Configure(string modDirectory, string extraCharacters, ConfigFile config)
    {
        _extraCharacters = extraCharacters ?? string.Empty;
        _files = new List<string>();

        // The player's folder first, so a player's font can take over a bundled font's name.
        // It lives outside the mod on purpose: a Workshop update replaces the mod folder, and
        // fonts dropped in there would be lost with it.
        var userFolder = UserFontsFolder();
        if (userFolder != null)
            Scan(userFolder, "Your fonts: ", config, create: true);

        Scan(Path.Combine(modDirectory, FontsFolder), "Font files: ", config, create: false);
    }

    /// <summary>
    /// <c>fonts</c> in the game's save folder (<c>Documents/My Games/Stationeers</c>, or the
    /// path the game settings or LaunchPad override it with).
    /// </summary>
    /// <remarks>
    /// Read from the game's own setting, never through <c>StationeersLaunchPad.dll</c>:
    /// LaunchPad flags any mod that references it as unsupported and shows a popup. LaunchPad
    /// writes its save-path override into this same setting at startup.
    /// </remarks>
    /// <summary>
    /// A file downloaded on this launch. It was not on disk when <see cref="Configure"/> scanned,
    /// so it loads without a config switch this time; the next launch finds it and binds one.
    /// </summary>
    internal static void AddDownloaded(string path)
    {
        if (_files != null && !_files.Exists(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase)))
            _files.Add(path);
    }

    internal static string? UserFontsFolder()
    {
        try
        {
            var root = Assets.Scripts.Serialization.Settings.CurrentData?.SavePath;
            if (string.IsNullOrEmpty(root))
                root = StationSaveUtils.DefaultPath;

            return Path.Combine(root, "fonts");
        }
        catch (Exception ex)
        {
            ScriptedScreensFontsPlugin.Log?.LogWarning($"Could not resolve the game's save folder; only bundled fonts load: {ex.Message}");
            return null;
        }
    }

    private static void Scan(string folder, string sectionPrefix, ConfigFile config, bool create)
    {
        try
        {
            if (!Directory.Exists(folder))
            {
                if (!create)
                    return;

                // Created so a player can find it; empty is fine.
                Directory.CreateDirectory(folder);
                ScriptedScreensFontsPlugin.Log?.LogInfo($"Created \"{folder}\"; put your .ttf and .otf files there.");
                return;
            }

            // Recursive: organising fonts into per-project subfolders is the obvious thing to
            // do, and a skipped subfolder looks identical to a font that failed to load.
            var found = new List<string>();
            found.AddRange(Directory.GetFiles(folder, "*.ttf", SearchOption.AllDirectories));
            found.AddRange(Directory.GetFiles(folder, "*.otf", SearchOption.AllDirectories));
            found.Sort(StringComparer.OrdinalIgnoreCase);
            ScriptedScreensFontsPlugin.Log?.LogInfo($"{found.Count} font file(s) in \"{folder}\".");

            foreach (var path in found)
            {
                var relative = path.Substring(folder.Length).Replace(Path.DirectorySeparatorChar, '/').TrimStart('/');
                var enabled = config.Bind(
                    ConfigSection(sectionPrefix, relative),
                    ConfigName(Path.GetFileName(relative)),
                    true,
                    "Load this font file. Takes effect after a restart; a disabled file costs no memory.");

                if (enabled.Value)
                {
                    _files!.Add(path);
                }
                else
                {
                    Disabled.Add(Path.GetFullPath(path));
                    ScriptedScreensFontsPlugin.Log?.LogInfo($"Font file disabled in config: {relative}");
                }
            }
        }
        catch (Exception ex)
        {
            ScriptedScreensFontsPlugin.Log?.LogWarning($"Could not read the font folder \"{folder}\": {ex.Message}");
        }
    }

    /// <summary>
    /// One config section per folder, subfolder and family, so a family's weights sit together.
    /// The family is the file name up to its first <c>-</c> (<c>Barlow-Bold.ttf</c> is
    /// <c>Barlow</c>), the Google Fonts convention; the font's own family name is only known
    /// once the engine has read the file, which is later than LaunchPad reads the config.
    /// </summary>
    private static string ConfigSection(string prefix, string relative)
    {
        var slash = relative.LastIndexOf('/');
        var folder = slash < 0 ? string.Empty : relative.Substring(0, slash + 1);
        var stem = Path.GetFileNameWithoutExtension(relative);
        var dash = stem.IndexOf('-', StringComparison.Ordinal);
        var family = dash > 0 ? stem.Substring(0, dash) : stem;

        // Manrope/Manrope-Bold.ttf reads "Manrope", not "Manrope/Manrope".
        if (folder.TrimEnd('/').Split('/').Last().Equals(family, StringComparison.OrdinalIgnoreCase))
            return ConfigName(prefix + folder.TrimEnd('/'));

        return ConfigName(prefix + folder + family);
    }

    /// <summary>BepInEx rejects these characters in section and key names.</summary>
    private static readonly char[] InvalidConfigChars = { '=', '\n', '\t', '\\', '"', '\'', '[', ']' };

    private static string ConfigName(string name)
    {
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(InvalidConfigChars, chars[i]) >= 0)
                chars[i] = '_';
        }

        return new string(chars);
    }

    /// <summary>
    /// Builds the enabled fonts once TMP's shaders are reachable.
    /// </summary>
    /// <remarks>
    /// Not done at mod load. <c>ShaderUtilities.ShaderRef_MobileSDF</c> resolves through
    /// <c>Shader.Find</c>, which returns null until TMP's own resources are loaded, and
    /// <c>new Material(null)</c> throws. Retrying on a null shader rather than deferring by
    /// a fixed delay means the load happens as soon as it can, and keeps trying if the game
    /// takes its time.
    /// </remarks>
    internal static void TryLoadPending()
    {
        // Downloads first: a font fetched on this launch is loaded on this launch.
        if (_files == null || FontDownloader.Busy || ShaderUtilities.ShaderRef_MobileSDF == null)
            return;

        var files = _files;
        _files = null;

        var charset = _charset ??= BuildCharacterSet(_extraCharacters);
        foreach (var file in files)
        {
            try
            {
                Load(file, charset);
            }
            catch (Exception ex)
            {
                ScriptedScreensFontsPlugin.Log?.LogWarning($"Could not load \"{Path.GetFileName(file)}\": {ex}");
            }
        }

        LinkWeights();
    }

    /// <summary>
    /// Printable ASCII, the Latin-1 supplement (accented European text plus degree,
    /// plus-minus, micro and superscripts), and <see cref="DefaultExtras"/>.
    /// </summary>
    private static List<uint> BuildCharacterSet(string extraCharacters)
    {
        var set = new HashSet<uint>();
        var ordered = new List<uint>();

        void Add(uint c)
        {
            if (set.Add(c))
                ordered.Add(c);
        }

        for (uint c = 0x20; c <= 0x7E; c++)
            Add(c);

        for (uint c = 0xA0; c <= 0xFF; c++)
            Add(c);

        foreach (var c in DefaultExtras)
            Add(c);

        foreach (var c in extraCharacters ?? string.Empty)
        {
            if (!char.IsControl(c))
                Add(c);
        }

        return ordered;
    }

    /// <returns>The name the font is available under, or null when it could not be loaded.</returns>
    private static string? Load(string path, List<uint> charset)
    {
        if (!_engineReady)
        {
            if (FontEngine.InitializeFontEngine() != FontEngineError.Success)
            {
                ScriptedScreensFontsPlugin.Log?.LogWarning("Could not initialise the font engine.");
                return null;
            }

            _engineReady = true;
        }

        var file = Path.GetFileName(path);

        var sourceBytes = File.ReadAllBytes(path);

        if (FontEngine.LoadFontFace(sourceBytes, SamplingPointSize) != FontEngineError.Success)
        {
            ScriptedScreensFontsPlugin.Log?.LogWarning($"\"{file}\" is not a font file the engine can read.");
            return null;
        }

        var faceInfo = FontEngine.GetFaceInfo();
        var fontName = ComposeName(faceInfo);

        if (!FontRegistry.Claim(fontName))
        {
            // The name still resolves, to whichever file claimed it first.
            ScriptedScreensFontsPlugin.Log?.LogWarning($"\"{file}\" declares the name \"{fontName}\", which is already registered; skipping.");
            LoadedByPath[Path.GetFullPath(path)] = fontName;
            return fontName;
        }

        var asset = ScriptableObject.CreateInstance<TMP_FontAsset>();

        // Load bearing. ReadFontAssetDefinition treats a version-less asset as pre-1.1 and
        // runs UpgradeFontAsset, which dereferences legacy fields a fresh asset never had.
        asset.version = "1.1.0";

        asset.name = fontName;
        asset.hideFlags = HideFlags.DontUnloadUnusedAsset;
        asset.faceInfo = faceInfo;
        asset.atlasPopulationMode = AtlasPopulationMode.Static;
        asset.atlasWidth = AtlasSize;
        asset.atlasHeight = AtlasSize;
        asset.atlasPadding = AtlasPadding;
        asset.atlasRenderMode = GlyphRenderMode.SDFAA;

        var texture = new Texture2D(AtlasSize, AtlasSize, TextureFormat.Alpha8, mipChain: false)
        {
            name = fontName + " Atlas",
            hideFlags = HideFlags.DontUnloadUnusedAsset,
        };

        FontEngine.ResetAtlasTexture(texture);
        asset.atlasTextures = new[] { texture };

        // Mobile SDF rather than the desktop shader on purpose: it is the variant that
        // declares _CullMode, which TextMeshProUGUI writes on every canvas update. The
        // desktop shader is why so many of the game's own fonts spam errors in a UI label.
        var material = new Material(ShaderUtilities.ShaderRef_MobileSDF)
        {
            name = fontName + " Material",
            hideFlags = HideFlags.DontUnloadUnusedAsset,
        };

        material.SetTexture(ShaderUtilities.ID_MainTex, texture);
        material.SetFloat(ShaderUtilities.ID_TextureWidth, AtlasSize);
        material.SetFloat(ShaderUtilities.ID_TextureHeight, AtlasSize);
        material.SetFloat(ShaderUtilities.ID_GradientScale, AtlasPadding + 1);
        material.SetFloat(ShaderUtilities.ID_WeightNormal, asset.normalStyle);
        material.SetFloat(ShaderUtilities.ID_WeightBold, asset.boldStyle);
        asset.material = material;

        var freeRects = new List<GlyphRect> { new(0, 0, AtlasSize - 1, AtlasSize - 1) };
        var usedRects = new List<GlyphRect>();
        asset.freeGlyphRects = freeRects;
        asset.usedGlyphRects = usedRects;

        // One glyph can back several characters, so indices are de-duplicated before
        // rendering and the characters mapped back onto them afterwards.
        var glyphIndexes = new List<uint>();
        var seenIndexes = new HashSet<uint>();
        var wanted = new List<KeyValuePair<uint, uint>>();

        foreach (var unicode in charset)
        {
            if (!FontEngine.TryGetGlyphIndex(unicode, out var glyphIndex) || glyphIndex == 0)
                continue;

            wanted.Add(new KeyValuePair<uint, uint>(unicode, glyphIndex));
            if (seenIndexes.Add(glyphIndex))
                glyphIndexes.Add(glyphIndex);
        }

        if (glyphIndexes.Count == 0)
        {
            FontRegistry.Release(fontName);
            ScriptedScreensFontsPlugin.Log?.LogWarning($"\"{file}\" has none of the requested characters.");
            return null;
        }

        var complete = FontEngine.TryAddGlyphsToTexture(
            glyphIndexes, AtlasPadding, GlyphPackingMode.BestShortSideFit,
            freeRects, usedRects, GlyphRenderMode.SDFAA, texture, out var glyphs);

        var glyphTable = asset.glyphTable;
        var byIndex = new Dictionary<uint, Glyph>();

        foreach (var glyph in glyphs)
        {
            if (glyph == null)
                continue;

            glyph.atlasIndex = 0;
            glyphTable.Add(glyph);
            byIndex[glyph.index] = glyph;
        }

        var characterTable = asset.characterTable;
        foreach (var pair in wanted)
        {
            if (byIndex.TryGetValue(pair.Value, out var glyph))
                characterTable.Add(new TMP_Character(pair.Key, asset, glyph));
        }

        texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        asset.ReadFontAssetDefinition();

        // Kerning: see LoadKerning. After ReadFontAssetDefinition, which creates the lookup
        // dictionary the records go into, and before another font's face replaces this one in
        // the engine.
        var kerningPairs = Kerning ? LoadKerning(asset, glyphIndexes, UnitsPerEm(sourceBytes), file) : 0;

        MaterialReferenceManager.AddFontAsset(asset);
        if (Kerning)
            WarnIfKerningIsOff();

        RememberForWeightLinking(faceInfo, asset);

        ScriptedScreensFontsPlugin.Log?.LogInfo(
            $"Font available: <font=\"{fontName}\"> ({characterTable.Count} characters, {kerningPairs} kerning pairs from {file})");
        FontRegistry.Record(fontName, $"font file {file}, {characterTable.Count} characters, {kerningPairs} kerning pairs"
            + (complete ? string.Empty : ", atlas full so some characters are missing"));

        if (!complete)
        {
            // A static atlas cannot spill into a second texture, so the overflow is simply
            // absent. Say so rather than leaving blank glyphs to be found in game.
            ScriptedScreensFontsPlugin.Log?.LogWarning(
                $"\"{fontName}\" did not fit {AtlasSize}x{AtlasSize}; some characters are missing.");
        }

        LoadedByPath[Path.GetFullPath(path)] = fontName;
        return fontName;
    }

    /// <summary>
    /// Fills the asset's feature table with the font's letter-pair spacing, so TMP draws "AV" and
    /// "To" tucked together the way a browser does rather than evenly spaced.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not <c>TMP_FontAsset.UpdateGlyphAdjustmentRecords</c>, which is what TMP itself calls: its
    /// engine overload reads the **legacy `kern` table**, and a modern font does not have one.
    /// Barlow keeps its kerning only in GPOS, so that route returned 0 pairs for all 36 faces.
    /// </para>
    /// <para>
    /// The engine can read GPOS, through a different overload: walk the GPOS feature list, take
    /// every feature tagged <c>kern</c>, and ask for the pair records of each of its lookups. The
    /// native side does the parsing, so both PairPos formats are covered without a parser here.
    /// No script or language filtering: all <c>kern</c> features in the table are loaded, which
    /// for a Latin font is the one the default script uses anyway.
    /// </para>
    /// <para>
    /// The records are keyed exactly as TMP keys them (second glyph in the high 16 bits), because
    /// <c>TMP_Text</c> looks them up by that key while it lays out a line.
    /// </para>
    /// </remarks>
    private static int LoadKerning(TMP_FontAsset asset, List<uint> glyphIndexes, int unitsPerEm, string file)
    {
        if (unitsPerEm <= 0)
        {
            ScriptedScreensFontsPlugin.Log?.LogWarning(
                $"Could not read the em size of \"{file}\", so its kerning is left out.");
            return 0;
        }

        var table = FontEngine.GetOpenTypeLayoutTable(OTL_TableType.GPOS);
        if (table.features == null)
            return 0;

        // GPOS values are in the font's own design units; the glyph metrics TMP adds them to are
        // in pixels at the sampling size. Unscaled, a -48 unit pair moved the text back further
        // than the glyph was wide and every row collapsed into itself.
        var scale = (float)SamplingPointSize / unitsPerEm;

        var features = asset.fontFeatureTable;
        var lookup = features.m_GlyphPairAdjustmentRecordLookupDictionary;
        var added = 0;
        var widest = 0f;

        foreach (var feature in table.features)
        {
            if (!string.Equals(feature.tag, "kern", StringComparison.Ordinal) || feature.lookupIndexes == null)
                continue;

            foreach (var index in feature.lookupIndexes)
            {
                var records = FontEngine.GetPairAdjustmentRecords((int)index, glyphIndexes);
                if (records == null)
                    continue;

                foreach (var record in records)
                {
                    // The engine returns a shared buffer closed by a zero record.
                    if (record.firstAdjustmentRecord.glyphIndex == 0)
                        break;

                    var key = (record.secondAdjustmentRecord.glyphIndex << 16) | record.firstAdjustmentRecord.glyphIndex;
                    if (lookup.ContainsKey(key))
                        continue;

                    var raw = Math.Abs(record.firstAdjustmentRecord.glyphValueRecord.xAdvance);
                    if (raw > widest)
                        widest = raw;

                    var pair = new TMP_GlyphPairAdjustmentRecord(
                        Scaled(record.firstAdjustmentRecord, scale),
                        Scaled(record.secondAdjustmentRecord, scale));
                    features.glyphPairAdjustmentRecords.Add(pair);
                    lookup.Add(key, pair);
                    added++;
                }
            }
        }

        // Once a session: the numbers behind the scale, so a wrong unit is visible in the log
        // rather than only on a console. A Latin text face kerns by a few per cent of an em.
        if (added > 0 && !_kerningLogged)
        {
            _kerningLogged = true;
            ScriptedScreensFontsPlugin.Log?.LogInfo(
                $"Kerning: {file} measures {unitsPerEm} units per em, widest pair {widest} units"
                + $" = {widest * scale:0.##} px at {SamplingPointSize} pt.");
        }

        return added;
    }

    /// <summary>Converts one adjustment record from design units to pixels at the sampling size.</summary>
    private static TMP_GlyphAdjustmentRecord Scaled(GlyphAdjustmentRecord record, float scale)
    {
        var value = record.glyphValueRecord;
        return new TMP_GlyphAdjustmentRecord(
            record.glyphIndex,
            new TMP_GlyphValueRecord(
                value.xPlacement * scale,
                value.yPlacement * scale,
                value.xAdvance * scale,
                value.yAdvance * scale));
    }

    /// <summary>
    /// Reads <c>head.unitsPerEm</c> out of the font file, which is the unit GPOS values are in.
    /// The engine does not expose it (<c>FaceInfo</c> has no such field), and it is not always
    /// 1000: TrueType outlines usually use 2048. Returns 0 if the file cannot be walked.
    /// </summary>
    private static int UnitsPerEm(byte[] font)
    {
        try
        {
            var directory = 0;

            // A font collection: take the first face's table directory.
            if (font[0] == (byte)'t' && font[1] == (byte)'t' && font[2] == (byte)'c' && font[3] == (byte)'f')
                directory = (int)ReadUInt32(font, 12);

            var tables = ReadUInt16(font, directory + 4);
            for (var i = 0; i < tables; i++)
            {
                var record = directory + 12 + (i * 16);
                if (font[record] != (byte)'h' || font[record + 1] != (byte)'e'
                    || font[record + 2] != (byte)'a' || font[record + 3] != (byte)'d')
                    continue;

                return ReadUInt16(font, (int)ReadUInt32(font, record + 8) + 18);
            }
        }
        catch (Exception ex)
        {
            ScriptedScreensFontsPlugin.Log?.LogWarning($"Could not read the font's em size: {ex}");
        }

        return 0;
    }

    private static int ReadUInt16(byte[] data, int offset) => (data[offset] << 8) | data[offset + 1];

    private static uint ReadUInt32(byte[] data, int offset) =>
        ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16)
        | ((uint)data[offset + 2] << 8) | data[offset + 3];

    /// <summary>
    /// Says so once when the game has kerning switched off, since the pairs are then read and
    /// stored for nothing: <c>TMP_Text</c> only consults them when <c>enableKerning</c> is set,
    /// and it takes that from the game's own TMP settings.
    /// </summary>
    private static void WarnIfKerningIsOff()
    {
        if (_kerningChecked)
            return;

        _kerningChecked = true;
        try
        {
            if (!TMP_Settings.enableKerning)
                ScriptedScreensFontsPlugin.Log?.LogInfo("Kerning is off in the game's TextMeshPro settings, so the pairs loaded here are not applied when text is drawn.");
        }
        catch (Exception ex)
        {
            ScriptedScreensFontsPlugin.Log?.LogInfo($"Could not read the TextMeshPro kerning setting: {ex.Message}");
        }
    }

    /// <summary>
    /// Names the asset the way an author would write it: family alone for the regular
    /// <summary>Every loaded face of one family, by family name, for <see cref="LinkWeights"/>.</summary>
    private static readonly Dictionary<string, List<(int Weight, bool Italic, TMP_FontAsset Asset)>> Families =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Files the face under its family so its siblings can be linked to it once they are all
    /// built. Nothing can be linked while loading, because a family's Bold may load after its
    /// Regular.
    /// </summary>
    private static void RememberForWeightLinking(FaceInfo faceInfo, TMP_FontAsset asset)
    {
        var family = faceInfo.familyName;
        if (string.IsNullOrEmpty(family))
            return;

        var weight = WeightIndex(faceInfo.styleName, out var italic);
        if (weight == 0)
            return;

        if (!Families.TryGetValue(family, out var faces))
            Families[family] = faces = new List<(int, bool, TMP_FontAsset)>();

        faces.Add((weight, italic, asset));
    }

    /// <summary>
    /// Reads a face's style name as a TextMeshPro weight slot, so "Bold Italic" becomes slot 7
    /// italic. Returns 0 for a style this does not recognise, which is then left out rather than
    /// guessed at: a wrong slot would make &lt;b&gt; draw the wrong face.
    /// </summary>
    private static int WeightIndex(string? styleName, out bool italic)
    {
        // Upper case and letters only, with "ITALIC" taken out, so "Bold Italic", "BoldItalic"
        // and "bold-italic" all reduce to "BOLD". Built by hand because the framework here has
        // no Replace/Contains overload taking a StringComparison.
        var style = (styleName ?? string.Empty).ToUpperInvariant();
        italic = false;

        var token = new StringBuilder(style.Length);
        for (var i = 0; i < style.Length; i++)
        {
            if (i + 6 <= style.Length && string.CompareOrdinal(style, i, "ITALIC", 0, 6) == 0)
            {
                italic = true;
                i += 5;
                continue;
            }

            if (style[i] >= 'A' && style[i] <= 'Z')
                token.Append(style[i]);
        }

        // The slots TMP_FontAssetUtilities indexes: Thin 1 .. Black 9, Regular 4.
        switch (token.ToString())
        {
            case "":
            case "REGULAR": return 4;
            case "THIN":
            case "HAIRLINE": return 1;
            case "EXTRALIGHT":
            case "ULTRALIGHT": return 2;
            case "LIGHT": return 3;
            case "MEDIUM": return 5;
            case "SEMIBOLD":
            case "DEMIBOLD": return 6;
            case "BOLD": return 7;
            case "EXTRABOLD":
            case "ULTRABOLD": return 8;
            case "BLACK":
            case "HEAVY": return 9;
            default: return 0;
        }
    }

    /// <summary>
    /// Points each face's weight table at its siblings, so &lt;b&gt;, &lt;i&gt; and
    /// &lt;font-weight&gt; inside a label draw the family's real Bold, Italic and Light instead of
    /// TextMeshPro slanting and smearing the one face it was given.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>TMP_FontAssetUtilities.GetCharacterFromFontAsset_Internal</c> reads
    /// <c>sourceFontAsset.fontWeightTable</c> whenever the style is italic or the weight is not
    /// Regular, and takes <c>italicTypeface</c> or <c>regularTypeface</c> from the slot. (The
    /// <c>TMP_Text.GetFontAssetForWeight</c> that indexes by <c>weight / 100</c> is dead code in
    /// this version and uses a different slot numbering; do not follow it.)
    /// </para>
    /// <para>
    /// Every face of the family gets the same table, so the tags work whichever member the label
    /// names. A weight the family does not ship stays null, which simply falls through to the
    /// face in use. Rebuilt from scratch each call, so loading a font later re-links cleanly.
    /// </para>
    /// </remarks>
    private static void LinkWeights()
    {
        var summary = new StringBuilder();

        foreach (var family in Families)
        {
            var table = new TMP_FontWeightPair[10];
            foreach (var (weight, italic, asset) in family.Value)
            {
                if (italic)
                    table[weight].italicTypeface = asset;
                else
                    table[weight].regularTypeface = asset;
            }

            foreach (var face in family.Value)
                face.Asset.fontWeightTable = table;

            var upright = table.Count(pair => pair.regularTypeface != null);
            var italics = table.Count(pair => pair.italicTypeface != null);
            if (summary.Length > 0)
                summary.Append(", ");

            summary.Append($"{family.Key} {upright}+{italics}i");
        }

        // The weights each family ended up with, so a style name this does not recognise shows up
        // as a missing slot in the log rather than as a tag silently drawing the wrong face.
        if (summary.Length > 0 && !string.Equals(_weightSummary, summary.ToString(), StringComparison.Ordinal))
        {
            _weightSummary = summary.ToString();
            ScriptedScreensFontsPlugin.Log?.LogInfo($"Weights linked: {_weightSummary}.");
        }
    }

    private static string? _weightSummary;

    /// weight, family plus style otherwise, so Barlow-Bold.ttf becomes "Barlow Bold".
    /// </summary>
    /// <remarks>
    /// A contract, not a detail: ScriptedScreens Html looks registered faces up by this name
    /// (case, spaces and dashes ignored) and maps <c>@font-face url(Family-Style.ttf)</c> onto
    /// it. Changing the format breaks every page that names a font.
    /// </remarks>
    private static string ComposeName(FaceInfo faceInfo)
    {
        var family = faceInfo.familyName;
        var style = faceInfo.styleName;

        if (string.IsNullOrEmpty(family))
            return string.IsNullOrEmpty(style) ? "Unnamed" : style;

        if (string.IsNullOrEmpty(style) || style.Equals("Regular", StringComparison.OrdinalIgnoreCase))
            return family;

        return family + " " + style;
    }
}
