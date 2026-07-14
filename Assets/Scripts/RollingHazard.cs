using System.Collections.Generic;
using UnityEngine;

// ③b 転がる巨大物。戦闘中に盤面の1行を端から端へ転がり、経路上のユニットを敵味方問わず潰す（ダメージ＋スタン）。
// GameManager.LaunchRollingHazard から生成される。数値は暫定（R3-balance）。
public class RollingHazard : MonoBehaviour
{
    private float speed;
    private float damageFraction;
    private float stunDuration;
    private float endX;
    private int dir;
    private const float HitRadiusX = 0.7f;
    private const float HitRadiusY = 0.95f;
    private readonly HashSet<BaseEntity> hitUnits = new HashSet<BaseEntity>();
    private static Sprite cachedCircle;

    public static void Launch(float startX, float endX, float y, float speed, float damageFraction, float stunDuration)
    {
        GameObject go = new GameObject("RollingHazard");
        go.transform.position = new Vector3(startX, y, 0f);
        RollingHazard h = go.AddComponent<RollingHazard>();
        h.speed = Mathf.Max(0.5f, speed);
        h.endX = endX;
        h.dir = endX >= startX ? 1 : -1;
        h.damageFraction = Mathf.Clamp01(damageFraction);
        h.stunDuration = Mathf.Max(0f, stunDuration);
        h.BuildVisual();
    }

    private void BuildVisual()
    {
        SpriteRenderer sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = GetCircleSprite();
        sr.color = new Color(0.2f, 0.17f, 0.24f, 0.97f);
        sr.sortingOrder = 5000;
        transform.localScale = Vector3.one * 1.7f;
    }

    private void Update()
    {
        transform.position += new Vector3(dir * speed * Time.deltaTime, 0f, 0f);
        transform.Rotate(0f, 0f, -dir * 240f * Time.deltaTime);
        ApplyHits();

        bool finished = dir > 0 ? transform.position.x > endX : transform.position.x < endX;
        if (finished)
            Destroy(gameObject);
    }

    private void ApplyHits()
    {
        if (GameManager.Instance == null)
            return;
        List<BaseEntity> all = GameManager.Instance.AllBoardEntities();
        if (all == null)
            return;

        Vector3 p = transform.position;
        for (int i = 0; i < all.Count; i++)
        {
            BaseEntity e = all[i];
            if (e == null || e.IsDead || !e.IsOnBoard || hitUnits.Contains(e))
                continue;
            Vector3 ep = e.transform.position;
            if (Mathf.Abs(ep.x - p.x) <= HitRadiusX && Mathf.Abs(ep.y - p.y) <= HitRadiusY)
            {
                hitUnits.Add(e);
                int dmg = Mathf.Max(1, Mathf.RoundToInt(e.MaxHealth * damageFraction));
                e.TakeDamage(dmg);
                if (stunDuration > 0f)
                    e.ApplyStun(stunDuration);
            }
        }
    }

    // 簡易な岩石風の円スプライトを手続き生成（外部素材不要）。
    private static Sprite GetCircleSprite()
    {
        if (cachedCircle != null)
            return cachedCircle;
        int n = 64;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2(n / 2f, n / 2f);
        float radius = n / 2f - 1f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                if (d > radius)
                {
                    tex.SetPixel(x, y, new Color(0, 0, 0, 0));
                    continue;
                }
                float sh = Mathf.Clamp01(1f - d / radius);
                tex.SetPixel(x, y, new Color(0.16f + 0.22f * sh, 0.14f + 0.2f * sh, 0.2f + 0.26f * sh, 1f));
            }
        }
        tex.Apply();
        cachedCircle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return cachedCircle;
    }
}
