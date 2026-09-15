using System;
using LitMotion;
using UnityEngine;

/// <summary>
/// CanvasGroupのAlphaを使用したフェード実装.
/// </summary>
public class Fade_Alpha : Fade_abstract
{
    [SerializeField]
    private CanvasGroup _canvasGroup;

    protected override MotionHandle CreateFadeMotion(float from, float to, float duration, Action onComplete)
    {
        return LMotion.Create(from, to, duration)
            .WithEase(GetEase())
            .WithOnComplete(() => onComplete?.Invoke())
            .Bind(x => _canvasGroup.alpha = x);
    }
}
