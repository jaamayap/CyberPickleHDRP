using System.Collections.Generic;
using UnityEngine;
using static MagicPigGames.ProjectileFactory.ProjectileUtilities;

namespace MagicPigGames.ProjectileFactory
{
    public class DemoController : MonoBehaviour
    {
        public ProjectileSpawner player;
        public KeyCode shootKey = KeyCode.Mouse0;
        public KeyCode nextProjectileKey = KeyCode.Tab;

        [Tooltip("Used to negate the other key, i.e. hold this to do the opposite action.")]
        public KeyCode oppositeModifierKey = KeyCode.LeftShift;

        public KeyCode showTrajectoryKey = KeyCode.T;

        [Header("Options")] [Tooltip("This is the tag that will be used to check if the mouse is over a UI element.")]
        public string clickBlockString = "ClickBlock";

        [Header("Debug Options")] public KeyCode timescaleMinus = KeyCode.LeftBracket;

        public KeyCode timescalePlus = KeyCode.RightBracket;
        public float timescaleStep = 0.25f;
        public KeyCode toggleCanvas = KeyCode.C;
        public GameObject canvasObject;

        public List<MoveBetweenPoints> targets = new();

        protected virtual void Update()
        {
            TryShoot();
            TryStopShooting();
            TryChangeProjectile();
            SetTrajectoryValue();

            DebugButtons();
        }

        private void DebugButtons()
        {
            if (Input.GetKeyDown(timescaleMinus))
                SetTimescale(-timescaleStep);
            if (Input.GetKeyDown(timescalePlus))
                SetTimescale(timescaleStep);

            if (Input.GetKeyDown(toggleCanvas))
                canvasObject.SetActive(!canvasObject.activeSelf);
        }

        private void SetTimescale(float step)
        {
            Time.timeScale = Mathf.Clamp(Time.timeScale + step, 0.01f, 1f);
            Debug.Log($"Timescale is now {Time.timeScale}, added {step}");
        }

        // This will just ensure that the trajectory is set to be on whenever the key is pressed.
        protected virtual void SetTrajectoryValue()
        {
            player.showTrajectory = Input.GetKey(showTrajectoryKey);
        }

        protected virtual void TryChangeProjectile()
        {
            if (!Input.GetKeyDown(nextProjectileKey)) return;

            if (Input.GetKey(oppositeModifierKey))
                player.NextProjectile(-1);
            else
                player.NextProjectile();
        }

        protected virtual bool TryShoot()
        {
            if (!Input.GetKeyDown(shootKey)) return false;

            if (IsPointerOverUIElementWithTag(clickBlockString))
                return false;

            StartShooting();
            return true;
        }

        protected virtual void StartShooting()
        {
            player.SpawnProjectile();
        }

        protected virtual bool TryStopShooting()
        {
            if (!Input.GetKeyUp(shootKey)) return false;
            player.StopProjectile();
            return true;
        }

        public virtual void NewProjectileSelected(Projectile newBasicProjectile, Projectile oldBasicProjectile)
        {
            if (Input.GetKey(shootKey))
                StartShooting();
        }

        public virtual void ToggleAllTargetMotion()
        {
            SetTargets();

            foreach (var target in targets)
                target.ToggleMoving();
        }

        public virtual void SetTargetsSpeed(float value)
        {
            SetTargets();

            foreach (var target in targets)
                target.SetMaxSpeed(value);
        }

        private void SetTargets()
        {
            if (targets.Count != 0) return;
            var layerActor = LayerMask.NameToLayer("Actor");
            var objects = FindObjectsOfType<MoveBetweenPoints>();

            foreach (var obj in objects)
                targets.Add(obj.GetComponent<MoveBetweenPoints>());
        }
    }
}