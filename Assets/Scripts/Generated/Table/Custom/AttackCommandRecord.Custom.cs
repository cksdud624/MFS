using System.Collections.Generic;

namespace Generated.Table
{
    /// <summary>
    /// 커맨드 문자열로 공격을 찾기 위한 색인.
    /// 커맨드는 누른 공격 버튼 번호를 순서대로 이어붙인 문자열이라("1" → "11" → "111")
    /// 오브젝트와 커맨드 조합으로 바로 꺼낼 수 있어야 한다.
    /// </summary>
    public partial class AttackCommandRecord
    {
        private readonly Dictionary<(long ObjectId, string Command), AttackCommandData> _datasByCommand = new();
        //오브젝트가 쓰는 공격 애니메이션 / 이펙트 번호. 클립과 프리팹을 미리 로드할 때 쓴다
        private readonly Dictionary<long, List<int>> _animationsByObjectId = new();
        private readonly Dictionary<long, List<int>> _effectsByObjectId = new();

        partial void InitCustomRecord()
        {
            foreach (var data in datas)
            {
                _datasByCommand[(data.ObjectId, data.Command)] = data;
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

        /// <summary>쌓인 커맨드에 해당하는 공격. 없는 조합이면 null</summary>
        public AttackCommandData GetCommand(ObjectData objectData, string command)
        {
            _datasByCommand.TryGetValue((objectData.Id, command), out var data);
            return data;
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
