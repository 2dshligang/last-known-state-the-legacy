using System;
using UnityEngine;

namespace LKS.World
{
    public sealed class StationLayout : ScriptableObject
    {
        [SerializeField] private float bayLength = 4;
        [SerializeField] private float innerWidth = 4;
        [SerializeField] private float ceilingHeight = 3;
        [SerializeField] private float exteriorDepth = 12;
        [SerializeField] private float exteriorWidth = 11;
        [SerializeField] private Vector3 spawn = new Vector3(0, 0, 1);

        public float BayLength => bayLength;
        public float InnerWidth => innerWidth;
        public float CeilingHeight => ceilingHeight;
        public float ExteriorDepth => exteriorDepth;
        public float ExteriorWidth => exteriorWidth;
        public Vector3 Spawn => spawn;
        public float InteriorEnd => 6 * bayLength;

        public void Validate()
        {
            if (!(bayLength >= 4 && bayLength <= 8 && innerWidth >= 4 && innerWidth <= 8 &&
                ceilingHeight >= 3 && ceilingHeight <= 5 && exteriorDepth >= 10 && exteriorDepth <= 22 &&
                exteriorWidth >= 10 && exteriorWidth <= 20))
                throw new InvalidOperationException("Station dimensions exceed the supported graybox range.");
            if (!(Mathf.Abs(spawn.x) < innerWidth / 2 - .5f && spawn.y == 0 &&
                spawn.z > .5f && spawn.z < bayLength - .5f))
                throw new InvalidOperationException("Spawn must be on the floor in the arrival bay.");
        }
    }
}
