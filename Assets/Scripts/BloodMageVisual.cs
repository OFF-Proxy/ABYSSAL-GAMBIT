using System.Collections;
using UnityEngine;

// アルカナの「BloodMage」VFX を制御します。
// start を一度再生してから loop を再生し続け、PlayEnd() で end を再生してから自動的に消えます。
// 引き寄せ・ダメージなどのゲーム挙動は呼び出し側（BaseEntity 側のコルーチン）が担当し、本コンポーネントは見た目専用です。
public class BloodMageVisual : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Sprite[] startSprites;
    private Sprite[] loopSprites;
    private Sprite[] endSprites;
    private float frameRate = 16f;
    private bool ending;

    public bool IsEnding => ending;

    // スプライト配列・色・描画順・FPS を受け取って再生を開始します。
    public void Initialize(Sprite[] start, Sprite[] loop, Sprite[] end, Color color, int sortingOrder, float fps)
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        startSprites = start;
        loopSprites = loop;
        endSprites = end;
        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = sortingOrder;
        if (fps > 0f)
            frameRate = fps;

        StartCoroutine(PlayStartThenLoop());
    }

    // start を1回再生し、その後 loop を固定位置で再生し続けます。
    private IEnumerator PlayStartThenLoop()
    {
        float frameTime = 1f / Mathf.Max(1f, frameRate);

        if (startSprites != null)
        {
            for (int i = 0; i < startSprites.Length && !ending; i++)
            {
                spriteRenderer.sprite = startSprites[i];
                yield return new WaitForSeconds(frameTime);
            }
        }

        int index = 0;
        while (!ending)
        {
            if (loopSprites != null && loopSprites.Length > 0)
            {
                spriteRenderer.sprite = loopSprites[index % loopSprites.Length];
                index++;
            }

            yield return new WaitForSeconds(frameTime);
        }
    }

    // end を再生してから GameObject を破棄します。多重呼び出しは無視します。
    public void PlayEnd()
    {
        if (ending)
            return;

        ending = true;
        StopAllCoroutines();
        StartCoroutine(PlayEndCoroutine());
    }

    private IEnumerator PlayEndCoroutine()
    {
        float frameTime = 1f / Mathf.Max(1f, frameRate);

        if (endSprites != null)
        {
            for (int i = 0; i < endSprites.Length; i++)
            {
                if (spriteRenderer != null)
                    spriteRenderer.sprite = endSprites[i];
                yield return new WaitForSeconds(frameTime);
            }
        }

        Destroy(gameObject);
    }
}
