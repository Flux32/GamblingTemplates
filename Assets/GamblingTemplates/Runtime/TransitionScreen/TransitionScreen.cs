using System;
using Modules.Road;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace Modules.Pepe.Art.TransitionScreen
{
    public class TransitionScreen : MonoBehaviour
    {
        [SerializeField] private SkeletonGraphic _transitionScreen;
        [SerializeField] private string _transitionAnimationName;
        [SerializeField, Range(0f, 1f)] private float _needChangeSceneNormalizedTime = 0.5f;

        private TrackEntry _activeTransitionTrack;
        private Action _onTransitionCompleted;
        private Action _onNeedChangeScene;
        private Coroutine _needChangeSceneRoutine;
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
            _activeTransitionTrack = 
                _transitionScreen.AnimationState.SetAnimation(0, _transitionAnimationName, false);
            
            _activeTransitionTrack.MixDuration = 0f;
            
            _activeTransitionTrack.Complete += HandleTransitionFinished;
            _activeTransitionTrack.End += HandleTransitionFinished;

            if (_onNeedChangeScene != null)
                _needChangeSceneRoutine = StartCoroutine(WaitForNeedChangeSceneTime(_activeTransitionTrack));
        }

        private System.Collections.IEnumerator WaitForNeedChangeSceneTime(TrackEntry trackEntry)
        {
            float targetTrackTime = Mathf.Clamp01(_needChangeSceneNormalizedTime) * trackEntry.Animation.Duration;
            while (ReferenceEquals(trackEntry, _activeTransitionTrack) && trackEntry.TrackTime < targetTrackTime)
                yield return null;

            if (ReferenceEquals(trackEntry, _activeTransitionTrack))
                InvokeNeedChangeScene();

            _needChangeSceneRoutine = null;
        }

        private void HandleTransitionFinished(TrackEntry trackEntry)
        {
            if (!ReferenceEquals(trackEntry, _activeTransitionTrack))
                return;

            CompleteTransition();
        }

        private void CompleteTransition()
        {
            if (_activeTransitionTrack == null)
                return;

            _activeTransitionTrack.Complete -= HandleTransitionFinished;
            _activeTransitionTrack.End -= HandleTransitionFinished;
            _activeTransitionTrack = null;

            if (_needChangeSceneRoutine != null)
            {
                StopCoroutine(_needChangeSceneRoutine);
                _needChangeSceneRoutine = null;
            }

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

        private void ResetActiveTransitionState()
        {
            if (_activeTransitionTrack != null)
            {
                _activeTransitionTrack.Complete -= HandleTransitionFinished;
                _activeTransitionTrack.End -= HandleTransitionFinished;
                _activeTransitionTrack = null;
            }

            if (_needChangeSceneRoutine != null)
            {
                StopCoroutine(_needChangeSceneRoutine);
                _needChangeSceneRoutine = null;
            }

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
