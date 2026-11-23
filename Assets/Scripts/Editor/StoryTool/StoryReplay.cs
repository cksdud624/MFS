using System.Collections.Generic;
using System.Globalization;
using InGame;
using InGame.Story;
using UnityEngine;
using static Common.GameDefine;

namespace StoryTool
{
    /// <summary>
    /// 이야기를 처음부터 표로만 따라가서, 줄마다 가리키는 곳과 고른 줄까지 진행했을 때의 모습을 계산한다.
    /// 모습은 누가 어디에 서 있는지, 떠 있는 말풍선, 게임 카메라에 보이는 범위다.
    /// 게임이 진행하는 순서와 같다. 상주 대상을 세우고, 퀘스트를 번호 순으로, 퀘스트가 부르는 시나리오는 그 자리에서 펼쳐서 따라간다.
    /// Play 없이 표만 읽으므로 물리(낙하, 밀림)와 전투는 반영하지 않는다. 대상은 표에 적힌 자리에 그대로 서 있는 것으로 본다
    /// </summary>
    public class StoryReplay
    {
        #region Types
        /// <summary>줄이 가리키는 곳. 걸어가는 줄(Move)은 걷기 시작한 곳도 같이 든다</summary>
        public readonly struct Marker
        {
            public Vector2 World { get; }
            public bool HasOrigin { get; }
            public Vector2 Origin { get; }

            public Marker(Vector2 world)
            {
                World = world;
                HasOrigin = false;
                Origin = world;
            }

            public Marker(Vector2 world, Vector2 origin)
            {
                World = world;
                HasOrigin = true;
                Origin = origin;
            }
        }

        /// <summary>떠 있는 말풍선. Speaker는 표에 적힌 그대로다 (Player 또는 Actor 번호)</summary>
        public readonly struct Bubble
        {
            public bool IsOpen { get; }
            public string Speaker { get; }
            public string Text { get; }

            public Bubble(StoryToolDocument.DialogueLine speech)
            {
                IsOpen = true;
                Speaker = speech.Speaker;
                Text = speech.Text;
            }
        }

        /// <summary>게임 카메라에 보이는 범위</summary>
        public readonly struct CameraView
        {
            //Main 씬에서 카메라를 읽지 못했으면 false다
            public bool IsValid { get; }
            public Rect Rect { get; }
            public float Zoom { get; }
            //FadeOut으로 화면이 덮여 있는지
            public bool IsCovered { get; }

            public CameraView(Rect rect, float zoom, bool isCovered)
            {
                IsValid = true;
                Rect = rect;
                Zoom = zoom;
                IsCovered = isCovered;
            }
        }
        #endregion

        #region Fields
        private const float DefaultZoom = 1f;

        private readonly StoryToolDocument _document;
        //고른 줄의 키. 이 줄까지 진행한 모습을 남긴다
        private readonly string _targetKey;
        //고른 줄이 대화일 때 몇 번째 대사를 하는 중으로 볼지
        private readonly int _speechIndex;
        //고른 줄이 일반 대화(Talk)면 그 대화의 번호. 아니면 null
        private readonly string _talkDialogueId;
        //줄(키) → 그 줄이 가리키는 곳. 위치를 쓰는 줄만 들어 있다
        private readonly Dictionary<string, Marker> _markers = new();
        //고른 줄까지 진행했을 때 월드에 있는 대상 → 서 있는 곳
        private readonly Dictionary<string, Vector2> _standing = new();
        private bool _isCaptured;

        //따라가는 동안 바뀌는 연출 상태. 시나리오가 끝나면 처음으로 돌아간다
        private float _zoom = DefaultZoom;
        //카메라가 바라보는 곳. 없으면 플레이어를 따라간다
        private Vector2? _focus;
        private bool _isCovered;
        private Bubble _bubble;

        public IReadOnlyDictionary<string, Vector2> Standing => _standing;
        public Bubble OpenBubble { get; private set; }
        public CameraView Camera { get; private set; }
        #endregion

        #region Init
        public StoryReplay(StoryToolDocument document, StoryOutline outline, StoryDoc doc, StoryLine target, int speechIndex)
        {
            _document = document;
            _speechIndex = speechIndex;
            if (doc.Kind == StoryLineKind.Actor)
            {
                PlaceActors(doc);
                return;
            }

            _targetKey = target?.Key;
            if (target != null && target.Kind == StoryLineKind.Talk)
            {
                //일반 대화는 이야기의 한 줄이 아니라 그 퀘스트가 시작된 뒤로 언제든 할 수 있는 대화다.
                //할 수 있게 되는 때(그 퀘스트의 첫 줄을 지난 모습)를 보여주고 말풍선에 그 대화의 대사를 띄운다
                _talkDialogueId = target.Get(StoryToolDocument.ColumnDialogue);
                var quest = outline.Find(StoryLineKind.Quest, target.Get(StoryToolDocument.ColumnQuest));
                _targetKey = quest != null && quest.Lines.Count > 0 ? quest.Lines[0].Key : null;
            }
            FollowStory(outline);
        }
        #endregion

        #region Public
        public bool TryGetMarker(StoryLine line, out Marker marker) => _markers.TryGetValue(line.Key, out marker);
        #endregion

        #region Private - Follow
        //대상 묶음은 이야기의 어느 시점도 아니다. 그 챕터의 대상을 모두 Actor 표의 자리에 세워서 한눈에 보여준다
        private void PlaceActors(StoryDoc doc)
        {
            foreach (var line in doc.Lines)
            {
                if (!TryReadWorld(line, out var world)) continue;
                _standing[line.Actor] = world;
                _markers[line.Key] = new Marker(world);
            }
        }

        private void FollowStory(StoryOutline outline)
        {
            var world = new Dictionary<string, Vector2>();
            PlaceResidents(outline, world);

            var playedScenarios = new HashSet<long>();
            foreach (var quest in outline.Quests)
            {
                foreach (var line in quest.Lines)
                {
                    //시나리오를 부르는 줄은 그 시나리오가 다 끝난 모습이 그 줄의 모습이다
                    var scenario = line.Command == StoryCommandCatalog.CommandScenario ? outline.Find(StoryLineKind.Scenario, line.Param1) : null;
                    if (scenario != null)
                    {
                        playedScenarios.Add(scenario.Number);
                        PlayScenario(world, scenario);
                    }
                    Apply(world, line);
                }
            }

            //어느 퀘스트도 부르지 않는 시나리오는 언제 재생될지 알 수 없다. 상주 대상만 있는 처음 상태에서 시작한 것으로 본다
            foreach (var scenario in outline.Scenarios)
            {
                if (playedScenarios.Contains(scenario.Number)) continue;

                world.Clear();
                PlaceResidents(outline, world);
                PlayScenario(world, scenario);
            }
        }

        private void PlaceResidents(StoryOutline outline, Dictionary<string, Vector2> world)
        {
            foreach (var line in outline.Actors.Lines)
            {
                bool resident = bool.TryParse(line.Get(StoryToolDocument.ColumnResident), out bool value) && value;
                if (resident && TryReadWorld(line, out var position))
                    world[line.Actor] = position;
            }
        }

        //시나리오가 끝나면 열어둔 말풍선을 닫고 카메라를 처음 상태로 되돌린다 (PlayScenarioStep과 같다)
        private void PlayScenario(Dictionary<string, Vector2> world, StoryDoc scenario)
        {
            foreach (var line in scenario.Lines)
                Apply(world, line);

            _bubble = default;
            _zoom = DefaultZoom;
            _focus = null;
        }

        //줄 하나를 진행한다. 고른 줄이면 진행한 직후의 모습을 남긴다
        private void Apply(Dictionary<string, Vector2> world, StoryLine line)
        {
            string actor = line.Actor;
            switch (line.Command)
            {
                case StoryCommandCatalog.CommandSpawn:
                case StoryCommandCatalog.CommandPlace:
                    if (TryReadWorld(line, out var position))
                    {
                        world[actor] = position;
                        AddMarker(line, new Marker(position));
                    }
                    break;
                case StoryCommandCatalog.CommandMove:
                    //x만 적으므로 높이는 대상이 서 있던 높이다. 아직 월드에 없는 대상이면 어디인지 정할 수 없다
                    if (world.TryGetValue(actor, out var origin) && TryReadWorld(line, out var target))
                    {
                        var arrival = new Vector2(target.x, origin.y);
                        world[actor] = arrival;
                        AddMarker(line, new Marker(arrival, origin));
                    }
                    break;
                case StoryCommandCatalog.CommandReach:
                    if (TryReadWorld(line, out var destination))
                        AddMarker(line, new Marker(destination));
                    break;
                case StoryCommandCatalog.CommandDespawn:
                    world.Remove(actor);
                    break;
                case StoryCommandCatalog.CommandClear:
                //보스전도 통과하면 보스가 쓰러져서 나간다
                case StoryCommandCatalog.CommandBoss:
                    Clear(world, actor);
                    break;
                case StoryCommandCatalog.CommandFadeOut:
                    _isCovered = true;
                    break;
                case StoryCommandCatalog.CommandFadeIn:
                    _isCovered = false;
                    break;
                case StoryCommandCatalog.CommandZoom:
                    //대상을 적으면 그때 대상의 몸통을 본다. 그 뒤에 대상이 움직여도 카메라는 따라가지 않는다. 비우면 플레이어를 따라가는 자리로 돌아간다
                    if (TryParseFloat(line.Param1, out float zoom) && zoom > 0f)
                        _zoom = zoom;
                    _focus = TryGetCenter(world, actor, out var center) ? center : null;
                    break;
                case StoryCommandCatalog.CommandLine:
                    //닫지 않는다. 뒤의 연출 동안 떠 있다가 CloseDialogue, 대화 하나, 시나리오 끝에서 닫힌다
                    if (_document.TryGetDialogueLine(line.Param1, out var speech))
                        _bubble = new Bubble(speech);
                    break;
                case StoryCommandCatalog.CommandCloseDialogue:
                    _bubble = default;
                    break;
                case StoryCommandCatalog.CommandDialogue:
                    if (IsTarget(line))
                        CaptureDialogue(world, line);
                    //대화 하나는 끝나면 말풍선을 닫는다. 앞에서 Line으로 열어둔 것도 같이 닫힌다
                    _bubble = default;
                    break;
            }

            if (IsTarget(line))
                Capture(world, _bubble, _zoom, _focus);
        }

        //대상을 적으면 그 적만, 비우면 월드의 적 전부가 쓰러져서 나간다
        private void Clear(Dictionary<string, Vector2> world, string actor)
        {
            if (!string.IsNullOrEmpty(actor))
            {
                world.Remove(actor);
                return;
            }

            var enemies = new List<string>();
            foreach (var standing in world.Keys)
            {
                if (_document.GetActorType(standing) == StoryActorType.Enemy)
                    enemies.Add(standing);
            }
            foreach (var enemy in enemies)
                world.Remove(enemy);
        }
        #endregion

        #region Private - Capture
        //같은 시나리오를 두 번 재생하면 처음 재생했을 때의 모습을 남긴다
        private bool IsTarget(StoryLine line) => !_isCaptured && line.Key == _targetKey;

        /// <summary>
        /// 대화는 끝난 뒤가 아니라 하는 중의 모습을 남긴다. 끝나면 말풍선이 닫히고 카메라가 돌아가서 볼 것이 없다.
        /// 시나리오의 대화는 배율을 적으면 대화 상대(없으면 플레이어)를 확대한다 (DialogueStep과 같다)
        /// </summary>
        private void CaptureDialogue(Dictionary<string, Vector2> world, StoryLine line)
        {
            var bubble = GetSpeechBubble(line.Param1);

            float zoom = _zoom;
            var focus = _focus;
            if (line.Kind == StoryLineKind.Scenario && TryParseFloat(line.Param2, out float dialogueZoom) && dialogueZoom > 0f)
            {
                string partner = string.IsNullOrEmpty(line.Actor) ? StoryActor.PlayerName : line.Actor;
                if (TryGetCenter(world, partner, out var center))
                {
                    zoom = dialogueZoom;
                    focus = center;
                }
            }
            Capture(world, bubble, zoom, focus);
        }

        private void Capture(Dictionary<string, Vector2> world, Bubble bubble, float zoom, Vector2? focus)
        {
            _isCaptured = true;
            foreach (var pair in world)
                _standing[pair.Key] = pair.Value;
            OpenBubble = _talkDialogueId != null ? GetSpeechBubble(_talkDialogueId) : bubble;
            Camera = BuildCamera(world, zoom, focus);
        }

        //대화에서 지금 하는 중으로 보는 대사(_speechIndex)의 말풍선. 대사가 없는 대화면 닫힌 말풍선이다
        private Bubble GetSpeechBubble(string dialogueId)
        {
            var speeches = _document.GetDialogue(dialogueId);
            return speeches.Count > 0 ? new Bubble(speeches[Mathf.Clamp(_speechIndex, 0, speeches.Count - 1)]) : default;
        }

        /// <summary>
        /// 게임 카메라가 그 순간 보는 범위. 바라보는 곳이 없으면 플레이어 발밑에서 CameraFollowHeight만큼 위를 가운데로 두고,
        /// 플레이어도 없으면 씬에 놓인 자리에 있다. 화면은 Zone 밖을 보여주지 않는다 (CameraDirector와 같다)
        /// </summary>
        private CameraView BuildCamera(Dictionary<string, Vector2> world, float zoom, Vector2? focus)
        {
            var camera = _document.GameCamera;
            if (!camera.IsValid) return default;

            var halfView = camera.GetHalfView(zoom);
            Vector2 center;
            if (focus.HasValue)
                center = ClampToZone(focus.Value, halfView, focus.Value);
            else if (world.TryGetValue(StoryActor.PlayerName, out var feet))
                center = ClampToZone(feet + new Vector2(0f, CameraFollowHeight), halfView, feet);
            else
                center = camera.Position;
            return new CameraView(new Rect(center - halfView, halfView * 2f), zoom, _isCovered);
        }

        //기준(anchor)이 있는 Zone의 끝을 넘지 않게 맞춘다. 격자 밖이면 막지 않는다
        private Vector2 ClampToZone(Vector2 center, Vector2 halfView, Vector2 anchor)
            => _document.TryFindZoneAt(anchor, out var zone)
                ? CameraDirector.ClampToZone(_document.ZoneLayout, zone, center, halfView)
                : center;
        #endregion

        #region Private - Read
        private void AddMarker(StoryLine line, Marker marker) => _markers.TryAdd(line.Key, marker);

        private bool TryReadWorld(StoryLine line, out Vector2 world)
        {
            world = Vector2.zero;
            if (!_document.TryGetPositionSlot(line, out var slot) || !_document.TryReadPosition(slot, out long zone, out var local))
                return false;

            world = _document.ZoneLayout.ToWorld(zone, local);
            return true;
        }

        //대상의 몸통 가운데. 월드에 없는 대상이면 false
        private bool TryGetCenter(Dictionary<string, Vector2> world, string actor, out Vector2 center)
        {
            center = Vector2.zero;
            if (string.IsNullOrEmpty(actor) || !world.TryGetValue(actor, out var feet)) return false;

            center = feet + _document.GetActorCenterOffset(actor);
            return true;
        }

        //표의 숫자는 항상 소수점을 .으로 적으므로 PC 지역 설정과 상관없이 읽는다
        private static bool TryParseFloat(string raw, out float value)
            => float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        #endregion
    }
}
