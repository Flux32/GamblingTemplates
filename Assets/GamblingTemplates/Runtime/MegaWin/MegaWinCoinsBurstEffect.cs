using System;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace Modules.Pepe
{
    public sealed class MegaWinCoinsBurstEffect : MonoBehaviour
    {
        private static readonly string[] CoinSpinAnimations =
        {
            "idle_1",
            "idle_2",
            "idle_3",
            "idle_4",
            "idle_5",
            "idle_6"
        };

        [Serializable]
        private struct DepthLayerConfig
        {
            [Min(0f)] public float Weight;
            [Min(0f)] public float MinScaleMultiplier;
            [Min(0f)] public float MaxScaleMultiplier;
            [Min(0f)] public float MinSpeedMultiplier;
            [Min(0f)] public float MaxSpeedMultiplier;
            [Min(0f)] public float DragMultiplier;
            [Min(0f)] public float GravityMultiplier;
            [Min(0f)] public float RotationMultiplier;
            [Range(0f, 1f)] public float Alpha;
            [Min(0f)] public float Brightness;
            [Min(0f)] public float MinLifetimeMultiplier;
            [Min(0f)] public float MaxLifetimeMultiplier;
            [Min(0f)] public float StretchMultiplier;
            [Min(0)] public int MaxBounces;
            public bool UseAdditiveMaterial;
        }

        private struct CoinState
        {
            public RectTransform RectTransform;
            public SkeletonGraphic Graphic;
            public Vector2 Velocity;
            public Vector3 AngularVelocity;
            public Vector3 BaseScale;
            public float Gravity;
            public float Drag;
            public float Age;
            public float Lifetime;
            public float FadeDuration;
            public float BaseAlpha;
            public float Brightness;
            public int FloorBounceCount;
            public bool IsActive;
        }

        [SerializeField] private SkeletonGraphic _coinTemplate;
        [SerializeField] private RectTransform _spawnOrigin;
        [SerializeField, Min(1)] private int _coinCount = 82;

        [SerializeField, Min(0f)] private float _spawnRatePerSecond = 64f;
        [SerializeField, Min(0.001f)] private float _physicsStep = 0.008333334f;

        [SerializeField, Min(0f)] private float _spawnRadius = 9f;
        [SerializeField] private Vector2 _launchAngleRange = new Vector2(40f, 140f);
        [SerializeField, Min(0.1f)] private float _launchSpreadBiasPower = 1.8f;
        [SerializeField, Min(0f)] private float _minLaunchSpeed = 1480f;
        [SerializeField, Min(0f)] private float _maxLaunchSpeed = 2550f;
        [SerializeField, Min(0f)] private float _upwardBoostMin = 980f;
        [SerializeField, Min(0f)] private float _upwardBoostMax = 1480f;
        [SerializeField, Min(0f)] private float _horizontalBoost = 260f;

        [SerializeField, Min(0f)] private float _baseGravity = 1720f;
        [SerializeField, Min(0f)] private float _baseDrag = 0.045f;
        [SerializeField, Range(0f, 1f)] private float _bounciness = 0.84f;
        [SerializeField, Range(0f, 1f)] private float _wallFriction = 0.95f;
        [SerializeField, Range(0f, 1f)] private float _floorFriction = 0.9f;
        [SerializeField, Min(0f)] private float _floorSoftClampSpeed = 88f;
        [SerializeField, Min(0f)] private float _bottomFadeZoneHeight = 66f;
        [SerializeField, Range(0f, 1f)] private float _bottomFadeDamping = 0.9f;
        [SerializeField, Min(0f)] private float _screenEdgePadding = 16f;
        [SerializeField, Min(0f)] private float _despawnBelowBoundsOffset = 120f;
        [SerializeField, Range(0.1f, 1f)] private float _collisionHeightFactor = 0.36f;

        [SerializeField, Min(0f)] private float _minLifetime = 2.9f;
        [SerializeField, Min(0f)] private float _maxLifetime = 4.4f;
        [SerializeField, Min(0f)] private float _minEndFadeDuration = 0.42f;
        [SerializeField, Min(0f)] private float _maxEndFadeDuration = 0.8f;

        [SerializeField] private Vector2 _spinZRange = new Vector2(-680f, 680f);
        [SerializeField] private Vector2 _spinXRange = new Vector2(0f, 0f);
        [SerializeField] private Vector2 _spinYRange = new Vector2(0f, 0f);
        [SerializeField, Min(0f)] private float _maxTiltDegrees = 0f;
        [SerializeField] private Color _baseTint = new Color(1f, 1f, 1f, 1f);
        [SerializeField, Min(0.01f)] private float _dropFadeDuration = 0.4f;

        [SerializeField] private DepthLayerConfig _backgroundLayer = new DepthLayerConfig
        {
            Weight = 0.32f,
            MinScaleMultiplier = 0.58f,
            MaxScaleMultiplier = 0.8f,
            MinSpeedMultiplier = 0.6f,
            MaxSpeedMultiplier = 0.82f,
            DragMultiplier = 1.22f,
            GravityMultiplier = 1.1f,
            RotationMultiplier = 0.42f,
            Alpha = 0.62f,
            Brightness = 0.96f,
            MinLifetimeMultiplier = 1.03f,
            MaxLifetimeMultiplier = 1.26f,
            StretchMultiplier = 0.55f,
            MaxBounces = 4,
            UseAdditiveMaterial = false
        };

        [SerializeField] private DepthLayerConfig _midLayer = new DepthLayerConfig
        {
            Weight = 0.44f,
            MinScaleMultiplier = 0.82f,
            MaxScaleMultiplier = 1.08f,
            MinSpeedMultiplier = 0.9f,
            MaxSpeedMultiplier = 1.12f,
            DragMultiplier = 1f,
            GravityMultiplier = 1f,
            RotationMultiplier = 0.78f,
            Alpha = 0.82f,
            Brightness = 1.02f,
            MinLifetimeMultiplier = 0.95f,
            MaxLifetimeMultiplier = 1.1f,
            StretchMultiplier = 0.85f,
            MaxBounces = 3,
            UseAdditiveMaterial = false
        };

        [SerializeField] private DepthLayerConfig _foregroundLayer = new DepthLayerConfig
        {
            Weight = 0.24f,
            MinScaleMultiplier = 1.06f,
            MaxScaleMultiplier = 1.34f,
            MinSpeedMultiplier = 1.08f,
            MaxSpeedMultiplier = 1.28f,
            DragMultiplier = 0.88f,
            GravityMultiplier = 0.92f,
            RotationMultiplier = 1.2f,
            Alpha = 1f,
            Brightness = 1.11f,
            MinLifetimeMultiplier = 0.9f,
            MaxLifetimeMultiplier = 1.04f,
            StretchMultiplier = 1.14f,
            MaxBounces = 2,
            UseAdditiveMaterial = false
        };

        private readonly List<CoinState> _coinStates = new();
        private Material _defaultCoinMaterial;
        private Vector3 _templateScale = Vector3.one;
        private bool _isPlaying;
        private bool _isDroppingCoins;
        private float _dropElapsed;
        private float _elapsed;
        private float _spawnAccumulator;
        private float _physicsAccumulator;
        private int _nextCoinIndex;

        private void Awake()
        {
            _templateScale = _coinTemplate.rectTransform.localScale;
            _defaultCoinMaterial = _coinTemplate.material;
            _coinTemplate.gameObject.SetActive(false);
            BuildPool();
        }

        private void OnDisable()
        {
            StopAndReset();
        }

        private void Update()
        {
            if (!_isPlaying && !_isDroppingCoins)
                return;

            float deltaTime = Time.unscaledDeltaTime;
            _elapsed += deltaTime;

            if (_isDroppingCoins)
                _dropElapsed += deltaTime;
            else
                EmitContinuous(deltaTime);

            _physicsAccumulator += deltaTime;

            while (_physicsAccumulator >= _physicsStep)
            {
                UpdateCoins(_physicsStep);
                _physicsAccumulator -= _physicsStep;
            }
        }

        public void Play()
        {
            StopAndReset();
            _isPlaying = true;
        }

        public void StopWithGravityDrop(float gravityMultiplier)
        {
            _isPlaying = false;
            _isDroppingCoins = true;
            _dropElapsed = 0f;

            for (int i = 0; i < _coinStates.Count; i++)
            {
                CoinState coin = _coinStates[i];
                if (!coin.IsActive)
                    continue;

                coin.Gravity *= gravityMultiplier;
                coin.FloorBounceCount = 100;
                _coinStates[i] = coin;
            }
        }

        public void StopAndReset()
        {
            _isPlaying = false;
            _isDroppingCoins = false;
            _dropElapsed = 0f;
            _elapsed = 0f;
            _spawnAccumulator = 0f;
            _physicsAccumulator = 0f;
            _nextCoinIndex = 0;

            for (var i = 0; i < _coinStates.Count; i++)
            {
                CoinState coin = _coinStates[i];
                coin.RectTransform.anchoredPosition = OffScreenPosition;
                coin.RectTransform.localScale = _templateScale;
                coin.RectTransform.localRotation = Quaternion.identity;
                coin.Graphic.material = _defaultCoinMaterial;
                coin.Graphic.color = Color.white;
                coin.Graphic.gameObject.SetActive(false);
                coin.Velocity = Vector2.zero;
                coin.AngularVelocity = Vector3.zero;
                coin.FloorBounceCount = 0;
                coin.IsActive = false;
                _coinStates[i] = coin;
            }
        }

        private void EmitContinuous(float deltaTime)
        {
            _spawnAccumulator += _spawnRatePerSecond * deltaTime;
            int spawnCount = Mathf.FloorToInt(_spawnAccumulator);
            if (spawnCount <= 0)
                return;

            _spawnAccumulator -= spawnCount;
            for (int i = 0; i < spawnCount; i++)
            {
                if (!TrySpawnCoin())
                    return;
            }
        }

        private int UpdateCoins(float deltaTime)
        {
            CalculateBounds(out float minX, out float maxX, out float minY, out float maxY);
            int activeCoins = 0;

            for (int i = 0; i < _coinStates.Count; i++)
            {
                CoinState coin = _coinStates[i];
                if (!coin.IsActive)
                {
                    _coinStates[i] = coin;
                    continue;
                }

                activeCoins++;
                coin.Age += deltaTime;

                coin.Velocity *= Mathf.Exp(-coin.Drag * deltaTime);
                coin.Velocity += Vector2.down * (coin.Gravity * deltaTime);

                Vector2 currentPosition = coin.RectTransform.anchoredPosition;
                Vector2 nextPosition = currentPosition + coin.Velocity * deltaTime;
                float halfWidth = coin.RectTransform.rect.width * Mathf.Abs(coin.RectTransform.localScale.x) * 0.5f;
                float halfHeight = coin.RectTransform.rect.height
                    * Mathf.Abs(coin.RectTransform.localScale.y)
                    * 0.5f
                    * _collisionHeightFactor;

                float coinMinX = minX + halfWidth;
                float coinMaxX = maxX - halfWidth;
                float coinMinY = minY + halfHeight;

                if (nextPosition.x <= coinMinX)
                {
                    nextPosition.x = coinMinX;
                    coin.Velocity.x = Mathf.Abs(coin.Velocity.x) * _bounciness;
                    coin.Velocity.y *= _wallFriction;
                }
                else if (nextPosition.x >= coinMaxX)
                {
                    nextPosition.x = coinMaxX;
                    coin.Velocity.x = -Mathf.Abs(coin.Velocity.x) * _bounciness;
                    coin.Velocity.y *= _wallFriction;
                }

                bool isCrossingFloor = currentPosition.y > coinMinY && nextPosition.y <= coinMinY;
                if (isCrossingFloor)
                {
                    coin.FloorBounceCount++;
                    if (coin.FloorBounceCount == 1)
                    {
                        nextPosition.y = coinMinY;
                        coin.Velocity.y = Mathf.Abs(coin.Velocity.y) * _bounciness;
                        coin.Velocity.x *= _floorFriction;
                    }
                }

                if (coin.FloorBounceCount <= 1 && nextPosition.y <= coinMinY + _bottomFadeZoneHeight && coin.Age >= coin.Lifetime * 0.35f)
                {
                    coin.Velocity.x *= _bottomFadeDamping;
                }

                if (coin.FloorBounceCount >= 2 && nextPosition.y <= coinMinY - halfHeight - _despawnBelowBoundsOffset)
                {
                    DeactivateCoin(ref coin);
                    _coinStates[i] = coin;
                    activeCoins--;
                    continue;
                }

                coin.RectTransform.anchoredPosition = nextPosition;
                coin.RectTransform.localRotation *= Quaternion.Euler(coin.AngularVelocity * deltaTime);

                coin.RectTransform.localScale = coin.BaseScale;

                float alpha = coin.BaseAlpha;
                if (_isDroppingCoins)
                    alpha *= Mathf.Clamp01(1f - _dropElapsed / _dropFadeDuration);

                Color color = new Color(
                    _baseTint.r * coin.Brightness,
                    _baseTint.g * coin.Brightness,
                    _baseTint.b * coin.Brightness,
                    alpha);

                coin.Graphic.color = color;
                _coinStates[i] = coin;
            }

            return activeCoins;
        }

        private bool TrySpawnCoin()
        {
            int coinCount = _coinStates.Count;
            for (int i = 0; i < coinCount; i++)
            {
                int coinIndex = (_nextCoinIndex + i) % coinCount;
                if (_coinStates[coinIndex].IsActive)
                    continue;

                _nextCoinIndex = coinIndex + 1;
                if (_nextCoinIndex >= coinCount)
                    _nextCoinIndex = 0;

                SpawnCoin(coinIndex);
                return true;
            }

            return false;
        }

        private void SpawnCoin(int coinIndex)
        {
            CoinState coin = _coinStates[coinIndex];
            DepthLayerConfig layer = SelectLayerConfig();

            Vector2 spawnCenter = GetSpawnCenter();
            Vector2 spawnJitter = UnityEngine.Random.insideUnitCircle * _spawnRadius;

            float centerAngle = (_launchAngleRange.x + _launchAngleRange.y) * 0.5f;
            float halfSpread = (_launchAngleRange.y - _launchAngleRange.x) * 0.5f;
            float spreadSample = UnityEngine.Random.Range(-1f, 1f);
            float biasedSpread = Mathf.Sign(spreadSample) * Mathf.Pow(Mathf.Abs(spreadSample), _launchSpreadBiasPower);
            float angle = (centerAngle + biasedSpread * halfSpread) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float speed = UnityEngine.Random.Range(_minLaunchSpeed, _maxLaunchSpeed)
                * UnityEngine.Random.Range(layer.MinSpeedMultiplier, layer.MaxSpeedMultiplier);

            Vector2 velocity = direction * speed;
            velocity.y += UnityEngine.Random.Range(_upwardBoostMin, _upwardBoostMax);
            velocity.x += UnityEngine.Random.Range(-_horizontalBoost, _horizontalBoost);

            float scaleMul = UnityEngine.Random.Range(layer.MinScaleMultiplier, layer.MaxScaleMultiplier);
            float tiltX = UnityEngine.Random.Range(-_maxTiltDegrees, _maxTiltDegrees);
            float tiltY = UnityEngine.Random.Range(-_maxTiltDegrees, _maxTiltDegrees);
            float spinZ = UnityEngine.Random.Range(0f, 360f);

            coin.RectTransform.localRotation = Quaternion.Euler(tiltX, tiltY, spinZ);
            coin.RectTransform.localScale = _templateScale * scaleMul;

            coin.Graphic.material = layer.UseAdditiveMaterial ? coin.Graphic.additiveMaterial : _defaultCoinMaterial;
            coin.Graphic.gameObject.SetActive(true);
            PlayCoinSpinAnimation(coin.Graphic);
            coin.Graphic.color = new Color(
                _baseTint.r * layer.Brightness,
                _baseTint.g * layer.Brightness,
                _baseTint.b * layer.Brightness,
                layer.Alpha);
            coin.Graphic.UpdateMesh();
            coin.RectTransform.anchoredPosition = spawnCenter + spawnJitter;

            coin.Velocity = velocity;
            coin.AngularVelocity = new Vector3(
                UnityEngine.Random.Range(_spinXRange.x, _spinXRange.y),
                UnityEngine.Random.Range(_spinYRange.x, _spinYRange.y),
                UnityEngine.Random.Range(_spinZRange.x, _spinZRange.y)) * layer.RotationMultiplier;

            coin.BaseScale = _templateScale * scaleMul;
            coin.Gravity = _baseGravity * layer.GravityMultiplier;
            coin.Drag = _baseDrag * layer.DragMultiplier;
            coin.Age = 0f;
            coin.Lifetime = UnityEngine.Random.Range(_minLifetime, _maxLifetime)
                * UnityEngine.Random.Range(layer.MinLifetimeMultiplier, layer.MaxLifetimeMultiplier);
            coin.FadeDuration = UnityEngine.Random.Range(_minEndFadeDuration, _maxEndFadeDuration);
            coin.BaseAlpha = layer.Alpha;
            coin.Brightness = layer.Brightness;
            coin.FloorBounceCount = 0;
            coin.IsActive = true;

            _coinStates[coinIndex] = coin;
        }

        private void PlayCoinSpinAnimation(SkeletonGraphic graphic)
        {
            int animationIndex = UnityEngine.Random.Range(0, CoinSpinAnimations.Length);
            TrackEntry entry = graphic.AnimationState.SetAnimation(0, CoinSpinAnimations[animationIndex], true);
            entry.MixDuration = 0f;
            entry.TrackTime = UnityEngine.Random.Range(0f, entry.AnimationEnd);
        }

        private DepthLayerConfig SelectLayerConfig()
        {
            float totalWeight = _backgroundLayer.Weight + _midLayer.Weight + _foregroundLayer.Weight;
            float pick = UnityEngine.Random.Range(0f, totalWeight);

            if (pick <= _backgroundLayer.Weight)
                return _backgroundLayer;

            pick -= _backgroundLayer.Weight;
            if (pick <= _midLayer.Weight)
                return _midLayer;

            return _foregroundLayer;
        }

        private void BuildPool()
        {
            _coinStates.Clear();

            for (int i = 0; i < _coinCount; i++)
            {
                SkeletonGraphic graphic = Instantiate(_coinTemplate, _spawnOrigin);
                RectTransform rectTransform = graphic.rectTransform;

                rectTransform.anchoredPosition = Vector2.zero;
                rectTransform.localScale = _templateScale;
                rectTransform.localRotation = Quaternion.identity;

                graphic.material = _defaultCoinMaterial;
                graphic.color = Color.white;
                graphic.gameObject.SetActive(false);
                CoinState coin = new CoinState
                {
                    RectTransform = rectTransform,
                    Graphic = graphic,
                    Velocity = Vector2.zero,
                    AngularVelocity = Vector3.zero,
                    BaseScale = _templateScale,
                    Gravity = _baseGravity,
                    Drag = _baseDrag,
                    Age = 0f,
                    Lifetime = 0f,
                    FadeDuration = _minEndFadeDuration,
                    BaseAlpha = 1f,
                    Brightness = 1f,
                    FloorBounceCount = 0,
                    IsActive = false
                };

                _coinStates.Add(coin);
            }
        }

        private Vector2 GetSpawnCenter()
        {
            return _spawnOrigin.rect.center;
        }

        private void CalculateBounds(out float minX, out float maxX, out float minY, out float maxY)
        {
            Camera uiCamera = _coinTemplate.canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_spawnOrigin, Vector2.zero, uiCamera, out Vector2 bottomLeft);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _spawnOrigin,
                new Vector2(Screen.width, Screen.height),
                uiCamera,
                out Vector2 topRight);

            minX = Mathf.Min(bottomLeft.x, topRight.x) + _screenEdgePadding;
            maxX = Mathf.Max(bottomLeft.x, topRight.x) - _screenEdgePadding;
            minY = Mathf.Min(bottomLeft.y, topRight.y) + _screenEdgePadding;
            maxY = Mathf.Max(bottomLeft.y, topRight.y) - _screenEdgePadding;
        }

        private static readonly Vector2 OffScreenPosition = new(0f, 9999f);

        private void DeactivateCoin(ref CoinState coin)
        {
            coin.RectTransform.anchoredPosition = OffScreenPosition;
            coin.Graphic.gameObject.SetActive(false);
            coin.Velocity = Vector2.zero;
            coin.AngularVelocity = Vector3.zero;
            coin.IsActive = false;
        }

    }
}
