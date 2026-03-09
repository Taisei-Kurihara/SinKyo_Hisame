using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using InGame;
using InGame.Model;
using R3;
using UnityEngine;
using InGame.Player;


namespace InGame
{
    public class EnemyModelTemporary : MonoBehaviour, IEnemyModel
    {
        
        public GameObject Character => gameObject;

        public int Hp { get; set; } = 30;
        public int Str { get; private set; } = 10;   // 攻撃力
        public float Speed { get; private set; } = 2f; // 移動速度
        public float TargetCirsol { get; private set; } = 2f; // 攻撃範囲半径
        public Vector3 DirectionVector { get; set; }
        public UniTask action { get; set; }
        public EnemStatus nowstatus { get; set; } = EnemStatus.Normal;

        public float DelayTime { get; private set; } = 3f;
        public float StanDuration { get; private set; } = 2f; // スタン持続時間

        // 遠距離攻撃パラメータ
        public float RangedAttackDistance { get; private set; } = 8f; // 遠距離攻撃の射程
        public float ProjectileSpeed { get; private set; } = 5f; // 弾の速度
        public float HomingStrength { get; private set; } = 2f; // ホーミング強度
        public float RangedCooldown { get; private set; } = 2f; // 遠距離攻撃のクールダウン

        private float lastRangedAttackTime = 0f;

        private bool isAlive = true;
        private CancellationTokenSource aiCancellationTokenSource;

        // プレイヤーの参照
        private Transform player;

        void OnDestroy()
        {
            Debug.Log("[EnemyAI] OnDestroy: 破棄開始");
            isAlive = false;
            aiCancellationTokenSource?.Cancel();
            aiCancellationTokenSource?.Dispose();
            Debug.Log("[EnemyAI] OnDestroy: 破棄完了");
        }

        void IEnemyModel.Break(PlayerControllModel model) { }

        public void Init()
        {
            // プレイヤー取得.
            var playerScope = UnityEngine.Object.FindFirstObjectByType<PlayerScope>();
            var playerObj = playerScope?.gameObject;
            if (playerObj != null)
            {
                player = playerObj.transform;
                Debug.Log($"[EnemyAI] Init: プレイヤー発見 - {playerObj.name}");
            }
            else
            {
                Debug.LogWarning("[EnemyAI] Init: プレイヤーが見つかりません");
            }

            // AIを開始.
            Debug.Log("[EnemyAI] Init: AI開始");
            aiCancellationTokenSource = new CancellationTokenSource();
            AI().Forget(e =>
            {
                // キャンセル例外は正常動作のため無視.
                if (e is OperationCanceledException) return;
                Debug.LogError(e);
            });
        }

        /// <summary>
        /// AI処理実装
        /// </summary>
        /// <returns></returns>
        public async UniTask AI()
        {

            Debug.Log("[EnemyAI] AI: ループ開始");
            while (isAlive && !aiCancellationTokenSource.Token.IsCancellationRequested)
            {
                GetComponent<Animator>()?.SetInteger("Walk", 0);
                // スタン状態のときは待機.
                if (nowstatus == EnemStatus.Stan)
                {
                    Debug.Log("[EnemyAI] AI: スタン状態 - 待機中");
                    await UniTask.Yield(cancellationToken: aiCancellationTokenSource.Token);
                    continue;
                }
                try
                {
                    var playerScope = UnityEngine.Object.FindFirstObjectByType<PlayerScope>();
                    if (playerScope != null)
                    {
                        player = playerScope.transform;
                    }
                }
                catch
                {

                }
                Vector3 playerpos;
                if (player != null)
                {
                    playerpos = player.position;
                }
                else
                {
                    // プレイヤーがいない場合はマウスポインタの位置を取得.
                    Vector3 mouseScreenPos = Input.mousePosition;
                    mouseScreenPos.z = 10f; // カメラからの距離.
                    playerpos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
                    Debug.Log($"[EnemyAI] AI: マウス追跡モード (位置={playerpos})");
                }

                // プレイヤーとの距離.
                float distance = Vector3.Distance(transform.position, playerpos);

                // 状態ログ出力.
                //Debug.Log($"[EnemyAI] AI: 状態={nowstatus}, Player={player != null}, 距離={distance:F2}, 近距離範囲={TargetCirsol}, 遠距離範囲={RangedAttackDistance}");

                string selectedAction = "";

                // 条件分岐（優先順位：近距離攻撃 > 遠距離攻撃 > 移動）.
                if (distance <= TargetCirsol)
                {
                    // 近距離攻撃範囲内なら近距離攻撃.
                    selectedAction = "近距離攻撃";
                    action = Attack();
                }
                //else if (distance <= RangedAttackDistance && CanPerformRangedAttack())
                //{
                //    // 遠距離攻撃範囲内かつクールダウン完了なら遠距離攻撃.
                //    // 距離に応じてホーミング有無を決定（遠いほどホーミング使用）.
                //    bool useHoming = distance > RangedAttackDistance * 0.6f;
                //    selectedAction = $"遠距離攻撃(ホーミング={useHoming})";
                //    action = RangedAttack(useHoming);
                //}
                else
                {
                    // Y軸を含めずに移動方向を計算.
                    GetComponent<Animator>()?.SetInteger("Walk", 1);
                    Vector3 diff = playerpos - transform.position;
                    Vector3 direction = new Vector3(diff.x, 0f, diff.z).normalized;
                    selectedAction = $"移動(方向={direction})";
                    action = Move(direction);
                }

                //Debug.Log($"[EnemyAI] AI: アクション選択 -> {selectedAction}");

                // アクションを実行（キャンセル可能）.
                try
                {
                    await action.AttachExternalCancellation(aiCancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    Debug.Log("[EnemyAI] AI: キャンセルされました");
                    break;
                }
                catch (System.Exception ex) when (!isAlive || this == null)
                {
                    // オブジェクト破棄後の例外は無視.
                    Debug.Log($"[EnemyAI] AI: 破棄後の例外を無視 ({ex.GetType().Name})");
                    break;
                }
            }
            Debug.Log($"[EnemyAI] AI: ループ終了 (isAlive={isAlive})");
        }

        // 攻撃処理.
        private async UniTask Attack()
        {
            if (!isAlive || this == null) return;
            Debug.Log("[EnemyAI] Attack: 攻撃準備");

            // 攻撃前に待機.
            await UniTask.Delay((int)(DelayTime * 1000f), cancellationToken: aiCancellationTokenSource.Token);

            if (!isAlive || this == null) return;
            Debug.Log("[EnemyAI] Attack: 攻撃実行");

            // 攻撃アニメーション再生.
            GetComponent<Animator>()?.SetTrigger("Attack");

            // アニメーション後の処理.
            await UniTask.Delay(300, cancellationToken: aiCancellationTokenSource.Token);

            if (!isAlive || this == null) return;
            Debug.Log("[EnemyAI] Attack: アニメーション終了");

            // 範囲内のプレイヤーを検索.
            Vector3 pos = transform.position + (Vector3.left * Mathf.Clamp(transform.localScale.x, -1, 1));
            
            Collider2D[] hits = Physics2D.OverlapCircleAll(pos, TargetCirsol);

            Debug.Log("[EnemyAI] Attack: pos hits ok");


            // デバッグ表示
            for (float f = 0; f < Mathf.PI * 2; f += (Mathf.PI * 2) / 15)
            {
                Debug.DrawLine(pos, pos + new Vector3(Mathf.Cos(f), Mathf.Sin(f)) * TargetCirsol, Color.red, 1f);
            }

            Debug.Log("[EnemyAI] Attack: デバッグ表示 ok");

            Debug.Log($"[EnemyAI] Attack: ヒット判定開始 hits.Length={hits.Length}");
            foreach (Collider2D hit in hits)
            {
                //Debug.Log($"[EnemyAI] Attack: ヒット対象確認 name={hit.name}, tag={hit.tag}, layer={LayerMask.LayerToName(hit.gameObject.layer)}");
                var hitPlayerScope = hit.gameObject.GetComponent<PlayerScope>();
                if (hitPlayerScope != null)
                {
                    // プレイヤーにダメージを与える.
                    Debug.Log($"[EnemyAI] Attack: ★プレイヤーヒット確認★ PlayerScope検出 name={hit.name}");

                    // ガード状態を事前に確認.
                    bool isGuarding = hitPlayerScope.playerControllModel != null;
                    Debug.Log($"[EnemyAI] Attack: プレイヤー状態確認 playerControllModel存在={isGuarding}");

                    GuardState guardState = hitPlayerScope.OnReceiveAttack(Str);
                    Debug.Log($"[EnemyAI] Attack: ダメージ処理結果 ガード状態={guardState}, 与ダメージ={Str}");

                    // エネミーに攻撃ヒットを通知.
                    int actualDamage = guardState == GuardState.Parry ? 0 : (guardState == GuardState.Guard ? Str / 2 : Str);
                    OnAttackHitPlayer(guardState, actualDamage);

                    // ガード状態による分岐ログ.
                    switch (guardState)
                    {
                        case GuardState.Parry:
                            Debug.Log("[EnemyAI] Attack: 【パリィ成功】プレイヤーがパリィ！ダメージ無効、敵スタン");
                            Stan();
                            break;
                        case GuardState.Guard:
                            Debug.Log("[EnemyAI] Attack: 【ガード成功】ダメージ半減");
                            break;
                        case GuardState.None:
                            Debug.Log("[EnemyAI] Attack: 【ノーガード】フルダメージ");
                            break;
                        default:
                            Debug.Log($"[EnemyAI] Attack: 【その他】ガード状態={guardState}");
                            break;
                    }
                }
                else
                {
                    // プレイヤー以外のオブジェクトにヒット.
                    //Debug.Log($"[EnemyAI] Attack: ヒットしたがプレイヤーではない name={hit.name}, tag={hit.tag}");
                }
            }

            Debug.Log("[EnemyAI] Attack: 完了");
        }

        // 移動処理.
        private async UniTask Move(Vector3 direction)
        {
            if (!isAlive || this == null) return;
            Debug.Log($"[EnemyAI] Move: 移動中 (方向={direction})");

            // 移動.
            transform.position += direction * Speed * Time.deltaTime;

            // 向きを反転（左右だけ）.
            if (direction.x != 0)
            {
                Vector3 scale = transform.localScale;
                scale.x = Mathf.Sign(direction.x) * -Mathf.Abs(scale.x);
                transform.localScale = scale;
            }

            // 1フレーム待機.
            await UniTask.Yield(cancellationToken: aiCancellationTokenSource.Token);
        }

        // スタン処理.
        public void Stan()
        {
            Debug.Log("[EnemyAI] Stan: スタン開始");

            // 現在実行中のactionをキャンセル.
            Debug.Log("[EnemyAI] Stan: 現在のAIをキャンセル");
            aiCancellationTokenSource?.Cancel();
            aiCancellationTokenSource?.Dispose();
            aiCancellationTokenSource = new CancellationTokenSource();

            // スタン状態に設定.
            nowstatus = EnemStatus.Stan;
            Debug.Log($"[EnemyAI] Stan: 状態変更 -> {nowstatus}");

            // スタンアニメーション再生.
            GetComponent<Animator>()?.SetTrigger("Stan");

            // スタンからの回復処理を開始.
            Debug.Log($"[EnemyAI] Stan: 回復処理開始 (待機時間={StanDuration}秒)");
            RecoverFromStan().Forget();

            // AIを再開始.
            Debug.Log("[EnemyAI] Stan: AI再開始");
            AI().Forget(e =>
            {
                // キャンセル例外は正常動作のため無視.
                if (e is OperationCanceledException) return;
                Debug.LogError(e);
            });
        }

        // スタン状態解除.
        public async UniTask RecoverFromStan()
        {
            Debug.Log($"[EnemyAI] RecoverFromStan: 回復待機中 ({StanDuration}秒)");
            // 指定時間待機.
            await UniTask.Delay((int)(StanDuration * 1000f));

            // 通常状態に戻す.
            nowstatus = EnemStatus.Normal;
            Debug.Log($"[EnemyAI] RecoverFromStan: スタン解除 -> 状態={nowstatus}");
        }

        // ダメージリアクション.
        public void DamageReaction()
        {
            Debug.Log("[EnemyAI] DamageReaction: ダメージを受けた");
            GetComponent<Animator>()?.SetTrigger("Damage");
        }

        // 攻撃がプレイヤーにヒットした時の通知.
        public void OnAttackHitPlayer(GuardState guardState, int damage)
        {
            Debug.Log($"[EnemyAI] OnAttackHitPlayer: 攻撃ヒット通知 - ガード状態={guardState}, ダメージ={damage}");
            // ヒットエフェクトやサウンド再生などの処理を追加可能.
        }

        // ヒットバック処理.
        public void HitBack()
        {
            Debug.Log($"[EnemyAI] HitBack: ノックバック開始 (状態: {nowstatus} -> HitBack)");
            nowstatus = EnemStatus.HitBack;
            // ノックバック処理を実装.
        }

        // 遠距離攻撃処理（ホーミング機能付き）.
        private async UniTask RangedAttack(bool homing)
        {
            if (!isAlive || this == null) return;
            Debug.Log($"[EnemyAI] RangedAttack: 遠距離攻撃準備 (ホーミング={homing})");

            // 攻撃前待機.
            await UniTask.Delay((int)(DelayTime * 500f), cancellationToken: aiCancellationTokenSource.Token);

            if (!isAlive || this == null) return;
            Debug.Log("[EnemyAI] RangedAttack: 発射");

            // アニメーション再生.
            GetComponent<Animator>()?.SetTrigger("RangedAttack");

            if (player != null)
            {
                // 弾の生成位置.
                Vector3 spawnPosition = transform.position + Vector3.up * 0.5f;

                // 初期方向.
                Vector3 direction = (player.position - spawnPosition).normalized;

                // ホーミング弾の場合の処理.
                if (homing)
                {
                    // ホーミング弾のシミュレーション（実際のプレハブがある場合に実装）.
                    Debug.Log($"[EnemyAI] RangedAttack: ホーミング弾発射 (位置={spawnPosition}, 方向={direction})");

                    // ここで実際のホーミング弾生成処理.
                    // GameObject projectile = Instantiate(homingProjectilePrefab, spawnPosition, Quaternion.identity);
                    // var homingComponent = projectile.GetComponent<HomingProjectile>();
                    // homingComponent?.SetTarget(player, HomingStrength);
                }
                else
                {
                    // 通常弾の処理.
                    Debug.Log($"[EnemyAI] RangedAttack: 通常弾発射 (位置={spawnPosition}, 方向={direction})");

                    // ここで実際の弾生成処理.
                    // GameObject projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
                    // projectile.GetComponent<Rigidbody2D>()?.AddForce(direction * ProjectileSpeed, ForceMode2D.Impulse);
                }

                // デバッグ用の弾道表示.
                if (homing)
                {
                    // ホーミング軌道の曲線表示.
                    Debug.DrawLine(spawnPosition, spawnPosition + direction * 2f, Color.magenta, 2f);
                    Debug.DrawLine(spawnPosition + direction * 2f, player.position, Color.magenta, 2f);
                }
                else
                {
                    // 直線弾道表示.
                    Debug.DrawLine(spawnPosition, spawnPosition + direction * RangedAttackDistance, Color.cyan, 2f);
                }
            }

            // クールダウン記録.
            lastRangedAttackTime = Time.time;

            // アニメーション後処理.
            await UniTask.Delay(500, cancellationToken: aiCancellationTokenSource.Token);

            if (!isAlive || this == null) return;
            Debug.Log("[EnemyAI] RangedAttack: 完了");
        }

        // 遠距離攻撃が可能かチェック
        private bool CanPerformRangedAttack()
        {
            return Time.time - lastRangedAttackTime >= RangedCooldown;
        }

        // Sceneビューで攻撃範囲を可視化（デバッグ用）
        private void OnDrawGizmosSelected()
        {
            // 近距離攻撃範囲
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, TargetCirsol);

            // 遠距離攻撃範囲
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, RangedAttackDistance);
        }
    }
}
