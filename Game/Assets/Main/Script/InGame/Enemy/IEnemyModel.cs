using R3;
using R3.Triggers;
using UnityEngine;
using Cysharp.Threading.Tasks;
using InGame.Player;
namespace InGame.Model
{
    /// <summary>
    /// 敵の状態
    /// </summary>
    public enum EnemStatus
    {
        Normal,     // 通常状態
        Stan,       // スタン状態
        HitBack     // ノックバック状態
    }

    /// <summary>
    /// 敵用モデル。
    /// </summary>
    public interface IEnemyModel 
    {

        public GameObject Character { get; }
        //--------------データ-------------------------
        public int Hp { get; set; }
        public int Str { get; }
        public float Speed { get; }

        /// <summary>
        /// ターゲット範囲
        /// </summary>
        public float TargetCirsol { get;}
        /// <summary>
        /// 進む
        /// </summary>
        public Vector3 DirectionVector { get; set; }

        /// <summary>
        /// AI用アクション
        /// </summary>
        public UniTask action { get; set; }

        /// <summary>
        /// 現在の状態
        /// </summary>
        public EnemStatus nowstatus { get; set; }

        //-----------------関数------------------------
        /// <summary>
        /// AI処理
        /// </summary>
        UniTask AI();

        /// <summary>
        /// ダメージリアクション
        /// </summary>
        void DamageReaction();

        /// <summary>
        /// ヒットバック処理
        /// </summary>
        void HitBack();

        /// <summary>
        /// スタン処理
        /// </summary>
        void Stan();

        /// <summary>
        /// スタン状態解除
        /// </summary>
        UniTask RecoverFromStan();
        /// <summary>
        /// コライダーのセット
        /// </summary>
        public void SetCollider()
        {
            GameObject Check = new GameObject("PlayerCheck");

            // Charaの子オブジェクトに設定
            Check.transform.SetParent(Character.transform);
            // 子オブジェクトの位置をCharaに対してローカルでゼロに設定
            Check.transform.localPosition = Vector3.zero;
            Check.transform.localRotation = Quaternion.identity;
            Check.transform.localScale = Vector3.one;
            // Colliderを追加
            CircleCollider2D collider = Check.AddComponent<CircleCollider2D>();

            // 必要に応じてColliderのサイズやオフセットを調整
            collider.radius = TargetCirsol; // 半径
            collider.offset = Vector2.zero;
            collider.isTrigger = true;

            //キャラクターが近くに来ている時、自動検出
            Check.OnTriggerStay2DAsObservable()
                .Where(_ => _?.GetComponent<PlayerControllModel>() != null)
                .Subscribe(_ => {
                    Vector3 hit = _.transform.position;
                    Vector3 a = Character.transform.position - hit;
                    ChangeDirection(a.x < 0);
                }).AddTo(Check);
        }
        /// <summary>
        /// 向き変更
        /// </summary>
        /// <param name="direction"></param>
        public void ChangeDirection(bool direction)
        {
            if (direction == true)//右方向
            {
                Character.transform.rotation = Quaternion.Euler(0, 0, 0);
                DirectionVector = Vector3.right;
            }
            else//左方向
            {
                Character.transform.rotation = Quaternion.Euler(0, 180, 0);
                DirectionVector = Vector3.left;
            }
        }
        /// <summary>
        /// 攻撃処理
        /// </summary>
        /// <param name="player"></param>
        /// <param name="damage"></param>
        public void Attack(PlayerControllModel player,int damage)
        {
            //player.OnDamage(damage);
        }
        /// <summary>
        /// ブレイクの時
        /// </summary>
        void Break(PlayerControllModel model);

        /// <summary>
        /// 攻撃がプレイヤーにヒットした時の通知.
        /// </summary>
        /// <param name="guardState">プレイヤーのガード状態.</param>
        /// <param name="damage">与えたダメージ量.</param>
        void OnAttackHitPlayer(GuardState guardState, int damage);

        void Damage(int Damage)
        {
            Hp -= Damage;
            if (Hp <= 0)
            {
                Death();
            }
        }
        /// <summary>
        /// 死亡時に呼び出される処理を記述
        /// </summary>
        void Death()
        {
            Debug.Log("死亡");
            UnityEngine.Object.Destroy(Character);
        }
    }
}
