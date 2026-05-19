using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Assets/Resources/ItemIcons に置かれたPNGを、同名plistのframe情報でSprite分割するEditor専用ツールです。
// アイテムアイコンがスプライトシート全体で表示される事故を防ぐため、Editor起動時にも不足分だけ自動で実行します。
public static class ItemIconSpriteImporter
{
    private const string ItemIconFolder = "Assets/Resources/ItemIcons";
    private const string SessionKey = "AutoChessBossRush.ItemIconSpriteImporter.Checked";

    // 手動でアイコンを再スライスしたい時のメニューです。
    [MenuItem("Tools/AutoChess/Slice Item Icons From Plist")]
    public static void SliceItemIconsFromPlist()
    {
        if (!Directory.Exists(ItemIconFolder))
        {
            Debug.LogWarning($"Item icon folder was not found: {ItemIconFolder}");
            return;
        }

        int slicedCount = 0;
        string[] pngPaths = Directory.GetFiles(ItemIconFolder, "*.png", SearchOption.TopDirectoryOnly)
            .Select(path => path.Replace("\\", "/"))
            .OrderBy(path => path)
            .ToArray();

        for (int i = 0; i < pngPaths.Length; i++)
        {
            if (SliceIconIfPossible(pngPaths[i], force: true))
                slicedCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Sliced {slicedCount}/{pngPaths.Length} item icon sheets from plist.");
    }

    // 指定PNGを、同名plistに従って分割します。
    private static bool SliceIconIfPossible(string pngPath, bool force)
    {
        string plistPath = Path.ChangeExtension(pngPath, ".plist")?.Replace("\\", "/");
        if (string.IsNullOrEmpty(plistPath) || !File.Exists(plistPath))
        {
            Debug.LogWarning($"Item icon plist was not found for {pngPath}.");
            return false;
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        if (texture == null)
        {
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        }

        TextureImporter importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
        if (texture == null || importer == null)
            return false;

        List<IconPlistFrame> frames = LoadFrames(plistPath);
        if (frames.Count == 0)
            return false;

        if (!force && importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Multiple)
        {
            Sprite[] existingSprites = AssetDatabase.LoadAllAssetsAtPath(pngPath).OfType<Sprite>().ToArray();
            if (existingSprites.Length >= frames.Count)
                return false;
        }

        Dictionary<string, GUID> existingSpriteIds = GetExistingSpriteIds(importer);
        List<SpriteRect> spriteRects = new List<SpriteRect>();
        string sheetName = Path.GetFileNameWithoutExtension(pngPath);

        for (int i = 0; i < frames.Count; i++)
        {
            IconPlistFrame frame = frames[i];
            if (!TryCreateSpriteRect(texture, frame, out Rect rect))
                continue;

            string spriteName = Path.GetFileNameWithoutExtension(frame.Name);
            if (string.IsNullOrEmpty(spriteName))
                spriteName = $"{sheetName}_{i:D3}";

            spriteRects.Add(new SpriteRect
            {
                name = spriteName,
                spriteID = GetSpriteId(existingSpriteIds, spriteName),
                rect = rect,
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            });
        }

        if (spriteRects.Count == 0)
        {
            Debug.LogWarning($"No item icon frames could be sliced from {pngPath}.");
            return false;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.spritePixelsPerUnit = 100f;
        ApplySpriteImporterSettings(importer);
        EditorUtility.SetDirty(importer);

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
        return true;
    }

    // plistから全frame情報を読みます。
    private static List<IconPlistFrame> LoadFrames(string plistPath)
    {
        List<IconPlistFrame> frames = new List<IconPlistFrame>();
        XDocument document = XDocument.Load(plistPath);
        XElement rootDictionary = document.Root?.Element("dict");
        XElement framesDictionary = GetDictionaryValue(rootDictionary, "frames");
        if (framesDictionary == null)
            return frames;

        List<XElement> children = framesDictionary.Elements().ToList();
        for (int i = 0; i < children.Count - 1; i += 2)
        {
            if (children[i].Name.LocalName != "key")
                continue;

            string frameName = children[i].Value;
            XElement frameDictionary = children[i + 1];
            string frameValue = GetStringValue(frameDictionary, "frame");
            bool rotated = GetBoolValue(frameDictionary, "rotated");

            if (!TryParsePlistRect(frameValue, out RectInt rect))
                continue;

            frames.Add(new IconPlistFrame(frameName, rect, rotated));
        }

        return frames
            .OrderBy(frame => GetFrameGroupPriority(frame.Name))
            .ThenBy(frame => ExtractTrailingNumber(frame.Name))
            .ThenBy(frame => frame.Name)
            .ToList();
    }

    // dict内で指定keyに続くdict要素を返します。
    private static XElement GetDictionaryValue(XElement dictionary, string key)
    {
        if (dictionary == null)
            return null;

        List<XElement> children = dictionary.Elements().ToList();
        for (int i = 0; i < children.Count - 1; i++)
        {
            if (children[i].Name.LocalName == "key" && children[i].Value == key && children[i + 1].Name.LocalName == "dict")
                return children[i + 1];
        }

        return null;
    }

    // dict内で指定keyに続くstring要素を返します。
    private static string GetStringValue(XElement dictionary, string key)
    {
        if (dictionary == null)
            return string.Empty;

        List<XElement> children = dictionary.Elements().ToList();
        for (int i = 0; i < children.Count - 1; i++)
        {
            if (children[i].Name.LocalName == "key" && children[i].Value == key && children[i + 1].Name.LocalName == "string")
                return children[i + 1].Value;
        }

        return string.Empty;
    }

    // dict内で指定keyに続くtrue/false要素を返します。
    private static bool GetBoolValue(XElement dictionary, string key)
    {
        if (dictionary == null)
            return false;

        List<XElement> children = dictionary.Elements().ToList();
        for (int i = 0; i < children.Count - 1; i++)
        {
            if (children[i].Name.LocalName == "key" && children[i].Value == key)
                return children[i + 1].Name.LocalName == "true";
        }

        return false;
    }

    // "{{x,y},{w,h}}" 形式のplist座標を読みます。
    private static bool TryParsePlistRect(string value, out RectInt rect)
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

    // plistの左上原点座標を、Unityの左下原点座標へ変換します。
    private static bool TryCreateSpriteRect(Texture2D texture, IconPlistFrame frame, out Rect rect)
    {
        rect = default;
        if (frame.Rotated)
        {
            Debug.LogWarning($"Rotated item icon frame is not supported and was skipped: {frame.Name}");
            return false;
        }

        RectInt atlasFrame = frame.AtlasFrame;
        if (atlasFrame.x < 0 || atlasFrame.y < 0 || atlasFrame.width <= 0 || atlasFrame.height <= 0 ||
            atlasFrame.xMax > texture.width || atlasFrame.yMax > texture.height)
        {
            Debug.LogWarning($"Item icon frame is outside the texture and was skipped: {frame.Name} {atlasFrame}");
            return false;
        }

        int unityY = texture.height - atlasFrame.y - atlasFrame.height;
        rect = new Rect(atlasFrame.x, unityY, atlasFrame.width, atlasFrame.height);
        return unityY >= 0;
    }

    // 既存SpriteのGUIDを維持して、参照切れを減らします。
    private static Dictionary<string, GUID> GetExistingSpriteIds(TextureImporter importer)
    {
        Dictionary<string, GUID> spriteIds = new Dictionary<string, GUID>();
        SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
        if (dataProvider == null)
            return spriteIds;

        dataProvider.InitSpriteEditorDataProvider();
        foreach (SpriteRect spriteRect in dataProvider.GetSpriteRects())
        {
            if (!string.IsNullOrEmpty(spriteRect.name) && !spriteIds.ContainsKey(spriteRect.name))
                spriteIds.Add(spriteRect.name, spriteRect.spriteID);
        }

        return spriteIds;
    }

    // 既存IDがなければ新しいGUIDを発行します。
    private static GUID GetSpriteId(Dictionary<string, GUID> existingSpriteIds, string spriteName)
    {
        return existingSpriteIds.TryGetValue(spriteName, out GUID spriteId)
            ? spriteId
            : GUID.Generate();
    }

    // FullRectにして、透明部分もアイコンとして自然に扱います。
    private static void ApplySpriteImporterSettings(TextureImporter importer)
    {
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
    }

    // 名前末尾の番号でフレーム順を揃えます。
    private static int ExtractTrailingNumber(string value)
    {
        Match match = Regex.Match(value ?? string.Empty, @"_(\d+)(?:\.png)?$");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    // 通常フレームを先、activeフレームを後にして、plist内の用途別アニメーションが混ざらないようにします。
    private static int GetFrameGroupPriority(string value)
    {
        return (value ?? string.Empty).Contains("_active_") ? 1 : 0;
    }

    private readonly struct IconPlistFrame
    {
        public IconPlistFrame(string name, RectInt atlasFrame, bool rotated)
        {
            Name = name;
            AtlasFrame = atlasFrame;
            Rotated = rotated;
        }

        public string Name { get; }
        public RectInt AtlasFrame { get; }
        public bool Rotated { get; }
    }
}

// Editor起動・スクリプト再読み込み後に、未分割のアイコンだけ自動でスライスします。
[InitializeOnLoad]
public static class ItemIconSpriteImporterAutoRunner
{
    static ItemIconSpriteImporterAutoRunner()
    {
        EditorApplication.delayCall += SliceOncePerSession;
    }

    private static void SliceOncePerSession()
    {
        if (SessionState.GetBool("AutoChessBossRush.ItemIconSpriteImporter.Checked", false))
            return;

        SessionState.SetBool("AutoChessBossRush.ItemIconSpriteImporter.Checked", true);
        ItemIconSpriteImporter.SliceItemIconsFromPlist();
    }
}
