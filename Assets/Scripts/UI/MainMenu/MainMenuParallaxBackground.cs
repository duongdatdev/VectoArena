using UnityEngine;
using UnityEngine.InputSystem;

namespace VectoArena.UI.MainMenu
{
    public class MainMenuParallaxBackground : MonoBehaviour
    {
        [SerializeField] private Transform _centerTriangle;
        [SerializeField] private float _centerTriangleShift = 0.1f;
        [SerializeField] private Transform _innerBolts;
        [SerializeField] private float _innerBoltsShift = 0.2f;
        [SerializeField] private Transform _outerBolts;
        [SerializeField] private float _outerBoltsShift = 0.3f;

        [SerializeField] private float _dampingSpeed = 1f;

        private UnityEngine.InputSystem.Gyroscope _gyro;

        private Vector3 _dampenedRotationRate;

        private void Start()
        {
            _gyro = UnityEngine.InputSystem.Gyroscope.current;

            if (_gyro != null)
            {
                InputSystem.EnableDevice(UnityEngine.InputSystem.Gyroscope.current);
            }
        }

        private void Update()
        {
            if (_gyro == null) return;

            var angularVelocity = _gyro.angularVelocity.ReadValue();

            _dampenedRotationRate = Vector3.Lerp(_dampenedRotationRate, angularVelocity, Time.deltaTime * _dampingSpeed);

            var positionOffset = new Vector3(-_dampenedRotationRate.y, -_dampenedRotationRate.x, 0f);

            if (_centerTriangle != null) _centerTriangle.localPosition = positionOffset * _centerTriangleShift;
            if (_innerBolts != null) _innerBolts.localPosition = positionOffset * _innerBoltsShift;
            if (_outerBolts != null) _outerBolts.localPosition = positionOffset * _outerBoltsShift;
        }

        public void SetupTransforms(Transform centerTriangle, Transform innerBolts, Transform outerBolts)
        {
            _centerTriangle = centerTriangle;
            _innerBolts = innerBolts;
            _outerBolts = outerBolts;
        }
    }
}
