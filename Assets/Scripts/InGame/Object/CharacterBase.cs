using Cysharp.Threading.Tasks;
using Generated.Table;
using InGame.Component;
using InGame.Context;

namespace InGame.Object
{
    /// <summary>
    /// 캐릭터 전용 확장 지점.
    /// 오브젝트 타입은 ObjectData.IsCharacter로 결정되므로 여기서 따로 지정하지 않는다.
    /// </summary>
    public class CharacterBase : ObjectBase
    {
        protected HitBoxController HitBoxController;

        public override async UniTask Init(InGameContext inGameContext, ObjectData objectData, bool isPlayer = false)
        {
            await base.Init(inGameContext, objectData, isPlayer);

            //공격 판정은 캐릭터만 사용한다
            HitBoxController = gameObject.AddComponent<HitBoxController>();
            await HitBoxController.Init(ObjectContext);
        }
    }
}
