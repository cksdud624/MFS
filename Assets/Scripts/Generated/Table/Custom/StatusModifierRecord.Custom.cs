using System;
using System.Collections.Generic;
using UnityEngine;
using static Common.GameDefine;

namespace Generated.Table
{
    /// <summary>
    /// 상태 효과의 보정 한 줄을 실행할 때 쓰는 모양으로 읽어둔 것.
    /// 표에는 이름(문자열)으로 적혀 있어서 불러올 때 한 번만 enum으로 바꿔둔다. 맞을 때마다 문자열을 비교하지 않게 한다
    /// </summary>
    public readonly struct StatusModifierEntry
    {
        public StatusModifierKind Kind { get; }
        //Kind가 Stat일 때 보정하는 수치와 방법, 값
        public StatType Stat { get; }
        public StatOperation Operation { get; }
        public float Value { get; }
        //Kind가 Trait일 때 켜는 특성
        public StatusTrait Trait { get; }

        public StatusModifierEntry(StatType stat, StatOperation operation, float value)
        {
            Kind = StatusModifierKind.Stat;
            Stat = stat;
            Operation = operation;
            Value = value;
            Trait = default;
        }

        public StatusModifierEntry(StatusTrait trait)
        {
            Kind = StatusModifierKind.Trait;
            Stat = default;
            Operation = default;
            Value = 0f;
            Trait = trait;
        }
    }

    /// <summary>
    /// 상태 효과 번호로 그 효과의 보정을 꺼내기 위한 색인.
    /// 효과 하나가 보정을 여러 개 가질 수 있어서(공격력 +10이면서 방어력 -5) 효과 번호로 묶어둔다.
    /// 칸에 적힌 이름이 틀린 줄은 에러를 남기고 뺀다
    /// </summary>
    public partial class StatusModifierRecord
    {
        #region Fields
        private readonly Dictionary<long, List<StatusModifierEntry>> _modifiersByEffect = new();
        #endregion

        #region Init
        partial void InitCustomRecord()
        {
            foreach (var data in datas)
            {
                if (!TryParse(data, out var entry)) continue;

                if (!_modifiersByEffect.TryGetValue(data.StatusEffect, out var modifiers))
                {
                    modifiers = new List<StatusModifierEntry>();
                    _modifiersByEffect[data.StatusEffect] = modifiers;
                }
                modifiers.Add(entry);
            }
        }
        #endregion

        #region Public
        /// <summary>상태 효과의 보정 목록. 보정이 하나도 없는 효과면 null</summary>
        public IReadOnlyList<StatusModifierEntry> GetModifiers(long statusEffectId)
        {
            _modifiersByEffect.TryGetValue(statusEffectId, out var modifiers);
            return modifiers;
        }
        #endregion

        #region Private
        private static bool TryParse(StatusModifierData data, out StatusModifierEntry entry)
        {
            entry = default;
            if (!TryParseName(data.Kind, out StatusModifierKind kind))
                return LogError(data, $"Kind must be Stat or Trait : '{data.Kind}'");

            if (kind == StatusModifierKind.Trait)
            {
                if (!TryParseName(data.Target, out StatusTrait trait))
                    return LogError(data, $"Target is not a StatusTrait : '{data.Target}'");
                entry = new StatusModifierEntry(trait);
                return true;
            }

            if (!TryParseName(data.Target, out StatType stat))
                return LogError(data, $"Target is not a StatType : '{data.Target}'");
            if (!TryParseName(data.Operation, out StatOperation operation))
                return LogError(data, $"Operation must be Add or Multiply : '{data.Operation}'");
            entry = new StatusModifierEntry(stat, operation, data.Value);
            return true;
        }

        //숫자도 Enum.TryParse를 통과하므로 정의된 이름인지 따로 확인한다
        private static bool TryParseName<T>(string raw, out T value) where T : struct, Enum
            => Enum.TryParse(raw, out value) && Enum.IsDefined(typeof(T), value);

        //에러를 남기고 false를 돌려준다. 실패 지점에서 바로 return할 수 있게 bool을 돌려준다
        private static bool LogError(StatusModifierData data, string message)
        {
            Debug.LogError($"StatusModifier {data.Id} : {message}");
            return false;
        }
        #endregion
    }
}
