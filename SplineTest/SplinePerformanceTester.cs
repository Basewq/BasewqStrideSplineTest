using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Splines.Components;
using Stride.Rendering;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Stride.Input;
using Stride.Engine.Splines.Models;
using Stride.Core;

namespace SplineTools;

public class SplinesPerformanceTest : SyncScript
{
    public int SplineAmount = 1000;
    public int SplineControlPointsPerSpline = 1000;
    public int SplineCurveCount = 100;
    public Vector3 SplineGenerationArea = new(1000, 200, 1000);
    public Vector3 TangentOffet = new(2, 2, 2);
    public bool UseStructuredSpline = false;
    public float StructureOffset = 20.0f;

    public List<Material> SplineMaterials = new List<Material>();
    public List<Color> SplineColors = new List<Color>();
    private SplineComponent splineComponent;
    private Random random;
    [DataMemberIgnore]
    public Entity[] SplineEntities;
    [DataMemberIgnore]
    public SplineComponent[] SplineComponents;

    private Stopwatch stopwatch;

    public override void Start()
    {
        SplineEntities = new Entity[SplineAmount];
        SplineComponents = new SplineComponent[SplineAmount];
        random = new Random((int)Game.TargetElapsedTime.TotalMilliseconds);
        GenerateSplines();
    }

    private void GenerateSplines()
    {
        stopwatch = new Stopwatch();
        stopwatch.Start();

        ClearSplines();
        for (var i = 0; i < SplineAmount; i++)
        {
            GenerateSpline(i);
        }

        stopwatch.Stop();
    }

    private void GenerateSpline(int iteration)
    {
        var controlPointPositions = new Vector3[SplineControlPointsPerSpline];
        var division = SplineGenerationArea / SplineControlPointsPerSpline;
        for (var i = 0; i < SplineControlPointsPerSpline; i++)
        {
            if (UseStructuredSpline)
            {
                var structuredArea = new Vector3(SplineGenerationArea.X, SplineGenerationArea.Y, division.Z * i);
                controlPointPositions[i] = structuredArea;
            }
            else
                controlPointPositions[i] = RandomVector3(SplineGenerationArea);
        }

        //In and Out tangent
        var tangents = new Vector3[SplineControlPointsPerSpline * 2];
        for (var i = 0; i < SplineControlPointsPerSpline * 2; i++)
        {
            tangents[i] = RandomOffsetVector3();
        }

        var splineEntity = new Entity($"Spline{iteration}");
        splineComponent = new SplineComponent();
        splineComponent.Spline.IsClosedLoop = false;

        splineEntity.Add(splineComponent);
        Entity.Scene.Entities.Add(splineEntity);
        splineEntity.Transform.Position = Entity.Transform.WorldMatrix.TranslationVector;
        splineEntity.Transform.Position.X += iteration * StructureOffset;

        SplineEntities[iteration] = splineEntity;
        SplineComponents[iteration] = splineComponent;

        for (var i = 0; i < controlPointPositions.Length; i++)
        {
            splineComponent.Spline.Add(new SplineControlPoint
            {
                Position = controlPointPositions[i],
                TangentIn = tangents[i * 2],
                TangentOut = tangents[i * 2 + 1],
            });
            (splineComponent.Spline.SplineEvaluator as SplineEvaluator)?.SampleResolutionPerCurve = SplineCurveCount;
            //var controlPointEntity = new Entity("controlPoint" + i, controlPointPositions[i]);
            //var controlPointComponent = new SplineNodeComponent(SplineCurveCount, tangents[i * 2], tangents[i * 2 + 1]);
            //controlPointEntity.Add(controlPointComponent);
            //
            //splineEntity.AddChild(controlPointEntity);
            //SplineComponent.Nodes.Add(controlPointComponent);
        }

        // We use a spline renderer if we want to view our spline in the game
        var materialIndex = iteration % SplineColors.Count;
        var color = SplineColors[materialIndex];
        splineComponent.DebugRenderSettings.ShowBoundingBox = true;
        splineComponent.DebugRenderSettings.ShowCurves = true;
        splineComponent.DebugRenderSettings.BoundingBoxColor = color;
        splineComponent.DebugRenderSettings.CurveColor = color;
    }

    public override void Update()
    {
        const int HelpTextStartX = 800;
        DebugText.Print($"Press C to clean splines", new Int2(HelpTextStartX, 20));
        DebugText.Print($"Press G to generate {SplineAmount} splines", new Int2(HelpTextStartX, 40));
        DebugText.Print($"Press B to toggle bounding box", new Int2(HelpTextStartX, 80));

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
        var bb = SplineComponents[0].DebugRenderSettings.ShowBoundingBox;
        for (var i = 0; i < SplineAmount; i++)
        {
            SplineComponents[i].DebugRenderSettings.ShowBoundingBox = !bb;
        }
    }

    private void ClearSplines()
    {
        for (var i = 0; i < SplineAmount; i++)
        {
            Entity.Scene.Entities.Remove(SplineEntities[i]);
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
