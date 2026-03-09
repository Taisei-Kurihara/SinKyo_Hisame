using TMPro;
using UnityEngine;
using InGame.Player;
using VContainer;

/// <summary>
/// DebugViewなので直接データを持っていく（本当はPresenterからView継承
/// </summary>
public class DebugView : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI BreachPoint;
    [SerializeField]
    private TextMeshProUGUI BreachSecond;
    [SerializeField]
    private TextMeshProUGUI State;

    private PlayerStatusModel playerStatusModel;
    public void Awake()
    {
    }
    // Update is called once per frame
    void Update()
    {
    }
}
