using Cysharp.Threading.Tasks;
using Generated.Table;
using InGame.Context;
using ObjectType = Common.GameDefine.ObjectType;
using ObjectState = Common.GameDefine.ObjectState;
using InGame.Component;

namespace InGame.Object
{
    public class CharacterBase : ObjectBase
    {
        public new async UniTask Init(InGameContext inGameContext, ObjectData objectData, bool isPlayer = false)
        {
            InGameContext = inGameContext;
            IsPlayer = isPlayer;
            InputContext = new ();
            ObjectContext = new(ObjectType.Character, objectData);
            AnimationPlayer = gameObject.AddComponent<AnimationPlayer>();
            await AnimationPlayer.Init(ObjectContext, objectData.Id);
            CameraController = gameObject.AddComponent<CameraController>();
            await CameraController.Init();
            PhysicsController = gameObject.AddComponent<PhysicsController>();
            await PhysicsController.Init(ObjectContext);
            ObjectStateController = gameObject.AddComponent<ObjectStateController>();
            await ObjectStateController.Init(InputContext, ObjectContext);
        }
    }
}
