using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

// ショップに並ぶ1枚のユニットカードを管理するクラスです。
// アイコン、名前、コスト、購入直前のスターアップ予告表示を担当します。
public class UICard : MonoBehaviour
{
    // Inspectorから紐づける、カード内の基本UIです。
    public Image icon;
    public Image frame;
    public new TextMeshProUGUI name;
    public TextMeshProUGUI cost;

    // 同じユニットを買うとスターアップできる時に使う強調色です。
    public Color upgradeReadyFrameColor = new Color(1f, 0.9f, 0.2f, 1f);
    public Color upgradeReadyGlowColor = new Color(1f, 0.75f, 0.05f, 0.38f);
    public Color upgradeReadyTextColor = new Color(1f, 0.95f, 0.15f, 1f);
    public Color upgradeReadyStar3Color = new Color(1f, 0.45f, 1f, 1f);

    // このカードが属しているショップと、このカードが表しているユニット情報です。
    private UIShop shopRef;
    private EntitiesDatabaseSO.EntityData myData;

    // 強調表示を解除した時に元へ戻せるよう、初期状態を保存します。
    private Color defaultFrameColor = Color.white;
    private Vector3 defaultFrameScale = Vector3.one;
    private bool defaultFrameColorSet;
    private bool defaultFrameScaleSet;

    // スターアップ予告用に実行時生成する光と文字です。
    private Image upgradeGlow;
    private TextMeshProUGUI upgradeBadge;
    private int upgradePreviewStarLevel;

    // ショップカード左下へ重ねるシナジー表示です。Prefabを触りすぎないよう、実行時に生成します。
    private const int MaxSynergyBadges = 3;
    private readonly List<GameObject> synergyBadgeObjects = new List<GameObject>();
    private readonly List<Image> synergyBadgeBackgrounds = new List<Image>();
    private readonly List<Image> synergyBadgeIcons = new List<Image>();
    private readonly List<TextMeshProUGUI> synergyBadgeLabels = new List<TextMeshProUGUI>();
    private RectTransform cardRect;
    private CanvasGroup cardCanvasGroup;
    private Tween appearTween;
    private Vector3 defaultCardScale = Vector3.one;

    // ショップ側が「このカードに有効なユニットが入っているか」を確認するための読み取り専用情報です。
    public bool HasData => myData.prefab != null;
    public string EntityName => myData.name;
    public int EntityCost => myData.cost;
    public EntitiesDatabaseSO.EntityData EntityData => myData;

    // カード生成直後に、フレーム参照と初期表示状態を保存します。
    private void Awake()
    {
        // シーン上でframe参照が入っていない場合でも動くように、起動時に探しておきます。
        LocalizationManager.EnsureExists();
        EnsureFrameReference();
        LocalizationManager.ApplyFont(name);
        LocalizationManager.ApplyFont(cost);
        ConfigureCardText(name);
        ConfigureCardText(cost);
        EnsureTweenReferences();
    }

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += RefreshLocalizedText;
        RefreshLocalizedText();
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= RefreshLocalizedText;
        KillAppearTween(false);
    }

    // ショップがカードを生成・更新する時に呼びます。
    // EntityDataの内容をUIへ反映し、このカードをクリックした時にショップへ通知できるようにします。
    public void Setup(EntitiesDatabaseSO.EntityData myData, UIShop shopRef)
    {
        EnsureFrameReference();

        // 表示に必要な画像とテキストを、渡されたユニットデータから差し替えます。
        icon.sprite = myData.icon;
        if (frame != null)
            frame.sprite = myData.frame;
        ConfigureCardText(name);
        ConfigureCardText(cost);
        name.text = LocalizationManager.UnitName(myData.name);
        cost.text = myData.cost.ToString();

        this.myData = myData;
        this.shopRef = shopRef;
        RefreshSynergyBadges();

        // カードが再利用されるので、前回のスターアップ強調を必ず消しておきます。
        SetUpgradeReady(false);
    }

    // ショップにカードが並んだ時だけ、表示を少し華やかにします。購入処理とは切り離した見た目専用Tweenです。
    public void PlayAppearAnimation(float delay)
    {
        EnsureTweenReferences();
        if (cardRect == null || cardCanvasGroup == null || !gameObject.activeInHierarchy)
            return;

        KillAppearTween(false);
        cardCanvasGroup.alpha = 0f;
        cardRect.localScale = defaultCardScale * 0.86f;

        appearTween = DOTween.Sequence()
            .SetTarget(this)
            .SetUpdate(true)
            .AppendInterval(Mathf.Max(0f, delay))
            .Append(cardCanvasGroup.DOFade(1f, 0.16f).SetEase(Ease.OutQuad))
            .Join(cardRect.DOScale(defaultCardScale, 0.24f).SetEase(Ease.OutBack));
    }

    // UIボタンのクリックイベントから呼ばれます。
    // 購入判定や所持金処理はカード自身ではなく、ショップに任せます。
    public void OnClick()
    {
        //Tell the shop!
        shopRef.OnCardClick(this, myData);
    }

    // 古い呼び出し用の入口です。
    // trueなら★2作成可能、falseなら強調なしとして扱います。
    public void SetUpgradeReady(bool ready)
    {
        SetUpgradeReady(ready ? 2 : 0);
    }

    // starLevelが2以上なら、購入するとその星のユニットを作れることをカード上で強調します。
    // 0の場合は通常表示へ戻します。
    public void SetUpgradeReady(int starLevel)
    {
        EnsureFrameReference();
        EnsureUpgradeVisuals();
        upgradePreviewStarLevel = starLevel;
        bool ready = starLevel > 0;

        // フレーム色をスターアップ用に変えるか、元の色に戻します。
        if (frame != null)
        {
            frame.color = ready ? GetUpgradeColor(starLevel) : defaultFrameColor;
            frame.transform.localScale = defaultFrameScale;
        }

        // 背面の光を表示し、★3予定なら少し特別な色にします。
        if (upgradeGlow != null)
        {
            upgradeGlow.gameObject.SetActive(ready);
            upgradeGlow.color = starLevel >= 3
                ? new Color(upgradeReadyStar3Color.r, upgradeReadyStar3Color.g, upgradeReadyStar3Color.b, 0.42f)
                : upgradeReadyGlowColor;
        }

        // カード上部に「STAR 2」「STAR 3」と出して、何が起きるか分かりやすくします。
        if (upgradeBadge != null)
        {
            upgradeBadge.gameObject.SetActive(ready);
            upgradeBadge.text = LocalizationManager.FormatUpgradeLabel(starLevel);
            upgradeBadge.color = Color.white;
            LocalizationManager.ApplyFont(upgradeBadge);
        }

        // スターアップ用の光が作られても、シナジー表示はカード表面に見えるようにします。
        BringSynergyBadgesToFront();
    }

    // 言語が切り替わった時、カード名とスターアップ予告を現在言語へ更新します。
    private void RefreshLocalizedText()
    {
        if (name != null && HasData)
        {
            LocalizationManager.ApplyFont(name);
            ConfigureCardText(name);
            name.text = LocalizationManager.UnitName(myData.name);
        }

        LocalizationManager.ApplyFont(cost);
        ConfigureCardText(cost);

        if (upgradeBadge != null && upgradePreviewStarLevel > 0)
        {
            LocalizationManager.ApplyFont(upgradeBadge);
            upgradeBadge.text = LocalizationManager.FormatUpgradeLabel(upgradePreviewStarLevel);
        }

        RefreshSynergyBadges();
    }

    // スターアップ予告中だけ、光とフレームを点滅させます。
    private void Update()
    {
        // スターアップ予告中だけ、フレームと光を軽く脈打たせます。
        if (upgradePreviewStarLevel <= 0 || frame == null)
            return;

        // Sin波を使うと、0から1の間をなめらかに往復する点滅値を作れます。
        float pulse = (Mathf.Sin(Time.unscaledTime * 7f) + 1f) * 0.5f;
        float baseScale = upgradePreviewStarLevel >= 3 ? 1.12f : 1.08f;
        float scale = baseScale + pulse * 0.06f;
        frame.transform.localScale = defaultFrameScale * scale;

        // 光の透明度も同じpulseで変え、ショップ内で目立つようにします。
        if (upgradeGlow != null)
        {
            Color color = upgradePreviewStarLevel >= 3
                ? new Color(upgradeReadyStar3Color.r, upgradeReadyStar3Color.g, upgradeReadyStar3Color.b, 1f)
                : upgradeReadyGlowColor;

            color.a = Mathf.Lerp(0.28f, upgradePreviewStarLevel >= 3 ? 0.58f : 0.48f, pulse);
            upgradeGlow.color = color;
        }
    }

    // ★3作成予定は★2よりレアなので、カードの色も変えます。
    private Color GetUpgradeColor(int starLevel)
    {
        return starLevel >= 3 ? upgradeReadyStar3Color : upgradeReadyFrameColor;
    }

    // 日本語名が長いユニットでもショップ枠を崩さないよう、折り返しを止めて自動縮小します。
    private void ConfigureCardText(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.enableAutoSizing = true;
        text.fontSizeMin = text == cost ? 10f : 8f;
        text.fontSizeMax = text == cost ? 14f : 13f;
    }

    // frame参照と初期状態を安全に取得します。
    // Prefabの構造が少し変わっても、子オブジェクト名がframeなら自動で拾えます。
    private void EnsureFrameReference()
    {
        if (frame == null)
        {
            Transform frameTransform = transform.Find("frame");
            if (frameTransform != null)
                frame = frameTransform.GetComponent<Image>();
        }

        // 強調表示を解除した時のため、最初の色を1回だけ保存します。
        if (frame != null && !defaultFrameColorSet)
        {
            defaultFrameColor = frame.color;
            defaultFrameColorSet = true;
        }

        // フレームを点滅拡大した後に戻すため、最初の拡大率も1回だけ保存します。
        if (frame != null && !defaultFrameScaleSet)
        {
            defaultFrameScale = frame.transform.localScale;
            defaultFrameScaleSet = true;
        }
    }

    // スターアップ予告用の光と文字を、必要になったタイミングで作ります。
    // 最初からシーンに置かず、コードで作ることで既存カードPrefabを大きく変えずに済みます。
    private void EnsureUpgradeVisuals()
    {
        RectTransform cardRect = transform as RectTransform;
        if (cardRect == null)
            return;

        // カード全面に重ねる半透明の光です。クリック判定の邪魔をしないようraycastTargetは切ります。
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

        // 「STAR 2」「STAR 3」と表示するバッジです。文字サイズはカード内に収まるよう自動調整します。
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
            LocalizationManager.ApplyFont(upgradeBadge);
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

    private void EnsureTweenReferences()
    {
        if (cardRect == null)
        {
            cardRect = transform as RectTransform;
            defaultCardScale = cardRect != null ? cardRect.localScale : transform.localScale;
        }

        if (cardCanvasGroup == null)
        {
            cardCanvasGroup = GetComponent<CanvasGroup>();
            if (cardCanvasGroup == null)
                cardCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void KillAppearTween(bool complete)
    {
        if (appearTween != null && appearTween.IsActive())
            appearTween.Kill(complete);

        appearTween = null;

        if (cardRect != null)
            cardRect.DOKill();

        if (cardCanvasGroup != null)
            cardCanvasGroup.DOKill();
    }

    // 現在カードに入っているユニットのシナジーを、カード左下へ小さく並べます。
    private void RefreshSynergyBadges()
    {
        EnsureSynergyBadgeVisuals();
        List<SynergyType> synergies = HasData
            ? SynergyManager.GetSynergiesForEntityData(myData)
            : new List<SynergyType>();

        for (int i = 0; i < MaxSynergyBadges; i++)
        {
            bool visible = i < synergies.Count && synergies[i] != SynergyType.None;
            if (synergyBadgeObjects.Count <= i)
                continue;

            synergyBadgeObjects[i].SetActive(visible);
            if (!visible)
                continue;

            SynergyType type = synergies[i];
            Color synergyColor = SynergyIconLibrary.GetColor(type);
            if (synergyBadgeBackgrounds[i] != null)
                synergyBadgeBackgrounds[i].color = new Color(synergyColor.r * 0.22f, synergyColor.g * 0.22f, synergyColor.b * 0.22f, 0.78f);

            if (synergyBadgeIcons[i] != null)
            {
                synergyBadgeIcons[i].sprite = SynergyIconLibrary.GetSprite(type);
                synergyBadgeIcons[i].color = synergyColor;
                synergyBadgeIcons[i].preserveAspect = true;
            }

            if (synergyBadgeLabels[i] != null)
            {
                LocalizationManager.ApplyFont(synergyBadgeLabels[i]);
                synergyBadgeLabels[i].text = SynergyIconLibrary.GetShortLabel(type);
                synergyBadgeLabels[i].color = Color.white;
            }
        }

        BringSynergyBadgesToFront();
    }

    // シナジー表示のUI部品を、ショップカード上へ実行時に作ります。
    private void EnsureSynergyBadgeVisuals()
    {
        if (synergyBadgeObjects.Count >= MaxSynergyBadges)
            return;

        RectTransform cardRect = transform as RectTransform;
        if (cardRect == null)
            return;

        for (int i = synergyBadgeObjects.Count; i < MaxSynergyBadges; i++)
        {
            GameObject badgeObject = new GameObject($"SynergyBadge_{i + 1}", typeof(RectTransform));
            badgeObject.transform.SetParent(transform, false);

            RectTransform badgeRect = badgeObject.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 0f);
            badgeRect.anchorMax = new Vector2(0f, 0f);
            badgeRect.pivot = new Vector2(0f, 0f);
            badgeRect.sizeDelta = new Vector2(64f, 22f);
            badgeRect.anchoredPosition = new Vector2(5f, 22f + i * 24f);

            Image background = badgeObject.AddComponent<Image>();
            background.raycastTarget = false;
            background.color = new Color(0f, 0f, 0f, 0.72f);

            GameObject iconObject = new GameObject("Icon", typeof(RectTransform));
            iconObject.transform.SetParent(badgeObject.transform, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(12f, 0f);
            iconRect.sizeDelta = new Vector2(19f, 19f);

            Image synergyIcon = iconObject.AddComponent<Image>();
            synergyIcon.raycastTarget = false;
            synergyIcon.preserveAspect = true;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(badgeObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(24f, 0f);
            labelRect.offsetMax = new Vector2(-3f, 0f);

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            LocalizationManager.ApplyFont(label);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableAutoSizing = true;
            label.fontSizeMin = 8f;
            label.fontSizeMax = 11.5f;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.fontStyle = FontStyles.Bold;
            label.outlineWidth = 0.16f;
            label.outlineColor = Color.black;
            label.raycastTarget = false;

            synergyBadgeObjects.Add(badgeObject);
            synergyBadgeBackgrounds.Add(background);
            synergyBadgeIcons.Add(synergyIcon);
            synergyBadgeLabels.Add(label);
        }
    }

    // カード上の他の強調表示よりシナジーバッジを前に出します。
    private void BringSynergyBadgesToFront()
    {
        for (int i = 0; i < synergyBadgeObjects.Count; i++)
        {
            if (synergyBadgeObjects[i] != null)
                synergyBadgeObjects[i].transform.SetAsLastSibling();
        }
    }
}
