using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Common
{
    /// <summary>
    /// Image.color の非同期アニメーションユーティリティ.
    /// Time.realtimeSinceStartup 使用のため timeScale=0 でも動作する.
    ///
    /// 【提供メソッド】
    ///  - LerpAsync(Color→Color):  任意の2色間をduration秒でLerp
    ///  - LerpAsync(float→float):  グレースケール値でLerp（RGBA全チャンネル同値）
    ///  - BlinkLoopAsync:           2色間を往復ループ
    ///  - BlinkOnceAsync:           1サイクル（A→B→A）再生
    /// </summary>
    public static class ImageColorAnimator
    {
        // ============================
        // === Lerp ===
        // ============================

        /// <summary>
        /// Image.color を from → to へ duration 秒かけて Lerp する.
        /// </summary>
        public static async UniTask LerpAsync(Image img, Color from, Color to, float duration, CancellationToken token)
        {
            float startTime = Time.realtimeSinceStartup;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                float elapsed = Time.realtimeSinceStartup - startTime;
                if (elapsed >= duration)
                {
                    img.color = to;
                    return;
                }
                float t = elapsed / duration;
                img.color = Color.Lerp(from, to, t);
                await UniTask.Yield(token);
            }
        }

        /// <summary>
        /// Image.color をグレースケール値(RGBA同値)で from → to へ duration 秒かけて Lerp する.
        /// </summary>
        public static async UniTask LerpAsync(Image img, float from, float to, float duration, CancellationToken token)
        {
            await LerpAsync(
                img,
                new Color(from, from, from, from),
                new Color(to, to, to, to),
                duration,
                token
            );
        }

        // ============================
        // === Blink ===
        // ============================

        /// <summary>
        /// 2色間を halfDuration 秒周期で往復ループ（キャンセルまで継続）.
        /// </summary>
        public static async UniTaskVoid BlinkLoopAsync(Image img, Color a, Color b, float halfDuration, CancellationToken token)
        {
            try
            {
                img.color = a;
                while (!token.IsCancellationRequested)
                {
                    await LerpAsync(img, a, b, halfDuration, token);
                    await LerpAsync(img, b, a, halfDuration, token);
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// グレースケール値で2値間を往復ループ.
        /// </summary>
        public static UniTaskVoid BlinkLoopAsync(Image img, float a, float b, float halfDuration, CancellationToken token)
        {
            return BlinkLoopAsync(
                img,
                new Color(a, a, a, a),
                new Color(b, b, b, b),
                halfDuration,
                token
            );
        }

        /// <summary>
        /// 1サイクル（A→B→A）を再生して完了.
        /// </summary>
        public static async UniTask BlinkOnceAsync(Image img, Color a, Color b, float halfDuration, CancellationToken token)
        {
            img.color = a;
            await LerpAsync(img, a, b, halfDuration, token);
            await LerpAsync(img, b, a, halfDuration, token);
        }

        /// <summary>
        /// グレースケール値で1サイクル再生.
        /// </summary>
        public static async UniTask BlinkOnceAsync(Image img, float a, float b, float halfDuration, CancellationToken token)
        {
            await BlinkOnceAsync(
                img,
                new Color(a, a, a, a),
                new Color(b, b, b, b),
                halfDuration,
                token
            );
        }
    }
}
