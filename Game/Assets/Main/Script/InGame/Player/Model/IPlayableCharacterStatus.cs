using UnityEngine;
using R3;
namespace InGame.Player
{
    public interface IPlayableCharacterStatus
    {
        int HpMax { get; }
        int Str { get; }
        float Speed { get; }
        int JumpMax { get; }
    }
}