using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

// UIボタンに後付けできる、見た目専用の押下/表示アニメーションです。
// 購入や戦闘開始などのゲーム処理は持たず、DOTweenへの依存を演出だけに閉じ込めます。
[DisallowMultipleComponent]
public class TweenButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public float appearDuration = 0.22f;
    public float pressDuration = 0.08f;
    public float releaseDuration = 0.12f;
    public float appearStartScale = 0.9f;
    public float pressedScale = 0.94f;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector3 baseScale = Vector3.one;
    private Tween scaleTween;
    private Tween fadeTween;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        baseScale = rectTransform != null ? rectTransform.localScale : Vector3.one;
    }

    private void OnDisable()
    {
        KillTweens(false);
    }

    public void PlayAppear(float delay = 0f)
    {
        CacheReferences();
        if (rectTransform == null || canvasGroup == null || !gameObject.activeInHierarchy)
            return;

        KillTweens(false);
        baseScale = rectTransform.localScale == Vector3.zero ? Vector3.one : rectTransform.localScale;
        canvasGroup.alpha = 0f;
        rectTransform.localScale = baseScale * appearStartScale;

        Sequence sequence = DOTween.Sequence()
            .SetTarget(this)
            .SetUpdate(true)
            .AppendInterval(Mathf.Max(0f, delay))
            .Append(canvasGroup.DOFade(1f, Mathf.Max(0.01f, appearDuration * 0.75f)).SetEase(Ease.OutQuad))
            .Join(rectTransform.DOScale(baseScale, Mathf.Max(0.01f, appearDuration)).SetEase(Ease.OutBack));

        scaleTween = sequence;
        fadeTween = sequence;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        TweenToScale(baseScale * pressedScale, pressDuration, Ease.OutQuad);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        TweenToScale(baseScale, releaseDuration, Ease.OutBack);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TweenToScale(baseScale, releaseDuration, Ease.OutBack);
    }

    private void CacheReferences()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void TweenToScale(Vector3 targetScale, float duration, Ease ease)
    {
        CacheReferences();
        if (rectTransform == null)
            return;

        scaleTween?.Kill(false);
        scaleTween = rectTransform
            .DOScale(targetScale, Mathf.Max(0.01f, duration))
            .SetEase(ease)
            .SetUpdate(true)
            .SetTarget(this);
    }

    private void KillTweens(bool complete)
    {
        scaleTween?.Kill(complete);
        fadeTween?.Kill(complete);
        scaleTween = null;
        fadeTween = null;
    }
}
