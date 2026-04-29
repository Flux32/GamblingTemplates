using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using Attributes.Source.Infrastructure.Inspector;
using Modules.Road;
using Spine;
using Spine.Unity;
using TMPro;
using UnityEngine;
using AnimationState = Spine.AnimationState;

namespace Modules.GamblingTemplates.GamblingTemplates.Runtime.Cashout
{
    public class CashOutWindow : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private SkeletonGraphic _skeletonAnimation;
        [SerializeField] private TMP_Text _cashoutValue;

        [Header("Animations")]
        [SerializeField, SpineAnimation] private string _openStartAnimationName;
        [SerializeField, SpineAnimation] private string _openIndleAnimationName;
        [SerializeField, SpineAnimation] private string _closeAnimationName;
        [SerializeField] private float _openTimeScale = 2f;
        [SerializeField] private float _idleTimeScale = 2f;

        [Header("Timings")]
        [SerializeField, Min(0f)] private float _openDuration = 0.35f;
        [SerializeField, Min(0f)] private float _valueAnimDuration = 0.6f;
        [SerializeField, Min(0f)] private float _idleDuration = 1f;
        [SerializeField, Min(0f)] private float _closeDuration = 0.25f;

        [Header("Audio")]
        [SerializeField, WebBridgeSound] private string _cashoutSound;

        private Coroutine _sequenceRoutine;
        private float _targetValue;
        private string _valuePrefix = "";
        private string _valueSuffix = "$";
        private int _valueDecimals;
        private bool _canAnimateValue;

        private void Awake()
        {
            SetCanvasAlpha(0f);
            _skeletonAnimation.gameObject.SetActive(false);
        }

        public void SetValue(float amount)
        {
            _targetValue = amount;
            _valuePrefix = "";
            _valueSuffix = "$";
            _valueDecimals = AmountTextUtility.GetDecimalPlaces(amount);
            _canAnimateValue = true;
            _cashoutValue.text = AmountTextUtility.FormatAmount(_targetValue, _valuePrefix, _valueSuffix, _valueDecimals);
        }

        public void SetValue(string amount)
        {
            _cashoutValue.text = amount;
            if (AmountTextUtility.TryParseAmount(amount, out float value, out string prefix, out string suffix, out int decimals))
            {
                _targetValue = value;
                _valuePrefix = prefix;
                _valueSuffix = suffix;
                _valueDecimals = decimals;
                _canAnimateValue = true;
            }
            else
            {
                _canAnimateValue = false;
            }
        }

        [Button]
        public void Show()
        {
            StopSequence();
            AudioWebBridge.Instance.PlaySound(_cashoutSound);
            _sequenceRoutine = StartCoroutine(OpenSequence());
        }
        
        private IEnumerator OpenSequence()
        {
            _skeletonAnimation.AnimationState.ClearTracks();
            _skeletonAnimation.gameObject.SetActive(false);

            yield return FadeCanvas(_canvasGroup.alpha, 1f, _openDuration);

            _skeletonAnimation.gameObject.SetActive(true);
            AnimationState animationState = _skeletonAnimation.AnimationState;

            TrackEntry openAnim = animationState.SetAnimation(0, _openStartAnimationName, false);
            openAnim.MixDuration = 0f;
            openAnim.TimeScale = _openTimeScale;

            TrackEntry idleAnim = animationState.AddAnimation(0, _openIndleAnimationName, true, 0);
            idleAnim.MixDuration = 0f;
            idleAnim.TimeScale = _idleTimeScale;

            float openRealtime = openAnim.Animation.Duration / Mathf.Max(Mathf.Abs(_openTimeScale), 0.0001f);
            yield return new WaitForSecondsRealtime(openRealtime);

            if (_canAnimateValue)
                yield return AnimateValue(_valueAnimDuration);

            yield return new WaitForSecondsRealtime(_idleDuration);

            yield return CloseSequence();
        }

        private IEnumerator CloseSequence()
        {
            _skeletonAnimation.gameObject.SetActive(true);

            TrackEntry closeAnim = _skeletonAnimation.AnimationState.SetAnimation(0, _closeAnimationName, false);
            closeAnim.MixDuration = 0f;

            yield return FadeCanvas(_canvasGroup.alpha, 0f, _closeDuration);

            _skeletonAnimation.AnimationState.ClearTracks();
            _skeletonAnimation.gameObject.SetActive(false);
            _sequenceRoutine = null;
        }

        private IEnumerator FadeCanvas(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                SetCanvasAlpha(to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                SetCanvasAlpha(Mathf.LerpUnclamped(from, to, elapsed / duration));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            SetCanvasAlpha(to);
        }

        private IEnumerator AnimateValue(float duration)
        {
            if (duration <= 0f)
            {
                SetValueText(_targetValue);
                yield break;
            }

            SetValueText(0f);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                SetValueText(Mathf.LerpUnclamped(0f, _targetValue, elapsed / duration));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            SetValueText(_targetValue);
        }

        private void StopSequence()
        {
            if (_sequenceRoutine == null)
                return;
            StopCoroutine(_sequenceRoutine);
            _sequenceRoutine = null;
        }

        private void SetCanvasAlpha(float value)
        {
            _canvasGroup.alpha = value;
            bool visible = value > 0.001f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        private void SetValueText(float value)
        {
            _cashoutValue.text = AmountTextUtility.FormatAmount(value, _valuePrefix, _valueSuffix, _valueDecimals);
        }
    }

    public static class AmountTextUtility
    {
        private static readonly Regex NumberRegex = new Regex(@"[-+]?\d+(?:[.,]\d+)?", RegexOptions.Compiled);

        public static string FormatAmount(float value, string prefix, string suffix, int decimals)
        {
            string formattedValue = decimals > 0
                ? value.ToString("F" + decimals, CultureInfo.InvariantCulture)
                : Mathf.Round(value).ToString("F0", CultureInfo.InvariantCulture);

            string trimmedPrefix = prefix?.Trim() ?? string.Empty;
            string trimmedSuffix = suffix?.Trim() ?? string.Empty;
            string leading = trimmedPrefix.Length > 0 ? trimmedPrefix + " " : string.Empty;
            string trailing = trimmedSuffix.Length > 0 ? " " + trimmedSuffix : string.Empty;
            return $"{leading}{formattedValue}{trailing}";
        }

        public static bool TryParseAmount(string text, out float value, out string prefix, out string suffix, out int decimals)
        {
            value = 0f;
            prefix = "";
            suffix = "";
            decimals = 0;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            Match match = NumberRegex.Match(text);
            if (!match.Success)
                return false;

            string numberText = match.Value;
            int separatorIndex = numberText.IndexOfAny(new[] { '.', ',' });
            if (separatorIndex >= 0)
                decimals = numberText.Length - separatorIndex - 1;

            string normalized = numberText.Replace(',', '.');
            if (!float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return false;

            if (value < 0f)
                value = 0f;

            prefix = text.Substring(0, match.Index);
            suffix = text.Substring(match.Index + match.Length);
            return true;
        }

        public static int GetDecimalPlaces(float amount)
        {
            string text = amount.ToString(CultureInfo.InvariantCulture);
            int separatorIndex = text.IndexOf('.');
            if (separatorIndex < 0)
                return 0;

            return Mathf.Clamp(text.Length - separatorIndex - 1, 0, 6);
        }
    }
}
