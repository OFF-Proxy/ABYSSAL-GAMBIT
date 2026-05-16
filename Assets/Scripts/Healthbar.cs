using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class HealthBar : MonoBehaviour
{
    public Transform bar;
    public Transform shieldBar;
    public Transform manaBar;
    public Vector3 offset;
    public SpriteRenderer frameRenderer;
    public SpriteRenderer fillRenderer;
    public SpriteRenderer fillMaskRenderer;
    public SpriteRenderer shieldRenderer;
    public SpriteRenderer shieldMaskRenderer;
    public SpriteRenderer manaRenderer;
    public SpriteRenderer manaMaskRenderer;
    public Vector3 worldScale = new Vector3(0.2f, 0.4f, 1f);
    public float verticalPadding = 0.04f;
    public Vector3 frameScale = new Vector3(1.32f, 2.35f, 1f);
    public float fillHeightScale = 0.58f;
    public int separatorHealthStep = 500;
    public Color separatorColor = new Color(0.02f, 0.02f, 0.02f, 0.9f);
    public Vector3 separatorScale = new Vector3(0.025f, 0.82f, 1f);
    public Color fillMaskColor = new Color(0.02f, 0.025f, 0.04f, 1f);
    public Color shieldFillColor = new Color(1f, 1f, 1f, 0.95f);
    public Color shieldMaskColor = new Color(0.015f, 0.04f, 0.06f, 1f);
    public Color manaFillColor = new Color(0.05f, 0.58f, 1f, 1f);
    public Color manaMaskColor = new Color(0.015f, 0.02f, 0.05f, 1f);
    public Color star1FrameColor = new Color(0.92f, 0.96f, 1f, 1f);
    public Color star2FrameColor = new Color(0.1f, 0.65f, 1f, 1f);
    public Color star3FrameColor = new Color(1f, 0.76f, 0f, 1f);
    public Sprite star1FrameSprite;
    public Sprite star2FrameSprite;
    public Sprite star3FrameSprite;
    public Vector2 frameTargetSize = new Vector2(7.28f, 0.78f);
    public Vector2 fillAreaPadding = new Vector2(-0.02f, -0.015f);
    public Vector2 fillMaskOverlap = new Vector2(0.05f, 0.03f);
    public Vector4 star1FillRectNormalized = new Vector4(0.198f, 0.445f, 0.948f, 0.725f);
    public Vector4 star2FillRectNormalized = new Vector4(0.198f, 0.445f, 0.951f, 0.725f);
    public Vector4 star3FillRectNormalized = new Vector4(0.236f, 0.445f, 0.943f, 0.725f);
    public Vector4 star1ManaRectNormalized = new Vector4(0.213f, 0.105f, 0.955f, 0.285f);
    public Vector4 star2ManaRectNormalized = new Vector4(0.216f, 0.105f, 0.953f, 0.285f);
    public Vector4 star3ManaRectNormalized = new Vector4(0.242f, 0.195f, 0.943f, 0.305f);

    private float maxHealth;
    private int maxMana;
    private Transform target;
    private BaseEntity ownerEntity;
    private SpriteRenderer targetRenderer;
    private readonly List<SpriteRenderer> separators = new List<SpriteRenderer>();
    private FillLayout healthLayout;
    private FillLayout shieldLayout;
    private FillLayout manaLayout;
    private int sortingBaseOrder = 1000;
    private static Sprite solidSprite;
    private const int SortingBaseOrder = 1000;
    private const int SortingDepthScale = 20;
    private const int SortingStride = 12;

    private struct FillLayout
    {
        public bool valid;
        public float left;
        public float centerY;
        public float fullScaleX;
        public float scaleY;
        public Bounds spriteBounds;
    }

    public void Setup(Transform target, float maxHealth)
    {
        Setup(target, maxHealth, 1, null, null);
    }

    public void Setup(Transform target, float maxHealth, int starLevel, SpriteRenderer targetRenderer)
    {
        Setup(target, maxHealth, starLevel, targetRenderer, null);
    }

    public void Setup(Transform target, float maxHealth, int starLevel, SpriteRenderer targetRenderer, BaseEntity ownerEntity)
    {
        this.maxHealth = maxHealth;
        this.target = target;
        this.targetRenderer = targetRenderer;
        this.ownerEntity = ownerEntity;
        maxMana = ownerEntity != null ? ownerEntity.MaxMana : 0;

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

    public void UpdateBar(float newValue)
    {
        float fraction = maxHealth <= 0f ? 0f : Mathf.Clamp01(newValue / maxHealth);
        UpdateFill(bar, healthLayout, fraction);
    }

    public void UpdateManaBar(int currentMana, int maxMana)
    {
        this.maxMana = maxMana;
        float fraction = maxMana <= 0 ? 0f : Mathf.Clamp01((float)currentMana / maxMana);
        UpdateFill(manaBar, manaLayout, fraction);
    }

    public void UpdateShieldBar(int currentShield, int referenceHealth)
    {
        float fraction = referenceHealth <= 0 ? 0f : Mathf.Clamp01((float)currentShield / referenceHealth);
        UpdateFill(shieldBar, shieldLayout, fraction);
    }

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

        if (frameRenderer != null && frameRenderer.sprite != null)
        {
            FitFillToFrameSlot(starLevel, false);
            FitShieldToFrameSlot(starLevel);
            FitFillToFrameSlot(starLevel, true);
        }
        else if (fillRenderer != null)
        {
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

    private void Update()
    {
        if(target == null)
            return;

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

        if (ownerEntity != null)
            UpdateManaBar(ownerEntity.CurrentMana, ownerEntity.MaxMana);
    }

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
    }

    private Color GetFrameColor(int starLevel)
    {
        if (starLevel >= 3)
            return star3FrameColor;

        if (starLevel == 2)
            return star2FrameColor;

        return star1FrameColor;
    }

    private Sprite GetFrameSprite(int starLevel)
    {
        ResolveMissingFrameSprites();

        if (starLevel >= 3 && star3FrameSprite != null)
            return star3FrameSprite;

        if (starLevel == 2 && star2FrameSprite != null)
            return star2FrameSprite;

        return star1FrameSprite;
    }

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

    private Vector4 GetFillRectNormalized(int starLevel)
    {
        if (starLevel >= 3)
            return star3FillRectNormalized;

        if (starLevel == 2)
            return star2FillRectNormalized;

        return star1FillRectNormalized;
    }

    private Vector4 GetManaRectNormalized(int starLevel)
    {
        if (starLevel >= 3)
            return star3ManaRectNormalized;

        if (starLevel == 2)
            return star2ManaRectNormalized;

        return star1ManaRectNormalized;
    }

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

    private float GetParentScaleCompensation(Transform rendererTransform)
    {
        Transform parent = rendererTransform.parent;
        if (parent == null || Mathf.Abs(parent.lossyScale.y) <= 0.0001f)
            return 1f;

        return Mathf.Abs(parent.lossyScale.x / parent.lossyScale.y);
    }

    private Vector2 GetRendererLocalSize(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null)
            return frameTargetSize;

        Bounds spriteBounds = renderer.sprite.bounds;
        return new Vector2(
            spriteBounds.size.x * renderer.transform.localScale.x,
            spriteBounds.size.y * renderer.transform.localScale.y);
    }

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

    private void ConfigureRendererSorting(SpriteRenderer renderer, int sortingOrder)
    {
        if (renderer == null)
            return;

        if (fillRenderer != null)
            renderer.sortingLayerID = fillRenderer.sortingLayerID;

        renderer.sortingOrder = sortingBaseOrder + sortingOrder;
    }

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
