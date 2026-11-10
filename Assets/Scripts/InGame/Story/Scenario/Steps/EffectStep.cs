using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using EffectType = Common.GameDefine.EffectType;

namespace InGame.Story.Scenario.Steps
{
    /// <summary>
    /// Effect : 대상에게 이펙트를 띄운다. 판정 없이 연출로만 나가고, 재생이 끝나기를 기다리지 않고 바로 다음 줄로 넘어간다.
    /// 대상이 미리 받아둔 이펙트만 쓸 수 있다 (Effect/(ObjectId)/ 아래 프리팹). 재생 길이는 프리팹 수명을 따른다.
    /// Actor = 대상, Param1 = 프리팹 이름 (Attack1, Dash), Param2 = 오프셋 (오른쪽을 보는 기준, 비우면 0,0)
    /// </summary>
    public class EffectStep : StoryStep
    {
        #region Fields
        //테이블에 적힌 이름. 대상이 이 이펙트를 가졌는지 검사할 때 쓴다
        private string _effectName;
        private EffectType _effectType;
        private int _variant;
        private Vector2 _offset;
        #endregion

        #region Public
        public override void Validate(StoryValidator validator) => validator.CheckEffect(Actor, _effectName);

        public override UniTask Run(StoryContext context, CancellationToken cancellationToken)
        {
            if (context.TryGetActor(this, out var actor))
                actor.PlayEffect(_effectType, _variant, _offset, 0f);
            return UniTask.CompletedTask;
        }
        #endregion

        #region Protected
        protected override bool Parse(StoryParamReader reader, out StoryActor actor)
        {
            bool parsed = reader.ReadActor(true, out actor)
                          & reader.ReadString(1, "Effect", out _effectName)
                          & reader.ReadOptionalVector2(2, "Offset", Vector2.zero, out _offset);
            if (!parsed) return false;

            if (TryParseEffect(_effectName, out _effectType, out _variant)) return true;
            LogError($"Param1 (Effect) is not an effect name : '{_effectName}'");
            return false;
        }
        #endregion

        #region Private
        /// <summary>프리팹 이름 그대로 적은 값을 종류와 번호로 나눈다 (Attack3 → Attack, 3). 번호가 없으면 0(번호 없는 기본 프리팹)이다</summary>
        private static bool TryParseEffect(string effect, out EffectType effectType, out int variant)
        {
            effectType = default;
            variant = 0;
            int split = effect.Length;
            while (split > 0 && char.IsDigit(effect[split - 1]))
                split--;
            //번호가 int 범위를 넘으면 예외 대신 형식 에러로 남긴다. 예외로 끝나면 같은 시나리오의 다른 틀린 칸이 보이지 않는다
            if (split < effect.Length
                && !int.TryParse(effect.Substring(split), NumberStyles.None, CultureInfo.InvariantCulture, out variant))
                return false;

            //숫자만 적은 값도 Enum.TryParse를 통과하므로 정의된 이름인지 따로 확인한다
            return Enum.TryParse(effect.Substring(0, split), out effectType) && Enum.IsDefined(typeof(EffectType), effectType);
        }
        #endregion
    }
}
