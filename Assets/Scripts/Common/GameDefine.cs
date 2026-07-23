using UnityEngine;

namespace Common
{
    public static class GameDefine
    {
        public enum SceneType
        {
            BootStrap = 0,
            Main = 1
        }

        public enum ObjectType
        {
            Object,
            Character
        }

        public enum ObjectState
        {
            Raw,
            Loading,
            Ready,
            Playing,
            Sleep,
            Destroyed,
            Error
        }

        public enum Direction
        {
            Right,
            Left,
        }

        public enum FSMState
        {
            Ground,
            Air,
            Action,
            Damage,
            Event,
        }

        #region Animation
        public enum AnimationType
        {
            Idle,
            Move,
            Jump,
            DoubleJump,
            Dash,
            BackDash,
        }
        #endregion
    }
}