using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.Common;

public abstract class EnemyModel_abstract : MonoBehaviour
{
    protected EnemyPresenter_abstract presenter = null;
    public EnemyPresenter_abstract Presenter { get => presenter; set { if( presenter == null) presenter = value; } }

    // フォールバック値.
    private static readonly Vector2 defaultStageMin = new Vector2(-13f, -5f);
    private static readonly Vector2 defaultStageMax = new Vector2(13f, 0.5f);

    // EnemyStageBoundsMarker があればそちらを参照、なければフォールバック.
    public Vector2 StageMin
    {
        get
        {
            var marker = EnemyStageBoundsMarker.Current;
            return marker != null ? marker.StageMin : defaultStageMin;
        }
    }
    public Vector2 StageMax
    {
        get
        {
            var marker = EnemyStageBoundsMarker.Current;
            return marker != null ? marker.StageMax : defaultStageMax;
        }
    }

    protected Animator animator;
    public Animator Animator => animator;
    protected Rigidbody2D rigidbody;
    public Rigidbody2D Rigidbody => rigidbody;
    protected EnemyState currentState = EnemyState.Prepare;

    protected Vector2 moveSpeed = new Vector2(2,0);
    public Vector2 MoveSpeed => moveSpeed;

    // アニメーション速度を取得.
    public float AnimSpeed => animator != null ? animator.speed : 1f;

    // Platform検出（Awakeで初期化 — Unity APIはフィールド初期化子から呼べないため）.
    protected PlatformDetector platformDetector;

    /// <summary>
    /// Platform検出器を取得.
    /// </summary>
    public PlatformDetector PlatformDetector => platformDetector;

    /// <summary>
    /// 足元にPlatformがあるか.
    /// </summary>
    public bool IsOnPlatform => platformDetector.IsOnPlatform;

    /// <summary>
    /// ジャンプ中フラグ. trueの間はFixedUpdateのPlatform落下制御をスキップする.
    /// とびかかり切りなどisTrigger状態でジャンプする攻撃で使用.
    /// </summary>
    public bool IsJumping { get; set; } = false;

    protected virtual void Awake()
    {
        Debug.Log($"[EnemyModel_abstract] Awake - {gameObject.name}");
        animator = GetComponent<Animator>();
        rigidbody = GetComponent<Rigidbody2D>();
        platformDetector = new PlatformDetector(0.5f, 0.1f);
        Debug.Log($"[EnemyModel_abstract] Awake完了 - Animator: {(animator != null ? "取得" : "null")}, Rigidbody: {(rigidbody != null ? "取得" : "null")}");
    }

    // キャッシュ用Collider参照.
    private Collider2D cachedCollider;

    /// <summary>
    /// Colliderをキャッシュ付きで取得.
    /// </summary>
    protected Collider2D GetCachedCollider()
    {
        if (cachedCollider == null)
        {
            cachedCollider = GetComponent<Collider2D>();
        }
        return cachedCollider;
    }

    /// <summary>
    /// FixedUpdate: 足元のPlatformレイキャスト検出と落下制御.
    /// </summary>
    protected virtual void FixedUpdate()
    {
        if (rigidbody == null) return;

        Collider2D col = GetCachedCollider();
        Vector2 feetPos;
        float centerY;

        if (col != null)
        {
            feetPos = new Vector2(col.bounds.center.x, col.bounds.min.y);
            centerY = col.bounds.center.y;
        }
        else
        {
            feetPos = (Vector2)transform.position;
            centerY = transform.position.y;
        }

        // ジャンプ中はPlatform検出・落下制御をスキップ.
        if (IsJumping) return;

        // 足元のPlatformをレイキャストで検出（中心位置で上下判定）.
        platformDetector.CheckPlatformBelow(feetPos, centerY, rigidbody.linearVelocity.y);

        // Platform上にいて下方向に落下中の場合、落下を無効化.
        if (platformDetector.IsOnPlatform && rigidbody.linearVelocity.y <= 0f)
        {
            rigidbody.linearVelocity = new Vector2(rigidbody.linearVelocity.x, 0f);

            // Platformの表面に位置をクランプ（上り坂・下り坂の両方に追従）.
            if (col != null)
            {
                float clampDiff = platformDetector.ClampToPlatformSurface(col.bounds.min.y);
                if (Mathf.Abs(clampDiff) > 0.001f)
                {
                    transform.position += new Vector3(0f, clampDiff, 0f);
                }
            }
        }
    }

    protected EnemAIModel_abstract AIModel = new EnemAIModel_normal();

    // 登場時の演出開始関数（継承クラスでオーバーライド可能）.
    public virtual async UniTask StartAppearPerformance()
    {
        Debug.Log($"[EnemyModel_abstract] StartAppearPerformance - {gameObject.name}");
        await UniTask.CompletedTask;
    }

    public virtual void EnemAIStart()
    {
        Debug.Log($"[EnemyModel_abstract] EnemAIStart - {gameObject.name}");
        AIModel.SetOwnerTransform(transform);
        AIModel.SetOwnerModel(this);
        AIModel.StartLoop();
        Debug.Log($"[EnemyModel_abstract] EnemAIStart完了 - AIループ開始");
    }

    public virtual void EnemAIStop()
    {
        Debug.Log($"[EnemyModel_abstract] EnemAIStop - {gameObject.name}");
        AIModel.StopLoop();
    }

    public void TestInit()
    {
        Debug.Log($"[EnemyModel_abstract] TestInit - {gameObject.name}");
        EnemAIStart();
    }
}
