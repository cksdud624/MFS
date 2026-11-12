using System;
using System.Threading;
using Common;
using Common.Template.Interface;
using Cysharp.Threading.Tasks;
using InGame.Context;
using InGame.World;
using UnityEngine;
using static Common.GameDefine;

namespace InGame
{
    /// <summary>
    /// 게임 카메라를 움직이는 유일한 곳. 평소에는 플레이어를 따라가고, 연출(시나리오 Zoom, 대화 확대) 동안에는 바라볼 곳으로 옮긴다.
    /// 위치를 바꾸는 곳이 둘이면 서로 덮어쓰므로 따라가기와 연출을 한 클래스에 둔다.
    /// 카메라 화면은 Zone 밖을 보여주지 않는다. 플레이어(또는 바라볼 곳)가 있는 Zone의 옆에 Zone이 없으면 그쪽 끝에서 멈춘다.
    /// 배율은 씬에 놓인 처음 화각을 1로 본다
    /// </summary>
    public class CameraDirector : ILateUpdateable, IDisposable
    {
        #region Fields
        private readonly Camera _camera;
        private readonly InGameContext _inGameContext;
        private readonly ZoneLayout _zoneLayout;
        //지금 맵을 알려주는 쪽. Zone 끝을 찾을 때 어느 맵의 Zone인지 알아야 한다
        private readonly ZoneStreamer _zoneStreamer;

        //씬에 놓인 처음 상태. 플레이어가 아직 없을 때의 자리이고, 배율 1의 기준이다
        private readonly Vector3 _basePosition;
        private readonly float _baseFieldOfView;
        private readonly float _baseOrthographicSize;

        //지금 배율과 바라보는 곳. 바라보는 곳이 없으면 플레이어를 따라간다. 대화처럼 잠깐 바꿨다가 되돌릴 때 이 값을 기억해둔다
        public float CurrentZoom { get; private set; } = 1f;
        public Vector2? CurrentFocus { get; private set; }

        //연출로 옮겨가는 중인지. 이 동안은 따라가지 않는다. 같이 움직이면 연출 위치와 따라가는 위치가 매 프레임 서로 덮어쓴다
        private bool _isMoving;
        //부드럽게 따라갈 때 SmoothDamp가 쓰는 속도
        private Vector2 _followVelocity;
        //플레이어를 처음 잡았는지. 처음에는 부드럽게 가지 않고 바로 맞춘다. 시작 위치에서 미끄러져 오는 모습이 보이지 않게 한다
        private bool _hasFollowed;
        #endregion

        #region Init
        public CameraDirector(Camera camera, InGameContext inGameContext, ZoneLayout zoneLayout, ZoneStreamer zoneStreamer)
        {
            _camera = camera;
            _inGameContext = inGameContext;
            _zoneLayout = zoneLayout;
            _zoneStreamer = zoneStreamer;
            _basePosition = camera.transform.position;
            _baseFieldOfView = camera.fieldOfView;
            _baseOrthographicSize = camera.orthographicSize;

            Global.Instance.BindLateUpdate(this);
        }
        #endregion

        #region LifeCycle
        //오브젝트가 이번 프레임에 움직인 뒤, 그리기 전에 카메라를 맞춘다
        public void OnLateUpdate()
        {
            if (_camera == null || _isMoving || CurrentFocus.HasValue) return;
            if (!TryGetFollowPosition(out var target)) return;

            if (!_hasFollowed)
            {
                _hasFollowed = true;
                SetPosition(target);
                return;
            }
            Vector2 current = _camera.transform.position;
            SetPosition(Vector2.SmoothDamp(current, target, ref _followVelocity, CameraFollowSmoothTime));
        }
        #endregion

        #region Public
        /// <summary>
        /// 배율과 바라볼 곳을 duration 동안 바꾼다. 배율이 1보다 크면 확대, 작으면 축소다.
        /// focus가 없으면 플레이어를 따라가는 자리로 돌아가서 다시 따라간다. 카메라의 깊이(z)는 바꾸지 않는다.
        /// 이미 그 상태면 기다리지 않고 바로 끝낸다. 연출 구간 끝처럼 "원래대로"를 늘 부르는 곳이 헛되이 시간을 쓰지 않게 한다
        /// </summary>
        public async UniTask Zoom(float zoom, Vector2? focus, float duration, CancellationToken cancellationToken)
        {
            if (Mathf.Approximately(CurrentZoom, zoom) && CurrentFocus == focus) return;

            CurrentFocus = focus;
            float fromZoom = CurrentZoom;
            Vector2 fromPosition = _camera.transform.position;
            //바라볼 곳도 Zone 밖이 보이지 않게 맞춘다. 확대한 뒤의 화면 크기로 맞춰야 해서 바뀐 배율을 넘긴다
            Vector2 toPosition = focus.HasValue
                ? Clamp(focus.Value, zoom)
                : TryGetFollowPosition(zoom, out var follow) ? follow : (Vector2)_basePosition;

            _isMoving = true;
            try
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                    ApplyZoom(Mathf.Lerp(fromZoom, zoom, t));
                    SetPosition(Vector2.Lerp(fromPosition, toPosition, t));
                    await UniTask.Yield(PlayerLoopTiming.PreLateUpdate, cancellationToken);
                    //씬이 내려가며 카메라가 먼저 파괴되었을 수 있다
                    if (_camera == null) return;
                    elapsed += Time.deltaTime;
                }
                ApplyZoom(zoom);
                SetPosition(toPosition);
            }
            finally
            {
                _isMoving = false;
                //따라가기로 돌아올 때 남아 있던 속도로 튀지 않게 한다
                _followVelocity = Vector2.zero;
            }
        }

        public void Dispose() => Global.Instance?.UnBindLateUpdate(this);
        #endregion

        #region Private
        private bool TryGetFollowPosition(out Vector2 position) => TryGetFollowPosition(CurrentZoom, out position);

        //플레이어 발밑에서 CameraFollowHeight만큼 위를 화면 가운데로 둔다. 플레이어가 없으면(도입 연출 전) 따라가지 않는다
        private bool TryGetFollowPosition(float zoom, out Vector2 position)
        {
            position = default;
            var player = _inGameContext.Player;
            if (player == null) return false;

            Vector2 feet = player.transform.position;
            position = Clamp(feet + new Vector2(0f, CameraFollowHeight), zoom, feet);
            return true;
        }

        private Vector2 Clamp(Vector2 center, float zoom) => Clamp(center, zoom, center);

        /// <summary>
        /// 화면이 Zone 밖을 보여주지 않게 카메라 가운데를 맞춘다. 기준은 anchor(플레이어 발밑이나 바라볼 곳)가 있는 Zone이다.
        /// 그 Zone의 옆에 Zone이 있으면 그쪽으로는 막지 않는다. 화면이 Zone보다 크면 그 축은 Zone 가운데에 둔다.
        /// 격자 밖이면 막지 않는다
        /// </summary>
        private Vector2 Clamp(Vector2 center, float zoom, Vector2 anchor)
        {
            if (!_zoneLayout.TryFindZone(_zoneStreamer.MapId, anchor, out var zone)) return center;

            var (min, max) = _zoneLayout.GetBoundary(zone);
            var halfView = GetHalfView(zoom);
            return new Vector2(ClampAxis(center.x, min.x, max.x, halfView.x), ClampAxis(center.y, min.y, max.y, halfView.y));
        }

        //막힌 쪽이 없으면 limit이 무한대라 그대로 통과한다
        private static float ClampAxis(float center, float min, float max, float half)
        {
            float low = min + half;
            float high = max - half;
            if (low > high) return (min + max) * 0.5f;
            return Mathf.Clamp(center, low, high);
        }

        //배율 zoom일 때 카메라가 게임 평면(z = 0)에서 보는 영역의 절반 크기
        private Vector2 GetHalfView(float zoom)
        {
            float halfHeight = _camera.orthographic
                ? _baseOrthographicSize
                : Mathf.Abs(_basePosition.z) * Mathf.Tan(_baseFieldOfView * 0.5f * Mathf.Deg2Rad);
            halfHeight /= zoom;
            return new Vector2(halfHeight * _camera.aspect, halfHeight);
        }

        private void SetPosition(Vector2 position) => _camera.transform.position = new Vector3(position.x, position.y, _basePosition.z);

        private void ApplyZoom(float zoom)
        {
            CurrentZoom = zoom;
            //직교 카메라는 보이는 높이가, 원근 카메라는 화각의 탄젠트가 배율에 반비례한다
            if (_camera.orthographic)
            {
                _camera.orthographicSize = _baseOrthographicSize / zoom;
                return;
            }
            float baseHalfTan = Mathf.Tan(_baseFieldOfView * 0.5f * Mathf.Deg2Rad);
            _camera.fieldOfView = 2f * Mathf.Atan(baseHalfTan / zoom) * Mathf.Rad2Deg;
        }
        #endregion
    }
}
