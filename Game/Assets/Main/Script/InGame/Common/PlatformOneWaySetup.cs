using UnityEngine;

namespace InGame.Common
{
    /// <summary>
    /// ワンウェイプラットフォームのセットアップ用コンポーネント.
    /// Platformレイヤーのオブジェクトにアタッチすることで
    /// PlatformEffector2Dを自動設定し、下からの通り抜けを実現する.
    ///
    /// 使用方法:
    /// 1. 足場オブジェクトのLayerを "Platform"（Layer 9）に設定する.
    /// 2. BoxCollider2D（またはEdgeCollider2D）をアタッチする.
    /// 3. このコンポーネントをアタッチする.
    ///    → Awake時にPlatformEffector2Dが自動追加・設定される.
    ///    → Collider2DのusedByEffectorが自動でtrueになる.
    ///
    /// PlatformEffector2Dの動作:
    /// - 上方向からのみ衝突する（下からは通り抜けられる）.
    /// - surfaceArc: 上方向180度の範囲で衝突判定.
    /// - プレイヤー/Enemyがすり抜けたい場合はPlatformDetector.StartDropThroughを使用.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PlatformOneWaySetup : MonoBehaviour
    {
        [Header("PlatformEffector2D 設定")]
        [Tooltip("衝突する上方向の角度範囲（度）.")]
        [SerializeField] private float surfaceArc = 170f;

        [Tooltip("側面の摩擦を使用するか.")]
        [SerializeField] private bool useSideFriction = false;

        [Tooltip("側面のバウンスを使用するか.")]
        [SerializeField] private bool useSideBounce = false;

        private void Awake()
        {
            // PlatformEffector2Dを追加（既にあれば取得）.
            PlatformEffector2D effector = GetComponent<PlatformEffector2D>();
            if (effector == null)
            {
                effector = gameObject.AddComponent<PlatformEffector2D>();
            }

            // ワンウェイ設定.
            effector.useOneWay = true;
            effector.surfaceArc = surfaceArc;
            effector.useSideFriction = useSideFriction;
            effector.useSideBounce = useSideBounce;

            // Collider2DのusedByEffectorを有効化.
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.usedByEffector = true;
            }
        }
    }
}
