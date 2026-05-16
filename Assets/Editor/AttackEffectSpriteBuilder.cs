using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class AttackEffectSpriteBuilder
{
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
        string texturePath = $"Assets/Resources/AttackEffects/{effectName}.png";
        string plistPath = $"Assets/Resources/AttackEffects/{effectName}.plist";
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

        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null)
            return false;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.spritePixelsPerUnit = 100f;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        EditorUtility.SetDirty(importer);

        List<SpriteRect> spriteRects = new List<SpriteRect>();
        for (int i = 0; i < frames.Count; i++)
        {
            EffectFrame frame = frames[i];
            int unityY = texture.height - frame.Rect.y - frame.Rect.height;
            if (unityY < 0 || frame.Rect.xMax > texture.width)
                continue;

            spriteRects.Add(new SpriteRect
            {
                name = Path.GetFileNameWithoutExtension(frame.Name),
                spriteID = GUID.Generate(),
                rect = new Rect(frame.Rect.x, unityY, frame.Rect.width, frame.Rect.height),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            });
        }

        if (spriteRects.Count == 0)
            return false;

        SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
        dataProvider.InitSpriteEditorDataProvider();
        dataProvider.SetSpriteRects(spriteRects.ToArray());

        ISpriteNameFileIdDataProvider nameFileIdDataProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nameFileIdDataProvider != null)
        {
            List<SpriteNameFileIdPair> nameFileIdPairs = spriteRects
                .Select(sprite => new SpriteNameFileIdPair(sprite.name, sprite.spriteID))
                .ToList();
            nameFileIdDataProvider.SetNameFileIdPairs(nameFileIdPairs);
        }

        dataProvider.Apply();
        importer.SaveAndReimport();
        Debug.Log($"{effectName}: {spriteRects.Count} frames imported.");
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

            string frameName = elements[i].Value;
            string frameValue = GetStringValue(elements[i + 1], "frame");
            if (TryParseRect(frameValue, out RectInt rect))
                frames.Add(new EffectFrame(frameName, rect));
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

    private static int ExtractTrailingNumber(string value)
    {
        Match match = Regex.Match(value ?? string.Empty, @"_(\d+)\.png$");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    private readonly struct EffectFrame
    {
        public EffectFrame(string name, RectInt rect)
        {
            Name = name;
            Rect = rect;
        }

        public string Name { get; }
        public RectInt Rect { get; }
    }
}
