using UnityEngine;
using UnityEngine.UI;

// R5-shop-duelyst: 脱AIのカード表示ヘルパ。ショップ以外（ボス報酬/ロスター/図鑑ハブ 等）でも
// AIイラスト(EntityData.icon)/AI枠(EntityData.frame) をやめ、ユニットのドット絵＋プログラム枠に統一する。
public static class UnitCardVisual
{
    // コスト帯 → 枠色（Duelystのレアリティ配色風：1灰/2緑/3青/4紫/5橙）。UICard と一致させる。
    public static Color CostTierColor(int cost)
    {
        switch (Mathf.Clamp(cost, 1, 5))
        {
            case 1: return new Color(0.50f, 0.55f, 0.62f, 0.96f);
            case 2: return new Color(0.33f, 0.58f, 0.40f, 0.96f);
            case 3: return new Color(0.26f, 0.46f, 0.78f, 0.96f);
            case 4: return new Color(0.52f, 0.33f, 0.72f, 0.96f);
            default: return new Color(0.86f, 0.55f, 0.22f, 0.96f);
        }
    }

    private static Sprite roundedFrameSprite;

    // AI枠に頼らず、コスト帯で色分けした角丸フレームをプログラム描画する。
    public static void ApplyProceduralFrame(Image frame, int cost)
    {
        if (frame == null) return;
        if (roundedFrameSprite == null)
            roundedFrameSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        frame.sprite = roundedFrameSprite;
        frame.type = Image.Type.Sliced;
        frame.color = CostTierColor(cost);
    }
}

// 指定 Image に、ユニット実体のドット絵（盤外インスタンスの SpriteRenderer ミラー）を表示するコンポーネント。
// 盤面の同ユニットとは別インスタンス（別Animator）なのでアニメは非連動。生成体は本コンポーネントの寿命で破棄。
public class UnitCardPreview : MonoBehaviour
{
    private GameObject instance;
    private CollectionBossAnimator mirror;
    private static int spawnCounter;

    // target=写す先の Image（未指定なら自身の Image）。data=表示ユニット。
    public void Bind(EntitiesDatabaseSO.EntityData data, Image target = null)
    {
        Clear();
        Image img = target != null ? target : GetComponent<Image>();
        if (img == null) return;
        img.preserveAspect = true;

        if (data.prefab == null)
        {
            img.sprite = data.icon; // 実体が無い場合のみ従来アイコンへフォールバック。
            return;
        }

        BaseEntity p = Object.Instantiate(data.prefab);
        spawnCounter++;
        p.transform.position = new Vector3(-100000f - spawnCounter * 40f, -100000f, 0f);
        p.InitializeIdentity(data.name, Mathf.Max(1, data.cost), 1);
        // GameScene/ロビーでの Start 購読など見た目以外の副作用を止める（Animator は別Componentなので動き続ける）。
        foreach (MonoBehaviour mb in p.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
        foreach (Collider2D c in p.GetComponentsInChildren<Collider2D>(true)) c.enabled = false;
        instance = p.gameObject;

        mirror = img.GetComponent<CollectionBossAnimator>();
        if (mirror == null) mirror = img.gameObject.AddComponent<CollectionBossAnimator>();
        mirror.source = p.spriteRender;
        mirror.target = img;
        if (p.spriteRender != null && p.spriteRender.sprite != null) img.sprite = p.spriteRender.sprite;
    }

    private void Clear()
    {
        if (mirror != null) mirror.source = null;
        if (instance != null) { Destroy(instance); instance = null; }
    }

    private void OnDisable() { Clear(); }
    private void OnDestroy() { Clear(); }
}
