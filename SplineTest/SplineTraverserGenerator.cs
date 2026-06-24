using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Splines.Components;
using System.Diagnostics;
using Stride.Engine.Splines.Models;
using Stride.Input;

namespace SplineTools
{
    public class SplineTraverserGenerator : SyncScript
    {
        public SplinesPerformanceTest SplinesPerformanceTest;
        public Entity EntityToClone;

        public float Speed = 5.0f;

        private Stopwatch stopwatch;

        public override void Start()
        {
            SplinesPerformanceTest = Entity.Get<SplinesPerformanceTest>();
        }

        public override void Update()
        {
            DebugText.Print($"Press T to create traverser", new Int2(1600, 120));
            // DebugText.Print($"Press space to toggle movement. Moving:{splineTraverserComponent.IsMoving}", new Int2(600, 20));
            // DebugText.Print($"Use mouse wheel to adjust speed {splineTraverserComponent.SplineTraverser.Speed:0.00}", new Int2(600, 40));
            //
            if (Input.IsKeyPressed(Keys.T))
            {
                foreach (var t in SplinesPerformanceTest.splineComponents)
                {
                    CreateSplineTraverserForSpline(t);
                }
            }
            
            //
            //
            // if (Input.IsKeyPressed(Keys.Space))
            // {
            //     splineTraverserComponent.IsMoving = !splineTraverserComponent.IsMoving;
            // }
            //
            // var scrollValue = Input.MouseWheelDelta;
            // if (scrollValue != 0)
            // {
            //     splineTraverserComponent.Speed += (float)Math.Round(scrollValue * 0.1f, 1);
            // }
        }

        private void CreateSplineTraverserForSpline(SplineComponent splineComponent)
        {
            var clone = EntityToClone.Clone();
          
            Entity.Scene.Entities.Add(clone);
            clone.Transform.Position = splineComponent.Nodes[0].SplineNode.WorldPosition;
            clone.Transform.UpdateWorldMatrix();
            
            var splineTraverserComponent = new SplineTraverserComponent
            {
                SplineTraverser =
                {
                    Entity = Entity
                },
                SplineComponent = splineComponent,
                Speed = Speed,
                IsRotating = true,
                IsMoving = true
            };
            
            clone.Add(splineTraverserComponent);
            splineTraverserComponent.SplineTraverser.OnSplineEndReached += (e) =>
            {
                DestroyEntity(clone);
            };
        }

        private void DestroyEntity(Entity clone)
        {
            Entity.Scene.Entities.Remove(clone);
            clone = null;
        }
    }
}