using System.Diagnostics;
using SteamCmdHelper.Games;
using SteamCmdHelper.Games.RimWorld;

namespace SteamCmdHelper;

/// <summary>
/// The installed copy of RimWorld that the Launch Pad's RimWorld tools (clean-colony test, play it live, the XML patch
/// check, the compatibility grid) run against: found through Steam, or the folder picked by hand.
/// </summary>
public sealed partial class MainForm
{
    readonly IGameModule _rwGame = new RimWorldModule();
    /// <summary>RimWorld's installed mods, once found; null until then.</summary>
    LibraryScan? _rwScan;
    /// <summary>The normal game's install (no instance folders put in); null until found.</summary>
    GameInstall? _rwUsual;
    bool _rwScanning;

    InstanceStore RimWorldInstances => new(Path.Combine(AppSettings.Folder, "rimworld", "instances"));

    /// <summary>Finds RimWorld and reads its installed mods. Leaves <see cref="_rwScan"/> null when it isn't found.</summary>
    async Task ScanRimWorldAsync()
    {
        if (_rwScanning) return;
        _rwScanning = true;
        try
        {
            _s.GameFolders.TryGetValue(_rwGame.AppId, out var gameFolder);
            _s.GameDataFolders.TryGetValue(_rwGame.AppId, out var dataFolder);
            var (usual, scan) = await Task.Run(() =>
            {
                var install = _rwGame.Detect(SteamLibraries.Find(), gameFolder, dataFolder);
                return install == null ? (null, null) : (install, ModLibrary.Scan(_rwGame, install));
            });
            _rwUsual = usual;
            _rwScan = scan;
            // Frozen copies are only linked in while their instance plays; take out any a crash left behind.
            if (usual != null && GameRunning() == null) Freezer.Unlink(Launcher.ModsFolder(usual));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Append("Could not read RimWorld's mods: " + e.Message, LogKind.Error);
        }
        finally { _rwScanning = false; }
    }

    /// <summary>Asks for RimWorld's install folder when Steam doesn't know it, then looks again.</summary>
    async Task<bool> PickRimWorldFolderAsync()
    {
        using var dlg = new FolderBrowserDialog { Description = "RimWorld was not found in your Steam libraries. Pick its install folder (the one with RimWorldWin64.exe).", UseDescriptionForTitle = true };
        if (dlg.ShowDialog(this) != DialogResult.OK) return false;
        _s.GameFolders[_rwGame.AppId] = dlg.SelectedPath;
        _s.Save();
        await ScanRimWorldAsync();
        return _rwScan != null;
    }

    /// <summary>The game's process when it is running, or null.</summary>
    string? GameRunning() =>
        _rwGame.ProcessNames.FirstOrDefault(n => { try { return Process.GetProcessesByName(n).Length > 0; } catch { return false; } });

    /// <summary>Mods (in load order) as the XML simulation takes them.</summary>
    static List<XmlMod> XmlMods(IEnumerable<ModManifest> mods) =>
        mods.Select(m => new XmlMod(m.Key, m.DisplayName, m.Id, m.Folder, m.Source == ModSource.Official)).ToList();

    /// <summary>The game's log, with the errors grouped and the mods most likely at fault.</summary>
    void OpenRimWorldLog(string path, bool watch)
    {
        if (_rwScan is not { } scan) return;
        ModIndex? index = null;
        var byId = scan.ById();
        var f = new LogForm(path,
            (p, ct) => LogAnalysis.Read(p, index ??= new ModIndex(scan.Mods, ModAssemblies.ForMods(scan.Mods, ct)), ct),
            key => byId.TryGetValue(key, out var m) ? m.DisplayName : key,
            watch,
            issue =>
            {
                if (issue.Mods.FirstOrDefault() is not { } b || !byId.TryGetValue(b.Mod, out var m)) return null;
                var body = BugReport.Text(issue, m, scan.Install.Version, scan.Active?.Count ?? 0);
                return (BugReport.Target(m, $"{issue.Title} (RimWorld {GameVersion.Short(scan.Install.Version ?? "")})", body), body);
            });
        f.Show(this);
    }
}
