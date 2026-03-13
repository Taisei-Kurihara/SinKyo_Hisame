using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using System.Reflection;

namespace Common
{
    /// <summary>
    /// シーンマネージャーとして設計。
    /// Addressableでの非同期読み込みとしてシーンを運用します。
    /// </summary>
    public class SceneManager : SingletonMonoBase<SceneManager>
    {
        /// <summary>
        /// メイン
        /// </summary>
        private AsyncOperationHandle<SceneInstance> _mainScene = default;
        private ISceneInfo _mainSceneInfo = null;

        /// <summary>
        /// サブ（上書きで呼び出すシーン⇒メニュー等）
        /// </summary>
        private AsyncOperationHandle<SceneInstance> _subScene = default;
        private ISceneInfo _subSceneInfo = null;

        // Note: フェード用オブジェクトは StartFadeIn/StartFadeOut で直接管理


        /// <summary>
        /// 単一シーンロード:メイン
        /// </summary>
        /// <param name="mainSceneInfo"></param>
        public async UniTask LoadMainScene(ISceneInfo mainSceneInfo)
        {
            if( _mainSceneInfo != null)
            {
                //終了処理
                await _mainSceneInfo.End();
                _mainSceneInfo.InputStop();
            }
        

            //こちらでメインのロードを行う。
            using (var _cts = new CancellationTokenSource())
            {
                //トークン発行
                var token = _cts.Token;
                try
                {
                    if (_subSceneInfo != null)
                    {
                        await UnloadSubScene();
                    }

                    // フェードイン開始
                    //await StartFadeIn();

                    //ロード
                    _mainScene = Addressables.LoadSceneAsync(mainSceneInfo.SceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
                    await _mainScene.ToUniTask(cancellationToken: token);

                    // フェードアウト開始
                    await StartFadeOut();
                }
                catch (OperationCanceledException)
                {
                    Debug.Log("シーンロードキャンセル");
                }
                finally
                {
                    //メインシーンインスタンス更新
                    _mainSceneInfo = mainSceneInfo;

                    //初期化処理(ゲーム開始等の処理）
                    _mainSceneInfo.InputStart();
                    try
                    {
                        await _mainSceneInfo.Init();
                    }
                    catch (Exception e)
                    {
                        // LoadMainScene自体が.Forget()で呼ばれるため例外が握りつぶされる.
                        // ここで確実にログ出力する.
                        Debug.LogError($"[SceneManager] Init()で例外発生: {_mainSceneInfo.GetType().Name} | {e}");
                    }
                }
            }
        }
        
        
        /// <summary>
        /// シーンロード：サブ用。（メニュー画面等）
        /// </summary>
        /// <param name="sceneInfo"></param>
        /// <returns></returns>
        public async UniTask LoadSubScene(ISceneInfo sceneInfo)
        {
            using (var cts = new CancellationTokenSource())
            {
                var token = cts.Token;
                try
                {
                    _subScene = Addressables.LoadSceneAsync(sceneInfo.SceneName,UnityEngine.SceneManagement.LoadSceneMode.Additive);
                    await _subScene.ToUniTask(cancellationToken: token);

                    //データ
                    _subSceneInfo = sceneInfo;
                    _subSceneInfo.Init().Forget();
                    _subSceneInfo.InputStart();
                }
                catch (OperationCanceledException)
                {
                    Debug.Log("シーンロードキャンセル");
                }
            }
        }
    
        /// <summary>
        /// サブシーンをアンロードする。
        /// </summary>
        public async UniTask UnloadSubScene()
        {
            if (_subSceneInfo!=null)
            {
                //サブシーンのActionMapを終了
                _subSceneInfo?.InputStop();
                //終了処理
                await _subSceneInfo.End();
                //入力だけ可能にする。
                _mainSceneInfo?.InputStart();

                using (var _cts = new CancellationTokenSource()) { 

                    var token = _cts.Token;
                    try
                    {
                        //アンロード
                        _subScene = Addressables.UnloadSceneAsync(_subScene);
                        await _subScene.ToUniTask(cancellationToken: token);
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.Log("シーンアンロードキャンセル");
                    }
                    finally
                    {
                        _subSceneInfo = null;
                        _subScene=default;
                    }
                }
            }
        }

        /// <summary>
        /// 初期に呼び出す処理(メインシーン初期設定をここで行う）
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Init()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            SceneManager _manager = SceneManager.Instance();
            Type targetInterface = typeof(ISceneInfo);

            foreach (var assembly in assemblies)
            {
                try
                {
                    // ISceneInfo を実装したクラスを列挙
                    var types = assembly.GetTypes()
                                        .Where(t => t.IsClass && !t.IsAbstract && targetInterface.IsAssignableFrom(t));

                    foreach (var type in types)
                    {
                        // インスタンスを生成
                        if (Activator.CreateInstance(type) is ISceneInfo sceneInfo)
                        {
                            // シーン名が一致するかチェック
                            if (sceneInfo.SceneName == sceneName)
                            {
                                _manager._mainSceneInfo = sceneInfo;
                                Debug.Log($"初期シーン[{sceneName}]に対応するクラス: {type.FullName}");
                                break;
                            }
                        }
                    }
                }
                catch (ReflectionTypeLoadException e)
                {
                    foreach (var t in e.Types.Where(t => t != null))
                    {
                        if (Activator.CreateInstance(t) is ISceneInfo sceneInfo)
                        {
                            if (sceneInfo.SceneName == sceneName)
                            {
                                _manager._mainSceneInfo = sceneInfo;
                                Debug.Log($"初期シーン[{sceneName}]に対応するクラス: {t.FullName}");
                                break;
                            }
                        }
                    }
                }

                if (_manager._mainSceneInfo != null) break;
            }

            if (_manager._mainSceneInfo == null)
            {
                Debug.LogError($"初期シーン[{sceneName}]に対応する ISceneInfo が見つかりませんでした");
            }
            else
            {
                // LoadMainSceneと同じ順序で初期化（InputStart→Init）.
                _manager._mainSceneInfo.InputStart();
                _manager.InitSceneAsync(_manager._mainSceneInfo).Forget();
            }
        }

        /// <summary>
        /// シーン初期化の非同期処理（エラーハンドリング付き）.
        /// LoadMainSceneのfinally内と同等の処理を行う.
        /// </summary>
        private async UniTaskVoid InitSceneAsync(ISceneInfo sceneInfo)
        {
            try
            {
                await sceneInfo.Init();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SceneManager] 初期シーンInit()で例外発生: {sceneInfo.GetType().Name} | {e}");
            }
        }

        LoadScene_interface loadScene;

        /// <summary>
        /// フェードイン処理を開始
        /// </summary>
        private async UniTask StartFadeIn()
        {
            // フェード用キャンバスを呼び出し
            var fadeHandle = Addressables.InstantiateAsync("Load");
            await fadeHandle.Task;
            var fadeObj = fadeHandle.Result;
            //fadeObj.transform.SetParent(transform);

            // UIManagerのCanvas親子付け関数で親子付け
            await UIManager.Instance().AttachToCanvas(fadeObj);

            Debug.Log("StartFadeIn");

            loadScene = fadeObj.GetComponent<LoadScene_interface>();
            if (loadScene != null)
            {
                Debug.Log("LoadScene_interfaceがアタッチされています");
                await loadScene.StartFadeIn();
            }
            else
            {
                Debug.LogWarning("LoadScene_interfaceがアタッチされていません");
            }
        }

        /// <summary>
        /// フェードアウト処理を開始
        /// </summary>
        private async UniTask StartFadeOut()
        {
            if (loadScene != null)
            {
                UnityEngine.Debug.Log("StartFadeOut");
                await loadScene.StartFadeOut();
            }
        }
    }
}
