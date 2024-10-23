using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    public class SimpleMovement : MonoBehaviour
    {
        public Transform relativeTo;

        public float speed = 25f;
        public KeyCode keyForward = KeyCode.W;
        public KeyCode keyBackward = KeyCode.S;
        public KeyCode keyLeft = KeyCode.A;
        public KeyCode keyRight = KeyCode.D;

        [Header("Barrel Angle")] public GameObject barrel;

        public float maxAngle = 45f;
        public float minAngle = -25f;
        public float angleSpeed = 5f;
        public KeyCode keyUp = KeyCode.E;
        public KeyCode keyDown = KeyCode.Q;

        private void Update()
        {
            if (relativeTo)
                MoveRelativeTo();
            else
                Move();

            ChangeBarrelAngle();
        }

        private void ChangeBarrelAngle()
        {
            var moveUp = Input.GetKey(keyUp);
            var moveDown = Input.GetKey(keyDown);
            if (!moveUp && !moveDown) return;

            var angle = barrel.transform.localEulerAngles.x;
            if (angle > 180)
                angle -= 360;

            if (moveUp)
                angle -= angleSpeed * Time.deltaTime;

            if (moveDown)
                angle += angleSpeed * Time.deltaTime;

            angle = Mathf.Clamp(angle, minAngle, maxAngle);

            barrel.transform.localEulerAngles = new Vector3(angle, 0, 0);
        }

        private void MoveRelativeTo()
        {
            var direction = Vector3.zero;

            if (Input.GetKey(keyForward))
                direction += relativeTo.forward;

            if (Input.GetKey(keyBackward))
                direction += -relativeTo.forward;

            if (Input.GetKey(keyLeft))
                direction += -relativeTo.right;

            if (Input.GetKey(keyRight))
                direction += relativeTo.right;

            transform.position += direction.normalized * (speed * Time.deltaTime);
        }

        private void Move()
        {
            var direction = Vector3.zero;

            if (Input.GetKey(keyForward))
                direction += Vector3.forward;

            if (Input.GetKey(keyBackward))
                direction += Vector3.back;

            if (Input.GetKey(keyLeft))
                direction += Vector3.left;

            if (Input.GetKey(keyRight))
                direction += Vector3.right;

            transform.position += direction.normalized * (speed * Time.deltaTime);
        }
    }
}