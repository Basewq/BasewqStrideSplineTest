using Stride.Engine;
using Stride.Engine.Splines.Components;
using Stride.Engine.Splines.Models;
using Stride.Particles.Components;

namespace SplineTools
{
    public class SplineTraverserEventsDemo : StartupScript
    {
        public Entity SplineEndReachedParticle;
        public Entity SplineNodeReachedParticle;
        public bool ReverseOnEnd;
        private SplineTraverserComponent traverserComponent;

        public override void Start()
        {
            traverserComponent = Entity.Get<SplineTraverserComponent>();

            if (traverserComponent != null)
            {
                traverserComponent.SplineTraverser.OnSplineEndReached += SplineTraverser_OnSplineEndReached;
                traverserComponent.SplineTraverser.OnSplineNodeReached += SplineTraverser_OnSplineNodeReached;
            }
        }

        private void SplineTraverser_OnSplineEndReached(SplineNode splineNode)
        {
            CloneParticleAndEnabled(SplineEndReachedParticle);

            if (ReverseOnEnd)
            {
                traverserComponent.SplineTraverser.Speed *= -1;
                traverserComponent.SplineTraverser.IsMoving = true;
            }
        }

        private void SplineTraverser_OnSplineNodeReached(SplineNode splineNode)
        {
            CloneParticleAndEnabled(SplineNodeReachedParticle);
        }

        private void CloneParticleAndEnabled(Entity particleEntity)
        {
            var particleEntityClone = particleEntity.Clone();
            Entity.AddChild(particleEntityClone);
            particleEntityClone.Get<ParticleSystemComponent>().Enabled = true;
        }
    }
}
