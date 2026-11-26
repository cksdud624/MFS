using System.Collections.Generic;
using Generated.Table;
using InGame.Component.Controller.Boss.Commands;
using InGame.Component.Controller.Boss.Patterns;
using UnityEngine;

namespace InGame.Component.Controller.Boss
{
    /// <summary>
    /// Character 표의 Boss 칸에 적은 이름으로 Boss, BossPattern, BossCommand 표를 읽어 보스의 설계(BossDefinition)를 만든다.
    /// 새 보스는 코드 없이 세 표에 줄을 더해서 만든다. 표가 틀렸으면 틀린 곳을 모두 에러로 남기고 만들지 않는다.
    /// 반쯤 맞는 설계로 싸우면 빠진 패턴이나 커맨드가 조용히 사라져서 어디가 틀렸는지 알기 어렵다
    /// </summary>
    public static class BossFactory
    {
        #region Public
        /// <summary>Boss 표에 있는 이름인지. 빈 이름은 보스가 아니라는 뜻이다</summary>
        public static bool IsRegistered(BossRecord bossRecord, string bossName)
            => !string.IsNullOrEmpty(bossName) && bossRecord.GetRecord(bossName) != null;

        /// <summary>
        /// 보스의 설계를 만든다. bossObject는 이 설계로 싸울 오브젝트이고, 커맨드가 누를 공격이 그 오브젝트의 AttackCommand 표에 있는지 확인하는 데 쓴다.
        /// 표가 틀렸으면 에러를 남기고 false
        /// </summary>
        public static bool TryCreate(TableManager tableManager, ObjectData bossObject, string bossName, out BossDefinition definition)
        {
            definition = null;
            var boss = string.IsNullOrEmpty(bossName) ? null : tableManager.BossRecord.GetRecord(bossName);
            if (boss == null)
            {
                Debug.LogError($"Boss '{bossName}' is not in Boss table.");
                return false;
            }

            bool valid = ValidateBoss(boss);
            var patternDatas = tableManager.BossPatternRecord.GetPatterns(boss.Id);
            if (patternDatas == null)
            {
                Debug.LogError($"Boss '{boss.Id}' has no pattern in BossPattern table.");
                return false;
            }

            var patterns = new List<BossPatternDefinition>();
            //페이즈마다 쓸 패턴이 하나라도 있는지. 없는 페이즈에 들어가면 보스가 쉬기만 한다
            var phaseCovered = new bool[Mathf.Max(1, boss.PhaseCount)];
            foreach (var patternData in patternDatas)
            {
                if (!TryCreatePattern(tableManager, bossObject, boss, patternData, out var pattern))
                {
                    valid = false;
                    continue;
                }

                patterns.Add(pattern);
                for (int phase = 0; phase < phaseCovered.Length; phase++)
                {
                    if (pattern.Phases.Count == 0 || Contains(pattern.Phases, phase))
                        phaseCovered[phase] = true;
                }
            }

            for (int phase = 0; phase < phaseCovered.Length; phase++)
            {
                if (phaseCovered[phase]) continue;
                Debug.LogError($"Boss '{boss.Id}' has no pattern for phase {phase + 1}.");
                valid = false;
            }

            if (!valid) return false;

            definition = new BossDefinition(boss.Id, boss.PhaseCount, boss.IdleDuration, boss.PhaseTransitionDuration, patterns);
            return true;
        }
        #endregion

        #region Private
        private static bool ValidateBoss(BossData boss)
        {
            bool valid = true;
            if (boss.PhaseCount < 1)
                valid = Error($"Boss '{boss.Id}' : PhaseCount must be 1 or more : {boss.PhaseCount}");
            if (boss.IdleDuration < 0f)
                valid = Error($"Boss '{boss.Id}' : IdleDuration must not be negative : {boss.IdleDuration}");
            if (boss.PhaseTransitionDuration < 0f)
                valid = Error($"Boss '{boss.Id}' : PhaseTransitionDuration must not be negative : {boss.PhaseTransitionDuration}");
            return valid;
        }

        //패턴 한 줄과 그 패턴의 커맨드를 읽는다. 한 곳이 틀려도 끝까지 읽어서 틀린 곳을 한 번에 다 보여준다
        private static bool TryCreatePattern(TableManager tableManager, ObjectData bossObject, BossData boss, BossPatternData data, out BossPatternDefinition pattern)
        {
            pattern = null;
            string label = $"BossPattern {data.Id}";
            bool valid = true;

            if (string.IsNullOrEmpty(data.Name))
                valid = Error($"{label} : Name is required. It is shown under the boss hp bar.");
            //빈칸은 0으로 읽혀서 뽑히지 않는 패턴이 된다. 쓰지 않을 패턴은 줄을 지운다
            if (data.Weight <= 0f)
                valid = Error($"{label} : Weight must be greater than 0 : {data.Weight}");
            if (data.Cooldown < 0f || data.MinDistance < 0f || data.MaxDistance < 0f || data.RecoveryDuration < 0f)
                valid = Error($"{label} : Cooldown, MinDistance, MaxDistance and RecoveryDuration must not be negative.");
            if (data.MaxDistance > 0f && data.MinDistance > data.MaxDistance)
                valid = Error($"{label} : MinDistance ({data.MinDistance}) is greater than MaxDistance ({data.MaxDistance}).");

            var phases = new List<int>();
            foreach (int phase in data.Phases)
            {
                //빈칸은 0 하나로 읽힌다. 0은 "사용 안 함"이라 건너뛴다
                if (phase == 0) continue;
                if (phase < 1 || phase > boss.PhaseCount)
                {
                    valid = Error($"{label} : Phases must be between 1 and {boss.PhaseCount} (PhaseCount of boss '{boss.Id}') : {phase}");
                    continue;
                }
                if (!phases.Contains(phase - 1))
                    phases.Add(phase - 1);
            }

            var commandDatas = tableManager.BossCommandRecord.GetCommands(data.Id);
            var commands = new List<BossCommand>();
            if (commandDatas == null)
                valid = Error($"{label} has no command in BossCommand table.");
            else
            {
                foreach (var commandData in commandDatas)
                {
                    //표 참조는 칸을 제대로 읽은 뒤에만 본다. 읽지 못한 값으로 표를 찾으면 엉뚱한 에러가 겹친다
                    if (!BossCommandFactory.TryCreate(commandData, out var command) || !command.Validate(bossObject, tableManager))
                    {
                        valid = false;
                        continue;
                    }
                    commands.Add(command);
                }
            }

            if (!valid) return false;

            pattern = new BossPatternDefinition(data.Id, data.Name, phases, new BossPatternSettings(data), commands);
            return true;
        }

        private static bool Contains(IReadOnlyList<int> values, int value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == value) return true;
            }
            return false;
        }

        private static bool Error(string message)
        {
            Debug.LogError(message);
            return false;
        }
        #endregion
    }
}
