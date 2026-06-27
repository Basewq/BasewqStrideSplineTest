using Stride.Engine;
using Stride.Engine.Splines.Components;
using Stride.Engine.Splines.Models;
using Stride.Particles.Components;
using System.Diagnostics;

namespace SplineTools
{
    public class SplineTraverserEventsDemo : StartupScript
    {
        public Entity SplineEndReachedParticle;
        public Entity SplineControlPointReachedParticle;
        public bool ReverseOnEnd;
        private SplineTraverserComponent traverserComponent;

        public override void Start()
        {
            traverserComponent = Entity.Get<SplineTraverserComponent>();

            if (traverserComponent is not null)
            {
                traverserComponent.SplineTraverser.SplineEndReached += SplineTraverser_OnSplineEndReached;
                traverserComponent.SplineTraverser.SplineControlPointReached += SplineTraverser_OnSplineControlPointReached;
            }
        }

        private void SplineTraverser_OnSplineEndReached(SplineControlPoint controlPoint)
        {
            Debug.WriteLine($"{Entity.Name} - Spline End Reached: {controlPoint.Position}");
            CloneParticleAndEnabled(SplineEndReachedParticle);

            if (ReverseOnEnd)
            {
                traverserComponent.SplineTraverser.Speed *= -1;
                traverserComponent.SplineTraverser.IsMoving = true;
            }
        }

        private void SplineTraverser_OnSplineControlPointReached(int controlPointIndex, SplineControlPoint controlPoint)
        {
            Debug.WriteLine($"{Entity.Name} - Spline Point Reached: {controlPointIndex} - {controlPoint.Position}");
            CloneParticleAndEnabled(SplineControlPointReachedParticle);
        }

        private void CloneParticleAndEnabled(Entity particleEntity)
        {
            var particleEntityClone = particleEntity.Clone();
            Entity.AddChild(particleEntityClone);
            particleEntityClone.Get<ParticleSystemComponent>().Enabled = true;
        }
    }
}
