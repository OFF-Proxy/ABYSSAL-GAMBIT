using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class UnitAnimationBuilder
{
    private const byte AlphaThreshold = 10;
    private const int MinimumRunLength = 3;
    private const int MinimumFrameSize = 32;
    private const float FramesPerSecond = 12f;
    private const string Team1OutlineMaterialPath = "Assets/Materials/UnitOutlineTeam1.mat";
    private const string Team2OutlineMaterialPath = "Assets/Materials/UnitOutlineTeam2.mat";
    private const string EntityDatabasePath = "Assets/Resources/Entity Database.asset";
    private const string OutlineShaderName = "AutoChess/SpriteOutline";

    private static readonly Dictionary<string, string> PlistAnimationPrefixes = new Dictionary<string, string>
    {
        { "Default", "breathing" },
        { "Move", "run" },
        { "Attack", "attack" },
        { "Ability", "cast" },
        { "Dead", "death" }
    };

    private static readonly UnitAnimationSpec[] UnitSpecs =
    {
    };

    private static readonly PlistAtlasAnimationSpec[] PlistAtlasSpecs =
    {
        new PlistAtlasAnimationSpec("Andromeda", "boss_andromeda"),
        new PlistAtlasAnimationSpec("Antiswarm", "boss_antiswarm"),
        new PlistAtlasAnimationSpec("Borealjuggernaut", "boss_borealjuggernaut"),
        new PlistAtlasAnimationSpec("Chaosknight", "boss_chaosknight"),
        new PlistAtlasAnimationSpec("Christmas", "boss_christmas", baseScale: 1.18f),
        new PlistAtlasAnimationSpec("vampire", "boss_vampire"),
        new PlistAtlasAnimationSpec("valiant", "boss_valiant", baseScale: 1.18f),
        new PlistAtlasAnimationSpec("Candypanda", "boss_candypanda", "T2", 2, 1, "Assets/Prefabs/Unit/T1/Borealjuggernaut.prefab", 28, 180, 0.95f, 0.95f, 1.25f),
        new PlistAtlasAnimationSpec("City", "boss_city", "T2", 2, 1, "Assets/Prefabs/Unit/T1/Borealjuggernaut.prefab", 24, 210, 0.9f, 0.85f, 1.18f),
        new PlistAtlasAnimationSpec("Crystal", "boss_crystal", "T2", 2, 4, "Assets/Prefabs/Unit/T1/Andromeda.prefab", 26, 120, 1.05f, 1f, 1.18f),
        new PlistAtlasAnimationSpec("Cindera", "boss_cindera", "T2", 2, 4, "Assets/Prefabs/Unit/T1/Andromeda.prefab", 32, 110, 0.9f, 1f),
        new PlistAtlasAnimationSpec("Decepticle", "boss_decepticle", "T2", 2, 1, "Assets/Prefabs/Unit/T1/Chaosknight.prefab", 38, 130, 1.15f, 1.1f, 1.25f),
        new PlistAtlasAnimationSpec("Umbra", "boss_umbra", "T2", 2, 1, "Assets/Prefabs/Unit/T1/Borealjuggernaut.prefab", 30, 185, 0.95f, 1.05f, 1.2f),
        new PlistAtlasAnimationSpec("Spelleater", "boss_spelleater", "T2", 2, 4, "Assets/Prefabs/Unit/T1/Andromeda.prefab", 34, 115, 0.9f, 1f, 1.06f),
        new PlistAtlasAnimationSpec("Serpenti", "boss_serpenti", "T2", 2, 1, "Assets/Prefabs/Unit/T1/Chaosknight.prefab", 36, 150, 1.1f, 1.15f, 1.02f),
        new PlistAtlasAnimationSpec("Skindogehai", "f2_general_skindogehai", "T3", 3, 1, "Assets/Prefabs/Unit/T2/Serpenti.prefab", 220, 1800, 1f, 1f, 1.14f),
        new PlistAtlasAnimationSpec("Decepticleprime", "boss_decepticleprime", "T3", 3, 4, "Assets/Prefabs/Unit/T2/Crystal.prefab", 270, 1450, 1.12f, 0.95f, 1.18f),
        new PlistAtlasAnimationSpec("Decepticlechassis", "boss_decepticlechassis", "T3", 3, 1, "Assets/Prefabs/Unit/T2/Decepticle.prefab", 230, 1900, 0.95f, 0.95f, 1.3f)
    };

    [MenuItem("Tools/AutoChess/Build Listed Unit Animations")]
    public static void BuildListedUnitAnimations()
    {
        int builtCount = 0;
        foreach (UnitAnimationSpec spec in UnitSpecs)
        {
            if (BuildUnitAnimations(spec))
                builtCount++;
        }

        foreach (PlistAtlasAnimationSpec spec in PlistAtlasSpecs)
        {
            if (BuildPlistAtlasAnimationsInternal(spec))
                builtCount++;
        }

        SyncEntityDatabase();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Built animation controllers for {builtCount}/{UnitSpecs.Length + PlistAtlasSpecs.Length} listed units.");
    }

    [MenuItem("Tools/AutoChess/Sync Entity Database")]
    public static void SyncEntityDatabaseMenu()
    {
        SyncEntityDatabase();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/AutoChess/Build Andromeda Animations")]
    public static void BuildAndromedaAnimations()
    {
        if (BuildPlistAtlasAnimationsInternal(GetPlistAtlasSpec("Andromeda")))
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    [MenuItem("Tools/AutoChess/Build Antiswarm Animations")]
    public static void BuildAntiswarmAnimations()
    {
        if (BuildPlistAtlasAnimationsInternal(GetPlistAtlasSpec("Antiswarm")))
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    [MenuItem("Tools/AutoChess/Build Borealjuggernaut Animations")]
    public static void BuildBorealjuggernautAnimations()
    {
        if (BuildPlistAtlasAnimationsInternal(GetPlistAtlasSpec("Borealjuggernaut")))
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    [MenuItem("Tools/AutoChess/Build Chaosknight Animations")]
    public static void BuildChaosknightAnimations()
    {
        if (BuildPlistAtlasAnimationsInternal(GetPlistAtlasSpec("Chaosknight")))
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    [MenuItem("Tools/AutoChess/Build Christmas Animations")]
    public static void BuildChristmasAnimations()
    {
        if (BuildPlistAtlasAnimationsInternal(GetPlistAtlasSpec("Christmas")))
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    [MenuItem("Tools/AutoChess/Build Vampire Atlas Animations")]
    public static void BuildVampireAtlasAnimations()
    {
        if (BuildPlistAtlasAnimationsInternal(GetPlistAtlasSpec("vampire")))
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    [MenuItem("Tools/AutoChess/Build Valiant Atlas Animations")]
    public static void BuildValiantAtlasAnimations()
    {
        if (BuildPlistAtlasAnimationsInternal(GetPlistAtlasSpec("valiant")))
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    [MenuItem("Tools/AutoChess/Clear Console")]
    public static void ClearConsole()
    {
        Type logEntriesType = Type.GetType("UnityEditor.LogEntries,UnityEditor.dll");
        MethodInfo clearMethod = logEntriesType?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        clearMethod?.Invoke(null, null);
    }

    private static bool BuildUnitAnimations(UnitAnimationSpec spec)
    {
        EnsureOutlineMaterialAssets();
        Directory.CreateDirectory(spec.AnimationFolder);

        Sprite[] idleSprites = SliceAndLoadSprites(spec, "Default", null, true, out FrameGrid defaultGrid);
        if (idleSprites.Length == 0)
        {
            Debug.LogError($"{spec.UnitName} animation build failed: no default sprites were generated.");
            return false;
        }

        Sprite[] moveSprites = SliceAndLoadSprites(spec, "Move", defaultGrid, true, out FrameGrid moveGrid);
        Sprite[] attackSprites = SliceAndLoadSprites(spec, "Attack", defaultGrid, true, out FrameGrid attackGrid);
        Sprite[] abilitySprites = SliceAndLoadSprites(spec, "Ability", defaultGrid, true, out FrameGrid abilityGrid);
        Sprite[] deadSprites = SliceAndLoadSprites(spec, "Dead", defaultGrid, true, out FrameGrid deadGrid);

        if (moveSprites.Length == 0)
            moveSprites = idleSprites;
        if (attackSprites.Length == 0)
            attackSprites = idleSprites;
        if (abilitySprites.Length == 0)
            abilitySprites = attackSprites;
        if (deadSprites.Length == 0)
            deadSprites = idleSprites;

        AnimationClip idleClip = CreateSpriteClip(spec.GetClipPath("Default"), idleSprites, true);
        AnimationClip moveClip = CreateSpriteClip(spec.GetClipPath("Move"), moveSprites, true);
        AnimationClip attackClip = CreateSpriteClip(spec.GetClipPath("Attack"), attackSprites, false);
        AnimationClip abilityClip = CreateSpriteClip(spec.GetClipPath("Ability"), abilitySprites, false);
        AnimationClip deadClip = CreateSpriteClip(spec.GetClipPath("Dead"), deadSprites, false);
        AnimatorController controller = CreateController(spec.ControllerPath, idleClip, moveClip, attackClip, abilityClip, deadClip);

        ApplyToPrefab(spec.PrefabPath, spec.UnitName, idleSprites[0], controller);

        Debug.Log($"{spec.UnitName}: idle {idleSprites.Length} ({defaultGrid}), move {moveSprites.Length} ({moveGrid}), attack {attackSprites.Length} ({attackGrid}), ability {abilitySprites.Length} ({abilityGrid}), dead {deadSprites.Length} ({deadGrid}) frames.");
        return true;
    }

    private static bool BuildPlistAtlasAnimationsInternal(PlistAtlasAnimationSpec atlasSpec)
    {
        EnsureOutlineMaterialAssets();
        UnitAnimationSpec spec = new UnitAnimationSpec(atlasSpec);
        Directory.CreateDirectory(spec.AnimationFolder);

        if (!EnsurePrefabAsset(atlasSpec))
            return false;

        Dictionary<string, Sprite[]> spritesByAnimation = SliceAndLoadPlistAtlasSprites(atlasSpec);
        Sprite[] idleSprites = GetAnimationSprites(spritesByAnimation, "Default");
        if (idleSprites.Length == 0)
        {
            Debug.LogError($"{spec.UnitName} animation build failed: no default sprites were generated from {atlasSpec.AtlasPath}.");
            return false;
        }

        Sprite[] moveSprites = GetAnimationSprites(spritesByAnimation, "Move");
        Sprite[] attackSprites = GetAnimationSprites(spritesByAnimation, "Attack");
        Sprite[] abilitySprites = GetAnimationSprites(spritesByAnimation, "Ability");
        Sprite[] deadSprites = GetAnimationSprites(spritesByAnimation, "Dead");

        if (moveSprites.Length == 0)
            moveSprites = idleSprites;
        if (attackSprites.Length == 0)
            attackSprites = idleSprites;
        if (abilitySprites.Length == 0)
            abilitySprites = attackSprites;
        if (deadSprites.Length == 0)
            deadSprites = idleSprites;

        AnimationClip idleClip = CreateSpriteClip(spec.GetClipPath("Default"), idleSprites, true);
        AnimationClip moveClip = CreateSpriteClip(spec.GetClipPath("Move"), moveSprites, true);
        AnimationClip attackClip = CreateSpriteClip(spec.GetClipPath("Attack"), attackSprites, false);
        AnimationClip abilityClip = CreateSpriteClip(spec.GetClipPath("Ability"), abilitySprites, false);
        AnimationClip deadClip = CreateSpriteClip(spec.GetClipPath("Dead"), deadSprites, false);
        AnimatorController controller = CreateController(spec.ControllerPath, idleClip, moveClip, attackClip, abilityClip, deadClip);

        ApplyToPrefab(spec.PrefabPath, spec.UnitName, idleSprites[0], controller, atlasSpec);

        Debug.Log($"{spec.UnitName}: atlas idle {idleSprites.Length}, move {moveSprites.Length}, attack {attackSprites.Length}, ability {abilitySprites.Length}, dead {deadSprites.Length} frames.");
        return true;
    }

    private static Dictionary<string, Sprite[]> SliceAndLoadPlistAtlasSprites(PlistAtlasAnimationSpec spec)
    {
        Dictionary<string, Sprite[]> result = spec.AnimationPrefixes.Keys.ToDictionary(name => name, _ => Array.Empty<Sprite>());
        Texture2D texture = LoadTextureFromDisk(spec.AtlasPath);
        if (texture == null)
        {
            Debug.LogWarning($"Sprite sheet not found or unreadable: {spec.AtlasPath}");
            return result;
        }

        Dictionary<string, List<PlistFrame>> framesByPrefix = LoadPlistFrames(spec);
        if (framesByPrefix.Count == 0)
            return result;

        TextureImporter importer = AssetImporter.GetAtPath(spec.AtlasPath) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(spec.AtlasPath, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(spec.AtlasPath) as TextureImporter;
        }

        if (importer == null)
            return result;

        Dictionary<string, GUID> existingSpriteIds = GetExistingSpriteIds(importer);
        List<SpriteRect> spriteRects = new List<SpriteRect>();
        Dictionary<string, List<string>> namesByAnimation = spec.AnimationPrefixes.Keys.ToDictionary(name => name, _ => new List<string>());

        foreach (KeyValuePair<string, string> animation in spec.AnimationPrefixes)
        {
            List<PlistFrame> frames = GetPlistFramesForAnimation(framesByPrefix, spec, animation.Key);
            if (frames.Count == 0)
            {
                Debug.LogWarning($"{spec.UnitName} atlas prefix not found in plist: {string.Join(", ", GetPlistPrefixesForAnimation(spec, animation.Key))}");
                continue;
            }

            int spriteIndex = 0;
            foreach (PlistFrame frame in frames)
            {
                if (!TryCreateSpriteRectFromPlistFrame(texture, frame, out Rect spriteRect))
                    continue;

                int x = Mathf.RoundToInt(spriteRect.x);
                int y = Mathf.RoundToInt(spriteRect.y);
                int width = Mathf.RoundToInt(spriteRect.width);
                int height = Mathf.RoundToInt(spriteRect.height);
                if (!HasVisiblePixels(texture, x, y, width, height))
                    continue;

                string spriteName = $"{spec.UnitName}_{animation.Key}_{spriteIndex:D3}";
                spriteRects.Add(new SpriteRect
                {
                    name = spriteName,
                    spriteID = GetSpriteId(existingSpriteIds, spriteName),
                    rect = spriteRect,
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                });

                namesByAnimation[animation.Key].Add(spriteName);
                spriteIndex++;
            }
        }

        if (spriteRects.Count == 0)
        {
            Debug.LogWarning($"No visible atlas frames were found in {spec.AtlasPath}.");
            return result;
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

        Dictionary<string, Sprite> spritesByName = AssetDatabase.LoadAllAssetsAtPath(spec.AtlasPath)
            .OfType<Sprite>()
            .ToDictionary(sprite => sprite.name, sprite => sprite);

        foreach (KeyValuePair<string, List<string>> animation in namesByAnimation)
        {
            result[animation.Key] = animation.Value
                .Where(spritesByName.ContainsKey)
                .Select(name => spritesByName[name])
                .ToArray();
        }

        return result;
    }

    private static PlistAtlasAnimationSpec GetPlistAtlasSpec(string unitName)
    {
        return PlistAtlasSpecs.First(spec => string.Equals(spec.UnitName, unitName, StringComparison.OrdinalIgnoreCase));
    }

    private static Sprite[] GetAnimationSprites(Dictionary<string, Sprite[]> spritesByAnimation, string animationName)
    {
        return spritesByAnimation.TryGetValue(animationName, out Sprite[] sprites) ? sprites : Array.Empty<Sprite>();
    }

    private static Dictionary<string, List<PlistFrame>> LoadPlistFrames(PlistAtlasAnimationSpec spec)
    {
        Dictionary<string, List<PlistFrame>> framesByPrefix = new Dictionary<string, List<PlistFrame>>();
        if (!File.Exists(spec.PlistPath))
        {
            Debug.LogWarning($"{spec.UnitName} plist not found: {spec.PlistPath}");
            return framesByPrefix;
        }

        XDocument document;
        XmlReaderSettings readerSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
        using (XmlReader reader = XmlReader.Create(spec.PlistPath, readerSettings))
        {
            document = XDocument.Load(reader);
        }

        XElement rootDictionary = document.Root?.Element("dict");
        XElement framesDictionary = GetPlistDictionaryValue(rootDictionary, "frames");
        if (framesDictionary == null)
        {
            Debug.LogWarning($"{spec.UnitName} plist has no frames dictionary: {spec.PlistPath}");
            return framesByPrefix;
        }

        HashSet<string> knownPrefixes = GetKnownPlistPrefixes(spec);
        string frameNamePattern = $"^{Regex.Escape(spec.SourcePrefix)}_(?<prefix>[a-z]+)_(?<index>\\d+)\\.png$";
        List<XElement> frameEntries = framesDictionary.Elements().ToList();
        for (int i = 0; i < frameEntries.Count - 1; i += 2)
        {
            XElement nameElement = frameEntries[i];
            XElement dictionaryElement = frameEntries[i + 1];
            if (nameElement.Name.LocalName != "key" || dictionaryElement.Name.LocalName != "dict")
                continue;

            string spriteName = nameElement.Value;
            Match nameMatch = Regex.Match(spriteName, frameNamePattern, RegexOptions.IgnoreCase);
            if (!nameMatch.Success)
                continue;

            string prefix = nameMatch.Groups["prefix"].Value.ToLowerInvariant();
            if (!knownPrefixes.Contains(prefix))
                continue;

            string frameValue = GetPlistStringValue(dictionaryElement, "frame");
            if (!TryParsePlistRect(frameValue, out RectInt frameRect))
                continue;

            bool rotated = GetPlistBooleanValue(dictionaryElement, "rotated");
            if (!framesByPrefix.TryGetValue(prefix, out List<PlistFrame> frames))
            {
                frames = new List<PlistFrame>();
                framesByPrefix[prefix] = frames;
            }

            frames.Add(new PlistFrame(
                spriteName,
                prefix,
                int.Parse(nameMatch.Groups["index"].Value),
                frameRect,
                rotated));
        }

        return framesByPrefix;
    }

    private static List<PlistFrame> GetPlistFramesForAnimation(Dictionary<string, List<PlistFrame>> framesByPrefix, PlistAtlasAnimationSpec spec, string animationName)
    {
        List<PlistFrame> frames = new List<PlistFrame>();
        foreach (string prefix in GetPlistPrefixesForAnimation(spec, animationName))
        {
            if (!framesByPrefix.TryGetValue(prefix, out List<PlistFrame> prefixFrames))
                continue;

            frames.AddRange(prefixFrames.OrderBy(frame => frame.Index));
        }

        return frames;
    }

    private static IEnumerable<string> GetPlistPrefixesForAnimation(PlistAtlasAnimationSpec spec, string animationName)
    {
        if (string.Equals(animationName, "Ability", StringComparison.OrdinalIgnoreCase))
        {
            yield return "caststart";
            yield return "cast";
            yield return "castloop";
            yield return "castend";
            yield break;
        }

        if (spec.AnimationPrefixes.TryGetValue(animationName, out string prefix))
            yield return prefix;
    }

    private static HashSet<string> GetKnownPlistPrefixes(PlistAtlasAnimationSpec spec)
    {
        HashSet<string> prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string animationName in spec.AnimationPrefixes.Keys)
        {
            foreach (string prefix in GetPlistPrefixesForAnimation(spec, animationName))
                prefixes.Add(prefix);
        }

        return prefixes;
    }

    private static XElement GetPlistDictionaryValue(XElement dictionaryElement, string key)
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

    private static string GetPlistStringValue(XElement dictionaryElement, string key)
    {
        List<XElement> children = dictionaryElement.Elements().ToList();
        for (int i = 0; i < children.Count - 1; i++)
        {
            if (children[i].Name.LocalName == "key" && children[i].Value == key && children[i + 1].Name.LocalName == "string")
                return children[i + 1].Value;
        }

        return string.Empty;
    }

    private static bool GetPlistBooleanValue(XElement dictionaryElement, string key)
    {
        List<XElement> children = dictionaryElement.Elements().ToList();
        for (int i = 0; i < children.Count - 1; i++)
        {
            if (children[i].Name.LocalName != "key" || children[i].Value != key)
                continue;

            return children[i + 1].Name.LocalName == "true";
        }

        return false;
    }

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

    private static bool TryCreateSpriteRectFromPlistFrame(Texture2D texture, PlistFrame frame, out Rect spriteRect)
    {
        spriteRect = default;

        if (frame.Rotated)
        {
            Debug.LogWarning($"Rotated atlas frame is not supported yet and was skipped: {frame.Name}");
            return false;
        }

        RectInt atlasFrame = frame.AtlasFrame;
        if (atlasFrame.x < 0 || atlasFrame.y < 0 || atlasFrame.width <= 0 || atlasFrame.height <= 0 ||
            atlasFrame.xMax > texture.width || atlasFrame.yMax > texture.height)
        {
            Debug.LogWarning($"Atlas frame is outside the texture and was skipped: {frame.Name} {atlasFrame}");
            return false;
        }

        int unityY = texture.height - atlasFrame.y - atlasFrame.height;
        spriteRect = new Rect(atlasFrame.x, unityY, atlasFrame.width, atlasFrame.height);
        return unityY >= 0;
    }


    private static Sprite[] SliceAndLoadSprites(UnitAnimationSpec spec, string animationName, FrameGrid fallbackGrid, bool warnIfMissing, out FrameGrid detectedGrid)
    {
        detectedGrid = fallbackGrid;
        string texturePath = FindSpriteSheetPath(spec.SpriteFolder, spec.UnitName, animationName);
        if (string.IsNullOrEmpty(texturePath))
        {
            if (warnIfMissing)
                Debug.LogWarning($"{spec.UnitName} {animationName} sprite sheet not found in {spec.SpriteFolder}.");
            return Array.Empty<Sprite>();
        }

        Texture2D texture = LoadTextureFromDisk(texturePath);
        if (texture == null)
        {
            Debug.LogWarning($"Sprite sheet not found or unreadable: {texturePath}");
            return Array.Empty<Sprite>();
        }

        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null)
            return Array.Empty<Sprite>();

        FrameGrid frameGrid = DetectFrameGrid(texture, fallbackGrid);
        detectedGrid = frameGrid;

        Dictionary<string, GUID> existingSpriteIds = GetExistingSpriteIds(importer);
        List<SpriteRect> sprites = new List<SpriteRect>();
        int rows = texture.height / frameGrid.FrameHeight;
        int columns = texture.width / frameGrid.FrameWidth;
        int index = 0;

        for (int row = 0; row < rows; row++)
        {
            int y = texture.height - ((row + 1) * frameGrid.FrameHeight);
            for (int column = 0; column < columns; column++)
            {
                int x = column * frameGrid.FrameWidth;
                if (!HasVisiblePixels(texture, x, y, frameGrid.FrameWidth, frameGrid.FrameHeight))
                    continue;

                sprites.Add(new SpriteRect
                {
                    name = $"{spec.UnitName}_{animationName}_{index}",
                    spriteID = GetSpriteId(existingSpriteIds, $"{spec.UnitName}_{animationName}_{index}"),
                    rect = new Rect(x, y, frameGrid.FrameWidth, frameGrid.FrameHeight),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                });
                index++;
            }
        }

        if (sprites.Count == 0)
        {
            Debug.LogWarning($"No visible {frameGrid} frames were found in {texturePath}.");
            return Array.Empty<Sprite>();
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
        dataProvider.SetSpriteRects(sprites.ToArray());

        ISpriteNameFileIdDataProvider nameFileIdDataProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nameFileIdDataProvider != null)
        {
            List<SpriteNameFileIdPair> nameFileIdPairs = sprites
                .Select(sprite => new SpriteNameFileIdPair(sprite.name, sprite.spriteID))
                .ToList();
            nameFileIdDataProvider.SetNameFileIdPairs(nameFileIdPairs);
        }

        dataProvider.Apply();
        importer.SaveAndReimport();

        return AssetDatabase.LoadAllAssetsAtPath(texturePath)
            .OfType<Sprite>()
            .OrderBy(sprite => ExtractTrailingNumber(sprite.name))
            .ThenBy(sprite => sprite.name)
            .ToArray();
    }

    private static Dictionary<string, GUID> GetExistingSpriteIds(TextureImporter importer)
    {
        Dictionary<string, GUID> spriteIds = new Dictionary<string, GUID>();
        if (importer == null)
            return spriteIds;

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

    private static GUID GetSpriteId(Dictionary<string, GUID> existingSpriteIds, string spriteName)
    {
        if (existingSpriteIds != null && existingSpriteIds.TryGetValue(spriteName, out GUID spriteId))
            return spriteId;

        return GUID.Generate();
    }

    private static FrameGrid DetectFrameGrid(Texture2D texture, FrameGrid fallbackGrid)
    {
        List<AxisRun> xRuns = GetMajorAlphaRuns(texture, true);
        List<AxisRun> yRuns = GetMajorAlphaRuns(texture, false);

        int fallbackWidth = fallbackGrid?.FrameWidth ?? 0;
        int fallbackHeight = fallbackGrid?.FrameHeight ?? 0;

        int frameWidth = InferCellSize(texture.width, xRuns, fallbackWidth, fallbackHeight);
        int frameHeight = InferCellSize(texture.height, yRuns, fallbackHeight, frameWidth);

        if (frameWidth <= 0 && frameHeight > 0)
            frameWidth = frameHeight;
        if (frameHeight <= 0 && frameWidth > 0)
            frameHeight = frameWidth;

        frameWidth = Mathf.Clamp(frameWidth, MinimumFrameSize, texture.width);
        frameHeight = Mathf.Clamp(frameHeight, MinimumFrameSize, texture.height);

        return new FrameGrid(frameWidth, frameHeight);
    }

    private static List<AxisRun> GetMajorAlphaRuns(Texture2D texture, bool xAxis)
    {
        int axisLength = xAxis ? texture.width : texture.height;
        bool[] hasAlpha = new bool[axisLength];
        Color32[] pixels = texture.GetPixels32();

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                if (pixels[y * texture.width + x].a <= AlphaThreshold)
                    continue;

                hasAlpha[xAxis ? x : y] = true;
            }
        }

        List<AxisRun> runs = new List<AxisRun>();
        int start = -1;
        for (int i = 0; i < hasAlpha.Length; i++)
        {
            if (hasAlpha[i] && start == -1)
                start = i;

            bool isEnd = start != -1 && (!hasAlpha[i] || i == hasAlpha.Length - 1);
            if (!isEnd)
                continue;

            int end = hasAlpha[i] ? i : i - 1;
            if (end - start + 1 >= MinimumRunLength)
                runs.Add(new AxisRun(start, end));

            start = -1;
        }

        if (runs.Count <= 2)
            return runs;

        float medianLength = Median(runs.Select(run => (float)run.Length).ToList());
        float minimumMajorLength = Mathf.Max(8f, medianLength * 0.25f);
        return runs.Where(run => run.Length >= minimumMajorLength).ToList();
    }

    private static int InferCellSize(int textureLength, List<AxisRun> runs, int fallbackSize, int secondaryFallbackSize)
    {
        int spacing = InferRunSpacing(runs);
        if (spacing > 0)
        {
            if (fallbackSize > 0 && textureLength % fallbackSize == 0)
            {
                float allowedFallbackDifference = Mathf.Max(8f, spacing * 0.1f);
                if (Mathf.Abs(fallbackSize - spacing) <= allowedFallbackDifference)
                    return fallbackSize;
            }

            int byRunCount = TryInferFromRunCount(textureLength, runs.Count, spacing);
            if (byRunCount > 0)
                return byRunCount;

            int nearbyDivisor = FindNearbyDivisor(textureLength, spacing);
            if (nearbyDivisor > 0)
                return nearbyDivisor;

            return spacing;
        }

        if (fallbackSize > 0 && textureLength % fallbackSize == 0)
            return fallbackSize;

        if (secondaryFallbackSize > 0 && textureLength % secondaryFallbackSize == 0)
            return secondaryFallbackSize;

        if (fallbackSize > 0)
            return fallbackSize;

        return textureLength;
    }

    private static int InferRunSpacing(List<AxisRun> runs)
    {
        if (runs.Count < 2)
            return 0;

        List<float> spacings = new List<float>();
        for (int i = 1; i < runs.Count; i++)
        {
            float spacing = runs[i].Center - runs[i - 1].Center;
            if (spacing >= MinimumFrameSize)
                spacings.Add(spacing);
        }

        if (spacings.Count == 0)
            return 0;

        return Mathf.Max(MinimumFrameSize, Mathf.RoundToInt(Median(spacings)));
    }

    private static int TryInferFromRunCount(int textureLength, int runCount, int spacing)
    {
        if (runCount <= 0 || textureLength % runCount != 0)
            return 0;

        int cellSize = textureLength / runCount;
        float allowedDifference = Mathf.Max(3f, spacing * 0.05f);
        return Mathf.Abs(cellSize - spacing) <= allowedDifference ? cellSize : 0;
    }

    private static int FindNearbyDivisor(int textureLength, int preferredSize)
    {
        for (int offset = 0; offset <= 6; offset++)
        {
            int smaller = preferredSize - offset;
            if (smaller >= MinimumFrameSize && textureLength % smaller == 0)
                return smaller;

            int larger = preferredSize + offset;
            if (larger >= MinimumFrameSize && textureLength % larger == 0)
                return larger;
        }

        return 0;
    }

    private static float Median(List<float> values)
    {
        if (values == null || values.Count == 0)
            return 0f;

        values.Sort();
        int middle = values.Count / 2;
        if (values.Count % 2 == 1)
            return values[middle];

        return (values[middle - 1] + values[middle]) * 0.5f;
    }

    private static string FindSpriteSheetPath(string spriteFolder, string unitName, string animationName)
    {
        if (!Directory.Exists(spriteFolder))
            return null;

        string expectedName = $"{unitName}_{animationName}";
        return Directory
            .GetFiles(spriteFolder, "*.png", SearchOption.TopDirectoryOnly)
            .Select(path => path.Replace("\\", "/"))
            .FirstOrDefault(path => string.Equals(Path.GetFileNameWithoutExtension(path), expectedName, StringComparison.OrdinalIgnoreCase));
    }

    private static Texture2D LoadTextureFromDisk(string texturePath)
    {
        if (!File.Exists(texturePath))
            return null;

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        return texture.LoadImage(File.ReadAllBytes(texturePath)) ? texture : null;
    }

    private static bool HasVisiblePixels(Texture2D texture, int startX, int startY, int width, int height)
    {
        Color32[] pixels = texture.GetPixels32();
        int textureWidth = texture.width;
        int endX = Mathf.Min(startX + width, texture.width);
        int endY = Mathf.Min(startY + height, texture.height);

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                if (pixels[y * textureWidth + x].a > AlphaThreshold)
                    return true;
            }
        }

        return false;
    }

    private static AnimationClip CreateSpriteClip(string clipPath, Sprite[] sprites, bool loop)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, clipPath);
        }

        clip.ClearCurves();
        clip.frameRate = FramesPerSecond;

        ObjectReferenceKeyframe[] frames = new ObjectReferenceKeyframe[sprites.Length + (loop ? 1 : 0)];
        for (int i = 0; i < sprites.Length; i++)
        {
            frames[i] = new ObjectReferenceKeyframe
            {
                time = i / FramesPerSecond,
                value = sprites[i]
            };
        }

        if (loop)
        {
            frames[frames.Length - 1] = new ObjectReferenceKeyframe
            {
                time = sprites.Length / FramesPerSecond,
                value = sprites[0]
            };
        }

        EditorCurveBinding binding = new EditorCurveBinding
        {
            path = "",
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };

        AnimationUtility.SetObjectReferenceCurve(clip, binding, frames);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorController CreateController(string controllerPath, AnimationClip idleClip, AnimationClip moveClip, AnimationClip attackClip, AnimationClip abilityClip, AnimationClip deadClip)
    {
        if (File.Exists(controllerPath))
            AssetDatabase.DeleteAsset(controllerPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("walking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("attacking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("abilitying", AnimatorControllerParameterType.Bool);
        controller.AddParameter("attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("ability", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("dead", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idle = stateMachine.AddState("Idle", new Vector3(250f, 120f, 0f));
        AnimatorState move = stateMachine.AddState("Move", new Vector3(500f, 120f, 0f));
        AnimatorState attack = stateMachine.AddState("Attack", new Vector3(500f, 250f, 0f));
        AnimatorState ability = stateMachine.AddState("Ability", new Vector3(740f, 250f, 0f));
        AnimatorState dead = stateMachine.AddState("Dead", new Vector3(250f, 330f, 0f));

        idle.motion = idleClip;
        move.motion = moveClip;
        attack.motion = attackClip;
        ability.motion = abilityClip;
        dead.motion = deadClip;
        stateMachine.defaultState = idle;

        AnimatorStateTransition idleToMove = idle.AddTransition(move);
        ConfigureInstantTransition(idleToMove);
        idleToMove.AddCondition(AnimatorConditionMode.If, 0f, "walking");

        AnimatorStateTransition moveToIdle = move.AddTransition(idle);
        ConfigureInstantTransition(moveToIdle);
        moveToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "walking");

        AnimatorStateTransition anyToAttackByTrigger = stateMachine.AddAnyStateTransition(attack);
        ConfigureInstantTransition(anyToAttackByTrigger);
        anyToAttackByTrigger.canTransitionToSelf = false;
        anyToAttackByTrigger.AddCondition(AnimatorConditionMode.If, 0f, "attack");

        AnimatorStateTransition anyToAttackByBool = stateMachine.AddAnyStateTransition(attack);
        ConfigureInstantTransition(anyToAttackByBool);
        anyToAttackByBool.canTransitionToSelf = false;
        anyToAttackByBool.AddCondition(AnimatorConditionMode.If, 0f, "attacking");

        AnimatorStateTransition attackToMove = attack.AddTransition(move);
        ConfigureInstantTransition(attackToMove);
        attackToMove.AddCondition(AnimatorConditionMode.IfNot, 0f, "attacking");
        attackToMove.AddCondition(AnimatorConditionMode.If, 0f, "walking");

        AnimatorStateTransition attackToIdle = attack.AddTransition(idle);
        ConfigureInstantTransition(attackToIdle);
        attackToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "attacking");
        attackToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "walking");

        AnimatorStateTransition anyToAbilityByTrigger = stateMachine.AddAnyStateTransition(ability);
        ConfigureInstantTransition(anyToAbilityByTrigger);
        anyToAbilityByTrigger.canTransitionToSelf = false;
        anyToAbilityByTrigger.AddCondition(AnimatorConditionMode.If, 0f, "ability");

        AnimatorStateTransition anyToAbilityByBool = stateMachine.AddAnyStateTransition(ability);
        ConfigureInstantTransition(anyToAbilityByBool);
        anyToAbilityByBool.canTransitionToSelf = false;
        anyToAbilityByBool.AddCondition(AnimatorConditionMode.If, 0f, "abilitying");

        AnimatorStateTransition abilityToMove = ability.AddTransition(move);
        ConfigureInstantTransition(abilityToMove);
        abilityToMove.AddCondition(AnimatorConditionMode.IfNot, 0f, "abilitying");
        abilityToMove.AddCondition(AnimatorConditionMode.If, 0f, "walking");

        AnimatorStateTransition abilityToIdle = ability.AddTransition(idle);
        ConfigureInstantTransition(abilityToIdle);
        abilityToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "abilitying");
        abilityToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "walking");

        AnimatorStateTransition anyToDead = stateMachine.AddAnyStateTransition(dead);
        ConfigureInstantTransition(anyToDead);
        anyToDead.canTransitionToSelf = false;
        anyToDead.AddCondition(AnimatorConditionMode.If, 0f, "dead");

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void ApplyToPrefab(string prefabPath, string unitName, Sprite defaultSprite, AnimatorController controller, PlistAtlasAnimationSpec atlasSpec = null)
    {
        if (!File.Exists(prefabPath))
        {
            Debug.LogWarning($"Prefab not found: {prefabPath}");
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        SpriteRenderer spriteRenderer = prefabRoot.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = prefabRoot.AddComponent<SpriteRenderer>();

        Animator animator = prefabRoot.GetComponent<Animator>();
        if (animator == null)
            animator = prefabRoot.AddComponent<Animator>();

        BaseEntity entity = prefabRoot.GetComponent<BaseEntity>();
        BoxCollider2D collider = prefabRoot.GetComponent<BoxCollider2D>();

        prefabRoot.name = unitName;
        prefabRoot.layer = LayerMask.NameToLayer("Unit");
        if (atlasSpec != null)
            prefabRoot.transform.localScale = Vector3.one * atlasSpec.BaseScale;

        spriteRenderer.sprite = defaultSprite;
        spriteRenderer.sortingOrder = 3;

        animator.enabled = true;
        animator.runtimeAnimatorController = controller;

        if (entity != null)
        {
            entity.spriteRender = spriteRenderer;
            entity.animator = animator;
            entity.range = atlasSpec != null && atlasSpec.Range > 0 ? atlasSpec.Range : GetDefaultRange(unitName);
            entity.team1OutlineMaterial = AssetDatabase.LoadAssetAtPath<Material>(Team1OutlineMaterialPath);
            entity.team2OutlineMaterial = AssetDatabase.LoadAssetAtPath<Material>(Team2OutlineMaterialPath);

            if (atlasSpec != null && atlasSpec.HasStats)
            {
                entity.baseDamage = atlasSpec.BaseDamage;
                entity.baseHealth = atlasSpec.BaseHealth;
                entity.attackSpeed = atlasSpec.AttackSpeed;
                entity.movementSpeed = atlasSpec.MovementSpeed;
            }
        }

        Material defaultOutlineMaterial = AssetDatabase.LoadAssetAtPath<Material>(Team1OutlineMaterialPath);
        if (defaultOutlineMaterial != null)
            spriteRenderer.sharedMaterial = defaultOutlineMaterial;

        if (collider != null)
        {
            collider.size = Vector2.one;
            collider.offset = Vector2.zero;
        }

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);
    }

    private static bool EnsurePrefabAsset(PlistAtlasAnimationSpec spec)
    {
        if (File.Exists(spec.PrefabPath))
            return true;

        string prefabFolder = Path.GetDirectoryName(spec.PrefabPath)?.Replace("\\", "/");
        if (string.IsNullOrEmpty(prefabFolder))
            return false;

        Directory.CreateDirectory(prefabFolder);
        AssetDatabase.Refresh();

        string templatePath = string.IsNullOrEmpty(spec.TemplatePrefabPath)
            ? "Assets/Prefabs/Unit/T1/Chaosknight.prefab"
            : spec.TemplatePrefabPath;

        if (!File.Exists(templatePath))
        {
            Debug.LogError($"{spec.UnitName} prefab template not found: {templatePath}");
            return false;
        }

        if (!AssetDatabase.CopyAsset(templatePath, spec.PrefabPath))
        {
            Debug.LogError($"{spec.UnitName} prefab could not be created from template {templatePath}.");
            return false;
        }

        AssetDatabase.ImportAsset(spec.PrefabPath, ImportAssetOptions.ForceUpdate);
        return true;
    }

    private static void SyncEntityDatabase()
    {
        EntitiesDatabaseSO database = AssetDatabase.LoadAssetAtPath<EntitiesDatabaseSO>(EntityDatabasePath);
        if (database == null)
        {
            Debug.LogWarning($"Entity database not found: {EntityDatabasePath}");
            return;
        }

        Dictionary<string, EntitiesDatabaseSO.EntityData> existingEntries = (database.allEntities ?? new List<EntitiesDatabaseSO.EntityData>())
            .Where(entry => !string.IsNullOrEmpty(entry.name))
            .GroupBy(entry => entry.name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        Sprite fallbackFrame = (database.allEntities ?? new List<EntitiesDatabaseSO.EntityData>())
            .Select(entry => entry.frame)
            .FirstOrDefault(frame => frame != null);

        List<EntitiesDatabaseSO.EntityData> entries = new List<EntitiesDatabaseSO.EntityData>();
        foreach (PlistAtlasAnimationSpec spec in PlistAtlasSpecs)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
            BaseEntity entityPrefab = prefab != null ? prefab.GetComponent<BaseEntity>() : null;
            if (entityPrefab == null)
            {
                Debug.LogWarning($"{spec.UnitName} was skipped in entity database sync because its prefab is missing a BaseEntity.");
                continue;
            }

            existingEntries.TryGetValue(spec.UnitName, out EntitiesDatabaseSO.EntityData existingEntry);
            Sprite icon = LoadIconForSpec(spec) ?? existingEntry.icon;
            Sprite frame = LoadFrameForSpec(spec) ?? existingEntry.frame ?? fallbackFrame;

            entries.Add(new EntitiesDatabaseSO.EntityData
            {
                prefab = entityPrefab,
                name = spec.UnitName,
                icon = icon,
                cost = spec.Cost,
                frame = frame
            });
        }

        database.allEntities = entries;
        EditorUtility.SetDirty(database);
        Debug.Log($"Entity database synced with {entries.Count} units.");
    }

    private static Sprite LoadIconForSpec(PlistAtlasAnimationSpec spec)
    {
        foreach (string iconName in GetIconCandidateNames(spec.UnitName))
        {
            Sprite icon = LoadSingleSpriteAsset($"Assets/Images/Units/Icon/{spec.TierFolder}/{iconName}.png");
            if (icon != null)
                return icon;

            icon = LoadSingleSpriteAsset($"Assets/Images/Units/Icon/{spec.TierFolder}/{iconName}.jpg");
            if (icon != null)
                return icon;

            icon = LoadSingleSpriteAsset($"Assets/Images/Units/Icon/{iconName}.png");
            if (icon != null)
                return icon;

            icon = LoadSingleSpriteAsset($"Assets/Images/Units/Icon/{iconName}.jpg");
            if (icon != null)
                return icon;
        }

        Sprite[] atlasSprites = AssetDatabase.LoadAllAssetsAtPath(spec.AtlasPath)
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name.IndexOf("_Default_", StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1)
            .ThenBy(sprite => ExtractTrailingNumber(sprite.name))
            .ThenBy(sprite => sprite.name)
            .ToArray();

        return atlasSprites.FirstOrDefault();
    }

    private static IEnumerable<string> GetIconCandidateNames(string unitName)
    {
        yield return unitName;

        if (string.Equals(unitName, "Borealjuggernaut", StringComparison.OrdinalIgnoreCase))
            yield return "Borealjuggernau";
    }

    private static Sprite LoadFrameForSpec(PlistAtlasAnimationSpec spec)
    {
        if (spec.Cost == 2)
            return LoadSingleSpriteAsset("Assets/Images/UI/Frame/T2_Comon.png");

        if (spec.Cost == 3)
            return LoadSingleSpriteAsset("Assets/Images/UI/Frame/T3_Rare.png");

        if (spec.Cost == 4)
            return LoadSingleSpriteAsset("Assets/Images/UI/Frame/T4_Epic.png");

        if (spec.Cost == 5)
            return LoadSingleSpriteAsset("Assets/Images/UI/Frame/T5_Prism.png");

        return LoadSingleSpriteAsset("Assets/Images/UI/Frame/T1_Nomal.png");
    }

    private static Sprite LoadSingleSpriteAsset(string path)
    {
        if (!File.Exists(path))
            return null;

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            ApplySpriteImporterSettings(importer);
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void EnsureOutlineMaterialAssets()
    {
        Directory.CreateDirectory("Assets/Materials");

        Shader outlineShader = Shader.Find(OutlineShaderName);
        if (outlineShader == null)
        {
            Debug.LogWarning($"Outline shader was not found: {OutlineShaderName}");
            return;
        }

        EnsureOutlineMaterial(Team1OutlineMaterialPath, outlineShader, new Color(0.15f, 1f, 0.2f, 1f));
        EnsureOutlineMaterial(Team2OutlineMaterialPath, outlineShader, new Color(1f, 0.1f, 0.08f, 1f));
    }

    private static void ApplySpriteImporterSettings(TextureImporter importer)
    {
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
    }

    private static void EnsureOutlineMaterial(string path, Shader shader, Color outlineColor)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.SetColor("_OutlineColor", outlineColor);
        material.SetFloat("_OutlineSize", 1.5f);
        material.SetFloat("_AlphaThreshold", 0.05f);
        EditorUtility.SetDirty(material);
    }

    private static int GetDefaultRange(string unitName)
    {
        if (string.Equals(unitName, "Andromeda", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitName, "Antiswarm", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitName, "vampire", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitName, "Crystal", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitName, "Cindera", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitName, "Spelleater", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unitName, "Decepticleprime", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        return 1;
    }

    private static void ConfigureInstantTransition(AnimatorStateTransition transition)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0f;
        transition.offset = 0f;
    }

    private static int ExtractTrailingNumber(string value)
    {
        Match match = Regex.Match(value, @"_(\d+)$");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    private sealed class UnitAnimationSpec
    {
        public UnitAnimationSpec(string unitName)
        {
            UnitName = unitName;
            SpriteFolder = $"Assets/Images/Units/Sprite/T1/{unitName}";
            AnimationFolder = $"Assets/Animations/{unitName}";
            PrefabPath = $"Assets/Prefabs/Unit/T1/{unitName}.prefab";
            ControllerPath = $"{AnimationFolder}/{unitName}.controller";
        }

        public UnitAnimationSpec(PlistAtlasAnimationSpec atlasSpec)
        {
            UnitName = atlasSpec.UnitName;
            SpriteFolder = atlasSpec.SpriteFolder;
            AnimationFolder = $"Assets/Animations/{atlasSpec.UnitName}";
            PrefabPath = atlasSpec.PrefabPath;
            ControllerPath = $"{AnimationFolder}/{atlasSpec.UnitName}.controller";
        }

        public string UnitName { get; }
        public string SpriteFolder { get; }
        public string AnimationFolder { get; }
        public string PrefabPath { get; }
        public string ControllerPath { get; }

        public string GetClipPath(string animationName)
        {
            return $"{AnimationFolder}/{UnitName}_{animationName}.anim";
        }
    }

    private sealed class PlistAtlasAnimationSpec
    {
        public PlistAtlasAnimationSpec(
            string unitName,
            string sourcePrefix,
            string tierFolder = "T1",
            int cost = 1,
            int range = -1,
            string templatePrefabPath = null,
            int baseDamage = -1,
            int baseHealth = -1,
            float attackSpeed = -1f,
            float movementSpeed = -1f,
            float baseScale = 1f)
        {
            UnitName = unitName;
            SourcePrefix = sourcePrefix;
            TierFolder = tierFolder;
            Cost = cost;
            Range = range > 0 ? range : GetDefaultRange(unitName);
            TemplatePrefabPath = templatePrefabPath;
            BaseDamage = baseDamage;
            BaseHealth = baseHealth;
            AttackSpeed = attackSpeed;
            MovementSpeed = movementSpeed;
            BaseScale = Mathf.Max(0.1f, baseScale);
            SpriteFolder = $"Assets/Images/Units/Sprite/{tierFolder}/{unitName}";
            PrefabPath = $"Assets/Prefabs/Unit/{tierFolder}/{unitName}.prefab";
            AtlasPath = $"{SpriteFolder}/{sourcePrefix}.png";
            PlistPath = $"{SpriteFolder}/{sourcePrefix}.plist";
            AnimationPrefixes = PlistAnimationPrefixes;
        }

        public string UnitName { get; }
        public string SourcePrefix { get; }
        public string TierFolder { get; }
        public int Cost { get; }
        public int Range { get; }
        public string TemplatePrefabPath { get; }
        public int BaseDamage { get; }
        public int BaseHealth { get; }
        public float AttackSpeed { get; }
        public float MovementSpeed { get; }
        public float BaseScale { get; }
        public string SpriteFolder { get; }
        public string PrefabPath { get; }
        public string AtlasPath { get; }
        public string PlistPath { get; }
        public Dictionary<string, string> AnimationPrefixes { get; }
        public bool HasStats => BaseDamage > 0 && BaseHealth > 0 && AttackSpeed > 0f && MovementSpeed > 0f;
    }

    private sealed class PlistFrame
    {
        public PlistFrame(string name, string prefix, int index, RectInt atlasFrame, bool rotated)
        {
            Name = name;
            Prefix = prefix;
            Index = index;
            AtlasFrame = atlasFrame;
            Rotated = rotated;
        }

        public string Name { get; }
        public string Prefix { get; }
        public int Index { get; }
        public RectInt AtlasFrame { get; }
        public bool Rotated { get; }
    }

    private sealed class FrameGrid
    {
        public FrameGrid(int frameWidth, int frameHeight)
        {
            FrameWidth = frameWidth;
            FrameHeight = frameHeight;
        }

        public int FrameWidth { get; }
        public int FrameHeight { get; }

        public override string ToString()
        {
            return $"{FrameWidth}x{FrameHeight}";
        }
    }

    private readonly struct AxisRun
    {
        public AxisRun(int start, int end)
        {
            Start = start;
            End = end;
        }

        public int Start { get; }
        public int End { get; }
        public int Length => End - Start + 1;
        public float Center => (Start + End) * 0.5f;
    }

}
