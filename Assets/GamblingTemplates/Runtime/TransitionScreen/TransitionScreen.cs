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
        private bool _hasCachedUiState;
        private bool _cachedHideDesktopBetBar;
        private bool _cachedHideMobileBetBar;
        private bool _cachedHideMobileLastWin;
        private bool _cachedHideSettingsMenuButton;

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

            _cachedHideDesktopBetBar = layout.IsDesktopBetBarHidden;
            _cachedHideMobileBetBar = layout.IsMobileBetBarHidden;
            _cachedHideMobileLastWin = layout.IsMobileLastWinHidden;
            _cachedHideSettingsMenuButton = layout.IsSettingsMenuButtonHidden;
            _hasCachedUiState = true;

            layout.SetHideDesktopBetBar(true);
            layout.SetHideMobileBetBar(true);
            layout.SetHideMobileLastWin(true);
            layout.SetHideSettingsMenuButton(true);
        }

        private void RestoreWebUiAfterTransition()
        {
            if (!_hasCachedUiState)
                return;

            LayoutWebBridge layout = LayoutWebBridge.Instance;
            if (layout != null)
            {
                layout.SetHideDesktopBetBar(_cachedHideDesktopBetBar);
                layout.SetHideMobileBetBar(_cachedHideMobileBetBar);
                layout.SetHideMobileLastWin(_cachedHideMobileLastWin);
                layout.SetHideSettingsMenuButton(_cachedHideSettingsMenuButton);
            }

            _hasCachedUiState = false;
        }

        private void OnDisable()
        {
            ResetActiveTransitionState();
        }
    }
}
