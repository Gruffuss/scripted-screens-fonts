using BepInEx.Logging;
using StationeersMods.Interface;
using UnityEngine;

namespace ScriptedScreensFonts;

/// <summary>
/// LaunchPad entrypoint. Client-side only: rendering happens on the client, and a headless
/// server has no TMP font assets to register.
/// </summary>
[StationeersMod(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION_CONST)]
public sealed class ScriptedScreensFontsPlugin : ModBehaviour
{
    private static bool _initialized;

    internal static ManualLogSource? Log { get; private set; }

    /// <inheritdoc />
    public override void OnLoaded(ContentHandler contentHandler)
    {
        base.OnLoaded(contentHandler);

        if (_initialized)
            return;

        _initialized = true;

        try
        {
            Log = new ManualLogSource(PluginInfo.PLUGIN_NAME);
            try
            {
                BepInEx.Logging.Logger.Sources.Add(Log);
            }
            catch
            {
                // Logger already disposed or source name taken; logging is non-essential.
            }

            // Which build is actually running, from the loaded assembly rather than a file date.
            // Without it, "is my build loaded?" can only be answered by comparing the deployed
            // DLL's timestamp against the game's process start time -- and on 2026-10-10 a
            // restart script that reported failure while the game was up meant several builds
            // were "tested" against a process older than the build itself.
            Log.LogInfo($"ScriptedScreens Fonts {PluginInfo.PLUGIN_VERSION_CONST} loaded.");

            if (Application.isBatchMode)
            {
                Log.LogInfo("Headless server detected, skipping font registration.");
                return;
            }

            // Config must bind into ModBehaviour.Config: that is the instance LaunchPad hands
            // to its settings UI. A private ConfigFile writes a valid .cfg nobody ever sees.
            FontLoader.Kerning = Config.Bind(
                "Fonts",
                "Kerning",
                true,
                "Space letter pairs as the font says they should be spaced (AV, To, Wa sit closer), "
                + "the way a browser draws them. Text becomes slightly narrower, usually 1-4%. Off "
                + "restores the wider, evenly spaced look. Takes effect after a restart, and only "
                + "for fonts loaded from files.").Value;

            var modDirectory = System.IO.Path.GetDirectoryName(typeof(ScriptedScreensFontsPlugin).Assembly.Location);
            if (!string.IsNullOrEmpty(modDirectory))
                FontLoader.Configure(modDirectory, Config);

            var fontUrls = Config.Bind(
                "Fonts",
                "FontUrls",
                "",
                "Fonts to download: links to .ttf or .otf files, or Google Fonts CSS links such as "
                + "https://fonts.googleapis.com/css2?family=Manrope:wght@400;700 , separated by spaces. "
                + "Each is downloaded once into the fonts/downloaded folder in your save folder and then "
                + "loads from there. Takes effect after a restart.");

            System.Collections.IEnumerator? download = null;
            var urls = FontDownloader.ParseUrls(fontUrls.Value);
            var userFolder = FontLoader.UserFontsFolder();
            if (urls.Count > 0 && userFolder != null)
            {
                FontDownloader.Prepare(urls);
                download = FontDownloader.Run(urls, System.IO.Path.Combine(userFolder, "downloaded"));
            }

            // Before any font is built: whether growth is available decides how each is built.
            DynamicAtlas.Install();

            FontApi.Configure(Config.Bind(
                "Fonts",
                "PageFontHosts",
                "fonts.googleapis.com fonts.gstatic.com",
                "Hosts a page (a ScriptedScreens Html @font-face, or any mod through the fonts API) may "
                + "download fonts from, separated by spaces; subdomains included. A font file is "
                + "read by native code, so list only hosts that serve fonts you trust: Google Fonts serves "
                + "only its own curated files, a general CDN serves anything anyone uploads. Empty refuses "
                + "every page download. Links in FontUrls are not limited by this. Applies at once."));

            FontRegistryLoader.Install(download);
            Log.LogInfo("Watching for game fonts to register with TextMeshPro.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError(ex);
        }
    }
}
