using Xunit;

namespace SteamCmdHelper.Tests;

public sealed class PackageSizeTests
{
    static PackageFile F(string path, long bytes) => new(path, bytes, "");

    [Fact]
    public void Finds_what_players_rarely_need_and_where_the_bytes_are()
    {
        var files = new[] { F("Textures/a.png", 900), F("Textures/a.psd", 5000), F("Source/Mod.cs", 10), F("Source/Mod.csproj", 5), F("Assemblies/Mod.pdb", 50), F("About/About.xml", 1), F("Thumbs.db", 2) };
        var rare = PackageCheck.RarelyNeeded(files);
        Assert.Equal(new[] { "*.psd", "*.pdb", "Source", "Thumbs.db" }, rare.Select(r => r.Pattern));
        Assert.Equal((2, 15L), (rare[2].Files, rare[2].Bytes));
        var top = PackageCheck.ByTopFolder(files);
        Assert.Equal(("Textures/", 5900L), top[0]);
    }

    [Fact]
    public void Prune_removes_matching_files_and_folders()
    {
        var stage = Path.Combine(Path.GetTempPath(), "prune_" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var rel in new[] { "Textures/a.png", "Textures/a.psd", "Source/x.cs", "About/About.xml" })
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(stage, rel))!);
                File.WriteAllText(Path.Combine(stage, rel), "x");
            }
            Publisher.Prune(stage, new[] { "*.psd", "Source" });
            Assert.True(File.Exists(Path.Combine(stage, "Textures", "a.png")));
            Assert.False(File.Exists(Path.Combine(stage, "Textures", "a.psd")));
            Assert.False(Directory.Exists(Path.Combine(stage, "Source")));
        }
        finally { try { Directory.Delete(stage, true); } catch { } }
    }

    [Fact]
    public void A_built_in_layout_keeps_its_leave_out_list()
    {
        var repo = Path.Combine(Path.GetTempPath(), "leave_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repo);
        try
        {
            var cfg = ProjectConfig.GetPreset("RimWorld");
            cfg.LeaveOut = new[] { "*.psd" };
            cfg.Save(repo);
            var again = ProjectConfig.Load(repo);
            Assert.Equal(new[] { "*.psd" }, again.LeaveOut);
            Assert.Equal("RimWorld", again.PresetName());
        }
        finally { try { Directory.Delete(repo, true); } catch { } }
    }
}

public sealed class SettingsBackupsTests : IDisposable
{
    readonly string _data = Path.Combine(Path.GetTempPath(), "sb_" + Guid.NewGuid().ToString("N"));
    string Dir => Path.Combine(_data, "backups", "daily");

    public void Dispose()
    {
        try { Directory.Delete(_data, true); } catch { }
    }

    [Fact]
    public void Backs_up_once_a_day_and_keeps_14()
    {
        Assert.Null(SettingsBackups.MakeIfDue(_data, Dir)); // no settings yet
        File.WriteAllText(Path.Combine(_data, "settings.json"), "{}");
        File.WriteAllText(Path.Combine(_data, "history.json"), "[]");
        var start = new DateTime(2026, 10, 1, 9, 0, 0);
        var first = SettingsBackups.MakeIfDue(_data, Dir, start)!;
        using (var zip = System.IO.Compression.ZipFile.OpenRead(first))
        {
            var names = zip.Entries.Select(e => e.FullName).ToList();
            Assert.Contains("settings.json", names);
            Assert.Contains("history.json", names);
        }
        Assert.Null(SettingsBackups.MakeIfDue(_data, Dir, start.AddHours(5)));
        for (var d = 1; d <= 20; d++) Assert.NotNull(SettingsBackups.MakeIfDue(_data, Dir, start.AddDays(d)));
        var list = SettingsBackups.List(Dir);
        Assert.Equal(14, list.Count);
        Assert.Equal(start.AddDays(20), list[0].Made);
    }

    [Fact]
    public void Restoring_puts_the_history_back_and_keeps_the_current_one()
    {
        File.WriteAllText(Path.Combine(_data, "settings.json"), "{}");
        File.WriteAllText(Path.Combine(_data, "history.json"), "old");
        var backup = SettingsBackups.MakeIfDue(_data, Dir, new DateTime(2026, 10, 1))!;
        File.WriteAllText(Path.Combine(_data, "history.json"), "new");
        var (settings, error) = SettingsTransfer.Read(backup);
        Assert.Null(error);
        var before = SettingsTransfer.Import(backup, settings!, _data, new DateTime(2026, 10, 2));
        Assert.Equal("old", File.ReadAllText(Path.Combine(_data, "history.json")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(before, "history.json")));
    }
}
