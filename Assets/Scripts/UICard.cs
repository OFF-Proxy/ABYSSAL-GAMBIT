using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UICard : MonoBehaviour
{
    public Image icon;
    public Image frame;
    public new TextMeshProUGUI name;
    public TextMeshProUGUI cost;
    public Color upgradeReadyFrameColor = new Color(1f, 0.9f, 0.2f, 1f);
    public Color upgradeReadyGlowColor = new Color(1f, 0.75f, 0.05f, 0.38f);
    public Color upgradeReadyTextColor = new Color(1f, 0.95f, 0.15f, 1f);
    public Color upgradeReadyStar3Color = new Color(1f, 0.45f, 1f, 1f);

    private UIShop shopRef;
    private EntitiesDatabaseSO.EntityData myData;
    private Color defaultFrameColor = Color.white;
    private Vector3 defaultFrameScale = Vector3.one;
    private bool defaultFrameColorSet;
    private bool defaultFrameScaleSet;
    private Image upgradeGlow;
    private TextMeshProUGUI upgradeBadge;
    private int upgradePreviewStarLevel;
    public bool HasData => myData.prefab != null;
    public string EntityName => myData.name;

    private void Awake()
    {
        EnsureFrameReference();
    }

    public void Setup(EntitiesDatabaseSO.EntityData myData, UIShop shopRef)
    {
        EnsureFrameReference();
        icon.sprite = myData.icon;
        if (frame != null)
            frame.sprite = myData.frame;
        name.text = myData.name;
        cost.text = myData.cost.ToString();

        this.myData = myData;
        this.shopRef = shopRef;
        SetUpgradeReady(false);
    }

    public void OnClick()
    {
        //Tell the shop!
        shopRef.OnCardClick(this, myData);
    }

    public void SetUpgradeReady(bool ready)
    {
        SetUpgradeReady(ready ? 2 : 0);
    }

    public void SetUpgradeReady(int starLevel)
    {
        EnsureFrameReference();
        EnsureUpgradeVisuals();
        upgradePreviewStarLevel = starLevel;
        bool ready = starLevel > 0;

        if (frame != null)
        {
            frame.color = ready ? GetUpgradeColor(starLevel) : defaultFrameColor;
            frame.transform.localScale = defaultFrameScale;
        }

        if (upgradeGlow != null)
        {
            upgradeGlow.gameObject.SetActive(ready);
            upgradeGlow.color = starLevel >= 3
                ? new Color(upgradeReadyStar3Color.r, upgradeReadyStar3Color.g, upgradeReadyStar3Color.b, 0.42f)
                : upgradeReadyGlowColor;
        }

        if (upgradeBadge != null)
        {
            upgradeBadge.gameObject.SetActive(ready);
            upgradeBadge.text = starLevel >= 3 ? "STAR 3" : "STAR 2";
            upgradeBadge.color = Color.white;
        }
    }

    private void Update()
    {
        if (upgradePreviewStarLevel <= 0 || frame == null)
            return;

        float pulse = (Mathf.Sin(Time.unscaledTime * 7f) + 1f) * 0.5f;
        float baseScale = upgradePreviewStarLevel >= 3 ? 1.12f : 1.08f;
        float scale = baseScale + pulse * 0.06f;
        frame.transform.localScale = defaultFrameScale * scale;

        if (upgradeGlow != null)
        {
            Color color = upgradePreviewStarLevel >= 3
                ? new Color(upgradeReadyStar3Color.r, upgradeReadyStar3Color.g, upgradeReadyStar3Color.b, 1f)
                : upgradeReadyGlowColor;

            color.a = Mathf.Lerp(0.28f, upgradePreviewStarLevel >= 3 ? 0.58f : 0.48f, pulse);
            upgradeGlow.color = color;
        }
    }

    private Color GetUpgradeColor(int starLevel)
    {
        return starLevel >= 3 ? upgradeReadyStar3Color : upgradeReadyFrameColor;
    }

    private void EnsureFrameReference()
    {
        if (frame == null)
        {
            Transform frameTransform = transform.Find("frame");
            if (frameTransform != null)
                frame = frameTransform.GetComponent<Image>();
        }

        if (frame != null && !defaultFrameColorSet)
        {
            defaultFrameColor = frame.color;
            defaultFrameColorSet = true;
        }

        if (frame != null && !defaultFrameScaleSet)
        {
            defaultFrameScale = frame.transform.localScale;
            defaultFrameScaleSet = true;
        }
    }

    private void EnsureUpgradeVisuals()
    {
        RectTransform cardRect = transform as RectTransform;
        if (cardRect == null)
            return;

        if (upgradeGlow == null)
        {
            GameObject glowObject = new GameObject("UpgradeGlow", typeof(RectTransform));
            glowObject.transform.SetParent(transform, false);
            glowObject.transform.SetAsLastSibling();

            RectTransform glowRect = glowObject.GetComponent<RectTransform>();
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = Vector2.zero;
            glowRect.offsetMax = Vector2.zero;

            upgradeGlow = glowObject.AddComponent<Image>();
            upgradeGlow.raycastTarget = false;
            upgradeGlow.color = upgradeReadyGlowColor;
            upgradeGlow.gameObject.SetActive(false);
        }

        if (upgradeBadge == null)
        {
            GameObject badgeObject = new GameObject("UpgradeBadge", typeof(RectTransform));
            badgeObject.transform.SetParent(transform, false);
            badgeObject.transform.SetAsLastSibling();

            RectTransform badgeRect = badgeObject.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 0.68f);
            badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.offsetMin = new Vector2(4f, 0f);
            badgeRect.offsetMax = new Vector2(-4f, -4f);

            upgradeBadge = badgeObject.AddComponent<TextMeshProUGUI>();
            upgradeBadge.alignment = TextAlignmentOptions.Center;
            upgradeBadge.enableAutoSizing = true;
            upgradeBadge.fontSizeMin = 16f;
            upgradeBadge.fontSizeMax = 30f;
            upgradeBadge.enableWordWrapping = false;
            upgradeBadge.overflowMode = TextOverflowModes.Overflow;
            upgradeBadge.fontStyle = FontStyles.Bold;
            upgradeBadge.outlineWidth = 0.22f;
            upgradeBadge.outlineColor = Color.black;
            upgradeBadge.raycastTarget = false;
            upgradeBadge.gameObject.SetActive(false);
        }
    }
}
