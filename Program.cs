// A simple C# program to extract and convert Hogwarts Legacy save files.
// Based on https://github.com/NativeSmell/hogwarts-legacy-save-convertor/

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

internal static class Program
{
    // Matches "HL-00-00", "HL-00-10", etc.
    private static readonly Regex Pattern = new(@"HL-\d{2}-\d{2}", RegexOptions.Compiled);
    private static readonly HashSet<string> NeedToSave = new(StringComparer.OrdinalIgnoreCase)
    {
        "SavedUserOptions.sav",
        "SaveGameList.sav",
        "HL-00-00.sav",
        "HL-00-10.sav"
    };

    private const string WorkingDir = "hogwarts-legacy-save-converter-output";
    private static string GetWorkingDir() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, WorkingDir));

    private static int Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Hogwarts Legacy Save Converter");
        Console.WriteLine("https://github.com/Axolittles/hogwarts-legacy-save-converter");
        Console.WriteLine("Based on https://github.com/NativeSmell/hogwarts-legacy-save-convertor/\n");

        var working = GetWorkingDir();
        // Delete working folder if it exists
        if (Directory.Exists(working))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("WARNING: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"Working folder {working} is not empty. It will be erased!");

            // Allow user to abort
            if (!PromptYesNo("Continue? [Y/N] "))
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("Cancelled by user.");
                Pause();
                return 0;
            }

            DeleteDirectory(working);
        }

        // Create working folder
        Directory.CreateDirectory(working);

        // Find paths
        var baseAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var defaultWgsRoot = Path.Combine(
            baseAppData,
            "Packages",
            // ReSharper disable StringLiteralTypo
            "WarnerBros.Interactive.PHX_ktmk1xygcecda",
            // ReSharper enable StringLiteralTypo
            "SystemAppData",
            "wgs"
        );

        var cliPath = ParsePathArg(args);
        string? path = null;

        if (!string.IsNullOrWhiteSpace(cliPath) && Directory.Exists(cliPath))
        {
            path = ResolveSaveDir(cliPath);
            if (path == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Path provided with -p does not contain save files: {cliPath}");
            }
        }

        if (path == null && Directory.Exists(defaultWgsRoot))
        {
            path = SelectWgsUserDirectory(defaultWgsRoot);
        }

        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Could not find Hogwarts Legacy save folder automatically.\n" +
                              "Usage: convert -p <PATH_TO_SAVE_FOLDER>");
            return 1;
        }

        // Find saves
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"Checking path: {path}");
        try
        {
            // Convert saves
            Console.ForegroundColor = ConsoleColor.DarkGray;
            ConvertSaves(path, NeedToSave, working);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nConversion complete!");

            // Ask whether to migrate automatically
            if (PromptYesNo("\nPerform Automatic Migration to Steam? [Y/N] "))
            {
                var steamUserDir = SelectSteamUserDirectory(baseAppData);
                if (steamUserDir is null)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine("Cancelled. Opening folders instead.\n");
                    OpenPostConversionFolders(baseAppData, working);
                    Pause();
                    return 0;
                }

                var steamId = new DirectoryInfo(steamUserDir).Name;

                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("\nWARNING: ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"About to migrate Hogwarts Legacy Save Files from GamePass to Steam, overwriting saves for SteamID [{steamId}].");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("This cannot be reversed! Please remember to backup your Steam saves!");

                if (PromptYesNo("\nContinue? [Y/N] "))
                {
                    try
                    {
                        var copied = CopyOutputToSteam(working, steamUserDir, overwrite: true);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("\nMigration complete.");
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine($"{copied} file(s) copied to: {steamUserDir}");
                        // Clean up the temporary output folder after successful auto migration
                        DeleteDirectory(working);
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Migration failed: {ex.Message}");
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine("Opening folders for manual copy...");
                        OpenPostConversionFolders(baseAppData, working);
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine("Migration aborted by user. Opening folders for manual copy...");
                    OpenPostConversionFolders(baseAppData, working);
                }
            }
            else
            {
                OpenPostConversionFolders(baseAppData, working);
            }

            Pause();
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Pause();
            return 2;
        }
    }

    private static string? ParsePathArg(string[] args)
    {
        // Supports: -p <path>, --path <path>, -p=<path>, --path=<path>
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a.Equals("-p", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("--path", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) return args[i + 1];
            }
            else if (a.StartsWith("-p=", StringComparison.OrdinalIgnoreCase))
            {
                return a[3..];
            }
            else if (a.StartsWith("--path=", StringComparison.OrdinalIgnoreCase))
            {
                return a[7..];
            }
        }
        return null;
    }

    private static string? SelectWgsUserDirectory(string wgsRoot)
    {
        if (!Directory.Exists(wgsRoot))
            return null;

        var candidates = EnumerateWgsUserFolders(wgsRoot).ToList();

        switch (candidates.Count)
        {
            case 0:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"No user save folders found under: {wgsRoot}");
                return null;

            case 1:
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"Found single WGS user folder: {candidates[0].Name}");
                return candidates[0].FullName;

            default:
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("\nFound Game Pass (WGS) user save folders:\n");
                for (var i = 0; i < candidates.Count; i++)
                    Console.WriteLine($"  {i + 1}.  {candidates[i].Name}");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("\nPlease select an input save folder: [1, 2, .. / C to cancel] ");
                Console.ForegroundColor = ConsoleColor.White;

                while (true)
                {
                    var input = Console.ReadLine()?.Trim();
                    if (string.IsNullOrEmpty(input))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write("Enter a number or C: ");
                        Console.ForegroundColor = ConsoleColor.White;
                        continue;
                    }

                    if (input.Equals("C", StringComparison.OrdinalIgnoreCase))
                        return null;

                    if (int.TryParse(input, out var idx) && idx >= 1 && idx <= candidates.Count)
                        return candidates[idx - 1].FullName;

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("Invalid selection. Enter a valid number or C to cancel: ");
                    Console.ForegroundColor = ConsoleColor.White;
                }
        }
    }

    private static IEnumerable<DirectoryInfo> EnumerateWgsUserFolders(string wgsRoot)
    {
        foreach (var d in Directory.EnumerateDirectories(wgsRoot))
        {
            var di = new DirectoryInfo(d);
            var name = di.Name;
            if (string.Equals(name, "t", StringComparison.OrdinalIgnoreCase))
                continue;
            var hasContent = false;
            try
            {
                hasContent = Directory.EnumerateFileSystemEntries(di.FullName).Any();
            }
            catch { /* ignore IO errors */ }

            if (hasContent)
                yield return di;
        }
    }

    private static string? ResolveSaveDir(string inputPath)
    {
        // If the folder itself contains save-looking files, use it directly.
        try
        {
            if (Directory.EnumerateFiles(inputPath, "containers.index", SearchOption.TopDirectoryOnly).Any())
                return inputPath;
        }
        catch { /* ignore */ }

        // Otherwise, see if it looks like a WGS root with subfolders / choose one.
        try
        {
            var subs = EnumerateWgsUserFolders(inputPath).ToList();
            if (subs.Count == 1) return subs[0].FullName;
            if (subs.Count > 1)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"\nMultiple candidate folders inside: {inputPath}\n");
                for (var i = 0; i < subs.Count; i++)
                    Console.WriteLine($"  {i + 1}.  {subs[i].Name}");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("\nSelect one to use: [1, 2, .. / C to cancel] ");
                Console.ForegroundColor = ConsoleColor.White;

                while (true)
                {
                    var choice = Console.ReadLine()?.Trim();
                    if (string.Equals(choice, "C", StringComparison.OrdinalIgnoreCase))
                        return null;
                    if (int.TryParse(choice, out var idx) && idx >= 1 && idx <= subs.Count)
                        return subs[idx - 1].FullName;

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("Invalid selection. Enter a valid number or C: ");
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }
        }
        catch { /* ignore */ }

        // Fallback: if no files and no subfolders, reject
        return null;
    }

    // ---------- Helpers: UX / Prompts / Opening  ----------
    private static void OpenPostConversionFolders(string baseAppData, string workingPath)
    {
        TryOpenFolder(workingPath, "Converted output folder");
        OpenHogwartsSavesSmart(baseAppData);
    }

    private static bool PromptYesNo(string? message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        if (!string.IsNullOrEmpty(message))
            Console.Write(message);

        Console.ForegroundColor = ConsoleColor.White;
        while (true)
        {
            var line = Console.ReadLine();
            if (line == null) return false;

            line = line.Trim();
            if (line.Equals("Y", StringComparison.OrdinalIgnoreCase)) return true;
            if (line.Equals("N", StringComparison.OrdinalIgnoreCase)) return false;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Please enter Y or N: ");
            Console.ForegroundColor = ConsoleColor.White;
        }
    }

    private static string? SelectSteamUserDirectory(string baseAppData)
    {
        var saveRoot = Path.Combine(baseAppData, "Hogwarts Legacy", "Saved", "SaveGames");
        if (!Directory.Exists(saveRoot))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Could not find Steam SaveGames folder. Is Hogwarts Legacy installed via Steam?");
            return null;
        }

        // Find numeric subfolders (SteamID64 style)
        var steamUserDirs = Directory.EnumerateDirectories(saveRoot)
            .Select(d => new DirectoryInfo(d))
            .Where(di =>
            {
                var name = di.Name;
                return name.All(char.IsDigit) && name.Length >= 8;
            })
            .OrderBy(di => di.Name)
            .ToList();

        switch (steamUserDirs.Count)
        {
            case 0:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"No SteamID subfolders found in: {saveRoot}");
                return null;
            case 1:
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"Found single SteamID: {steamUserDirs[0].Name}");
                return steamUserDirs[0].FullName;
        }

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("\nFound Steam Saves:\n");
        for (var i = 0; i < steamUserDirs.Count; i++)
            Console.WriteLine($"  {i + 1}.  {steamUserDirs[i].Name}");

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("\nPlease select a SteamID: [1, 2, .. / C to cancel] ");

        Console.ForegroundColor = ConsoleColor.White;
        while (true)
        {
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Enter a number or C: ");
                Console.ForegroundColor = ConsoleColor.White;
                continue;
            }

            if (input.Equals("C", StringComparison.OrdinalIgnoreCase))
                return null;

            if (int.TryParse(input, out var idx) && idx >= 1 && idx <= steamUserDirs.Count)
                return steamUserDirs[idx - 1].FullName;

            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("Invalid selection. Enter a valid number or C to cancel: ");
            Console.ForegroundColor = ConsoleColor.White;
        }
    }

    private static int CopyOutputToSteam(string srcDir, string destDir, bool overwrite)
    {
        if (!Directory.Exists(srcDir))
            throw new DirectoryNotFoundException($"Output folder not found: {srcDir}");

        if (!Directory.Exists(destDir))
            throw new DirectoryNotFoundException($"SteamID folder not found: {destDir}");

        var copied = 0;
        foreach (var file in Directory.EnumerateFiles(srcDir))
        {
            var name = Path.GetFileName(file);
            var destPath = Path.Combine(destDir, name);

            File.Copy(file, destPath, overwrite);
            copied++;
        }
        return copied;
    }

    private static void Pause()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\nPress Enter to exit...");
        Console.ReadLine();
    }

    private static void ConvertSaves(string saveDir, HashSet<string> needToSave, string outputDir)
    {
        var files = EnumerateAllFiles(saveDir).ToList();
        var savedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var f in files)
        {
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(f);
            }
            catch
            {
                continue;
            }

            var type = RecognizeFileType(bytes);
            if (type is null)
                continue;

            var outPath = Path.Combine(outputDir, type);
            try
            {
                File.WriteAllBytes(outPath, bytes);
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"Writing file: {Path.GetFileName(Path.GetDirectoryName(outPath))}/{Path.GetFileName(outPath)}");
                savedFiles.Add(type);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"Failed to write {outPath}: {ex.Message}");
            }
        }

        foreach (var missing in needToSave.Except(savedFiles, StringComparer.OrdinalIgnoreCase))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"File {missing} not found. It will make problems!...");
            if (missing.EndsWith("10.sav", StringComparison.OrdinalIgnoreCase))
                Console.WriteLine("There is no Autosave files");
            else if (missing.EndsWith("00-00.sav", StringComparison.OrdinalIgnoreCase))
                Console.WriteLine("There is no Manual Save files! Just save game manually");
        }
    }

    private static IEnumerable<string> EnumerateAllFiles(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            IEnumerable<string> files = [];
            IEnumerable<string> dirs = [];

            try { files = Directory.EnumerateFiles(dir); } catch { }
            foreach (var file in files)
                yield return file;

            try { dirs = Directory.EnumerateDirectories(dir); } catch { }
            foreach (var d in dirs)
                stack.Push(d);
        }
    }

    private static string? RecognizeFileType(byte[] data)
    {
        if (ContainsBytes(data, "/Script/Phoenix.SavedSettingsData"u8.ToArray()))
            return "SavedUserOptions.sav";

        if (!ContainsBytes(data, "/Script/PersistentData.PersistentGameData"u8.ToArray()))
            return null;

        var text = Encoding.UTF8.GetString(data);
        var matches = Pattern.Matches(text);
        return matches.Count switch
        {
            > 1 => "SaveGameList.sav",
            1 => matches[0].Value + ".sav",
            _ => null
        };
    }

    private static bool ContainsBytes(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length) return false;

        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack[i] != needle[0]) continue;

            var j = 1;
            for (; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) break;
            }
            if (j == needle.Length) return true;
        }
        return false;
    }

    private static void OpenHogwartsSavesSmart(string baseAppData)
    {
        var saveRoot = Path.Combine(baseAppData, "Hogwarts Legacy", "Saved", "SaveGames");
        if (!Directory.Exists(saveRoot))
            return;

        try
        {
            var steamUserDirs = Directory.EnumerateDirectories(saveRoot)
                .Select(d => new DirectoryInfo(d))
                .Where(di =>
                {
                    var name = di.Name;
                    return name.All(char.IsDigit) && name.Length >= 8;
                })
                .ToList();

            if (steamUserDirs.Count == 1)
            {
                TryOpenFolder(steamUserDirs[0].FullName, "Steam user save folder");
            }
            else
            {
                TryOpenFolder(saveRoot, "Hogwarts Legacy SaveGames folder");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Could not enumerate SaveGames subfolders: {ex.Message}");
            TryOpenFolder(saveRoot, "Hogwarts Legacy SaveGames folder");
        }
    }

    private static void TryOpenFolder(string path, string label)
    {
        try
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"Opening {label}: {path}");
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Could not open {label}: {ex.Message}");
        }
    }

    private static void DeleteDirectory(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return;
            Directory.Delete(dir, recursive: true);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"Cleaned up temporary folder: {dir}");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Warning: could not delete '{dir}': {ex.Message}");
        }
    }
}
