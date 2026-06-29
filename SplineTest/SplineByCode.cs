using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Splines.Components;
using Stride.Engine.Splines.Models;
using Stride.Input;
using Stride.Rendering;
using System;

namespace SplineTools
{
    public class SplineByCode : SyncScript
    {
        public Material SplineMaterial;
        public Material BoundingBoxMaterial;
        public Color SplineColor = Color.Aqua;
        public Color BoundingBoxColor = Color.Orange;

        private SplineComponent splineComponent;
        private Random random;
        private bool toggleLoop = true;
        private bool toggleBoundingBox = true;

        public override void Start()
        {
            random = new Random((int)Game.TargetElapsedTime.TotalMilliseconds);
            GenerateSpline();
        }

        private void GenerateSpline()
        {
            var controlPointPositions = new Vector3[]
            {
                new (Random(-4, 4), 1, 0),
                new (0, 2, Random(-2, 2)),
                new (-2, 1, Random(-2, 2))
            };

            var tangents = new Vector3[]
            {
                new (Random(-2, 2), Random(-2, 2), Random(0,  3)), //Node 1 - out
                new (Random(-3, 3), Random(-2, 2), Random(-3, 0)), //Node 1 - in
                new (Random(-1, 4), Random(-2, 2), Random(0,  3)), //Node 2 - out
                new (Random(-2, 2), Random(-2, 2), Random(-3, 3)), //Node 2 - in
                new (Random(-1, 3), Random(-2, 2), Random(-3, 0)), //Node 3 - out
                new (Random(-4, 1), Random(-2, 2), Random(0,  3))  //Node 3 - in
            };

            splineComponent = new SplineComponent();
            splineComponent.Spline.IsClosedLoop = toggleLoop;
            Entity.Add(splineComponent);

            for (var i = 0; i < controlPointPositions.Length; i++)
            {
                splineComponent.Spline.Add(new SplineControlPoint
                {
                    Position = controlPointPositions[i],
                    TangentIn = tangents[i * 2],
                    TangentOut = tangents[i * 2 + 1],
                });
                //var controlPointEntity = new Entity("controlPoint"+i,controlPointPositions[i]);
                //var controlPointComponent = new SplineNodeComponent(50, tangents[i * 2], tangents[i * 2 + 1]);
                //controlPointEntity.Add(controlPointComponent);

                //Entity.AddChild(controlPointEntity);
                //SplineComponent.Nodes.Add(controlPointComponent);
            }

            // We use spline render settings if we want to view our spline in the game
            splineComponent.DebugRenderSettings.ShowCurves = true;
            splineComponent.DebugRenderSettings.ShowBoundingBox = toggleBoundingBox;
            splineComponent.DebugRenderSettings.CurveColor = SplineColor;
            splineComponent.DebugRenderSettings.BoundingBoxColor = BoundingBoxColor;
        }

        public override void Update()
        {
            const int HelpTextStartX = 800;
            DebugText.Print($"Press Space to create spline", new Int2(HelpTextStartX, 20));
            DebugText.Print($"Press L to toggle spline 'looping'", new Int2(HelpTextStartX, 40));
            DebugText.Print($"Press B to toggle spline 'Boundingbox render'", new Int2(HelpTextStartX, 60));

            //Generate a new spline by pressing space
            if (Input.IsKeyPressed(Keys.Space))
            {
                Entity.Remove(splineComponent);
                GenerateSpline();
            }

            //Press L to toggle Looping of the spline
            if (Input.IsKeyPressed(Keys.L))
            {
                splineComponent.Spline.IsClosedLoop = toggleLoop = !toggleLoop;
            }

            //Press B to toggle Bounding box of the spline
            if (Input.IsKeyPressed(Keys.B))
            {
                splineComponent.DebugRenderSettings.ShowBoundingBox = toggleBoundingBox = !toggleBoundingBox;
            }
        }

        private int Random(int min, int max)
        {
            return random.Next(min, max);
        }
    }
}
