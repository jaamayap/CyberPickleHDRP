using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [Documentation("IMPORTANT: Do not put this component on any object you plan to toggle, " +
                   "since once the object is off, this script will not work. This will toggle off " +
                   "objects on collision, and toggle them back on at launch.\n\nThis " +
                   "version specifically will handle both objects that you manually add, and" +
                   " objects with TrailRenderer components. The TrailRenderers will be added " +
                   "automatically, but you can still add other objects which don't have TrailRenderer " +
                   "components, and they will be toggled as well.",
        "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/projectile-factory-documentation/observers-global-observers-and-observer-objects")]
    [Serializable]
    public class ToggleTrailRendererObjectsDestroyAndLaunchObserverObject : ToggleObjectsAtEventsObserverObject
    {
        protected bool isSetup;
        protected List<TrailRenderer> trailRenderers = new();

        protected override void OnValidate()
        {
            base.OnValidate();
            FindTrailRendererObjects();
        }

        protected virtual void FindTrailRendererObjects()
        {
            // Find all objects on this or children with a TrainRenderer, and then add them to the objects list
            foreach (var obj in GetComponentsInChildren<TrailRenderer>())
            {
                if (objects.Contains(obj.gameObject))
                    continue;

                objects.Add(obj.gameObject);
            }
        }

        protected override void SetObject(GameObject obj, bool value)
        {
            if (!isSetup)
                Setup();

            obj.SetActive(true);
            var index = objects.IndexOf(obj);

            // Handle the trail renderers
            if (trailRenderers[index] == null)
            {
                base.SetObject(obj, value);
                return;
            }

            // Before enabling the trail renderer, clear its previous path if we are enabling it
            if (value) trailRenderers[index].Clear();

            trailRenderers[index].enabled = value;
            
            base.SetObject(obj, value);
        }

        protected virtual void Setup()
        {
            isSetup = true;
            foreach (var obj in objects)
                AddTrailRenderer(obj.GetComponent<TrailRenderer>());
        }

        protected virtual void AddTrailRenderer(TrailRenderer trailRenderer)
        {
            isSetup = true;
            trailRenderers.Add(trailRenderer);
        }
    }
}