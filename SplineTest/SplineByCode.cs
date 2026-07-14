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
        private bool currentShowCurve = true;
        private bool currentIsClosedLoop = true;
        private bool currentShowBoundingBox = true;
        private bool currentShowUpDirections = false;
        private bool currentShowTangents = false;
        private bool currentShowControlPoints = true;

        public override void Start()
        {
            random = new Random((int)Game.TargetElapsedTime.TotalMilliseconds);
            GenerateSpline();
        }

        private void GenerateSpline()
        {
            Span<Vector3> controlPointPositions = stackalloc Vector3[]
            {
                new (Random(-4, 4), 1, 0),
                new (0, 2, Random(-2, 2)),
                new (-2, 1, Random(-2, 2))
            };

            Span<Vector3> tangents = stackalloc Vector3[]
            {
                new (Random(-2, 2), Random(-2, 2), Random(0,  3)),  // CtrlPt1 - Out
                new (Random(-3, 3), Random(-2, 2), Random(-3, 0)),  // CtrlPt1 - In
                new (Random(-1, 4), Random(-2, 2), Random(0,  3)),  // CtrlPt2 - Out
                new (Random(-2, 2), Random(-2, 2), Random(-3, 3)),  // CtrlPt2 - In
                new (Random(-1, 3), Random(-2, 2), Random(-3, 0)),  // CtrlPt3 - Out
                new (Random(-4, 1), Random(-2, 2), Random(0,  3))   // CtrlPt3 - In
            };

            splineComponent = new SplineComponent();
            splineComponent.Spline.IsClosedLoop = currentIsClosedLoop;
            Entity.Add(splineComponent);

            for (int i = 0; i < controlPointPositions.Length; i++)
            {
                splineComponent.Spline.Add(new SplineControlPoint
                {
                    Position = controlPointPositions[i],
                    TangentIn = tangents[i * 2],
                    TangentOut = tangents[i * 2 + 1],
                });
            }

            // We use spline render settings if we want to view our spline in the game
            splineComponent.DebugRenderSettings.ShowCurves = true;
            splineComponent.DebugRenderSettings.ShowBoundingBox = currentShowBoundingBox;
            splineComponent.DebugRenderSettings.ShowUpDirections = currentShowUpDirections;
            splineComponent.DebugRenderSettings.ShowTangents = currentShowTangents;
            splineComponent.DebugRenderSettings.ShowControlPoints = currentShowControlPoints;
            splineComponent.DebugRenderSettings.CurveColor = SplineColor;
            splineComponent.DebugRenderSettings.BoundingBoxColor = BoundingBoxColor;
        }

        public override void Update()
        {
            const int HelpTextStartX = 800;
            DebugText.Print($"Press Space to create spline", new Int2(HelpTextStartX, 20));
            DebugText.Print($"Press L to toggle spline 'IsClosedLoop'", new Int2(HelpTextStartX, 40));
            DebugText.Print($"Press B to toggle spline 'ShowBoundingBox'", new Int2(HelpTextStartX, 60));
            DebugText.Print($"Press U to toggle spline 'ShowUpDirections'", new Int2(HelpTextStartX, 80));
            DebugText.Print($"Press T to toggle spline 'ShowTangents'", new Int2(HelpTextStartX, 100));
            DebugText.Print($"Press P to toggle spline 'ShowControlPoints'", new Int2(HelpTextStartX, 120));

            //Generate a new spline by pressing space
            if (Input.IsKeyPressed(Keys.Space))
            {
                Entity.Remove(splineComponent);
                GenerateSpline();
            }

            if (Input.IsKeyPressed(Keys.L))
            {
                splineComponent.Spline.IsClosedLoop = currentIsClosedLoop = !currentIsClosedLoop;
            }
            if (Input.IsKeyPressed(Keys.B))
            {
                splineComponent.DebugRenderSettings.ShowBoundingBox = currentShowBoundingBox = !currentShowBoundingBox;
            }
            if (Input.IsKeyPressed(Keys.U))
            {
                splineComponent.DebugRenderSettings.ShowUpDirections = currentShowUpDirections = !currentShowUpDirections;
            }
            if (Input.IsKeyPressed(Keys.T))
            {
                splineComponent.DebugRenderSettings.ShowTangents = currentShowTangents = !currentShowTangents;
            }
            if (Input.IsKeyPressed(Keys.P))
            {
                splineComponent.DebugRenderSettings.ShowControlPoints = currentShowControlPoints = !currentShowControlPoints;
            }
        }

        private int Random(int min, int max)
        {
            return random.Next(min, max);
        }
    }
}
