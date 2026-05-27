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
    private const string LegacyGeneratedRoot = "Assets/Resources/AttackEffects/Generated";
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
        "fx_whiteexplosion",
        "fx_fireimpact",
        "fx_f2_phoenixfire",
        "fx_chainlightning",
        "fx_f4_shadownova",
        "fx_f4_voidpulse",
        "fx_summonlegendary",
        "fx_f4_nethersummoning",
        "fx_f3_blaststarfire"
    };

    private static readonly HashSet<string> AlreadyUprightRotatedEffects = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "fx_distortion_hex_shield"
    };

    [MenuItem("Tools/AutoChess/Build Attack Effect Sprites")]
    public static void BuildAttackEffectSprites()
    {
        if (AssetDatabase.IsValidFolder(LegacyGeneratedRoot))
            AssetDatabase.DeleteAsset(LegacyGeneratedRoot);

        int builtCount = 0;
        for (int i = 0; i < EffectNames.Length; i++)
        {
            if (BuildEffect(EffectNames[i]))
                builtCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Sliced attack effect sprite sheets: {builtCount}/{EffectNames.Length}");
    }

    [MenuItem("Tools/AutoChess/Build Lightning Skill Effect Sprites")]
    public static void BuildLightningSkillEffectSprites()
    {
        string texturePath = $"{SourceRoot}/Pixel Art Skill Animations - Lightning/VFX3/Sprite-sheet/Sprite-sheet.png";
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null)
        {
            Debug.LogWarning($"Lightning skill sprite sheet missing: {texturePath}");
            return;
        }

        ConfigureUniformSpriteSheet(texturePath, texture, 128, 256, 5, "lightning_vfx3");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Sliced lightning skill VFX3 sprite sheet into 5 frames.");
    }

    [MenuItem("Tools/AutoChess/Slice Free4 Skill Effect Sprites")]
    public static void SliceFree4SkillEffectSprites()
    {
        string free4Root = $"{SourceRoot}/Free4";
        string[] texturePaths = Directory.GetFiles(free4Root, "*.png")
            .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            .Select(path => path.Replace("\\", "/"))
            .OrderBy(path => path)
            .ToArray();

        int slicedCount = 0;
        for (int i = 0; i < texturePaths.Length; i++)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePaths[i]);
            if (texture == null)
                continue;

            ConfigureUniformGridSpriteSheet(texturePaths[i], texture, 64, 64, Path.GetFileNameWithoutExtension(texturePaths[i]));
            slicedCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Sliced Free4 skill effect sprite sheets: {slicedCount}/{texturePaths.Length}");
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

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null)
        {
            Debug.LogWarning($"Attack effect texture could not be loaded: {texturePath}");
            return false;
        }

        List<EffectFrame> frames = LoadFrames(plistPath);
        if (frames.Count == 0)
        {
            Debug.LogWarning($"Attack effect plist has no frames: {plistPath}");
            return false;
        }

        ConfigureSourceSpriteSheet(texturePath, texture, frames);
        Debug.Log($"{effectName}: {frames.Count} plist frames sliced on source texture.");
        return true;
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

    private static void ConfigureSourceSpriteSheet(string texturePath, Texture2D texture, List<EffectFrame> frames)
    {
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null)
            return;

        List<SpriteMetaData> sprites = new List<SpriteMetaData>();
        HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < frames.Count; i++)
        {
            EffectFrame frame = frames[i];
            int packedWidth = frame.Rotated ? frame.Rect.height : frame.Rect.width;
            int packedHeight = frame.Rotated ? frame.Rect.width : frame.Rect.height;
            int unityY = texture.height - frame.Rect.y - packedHeight;
            if (frame.Rect.x < 0 || unityY < 0 || packedWidth <= 0 || packedHeight <= 0 ||
                frame.Rect.x + packedWidth > texture.width || unityY + packedHeight > texture.height)
            {
                Debug.LogWarning($"Attack effect frame is outside the texture and was skipped: {frame.Name} {frame.Rect}");
                continue;
            }

            string spriteName = Path.GetFileNameWithoutExtension(frame.Name);
            if (string.IsNullOrEmpty(spriteName))
                spriteName = $"{Path.GetFileNameWithoutExtension(texturePath)}_{i:D3}";

            string uniqueName = spriteName;
            int duplicateIndex = 1;
            while (!usedNames.Add(uniqueName))
            {
                uniqueName = $"{spriteName}_{duplicateIndex}";
                duplicateIndex++;
            }

            sprites.Add(new SpriteMetaData
            {
                name = uniqueName,
                rect = new Rect(frame.Rect.x, unityY, packedWidth, packedHeight),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            });
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.spritePixelsPerUnit = SpritePixelsPerUnit;
        importer.spritesheet = sprites.ToArray();

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        importer.SetTextureSettings(settings);
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static void ConfigureUniformSpriteSheet(string texturePath, Texture2D texture, int frameWidth, int frameHeight, int frameCount, string spritePrefix)
    {
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null)
            return;

        List<SpriteMetaData> sprites = new List<SpriteMetaData>();
        int columns = Mathf.Max(1, texture.width / frameWidth);
        int safeFrameCount = Mathf.Clamp(frameCount, 1, columns);
        int y = texture.height - frameHeight;
        for (int i = 0; i < safeFrameCount; i++)
        {
            sprites.Add(new SpriteMetaData
            {
                name = $"{spritePrefix}_{i:D3}",
                rect = new Rect(i * frameWidth, y, frameWidth, frameHeight),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            });
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.spritePixelsPerUnit = SpritePixelsPerUnit;
        importer.spritesheet = sprites.ToArray();

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        importer.SetTextureSettings(settings);
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static void ConfigureUniformGridSpriteSheet(string texturePath, Texture2D texture, int frameWidth, int frameHeight, string spritePrefix)
    {
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null)
            return;

        List<SpriteMetaData> sprites = new List<SpriteMetaData>();
        int columns = Mathf.Max(1, texture.width / frameWidth);
        int rows = Mathf.Max(1, texture.height / frameHeight);
        for (int rowFromTop = 0; rowFromTop < rows; rowFromTop++)
        {
            int y = texture.height - (rowFromTop + 1) * frameHeight;
            for (int column = 0; column < columns; column++)
            {
                sprites.Add(new SpriteMetaData
                {
                    name = $"{spritePrefix}_row{rowFromTop:00}_{column:000}",
                    rect = new Rect(column * frameWidth, y, frameWidth, frameHeight),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                });
            }
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.spritePixelsPerUnit = SpritePixelsPerUnit;
        importer.spritesheet = sprites.ToArray();

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
