using Cysharp.Threading.Tasks;
using InGame.Context;
using ObjectType = Common.GameDefine.ObjectType;
using ObjectState = Common.GameDefine.ObjectState;

namespace InGame.Object
{
    public class CharacterBase : ObjectBase
    {
        public override ObjectType ObjectType => ObjectType.Character;

        public new async UniTask Init(InGameContext inGameContext)
        {
            InGameContext = inGameContext;
            await UniTask.CompletedTask;
        }
    }
}
