using Cysharp.Threading.Tasks;
using Generated.Table;
using InGame.Component;
using InGame.Context;
using static Common.GameDefine;

namespace InGame.Object
{
    /// <summary>
    /// 캐릭터 전용 확장 지점.
    /// 오브젝트 타입은 ObjectData.IsCharacter로 결정되므로 여기서 따로 지정하지 않는다.
    /// </summary>
    public class CharacterBase : ObjectBase
    {
        #region Fields
        protected HitBoxController HitBoxController;
        #endregion

        #region Init
        public override async UniTask Init(InGameContext inGameContext, ObjectData objectData, Team team, bool isPlayer = false)
        {
            //캐릭터는 서로 충돌하지 않아야 하므로 콜라이더가 붙기 전에 레이어부터 맞춰둔다
            if (TryGetLayer(LayerIndependent, out int independentLayer))
                gameObject.layer = independentLayer;

            await base.Init(inGameContext, objectData, team, isPlayer);

            //공격 판정은 캐릭터만 사용한다
            HitBoxController = gameObject.AddComponent<HitBoxController>();
            HitBoxController.Init(InGameContext, ObjectContext);
        }
        #endregion
    }
}
