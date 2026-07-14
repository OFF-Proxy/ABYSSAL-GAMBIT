using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

// ショップに並ぶ1枚のユニットカードを管理するクラスです。
// アイコン、名前、コスト、購入直前のスターアップ予告表示を担当します。
public class UICard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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
    private bool frameSizeAdjusted;
    // ショップスロットのアイコン/フレームの最終サイズ（ユーザー指定 2026-06-06）。
    // ランタイムで確実にこのサイズへ揃える（シーン/Prefab値より優先）。インスペクタで微調整可。
    public Vector2 iconTargetSize = new Vector2(255f, 140f);
    public float frameTargetWidth = 255f;
    public float frameTargetHeight = 150f;
    public float frameVerticalOffset = 11.5f;

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

    // R5-shop-duelyst P1: カードの顔＝AIイラストではなく、盤外に生成したユニット実体の SpriteRenderer を
    // icon へミラーして表示する（別インスタンス＝盤面の同ユニットとアニメが非連動）。
    private GameObject previewInstance;      // 盤外に置く見た目専用インスタンス。
    private CollectionBossAnimator previewMirror; // icon 上のミラー（source→target）。
    private static int previewSpawnCounter;  // 盤外配置位置をずらして重なりを避ける。

    // R5-P7: 足元のひし形足場（Duelyst tile_board を45°回転＋縦つぶし＋コスト色）。旧・角丸枠(frame)は撤去する。
    private RectTransform footSquash;   // 縦つぶしの親。回転した子を isometric なひし形（floorタイル風）に見せる。
    private Image footDiamond;          // ひし形本体。
    private Image footGlow;             // ひし形の下グロウ。
    private Color defaultPlatformColor = Color.white;
    private static Sprite platformSprite;
    private static Sprite platformGlowSprite;
    // 見た目の微調整（インスペクタ）。
    public float platformSize = 132f;     // ひし形（回転前の正方形）の一辺。
    public float platformSquashY = 0.55f; // 縦つぶし率（isometric感）。
    public float platformFeetNudge = 24f; // アイコン下端からの持ち上げ（足に少し重ねる）。

    // 購入済みで空スロット表示にしている間は true（円＋空六角形のみ）。
    private bool isEmptyPurchased;
    private static Sprite manaInactiveSprite;

    // ショップ側が「このカードに有効なユニットが入っているか」を確認するための読み取り専用情報です。
    public bool HasData => myData.prefab != null && !isEmptyPurchased;
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
        ClearUnitSpritePreview(); // 非表示/リユース時に盤外実体を破棄（リーク防止）。
    }

    private void OnDestroy()
    {
        ClearUnitSpritePreview();
    }

    // R5-shop-duelyst P1: 盤外にユニット実体を生成し、その SpriteRenderer を icon へミラー表示する。
    // 盤面の同ユニットとは別インスタンス（別Animator）なのでアニメは連動しない。
    private void SetupUnitSpritePreview(EntitiesDatabaseSO.EntityData data)
    {
        ClearUnitSpritePreview();
        if (icon == null) return;
        icon.preserveAspect = true;

        if (data.prefab == null)
        {
            // 実体が無い場合のみ従来アイコンへフォールバック。
            icon.sprite = data.icon;
            return;
        }

        BaseEntity preview = Instantiate(data.prefab);
        // 画面外（メインカメラ視錐台外）へ。各カードで少しずらして重なりを避ける。
        previewSpawnCounter++;
        preview.transform.position = new Vector3(-100000f - previewSpawnCounter * 40f, -100000f, 0f);
        preview.InitializeIdentity(data.name, Mathf.Max(1, data.cost), 1);

        // GameScene には GameManager が居るため、実体の Start がラウンドイベント購読等の副作用を起こす。
        // 見た目(Animator/SpriteRenderer)以外の挙動を止める（Animatorは MonoBehaviour ではないので動き続ける）。
        foreach (MonoBehaviour mb in preview.GetComponentsInChildren<MonoBehaviour>(true))
            mb.enabled = false;
        foreach (Collider2D col in preview.GetComponentsInChildren<Collider2D>(true))
            col.enabled = false;

        previewInstance = preview.gameObject;

        previewMirror = icon.GetComponent<CollectionBossAnimator>();
        if (previewMirror == null) previewMirror = icon.gameObject.AddComponent<CollectionBossAnimator>();
        previewMirror.source = preview.spriteRender;
        previewMirror.target = icon;
        if (preview.spriteRender != null && preview.spriteRender.sprite != null)
            icon.sprite = preview.spriteRender.sprite;
    }

    private void ClearUnitSpritePreview()
    {
        if (previewMirror != null) previewMirror.source = null; // ミラー停止（コンポーネントは再利用）。
        if (previewInstance != null)
        {
            Destroy(previewInstance);
            previewInstance = null;
        }
    }

    // オリジナルDuelyst流の足元土台を生成（1回だけ）。
    // 原Duelyst(BottomDeckCardNode)と同じく：card_background(丸背景リング)＋unit_shadow(影)を敷く。
    // card_background は X軸回転で「地面に寝た円」にして、影とユニットを囲む角度付き円にする。
    private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.72f);
    public float cardBgTiltX = 58f;    // 丸背景をX軸で寝かせる角度（度）。
    public float cardBgScale = 1.55f;  // 丸背景の大きさ（platformSize倍）。
    private Image cardBgImage;
    private static Sprite cardBgNormal, cardBgHighlight;
    private void EnsureFootPlatform()
    {
        if (footSquash != null) return;
        if (platformSprite == null) platformSprite = Resources.Load<Sprite>("UI/Duelyst/unit_shadow");
        if (cardBgNormal == null) cardBgNormal = Resources.Load<Sprite>("UI/Duelyst/card_background");
        if (cardBgHighlight == null) cardBgHighlight = Resources.Load<Sprite>("UI/Duelyst/card_background_highlight");

        // 足元位置＝アイコン下端あたり。
        float feetY = -platformSize * 0.4f;
        if (icon != null)
            feetY = icon.rectTransform.anchoredPosition.y - icon.rectTransform.sizeDelta.y * 0.5f + platformFeetNudge;
        footFeetY = feetY;

        // ① 丸い背景リング(card_background)を X軸回転で寝かせて最背面に。
        GameObject bgGo = new GameObject("CardBackground", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(transform, false);
        RectTransform bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
        bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.sizeDelta = new Vector2(platformSize * cardBgScale, platformSize * cardBgScale);
        bgRt.anchoredPosition = new Vector2(0f, feetY);
        bgRt.localEulerAngles = new Vector3(cardBgTiltX, 0f, 0f);
        cardBgImage = bgGo.GetComponent<Image>();
        if (cardBgNormal != null) cardBgImage.sprite = cardBgNormal;
        cardBgImage.raycastTarget = false;
        cardBgImage.preserveAspect = true;
        bgGo.transform.SetAsFirstSibling(); // 最背面。

        // ② 影(unit_shadow)を丸背景の上に。
        GameObject shadowGo = new GameObject("FootShadow", typeof(RectTransform), typeof(Image));
        shadowGo.transform.SetParent(transform, false);
        footSquash = shadowGo.GetComponent<RectTransform>();
        footSquash.anchorMin = footSquash.anchorMax = new Vector2(0.5f, 0.5f);
        footSquash.pivot = new Vector2(0.5f, 0.5f);
        footSquash.sizeDelta = new Vector2(platformSize * 1.1f, platformSize * 0.56f);
        footSquash.anchoredPosition = new Vector2(0f, feetY);
        footSquash.localScale = Vector3.one;
        footDiamond = shadowGo.GetComponent<Image>();
        if (platformSprite != null) footDiamond.sprite = platformSprite;
        footDiamond.raycastTarget = false;
        footDiamond.preserveAspect = false;
        footDiamond.color = ShadowColor;
        footGlow = null;
        footSquash.SetSiblingIndex(1); // card_background(0)の上・ユニットの下。
    }

    // コストを Duelyst 流に「icon_mana(青い六角形)＋中央に数字(濃紺)」で表示する。
    private Image costBadge;
    private static Sprite manaHexSprite;
    private void EnsureCostBadge()
    {
        if (cost == null) return;
        if (manaHexSprite == null) manaHexSprite = Resources.Load<Sprite>("UI/Duelyst/icon_mana");

        // 旧コスト表示（ManaGemの "coin"）は新しい六角形バッジと二重になるので隠す。
        if (cost.transform.parent != null)
        {
            Transform oldCoin = cost.transform.parent.Find("coin");
            if (oldCoin != null) oldCoin.gameObject.SetActive(false);
        }

        if (costBadge == null)
        {
            GameObject go = new GameObject("CostBadge", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(cost.transform.parent, false);
            costBadge = go.GetComponent<Image>();
            costBadge.raycastTarget = false;
            costBadge.preserveAspect = true;
        }
        if (manaHexSprite != null) costBadge.sprite = manaHexSprite;

        // バッジ・数字を「丸背景のフチ」に置く：円の中心(footFeetY)から costRimDrop 下げ、
        // コスト画像の中心が円の下フチと重なる位置にする。
        float costY = footFeetY - costRimDrop;
        RectTransform brt = costBadge.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.anchoredPosition = new Vector2(0f, costY);
        brt.sizeDelta = new Vector2(48f, 52f);

        RectTransform crt = cost.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = new Vector2(0f, costY + 1f);
        crt.sizeDelta = new Vector2(48f, 48f);

        // 数字は六角形の中央・濃紺（原Duelyst: rgb(0,33,159)）・大きめ固定。読みやすさに薄い白縁。
        cost.enableAutoSizing = false;
        cost.enableWordWrapping = false;
        cost.fontSize = 23f;
        cost.color = new Color(0f, 33f / 255f, 159f / 255f, 1f);
        cost.alignment = TextAlignmentOptions.Center;
        cost.fontStyle = FontStyles.Bold;
        cost.outlineWidth = 0.12f;
        cost.outlineColor = new Color(1f, 1f, 1f, 0.6f);

        // 前後: バッジ→数字。
        costBadge.transform.SetSiblingIndex(cost.transform.GetSiblingIndex());
        cost.transform.SetAsLastSibling();
    }

    // カード下端からのコスト表示の高さ（旧・未使用）。
    public float cardCostBottomY = 16f;
    private float footFeetY;             // 足元（丸背景の中心）Y。
    public float costRimDrop = 54f;      // 円の中心→下フチ。コスト画像の中心をここに置く。

    // 足元土台＋コストバッジを毎 Setup で反映（土台色はコストに依らず影のまま）。
    private void ApplyFootPlatform(int cost)
    {
        EnsureFootPlatform();
        defaultPlatformColor = ShadowColor;
        if (footDiamond != null) footDiamond.color = ShadowColor;
        EnsureCostBadge();
        RefreshCardBackground();
    }

    // ホバーで丸背景をハイライトに。
    private bool isHovered;
    private static Sprite cardBgDisabled;
    public void OnPointerEnter(PointerEventData eventData) { isHovered = true; RefreshCardBackground(); }
    public void OnPointerExit(PointerEventData eventData) { isHovered = false; RefreshCardBackground(); }

    // 丸背景(card_background)を状態で切替：ホバー/スターアップ=highlight、購入不可=disabled、通常=normal。
    // 原Duelyst(BottomDeckCardNode.showCardBackground)の状態遷移に対応。
    public void RefreshCardBackground()
    {
        if (cardBgImage == null) return;
        if (cannotBuyAnimating) return; // 購入不可演出中は上書きしない。
        if (isEmptyPurchased) return;   // 購入済み空スロットは暗円のまま（ホバーでも光らせない）。
        if (cardBgNormal == null) cardBgNormal = Resources.Load<Sprite>("UI/Duelyst/card_background");
        if (cardBgHighlight == null) cardBgHighlight = Resources.Load<Sprite>("UI/Duelyst/card_background_highlight");
        if (cardBgDisabled == null) cardBgDisabled = Resources.Load<Sprite>("UI/Duelyst/card_background_disabled");

        bool ready = upgradePreviewStarLevel > 0;
        bool affordable = !HasData || PlayerData.Instance == null || PlayerData.Instance.CanAfford(myData.cost);

        Sprite target = cardBgNormal;
        if (isHovered || ready) target = cardBgHighlight;
        else if (!affordable) target = cardBgDisabled;
        if (target != null) cardBgImage.sprite = target;
    }

    // コスト帯 → 枠色（Duelystのレアリティ配色風：1灰/2緑/3青/4紫/5橙）。調整可。
    private static Color CostTierColor(int cost)
    {
        switch (Mathf.Clamp(cost, 1, 5))
        {
            case 1: return new Color(0.50f, 0.55f, 0.62f, 0.96f); // common: steel grey
            case 2: return new Color(0.33f, 0.58f, 0.40f, 0.96f); // green
            case 3: return new Color(0.26f, 0.46f, 0.78f, 0.96f); // blue (rare)
            case 4: return new Color(0.52f, 0.33f, 0.72f, 0.96f); // purple (epic)
            default: return new Color(0.86f, 0.55f, 0.22f, 0.96f); // orange (legendary)
        }
    }

    // ショップがカードを生成・更新する時に呼びます。
    // EntityDataの内容をUIへ反映し、このカードをクリックした時にショップへ通知できるようにします。
    public void Setup(EntitiesDatabaseSO.EntityData myData, UIShop shopRef)
    {
        EnsureFrameReference();

        // 前回の「購入済み(空スロット)」表示を解除して、通常のカードへ戻す。
        isEmptyPurchased = false;
        if (icon != null) icon.enabled = true;
        if (name != null) name.enabled = true;

        // R5 P1: カードの顔をユニットのドット絵（実体のSpriteRendererミラー）へ。AIイラスト(myData.icon)は不使用。
        SetupUnitSpritePreview(myData);
        // R5 P7: 角丸枠(frame)は撤去し、Duelyst風の「足元のひし形足場」でユニットを盤面のように立たせる。
        if (frame != null) frame.enabled = false;
        ApplyFootPlatform(myData.cost);
        ConfigureCardText(name);
        ConfigureCardText(cost);
        name.text = LocalizationManager.UnitName(myData.name);
        cost.text = myData.cost.ToString();
        EnsureCostBadge(); // ConfigureCardText 後に再適用してフォント/中央下位置を確定。

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
        if (!HasData) return; // 空スロット/無データは無反応。

        // 購入不可（マナ不足/ベンチ満杯等）なら、スロットを揺らして赤円(replaced)→ゆっくり暗円へ。
        bool canBuy = shopRef != null
            && GameManager.Instance != null && PlayerData.Instance != null
            && GameManager.Instance.CanBuyEntity(myData)
            && PlayerData.Instance.CanAfford(myData.cost);
        if (!canBuy)
        {
            PlayCannotBuyFeedback();
            return;
        }
        shopRef.OnCardClick(this, myData);
    }

    // 購入後：スロットの円は残し、ユニット/名前/シナジーを消し、コストを「数字なしの空六角形(icon_mana_inactive)」にする。
    public void ShowPurchasedEmpty()
    {
        isEmptyPurchased = true;
        cannotBuyTween?.Kill();
        ClearUnitSpritePreview();
        if (icon != null) { icon.sprite = null; icon.enabled = false; }
        if (name != null) name.enabled = false;
        for (int i = 0; i < synergyBadgeObjects.Count; i++)
            if (synergyBadgeObjects[i] != null) synergyBadgeObjects[i].SetActive(false);
        if (upgradeGlow != null) upgradeGlow.gameObject.SetActive(false);
        if (upgradeBadge != null) upgradeBadge.gameObject.SetActive(false);

        // コスト＝空六角形、数字は消す。
        if (manaInactiveSprite == null) manaInactiveSprite = Resources.Load<Sprite>("UI/Duelyst/icon_mana_inactive");
        if (costBadge != null && manaInactiveSprite != null) { costBadge.enabled = true; costBadge.sprite = manaInactiveSprite; }
        if (cost != null) cost.text = string.Empty;

        // 円は暗い(disabled)状態に。
        if (cardBgImage != null)
        {
            cardBgImage.color = Color.white;
            if (cardBgDisabled == null) cardBgDisabled = Resources.Load<Sprite>("UI/Duelyst/card_background_disabled");
            if (cardBgDisabled != null) cardBgImage.sprite = cardBgDisabled;
        }
    }

    // 購入不可クリック時の演出：スロットを軽く揺らし、丸背景を赤(replaced)にしてから
    // ゆっくり暗い円(disabled)へフェードで戻す。
    private static Sprite cardBgReplaced;
    private bool cannotBuyAnimating;
    private Tween cannotBuyTween;
    private void PlayCannotBuyFeedback()
    {
        AttackEffectPlayer.PlayUiSfx("error");

        // 揺らす（ポーズ中も動くよう SetUpdate(true)）。
        RectTransform rt = transform as RectTransform;
        if (rt != null)
        {
            rt.DOKill();
            rt.DOShakeAnchorPos(0.35f, new Vector2(11f, 0f), 20, 90f, false, true).SetUpdate(true);
        }

        if (cardBgImage == null) return;
        if (cardBgReplaced == null) cardBgReplaced = Resources.Load<Sprite>("UI/Duelyst/card_background_replaced");
        if (cardBgDisabled == null) cardBgDisabled = Resources.Load<Sprite>("UI/Duelyst/card_background_disabled");

        cannotBuyTween?.Kill();
        cardBgImage.DOKill();
        cannotBuyAnimating = true;

        // 赤円へ即切替 → 少し保持 → フェードアウト → 暗円をフェードイン。
        cardBgImage.sprite = cardBgReplaced;
        cardBgImage.color = Color.white;

        Sequence seq = DOTween.Sequence().SetUpdate(true);
        seq.AppendInterval(0.45f);
        seq.Append(cardBgImage.DOFade(0f, 0.7f));
        seq.AppendCallback(() =>
        {
            if (cardBgImage != null && cardBgDisabled != null) cardBgImage.sprite = cardBgDisabled;
        });
        seq.Append(cardBgImage.DOFade(1f, 0.5f));
        seq.OnComplete(() =>
        {
            cannotBuyAnimating = false;
            RefreshCardBackground();
        });
        cannotBuyTween = seq;
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

        // スターアップ予告時だけ足元の影を薄く強調色に、通常時は影のまま。
        if (footDiamond != null)
        {
            if (ready)
            {
                Color pc = GetUpgradeColor(starLevel);
                footDiamond.color = new Color(pc.r * 0.5f, pc.g * 0.5f, pc.b * 0.5f, 0.6f);
            }
            else
            {
                footDiamond.color = ShadowColor;
            }
        }

        // 丸背景(card_background)を状態で更新（ホバー/スターアップ/購入不可）。
        RefreshCardBackground();

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

        // アイコン/フレームをユーザー指定サイズへ揃える（1回だけ）。
        // 既定: アイコン 255×140 / フレーム 255×150・縦位置+11.5。インスペクタで微調整可。
        if (!frameSizeAdjusted)
        {
            frameSizeAdjusted = true;
            if (icon != null)
                icon.rectTransform.sizeDelta = iconTargetSize;
            if (frame != null)
            {
                RectTransform fr = frame.rectTransform;
                fr.sizeDelta = new Vector2(frameTargetWidth, frameTargetHeight);
                fr.anchoredPosition = new Vector2(fr.anchoredPosition.x, frameVerticalOffset);
            }
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
