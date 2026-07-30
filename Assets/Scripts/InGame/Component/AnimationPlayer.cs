using Common;
using Cysharp.Threading.Tasks;
using InGame.Context;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using static Common.GameDefine;
using static Common.AssetKeys;

namespace InGame.Component
{
    public class AnimationPlayer : MonoBehaviour
    {
        private Animator _animator;
        private SpriteRenderer _spriteRenderer;
        private PlayableGraph _graph;
        private AnimationPlayableOutput _output;
        private AnimationClipPlayable _currentPlayable;
        private readonly Dictionary<AnimationType, AnimationClip> _clips = new();
        private AnimationType _current;
        private CancellationTokenSource _animationEndCts;
        private ObjectContext _objectContext;

        public async UniTask Init(ObjectContext objectContext, long characterId)
        {
            _objectContext = objectContext;
            _objectContext.OnDirectionChanged += OnDirectionChanged;
            _objectContext.OnAnimationChanged += Play;

            var animator = GetComponent<Animator>();
            _animator = animator != null ? animator : gameObject.AddComponent<Animator>();
            _animator.applyRootMotion = false;

            var spriteRenderer = GetComponent<SpriteRenderer>();
            _spriteRenderer = spriteRenderer != null ? spriteRenderer : gameObject.AddComponent<SpriteRenderer>();
            _spriteRenderer.flipX = _objectContext.Direction == Direction.Left;

            _graph = PlayableGraph.Create($"AnimGraph_{characterId}");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _output = AnimationPlayableOutput.Create(_graph, "Animation", _animator);

            var assetManager = Global.Instance.AssetManager;
            var loadTasks = new List<UniTask>();
            foreach (AnimationType animType in System.Enum.GetValues(typeof(AnimationType)))
                loadTasks.Add(LoadClip(assetManager, characterId, animType));
            await UniTask.WhenAll(loadTasks);

            _graph.Play();
            Play(AnimationType.Idle);
        }

        private async UniTask LoadClip(AssetManager assetManager, long characterId, AnimationType animType)
        {
            string assetName = $"{characterId}/{animType.ToString().ToLower()}";
            var clip = await assetManager.LoadAssetAsync<AnimationClip>(LoadTarget.Animation, assetName, logOnMissing: false);
            if (clip == null) return;
            _clips[animType] = clip;
        }

        private void OnDirectionChanged(Direction direction)
        {
            _spriteRenderer.flipX = direction == Direction.Left;
        }

        public void Play(AnimationType animType, Action notifyAnimationEnd = null)
        {
            if (!_clips.ContainsKey(animType)) return;

            if (_currentPlayable.IsValid())
                _currentPlayable.Destroy();

            var clip = _clips[animType];

            _animationEndCts?.Cancel();
            _animationEndCts?.Dispose();
            _animationEndCts = null;

            if (!clip.isLooping && notifyAnimationEnd != null)
            {
                _animationEndCts = new CancellationTokenSource();
                WaitForAnimationEnd(clip.length, notifyAnimationEnd, _animationEndCts.Token).Forget();
            }

            _current = animType;
            _currentPlayable = AnimationClipPlayable.Create(_graph, clip);
            _output.SetSourcePlayable(_currentPlayable);
        }

        private async UniTaskVoid WaitForAnimationEnd(float duration, Action notifyAnimationEnd, CancellationToken cancellationToken)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: cancellationToken);
            notifyAnimationEnd?.Invoke();
        }

        private void OnDestroy()
        {
            if (_objectContext != null)
            {
                _objectContext.OnDirectionChanged -= OnDirectionChanged;
                _objectContext.OnAnimationChanged -= Play;
            }

            _animationEndCts?.Cancel();
            _animationEndCts?.Dispose();

            if (_graph.IsValid())
                _graph.Destroy();
        }
    }
}
