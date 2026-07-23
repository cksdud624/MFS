using InGame.Object;

namespace InGame.Context
{
    public class InGameContext
    {
        public ObjectBase Player { get; private set; }
        public void SetPlayer(ObjectBase player)
        {
            Player = player;
        }
    }
}
