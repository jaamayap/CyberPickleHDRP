using UnityEngine;

namespace MagicPigGames
{
    public interface IHandleRaycasts
    {
        void HandleRaycastHit(RaycastHit raycastHit, float maxDistance);
    }
}