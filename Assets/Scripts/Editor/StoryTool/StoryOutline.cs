using System.Collections.Generic;
using static Common.GameDefine;

namespace StoryTool
{
    /// <summary>
    /// 문서의 표를 챕터별 묶음으로 나눈 목차. 챕터마다 대상, 시나리오들, 퀘스트들, 일반 대화가 있다.
    /// 챕터는 번호에 들어 있어서(퀘스트 101 → 1장, 대상 10001 → 1장) 챕터 폴더로 나뉜 파일도 번호로 한데 모은다.
    /// 줄이 늘거나 줄지 않는 동안은 다시 만들지 않아도 된다. 줄의 칸은 볼 때마다 문서에서 읽는다
    /// </summary>
    public class StoryOutline
    {
        #region Fields
        //어느 표에든 줄이 하나라도 있는 챕터. 번호 순이다
        public IReadOnlyList<long> Chapters { get; }
        //번호 순서가 곧 진행 순서다
        public IReadOnlyList<StoryDoc> Quests { get; }
        public IReadOnlyList<StoryDoc> Scenarios { get; }
        //모든 챕터의 대상. 목차에는 챕터별 묶음을 띄우고, 이것은 대상을 전부 훑을 때 쓴다 (대상은 챕터를 넘어서도 월드에 남는다)
        public StoryDoc Actors { get; }

        private readonly Dictionary<string, StoryDoc> _docs = new();
        #endregion

        #region Init
        public StoryOutline(StoryToolDocument document)
        {
            Quests = BuildNumberDocs(document, StoryToolDocument.QuestTable, StoryLineKind.Quest);
            Scenarios = BuildNumberDocs(document, StoryToolDocument.ScenarioTable, StoryLineKind.Scenario);
            var actors = CollectLines(document, StoryToolDocument.ActorTable, StoryLineKind.Actor);
            var talks = CollectLines(document, StoryToolDocument.TalkTable, StoryLineKind.Talk);
            Actors = new StoryDoc(StoryLineKind.Actor, 0, 0, string.Empty, actors);

            var chapters = new SortedSet<long>();
            foreach (var quest in Quests)
                chapters.Add(quest.Chapter);
            foreach (var scenario in Scenarios)
                chapters.Add(scenario.Chapter);
            var actorGroups = GroupByChapter(actors, StoryActorChapterRange, chapters);
            var talkGroups = GroupByChapter(talks, StoryChapterRange, chapters);
            Chapters = new List<long>(chapters);

            foreach (var quest in Quests)
                _docs[quest.Key] = quest;
            foreach (var scenario in Scenarios)
                _docs[scenario.Key] = scenario;
            //줄이 없는 챕터에도 빈 묶음을 둔다. 묶음이 있어야 그 챕터에 첫 대상이나 첫 대화를 만들 수 있다
            foreach (long chapter in Chapters)
            {
                AddChapterDoc(StoryLineKind.Actor, chapter, actorGroups);
                AddChapterDoc(StoryLineKind.Talk, chapter, talkGroups);
            }
        }
        #endregion

        #region Public
        public StoryDoc Find(string key) => key != null && _docs.TryGetValue(key, out var doc) ? doc : null;

        /// <summary>번호로 퀘스트나 시나리오를 찾는다. 줄이 가리키는 번호(퀘스트의 Scenario, 일반 대화의 Quest)를 따라갈 때 쓴다. 없으면 null</summary>
        public StoryDoc Find(StoryLineKind kind, string number) => Find($"{kind}:{StoryLine.ParseLong(number)}");

        /// <summary>그 챕터의 대상이나 일반 대화 묶음. 그 챕터에 아직 줄이 없어도 빈 묶음을 돌려주고, 없는 챕터면 null이다</summary>
        public StoryDoc FindChapterDoc(StoryLineKind kind, long chapter) => Find($"{kind}:{chapter}");
        #endregion

        #region Private
        //퀘스트와 시나리오는 번호 칸이 같은 줄이 한 묶음이다
        private static List<StoryDoc> BuildNumberDocs(StoryToolDocument document, string tableName, StoryLineKind kind)
        {
            //번호 칸의 이름은 표 이름과 같다 (Quest 표의 Quest, Scenario 표의 Scenario)
            var groups = new SortedDictionary<long, List<StoryLine>>();
            foreach (var line in CollectLines(document, tableName, kind))
                GetOrAdd(groups, StoryLine.ParseLong(line.Get(tableName))).Add(line);

            var docs = new List<StoryDoc>(groups.Count);
            foreach (var pair in groups)
            {
                pair.Value.Sort((a, b) => a.Id.CompareTo(b.Id));
                string title = kind == StoryLineKind.Quest ? document.GetQuestTitle(pair.Key) : FindFirstNote(pair.Value);
                docs.Add(new StoryDoc(kind, pair.Key, pair.Key / StoryChapterRange, title, pair.Value));
            }
            return docs;
        }

        //대상과 일반 대화는 Id에 챕터가 들어 있다 (Id = 챕터 × range + 순번). 나온 챕터는 chapters에 더한다
        private static SortedDictionary<long, List<StoryLine>> GroupByChapter(List<StoryLine> lines, long range, SortedSet<long> chapters)
        {
            var groups = new SortedDictionary<long, List<StoryLine>>();
            foreach (var line in lines)
            {
                long chapter = line.Id / range;
                GetOrAdd(groups, chapter).Add(line);
                chapters.Add(chapter);
            }
            foreach (var group in groups.Values)
                group.Sort((a, b) => a.Id.CompareTo(b.Id));
            return groups;
        }

        private void AddChapterDoc(StoryLineKind kind, long chapter, SortedDictionary<long, List<StoryLine>> groups)
        {
            var lines = groups.TryGetValue(chapter, out var found) ? found : new List<StoryLine>();
            var doc = new StoryDoc(kind, chapter, chapter, string.Empty, lines);
            _docs[doc.Key] = doc;
        }

        private static List<StoryLine> GetOrAdd(SortedDictionary<long, List<StoryLine>> groups, long key)
        {
            if (!groups.TryGetValue(key, out var lines))
            {
                lines = new List<StoryLine>();
                groups[key] = lines;
            }
            return lines;
        }

        private static List<StoryLine> CollectLines(StoryToolDocument document, string tableName, StoryLineKind kind)
        {
            var lines = new List<StoryLine>();
            for (int i = 0; i < document.Tables.Count; i++)
            {
                if (document.Tables[i].Name != tableName) continue;
                for (int row = 0; row < document.Tables[i].RowCount; row++)
                    lines.Add(new StoryLine(document, kind, i, row));
            }
            return lines;
        }

        //시나리오는 제목 칸이 없다. 설계자가 첫 줄쯤에 적어 둔 메모가 장면 이름 노릇을 한다
        private static string FindFirstNote(List<StoryLine> lines)
        {
            foreach (var line in lines)
            {
                if (!string.IsNullOrEmpty(line.Note)) return line.Note;
            }
            return string.Empty;
        }
        #endregion
    }
}
