using System.Collections.Generic;
using Generated.Table;
using UnityEngine;
using static Common.GameDefine;

namespace InGame.Context
{
    /// <summary>
    /// 오브젝트에 걸려 있는 상태 효과(버프, 디버프, 타고난 특성). 수치나 특성을 물으면 걸린 효과를 반영해서 답한다.
    /// 효과가 무엇을 하는지는 표가 정한다 (StatusEffect : 지속 시간과 중첩, StatusModifier : 수치 보정과 특성).
    /// 수치는 (기본값 + 더하기의 합) × 곱하기의 곱으로 계산한다. 더하기를 먼저 모아야 효과가 걸린 순서에 따라 결과가 달라지지 않는다.
    /// 같은 효과를 다시 걸면 새로 추가하지 않고 중첩을 올리고 남은 시간을 처음으로 되돌린다
    /// </summary>
    public class StatusEffects
    {
        #region Types
        /// <summary>걸려 있는 효과 하나</summary>
        private class Entry
        {
            public StatusEffectData Data;
            public IReadOnlyList<StatusModifierEntry> Modifiers;
            //겹친 수. 더하기는 이 수만큼 더하고, 곱하기는 이 수만큼 거듭 곱한다
            public int Stack;
            //남은 시간(초). 지속 시간이 0인 효과는 무기한이라 세지 않는다
            public float Remaining;
        }
        #endregion

        #region Fields
        private readonly TableManager _tableManager;
        //로그에 붙이는 주인 이름
        private readonly string _owner;
        private readonly List<Entry> _entries = new();
        #endregion

        #region Init
        public StatusEffects(TableManager tableManager, string owner)
        {
            _tableManager = tableManager;
            _owner = owner;
        }
        #endregion

        #region Public
        /// <summary>
        /// 상태 효과를 건다. 이미 걸려 있으면 중첩을 하나 올리고(MaxStack까지) 남은 시간을 처음으로 되돌린다.
        /// 표에 없는 효과이거나 보정이 하나도 없는 효과면 에러를 남기고 걸지 않는다
        /// </summary>
        public bool Add(long effectId)
        {
            var entry = Find(effectId);
            if (entry == null)
            {
                var data = _tableManager.StatusEffectRecord.GetRecord(effectId);
                if (data == null)
                {
                    Debug.LogError($"StatusEffect {effectId} is not defined.");
                    return false;
                }
                var modifiers = _tableManager.StatusModifierRecord.GetModifiers(effectId);
                if (modifiers == null)
                {
                    Debug.LogError($"StatusEffect {effectId} has no modifier in StatusModifier table. It does nothing.");
                    return false;
                }

                entry = new Entry { Data = data, Modifiers = modifiers };
                _entries.Add(entry);
            }

            //MaxStack을 비워둔 효과는 겹치지 않는 효과로 본다
            entry.Stack = Mathf.Min(entry.Stack + 1, Mathf.Max(1, entry.Data.MaxStack));
            entry.Remaining = entry.Data.Duration;
            Debug.Log($"[상태 효과] {_owner} : 효과 {effectId} 걸림 (중첩 {entry.Stack}, "
                      + (entry.Data.Duration > 0f ? $"{entry.Data.Duration}초)" : "무기한)"));
            return true;
        }

        /// <summary>상태 효과를 푼다. 겹쳐 있어도 한 번에 다 푼다. 걸려 있지 않으면 false</summary>
        public bool Remove(long effectId)
        {
            var entry = Find(effectId);
            if (entry == null) return false;

            _entries.Remove(entry);
            Debug.Log($"[상태 효과] {_owner} : 효과 {effectId} 풀림");
            return true;
        }

        public bool Has(long effectId) => Find(effectId) != null;

        /// <summary>시간을 흘려서 지속 시간이 끝난 효과를 푼다</summary>
        public void Tick(float deltaTime)
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];
                if (entry.Data.Duration <= 0f) continue;

                entry.Remaining -= deltaTime;
                if (entry.Remaining > 0f) continue;

                _entries.RemoveAt(i);
                Debug.Log($"[상태 효과] {_owner} : 효과 {entry.Data.Id} 끝남");
            }
        }

        /// <summary>걸려 있는 효과 중에 그 특성을 켜는 것이 있는지</summary>
        public bool HasTrait(StatusTrait trait)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                var modifiers = _entries[i].Modifiers;
                for (int j = 0; j < modifiers.Count; j++)
                {
                    if (modifiers[j].Kind == StatusModifierKind.Trait && modifiers[j].Trait == trait)
                        return true;
                }
            }
            return false;
        }

        /// <summary>기본값에 걸려 있는 효과의 보정을 반영한다. (기본값 + 더하기의 합) × 곱하기의 곱</summary>
        public float Apply(StatType stat, float baseValue)
        {
            float add = 0f;
            float multiply = 1f;
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                for (int j = 0; j < entry.Modifiers.Count; j++)
                {
                    var modifier = entry.Modifiers[j];
                    if (modifier.Kind != StatusModifierKind.Stat || modifier.Stat != stat) continue;

                    if (modifier.Operation == StatOperation.Add)
                        add += modifier.Value * entry.Stack;
                    else
                        multiply *= Mathf.Pow(modifier.Value, entry.Stack);
                }
            }
            return (baseValue + add) * multiply;
        }
        #endregion

        #region Private
        //한 오브젝트에 걸리는 효과는 몇 개뿐이라 목록을 그대로 훑는다
        private Entry Find(long effectId)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Data.Id == effectId)
                    return _entries[i];
            }
            return null;
        }
        #endregion
    }
}
