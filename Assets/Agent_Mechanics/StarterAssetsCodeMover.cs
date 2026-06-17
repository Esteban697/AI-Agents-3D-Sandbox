using StarterAssets;
using UnityEngine;

namespace Agent_Mechanics
{
    [RequireComponent(typeof(CharacterController))]
    public class StarterAssetsCodeMover : MonoBehaviour
    {
        [SerializeField] private StarterAssetsInputs starterAssetsInputs;

        private void Awake()
        {
            starterAssetsInputs = GetComponent<StarterAssetsInputs>();
        }

        // Callable methods
        public void MoveForward() => starterAssetsInputs.move = new Vector2(starterAssetsInputs.move.x, 1f);
        public void MoveRight() => starterAssetsInputs.move = new Vector2(1f, starterAssetsInputs.move.y);
        public void MoveLeft() => starterAssetsInputs.move = new Vector2(-1f, starterAssetsInputs.move.y);

        public void StopForwardBack() => starterAssetsInputs.move = new Vector2(starterAssetsInputs.move.x, 0f);
        public void StopLeftRight() => starterAssetsInputs.move = new Vector2(0f, starterAssetsInputs.move.y);
        
        public void RotateRight() => starterAssetsInputs.look = new Vector2(45f, starterAssetsInputs.look.y);
        public void RotateLeft() => starterAssetsInputs.look = new Vector2(-45, starterAssetsInputs.look.y);

        public void MoveBackward()
        {
            starterAssetsInputs.look = new Vector2(180f, starterAssetsInputs.look.y);
            starterAssetsInputs.move = new Vector2(starterAssetsInputs.move.x, 0.02f);
        }

        public void StopAll() {
            starterAssetsInputs.move = Vector2.zero;
            starterAssetsInputs.look = Vector2.zero;
        } 
        
        public void MoveForwardRight()
        {
            starterAssetsInputs.look = new Vector2(45f, starterAssetsInputs.look.y);
            starterAssetsInputs.move = new Vector2(starterAssetsInputs.move.x, 0.01f);
        }

        public void MoveForwardLeft()
        {
            starterAssetsInputs.look = new Vector2(-45f, starterAssetsInputs.look.y);
            starterAssetsInputs.move = new Vector2(starterAssetsInputs.move.x, 0.01f);
        }

        public void Jump()
        {
            starterAssetsInputs.jump = true;
        }
    }
}