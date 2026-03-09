using UnityEngine;
using R3;
using Novel.Model;
using Novel.View;
namespace Novel.Presenter
{
    public class Novel_Presenter : MonoBehaviour
    {
        private Novel_Model model = new Novel_Model();
        private Novel_View view;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            view = gameObject.GetComponent<Novel_View>();
            model.Text.Subscribe(async _ =>
            {
                await view.NovelTextLoad_Into(_, model.TextLoadTime);
            });


        }
    }
}
