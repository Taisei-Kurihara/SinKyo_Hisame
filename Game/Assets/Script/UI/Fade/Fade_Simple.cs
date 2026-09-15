using System;
using LitMotion;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CanvasGroupを使用しないフェード実装.
/// Graphic(Image, RawImage, Text等)のcolorアルファを直接操作する.
/// </summary>
public class Fade_Simple : Fade_abstract
{
    [SerializeField]
    private Graphic _target;

    protected override MotionHandle CreateFadeMotion(float from, float to, float duration, Action onComplete)
    {
        return LMotion.Create(from, to, duration)
            .WithEase(GetEase())
            .WithOnComplete(() => onComplete?.Invoke())
            .Bind(x =>
            {
                var c = _target.color;
                c.a = x;
                _target.color = c;
            });
    }
}
