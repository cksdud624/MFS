using System.Collections.Generic;

namespace Generated.Table
{
    /// <summary>
    /// 대화 번호로 대사를 순서대로 꺼내기 위한 색인.
    /// 대사는 테이블에 적힌 줄 순서가 아니라 Id 순으로 나온다
    /// </summary>
    public partial class DialogueRecord
    {
        #region Fields
        private readonly Dictionary<long, List<DialogueData>> _linesByDialogueId = new();
        #endregion

        #region Init
        partial void InitCustomRecord()
        {
            foreach (var data in datas)
            {
                if (!_linesByDialogueId.TryGetValue(data.DialogueId, out var lines))
                {
                    lines = new List<DialogueData>();
                    _linesByDialogueId[data.DialogueId] = lines;
                }
                lines.Add(data);
            }

            //중간에 대사를 끼워 넣을 때 줄을 옮기지 않고 Id만 맞춰 적어도 되도록 Id로 정렬해둔다
            foreach (var lines in _linesByDialogueId.Values)
                lines.Sort((a, b) => a.Id.CompareTo(b.Id));
        }
        #endregion

        #region Public
        /// <summary>대화의 대사 목록. 없는 대화면 null</summary>
        public IReadOnlyList<DialogueData> GetLines(long dialogueId)
        {
            _linesByDialogueId.TryGetValue(dialogueId, out var lines);
            return lines;
        }
        #endregion
    }
}
