using UnityEngine;
using Cysharp.Threading.Tasks;

public abstract class EnemyModel_abstract : MonoBehaviour
{
    protected EnemyPresenter_abstract presenter = null;
    public EnemyPresenter_abstract Presenter { get => presenter; set { if( presenter == null) presenter = value; } }

    protected Vector2 stageMin = new Vector2(-13f, 0f);
    protected Vector2 stageMax = new Vector2(13f, 0f);
    public Vector2 StageMin => stageMin;
    public Vector2 StageMax => stageMax;

    protected Animator animator;
    public Animator Animator => animator;
    protected Rigidbody2D rigidbody;
    public Rigidbody2D Rigidbody => rigidbody;
    protected EnemyState currentState = EnemyState.Prepare;

    protected Vector2 moveSpeed = new Vector2(2,0);
    public Vector2 MoveSpeed => moveSpeed;

    // アニメーション速度を取得.
    public float AnimSpeed => animator != null ? animator.speed : 1f;

    protected virtual void Awake()
    {
        Debug.Log($"[EnemyModel_abstract] Awake - {gameObject.name}");
        animator = GetComponent<Animator>();
        rigidbody = GetComponent<Rigidbody2D>();
        Debug.Log($"[EnemyModel_abstract] Awake完了 - Animator: {(animator != null ? "取得" : "null")}, Rigidbody: {(rigidbody != null ? "取得" : "null")}");
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
