using System.Collections.Generic;

namespace StoryTool
{
    /// <summary>
    /// 툴에서 한 번에 열어 보는 묶음. 퀘스트 하나, 시나리오 하나, 또는 한 챕터의 대상(Actor 표)이나 일반 대화(Talk 표) 전체다.
    /// 퀘스트와 시나리오는 번호 하나가 묶음이고, 대상과 일반 대화는 줄마다 따로라서 챕터 단위로 묶는다
    /// </summary>
    public class StoryDoc
    {
        #region Fields
        public StoryLineKind Kind { get; }
        //퀘스트나 시나리오 번호. 대상과 일반 대화 묶음은 챕터 번호다
        public long Number { get; }
        public long Chapter { get; }
        //목록에 번호와 같이 띄우는 글. 퀘스트는 제목, 시나리오는 첫 메모다. 대상과 일반 대화 묶음은 비어 있다
        public string Title { get; }
        //Id 순으로 정렬되어 있다. 게임도 표에 적힌 순서가 아니라 Id 순으로 실행한다
        public IReadOnlyList<StoryLine> Lines { get; }

        //화면이 고른 묶음을 기억할 때 쓰는 키
        public string Key => $"{Kind}:{Number}";
        #endregion

        #region Init
        public StoryDoc(StoryLineKind kind, long number, long chapter, string title, IReadOnlyList<StoryLine> lines)
        {
            Kind = kind;
            Number = number;
            Chapter = chapter;
            Title = title;
            Lines = lines;
        }
        #endregion
    }
}
