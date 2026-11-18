using System;
using System.Collections.Generic;
using static Common.GameDefine;

namespace StoryTool
{
    /// <summary>
    /// 시나리오와 퀘스트 명령을 툴이 어떻게 다루는지 모아 둔 곳. 명령마다 어느 표에서 쓰는지, 칸을 어떻게 쓰는지, 대본에 띄울 글을 적는다.
    /// 명령이 실제로 하는 일은 게임의 명령 클래스(StoryStep)에 있다. 여기는 표의 칸을 사람이 읽는 글로 바꾸고, 고르는 칸을 알맞게 띄우는 데만 쓴다.
    /// 칸의 쓰임새는 명령 클래스의 Parse와 같아야 한다. 틀리게 적어도 게임은 그대로 돌고, 저장할 때 검사가 걸러낸다.
    /// 새 명령을 만들면 여기에도 한 줄 추가한다. 없는 명령은 표의 칸을 그대로 보여준다
    /// </summary>
    public static class StoryCommandCatalog
    {
        #region Types
        private class Entry
        {
            public string Command { get; set; }
            //어느 표에서 쓰는 명령인지
            public bool InScenario { get; set; }
            public bool InQuest { get; set; }
            public ActorUse Actor { get; set; }
            //Actor 칸에 붙이는 이름. 명령마다 뜻이 다르다 (세울 대상, 대화 상대, 카메라가 볼 대상)
            public string ActorLabel { get; set; } = "대상";
            //위치를 쓰는 명령은 Zone과 Param1이 위치다
            public PositionKind Position { get; set; }
            public ParamSpec Param1 { get; set; }
            public ParamSpec Param2 { get; set; }
            //Param2를 시나리오에서만 쓰는지 (대화의 확대 배율)
            public bool IsParam2ScenarioOnly { get; set; }
            //퀘스트 목표인지. 목표는 화면에 띄울 글(Text)을 적는다
            public bool IsGoal { get; set; }
            public Func<StoryLine, StoryToolDocument, string> Describe { get; set; }
        }
        #endregion

        #region Fields
        //퀘스트가 시나리오를 재생하는 명령. 대본에서는 이 줄 아래에 그 시나리오의 줄을 펼쳐 보여준다
        public const string CommandScenario = "Scenario";
        public const string CommandDialogue = "Dialogue";
        public const string CommandSpawn = "Spawn";
        public const string CommandPlace = "Place";
        public const string CommandMove = "Move";
        public const string CommandDespawn = "Despawn";
        public const string CommandClear = "Clear";
        public const string CommandReach = "Reach";
        public const string CommandLine = "Line";
        public const string CommandCloseDialogue = "CloseDialogue";
        public const string CommandFadeOut = "FadeOut";
        public const string CommandFadeIn = "FadeIn";
        public const string CommandZoom = "Zoom";
        private const string CommandEffect = "Effect";
        private const string CommandWait = "Wait";
        private const string CommandInteract = "Interact";

        //새 줄을 만들었을 때의 명령. 무엇을 넣든 고르기 전에는 뜻이 없으므로, 다른 줄에 영향을 주지 않는 것으로 둔다
        private const string DefaultScenarioCommand = CommandWait;
        private const string DefaultQuestCommand = CommandDialogue;

        //적힌 순서가 명령을 고르는 목록의 순서다
        private static readonly Entry[] EntryList =
        {
            //시나리오 : 대상을 세우고 옮기고 없앤다
            new Entry
            {
                Command = CommandSpawn,
                InScenario = true, Actor = ActorUse.Required, ActorLabel = "세울 대상", Position = PositionKind.OptionalPoint,
                Describe = (line, document) => $"{Name(line, document)} 등장",
            },
            new Entry
            {
                Command = CommandPlace,
                InScenario = true, Actor = ActorUse.Required, Position = PositionKind.Point,
                Describe = (line, document) => $"{Name(line, document)} 바로 옮기기",
            },
            new Entry
            {
                Command = CommandMove,
                InScenario = true, Actor = ActorUse.Required, Position = PositionKind.X,
                Param2 = new ParamSpec(ParamKind.Number, "걸리는 시간 (초, 비우면 걸음 속도)"),
                Describe = (line, document) => $"{Name(line, document)} 걸어가기{Seconds(line.Param2)}",
            },
            new Entry
            {
                Command = CommandDespawn,
                InScenario = true, Actor = ActorUse.Required,
                Describe = (line, document) => $"{Name(line, document)} 퇴장",
            },

            //시나리오 : 대사와 연출
            new Entry
            {
                Command = CommandLine,
                InScenario = true, Actor = ActorUse.Optional, ActorLabel = "대화 상대 (마주 본다)",
                Param1 = new ParamSpec(ParamKind.DialogueLine, "대사"),
                Describe = DescribeLine,
            },
            new Entry { Command = CommandCloseDialogue, InScenario = true, Describe = (_, _) => "말풍선 닫기" },
            new Entry
            {
                Command = CommandEffect,
                InScenario = true, Actor = ActorUse.Required, Param1 = new ParamSpec(ParamKind.Text, "이펙트 이름"),
                Describe = (line, document) => $"{Name(line, document)} 이펙트 {line.Param1}",
            },
            new Entry
            {
                Command = CommandWait,
                InScenario = true, Param1 = new ParamSpec(ParamKind.Number, "시간 (초)", "1"),
                Describe = (line, _) => $"{line.Param1}초 기다리기",
            },
            new Entry { Command = CommandFadeOut, InScenario = true, Describe = (_, _) => "화면 어둡게" },
            new Entry { Command = CommandFadeIn, InScenario = true, Describe = (_, _) => "화면 밝게" },
            new Entry
            {
                Command = CommandZoom,
                InScenario = true, Actor = ActorUse.Optional, ActorLabel = "바라볼 대상 (비우면 처음 위치)",
                Param1 = new ParamSpec(ParamKind.Number, "배율", "1.5"),
                Param2 = new ParamSpec(ParamKind.Number, "걸리는 시간 (초, 비우면 기본값)"),
                Describe = DescribeZoom,
            },

            //시나리오와 퀘스트가 같이 쓴다
            new Entry
            {
                Command = CommandDialogue,
                InScenario = true, InQuest = true, Actor = ActorUse.Optional, ActorLabel = "대화 상대 (마주 본다)",
                Param1 = new ParamSpec(ParamKind.Dialogue, "대화"),
                Param2 = new ParamSpec(ParamKind.Number, "확대 배율 (비우면 카메라 그대로)"), IsParam2ScenarioOnly = true,
                Describe = DescribeDialogue,
            },

            //퀘스트
            new Entry
            {
                Command = CommandScenario,
                InQuest = true, Param1 = new ParamSpec(ParamKind.Scenario, "연출"),
                Describe = (line, _) => $"연출 {line.Param1}",
            },
            new Entry
            {
                Command = CommandInteract,
                InQuest = true, IsGoal = true, Actor = ActorUse.Required, ActorLabel = "말을 걸 Npc",
                Describe = (line, document) => $"목표 : {line.Text} · {Name(line, document)}에게 말 걸기",
            },
            new Entry
            {
                Command = CommandReach,
                InQuest = true, IsGoal = true, Position = PositionKind.Point,
                Param2 = new ParamSpec(ParamKind.Number, "반경 (비우면 말 걸기 거리)"),
                Describe = DescribeReach,
            },
            new Entry
            {
                Command = CommandClear,
                InQuest = true, IsGoal = true, Actor = ActorUse.Optional, ActorLabel = "쓰러뜨릴 적 (비우면 전부)",
                Describe = DescribeClear,
            },
        };
        private static readonly Dictionary<string, Entry> Entries = CreateIndex();

        private static readonly ParamSpec PositionSpec = new(ParamKind.Position, "위치");
        #endregion

        #region Public
        public static PositionKind GetPositionKind(string command)
            => Entries.TryGetValue(command, out var entry) ? entry.Position : PositionKind.None;

        /// <summary>그 표에서 쓸 수 있는 명령. 고르는 목록에 이 순서로 띄운다</summary>
        public static List<string> GetCommands(StoryLineKind kind)
        {
            var commands = new List<string>();
            foreach (var entry in EntryList)
            {
                if (kind == StoryLineKind.Scenario ? entry.InScenario : entry.InQuest)
                    commands.Add(entry.Command);
            }
            return commands;
        }

        public static string GetDefaultCommand(StoryLineKind kind)
            => kind == StoryLineKind.Scenario ? DefaultScenarioCommand : DefaultQuestCommand;

        public static ActorUse GetActorUse(string command)
            => Entries.TryGetValue(command, out var entry) ? entry.Actor : ActorUse.Optional;

        public static string GetActorLabel(string command)
            => Entries.TryGetValue(command, out var entry) ? entry.ActorLabel : "대상";

        public static bool IsGoal(string command) => Entries.TryGetValue(command, out var entry) && entry.IsGoal;

        /// <summary>
        /// 줄의 Param 칸(number는 1 또는 2)을 어떻게 쓰는지. 쓰지 않는 칸이면 Kind가 None이다.
        /// 위치를 쓰는 명령은 Param1이 위치다. 같은 명령도 표에 따라 다를 수 있어서 줄을 받는다 (대화의 확대 배율은 시나리오에서만 쓴다)
        /// </summary>
        public static ParamSpec GetParam(StoryLine line, int number)
        {
            if (!Entries.TryGetValue(line.Command, out var entry)) return default;

            if (number == 1) return entry.Position != PositionKind.None ? PositionSpec : entry.Param1;
            return entry.IsParam2ScenarioOnly && line.Kind != StoryLineKind.Scenario ? default : entry.Param2;
        }

        /// <summary>줄을 대본에 띄울 글로 바꾼다. 번호는 이름으로, 대사 번호는 실제 대사로 보여주고 위치를 쓰는 줄은 어디인지 뒤에 붙인다</summary>
        public static string Describe(StoryLine line, StoryToolDocument document)
            => DescribeAction(line, document) + DescribePosition(line, document);

        /// <summary>줄이 하는 일만 글로 바꾼다. 위치는 붙이지 않는다. 배치 화면의 표식처럼 어디인지가 이미 보이는 곳에 쓴다</summary>
        public static string DescribeAction(StoryLine line, StoryToolDocument document)
        {
            if (line.Kind == StoryLineKind.Actor) return DescribeActor(line, document);
            if (line.Kind == StoryLineKind.Talk) return DescribeTalk(line, document);

            return Entries.TryGetValue(line.Command, out var entry)
                ? entry.Describe(line, document)
                : $"{line.Command} {line.Actor} {line.Param1} {line.Param2}".TrimEnd();
        }

        /// <summary>대사 한 줄을 대본에 띄울 글로 바꾼다 (마을 사람 : "저기요!")</summary>
        public static string DescribeSpeech(StoryToolDocument.DialogueLine speech, StoryToolDocument document)
            => $"{document.GetActorName(speech.Speaker)} : \"{speech.Text}\"";
        #endregion

        #region Private
        private static Dictionary<string, Entry> CreateIndex()
        {
            var index = new Dictionary<string, Entry>();
            foreach (var entry in EntryList)
                index[entry.Command] = entry;
            return index;
        }

        private static string Name(StoryLine line, StoryToolDocument document) => document.GetActorName(line.Actor);

        private static string Seconds(string seconds) => string.IsNullOrEmpty(seconds) ? string.Empty : $" ({seconds}초)";

        private static string DescribeActor(StoryLine line, StoryToolDocument document)
        {
            bool resident = bool.TryParse(line.Get(StoryToolDocument.ColumnResident), out bool value) && value;
            string appear = resident ? "처음부터 서 있음" : "Spawn으로 등장";
            return $"{Name(line, document)} ({line.Get(StoryToolDocument.ColumnType)}) · {appear}";
        }

        //일반 대화는 그 Npc에게 말을 걸면 나온다. 어느 퀘스트가 시작된 뒤부터 하는 대화인지를 같이 보여준다
        private static string DescribeTalk(StoryLine line, StoryToolDocument document)
            => $"{Name(line, document)}에게 말 걸기 · 퀘스트 {line.Get(StoryToolDocument.ColumnQuest)}부터 · 대화 {line.Get(StoryToolDocument.ColumnDialogue)}";

        private static string DescribeLine(StoryLine line, StoryToolDocument document)
            => document.TryGetDialogueLine(line.Param1, out var speech) ? DescribeSpeech(speech, document) : $"대사 {line.Param1}?";

        private static string DescribeDialogue(StoryLine line, StoryToolDocument document)
        {
            string text = $"대화 {line.Param1}";
            if (!string.IsNullOrEmpty(line.Actor))
                text += $" · 상대 {Name(line, document)}";
            if (!string.IsNullOrEmpty(line.Param2))
                text += $" · 확대 {line.Param2}배";
            return text;
        }

        private static string DescribeZoom(StoryLine line, StoryToolDocument document)
        {
            string target = string.IsNullOrEmpty(line.Actor) ? "처음 위치로" : $"{Name(line, document)} 쪽으로";
            return $"카메라 {line.Param1}배 · {target}{Seconds(line.Param2)}";
        }

        private static string DescribeReach(StoryLine line, StoryToolDocument document)
        {
            string radius = string.IsNullOrEmpty(line.Param2) ? StoryToolDocument.FormatNumber(InteractDistance) : line.Param2;
            return $"목표 : {line.Text} · 반경 {radius} 안에 도착";
        }

        private static string DescribeClear(StoryLine line, StoryToolDocument document)
        {
            string target = string.IsNullOrEmpty(line.Actor) ? "적 전부" : Name(line, document);
            return $"목표 : {line.Text} · {target} 처치";
        }

        //위치를 쓰는 줄은 어디인지 뒤에 붙인다. 형식이 틀려서 읽지 못한 위치는 붙이지 않는다 (검사가 에러로 알린다)
        private static string DescribePosition(StoryLine line, StoryToolDocument document)
        {
            if (!document.TryGetPositionSlot(line, out var slot) || !document.TryReadPosition(slot, out long zone, out var local))
                return string.Empty;

            string x = StoryToolDocument.FormatNumber(local.x);
            string position = slot.IsXOnly ? $"x {x}" : $"{x}, {StoryToolDocument.FormatNumber(local.y)}";
            //Actor 줄은 자기 자리를 보여주는 것이라 따로 알리지 않는다
            string source = slot.IsActorTable && line.Kind != StoryLineKind.Actor ? " · Actor 표의 자리" : string.Empty;
            return $" → Zone {zone} ({position}){source}";
        }
        #endregion
    }
}
