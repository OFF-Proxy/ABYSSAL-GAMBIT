using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

public static class AttackEffectSpriteBuilder
{
    private const string SourceRoot = "Assets/Resources/AttackEffects";
    private const string GeneratedRoot = "Assets/Resources/AttackEffects/Generated";
    private const float SpritePixelsPerUnit = 100f;

    private static readonly string[] EffectNames =
    {
        "fx_f1_casterprojectile",
        "fx_explosionblueelectrical",
        "fx_crossslash",
        "fx_heal",
        "fx_distortion_hex_shield",
        "fx_buff",
        "fx_f6_bbs_stun",
        "fx_frozen",
        "fx_impactgreen",
        "fx_impactred",
        "fx_impactblue",
        "fx_redlightning",
        "fx_ringswirl",
        "fx_slashfrenzy",
        "fx_whiteexplosion"
    };

    private static readonly HashSet<string> AlreadyUprightRotatedEffects = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "fx_distortion_hex_shield"
    };

    [MenuItem("Tools/AutoChess/Build Attack Effect Sprites")]
    public static void BuildAttackEffectSprites()
    {
        int builtCount = 0;
        for (int i = 0; i < EffectNames.Length; i++)
        {
            if (BuildEffect(EffectNames[i]))
                builtCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Built attack effect sprite sheets: {builtCount}/{EffectNames.Length}");
    }

    private static bool BuildEffect(string effectName)
    {
        string texturePath = $"{SourceRoot}/{effectName}.png";
        string plistPath = $"{SourceRoot}/{effectName}.plist";
        if (!File.Exists(texturePath) || !File.Exists(plistPath))
        {
            Debug.LogWarning($"Attack effect source missing: {effectName}");
            return false;
        }

        Texture2D texture = LoadTexture(texturePath);
        if (texture == null)
        {
            Debug.LogWarning($"Attack effect texture is unreadable: {texturePath}");
            return false;
        }

        List<EffectFrame> frames = LoadFrames(plistPath);
        if (frames.Count == 0)
        {
            Debug.LogWarning($"Attack effect plist has no frames: {plistPath}");
            return false;
        }

        string outputFolder = $"{GeneratedRoot}/{effectName}";
        PrepareOutputFolder(outputFolder);
        int writtenCount = 0;
        for (int i = 0; i < frames.Count; i++)
        {
            EffectFrame frame = frames[i];
            Texture2D frameTexture = CreateFrameTexture(texture, frame, effectName);
            if (frameTexture == null)
                continue;

            string spriteName = Path.GetFileNameWithoutExtension(frame.Name);
            if (string.IsNullOrEmpty(spriteName))
                spriteName = $"{effectName}_{i:D3}";

            string outputPath = $"{outputFolder}/{spriteName}.png";
            File.WriteAllBytes(outputPath, frameTexture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(frameTexture);
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
            ConfigureGeneratedSprite(outputPath);
            writtenCount++;
        }

        if (writtenCount == 0)
            return false;

        Debug.Log($"{effectName}: {writtenCount} plist-corrected frames generated.");
        return true;
    }

    private static void PrepareOutputFolder(string outputFolder)
    {
        if (!Directory.Exists(GeneratedRoot))
            Directory.CreateDirectory(GeneratedRoot);

        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        foreach (string filePath in Directory.GetFiles(outputFolder, "*.png", SearchOption.TopDirectoryOnly))
        {
            File.Delete(filePath);
            string metaPath = $"{filePath}.meta";
            if (File.Exists(metaPath))
                File.Delete(metaPath);
        }
    }

    private static Texture2D LoadTexture(string path)
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        return texture.LoadImage(File.ReadAllBytes(path)) ? texture : null;
    }

    private static List<EffectFrame> LoadFrames(string plistPath)
    {
        XDocument document;
        XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
        using (XmlReader reader = XmlReader.Create(plistPath, settings))
        {
            document = XDocument.Load(reader);
        }

        XElement rootDictionary = document.Root?.Element("dict");
        XElement framesDictionary = GetDictionaryValue(rootDictionary, "frames");
        if (framesDictionary == null)
            return new List<EffectFrame>();

        List<EffectFrame> frames = new List<EffectFrame>();
        List<XElement> elements = framesDictionary.Elements().ToList();
        for (int i = 0; i < elements.Count - 1; i += 2)
        {
            if (elements[i].Name.LocalName != "key" || elements[i + 1].Name.LocalName != "dict")
                continue;

            XElement frameDictionary = elements[i + 1];
            string frameName = elements[i].Value;
            string frameValue = GetStringValue(frameDictionary, "frame");
            string sourceColorRectValue = GetStringValue(frameDictionary, "sourceColorRect");
            string sourceSizeValue = GetStringValue(frameDictionary, "sourceSize");
            bool rotated = GetBoolValue(frameDictionary, "rotated");
            if (TryParseRect(frameValue, out RectInt rect))
            {
                TryParseRect(sourceColorRectValue, out RectInt sourceColorRect);
                TryParseSize(sourceSizeValue, out Vector2Int sourceSize);
                frames.Add(new EffectFrame(frameName, rect, sourceColorRect, sourceSize, rotated));
            }
        }

        return frames
            .OrderBy(frame => ExtractTrailingNumber(frame.Name))
            .ThenBy(frame => frame.Name)
            .ToList();
    }

    private static XElement GetDictionaryValue(XElement dictionaryElement, string key)
    {
        if (dictionaryElement == null)
            return null;

        List<XElement> children = dictionaryElement.Elements().ToList();
        for (int i = 0; i < children.Count - 1; i++)
        {
            if (children[i].Name.LocalName == "key" && children[i].Value == key && children[i + 1].Name.LocalName == "dict")
                return children[i + 1];
        }

        return null;
    }

    private static string GetStringValue(XElement dictionaryElement, string key)
    {
        List<XElement> children = dictionaryElement.Elements().ToList();
        for (int i = 0; i < children.Count - 1; i++)
        {
            if (children[i].Name.LocalName == "key" && children[i].Value == key && children[i + 1].Name.LocalName == "string")
                return children[i + 1].Value;
        }

        return string.Empty;
    }

    private static bool GetBoolValue(XElement dictionaryElement, string key)
    {
        if (dictionaryElement == null)
            return false;

        List<XElement> children = dictionaryElement.Elements().ToList();
        for (int i = 0; i < children.Count - 1; i++)
        {
            if (children[i].Name.LocalName == "key" && children[i].Value == key)
                return children[i + 1].Name.LocalName == "true";
        }

        return false;
    }

    private static bool TryParseRect(string value, out RectInt rect)
    {
        rect = default;
        Match match = Regex.Match(value ?? string.Empty, @"\{\{(?<x>-?\d+),(?<y>-?\d+)\},\{(?<w>\d+),(?<h>\d+)\}\}");
        if (!match.Success)
            return false;

        rect = new RectInt(
            int.Parse(match.Groups["x"].Value),
            int.Parse(match.Groups["y"].Value),
            int.Parse(match.Groups["w"].Value),
            int.Parse(match.Groups["h"].Value));
        return true;
    }

    private static bool TryParseSize(string value, out Vector2Int size)
    {
        size = default;
        Match match = Regex.Match(value ?? string.Empty, @"\{(?<w>\d+),(?<h>\d+)\}");
        if (!match.Success)
            return false;

        size = new Vector2Int(
            int.Parse(match.Groups["w"].Value),
            int.Parse(match.Groups["h"].Value));
        return true;
    }

    private static Texture2D CreateFrameTexture(Texture2D atlas, EffectFrame frame, string effectName)
    {
        int cropWidth = frame.Rect.width;
        int cropHeight = frame.Rect.height;
        bool rotatedInAtlas = frame.Rotated && !AlreadyUprightRotatedEffects.Contains(effectName);
        int packedWidth = rotatedInAtlas ? cropHeight : cropWidth;
        int packedHeight = rotatedInAtlas ? cropWidth : cropHeight;
        int unityY = atlas.height - frame.Rect.y - packedHeight;
        if (frame.Rect.x < 0 || unityY < 0 || packedWidth <= 0 || packedHeight <= 0 ||
            frame.Rect.x + packedWidth > atlas.width || unityY + packedHeight > atlas.height)
        {
            Debug.LogWarning($"Attack effect frame is outside the texture and was skipped: {frame.Name} {frame.Rect}");
            return null;
        }

        // fx_distortion_hex_shieldはplist側にrotated=trueが残っていますが、現在の参照画像では
        // そのコマがすでに正立しています。回転復元すると隣接コマを拾うため、画像の見た目を優先します。
        Color[] croppedPixels = rotatedInAtlas
            ? ExtractRotatedFramePixels(atlas, frame.Rect.x, unityY, cropWidth, cropHeight)
            : atlas.GetPixels(frame.Rect.x, unityY, cropWidth, cropHeight);

        int canvasWidth = frame.SourceSize.x > 0 ? frame.SourceSize.x : cropWidth;
        int canvasHeight = frame.SourceSize.y > 0 ? frame.SourceSize.y : cropHeight;
        Texture2D output = new Texture2D(canvasWidth, canvasHeight, TextureFormat.RGBA32, false);
        Color[] transparentPixels = new Color[canvasWidth * canvasHeight];
        for (int i = 0; i < transparentPixels.Length; i++)
            transparentPixels[i] = Color.clear;

        output.SetPixels(transparentPixels);

        int placementX = frame.SourceColorRect.width > 0 ? frame.SourceColorRect.x : Mathf.RoundToInt((canvasWidth - cropWidth) * 0.5f);
        int placementY = frame.SourceColorRect.height > 0
            ? canvasHeight - frame.SourceColorRect.y - cropHeight
            : Mathf.RoundToInt((canvasHeight - cropHeight) * 0.5f);
        placementX = Mathf.Clamp(placementX, 0, Mathf.Max(0, canvasWidth - cropWidth));
        placementY = Mathf.Clamp(placementY, 0, Mathf.Max(0, canvasHeight - cropHeight));

        output.SetPixels(placementX, placementY, cropWidth, cropHeight, croppedPixels);
        output.Apply(false, false);
        return output;
    }

    private static Color[] ExtractRotatedFramePixels(Texture2D atlas, int packedX, int packedY, int cropWidth, int cropHeight)
    {
        int packedWidth = cropHeight;
        int packedHeight = cropWidth;
        Color[] packedPixels = atlas.GetPixels(packedX, packedY, packedWidth, packedHeight);
        Color[] outputPixels = new Color[cropWidth * cropHeight];

        // TexturePackerのrotated=trueは、元画像がアトラス内で時計回りに90度回転している状態です。
        // UnityのSprite分割は回転を持てないため、ここで正立したピクセルへ戻します。
        for (int y = 0; y < cropHeight; y++)
        {
            for (int x = 0; x < cropWidth; x++)
            {
                int sourceX = cropHeight - 1 - y;
                int sourceY = x;
                outputPixels[y * cropWidth + x] = packedPixels[sourceY * packedWidth + sourceX];
            }
        }

        return outputPixels;
    }

    private static void ConfigureGeneratedSprite(string outputPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(outputPath) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.spritePixelsPerUnit = SpritePixelsPerUnit;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        importer.SetTextureSettings(settings);
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static int ExtractTrailingNumber(string value)
    {
        Match match = Regex.Match(value ?? string.Empty, @"_(\d+)\.png$");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    private readonly struct EffectFrame
    {
        public EffectFrame(string name, RectInt rect, RectInt sourceColorRect, Vector2Int sourceSize, bool rotated)
        {
            Name = name;
            Rect = rect;
            SourceColorRect = sourceColorRect;
            SourceSize = sourceSize;
            Rotated = rotated;
        }

        public string Name { get; }
        public RectInt Rect { get; }
        public RectInt SourceColorRect { get; }
        public Vector2Int SourceSize { get; }
        public bool Rotated { get; }
    }
}
