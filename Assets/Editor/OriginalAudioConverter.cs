using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class OriginalAudioConverter
{
    private const string SourceRoot = "Assets/OriginalAudio";
    private const string SeOutputRoot = "Assets/Resources/sfx";
    private const string BgmOutputRoot = "Assets/Resources/music";
    private const string FfmpegPathKey = "AutoChessBossRush.OriginalAudioConverter.FfmpegPath";

    [MenuItem("Tools/Audio/Convert OriginalAudio")]
    public static void ConvertAllOriginalAudio()
    {
        ConvertOriginalAudioAssets(null, true, true);
    }

    [MenuItem("Tools/Audio/Set FFmpeg Path...")]
    public static void SetFfmpegPath()
    {
        string currentPath = EditorPrefs.GetString(FfmpegPathKey, string.Empty);
        string startDirectory = string.IsNullOrEmpty(currentPath) ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) : Path.GetDirectoryName(currentPath);
        string selectedPath = EditorUtility.OpenFilePanel("Select ffmpeg.exe", startDirectory, "exe");

        if (string.IsNullOrEmpty(selectedPath))
            return;

        if (!Path.GetFileName(selectedPath).Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("Invalid FFmpeg", "ffmpeg.exe を選択してください。", "OK");
            return;
        }

        EditorPrefs.SetString(FfmpegPathKey, selectedPath);
        Debug.Log("[OriginalAudioConverter] FFmpeg path set: " + selectedPath);
    }

    public static void ConvertChangedAssets(IEnumerable<string> changedAssetPaths)
    {
        ConvertOriginalAudioAssets(changedAssetPaths, false, false);
    }

    private static void ConvertOriginalAudioAssets(IEnumerable<string> changedAssetPaths, bool force, bool showDialog)
    {
        EnsureFolder(SourceRoot);
        EnsureFolder(SeOutputRoot);
        EnsureFolder(BgmOutputRoot);

        List<string> sourceAssets = GetSourceAssets(changedAssetPaths);
        if (sourceAssets.Count == 0)
        {
            if (showDialog)
                EditorUtility.DisplayDialog("OriginalAudio Converter", "Assets/OriginalAudio 内に m4a ファイルがありません。", "OK");

            return;
        }

        string ffmpegPath = ResolveFfmpegPath();
        if (string.IsNullOrEmpty(ffmpegPath))
        {
            string message = "ffmpeg.exe が見つかりません。Tools > Audio > Set FFmpeg Path... から ffmpeg.exe を指定してください。";
            Debug.LogError("[OriginalAudioConverter] " + message);

            if (showDialog)
                EditorUtility.DisplayDialog("FFmpeg not found", message, "OK");

            return;
        }

        int convertedCount = 0;
        int skippedCount = 0;
        List<string> errors = new List<string>();

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < sourceAssets.Count; i++)
            {
                string sourceAsset = sourceAssets[i];
                AudioKind audioKind = GuessAudioKind(sourceAsset);
                string outputExtension = audioKind == AudioKind.Bgm ? ".ogg" : ".wav";
                string outputAsset = GetOutputRoot(audioKind) + "/" + Path.GetFileNameWithoutExtension(sourceAsset) + outputExtension;
                WarnIfLegacyResourceM4aExists(sourceAsset);

                if (!force && IsOutputFresh(sourceAsset, outputAsset))
                {
                    skippedCount++;
                    continue;
                }

                string sourceFullPath = ToFullPath(sourceAsset);
                string outputFullPath = ToFullPath(outputAsset);
                Directory.CreateDirectory(Path.GetDirectoryName(outputFullPath));

                string arguments = audioKind == AudioKind.Bgm
                    ? "-y -hide_banner -loglevel error -i " + Quote(sourceFullPath) + " -vn -ac 2 -ar 44100 -c:a libvorbis -q:a 5 " + Quote(outputFullPath)
                    : "-y -hide_banner -loglevel error -i " + Quote(sourceFullPath) + " -vn -ac 2 -ar 44100 -c:a pcm_s16le " + Quote(outputFullPath);

                string errorOutput;
                if (RunFfmpeg(ffmpegPath, arguments, out errorOutput))
                {
                    convertedCount++;
                    ConfigureImportedAudio(outputAsset, audioKind);
                }
                else
                {
                    errors.Add(sourceAsset + "\n" + errorOutput);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        if (errors.Count > 0)
        {
            Debug.LogError("[OriginalAudioConverter] Some audio files failed to convert:\n" + string.Join("\n\n", errors.ToArray()));
        }

        string result = string.Format("[OriginalAudioConverter] Converted: {0}, Skipped: {1}, Failed: {2}", convertedCount, skippedCount, errors.Count);
        Debug.Log(result);

        if (showDialog)
            EditorUtility.DisplayDialog("OriginalAudio Converter", result, "OK");
    }

    private static List<string> GetSourceAssets(IEnumerable<string> changedAssetPaths)
    {
        IEnumerable<string> candidates;
        if (changedAssetPaths == null)
        {
            candidates = Directory.Exists(SourceRoot)
                ? Directory.GetFiles(SourceRoot, "*.m4a", SearchOption.AllDirectories).Select(ToAssetPath)
                : Enumerable.Empty<string>();
        }
        else
        {
            candidates = changedAssetPaths;
        }

        return candidates
            .Where(path => !string.IsNullOrEmpty(path))
            .Select(path => path.Replace('\\', '/'))
            .Where(path => path.StartsWith(SourceRoot + "/", StringComparison.OrdinalIgnoreCase))
            .Where(path => Path.GetExtension(path).Equals(".m4a", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path)
            .ToList();
    }

    private static AudioKind GuessAudioKind(string assetPath)
    {
        string lowerPath = assetPath.Replace('\\', '/').ToLowerInvariant();
        string fileName = Path.GetFileNameWithoutExtension(lowerPath);

        if (lowerPath.Contains("/bgm/") || lowerPath.Contains("/music/") || fileName.StartsWith("bgm_") || fileName.StartsWith("music_") || fileName.Contains("battlemap"))
            return AudioKind.Bgm;

        return AudioKind.Se;
    }

    private static void ConfigureImportedAudio(string outputAsset, AudioKind audioKind)
    {
        AssetDatabase.ImportAsset(outputAsset, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        AudioImporter importer = AssetImporter.GetAtPath(outputAsset) as AudioImporter;
        if (importer == null)
            return;

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        if (audioKind == AudioKind.Bgm)
        {
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.65f;
        }
        else
        {
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
        }

        settings.preloadAudioData = audioKind == AudioKind.Se;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = false;
        importer.SaveAndReimport();
    }

    private static void WarnIfLegacyResourceM4aExists(string sourceAsset)
    {
        string fileName = Path.GetFileNameWithoutExtension(sourceAsset) + ".m4a";
        string[] legacyAssets =
        {
            SeOutputRoot + "/" + fileName,
            BgmOutputRoot + "/" + fileName,
            "Assets/Resources/Audio/" + fileName
        };

        string legacyAsset = legacyAssets.FirstOrDefault(path => File.Exists(ToFullPath(path)));
        if (string.IsNullOrEmpty(legacyAsset))
            return;

        Debug.LogWarning("[OriginalAudioConverter] " + legacyAsset + " が残っています。同名の wav/ogg と Resources パスが重なるため、元m4aは Assets/OriginalAudio 側だけに置くのがおすすめです。");
    }

    private static string GetOutputRoot(AudioKind audioKind)
    {
        return audioKind == AudioKind.Bgm ? BgmOutputRoot : SeOutputRoot;
    }

    private static bool IsOutputFresh(string sourceAsset, string outputAsset)
    {
        string sourceFullPath = ToFullPath(sourceAsset);
        string outputFullPath = ToFullPath(outputAsset);

        if (!File.Exists(outputFullPath))
            return false;

        return File.GetLastWriteTimeUtc(outputFullPath) >= File.GetLastWriteTimeUtc(sourceFullPath);
    }

    private static bool RunFfmpeg(string ffmpegPath, string arguments, out string errorOutput)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        try
        {
            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    errorOutput = "Failed to start ffmpeg process.";
                    return false;
                }

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                errorOutput = string.IsNullOrEmpty(error) ? output : error;
                return process.ExitCode == 0;
            }
        }
        catch (Exception exception)
        {
            errorOutput = exception.Message;
            return false;
        }
    }

    private static string ResolveFfmpegPath()
    {
        string editorPrefsPath = EditorPrefs.GetString(FfmpegPathKey, string.Empty);
        if (IsExecutableFile(editorPrefsPath))
            return editorPrefsPath;

        string environmentPath = Environment.GetEnvironmentVariable("FFMPEG_PATH");
        if (IsExecutableFile(environmentPath))
            return environmentPath;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string[] candidates =
        {
            Path.Combine(projectRoot, "Tools", "ffmpeg", "ffmpeg.exe"),
            Path.Combine(Application.dataPath, "Tools", "ffmpeg", "ffmpeg.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin", "ffmpeg.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "ffmpeg", "bin", "ffmpeg.exe"),
            Path.Combine("C:\\", "ffmpeg", "bin", "ffmpeg.exe")
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (IsExecutableFile(candidates[i]))
                return candidates[i];
        }

        return FindExecutableInPath("ffmpeg.exe");
    }

    private static bool IsExecutableFile(string path)
    {
        return !string.IsNullOrEmpty(path) && File.Exists(path);
    }

    private static string FindExecutableInPath(string executableName)
    {
        string pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVariable))
            return null;

        string[] directories = pathVariable.Split(Path.PathSeparator);
        for (int i = 0; i < directories.Length; i++)
        {
            string directory = directories[i];
            if (string.IsNullOrEmpty(directory))
                continue;

            string candidate = Path.Combine(directory.Trim(), executableName);
            if (IsExecutableFile(candidate))
                return candidate;
        }

        return null;
    }

    private static void EnsureFolder(string assetPath)
    {
        string[] parts = assetPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }

    private static string ToFullPath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static string ToAssetPath(string fullPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/').TrimEnd('/') + "/";
        string normalizedPath = Path.GetFullPath(fullPath).Replace('\\', '/');
        return normalizedPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
            ? normalizedPath.Substring(projectRoot.Length)
            : normalizedPath;
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private enum AudioKind
    {
        Se,
        Bgm
    }
}

public class OriginalAudioPostprocessor : AssetPostprocessor
{
    private static readonly HashSet<string> PendingAssets = new HashSet<string>();
    private static bool conversionQueued;

    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        QueueAssets(importedAssets);
        QueueAssets(movedAssets);

        if (PendingAssets.Count == 0 || conversionQueued)
            return;

        conversionQueued = true;
        EditorApplication.delayCall += ConvertPendingAssets;
    }

    private static void QueueAssets(IEnumerable<string> assetPaths)
    {
        if (assetPaths == null)
            return;

        foreach (string assetPath in assetPaths)
        {
            string normalizedPath = assetPath.Replace('\\', '/');
            if (normalizedPath.StartsWith("Assets/OriginalAudio/", StringComparison.OrdinalIgnoreCase) &&
                Path.GetExtension(normalizedPath).Equals(".m4a", StringComparison.OrdinalIgnoreCase))
            {
                PendingAssets.Add(normalizedPath);
            }
        }
    }

    private static void ConvertPendingAssets()
    {
        conversionQueued = false;

        string[] assets = PendingAssets.ToArray();
        PendingAssets.Clear();

        OriginalAudioConverter.ConvertChangedAssets(assets);
    }
}
