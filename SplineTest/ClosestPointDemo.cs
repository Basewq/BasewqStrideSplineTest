using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Splines.Components;
using Stride.Input;

namespace SplineTools
{
    public class ClosestPointDemo : SyncScript
    {
        public float MoveSpeed = 4.0f;
        public SplineComponent SplineComponent;
        public Entity ClosestPointOrb;

        public override void Start()
        {
        }

        public override void Update()
        {
            float deltaTime;
            Vector3 dir;
            UpdateKeyboardInput(out deltaTime, out dir);

            if (SplineComponent?.Spline is not null
                && dir.LengthSquared() > 0)
            {
                // Update movement
                dir = Vector3.Normalize(dir);
                Entity.Transform.Position += dir * MoveSpeed * deltaTime;

                var splineRootPosition = SplineComponent.Entity.Transform.Position;
                // Show closest point on spline
                var splineLocalPos = Entity.Transform.Position - splineRootPosition;
                var closestPositionInfo = SplineComponent.SplineEvaluator.FindClosestPoint(splineLocalPos);
                ClosestPointOrb.Transform.UseTRS = false;
                ClosestPointOrb.Transform.WorldMatrix.TranslationVector = closestPositionInfo.Position + splineRootPosition;
                ClosestPointOrb.Transform.UpdateLocalFromWorld();
            }
        }

        private void UpdateKeyboardInput(out float deltaTime, out Vector3 dir)
        {
            const int HelpTextStartX = 800;
            DebugText.Print($"Use WASD to move the sphere around.", new Int2(HelpTextStartX, 20));
            DebugText.Print($"The red sphere indicates closest point on the spline", new Int2(HelpTextStartX, 40));

            deltaTime = (float)Game.UpdateTime.Elapsed.TotalSeconds;
            dir = new();
            if (Input.HasKeyboard)
            {
                // Move with keyboard
                // Forward/Backward
                if (Input.IsKeyDown(Keys.W) || Input.IsKeyDown(Keys.Up))
                {
                    dir.Z += 1;
                }
                if (Input.IsKeyDown(Keys.S) || Input.IsKeyDown(Keys.Down))
                {
                    dir.Z -= 1;
                }

                // Left/Right
                if (Input.IsKeyDown(Keys.A) || Input.IsKeyDown(Keys.Left))
                {
                    dir.X += 1;
                }
                if (Input.IsKeyDown(Keys.D) || Input.IsKeyDown(Keys.Right))
                {
                    dir.X -= 1;
                }
            }
        }
    }
}
