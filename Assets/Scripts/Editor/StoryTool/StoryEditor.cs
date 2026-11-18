using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using static Common.GameDefine;

namespace StoryTool
{
    /// <summary>
    /// 문서의 표를 고치는 유일한 곳. 칸을 고치고, 줄을 끼우고 지우고 옮기고, 묶음(퀘스트, 연출), 대상, 일반 대화, 대사를 새로 만든다.
    /// 고치기 전에 Undo에 남기고, 고친 뒤에는 무엇이 바뀌었는지 이벤트로 알린다. 화면은 이벤트를 받아서 다시 그린다.
    /// 번호 규칙(줄 Id는 번호 × 1000부터 10 단위, 끼울 때는 사이 번호)을 여기서 지켜서, 툴로 만든 줄이 손으로 적은 줄과 같은 모양이 되게 한다
    /// </summary>
    public class StoryEditor
    {
        #region Types
        /// <summary>actor(Player 또는 Actor 번호, 대상 없는 줄이면 빈 문자열)를 새로 놓을 자리를 정한다. 정할 수 없으면 false</summary>
        public delegate bool PositionProvider(string actor, out long zone, out Vector2 local);
        #endregion

        #region Fields
        private const string UndoEdit = "Edit Story";
        private const string UndoMove = "Move Story Position";
        //줄 Id를 띄워 적는 간격. 사이에 줄을 끼울 자리를 남긴다
        private const long LineIdStep = 10;
        //대사 Id는 대화 번호 × 이 값 + 순번이다 (1001 → 100101)
        private const long SpeechIdRange = 100;
        private const long FirstDialogueId = 1001;
        private const string NewQuestTitle = "새 퀘스트";
        private const string NewActorName = "새 대상";
        private const string NewSpeechText = "...";

        private readonly StoryToolDocument _document;

        //새로 놓을 자리를 정해 주는 쪽. 대상이 서 있는 곳과 배치 화면에 보이는 곳은 창만 알아서 창이 넣어 준다. 없으면 위치를 채우지 않는다
        public PositionProvider DefaultPosition { get; set; }

        //칸의 값만 바뀌었다. 줄의 수와 순서는 그대로다
        public event Action OnChanged;
        //줄이 늘거나 줄었거나, 순서나 묶음이 바뀌었다. 화면이 들고 있던 줄은 더 이상 맞지 않으므로 목차부터 다시 만든다
        public event Action OnStructureChanged;
        #endregion

        #region Init
        public StoryEditor(StoryToolDocument document)
        {
            _document = document;
        }
        #endregion

        #region Public - Cell
        /// <summary>줄의 칸 하나를 고친다. 대본의 줄 수가 달라질 수 있는 칸(가리키는 대화나 연출)이라 화면은 목차부터 다시 만든다</summary>
        public void SetCell(StoryLine line, string column, string value)
        {
            Record(UndoEdit);
            if (!line.Set(column, value)) return;

            //위치를 비워 뒀던 Spawn의 대상을 플레이어로 바꾸면 위치를 적어야 한다 (플레이어는 Actor 표에 자리가 없다)
            if (column == StoryToolDocument.ColumnActor)
                EnsurePosition(line);
            OnStructureChanged?.Invoke();
        }

        /// <summary>
        /// 명령을 바꾼다. 명령마다 칸의 뜻이 달라서 앞 명령의 값은 지우고 새 명령의 기본값을 넣는다.
        /// 대상과 메모는 남긴다. 다만 새 명령이 대상을 쓰지 않으면 대상도 지운다 (남기면 쓰지 않는 칸에 값이 있다고 검사에 걸린다)
        /// </summary>
        public void SetCommand(StoryLine line, string command)
        {
            Record(UndoEdit);
            line.Set(StoryToolDocument.ColumnCommand, command);
            if (StoryCommandCatalog.GetActorUse(command) == ActorUse.None)
                line.Set(StoryToolDocument.ColumnActor, string.Empty);
            line.Set(StoryToolDocument.ColumnZone, string.Empty);
            line.Set(StoryToolDocument.ColumnParam1, GetDefault(StoryCommandCatalog.GetParam(line, 1)));
            line.Set(StoryToolDocument.ColumnParam2, GetDefault(StoryCommandCatalog.GetParam(line, 2)));
            if (!StoryCommandCatalog.IsGoal(command))
                line.Set(StoryToolDocument.ColumnText, string.Empty);
            EnsurePosition(line);
            OnStructureChanged?.Invoke();
        }

        /// <summary>위치 칸에 Zone과 그 Zone 안의 위치를 적는다. 끌어 놓는 동안 계속 불리므로 화면은 값만 다시 읽는다</summary>
        public void SetPosition(PositionSlot slot, long zone, Vector2 local)
        {
            Record(UndoMove);
            if (_document.WritePosition(slot, zone, local))
                OnChanged?.Invoke();
        }

        /// <summary>Spawn의 위치를 비워서 Actor 표의 자리를 쓰게 한다</summary>
        public void ClearPosition(StoryLine line)
        {
            Record(UndoEdit);
            //둘 다 지워야 하므로 ||로 잇지 않는다
            if (line.Set(StoryToolDocument.ColumnZone, string.Empty) | line.Set(StoryToolDocument.ColumnParam1, string.Empty))
                OnStructureChanged?.Invoke();
        }

        /// <summary>대사 한 줄의 칸(Speaker, Text)을 고친다</summary>
        public void SetSpeech(StoryToolDocument.DialogueLine speech, string column, string value)
        {
            Record(UndoEdit);
            if (_document.SetCell(speech.TableIndex, speech.Row, column, value))
                OnStructureChanged?.Invoke();
        }

        /// <summary>퀘스트 제목을 고친다 (QuestInfo 표). 그 퀘스트의 줄이 없으면 아무것도 하지 않는다</summary>
        public void SetQuestTitle(long quest, string title)
        {
            if (!TryFindRow(StoryToolDocument.QuestInfoTable, StoryToolDocument.ColumnId, ToText(quest), out int tableIndex, out int row)) return;

            Record(UndoEdit);
            if (_document.SetCell(tableIndex, row, StoryToolDocument.ColumnTitle, title))
                OnStructureChanged?.Invoke();
        }
        #endregion

        #region Public - Line
        /// <summary>
        /// 묶음에서 after 바로 다음에 새 줄을 끼우고 그 줄의 키를 돌려준다. 끼울 수 없으면 null.
        /// Id는 앞뒤 줄의 사이 번호를 쓴다. 사이가 비어 있지 않으면 그 묶음의 줄을 10 단위로 다시 매긴다 (줄 Id는 다른 표가 가리키지 않아서 바꿔도 된다)
        /// </summary>
        public string InsertLine(StoryDoc doc, StoryLine after)
        {
            int index = IndexOf(doc, after);
            if (index < 0) return null;

            long lower = after.Id;
            bool hasNext = index + 1 < doc.Lines.Count;
            long upper = hasNext ? doc.Lines[index + 1].Id : (doc.Number + 1) * StoryLineIdRange;
            //뒤에 줄이 있으면 가운데 번호, 마지막 줄이면 10을 띄운 번호. 남은 번호가 모자라면 바로 다음 번호라도 쓴다
            long id = hasNext ? lower + (upper - lower) / 2
                : lower + LineIdStep < upper ? lower + LineIdStep
                : lower + 1 < upper ? lower + 1 : lower;

            Record(UndoEdit);
            if (id == lower)
            {
                if (!TryRenumber(doc, index + 1, out id)) return null;
            }

            string kindColumn = doc.Kind == StoryLineKind.Quest ? StoryToolDocument.QuestTable : StoryToolDocument.ScenarioTable;
            int row = after.Row + 1;
            var table = _document.Tables[after.TableIndex];
            table.InsertRow(row);
            table.Set(row, StoryToolDocument.ColumnId, ToText(id));
            table.Set(row, kindColumn, ToText(doc.Number));

            var line = new StoryLine(_document, doc.Kind, after.TableIndex, row);
            FillCommand(line, StoryCommandCatalog.GetDefaultCommand(doc.Kind));
            NotifyStructureChanged();
            return line.Key;
        }

        /// <summary>줄을 지운다. 퀘스트의 마지막 줄을 지우면 그 퀘스트의 제목(QuestInfo)도 같이 지운다. 남으면 없는 퀘스트의 제목이라고 검사에 걸린다</summary>
        public void RemoveLine(StoryDoc doc, StoryLine line)
        {
            Record(UndoEdit);
            _document.Tables[line.TableIndex].RemoveRow(line.Row);
            if (doc.Kind == StoryLineKind.Quest && doc.Lines.Count == 1
                && TryFindRow(StoryToolDocument.QuestInfoTable, StoryToolDocument.ColumnId, ToText(doc.Number), out int tableIndex, out int row))
                _document.Tables[tableIndex].RemoveRow(row);
            NotifyStructureChanged();
        }

        /// <summary>
        /// 줄을 묶음 안에서 한 칸 위(-1)나 아래(1)로 옮기고, 옮긴 줄의 새 키를 돌려준다. 옮길 곳이 없으면 null.
        /// 줄은 Id 순으로 실행되므로 이웃 줄과 내용을 맞바꾸고 Id는 제자리에 둔다. 그래서 파일에서도 Id 순서가 유지된다
        /// </summary>
        public string MoveLine(StoryDoc doc, StoryLine line, int direction)
        {
            int index = IndexOf(doc, line);
            int neighborIndex = index + direction;
            if (index < 0 || neighborIndex < 0 || neighborIndex >= doc.Lines.Count) return null;

            var neighbor = doc.Lines[neighborIndex];
            string lineId = line.IdText;
            string neighborId = neighbor.IdText;

            Record(UndoEdit);
            //같은 파일이면 줄의 자리까지 바꿔서 파일의 순서를 지킨다. 다른 파일이면(같은 번호가 두 파일에 나뉘어 있을 때) Id만 바꾼다
            bool isSameFile = line.TableIndex == neighbor.TableIndex;
            if (isSameFile)
                _document.Tables[line.TableIndex].SwapRows(line.Row, neighbor.Row);
            //자리를 바꿨으면 Id가 내용을 따라갔으므로 제자리로 되돌리고, 바꾸지 않았으면 Id를 서로 바꾼다. 어느 쪽이든 옮긴 줄이 이웃의 Id를 갖는다
            line.Set(StoryToolDocument.ColumnId, isSameFile ? lineId : neighborId);
            neighbor.Set(StoryToolDocument.ColumnId, isSameFile ? neighborId : lineId);
            NotifyStructureChanged();
            return $"{doc.Kind}:{neighborId}";
        }
        #endregion

        #region Public - New
        /// <summary>
        /// 그 챕터에 새 퀘스트나 연출을 만들고 그 묶음의 키를 돌려준다. 번호는 그 챕터의 마지막 번호 다음이다.
        /// 첫 줄을 하나 넣어서 만든다. 줄이 없는 묶음은 표에 적을 수가 없다. 만들 수 없으면 에러를 남기고 null
        /// </summary>
        public string AddDoc(StoryOutline outline, StoryLineKind kind, long chapter)
        {
            string tableName = kind == StoryLineKind.Quest ? StoryToolDocument.QuestTable : StoryToolDocument.ScenarioTable;
            long number = chapter * StoryChapterRange;
            foreach (var doc in kind == StoryLineKind.Quest ? outline.Quests : outline.Scenarios)
            {
                if (doc.Chapter == chapter)
                    number = Math.Max(number, doc.Number);
            }
            number++;
            if (number >= (chapter + 1) * StoryChapterRange)
                return Fail($"Chapter {chapter} has no number left for a new {tableName}.");

            int tableIndex = _document.FindChapterTable(tableName, chapter);
            int infoIndex = kind == StoryLineKind.Quest ? _document.FindChapterTable(StoryToolDocument.QuestInfoTable, chapter) : 0;
            if (tableIndex < 0 || infoIndex < 0)
                return Fail($"{TableUpdater.ChapterDirectoryPrefix}{chapter} has no {tableName}.tsv. Create the chapter folder and copy the header lines first.");

            Record(UndoEdit);
            var table = _document.Tables[tableIndex];
            int row = table.RowCount;
            table.InsertRow(row);
            table.Set(row, StoryToolDocument.ColumnId, ToText(number * StoryLineIdRange + LineIdStep));
            table.Set(row, tableName, ToText(number));
            FillCommand(new StoryLine(_document, kind, tableIndex, row), StoryCommandCatalog.GetDefaultCommand(kind));

            if (kind == StoryLineKind.Quest)
            {
                var info = _document.Tables[infoIndex];
                int infoRow = info.RowCount;
                info.InsertRow(infoRow);
                info.Set(infoRow, StoryToolDocument.ColumnId, ToText(number));
                info.Set(infoRow, StoryToolDocument.ColumnTitle, NewQuestTitle);
            }
            NotifyStructureChanged();
            return $"{kind}:{number}";
        }

        /// <summary>그 챕터에 새 대상을 만들고 그 줄의 키를 돌려준다. 캐릭터는 플레이어와 같은 것으로 두고, 자리는 zone의 local이다</summary>
        public string AddActor(StoryOutline outline, long chapter, long zone, Vector2 local)
        {
            long id = chapter * StoryActorChapterRange;
            foreach (var line in outline.Actors.Lines)
            {
                if (line.Id / StoryActorChapterRange == chapter)
                    id = Math.Max(id, line.Id);
            }
            id++;

            int tableIndex = _document.FindChapterTable(StoryToolDocument.ActorTable, chapter);
            if (tableIndex < 0) return Fail($"{StoryToolDocument.ActorTable}.tsv is missing.");

            Record(UndoEdit);
            var table = _document.Tables[tableIndex];
            int row = table.RowCount;
            table.InsertRow(row);
            table.Set(row, StoryToolDocument.ColumnId, ToText(id));
            table.Set(row, StoryToolDocument.ColumnObjectId, ToText(PlayerObjectId));
            table.Set(row, StoryToolDocument.ColumnType, StoryActorType.Npc.ToString());
            table.Set(row, StoryToolDocument.ColumnName, NewActorName);
            table.Set(row, StoryToolDocument.ColumnResident, bool.FalseString.ToLowerInvariant());
            var actor = new StoryLine(_document, StoryLineKind.Actor, tableIndex, row);
            if (_document.TryGetOwnPositionSlot(actor, out var slot))
                _document.WritePosition(slot, zone, local);
            NotifyStructureChanged();
            return actor.Key;
        }

        /// <summary>
        /// 그 챕터에 새 일반 대화를 만들고 그 줄의 키를 돌려준다. 말을 걸 Npc, 퀘스트, 대화는 첫 번째 것으로 넣어 두고 만든 뒤에 고른다.
        /// 비워 두면 번호 칸이 0이라 저장할 때 형식 에러로만 보이고 무엇을 골라야 하는지 알기 어렵다
        /// </summary>
        public string AddTalk(StoryOutline outline, long chapter)
        {
            long id = chapter * StoryChapterRange;
            var doc = outline.FindChapterDoc(StoryLineKind.Talk, chapter);
            if (doc != null)
            {
                foreach (var line in doc.Lines)
                    id = Math.Max(id, line.Id);
            }
            id++;
            if (id >= (chapter + 1) * StoryChapterRange)
                return Fail($"Chapter {chapter} has no Id left for a new {StoryToolDocument.TalkTable}.");

            int tableIndex = _document.FindChapterTable(StoryToolDocument.TalkTable, chapter);
            if (tableIndex < 0)
                return Fail($"{TableUpdater.ChapterDirectoryPrefix}{chapter} has no {StoryToolDocument.TalkTable}.tsv. Create the chapter folder and copy the header lines first.");

            string npc = string.Empty;
            foreach (var actor in outline.Actors.Lines)
            {
                if (_document.GetActorType(actor.IdText) != StoryActorType.Npc) continue;
                npc = actor.IdText;
                break;
            }
            //일반 대화는 같은 챕터의 퀘스트만 가리킬 수 있다
            string quest = string.Empty;
            foreach (var questDoc in outline.Quests)
            {
                if (questDoc.Chapter != chapter) continue;
                quest = ToText(questDoc.Number);
                break;
            }

            Record(UndoEdit);
            var table = _document.Tables[tableIndex];
            int row = table.RowCount;
            table.InsertRow(row);
            table.Set(row, StoryToolDocument.ColumnId, ToText(id));
            table.Set(row, StoryToolDocument.ColumnActor, npc);
            table.Set(row, StoryToolDocument.ColumnQuest, quest);
            table.Set(row, StoryToolDocument.ColumnDialogue, GetDefault(new ParamSpec(ParamKind.Dialogue, string.Empty)));
            NotifyStructureChanged();
            return $"{StoryLineKind.Talk}:{id}";
        }

        /// <summary>새 대화를 만들고(대사 한 줄) 그 대사를 돌려준다. 대화 번호는 마지막 번호 다음이다. 만들 수 없으면 false</summary>
        public bool TryAddDialogue(string speaker, out StoryToolDocument.DialogueLine speech)
        {
            long dialogueId = FirstDialogueId - 1;
            foreach (var line in _document.GetAllDialogueLines())
                dialogueId = Math.Max(dialogueId, StoryLine.ParseLong(line.DialogueId));
            return TryAddSpeech(ToText(dialogueId + 1), speaker, out speech);
        }

        /// <summary>대화의 끝에 대사 한 줄을 붙인다. 대사의 Id는 다른 줄(Line)이 가리키므로 사이에 끼우지 않는다. 끼우면 뒤의 Id가 밀려서 가리키던 대사가 바뀐다</summary>
        public bool TryAddSpeech(string dialogueId, string speaker, out StoryToolDocument.DialogueLine speech)
        {
            speech = default;
            int tableIndex = _document.FindChapterTable(StoryToolDocument.DialogueTable, 0);
            if (tableIndex < 0)
            {
                Fail($"{StoryToolDocument.DialogueTable}.tsv is missing.");
                return false;
            }

            var table = _document.Tables[tableIndex];
            long id = StoryLine.ParseLong(dialogueId) * SpeechIdRange;
            //같은 대화의 마지막 대사 바로 아래에 적는다. 새 대화면 표의 끝이다
            int row = table.RowCount;
            foreach (var line in _document.GetDialogue(dialogueId))
            {
                id = Math.Max(id, StoryLine.ParseLong(line.Id));
                row = line.Row + 1;
            }

            Record(UndoEdit);
            table.InsertRow(row);
            table.Set(row, StoryToolDocument.ColumnId, ToText(id + 1));
            table.Set(row, StoryToolDocument.ColumnDialogueId, dialogueId);
            table.Set(row, StoryToolDocument.ColumnSpeaker, speaker);
            table.Set(row, StoryToolDocument.ColumnText, NewSpeechText);
            speech = new StoryToolDocument.DialogueLine(table, tableIndex, row);
            NotifyStructureChanged();
            return true;
        }

        public void RemoveSpeech(StoryToolDocument.DialogueLine speech)
        {
            Record(UndoEdit);
            _document.Tables[speech.TableIndex].RemoveRow(speech.Row);
            NotifyStructureChanged();
        }
        #endregion

        #region Private
        private void Record(string name) => Undo.RecordObject(_document, name);

        /// <summary>
        /// 줄이 위치를 써야 하는데 비어 있으면 채운다. 명령을 위치를 쓰는 것으로 바꾼 직후가 그렇다.
        /// 비워 두면 배치 화면에 핸들이 뜨지 않아서 끌어 놓을 수가 없다. 자리는 창이 정해 준다 (그 대상이 서 있던 곳)
        /// </summary>
        private void EnsurePosition(StoryLine line)
        {
            if (DefaultPosition == null) return;
            //위치를 비운 Spawn은 Actor 표의 자리를 쓰는 것이므로 채우지 않는다. 다만 Actor 표에 없는 대상(플레이어)은 적어야 한다
            if (StoryToolDocument.UsesActorPosition(line) && _document.TryFindActor(line.Actor, out _)) return;
            if (!_document.TryGetOwnPositionSlot(line, out var slot) || _document.TryReadPosition(slot, out _, out _)) return;

            if (DefaultPosition(line.Actor, out long zone, out var local))
                _document.WritePosition(slot, zone, local);
        }

        //줄을 끼우고 지우는 것은 표를 직접 고치므로 문서가 바뀐 줄 모른다
        private void NotifyStructureChanged()
        {
            _document.MarkChanged();
            OnStructureChanged?.Invoke();
        }

        //새 줄에 명령과 그 명령의 기본값을 넣는다
        private void FillCommand(StoryLine line, string command)
        {
            line.Set(StoryToolDocument.ColumnCommand, command);
            line.Set(StoryToolDocument.ColumnParam1, GetDefault(StoryCommandCatalog.GetParam(line, 1)));
            line.Set(StoryToolDocument.ColumnParam2, GetDefault(StoryCommandCatalog.GetParam(line, 2)));
        }

        /// <summary>
        /// 명령을 골랐을 때 Param 칸에 넣어 둘 값. 다른 표를 가리키는 칸은 그 표의 첫 줄을 넣는다.
        /// 비워 두면 형식 에러로 저장할 때 걸리는데, 고르기 전의 줄이 에러로 보이는 것보다 일단 맞는 값이 들어 있는 쪽이 낫다
        /// </summary>
        private string GetDefault(ParamSpec spec)
        {
            switch (spec.Kind)
            {
                case ParamKind.Dialogue:
                {
                    var lines = _document.GetAllDialogueLines();
                    return lines.Count > 0 ? lines[0].DialogueId : string.Empty;
                }
                case ParamKind.DialogueLine:
                {
                    var lines = _document.GetAllDialogueLines();
                    return lines.Count > 0 ? lines[0].Id : string.Empty;
                }
                default:
                    return spec.Default ?? string.Empty;
            }
        }

        /// <summary>
        /// 묶음의 줄 Id를 10 단위로 다시 매긴다. insertIndex 자리에 새 줄이 들어갈 번호를 비워 두고 그 번호를 newId로 돌려준다.
        /// 줄이 너무 많아서 10 단위로 다 들어가지 않으면 1 단위로 매기고, 그래도 안 들어가면 false
        /// </summary>
        private bool TryRenumber(StoryDoc doc, int insertIndex, out long newId)
        {
            newId = 0;
            int count = doc.Lines.Count + 1;
            long step = count * LineIdStep < StoryLineIdRange ? LineIdStep : 1;
            if (count * step >= StoryLineIdRange)
            {
                Fail($"{doc.Kind} {doc.Number} has no Id left for a new line.");
                return false;
            }

            long baseId = doc.Number * StoryLineIdRange;
            newId = baseId + (insertIndex + 1) * step;
            for (int i = 0; i < doc.Lines.Count; i++)
            {
                int order = i < insertIndex ? i : i + 1;
                doc.Lines[i].Set(StoryToolDocument.ColumnId, ToText(baseId + (order + 1) * step));
            }
            return true;
        }

        private static int IndexOf(StoryDoc doc, StoryLine line)
        {
            for (int i = 0; i < doc.Lines.Count; i++)
            {
                if (doc.Lines[i].TableIndex == line.TableIndex && doc.Lines[i].Row == line.Row) return i;
            }
            return -1;
        }

        private bool TryFindRow(string tableName, string column, string value, out int tableIndex, out int row)
        {
            for (tableIndex = 0; tableIndex < _document.Tables.Count; tableIndex++)
            {
                if (_document.Tables[tableIndex].Name != tableName) continue;
                row = _document.Tables[tableIndex].FindRow(column, value);
                if (row >= 0) return true;
            }
            row = -1;
            return false;
        }

        private static string ToText(long value) => value.ToString(CultureInfo.InvariantCulture);

        //에러를 남기고 null을 돌려준다. 실패 지점에서 바로 return할 수 있게 한다
        private static string Fail(string message)
        {
            Debug.LogError(message);
            return null;
        }
        #endregion
    }
}
