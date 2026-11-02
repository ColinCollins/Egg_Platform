using UnityEngine;

namespace GF
{
    public class AlignToTop : MonoBehaviour
    {
        public float distanceFromTop = 100f;

        private SpriteRenderer _spriteRenderer;
        private Camera _mainCamera;
        
        private float _ratio;

        void Start()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _mainCamera = Camera.main;
        
            if (_spriteRenderer == null || _mainCamera == null)
            {
                Debug.LogError("SpriteRenderer or Camera not found!");
                return;
            }
            
            _ratio = Screen.width / 1080f;

            Align();
        }

        void Update()
        {
            Align();
        }

        void Align()
        {
            // Calculate the position of the top of the sprite in world space
            float spriteHeight = _spriteRenderer.bounds.size.y;
            Vector3 spriteTopWorldPos = transform.position + new Vector3(0f, spriteHeight / 2f, 0f);

            // Convert the world position of the top of the sprite to screen space
            Vector3 spriteTopScreenPos = _mainCamera.WorldToScreenPoint(spriteTopWorldPos);

            // Calculate the desired position with the specified distance from the top
            Vector3 desiredScreenPos = new Vector3(spriteTopScreenPos.x, Screen.height - distanceFromTop * _ratio, spriteTopScreenPos.z);

            // Convert the desired screen position back to world space
            Vector3 desiredWorldPos = _mainCamera.ScreenToWorldPoint(desiredScreenPos);

            // Update the sprite's position
            transform.position = new Vector3(transform.position.x, desiredWorldPos.y - spriteHeight / 2f, transform.position.z);
        }
    }
}