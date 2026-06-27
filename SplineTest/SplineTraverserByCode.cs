using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Splines.Components;
using Stride.Input;
using System;

namespace SplineTools
{
    public class SplineTraverserByCode : SyncScript
    {
        public SplineComponent SplineComponent;
        public float Speed = 0.5f;

        private SplineTraverserComponent splineTraverserComponent;

        public override void Start()
        {
            if (SplineComponent is null)
            {
                throw new NullReferenceException("SplineComponent is empty");
            }

            splineTraverserComponent = new SplineTraverserComponent();
            //splineTraverserComponent.SplineTraverser.Entity = Entity;
            splineTraverserComponent.SplineComponent = SplineComponent;
            splineTraverserComponent.Speed = Speed;
            splineTraverserComponent.IsRotating = true;
            splineTraverserComponent.IsMoving = true;
            Entity.Add(splineTraverserComponent);
        }

        public override void Update()
        {
            const int HelpTextStartX = 800;
            DebugText.Print($"Press space to toggle movement. Moving:{splineTraverserComponent.IsMoving}", new Int2(HelpTextStartX, 20));
            DebugText.Print($"Use mouse wheel to adjust speed {splineTraverserComponent.SplineTraverser.Speed:0.00}", new Int2(HelpTextStartX, 40));

            if (Input.IsKeyPressed(Keys.Space))
            {
                splineTraverserComponent.IsMoving = !splineTraverserComponent.IsMoving;
            }

            var scrollValue = Input.MouseWheelDelta;
            if (scrollValue != 0)
            {
                splineTraverserComponent.Speed += (float)Math.Round(scrollValue * 0.1f, 1);
            }
        }

    }
}
