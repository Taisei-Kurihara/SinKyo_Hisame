using UnityEngine;
using UnityEngine.InputSystem;

namespace InGame.Player
{
    public class ShotDefault : AbstructSecondAttack
    {
        
        InputAction inputAction;

        public override void Act()
        {
            if (inputAction.WasPressedThisFrame())
            {

            }
        }
    }
}
