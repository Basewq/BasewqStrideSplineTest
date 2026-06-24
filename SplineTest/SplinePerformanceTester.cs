using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Splines.Components;
using Stride.Rendering;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Stride.Input;

namespace SplineTools
{
    public class SplinesPerformanceTest : SyncScript
    {
        public int splineAmount = 1000;
        public int splineNodesPerSpline = 1000;
        public int splineSegmentCount = 100;
        public Vector3 splineGenerationArea = new(1000, 200, 1000);
        public Vector3 TangentOffet = new(2, 2, 2);
        public bool UseStructuredSpline = false;
        public float StructureOffset = 20.0f;


        public List<Material> splineMaterials = new List<Material>();
        private SplineComponent splineComponent;
        private Random random;
        public Entity[] splineEntities;
        public SplineComponent[] splineComponents;

        private Stopwatch stopwatch;

        public override void Start()
        {
            splineEntities = new Entity[splineAmount];
            splineComponents = new SplineComponent[splineAmount];
            random = new Random((int)Game.TargetElapsedTime.TotalMilliseconds);
            GenerateSplines();
        }

        private void GenerateSplines()
        {
            stopwatch = new Stopwatch();
            stopwatch.Start();

            ClearSplines();
            for (var i = 0; i < splineAmount; i++)
            {
                GenerateSpline(i);
            }

            stopwatch.Stop();
        }

        private void GenerateSpline(int iteration)
        {
            var nodePositions = new Vector3[splineNodesPerSpline];
            var division = splineGenerationArea / splineNodesPerSpline;
            for (var i = 0; i < splineNodesPerSpline; i++)
            {
                if (UseStructuredSpline)
                {
                    var structuredArea = new Vector3(splineGenerationArea.X, splineGenerationArea.Y, division.Z * i);
                    nodePositions[i] = structuredArea;
                }
                else
                    nodePositions[i] = RandomVector3(splineGenerationArea);
            }

            //In and Out tangent
            var tangents = new Vector3[splineNodesPerSpline * 2];
            for (var i = 0; i < splineNodesPerSpline * 2; i++)
            {
                tangents[i] = RandomOffsetVector3();
            }

            var splineEntity = new Entity($"Spline{iteration}");
            splineComponent = new SplineComponent
            {
                Loop = false
            };

            splineEntity.Add(splineComponent);
            Entity.Scene.Entities.Add(splineEntity);
            splineEntity.Transform.Position = Entity.Transform.WorldMatrix.TranslationVector;
            splineEntity.Transform.Position.X += iteration * StructureOffset;

            splineEntities[iteration] = splineEntity;
            splineComponents[iteration] = splineComponent;

            for (var i = 0; i < nodePositions.Length; i++)
            {
                var nodeEntity = new Entity("node" + i, nodePositions[i]);
                var nodeComponent = new SplineNodeComponent(splineSegmentCount, tangents[i * 2], tangents[i * 2 + 1]);
                nodeEntity.Add(nodeComponent);

                splineEntity.AddChild(nodeEntity);
                splineComponent.Nodes.Add(nodeComponent);
            }

            // We use a spline renderer if we want to view our spline in the game
            var materialIndex = iteration % splineMaterials.Count;
            var material = splineMaterials[materialIndex];
            splineComponent.RenderSettings.ShowBoundingBox = true;
            splineComponent.RenderSettings.ShowSegments = true;
            splineComponent.RenderSettings.BoundingBoxMaterial = material;
            splineComponent.RenderSettings.SegmentsMaterial = material;
        }

        public override void Update()
        {
            DebugText.Print($"Press C to clean splines", new Int2(1600, 20));
            DebugText.Print($"Press G to generate {splineAmount} splines ", new Int2(1600, 40));
            DebugText.Print($"Press B to toggle bounding box ",
                new Int2(1600, 80));

            //Clean existing splines
            if (Input.IsKeyPressed(Keys.C))
            {
                ClearSplines();
            }

            //Generate new splines
            if (Input.IsKeyPressed(Keys.G))
            {
                GenerateSplines();
            }

            //Generate new splines
            if (Input.IsKeyPressed(Keys.B))
            {
                ToggleBoundingBox();
            }
        }

        private void ToggleBoundingBox()
        {
            var bb = splineComponents[0].RenderSettings.ShowBoundingBox;
            for (var i = 0; i < splineAmount; i++)
            {
                splineComponents[i].RenderSettings.ShowBoundingBox = !bb;
            }
        }

        private void ClearSplines()
        {
            for (var i = 0; i < splineAmount; i++)
            {
                Entity.Scene.Entities.Remove(splineEntities[i]);
            }
        }

        private Vector3 RandomVector3(Vector3 generationArea)
        {
            return new(
                Random(-(int)generationArea.X, (int)generationArea.X),
                Random(-(int)generationArea.Y, (int)generationArea.Y),
                Random(-(int)generationArea.Z, (int)generationArea.Z));
        }

        private Vector3 RandomOffsetVector3()
        {
            if (UseStructuredSpline)
            {
                return new Vector3(
                    Random(-(int)TangentOffet.X, (int)TangentOffet.X),
                    Random(-(int)TangentOffet.Y, (int)TangentOffet.Y),
                    TangentOffet.Z);
            }

            return new Vector3(
                Random(-(int)TangentOffet.X, (int)TangentOffet.X),
                Random(-(int)TangentOffet.Y, (int)TangentOffet.Y),
                Random(-(int)TangentOffet.Z, (int)TangentOffet.Z));
        }

        private int Random(int min, int max)
        {
            return random.Next(min, max);
        }
    }
}