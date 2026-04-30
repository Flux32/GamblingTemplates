using System;
using Modules.Road;
using Spine;
using Spine.Unity;
using UnityEngine;
using AnimationState = Spine.AnimationState;

namespace Modules.GamblingTemplates.GamblingTemplates.Runtime.TransitionScreen
{
    public class TransitionScreen : MonoBehaviour
    {
        [SerializeField] private SkeletonGraphic _transitionScreen;

        [SerializeField, SpineAnimation] private string _transitionStartAnimationName;
        [SerializeField, SpineAnimation] private string _transitionIdleAnimationName;
        [SerializeField, SpineAnimation] private string _transitionEndAnimationName;

        private TrackEntry _idleTrack;
        private TrackEntry _endTrack;
        private Action _onTransitionCompleted;
        private Action _onNeedChangeScene;
        private bool _isNeedChangeSceneInvoked;
        private bool _hasHiddenWebUi;

        public void Transit()
        {
            Transit(onCompleted: null, onNeedChangeScene: null);
        }

        public void Transit(Action onCompleted)
        {
            Transit(onCompleted, onNeedChangeScene: null);
        }

        public void Transit(Action onCompleted, Action onNeedChangeScene)
        {
            ResetActiveTransitionState();
            HideWebUiDuringTransition();
            _transitionScreen.gameObject.SetActive(true);
            _onTransitionCompleted = onCompleted;
            _onNeedChangeScene = onNeedChangeScene;

            AnimationState animationState = _transitionScreen.AnimationState;

            TrackEntry startTrack = animationState.SetAnimation(0, _transitionStartAnimationName, false);
            startTrack.MixDuration = 0f;

            _idleTrack = animationState.AddAnimation(0, _transitionIdleAnimationName, false, 0f);
            _idleTrack.MixDuration = 0f;
            _idleTrack.Start += HandleIdleStarted;

            _endTrack = animationState.AddAnimation(0, _transitionEndAnimationName, false, 0f);
            _endTrack.MixDuration = 0f;
            _endTrack.Complete += HandleTransitionFinished;
            _endTrack.End += HandleTransitionFinished;
        }

        private void HandleIdleStarted(TrackEntry trackEntry)
        {
            if (!ReferenceEquals(trackEntry, _idleTrack))
                return;

            _idleTrack.Start -= HandleIdleStarted;
            InvokeNeedChangeScene();
        }

        private void HandleTransitionFinished(TrackEntry trackEntry)
        {
            if (!ReferenceEquals(trackEntry, _endTrack))
                return;

            CompleteTransition();
        }

        private void CompleteTransition()
        {
            ClearTrackHandlers();

            InvokeNeedChangeScene();
            _transitionScreen.gameObject.SetActive(false);
            RestoreWebUiAfterTransition();
            _onTransitionCompleted?.Invoke();
            _onTransitionCompleted = null;
            _onNeedChangeScene = null;
        }

        private void InvokeNeedChangeScene()
        {
            if (_isNeedChangeSceneInvoked)
                return;

            _isNeedChangeSceneInvoked = true;
            _onNeedChangeScene?.Invoke();
        }

        private void ClearTrackHandlers()
        {
            if (_idleTrack != null)
            {
                _idleTrack.Start -= HandleIdleStarted;
                _idleTrack = null;
            }

            if (_endTrack != null)
            {
                _endTrack.Complete -= HandleTransitionFinished;
                _endTrack.End -= HandleTransitionFinished;
                _endTrack = null;
            }
        }

        private void ResetActiveTransitionState()
        {
            ClearTrackHandlers();

            _isNeedChangeSceneInvoked = false;
            _onTransitionCompleted = null;
            _onNeedChangeScene = null;
            RestoreWebUiAfterTransition();
        }

        private void HideWebUiDuringTransition()
        {
            LayoutWebBridge layout = LayoutWebBridge.Instance;
            if (layout == null)
                return;

            _hasHiddenWebUi = true;

            layout.SetHideDesktopBetBar(true);
            layout.SetHideMobileBetBar(true);
            layout.SetHideMobileLastWin(true);
            layout.SetHideSettingsMenuButton(true);
        }

        private void RestoreWebUiAfterTransition()
        {
            if (!_hasHiddenWebUi)
                return;

            _hasHiddenWebUi = false;

            LayoutWebBridge layout = LayoutWebBridge.Instance;
            if (layout == null)
                return;

            layout.SetHideDesktopBetBar(false);
            layout.SetHideMobileBetBar(false);
            layout.SetHideMobileLastWin(false);
            layout.SetHideSettingsMenuButton(false);
        }

        private void OnDisable()
        {
            ResetActiveTransitionState();
        }
    }
}
