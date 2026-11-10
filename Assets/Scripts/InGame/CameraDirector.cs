using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace InGame
{
    /// <summary>
    /// 연출용 카메라 조작. 씬의 메인 카메라를 확대, 축소하고 바라볼 곳으로 옮긴다.
    /// 처음 상태(위치와 화각)를 기억해두고 배율은 그 상태를 1로 본다. 그래서 배율 1로 되돌리면 씬에 놓인 그대로 돌아간다
    /// </summary>
    public class CameraDirector
    {
        #region Fields
        private readonly Camera _camera;
        //씬에 놓인 처음 상태. 배율 1과 바라볼 대상이 없을 때의 기준이다
        private readonly Vector3 _basePosition;
        private readonly float _baseFieldOfView;
        private readonly float _baseOrthographicSize;

        //지금 배율과 바라보는 곳(없으면 처음 위치). 대화처럼 잠깐 바꿨다가 되돌릴 때 이 값을 기억해둔다
        public float CurrentZoom { get; private set; } = 1f;
        public Vector2? CurrentFocus { get; private set; }
        #endregion

        #region Init
        public CameraDirector(Camera camera)
        {
            _camera = camera;
            _basePosition = camera.transform.position;
            _baseFieldOfView = camera.fieldOfView;
            _baseOrthographicSize = camera.orthographicSize;
        }
        #endregion

        #region Public
        /// <summary>
        /// 배율과 바라볼 곳을 duration 동안 바꾼다. 배율이 1보다 크면 확대, 작으면 축소다.
        /// focus가 없으면 처음 위치로 돌아간다. 카메라의 깊이(z)는 바꾸지 않는다.
        /// 이미 그 상태면 기다리지 않고 바로 끝낸다. 연출 구간 끝처럼 "원래대로"를 늘 부르는 곳이 헛되이 시간을 쓰지 않게 한다
        /// </summary>
        public async UniTask Zoom(float zoom, Vector2? focus, float duration, CancellationToken cancellationToken)
        {
            if (Mathf.Approximately(CurrentZoom, zoom) && CurrentFocus == focus) return;

            CurrentFocus = focus;
            float fromZoom = CurrentZoom;
            Vector3 fromPosition = _camera.transform.position;
            Vector3 toPosition = focus.HasValue
                ? new Vector3(focus.Value.x, focus.Value.y, _basePosition.z)
                : _basePosition;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                Apply(Mathf.Lerp(fromZoom, zoom, t), Vector3.Lerp(fromPosition, toPosition, t));
                //오브젝트가 이번 프레임에 움직인 뒤, 그리기 전에 카메라를 맞춘다
                await UniTask.Yield(PlayerLoopTiming.PreLateUpdate, cancellationToken);
                //씬이 내려가며 카메라가 먼저 파괴되었을 수 있다
                if (_camera == null) return;
                elapsed += Time.deltaTime;
            }
            Apply(zoom, toPosition);
        }
        #endregion

        #region Private
        private void Apply(float zoom, Vector3 position)
        {
            CurrentZoom = zoom;
            _camera.transform.position = position;
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
