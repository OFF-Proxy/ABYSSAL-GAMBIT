using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using TMPro;
using UnityEngine;

namespace DG.Tweening
{
    public static class DOTweenModuleTMP
    {
        public static TweenerCore<Color, Color, ColorOptions> DOColor(this TMP_Text target, Color endValue, float duration)
        {
            TweenerCore<Color, Color, ColorOptions> tween = DOTween.To(() => target.color, value => target.color = value, endValue, duration);
            tween.SetTarget(target);
            return tween;
        }

        public static TweenerCore<Color, Color, ColorOptions> DOFade(this TMP_Text target, float endValue, float duration)
        {
            TweenerCore<Color, Color, ColorOptions> tween = DOTween.ToAlpha(() => target.color, value => target.color = value, endValue, duration);
            tween.SetTarget(target);
            return tween;
        }
    }
}
