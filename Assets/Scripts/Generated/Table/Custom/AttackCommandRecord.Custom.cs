using System.Collections.Generic;

namespace Generated.Table
{
    /// <summary>
    /// 커맨드 문자열로 공격을 찾기 위한 색인.
    /// 커맨드는 누른 공격 버튼 번호를 순서대로 이어붙인 문자열이라("1" → "11" → "111")
    /// 오브젝트와 커맨드 조합으로 바로 꺼낼 수 있어야 한다.
    /// 한 번의 커맨드에 마법과 기계가 같이 나가므로 같은 조합에 타입별로 한 줄씩 붙는다.
    /// </summary>
    public partial class AttackCommandRecord
    {
        private readonly Dictionary<(long ObjectId, string Command), List<AttackCommandData>> _datasByCommand = new();
        //오브젝트가 쓰는 공격 애니메이션 / 이펙트 번호. 클립과 프리팹을 미리 로드할 때 쓴다
        private readonly Dictionary<long, List<int>> _animationsByObjectId = new();
        private readonly Dictionary<long, List<int>> _effectsByObjectId = new();

        partial void InitCustomRecord()
        {
            foreach (var data in datas)
            {
                var key = (data.ObjectId, data.Command);
                if (!_datasByCommand.TryGetValue(key, out var commands))
                {
                    commands = new List<AttackCommandData>();
                    _datasByCommand[key] = commands;
                }
                commands.Add(data);

                AddVariant(_animationsByObjectId, data.ObjectId, data.Animation);
                AddVariant(_effectsByObjectId, data.ObjectId, data.Effect);
            }
        }

        //번호가 0이면 쓰지 않겠다는 뜻이므로 로드 목록에 넣지 않는다
        private static void AddVariant(Dictionary<long, List<int>> variantsByObjectId, long objectId, int variant)
        {
            if (variant <= 0) return;

            if (!variantsByObjectId.TryGetValue(objectId, out var variants))
            {
                variants = new List<int>();
                variantsByObjectId[objectId] = variants;
            }

            if (!variants.Contains(variant))
                variants.Add(variant);
        }

        /// <summary>
        /// 쌓인 커맨드에 해당하는 공격. 타입(마법/기계)마다 한 줄씩 들어있고 전부 같이 나간다.
        /// 없는 조합이면 null. 목록의 첫 줄을 대표로 보고 애니메이션과 커맨드 문자열을 가져간다
        /// </summary>
        public IReadOnlyList<AttackCommandData> GetCommands(ObjectData objectData, string command)
        {
            _datasByCommand.TryGetValue((objectData.Id, command), out var commands);
            return commands;
        }

        /// <summary>이 오브젝트가 쓰는 공격 애니메이션 번호. 공격이 없는 오브젝트면 null</summary>
        public IReadOnlyList<int> GetAnimations(ObjectData objectData)
        {
            _animationsByObjectId.TryGetValue(objectData.Id, out var animations);
            return animations;
        }

        /// <summary>이 오브젝트가 쓰는 공격 이펙트 번호. 공격이 없는 오브젝트면 null</summary>
        public IReadOnlyList<int> GetEffects(ObjectData objectData)
        {
            _effectsByObjectId.TryGetValue(objectData.Id, out var effects);
            return effects;
        }
    }
}
