using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace ProcessorTycoon.Setup;

internal sealed record InstalledFile(string Path, string Hash, string? OriginalHash, string? Backup);
internal sealed record Manifest(int Version, List<InstalledFile> Files);
internal sealed record Change(string Path, string? BeforeHash, string? AfterHash, string? Snapshot);
internal sealed record Journal(List<Change> Changes, string? FinalManifest);
internal sealed class SetupException(string message, int exitCode = 3) : Exception(message)
{
    public int ExitCode { get; } = exitCode;
}

internal sealed class InstallerEngine
{
    const string StateName = ".processor-tycoon-russian";
    readonly string root;
    readonly string state;
    string ManifestPath => Path.Combine(state, "manifest.json");
    string JournalPath => Path.Combine(state, "pending.json");
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "winhttp.dll", "doorstop_config.ini", ".doorstop_version",
        "BepInEx/core/0Harmony.dll", "BepInEx/core/0Harmony.xml", "BepInEx/core/0Harmony20.dll",
        "BepInEx/core/BepInEx.dll", "BepInEx/core/BepInEx.xml", "BepInEx/core/BepInEx.Harmony.dll",
        "BepInEx/core/BepInEx.Harmony.xml", "BepInEx/core/BepInEx.Preloader.dll", "BepInEx/core/BepInEx.Preloader.xml",
        "BepInEx/core/HarmonyXInterop.dll", "BepInEx/core/Mono.Cecil.dll", "BepInEx/core/Mono.Cecil.Mdb.dll",
        "BepInEx/core/Mono.Cecil.Pdb.dll", "BepInEx/core/Mono.Cecil.Rocks.dll", "BepInEx/core/MonoMod.RuntimeDetour.dll",
        "BepInEx/core/MonoMod.RuntimeDetour.xml", "BepInEx/core/MonoMod.Utils.dll", "BepInEx/core/MonoMod.Utils.xml",
        "BepInEx/plugins/ProcessorTycoon.Russian/ProcessorTycoon.Russian.dll",
        "BepInEx/plugins/ProcessorTycoon.Russian/Newtonsoft.Json.dll",
        "BepInEx/plugins/ProcessorTycoon.Russian/translations/ui.json",
        "BepInEx/plugins/ProcessorTycoon.Russian/translations/code.json",
        "BepInEx/plugins/ProcessorTycoon.Russian/translations/extra.json",
        "BepInEx/plugins/ProcessorTycoon.Russian/translations/templates.json"
    };

    public InstallerEngine(string directory)
    {
        root = Path.GetFullPath(directory);
        state = Path.Combine(root, StateName);
    }

    public string Run(string operation)
    {
        ValidateGame();
        EnsureClosed();
        SafePath(StateName);
        var payload = operation == "--install" ? LoadPayload() : null;
        if (payload != null && !File.Exists(SafePath(StateName + "/manifest.json")) && !File.Exists(SafePath(StateName + "/pending.json")))
            CheckSharedRuntime(payload);
        Directory.CreateDirectory(state);
        SafePath(StateName + "/lock");
        using var gate = new FileStream(Path.Combine(state, "lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        Recover();
        var manifest = ReadManifest();
        if (operation == "--verify")
        {
            if (manifest == null) throw new SetupException("Русификатор не установлен.", 5);
            CheckInstalled(manifest);
            return "Проверка пройдена. Все файлы русификатора на месте.";
        }
        if (operation == "--uninstall")
        {
            if (manifest == null) return "Русификатор уже удалён.";
            CheckInstalled(manifest);
            var retainRuntime = HasForeignMods();
            var targets = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in manifest.Files)
            {
                if (retainRuntime && IsSharedRuntime(file.Path)) continue;
                byte[]? original = null;
                if (file.Backup != null)
                {
                    original = File.ReadAllBytes(StatePath(file.Backup));
                    if (Hash(original) != file.OriginalHash) throw new SetupException("Повреждена резервная копия: " + file.Path);
                }
                targets.Add(file.Path, original);
            }
            Commit(targets, null, manifest.Files.ToDictionary(f => f.Path, f => (string?)f.Hash, StringComparer.OrdinalIgnoreCase));
            return retainRuntime
                ? "Русификатор удалён. Общая среда BepInEx и загрузчик сохранены для сторонних модов; файлы перевода удалены или восстановлены."
                : "Русификатор удалён. Исходные файлы восстановлены; сторонние моды сохранены.";
        }
        if (operation != "--install") throw new SetupException("Неизвестная команда.", 2);
        if (manifest != null)
        {
            CheckInstalled(manifest);
            if (manifest.Files.Count != payload!.Count || manifest.Files.Any(f => !payload.TryGetValue(f.Path, out var b) || Hash(b) != f.Hash))
                throw new SetupException("Установлена другая версия. Сначала удалите её старым установщиком.");
            return "Эта версия русификатора уже установлена. Резервные копии сохранены.";
        }
        CheckSharedRuntime(payload!);
        var files = new List<InstalledFile>();
        var backupId = Guid.NewGuid().ToString("N");
        foreach (var (path, bytes) in payload!)
        {
            var target = SafePath(path);
            string? originalHash = null, backup = null;
            if (File.Exists(target))
            {
                var original = File.ReadAllBytes(target);
                originalHash = Hash(original);
                if (IsSharedRuntime(path) && originalHash != Hash(bytes))
                    throw new SetupException("Общая среда BepInEx изменилась во время подготовки. Повторите установку после проверки совместимости: " + path);
                backup = "backups/" + backupId + "/" + files.Count + ".bin";
                WriteNew(StatePath(backup), original);
            }
            files.Add(new(path, Hash(bytes), originalHash, backup));
        }
        Commit(payload.ToDictionary(x => x.Key, x => (byte[]?)x.Value, StringComparer.OrdinalIgnoreCase), JsonSerializer.Serialize(new Manifest(1, files), Json), files.ToDictionary(f => f.Path, f => f.OriginalHash, StringComparer.OrdinalIgnoreCase));
        return "Русификатор установлен. Запустите Processor Tycoon обычным способом.";
    }

    static bool IsSharedRuntime(string path) => !path.StartsWith("BepInEx/plugins/ProcessorTycoon.Russian/", StringComparison.OrdinalIgnoreCase);

    void CheckSharedRuntime(Dictionary<string, byte[]> payload)
    {
        foreach (var (path, bytes) in payload)
        {
            if (!IsSharedRuntime(path)) continue;
            var current = CurrentHash(path);
            if (current != null && current != Hash(bytes))
                throw new SetupException("Обнаружен другой загрузчик или общая среда BepInEx: " + path + ". Ничего не изменено. Используйте отдельную чистую копию игры либо согласуйте версии загрузчика и BepInEx с авторами сторонних модов; не заменяйте эти файлы вручную.");
        }
    }

    bool HasForeignMods()
    {
        return ContainsFiles("BepInEx/plugins") || ContainsFiles("BepInEx/patchers");
    }

    bool ContainsFiles(string relative)
    {
        var path = SafePath(relative);
        if (!Directory.Exists(path)) return File.Exists(path);
        foreach (var entry in Directory.EnumerateFileSystemEntries(path))
        {
            var child = relative + "/" + Path.GetFileName(entry);
            if (child.Equals("BepInEx/plugins/ProcessorTycoon.Russian", StringComparison.OrdinalIgnoreCase)) continue;
            SafePath(child);
            if (Directory.Exists(entry) ? ContainsFiles(child) : File.Exists(entry)) return true;
        }
        return false;
    }

    void ValidateGame()
    {
        if (!File.Exists(SafePath("Processor Tycoon.exe")) || !File.Exists(SafePath("Processor Tycoon_Data/Managed/Assembly-CSharp.dll")))
            throw new SetupException("Выберите папку с Processor Tycoon.exe и Processor Tycoon_Data/Managed/Assembly-CSharp.dll.", 2);
    }

    void EnsureClosed()
    {
        foreach (var process in Process.GetProcessesByName("Processor Tycoon"))
        {
            using (process)
            {
                try
                {
                    if (string.Equals(process.MainModule?.FileName, Path.Combine(root, "Processor Tycoon.exe"), StringComparison.OrdinalIgnoreCase))
                        throw new SetupException("Закройте Processor Tycoon перед изменением файлов.", 4);
                }
                catch (System.ComponentModel.Win32Exception) { throw new SetupException("Не удалось проверить запущенную игру. Закройте все процессы Processor Tycoon.", 4); }
                catch (InvalidOperationException) { }
            }
        }
    }

    string SafePath(string relative)
    {
        if (string.IsNullOrEmpty(relative) || relative.Contains('\\') || relative.Contains(':') || relative.StartsWith('/') || relative.Split('/').Any(p => p is "" or "." or ".." || p.EndsWith('.') || p.EndsWith(' ')))
            throw new SetupException("Недопустимый путь: " + relative);
        var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new SetupException("Путь вне папки игры.");
        for (string? p = full; p != null; p = Path.GetDirectoryName(p))
        {
            if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw new SetupException("Ссылки и junction не поддерживаются: " + p);
        }
        return full;
    }

    string StatePath(string relative) => SafePath(StateName + "/" + relative);
    static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    string? CurrentHash(string relative)
    {
        var path = SafePath(relative);
        if (Directory.Exists(path)) throw new SetupException("Вместо файла найдена папка: " + relative);
        return File.Exists(path) ? Hash(File.ReadAllBytes(path)) : null;
    }

    Dictionary<string, byte[]> LoadPayload()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip") ?? throw new SetupException("В установщик не встроен payload.zip.");
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.EndsWith('/')) { SafePath(entry.FullName.TrimEnd('/')); continue; }
            SafePath(entry.FullName);
            if (!Allowed.Contains(entry.FullName)) throw new SetupException("Файл не входит в разрешённый пакет: " + entry.FullName);
            total += entry.Length;
            if (entry.Length > 64 * 1024 * 1024 || total > 256 * 1024 * 1024) throw new SetupException("Слишком большой пакет.");
            using var input = entry.Open();
            using var output = new MemoryStream();
            input.CopyTo(output);
            if (!result.TryAdd(entry.FullName, output.ToArray())) throw new SetupException("Повторный путь в пакете.");
        }
        foreach (var required in new[] { "winhttp.dll", "doorstop_config.ini", "BepInEx/core/BepInEx.dll", "BepInEx/plugins/ProcessorTycoon.Russian/ProcessorTycoon.Russian.dll" })
            if (!result.ContainsKey(required)) throw new SetupException("В пакете отсутствует " + required);
        return result;
    }

    Manifest? ReadManifest()
    {
        SafePath(StateName + "/manifest.json");
        if (!File.Exists(ManifestPath)) return null;
        var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(ManifestPath)) ?? throw new SetupException("Пустой манифест.");
        if (manifest.Version != 1 || manifest.Files.Count == 0) throw new SetupException("Неизвестный манифест.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            if (!Allowed.Contains(file.Path) || !seen.Add(file.Path)) throw new SetupException("Недопустимый манифест.");
            SafePath(file.Path);
            if ((file.Backup == null) != (file.OriginalHash == null)) throw new SetupException("Неполный манифест.");
            if (file.Backup != null && !file.Backup.StartsWith("backups/", StringComparison.Ordinal)) throw new SetupException("Недопустимый путь копии.");
        }
        return manifest;
    }

    void CheckInstalled(Manifest manifest)
    {
        foreach (var file in manifest.Files)
        {
            if (CurrentHash(file.Path) != file.Hash) throw new SetupException("Конфликт: файл изменён или отсутствует. Ничего не изменено: " + file.Path);
            if (file.Backup != null && Hash(File.ReadAllBytes(StatePath(file.Backup))) != file.OriginalHash)
                throw new SetupException("Повреждена резервная копия: " + file.Path);
        }
    }

    void Commit(Dictionary<string, byte[]?> targets, string? finalManifest, Dictionary<string, string?> expected)
    {
        var changes = new List<Change>();
        var id = Guid.NewGuid().ToString("N");
        foreach (var (relative, bytes) in targets)
        {
            var path = SafePath(relative);
            byte[]? before = File.Exists(path) ? File.ReadAllBytes(path) : null;
            if (CurrentHash(relative) != expected[relative] || (before == null ? null : Hash(before)) != expected[relative])
                throw new SetupException("Файл изменился во время подготовки: " + relative);
            var snapshot = before == null ? null : "transactions/" + id + "/" + changes.Count + ".bin";
            if (snapshot != null) WriteNew(StatePath(snapshot), before!);
            changes.Add(new(relative, before == null ? null : Hash(before), bytes == null ? null : Hash(bytes), snapshot));
        }
        WriteNew(JournalPath, JsonSerializer.SerializeToUtf8Bytes(new Journal(changes, finalManifest), Json));
        try
        {
            foreach (var change in changes)
            {
                if (CurrentHash(change.Path) != change.BeforeHash) throw new SetupException("Файл изменился во время установки: " + change.Path);
                Replace(SafePath(change.Path), targets[change.Path]);
            }
            if (finalManifest == null) File.Delete(ManifestPath);
            else Replace(ManifestPath, System.Text.Encoding.UTF8.GetBytes(finalManifest));
            File.Delete(JournalPath);
        }
        catch
        {
            Recover();
            throw;
        }
    }

    void Recover()
    {
        SafePath(StateName + "/pending.json");
        if (!File.Exists(JournalPath)) return;
        var journal = JsonSerializer.Deserialize<Journal>(File.ReadAllText(JournalPath)) ?? throw new SetupException("Повреждён журнал транзакции.");
        var currentManifest = File.Exists(ManifestPath) ? File.ReadAllText(ManifestPath) : null;
        if (currentManifest == journal.FinalManifest) { File.Delete(JournalPath); return; }
        foreach (var change in journal.Changes)
        {
            if (!Allowed.Contains(change.Path)) throw new SetupException("Недопустимый путь журнала.");
            var hash = CurrentHash(change.Path);
            if (hash != change.BeforeHash && hash != change.AfterHash) throw new SetupException("Восстановление остановлено: файл изменён извне: " + change.Path);
            if (change.Snapshot != null)
            {
                if (!change.Snapshot.StartsWith("transactions/", StringComparison.Ordinal) || Hash(File.ReadAllBytes(StatePath(change.Snapshot))) != change.BeforeHash)
                    throw new SetupException("Повреждена копия транзакции.");
            }
            else if (change.BeforeHash != null) throw new SetupException("Нет копии транзакции.");
        }
        foreach (var change in journal.Changes.AsEnumerable().Reverse())
            if (CurrentHash(change.Path) != change.BeforeHash)
                Replace(SafePath(change.Path), change.Snapshot == null ? null : File.ReadAllBytes(StatePath(change.Snapshot)));
        File.Delete(JournalPath);
    }

    static void WriteNew(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        file.Write(bytes);
        file.Flush(true);
    }

    static void Replace(string path, byte[]? bytes)
    {
        if (bytes == null) { File.Delete(path); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { WriteNew(temp, bytes); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
