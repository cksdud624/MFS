using System;
using System.Collections.Generic;
using System.Globalization;
using InGame.Story;
using UnityEngine;
using UnityEngine.UIElements;
using static Common.GameDefine;

namespace StoryTool
{
    /// <summary>
    /// 시나리오 툴 창의 오른쪽 칸. 고른 줄의 칸을 고르는 칸으로 띄운다. 무엇을 띄울지는 명령마다 다르고, StoryCommandCatalog에 적힌 쓰임새를 따른다.
    /// 번호를 적는 칸은 이름이나 내용이 보이는 목록으로 고르게 해서(대상, 대화, 연출), 다른 표를 열어 번호를 찾아보지 않아도 되게 한다.
    /// 표를 직접 고치지 않는다. 고르면 StoryEditor에 넘기고, 화면을 다시 그리는 것은 창이 StoryEditor의 이벤트를 받아서 한다
    /// </summary>
    public class StoryToolInspector
    {
        #region Fields
        private const string ClassTitle = "story-inspector__title";
        private const string ClassMeta = "story-inspector__meta";
        private const string ClassSection = "story-inspector__section";
        private const string ClassField = "story-inspector__field";
        private const string ClassButton = "story-inspector__button";
        private const string ClassCell = "story-cell";
        private const string ClassCellName = "story-cell__name";
        private const string ClassCellValue = "story-cell__value";

        //목록에서 "새로 만들기"를 뜻하는 값. 표에 적는 값이 아니라 고르면 새 줄을 만들어 그 번호를 적는다
        private const string NewItem = "+new";
        private const string NewItemLabel = "+ 새로 만들기";
        private const string EmptyLabel = "(없음)";
        //목록에 띄우는 대사는 이 글자 수에서 자른다
        private const int SummaryLength = 24;
        private static readonly List<string> ActorTypes = new() { StoryActorType.Npc.ToString(), StoryActorType.Enemy.ToString() };

        private readonly StoryToolDocument _document;
        private readonly StoryEditor _editor;

        //지금 띄우고 있는 것. 버튼이 화면을 다시 만들 때 같은 것을 다시 띄운다
        private StoryOutline _outline;
        private StoryLine _line;
        private int _speechIndex;
        private string _error;
        private bool _isStageOpen;

        //위치 입력 칸. 핸들을 끄는 동안 값만 바꿔 넣으려고 들고 있는다
        private PopupField<long> _zoneField;
        private Vector2Field _pointField;
        private FloatField _xField;
        private VisualElement _cellList;
        //위치 입력 칸에 적은 값을 표에 적는 중인지. 이 동안은 입력 칸을 다시 맞추지 않는다. 적는 도중에 값을 바꿔 넣으면 입력하던 글자가 지워진다
        private bool _isApplyingPosition;

        public VisualElement Root { get; }
        #endregion

        #region Init
        public StoryToolInspector(StoryToolDocument document, StoryEditor editor)
        {
            _document = document;
            _editor = editor;
            Root = new ScrollView();
            Root.style.flexGrow = 1f;
        }
        #endregion

        #region Public
        /// <summary>
        /// 고른 줄을 띄운다. speechIndex가 0 이상이면 그 줄(대화) 아래에 펼친 대사를 고른 것이라 그 대사를 고치는 칸을 띄운다.
        /// error는 마지막 검사에서 그 줄에 걸린 내용이다 (없으면 null)
        /// </summary>
        public void Show(StoryOutline outline, StoryLine line, int speechIndex, string error, bool isStageOpen)
        {
            _outline = outline;
            _line = line;
            _speechIndex = speechIndex;
            _error = error;
            _isStageOpen = isStageOpen;
            Rebuild();
        }

        /// <summary>핸들을 끌거나 Undo로 값이 바뀌었을 때 위치 입력 칸과 표의 칸만 맞춘다. 칸을 다시 만들면 입력하던 칸이 포커스를 잃는다</summary>
        public void RefreshValues()
        {
            RefreshCells();
            if (_line == null || _zoneField == null || _isApplyingPosition) return;
            if (!_document.TryGetPositionSlot(_line, out var slot) || !_document.TryReadPosition(slot, out long zoneId, out var local)) return;

            _zoneField.SetValueWithoutNotify(zoneId);
            _pointField?.SetValueWithoutNotify(local);
            _xField?.SetValueWithoutNotify(local.x);
        }
        #endregion

        #region Private - Build
        private void Rebuild()
        {
            Root.Clear();
            _zoneField = null;
            _pointField = null;
            _xField = null;
            _cellList = null;
            if (_line == null) return;

            AddLabel(StoryCommandCatalog.DescribeAction(_line, _document), ClassTitle);
            AddLabel($"{_line.TableName} 표 · Id {_line.IdText}", ClassMeta);
            if (_error != null)
                Root.Add(new HelpBox(_error, HelpBoxMessageType.Error));

            if (TryGetSelectedSpeech(out var speech))
                AddSpeechFields(speech, true);
            else if (_line.Kind == StoryLineKind.Actor)
                AddActorFields();
            else if (_line.Kind == StoryLineKind.Talk)
                AddTalkFields();
            else
                AddLineFields();

            AddSection("표의 칸");
            _cellList = new VisualElement();
            Root.Add(_cellList);
            RefreshCells();
        }

        //대화 아래에 펼친 대사를 골랐을 때 그 대사
        private bool TryGetSelectedSpeech(out StoryToolDocument.DialogueLine speech)
        {
            speech = default;
            string dialogueId = StoryToolDocument.GetDialogueId(_line);
            if (_speechIndex < 0 || dialogueId == null) return false;

            var speeches = _document.GetDialogue(dialogueId);
            if (_speechIndex >= speeches.Count) return false;

            speech = speeches[_speechIndex];
            return true;
        }

        private void AddLineFields()
        {
            string command = _line.Command;
            AddPopup("명령", StoryCommandCatalog.GetCommands(_line.Kind), command, value => value, value => _editor.SetCommand(_line, value));

            var actorUse = StoryCommandCatalog.GetActorUse(command);
            if (actorUse != ActorUse.None)
                AddActorPopup(StoryCommandCatalog.GetActorLabel(command), _line.Actor, actorUse == ActorUse.Optional, true,
                    value => _editor.SetCell(_line, StoryToolDocument.ColumnActor, value));

            if (StoryCommandCatalog.GetPositionKind(command) != PositionKind.None)
                AddLinePosition();
            AddParamField(1, StoryToolDocument.ColumnParam1);
            AddParamField(2, StoryToolDocument.ColumnParam2);

            if (StoryCommandCatalog.IsGoal(command))
                AddTextField("할 일 (화면에 띄우는 글)", _line.Text, false, value => _editor.SetCell(_line, StoryToolDocument.ColumnText, value));
            AddTextField("메모 (게임은 읽지 않는다)", _line.Note, true, value => _editor.SetCell(_line, StoryToolDocument.ColumnNote, value));

            //한 줄짜리 대사는 그 자리에서 고친다
            if (command == StoryCommandCatalog.CommandLine && _document.TryGetDialogueLine(_line.Param1, out var speech))
                AddSpeechFields(speech, false);
            if (command == StoryCommandCatalog.CommandDialogue)
                AddSpeechButton(_line.Param1);
        }

        private void AddActorFields()
        {
            AddTextField("이름 (말풍선에 띄운다)", _line.Get(StoryToolDocument.ColumnName), false,
                value => _editor.SetCell(_line, StoryToolDocument.ColumnName, value));
            AddPopup("종류", new List<string>(ActorTypes), _line.Get(StoryToolDocument.ColumnType), value => value,
                value => _editor.SetCell(_line, StoryToolDocument.ColumnType, value));

            var objectIds = _document.GetCharacterObjectIds().ConvertAll(id => id.ToString(CultureInfo.InvariantCulture));
            AddPopup("캐릭터 (Object 번호)", objectIds, _line.Get(StoryToolDocument.ColumnObjectId), value => value,
                value => _editor.SetCell(_line, StoryToolDocument.ColumnObjectId, value));

            bool resident = bool.TryParse(_line.Get(StoryToolDocument.ColumnResident), out bool parsed) && parsed;
            var residentToggle = new Toggle("처음부터 서 있음") { value = resident, tooltip = "끄면 시나리오의 Spawn으로 등장한다" };
            residentToggle.AddToClassList(ClassField);
            residentToggle.RegisterValueChangedCallback(change =>
                _editor.SetCell(_line, StoryToolDocument.ColumnResident, change.newValue ? "true" : "false"));
            Root.Add(residentToggle);

            if (_document.TryGetPositionSlot(_line, out var slot))
                AddPositionFields(slot);
            AddTextField("메모 (누구인지)", _line.Note, true, value => _editor.SetCell(_line, StoryToolDocument.ColumnNote, value));
        }

        //일반 대화. 누구에게 말을 걸면, 어느 퀘스트가 시작된 뒤부터, 어떤 대화가 나오는지를 고른다. 대사는 그 아래에 펼친 줄을 골라서 고친다
        private void AddTalkFields()
        {
            AddActorPopup("말을 걸 Npc", _line.Actor, false, false, value => _editor.SetCell(_line, StoryToolDocument.ColumnActor, value));
            AddQuestPopup("이 퀘스트가 시작된 뒤부터", _line.Get(StoryToolDocument.ColumnQuest), StoryToolDocument.ColumnQuest);
            AddDialoguePopup("대화", _line.Get(StoryToolDocument.ColumnDialogue), StoryToolDocument.ColumnDialogue);
            AddTextField("메모 (게임은 읽지 않는다)", _line.Note, true, value => _editor.SetCell(_line, StoryToolDocument.ColumnNote, value));
            AddSpeechButton(_line.Get(StoryToolDocument.ColumnDialogue));
        }

        /// <summary>
        /// 줄의 위치. 위치를 비운 Spawn은 Actor 표의 자리를 쓰므로 그 자리를 보여주고, 이 줄에만 다른 자리를 적을지 고르게 한다.
        /// 옮기면 그 대상을 쓰는 다른 줄도 같이 바뀐다는 것을 알려야 한다
        /// </summary>
        private void AddLinePosition()
        {
            bool isOptional = StoryCommandCatalog.GetPositionKind(_line.Command) == PositionKind.OptionalPoint;
            if (StoryToolDocument.UsesActorPosition(_line))
            {
                if (!_document.TryGetPositionSlot(_line, out var actorSlot)) return;

                AddPositionFields(actorSlot);
                Root.Add(new HelpBox("위치를 비워서 Actor 표의 자리를 쓴다. 옮기면 Actor 표가 바뀐다.", HelpBoxMessageType.Info));
                AddButton("이 줄에만 다른 자리 적기", WriteOwnPosition);
                return;
            }

            if (_document.TryGetOwnPositionSlot(_line, out var slot))
                AddPositionFields(slot);
            //플레이어는 Actor 표에 없어서 비울 수 없다
            if (isOptional && _document.TryFindActor(_line.Actor, out _))
                AddButton("Actor 표의 자리 쓰기 (위치 비우기)", () => _editor.ClearPosition(_line));
        }

        private void AddPositionFields(PositionSlot slot)
        {
            AddSection("위치 (Zone 로컬 좌표)");
            if (!_document.TryReadPosition(slot, out long zoneId, out var local))
            {
                Root.Add(new HelpBox("위치를 읽지 못했다. 표의 Zone과 위치 칸을 확인한다.", HelpBoxMessageType.Warning));
                return;
            }

            var zones = new List<long>();
            foreach (var zone in _document.Zones)
                zones.Add(zone.Id);
            _zoneField = new PopupField<long>("Zone", zones, zoneId);
            _zoneField.AddToClassList(ClassField);
            _zoneField.RegisterValueChangedCallback(_ => ApplyPositionFields(slot));
            Root.Add(_zoneField);

            if (slot.IsXOnly)
            {
                _xField = new FloatField("X") { value = local.x, isDelayed = true };
                _xField.AddToClassList(ClassField);
                _xField.RegisterValueChangedCallback(_ => ApplyPositionFields(slot));
                Root.Add(_xField);
            }
            else
            {
                _pointField = new Vector2Field("위치") { value = local };
                _pointField.AddToClassList(ClassField);
                _pointField.RegisterValueChangedCallback(_ => ApplyPositionFields(slot));
                Root.Add(_pointField);
            }

            if (!_isStageOpen)
                Root.Add(new HelpBox("배치 화면을 열면 Scene 뷰에서 끌어서 옮길 수 있다.", HelpBoxMessageType.None));
        }

        //Param 칸을 쓰임새에 맞는 칸으로 띄운다. 위치는 위치 칸이 따로 띄우고, 쓰지 않는 칸은 띄우지 않는다
        private void AddParamField(int number, string column)
        {
            var spec = StoryCommandCatalog.GetParam(_line, number);
            string value = _line.Get(column);
            switch (spec.Kind)
            {
                case ParamKind.Number:
                case ParamKind.Text:
                    AddTextField(spec.Label, value, false, text => _editor.SetCell(_line, column, text));
                    break;
                case ParamKind.Dialogue:
                    AddDialoguePopup(spec.Label, value, column);
                    break;
                case ParamKind.DialogueLine:
                    AddDialogueLinePopup(spec.Label, value, column);
                    break;
                case ParamKind.Scenario:
                    AddScenarioPopup(spec.Label, value, column);
                    break;
            }
        }

        //대사 한 줄을 고치는 칸. canRemove면 대사를 지우는 버튼도 둔다 (대화 아래에 펼친 대사를 골랐을 때)
        private void AddSpeechFields(StoryToolDocument.DialogueLine speech, bool canRemove)
        {
            AddSection($"대사 (Dialogue 표 · Id {speech.Id})");
            AddActorPopup("말하는 대상", speech.Speaker, false, true, value => _editor.SetSpeech(speech, StoryToolDocument.ColumnSpeaker, value));
            AddTextField("대사", speech.Text, true, value => _editor.SetSpeech(speech, StoryToolDocument.ColumnText, value));
            if (!canRemove) return;

            AddSpeechButton(speech.DialogueId);
            AddButton("이 대사 지우기", () => _editor.RemoveSpeech(speech));
        }

        private void AddSpeechButton(string dialogueId)
        {
            //말하는 대상은 대화 상대가 있으면 상대, 없으면 플레이어로 넣어 둔다. 만든 뒤에 고르면 된다
            string speaker = string.IsNullOrEmpty(_line.Actor) ? StoryActor.PlayerName : _line.Actor;
            AddButton("+ 대사 (이 대화의 끝에)", () => _editor.TryAddSpeech(dialogueId, speaker, out _));
        }
        #endregion

        #region Private - Popup
        //대상을 이름으로 고른다. 표에는 Player나 Actor 번호가 적힌다. 플레이어를 가리킬 수 없는 칸(말을 걸 Npc)은 includePlayer를 끈다
        private void AddActorPopup(string label, string value, bool allowEmpty, bool includePlayer, Action<string> onChanged)
        {
            var choices = new List<string>();
            if (allowEmpty)
                choices.Add(string.Empty);
            if (includePlayer)
                choices.Add(StoryActor.PlayerName);
            foreach (var actor in _outline.Actors.Lines)
                choices.Add(actor.IdText);

            AddPopup(label, choices, value, FormatActor, onChanged);
        }

        private string FormatActor(string actor)
        {
            if (string.IsNullOrEmpty(actor)) return EmptyLabel;
            return StoryToolDocument.IsPlayer(actor) ? _document.GetActorName(actor) : $"{_document.GetActorName(actor)} ({actor})";
        }

        //대화를 내용으로 고른다. 표에는 대화 번호가 적힌다
        private void AddDialoguePopup(string label, string value, string column)
        {
            //대화 번호 → 첫 대사와 대사 수
            var summaries = new Dictionary<string, string>();
            var counts = new Dictionary<string, int>();
            var choices = new List<string>();
            foreach (var speech in _document.GetAllDialogueLines())
            {
                if (!summaries.ContainsKey(speech.DialogueId))
                {
                    summaries[speech.DialogueId] = Summarize(speech);
                    choices.Add(speech.DialogueId);
                }
                counts[speech.DialogueId] = counts.GetValueOrDefault(speech.DialogueId) + 1;
            }
            choices.Add(NewItem);

            string Format(string id)
            {
                if (id == NewItem) return NewItemLabel;
                return summaries.TryGetValue(id, out string summary) ? $"{id} · {summary} ({counts[id]}줄)" : $"{id} (없는 대화)";
            }
            AddPopup(label, choices, value, Format, id => SetDialogue(column, id, false));
        }

        //대사 한 줄을 내용으로 고른다. 표에는 대사의 Id가 적힌다
        private void AddDialogueLinePopup(string label, string value, string column)
        {
            var summaries = new Dictionary<string, string>();
            var choices = new List<string>();
            foreach (var speech in _document.GetAllDialogueLines())
            {
                summaries[speech.Id] = Summarize(speech);
                choices.Add(speech.Id);
            }
            choices.Add(NewItem);

            string Format(string id)
            {
                if (id == NewItem) return NewItemLabel;
                return summaries.TryGetValue(id, out string summary) ? $"{id} · {summary}" : $"{id} (없는 대사)";
            }
            AddPopup(label, choices, value, Format, id => SetDialogue(column, id, true));
        }

        private void AddQuestPopup(string label, string value, string column)
        {
            var titles = new Dictionary<string, string>();
            var choices = new List<string>();
            foreach (var quest in _outline.Quests)
            {
                string number = quest.Number.ToString(CultureInfo.InvariantCulture);
                titles[number] = quest.Title;
                choices.Add(number);
            }

            string Format(string number)
                => titles.TryGetValue(number, out string title) ? $"{number} · {title}" : $"{number} (없는 퀘스트)";
            AddPopup(label, choices, value, Format, number => _editor.SetCell(_line, column, number));
        }

        private void AddScenarioPopup(string label, string value, string column)
        {
            var titles = new Dictionary<string, string>();
            var choices = new List<string>();
            foreach (var scenario in _outline.Scenarios)
            {
                string number = scenario.Number.ToString(CultureInfo.InvariantCulture);
                titles[number] = scenario.Title;
                choices.Add(number);
            }

            string Format(string number)
                => titles.TryGetValue(number, out string title) ? $"{number} · {title}" : $"{number} (없는 연출)";
            AddPopup(label, choices, value, Format, number => _editor.SetCell(_line, column, number));
        }

        //고른 대화나 대사를 줄에 적는다. "새로 만들기"를 골랐으면 대사 한 줄짜리 새 대화를 만들어서 그것을 적는다
        private void SetDialogue(string column, string id, bool isLine)
        {
            if (id != NewItem)
            {
                _editor.SetCell(_line, column, id);
                return;
            }

            string speaker = string.IsNullOrEmpty(_line.Actor) ? StoryActor.PlayerName : _line.Actor;
            //만든 대사가 고르던 줄보다 위에 끼지 않으므로 줄의 자리는 그대로다 (대사는 Dialogue 표에 적힌다)
            if (_editor.TryAddDialogue(speaker, out var speech))
                _editor.SetCell(_line, column, isLine ? speech.Id : speech.DialogueId);
        }

        private string Summarize(StoryToolDocument.DialogueLine speech)
        {
            string text = speech.Text.Length > SummaryLength ? speech.Text.Substring(0, SummaryLength) + "…" : speech.Text;
            return $"{_document.GetActorName(speech.Speaker)}: {text}";
        }

        /// <summary>
        /// 목록에서 고르는 칸. choices는 표에 적는 값이고 format이 그 값을 보여줄 글로 바꾼다.
        /// 표에 적힌 값이 목록에 없으면(틀린 값) 목록에 넣어서 그대로 보여준다. 빼면 고르지도 않았는데 다른 값으로 보인다
        /// </summary>
        private void AddPopup(string label, List<string> choices, string value, Func<string, string> format, Action<string> onChanged)
        {
            if (!choices.Contains(value))
                choices.Insert(0, value);

            //목록은 /를 하위 메뉴로 나누는 표시로 읽는다. 대사에 /가 있으면 목록이 쪼개지므로 비슷한 글자로 바꿔 띄운다
            string Display(string item) => format(item).Replace('/', '∕');
            var field = new PopupField<string>(label, choices, value, Display, Display);
            field.AddToClassList(ClassField);
            field.RegisterValueChangedCallback(change => onChanged(change.newValue));
            Root.Add(field);
        }
        #endregion

        #region Private - Element
        private void AddLabel(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            Root.Add(label);
        }

        private void AddSection(string title) => AddLabel(title, ClassSection);

        //글을 적는 칸. Enter를 누르거나 다른 곳을 누를 때 표에 적는다. 글자마다 적으면 적는 도중에 화면이 다시 만들어져서 입력이 끊긴다
        private void AddTextField(string label, string value, bool multiline, Action<string> onChanged)
        {
            var field = new TextField(label) { value = value, isDelayed = true, multiline = multiline };
            field.AddToClassList(ClassField);
            field.RegisterValueChangedCallback(change => onChanged(change.newValue));
            Root.Add(field);
        }

        private void AddButton(string text, Action onClicked)
        {
            var button = new Button(onClicked) { text = text };
            button.AddToClassList(ClassButton);
            Root.Add(button);
        }

        //표의 칸을 적힌 그대로 보여준다. 대본의 글이 어느 칸에서 왔는지 확인할 때 본다
        private void RefreshCells()
        {
            if (_cellList == null || _line == null) return;

            _cellList.Clear();
            var table = _document.Tables[_line.TableIndex];
            foreach (string column in table.Columns)
            {
                var cell = new VisualElement();
                cell.AddToClassList(ClassCell);
                var name = new Label(column);
                name.AddToClassList(ClassCellName);
                var value = new Label(table.Get(_line.Row, column));
                value.AddToClassList(ClassCellValue);
                cell.Add(name);
                cell.Add(value);
                _cellList.Add(cell);
            }
        }
        #endregion

        #region Private - Position
        private void ApplyPositionFields(PositionSlot slot)
        {
            var local = slot.IsXOnly ? new Vector2(_xField.value, 0f) : _pointField.value;
            _isApplyingPosition = true;
            _editor.SetPosition(slot, _zoneField.value, local);
            _isApplyingPosition = false;
        }

        //Actor 표의 자리를 쓰던 Spawn에 그 자리를 그대로 옮겨 적는다. 그 뒤로는 이 줄만 따로 옮길 수 있다
        private void WriteOwnPosition()
        {
            if (!_document.TryGetPositionSlot(_line, out var actorSlot) || !_document.TryReadPosition(actorSlot, out long zoneId, out var local)) return;
            if (!_document.TryGetOwnPositionSlot(_line, out var slot)) return;

            _editor.SetPosition(slot, zoneId, local);
            //어느 칸을 고치는지가 바뀌었으므로 입력 칸을 다시 만든다
            Rebuild();
        }
        #endregion
    }
}
