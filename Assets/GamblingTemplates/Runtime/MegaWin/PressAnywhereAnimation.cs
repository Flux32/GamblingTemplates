using System.Collections;
using TMPro;
using UnityEngine;

namespace Modules.Road
{
    public class PressAnywhereAnimation
    {
        private readonly MonoBehaviour _coroutineHost;
        private readonly TMP_Text _label;
        private readonly float _minAlpha;
        private readonly float _minScale;
        private readonly float _pulseHalfDuration;

        private Coroutine _pulseRoutine;
        private Vector3 _baseScale = Vector3.one;
        private bool _isBaseScaleCached;

        public PressAnywhereAnimation(
            MonoBehaviour coroutineHost,
            TMP_Text label,
            float minAlpha,
            float minScale,
            float pulseHalfDuration)
        {
            _coroutineHost = coroutineHost;
            _label = label;
            _minAlpha = minAlpha;
            _minScale = minScale;
            _pulseHalfDuration = pulseHalfDuration;
        }

        public void PrepareForShow()
        {
            CacheBaseScale();
            SetVisualState(_minAlpha, _minScale);
        }

        public void StartPulse()
        {
            if (_coroutineHost == null || _label == null)
                return;

            StopPulse();
            _pulseRoutine = _coroutineHost.StartCoroutine(PulseLabel());
        }

        public void StopPulse()
        {
            if (_coroutineHost == null || _pulseRoutine == null)
                return;

            _coroutineHost.StopCoroutine(_pulseRoutine);
            _pulseRoutine = null;
        }

        private IEnumerator PulseLabel()
        {
            float halfDuration = Mathf.Max(0.01f, _pulseHalfDuration);
            float normalizedTime = 0f;

            while (true)
            {
                float t = Mathf.PingPong(normalizedTime, 1f);
                float alpha = Mathf.LerpUnclamped(_minAlpha, 1f, t);
                float scale = Mathf.LerpUnclamped(_minScale, 1f, t);

                SetVisualState(alpha, scale);
                normalizedTime += Time.unscaledDeltaTime / halfDuration;
                yield return null;
            }
        }

        private void SetVisualState(float alpha, float normalizedScale)
        {
            SetLabelAlpha(alpha);
            SetScale(normalizedScale);
        }

        private void SetScale(float normalizedScale)
        {
            if (_label == null)
                return;

            _label.rectTransform.localScale = _baseScale * Mathf.Max(0f, normalizedScale);
        }

        private void CacheBaseScale()
        {
            if (_isBaseScaleCached || _label == null)
                return;

            _baseScale = _label.rectTransform.localScale;
            _isBaseScaleCached = true;
        }

        private void SetLabelAlpha(float alpha)
        {
            if (_label == null)
                return;

            var color = _label.color;
            color.a = Mathf.Clamp01(alpha);
            _label.color = color;
        }
    }
}
