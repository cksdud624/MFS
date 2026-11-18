using System;
using System.Collections.Generic;
using System.Globalization;
using InGame.World;
using UnityEditor;
using UnityEngine;
using static Common.GameDefine;

namespace StoryTool
{
    /// <summary>
    /// 배치 화면(Scene 뷰) 위에 그리는 것. Zone 경계, 대상의 이름, 줄이 가리키는 곳의 표식, 고른 줄을 끌어 옮기는 핸들을 그린다.
    /// 무엇을 고칠지는 정하지 않는다. 표식을 누르거나 핸들을 끌면 이벤트로 알리고, 표를 고치는 것은 툴 창이 한다
    /// </summary>
    public class StoryToolSceneGui
    {
        #region Fields
        private static readonly Color ZoneOutlineColor = new(1f, 1f, 1f, 0.35f);
        private static readonly Color MarkerColor = new(1f, 0.85f, 0.3f, 0.9f);
        private static readonly Color SelectedColor = new(0.3f, 1f, 0.6f, 1f);
        private static readonly Color PathColor = new(1f, 1f, 1f, 0.6f);
        private static readonly Color CameraColor = new(0.4f, 0.85f, 1f, 0.9f);
        //화면이 덮여 있을 때(FadeOut) 카메라 범위를 이 색으로 채워서 지금은 아무것도 보이지 않는다는 것을 알린다
        private static readonly Color CoveredColor = new(0f, 0f, 0f, 0.45f);
        private static readonly Color BubbleTextColor = Color.black;
        private static readonly Color BubbleColor = new(0.98f, 0.98f, 0.96f, 1f);
        //말풍선의 가장 넓은 폭 (화면 픽셀). 넘으면 줄을 바꾼다
        private const float BubbleMaxWidth = 260f;
        private const int BubblePadding = 8;
        //표식 크기. 화면에서 늘 같은 크기로 보이게 핸들 크기에 곱한다
        private const float MarkerSize = 0.07f;
        private const float MarkerPickSize = 0.12f;
        private const float PathDashSize = 4f;
        private const float ArrowSize = 0.25f;
        //이름표와 표식 글자를 대상이나 표식에서 띄우는 간격 (화면 픽셀)
        private const float LabelGap = 6f;
        private const float LabelRowGap = 2f;
        //글자가 다른 글자와 겹칠 때 몇 줄까지 옮겨 적어 볼지. 그래도 겹치면 마지막 줄에 겹쳐 적는다
        private const int LabelRows = 6;
        //그림이 없는 대상의 몸통 크기를 모를 때 쓰는 값
        private static readonly Vector2 FallbackBodySize = new(0.7f, 2.3f);

        private GUIStyle _labelStyle;
        private GUIStyle _bubbleStyle;
        //이번에 그린 표식 글자와 이름표의 자리. 가까이 모인 것끼리 겹치지 않게 뒤에 그리는 글자가 이 자리를 피한다
        private readonly List<Rect> _markerLabelRects = new();
        private readonly List<Rect> _nameLabelRects = new();

        private GUIStyle LabelStyle
            => _labelStyle ??= new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter, fontSize = 11, wordWrap = false };

        //표식을 눌렀다. 그 줄을 고르라는 뜻이다
        public event Action<StoryLine> OnLineClicked;
        //핸들을 끌었다. 넘기는 위치는 월드 좌표다
        public event Action<PositionSlot, Vector2> OnPositionDragged;
        #endregion

        #region Public
        /// <summary>lines는 지금 대본에 보이는 줄이고, selected는 그중 고른 줄이다(없으면 null). showCamera면 게임 카메라에 보이는 범위도 그린다</summary>
        public void Draw(StoryToolDocument document, StoryReplay replay, StoryToolStage stage, IReadOnlyList<StoryLine> lines, StoryLine selected,
            bool showCamera)
        {
            //배치 화면의 Zone과 대상은 보여주기용이다. 눌러도 골라지지 않게 기본 선택을 막는다
            if (Event.current.type == EventType.Layout)
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            _markerLabelRects.Clear();
            _nameLabelRects.Clear();
            DrawZones(document.Zones);
            if (showCamera)
                DrawCamera(replay.Camera);
            DrawActors(document, replay, stage);
            foreach (var line in lines)
            {
                if (!replay.TryGetMarker(line, out var marker)) continue;

                if (selected != null && line.Key == selected.Key)
                    DrawSelected(document, line, marker);
                else
                    DrawMarker(document, line, marker);
            }
        }
        #endregion

        #region Private
        private void DrawZones(IReadOnlyList<ZoneLayout.ZoneEntry> zones)
        {
            foreach (var zone in zones)
            {
                var rect = zone.Rect;
                Handles.DrawSolidRectangleWithOutline(rect, Color.clear, ZoneOutlineColor);
                DrawLabel(new Vector2(rect.xMin, rect.yMax), $"Zone {zone.Id}", TextAnchor.UpperLeft, Color.white);
            }
        }

        private void DrawActors(StoryToolDocument document, StoryReplay replay, StoryToolStage stage)
        {
            //말하는 대상의 이름표 자리 (화면 좌표). 말풍선을 그 위에 띄운다
            Rect? speakerRect = null;
            foreach (var pair in replay.Standing)
            {
                long objectId = document.GetActorObjectId(pair.Key);
                var data = document.GetObject(objectId);
                var size = data != null ? data.ColliderSize : FallbackBodySize;
                var offset = data != null ? data.ColliderOffset : new Vector2(0f, FallbackBodySize.y * 0.5f);

                //그림이 없으면 몸통 크기의 틀을 그려서 어디 서 있는지 보이게 한다
                if (!stage.HasSprite(objectId))
                {
                    Handles.color = MarkerColor;
                    Handles.DrawWireCube(pair.Value + offset, size);
                }
                var head = pair.Value + offset + new Vector2(0f, size.y * 0.5f);
                var nameRect = DrawLabel(head, document.GetActorName(pair.Key), TextAnchor.LowerCenter, Color.white);
                if (replay.OpenBubble.IsOpen && replay.OpenBubble.Speaker == pair.Key)
                    speakerRect = nameRect;
            }

            //말풍선은 이름표를 다 그린 뒤에 그린다. 먼저 그리면 뒤에 그리는 대상의 이름표가 말풍선을 덮는다
            if (speakerRect.HasValue)
                DrawBubble(speakerRect.Value, replay.OpenBubble.Text);
        }

        //게임에서 이 순간 화면에 보이는 범위. 확대했으면 배율을, 화면이 덮여 있으면 그 사실을 같이 적는다
        private void DrawCamera(StoryReplay.CameraView camera)
        {
            if (!camera.IsValid) return;

            Handles.DrawSolidRectangleWithOutline(camera.Rect, camera.IsCovered ? CoveredColor : Color.clear, CameraColor);
            string text = "카메라";
            if (!Mathf.Approximately(camera.Zoom, 1f))
                text += $" {StoryToolDocument.FormatNumber(camera.Zoom)}배";
            if (camera.IsCovered)
                text += " · 화면이 덮여 있음";
            DrawLabel(new Vector2(camera.Rect.xMin, camera.Rect.yMax), text, TextAnchor.UpperLeft, CameraColor);
        }

        //말하는 대상의 이름표(nameRect, 화면 좌표) 바로 위에 대사를 띄운다
        private void DrawBubble(Rect nameRect, string text)
        {
            _bubbleStyle ??= CreateBubbleStyle();

            var content = new GUIContent(text);
            float width = Mathf.Min(_bubbleStyle.CalcSize(content).x, BubbleMaxWidth);
            float height = _bubbleStyle.CalcHeight(content, width);
            var rect = new Rect(nameRect.center.x - width * 0.5f, nameRect.yMin - LabelGap - height, width, height);
            //마주 선 대상의 이름표가 위로 밀려 올라와 있을 수 있다. 가리지 않게 그 위로 올린다
            rect = Place(rect, _nameLabelRects, -1f);

            Handles.BeginGUI();
            var previous = GUI.color;
            GUI.color = BubbleColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(rect, content, _bubbleStyle);
            Handles.EndGUI();
        }

        private static GUIStyle CreateBubbleStyle()
        {
            var style = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                //밝은 바탕 위의 가는 글자는 흐리게 보여서 굵게 쓴다
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                padding = new RectOffset(BubblePadding, BubblePadding, BubblePadding, BubblePadding),
            };
            style.normal.textColor = BubbleTextColor;
            return style;
        }

        //고르지 않은 줄의 표식. 누르면 그 줄을 고른다
        private void DrawMarker(StoryToolDocument document, StoryLine line, StoryReplay.Marker marker)
        {
            Handles.color = MarkerColor;
            float handleSize = HandleUtility.GetHandleSize(marker.World);
            if (Handles.Button(marker.World, Quaternion.identity, handleSize * MarkerSize, handleSize * MarkerPickSize, Handles.DotHandleCap))
                OnLineClicked?.Invoke(line);
            DrawLabel(marker.World, StoryCommandCatalog.DescribeAction(line, document), TextAnchor.UpperCenter, MarkerColor);
        }

        //고른 줄의 표식. 끌어서 옮기는 핸들을 같이 그린다
        private void DrawSelected(StoryToolDocument document, StoryLine line, StoryReplay.Marker marker)
        {
            if (!document.TryGetPositionSlot(line, out var slot)) return;

            Handles.color = SelectedColor;
            if (marker.HasOrigin)
                DrawPath(marker.Origin, marker.World);
            if (line.Command == StoryCommandCatalog.CommandReach)
                Handles.DrawWireDisc(marker.World, Vector3.forward, ReadRadius(line));
            DrawLabel(marker.World, StoryCommandCatalog.DescribeAction(line, document), TextAnchor.UpperCenter, SelectedColor);

            EditorGUI.BeginChangeCheck();
            //x만 적는 줄(Move)은 좌우로만 끌 수 있다
            Vector3 moved = slot.IsXOnly
                ? Handles.Slider(marker.World, Vector3.right)
                : Handles.PositionHandle(marker.World, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
                OnPositionDragged?.Invoke(slot, moved);
        }

        //걸어가는 길. 출발한 곳에서 도착할 곳까지 점선과 화살표로 그린다
        private static void DrawPath(Vector2 origin, Vector2 arrival)
        {
            Handles.color = PathColor;
            Handles.DrawDottedLine(origin, arrival, PathDashSize);

            float direction = Mathf.Sign(arrival.x - origin.x);
            float size = HandleUtility.GetHandleSize(arrival) * ArrowSize;
            var back = arrival - new Vector2(direction * size, 0f);
            Handles.DrawLine(arrival, back + new Vector2(0f, size * 0.5f));
            Handles.DrawLine(arrival, back - new Vector2(0f, size * 0.5f));
        }

        //Reach는 반경을 비우면 말 걸기 거리를 쓴다 (ReachStep과 같다)
        private static float ReadRadius(StoryLine line)
            => float.TryParse(line.Param2, NumberStyles.Float, CultureInfo.InvariantCulture, out float radius)
                ? radius
                : InteractDistance;

        /// <summary>
        /// 월드의 한 점에 글자를 붙인다. anchor는 글자 상자의 어느 쪽을 그 점에 맞출지다.
        /// LowerCenter는 점 위에(대상의 이름표), UpperLeft는 점의 오른쪽 아래에(Zone, 카메라), UpperCenter는 점 아래에(표식) 뜬다.
        /// 표식의 글자는 먼저 그린 표식의 글자와 겹치면 한 줄씩 내려 적고, 이름표는 먼저 그린 이름표와 겹치면 한 줄씩 올려 적는다.
        /// 글자를 그린 자리(화면 좌표)를 돌려준다
        /// </summary>
        private Rect DrawLabel(Vector2 world, string text, TextAnchor anchor, Color color)
        {
            var content = new GUIContent(text);
            var size = LabelStyle.CalcSize(content);
            var point = HandleUtility.WorldToGUIPoint(world);
            var rect = new Rect(point.x - size.x * 0.5f, point.y + LabelGap, size.x, size.y);
            if (anchor == TextAnchor.LowerCenter)
            {
                rect.y = point.y - size.y - LabelGap;
                rect = Place(rect, _nameLabelRects, -1f);
            }
            else if (anchor == TextAnchor.UpperLeft)
                rect.position = point + new Vector2(LabelGap, LabelGap);
            else
                rect = Place(rect, _markerLabelRects, 1f);

            Handles.BeginGUI();
            var previous = GUI.contentColor;
            GUI.contentColor = color;
            GUI.Label(rect, content, LabelStyle);
            GUI.contentColor = previous;
            Handles.EndGUI();
            return rect;
        }

        //먼저 놓인 글자(placed)와 겹치면 direction 쪽(1이면 아래, -1이면 위)으로 한 줄씩 옮겨서 놓는다
        private static Rect Place(Rect rect, List<Rect> placed, float direction)
        {
            for (int row = 1; row < LabelRows && Overlaps(rect, placed); row++)
                rect.y += direction * (rect.height + LabelRowGap);
            placed.Add(rect);
            return rect;
        }

        private static bool Overlaps(Rect rect, List<Rect> placed)
        {
            foreach (var other in placed)
            {
                if (other.Overlaps(rect)) return true;
            }
            return false;
        }
        #endregion
    }
}
