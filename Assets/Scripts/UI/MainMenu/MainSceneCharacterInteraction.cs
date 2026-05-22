using UnityEngine;
using UnityEngine.EventSystems;

namespace VectoArena.UI.MainMenu
{
    public class MainSceneCharacterInteraction : MonoBehaviour, IDragHandler, IPointerClickHandler
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private float _dragSensitivity = 10f;
        [SerializeField] private float _dragFriction = 0.93f;
        
        private const float MIN_FLARE_DELAY = 10f;
        private const float MAX_FLARE_DELAY = 25f;

        private float _nextFlareTime = -1f;
        private float _inertia;
        
        private readonly int _flairHash = Animator.StringToHash("flair");

        private void Start()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }
            
            _nextFlareTime = Time.time + Random.Range(MIN_FLARE_DELAY / 2, MAX_FLARE_DELAY / 2);
            
            // Add PhysicsRaycaster to Camera.main if not present (needed for 3D clicks)
            if (Camera.main != null && Camera.main.GetComponent<PhysicsRaycaster>() == null)
            {
                Camera.main.gameObject.AddComponent<PhysicsRaycaster>();
            }
        }

        private void Update()
        {
            if (Time.time > _nextFlareTime)
            {
                TriggerFlair();
                _nextFlareTime = Time.time + Random.Range(MIN_FLARE_DELAY, MAX_FLARE_DELAY);
            }
            
            if (_inertia != 0)
            {
                transform.Rotate(Vector3.up, _inertia * Time.deltaTime, Space.Self);
                _inertia *= _dragFriction;
                
                if (Mathf.Abs(_inertia) < 0.01f)
                {
                    _inertia = 0;
                }
            }
        }

        private void TriggerFlair()
        {
            if (_animator != null)
            {
                // Check if the animator has the "flair" trigger parameter before setting it
                foreach (AnimatorControllerParameter param in _animator.parameters)
                {
                    if (param.nameHash == _flairHash && param.type == AnimatorControllerParameterType.Trigger)
                    {
                        _animator.SetTrigger(_flairHash);
                        break;
                    }
                }
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            _inertia = -eventData.delta.x * _dragSensitivity;
            transform.Rotate(Vector3.up, -eventData.delta.x, Space.Self);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Can be used for character selection/click effects
        }
    }
}
