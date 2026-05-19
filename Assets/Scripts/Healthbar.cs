using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if UNITY_EDITOR
using UnityEditor;
#endif

// ユニット頭上のHPバー、シールドバー、MPバーを管理するクラスです。
// フレーム画像の内側にゲージを収め、ユニットの位置と描画順に追従させます。
public class HealthBar : MonoBehaviour
{
    // HP、シールド、MPのゲージ部分です。Transformの横スケールを変えて残量を表します。
    public Transform bar;
    public Transform shieldBar;
    public Transform manaBar;

    // ユニットから見たバー位置の追加オフセットです。
    public Vector3 offset;

    // フレーム、ゲージ、隙間を隠すマスクの描画コンポーネントです。
    public SpriteRenderer frameRenderer;
    public SpriteRenderer fillRenderer;
    public SpriteRenderer fillMaskRenderer;
    public SpriteRenderer shieldRenderer;
    public SpriteRenderer shieldMaskRenderer;
    public SpriteRenderer manaRenderer;
    public SpriteRenderer manaMaskRenderer;

    // バー全体のワールド上の大きさと、ユニット上部からの距離です。
    public Vector3 worldScale = new Vector3(0.2f, 0.4f, 1f);
    public float verticalPadding = 0.04f;

    // フレーム画像が無い場合に使う予備スケールや、ゲージの高さ設定です。
    public Vector3 frameScale = new Vector3(1.32f, 2.35f, 1f);
    public float fillHeightScale = 0.58f;

    // HP何ごとに区切り線を入れるか、その色と大きさです。
    public int separatorHealthStep = 500;
    public Color separatorColor = new Color(0.02f, 0.02f, 0.02f, 0.9f);
    public Vector3 separatorScale = new Vector3(0.025f, 0.82f, 1f);

    // ゲージの隙間を隠すためのマスク色と、各ゲージの色です。
    public Color fillMaskColor = new Color(0.02f, 0.025f, 0.04f, 1f);
    public Color shieldFillColor = new Color(1f, 1f, 1f, 0.95f);
    public Color shieldMaskColor = new Color(0.015f, 0.04f, 0.06f, 1f);
    public Color manaFillColor = new Color(0.05f, 0.58f, 1f, 1f);
    public Color manaMaskColor = new Color(0.015f, 0.02f, 0.05f, 1f);

    // フレーム画像が見つからない場合に色だけでスターを表すための予備色です。
    public Color star1FrameColor = new Color(0.92f, 0.96f, 1f, 1f);
    public Color star2FrameColor = new Color(0.1f, 0.65f, 1f, 1f);
    public Color star3FrameColor = new Color(1f, 0.76f, 0f, 1f);

    // ★1、★2、★3それぞれのHP/MPフレーム画像です。
    public Sprite star1FrameSprite;
    public Sprite star2FrameSprite;
    public Sprite star3FrameSprite;

    // フレームの横幅目標と、フレーム内のゲージ調整値です。
    public Vector2 frameTargetSize = new Vector2(7.28f, 0.78f);
    public Vector2 fillAreaPadding = new Vector2(-0.02f, -0.015f);
    public Vector2 fillMaskOverlap = new Vector2(0.05f, 0.03f);

    // フレーム画像の中で、HPゲージを入れる範囲です。x,y,z,w = 左,下,右,上 の比率です。
    public Vector4 star1FillRectNormalized = new Vector4(0.198f, 0.445f, 0.948f, 0.725f);
    public Vector4 star2FillRectNormalized = new Vector4(0.198f, 0.445f, 0.951f, 0.725f);
    public Vector4 star3FillRectNormalized = new Vector4(0.236f, 0.445f, 0.943f, 0.725f);

    // フレーム画像の中で、MPゲージを入れる範囲です。
    public Vector4 star1ManaRectNormalized = new Vector4(0.213f, 0.105f, 0.955f, 0.285f);
    public Vector4 star2ManaRectNormalized = new Vector4(0.216f, 0.105f, 0.953f, 0.285f);
    public Vector4 star3ManaRectNormalized = new Vector4(0.242f, 0.195f, 0.943f, 0.305f);

    // 装備アイテムのアイコンをHPバー上部に並べるための設定です。
    public float itemIconLocalWidth = 1.16f;
    public float itemIconSpacing = 1.75f;
    public float itemIconWorldGap = 0.03f;
    public Vector3 itemIconLocalOffset = new Vector3(0f, 0.7f, -0.04f);
    public Color itemIconBackgroundColor = new Color(0.02f, 0.03f, 0.05f, 0.9f);

    // 現在の最大HP、最大MP、追従対象です。
    private float maxHealth;
    private int maxMana;
    private Transform target;
    private BaseEntity ownerEntity;
    private SpriteRenderer targetRenderer;

    // HP区切り線と、各ゲージの配置情報です。
    private readonly List<SpriteRenderer> separators = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> itemIconBackgrounds = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> itemIconRenderers = new List<SpriteRenderer>();
    private FillLayout healthLayout;
    private FillLayout shieldLayout;
    private FillLayout manaLayout;

    // 描画順制御用の値です。ユニットの足元位置に合わせて毎フレーム更新します。
    private int sortingBaseOrder = 1000;
    private static Sprite solidSprite;
    private const int SortingBaseOrder = 1000;
    private const int SortingDepthScale = 20;
    private const int SortingStride = 12;

    // ゲージをフレーム内に配置するために必要な情報をまとめた構造体です。
    private struct FillLayout
    {
        public bool valid;
        public float left;
        public float centerY;
        public float fullScaleX;
        public float scaleY;
        public Bounds spriteBounds;
    }

    // 古い呼び出し用です。★1、MPなしとしてセットアップします。
    public void Setup(Transform target, float maxHealth)
    {
        Setup(target, maxHealth, 1, null, null);
    }

    // BaseEntity参照なしでセットアップする入口です。
    public void Setup(Transform target, float maxHealth, int starLevel, SpriteRenderer targetRenderer)
    {
        Setup(target, maxHealth, starLevel, targetRenderer, null);
    }

    // HPバーを対象ユニットに紐づけ、スターに合ったフレームとゲージ位置を作ります。
    public void Setup(Transform target, float maxHealth, int starLevel, SpriteRenderer targetRenderer, BaseEntity ownerEntity)
    {
        this.maxHealth = maxHealth;
        this.target = target;
        this.targetRenderer = targetRenderer;
        this.ownerEntity = ownerEntity;
        maxMana = ownerEntity != null ? ownerEntity.MaxMana : 0;

        // ユニットの拡大縮小にバーが引っ張られないよう、親から外します。
        if (transform.parent != null)
            transform.SetParent(null, true);

        transform.localScale = worldScale;

        EnsureRenderers();
        SetStarLevel(starLevel);
        RebuildSeparators();
        UpdateBar(maxHealth);
        UpdateShieldBar(0, Mathf.RoundToInt(maxHealth));
        UpdateManaBar(ownerEntity != null ? ownerEntity.CurrentMana : 0, maxMana);
    }

    // HPゲージの残量を更新します。
    public void UpdateBar(float newValue)
    {
        float fraction = maxHealth <= 0f ? 0f : Mathf.Clamp01(newValue / maxHealth);
        UpdateFill(bar, healthLayout, fraction);
    }

    // MPゲージの残量を更新します。
    public void UpdateManaBar(int currentMana, int maxMana)
    {
        this.maxMana = maxMana;
        float fraction = maxMana <= 0 ? 0f : Mathf.Clamp01((float)currentMana / maxMana);
        UpdateFill(manaBar, manaLayout, fraction);
    }

    // シールドゲージの残量を更新します。HPを基準に長さを決めます。
    public void UpdateShieldBar(int currentShield, int referenceHealth)
    {
        float fraction = referenceHealth <= 0 ? 0f : Mathf.Clamp01((float)currentShield / referenceHealth);
        UpdateFill(shieldBar, shieldLayout, fraction);
    }

    // 装備中アイテムのアイコンを、HPバーの上に最大3つ表示します。
    public void UpdateItemIcons(IReadOnlyList<ItemData> equippedItems)
    {
        NormalizeItemIconSettings();
        EnsureItemIconRenderers();

        int iconCount = equippedItems != null ? Mathf.Min(3, equippedItems.Count) : 0;
        for (int i = 0; i < itemIconRenderers.Count; i++)
        {
            ItemData itemData = i < iconCount ? equippedItems[i] : null;
            Sprite icon = itemData != null ? itemData.Icon : null;
            bool visible = icon != null;
            if (itemIconRenderers[i] != null)
            {
                itemIconRenderers[i].gameObject.SetActive(visible);
                itemIconRenderers[i].sprite = visible ? icon : null;
                if (visible)
                    FitItemIcon(itemIconRenderers[i]);

                UpdateItemIconClickTarget(itemIconRenderers[i], visible ? itemData : null);
            }

            if (itemIconBackgrounds[i] != null)
                itemIconBackgrounds[i].gameObject.SetActive(false);
        }

        UpdateItemIconPositions();
        UpdateItemIconSorting();
    }

    // スターに合わせてフレーム画像、ゲージ位置、描画順を設定します。
    public void SetStarLevel(int starLevel)
    {
        EnsureRenderers();

        if (frameRenderer != null)
        {
            Sprite frameSprite = GetFrameSprite(starLevel);
            if (frameSprite != null)
            {
                frameRenderer.sprite = frameSprite;
                frameRenderer.color = Color.white;
                FitRendererToTargetWidth(frameRenderer, frameTargetSize.x);
            }
            else
            {
                frameRenderer.color = GetFrameColor(starLevel);
                frameRenderer.transform.localScale = frameScale;
            }
        }

        ConfigureRendererSorting(fillRenderer, 20);
        ConfigureRendererSorting(fillMaskRenderer, 19);
        ConfigureRendererSorting(shieldRenderer, 21);
        ConfigureRendererSorting(shieldMaskRenderer, 19);
        ConfigureRendererSorting(manaRenderer, 20);
        ConfigureRendererSorting(manaMaskRenderer, 19);

        // フレーム画像がある場合は、画像内の空欄位置に合わせてゲージを配置します。
        if (frameRenderer != null && frameRenderer.sprite != null)
        {
            FitFillToFrameSlot(starLevel, false);
            FitShieldToFrameSlot(starLevel);
            FitFillToFrameSlot(starLevel, true);
        }
        else if (fillRenderer != null)
        {
            // フレーム画像がない場合の簡易表示です。
            healthLayout = new FillLayout
            {
                valid = true,
                fullScaleX = 1f,
                scaleY = fillHeightScale,
                spriteBounds = fillRenderer.sprite != null ? fillRenderer.sprite.bounds : new Bounds(Vector3.zero, Vector3.one)
            };

            Vector3 fillScale = fillRenderer.transform.localScale;
            fillScale.y = fillHeightScale;
            fillRenderer.transform.localScale = fillScale;
        }

        if (frameRenderer != null)
        {
            if (fillRenderer != null)
                frameRenderer.sortingLayerID = fillRenderer.sortingLayerID;

            frameRenderer.sortingOrder = sortingBaseOrder + 8;
        }

        UpdateSeparatorSorting();
    }

    // 毎フレーム、バーを対象ユニットの頭上へ追従させます。
    private void Update()
    {
        if(target == null)
            return;

        // 対象ユニットの上部にバーを追従させます。
        Vector3 position = target.position + offset;
        if (targetRenderer != null && targetRenderer.sprite != null)
        {
            Bounds bounds = targetRenderer.bounds;
            position.x = bounds.center.x + offset.x;
            position.y = bounds.max.y + verticalPadding + offset.y;
            position.z = target.position.z + offset.z;
        }

        transform.position = position;
        UpdateSortingOrder();

        // MPは攻撃や被ダメージで変わるので、所有者がいる時は毎フレーム反映します。
        if (ownerEntity != null)
            UpdateManaBar(ownerEntity.CurrentMana, ownerEntity.MaxMana);
    }

    // 必要なSpriteRendererを探すか、なければ作成します。
    private void EnsureRenderers()
    {
        if (fillRenderer == null && bar != null)
            fillRenderer = bar.GetComponent<SpriteRenderer>();

        if (frameRenderer == null)
        {
            Transform frame = transform.Find("healthbarContainer");
            if (frame != null)
                frameRenderer = frame.GetComponent<SpriteRenderer>();
        }

        ResolveMissingFrameSprites();
        EnsureChildRenderer(ref fillMaskRenderer, "healthbarMask", false);
        EnsureShieldRenderer();
        EnsureChildRenderer(ref shieldMaskRenderer, "shieldbarMask", false);
        EnsureManaRenderer();
        EnsureChildRenderer(ref manaMaskRenderer, "manabarMask", false);

        // HPマスクはHPゲージと同じSpriteを使うことで、隙間を自然に隠します。
        if (fillMaskRenderer != null && fillRenderer != null)
            fillMaskRenderer.sprite = fillRenderer.sprite;

        if (shieldRenderer != null)
        {
            shieldRenderer.sprite = GetSolidSprite();
            shieldRenderer.color = shieldFillColor;
        }

        if (shieldMaskRenderer != null)
        {
            shieldMaskRenderer.sprite = GetSolidSprite();
            shieldMaskRenderer.color = shieldMaskColor;
        }

        if (manaRenderer != null)
        {
            manaRenderer.sprite = GetSolidSprite();
            manaRenderer.color = manaFillColor;
        }

        if (manaMaskRenderer != null)
        {
            manaMaskRenderer.sprite = GetSolidSprite();
            manaMaskRenderer.color = manaMaskColor;
        }
    }

    // シールド用のゲージRendererを用意します。
    private void EnsureShieldRenderer()
    {
        if (shieldBar == null)
        {
            Transform existing = transform.Find("shieldbar");
            if (existing != null)
                shieldBar = existing;
        }

        if (shieldBar == null)
        {
            GameObject shieldObject = new GameObject("shieldbar");
            shieldObject.transform.SetParent(transform, false);
            shieldBar = shieldObject.transform;
        }

        if (shieldRenderer == null)
            shieldRenderer = shieldBar.GetComponent<SpriteRenderer>();

        if (shieldRenderer == null)
            shieldRenderer = shieldBar.gameObject.AddComponent<SpriteRenderer>();
    }

    // MP用のゲージRendererを用意します。
    private void EnsureManaRenderer()
    {
        if (manaBar == null)
        {
            Transform existing = transform.Find("manabar");
            if (existing != null)
                manaBar = existing;
        }

        if (manaBar == null)
        {
            GameObject manaObject = new GameObject("manabar");
            manaObject.transform.SetParent(transform, false);
            manaBar = manaObject.transform;
        }

        if (manaRenderer == null)
            manaRenderer = manaBar.GetComponent<SpriteRenderer>();

        if (manaRenderer == null)
            manaRenderer = manaBar.gameObject.AddComponent<SpriteRenderer>();
    }

    // 子オブジェクトのSpriteRendererを取得し、なければ新しく作ります。
    private void EnsureChildRenderer(ref SpriteRenderer renderer, string objectName, bool useFillSprite)
    {
        if (renderer == null)
        {
            Transform existing = transform.Find(objectName);
            if (existing != null)
                renderer = existing.GetComponent<SpriteRenderer>();
        }

        if (renderer == null)
        {
            GameObject child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            renderer = child.AddComponent<SpriteRenderer>();
        }

        if (useFillSprite && fillRenderer != null)
            renderer.sprite = fillRenderer.sprite;
    }

    // 最大HPに応じて、HPゲージ上の区切り線を作り直します。
    private void RebuildSeparators()
    {
        for (int i = 0; i < separators.Count; i++)
        {
            if (separators[i] != null)
                Destroy(separators[i].gameObject);
        }

        separators.Clear();

        if (!healthLayout.valid || fillRenderer == null || fillRenderer.sprite == null || maxHealth <= separatorHealthStep)
            return;

        int step = Mathf.Max(1, separatorHealthStep);
        int separatorCount = Mathf.FloorToInt((maxHealth - 1f) / step);
        float width = healthLayout.spriteBounds.size.x * healthLayout.fullScaleX;

        for (int i = 1; i <= separatorCount; i++)
        {
            float fraction = Mathf.Clamp01((i * step) / maxHealth);
            GameObject separatorObject = new GameObject($"HPSeparator_{i * step}");
            separatorObject.transform.SetParent(transform, false);
            separatorObject.transform.localPosition = new Vector3(
                healthLayout.left + width * fraction,
                healthLayout.centerY,
                -0.02f);
            separatorObject.transform.localScale = new Vector3(
                separatorScale.x,
                Mathf.Max(0.01f, healthLayout.scaleY * separatorScale.y),
                separatorScale.z);

            SpriteRenderer separator = separatorObject.AddComponent<SpriteRenderer>();
            separator.sprite = fillRenderer.sprite;
            separator.color = separatorColor;
            separator.sortingLayerID = fillRenderer.sortingLayerID;
            separator.sortingOrder = sortingBaseOrder + 5;
            separators.Add(separator);
        }
    }

    // 区切り線の描画順を、現在のバー描画順に合わせます。
    private void UpdateSeparatorSorting()
    {
        if (fillRenderer == null)
            return;

        for (int i = 0; i < separators.Count; i++)
        {
            if (separators[i] == null)
                continue;

            separators[i].sortingLayerID = fillRenderer.sortingLayerID;
            separators[i].sortingOrder = sortingBaseOrder + 5;
        }
    }

    // アイテムアイコン表示用の背景とSpriteRendererを3枠分用意します。
    private void EnsureItemIconRenderers()
    {
        NormalizeItemIconSettings();
        while (itemIconRenderers.Count < 3)
        {
            int index = itemIconRenderers.Count;

            GameObject backgroundObject = new GameObject($"ItemIconBackground_{index + 1}");
            backgroundObject.transform.SetParent(transform, false);
            SpriteRenderer backgroundRenderer = backgroundObject.AddComponent<SpriteRenderer>();
            backgroundRenderer.sprite = GetSolidSprite();
            backgroundRenderer.color = itemIconBackgroundColor;
            FitItemIconBackground(backgroundRenderer);
            backgroundRenderer.gameObject.SetActive(false);
            itemIconBackgrounds.Add(backgroundRenderer);

            GameObject iconObject = new GameObject($"ItemIcon_{index + 1}");
            iconObject.transform.SetParent(transform, false);
            SpriteRenderer iconRenderer = iconObject.AddComponent<SpriteRenderer>();
            iconRenderer.color = Color.white;
            itemIconRenderers.Add(iconRenderer);
        }

        for (int i = 0; i < itemIconBackgrounds.Count; i++)
        {
            if (itemIconBackgrounds[i] != null)
                FitItemIconBackground(itemIconBackgrounds[i]);
        }
    }

    // 既存Prefabに古い値が保存されていても、スクショで指定された配置へ補正します。
    private void NormalizeItemIconSettings()
    {
        itemIconLocalWidth = 1.16f;
        itemIconSpacing = 1.75f;
        itemIconLocalOffset = new Vector3(0f, 0.7f, -0.04f);
    }

    // 3つのアイコンをバー中央上に並べます。
    private void UpdateItemIconPositions()
    {
        for (int i = 0; i < itemIconRenderers.Count; i++)
        {
            float x = (i - 1) * itemIconSpacing + itemIconLocalOffset.x;
            Vector3 position = new Vector3(x, itemIconLocalOffset.y, itemIconLocalOffset.z);

            if (itemIconBackgrounds[i] != null)
                itemIconBackgrounds[i].transform.localPosition = position + new Vector3(0f, 0f, 0.01f);

            if (itemIconRenderers[i] != null)
                itemIconRenderers[i].transform.localPosition = position + new Vector3(0f, 0f, -0.01f);
        }
    }

    // アイコンSpriteの比率を保ったまま、指定幅に合わせます。
    private void FitItemIcon(SpriteRenderer iconRenderer)
    {
        if (iconRenderer == null || iconRenderer.sprite == null)
            return;

        Bounds bounds = iconRenderer.sprite.bounds;
        if (bounds.size.x <= 0f || bounds.size.y <= 0f)
            return;

        float desiredWorldSize = GetItemIconWorldSize();
        float scale = desiredWorldSize / Mathf.Max(bounds.size.x, bounds.size.y);
        iconRenderer.transform.localScale = GetItemIconLocalScale(scale, scale);
    }

    // アイコン背景も、親の非等倍スケールで潰れない正方形にします。
    private void FitItemIconBackground(SpriteRenderer backgroundRenderer)
    {
        if (backgroundRenderer == null || backgroundRenderer.sprite == null)
            return;

        Bounds bounds = backgroundRenderer.sprite.bounds;
        if (bounds.size.x <= 0f || bounds.size.y <= 0f)
            return;

        float scale = GetItemIconWorldSize() / Mathf.Max(bounds.size.x, bounds.size.y);
        backgroundRenderer.transform.localScale = GetItemIconLocalScale(scale, scale);
    }

    // 頭上の装備アイコンをクリックした時に、ベンチアイテムと同じ説明パネルを出せるようにします。
    private void UpdateItemIconClickTarget(SpriteRenderer iconRenderer, ItemData itemData)
    {
        if (iconRenderer == null)
            return;

        EquippedItemIconClickTarget clickTarget = iconRenderer.GetComponent<EquippedItemIconClickTarget>();
        if (clickTarget == null)
            clickTarget = iconRenderer.gameObject.AddComponent<EquippedItemIconClickTarget>();

        clickTarget.Setup(itemData, iconRenderer);
    }

    // HPバー本体は横と縦でスケールが違うため、子アイコンの縦横比が潰れないよう親スケールを打ち消します。
    private Vector3 GetItemIconLocalScale(float scalePerSpriteUnitX, float scalePerSpriteUnitY)
    {
        Vector3 parentScale = transform.lossyScale;
        float parentScaleX = Mathf.Max(0.0001f, Mathf.Abs(parentScale.x));
        float parentScaleY = Mathf.Max(0.0001f, Mathf.Abs(parentScale.y));
        return new Vector3(scalePerSpriteUnitX / parentScaleX, scalePerSpriteUnitY / parentScaleY, 1f);
    }

    // これまでの見た目の高さを基準に、正方形アイコンとして使うワールドサイズを返します。
    private float GetItemIconWorldSize()
    {
        Vector3 parentScale = transform.lossyScale;
        float largestParentAxis = Mathf.Max(Mathf.Max(Mathf.Abs(parentScale.x), Mathf.Abs(parentScale.y)), 0.0001f);
        return itemIconLocalWidth * largestParentAxis;
    }

    // 親の横方向スケールを考慮し、画面上でアイコン同士が少し離れる横間隔を返します。
    private float GetItemIconStepLocal()
    {
        Vector3 parentScale = transform.lossyScale;
        float parentScaleX = Mathf.Max(0.0001f, Mathf.Abs(parentScale.x));
        return (GetItemIconWorldSize() + itemIconWorldGap) / parentScaleX;
    }

    // アイコンがバーと一緒に前後関係を保つように描画順を更新します。
    private void UpdateItemIconSorting()
    {
        for (int i = 0; i < itemIconBackgrounds.Count; i++)
        {
            if (itemIconBackgrounds[i] != null)
            {
                if (fillRenderer != null)
                    itemIconBackgrounds[i].sortingLayerID = fillRenderer.sortingLayerID;

                itemIconBackgrounds[i].sortingOrder = sortingBaseOrder + 9;
            }

            if (itemIconRenderers[i] != null)
            {
                if (fillRenderer != null)
                    itemIconRenderers[i].sortingLayerID = fillRenderer.sortingLayerID;

                itemIconRenderers[i].sortingOrder = sortingBaseOrder + 10;
            }
        }
    }

    // ユニットの位置に合わせて、バー全体の描画順を更新します。
    private void UpdateSortingOrder()
    {
        if (target != null)
            sortingBaseOrder = BaseEntity.CalculateSortingOrder(target.position, 70);

        ConfigureRendererSorting(fillMaskRenderer, 0);
        ConfigureRendererSorting(shieldMaskRenderer, 0);
        ConfigureRendererSorting(manaMaskRenderer, 0);
        ConfigureRendererSorting(fillRenderer, 2);
        ConfigureRendererSorting(shieldRenderer, 3);
        ConfigureRendererSorting(manaRenderer, 4);

        if (frameRenderer != null)
        {
            if (fillRenderer != null)
                frameRenderer.sortingLayerID = fillRenderer.sortingLayerID;

            frameRenderer.sortingOrder = sortingBaseOrder + 8;
        }

        UpdateSeparatorSorting();
        UpdateItemIconSorting();
    }

    // フレーム画像がない場合のスター色を返します。
    private Color GetFrameColor(int starLevel)
    {
        if (starLevel >= 3)
            return star3FrameColor;

        if (starLevel == 2)
            return star2FrameColor;

        return star1FrameColor;
    }

    // スターに対応するフレームSpriteを返します。
    private Sprite GetFrameSprite(int starLevel)
    {
        ResolveMissingFrameSprites();

        if (starLevel >= 3 && star3FrameSprite != null)
            return star3FrameSprite;

        if (starLevel == 2 && star2FrameSprite != null)
            return star2FrameSprite;

        return star1FrameSprite;
    }

    // Editor上では、Inspector未設定でもAssets内のフレーム画像を自動で探します。
    private void ResolveMissingFrameSprites()
    {
#if UNITY_EDITOR
        if (star1FrameSprite == null)
            star1FrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/UI/health/HealthFrameStar1.png");

        if (star2FrameSprite == null)
            star2FrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/UI/health/HealthFrameStar2.png");

        if (star3FrameSprite == null)
            star3FrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/UI/health/HealthFrameStar3.png");
#endif
    }

    // スターに対応するHPゲージ枠範囲を返します。
    private Vector4 GetFillRectNormalized(int starLevel)
    {
        if (starLevel >= 3)
            return star3FillRectNormalized;

        if (starLevel == 2)
            return star2FillRectNormalized;

        return star1FillRectNormalized;
    }

    // スターに対応するMPゲージ枠範囲を返します。
    private Vector4 GetManaRectNormalized(int starLevel)
    {
        if (starLevel >= 3)
            return star3ManaRectNormalized;

        if (starLevel == 2)
            return star2ManaRectNormalized;

        return star1ManaRectNormalized;
    }

    // フレーム画像の比率を保ったまま、指定横幅に合わせます。
    private void FitRendererToTargetWidth(SpriteRenderer renderer, float targetWidth)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Bounds spriteBounds = renderer.sprite.bounds;
        if (spriteBounds.size.x <= 0f)
            return;

        float scaleX = targetWidth / spriteBounds.size.x;
        float scaleY = scaleX * GetParentScaleCompensation(renderer.transform);
        renderer.transform.localScale = new Vector3(scaleX, scaleY, 1f);
    }

    // 親の縦横スケール差で画像が潰れないよう補正します。
    private float GetParentScaleCompensation(Transform rendererTransform)
    {
        Transform parent = rendererTransform.parent;
        if (parent == null || Mathf.Abs(parent.lossyScale.y) <= 0.0001f)
            return 1f;

        return Mathf.Abs(parent.lossyScale.x / parent.lossyScale.y);
    }

    // SpriteRendererのローカル上の表示サイズを返します。
    private Vector2 GetRendererLocalSize(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null)
            return frameTargetSize;

        Bounds spriteBounds = renderer.sprite.bounds;
        return new Vector2(
            spriteBounds.size.x * renderer.transform.localScale.x,
            spriteBounds.size.y * renderer.transform.localScale.y);
    }

    // HPまたはMPゲージを、フレーム画像内の指定空欄にぴったり合わせます。
    private void FitFillToFrameSlot(int starLevel, bool mana)
    {
        Transform fillTransform = mana ? manaBar : bar;
        SpriteRenderer renderer = mana ? manaRenderer : fillRenderer;
        SpriteRenderer maskRenderer = mana ? manaMaskRenderer : fillMaskRenderer;

        if (fillTransform == null || renderer == null || renderer.sprite == null)
            return;

        Vector4 slot = mana ? GetManaRectNormalized(starLevel) : GetFillRectNormalized(starLevel);
        Vector2 frameSize = GetRendererLocalSize(frameRenderer);
        Vector3 frameCenter = frameRenderer != null ? frameRenderer.transform.localPosition : Vector3.zero;
        float minX = frameCenter.x + (slot.x - 0.5f) * frameSize.x + fillAreaPadding.x;
        float maxX = frameCenter.x + (slot.z - 0.5f) * frameSize.x - fillAreaPadding.x;
        float minY = frameCenter.y + (slot.y - 0.5f) * frameSize.y + fillAreaPadding.y;
        float maxY = frameCenter.y + (slot.w - 0.5f) * frameSize.y - fillAreaPadding.y;

        float targetWidth = Mathf.Max(0.01f, maxX - minX);
        float targetHeight = Mathf.Max(0.01f, maxY - minY);
        Bounds fillBounds = renderer.sprite.bounds;
        if (fillBounds.size.x <= 0f || fillBounds.size.y <= 0f)
            return;

        FillLayout layout = new FillLayout
        {
            valid = true,
            left = minX,
            centerY = (minY + maxY) * 0.5f,
            fullScaleX = targetWidth / fillBounds.size.x,
            scaleY = targetHeight / fillBounds.size.y,
            spriteBounds = fillBounds
        };

        if (mana)
            manaLayout = layout;
        else
            healthLayout = layout;

        UpdateFill(fillTransform, layout, 1f);
        FitFillMask(maskRenderer, renderer.sprite, layout, targetWidth, targetHeight, mana ? manaMaskColor : fillMaskColor);
    }

    // シールドゲージをHPゲージと同じ枠内に合わせます。
    private void FitShieldToFrameSlot(int starLevel)
    {
        if (shieldBar == null || shieldRenderer == null || shieldRenderer.sprite == null)
            return;

        Vector4 slot = GetFillRectNormalized(starLevel);
        Vector2 frameSize = GetRendererLocalSize(frameRenderer);
        Vector3 frameCenter = frameRenderer != null ? frameRenderer.transform.localPosition : Vector3.zero;
        float minX = frameCenter.x + (slot.x - 0.5f) * frameSize.x + fillAreaPadding.x;
        float maxX = frameCenter.x + (slot.z - 0.5f) * frameSize.x - fillAreaPadding.x;
        float minY = frameCenter.y + (slot.y - 0.5f) * frameSize.y + fillAreaPadding.y;
        float maxY = frameCenter.y + (slot.w - 0.5f) * frameSize.y - fillAreaPadding.y;

        float targetWidth = Mathf.Max(0.01f, maxX - minX);
        float targetHeight = Mathf.Max(0.01f, maxY - minY);
        Bounds fillBounds = shieldRenderer.sprite.bounds;
        if (fillBounds.size.x <= 0f || fillBounds.size.y <= 0f)
            return;

        shieldLayout = new FillLayout
        {
            valid = true,
            left = minX,
            centerY = (minY + maxY) * 0.5f,
            fullScaleX = targetWidth / fillBounds.size.x,
            scaleY = targetHeight / fillBounds.size.y,
            spriteBounds = fillBounds
        };

        UpdateFill(shieldBar, shieldLayout, 0f);
        FitFillMask(shieldMaskRenderer, shieldRenderer.sprite, shieldLayout, targetWidth, targetHeight, shieldMaskColor);
    }

    // 残量fractionに合わせて、ゲージの横幅と左端位置を更新します。
    private void UpdateFill(Transform fillTransform, FillLayout layout, float fraction)
    {
        if (!layout.valid || fillTransform == null)
            return;

        float scaleX = layout.fullScaleX * Mathf.Clamp01(fraction);
        fillTransform.localScale = new Vector3(scaleX, layout.scaleY, fillTransform.localScale.z);
        fillTransform.localPosition = new Vector3(
            layout.left - layout.spriteBounds.min.x * scaleX,
            layout.centerY - layout.spriteBounds.center.y * layout.scaleY,
            fillTransform.localPosition.z);
    }

    // ゲージの隙間から背景が見えないよう、空欄部分の裏にマスクを敷きます。
    private void FitFillMask(SpriteRenderer maskRenderer, Sprite sprite, FillLayout layout, float targetWidth, float targetHeight, Color color)
    {
        if (maskRenderer == null || sprite == null || !layout.valid)
            return;

        maskRenderer.sprite = sprite;
        maskRenderer.color = color;

        float maskWidth = Mathf.Max(0.01f, targetWidth + fillMaskOverlap.x * 2f);
        float maskHeight = Mathf.Max(0.01f, targetHeight + fillMaskOverlap.y * 2f);
        float maskScaleX = maskWidth / layout.spriteBounds.size.x;
        float maskScaleY = maskHeight / layout.spriteBounds.size.y;

        maskRenderer.transform.localScale = new Vector3(maskScaleX, maskScaleY, 1f);
        maskRenderer.transform.localPosition = new Vector3(
            layout.left - fillMaskOverlap.x - layout.spriteBounds.min.x * maskScaleX,
            layout.centerY - layout.spriteBounds.center.y * maskScaleY,
            -0.01f);
    }

    // 指定Rendererの描画順を、バー全体の基準値からの相対値で設定します。
    private void ConfigureRendererSorting(SpriteRenderer renderer, int sortingOrder)
    {
        if (renderer == null)
            return;

        if (fillRenderer != null)
            renderer.sortingLayerID = fillRenderer.sortingLayerID;

        renderer.sortingOrder = sortingBaseOrder + sortingOrder;
    }

    // 単色ゲージやマスクに使う白1枚のSpriteを作ります。
    private static Sprite GetSolidSprite()
    {
        if (solidSprite == null)
        {
            Texture2D texture = Texture2D.whiteTexture;
            solidSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        return solidSprite;
    }
}

// ユニット頭上に表示されている装備済みアイテムアイコンのクリックを受け取るクラスです。
// ベンチ上のアイテムと同じItemTooltipUIを使い、効果説明を表示します。
public class EquippedItemIconClickTarget : MonoBehaviour
{
    private ItemData itemData;
    private SpriteRenderer iconRenderer;
    private BoxCollider2D clickCollider;

    // HealthBarから現在表示しているアイテム情報とRendererを受け取ります。
    public void Setup(ItemData itemData, SpriteRenderer iconRenderer)
    {
        this.itemData = itemData;
        this.iconRenderer = iconRenderer;

        EnsureCollider();
        RefreshCollider();

        if (clickCollider != null)
            clickCollider.enabled = itemData != null && iconRenderer != null && iconRenderer.sprite != null;
    }

    // クリックできるよう、アイコンの見た目に合わせた2D当たり判定を用意します。
    private void EnsureCollider()
    {
        if (clickCollider == null)
            clickCollider = GetComponent<BoxCollider2D>();

        if (clickCollider == null)
            clickCollider = gameObject.AddComponent<BoxCollider2D>();

        clickCollider.isTrigger = true;
    }

    // Spriteの実サイズに合わせてクリック範囲を更新します。
    private void RefreshCollider()
    {
        if (clickCollider == null || iconRenderer == null || iconRenderer.sprite == null)
            return;

        Bounds bounds = iconRenderer.sprite.bounds;
        clickCollider.size = bounds.size;
        clickCollider.offset = bounds.center;
    }

    // マウスボタンを離した時、装備アイテムの説明パネルを表示します。
    private void OnMouseUpAsButton()
    {
        if (itemData == null)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        ItemTooltipUI.Show(itemData, Input.mousePosition);
    }
}
