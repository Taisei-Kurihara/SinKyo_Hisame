using System;
using System.Linq;
using UnityEngine;

namespace Common{
    /// <summary>
    /// ModelやViewに何が実装されているのかを判別する為のもの。
    /// </summary>
    public interface IPresenter
    {
        object Model { get;}
        object View { get;}

        /// <summary>
        /// モデルに何が実装されているかを単品で探索する。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        bool IModelSearch<T>() where T: class
        {
            return Model.GetType().GetInterfaces().Contains(typeof(T));
        }

        /// <summary>
        /// 返り値でInterfaceを返す
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T GetIModelSearch<T>() where T : class
        {
            return Model as T;
        }


        bool IViewSearch<T>() where T : class
        {
            return View.GetType().GetInterfaces().Contains(typeof(T));
        }
    }
}