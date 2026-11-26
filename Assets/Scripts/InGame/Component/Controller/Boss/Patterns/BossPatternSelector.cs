using System.Collections.Generic;
using UnityEngine;

namespace InGame.Component.Controller.Boss.Patterns
{
    /// <summary>
    /// 페이즈마다 등록된 패턴 중 다음에 할 패턴을 고른다.
    /// 강제로 정한 패턴이 있으면 조건 없이 그것을 쓴다. 없으면 CanStart(가중치, 쿨타임, 거리)를 통과한 패턴 중
    /// 우선순위가 가장 높은 것끼리 가중치로 뽑고, 그 후보가 둘 이상이면 바로 앞에 한 패턴은 뺀다
    /// </summary>
    public class BossPatternSelector
    {
        #region Fields
        private readonly Dictionary<int, List<BossPattern>> _patternsByPhase = new();
        //고르는 동안만 쓰는 후보 목록. 고를 때마다 만들지 않고 비워서 다시 쓴다
        private readonly List<BossPattern> _candidates = new();

        //바로 앞에 시작한 패턴. 같은 패턴이 이어서 나오지 않게 한다
        private BossPattern _lastPattern;
        //다음에 반드시 할 패턴. 시작하면 지워진다
        private BossPattern _forcedPattern;
        #endregion

        #region Public
        /// <summary>phase(0부터)에서 쓸 패턴을 등록한다. 같은 패턴 객체를 여러 페이즈에 등록하면 쿨타임을 같이 쓴다</summary>
        public void AddPattern(int phase, BossPattern pattern)
        {
            if (!_patternsByPhase.TryGetValue(phase, out var patterns))
            {
                patterns = new List<BossPattern>();
                _patternsByPhase[phase] = patterns;
            }
            patterns.Add(pattern);
        }

        /// <summary>
        /// 다음에 시작할 패턴을 정한다. 가중치, 우선순위, 쿨타임, 거리, 페이즈를 모두 무시한다. 그 패턴이 시작되면(MarkStarted) 지워진다.
        /// 쉬는 시간은 그대로 지난 뒤에 시작하므로, 바로 이으려면 앞 패턴의 RecoveryDuration을 0으로 둔다
        /// </summary>
        public void ForceNextPattern(BossPattern pattern) => _forcedPattern = pattern;

        public void ClearForcedPattern() => _forcedPattern = null;

        /// <summary>
        /// context.Phase에서 지금 시작할 수 있는 패턴을 하나 고른다. 고를 것이 없으면 false.
        /// 고르기만 하고 쿨타임과 강제 지정은 바꾸지 않으므로, 실제로 시작할 때 MarkStarted를 부른다
        /// </summary>
        public bool TrySelect(in BossPatternContext context, out BossPattern pattern)
        {
            if (_forcedPattern != null)
            {
                pattern = _forcedPattern;
                return true;
            }

            pattern = null;
            if (!_patternsByPhase.TryGetValue(context.Phase, out var patterns)) return false;

            _candidates.Clear();
            int topPriority = int.MinValue;
            for (int i = 0; i < patterns.Count; i++)
            {
                var candidate = patterns[i];
                if (!candidate.CanStart(in context)) continue;

                //더 높은 우선순위가 나오면 그보다 낮은 후보는 버린다
                if (candidate.Priority > topPriority)
                {
                    topPriority = candidate.Priority;
                    _candidates.Clear();
                }
                if (candidate.Priority == topPriority)
                    _candidates.Add(candidate);
            }

            //같은 패턴이 이어서 나오지 않게 하되, 후보가 하나뿐이면 그대로 쓴다
            if (_candidates.Count > 1)
                _candidates.Remove(_lastPattern);

            pattern = PickByWeight(_candidates);
            _candidates.Clear();
            return pattern != null;
        }

        /// <summary>패턴을 시작했다고 기록한다. 바로 앞 패턴과 쿨타임을 갱신하고, 강제로 정한 패턴이었으면 지운다</summary>
        public void MarkStarted(BossPattern pattern, float time)
        {
            if (_forcedPattern == pattern)
                _forcedPattern = null;

            _lastPattern = pattern;
            pattern.MarkStarted(time);
        }
        #endregion

        #region Private
        private static BossPattern PickByWeight(List<BossPattern> candidates)
        {
            if (candidates.Count == 0) return null;

            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++)
                totalWeight += candidates[i].Weight;

            float pick = Random.Range(0f, totalWeight);
            for (int i = 0; i < candidates.Count; i++)
            {
                pick -= candidates[i].Weight;
                if (pick <= 0f)
                    return candidates[i];
            }

            //부동소수 오차로 끝까지 남으면 마지막 후보를 쓴다
            return candidates[^1];
        }
        #endregion
    }
}
