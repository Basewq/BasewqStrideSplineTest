// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Mathematics;
using Stride.Engine.Splines.Components;
using Stride.Engine.Splines.Models;
using Stride.Engine.Splines.Models.Decorators;

namespace Stride.Engine.Splines.Processors.DecoratorProcessors;

public class IntervalDecoratorProcessor : BaseSplineDecoratorProcessor
{
    /// <summary>
    /// Decorates the given spline from the Spline component, using an interval
    /// The interval can be random
    /// Maximum number of prefabs to spawn is 1000
    /// </summary>
    public override void Decorate(SplineDecoratorComponent component)
    {
        var spline = component.SplineComponent?.Spline;
        var splineEval = component.SplineComponent?.SplineEvaluator;
        if (component.Entity is null
            || spline is null
            || spline.Count < 2
            || component.DecoratorSettings is null
            || component.DecoratorSettings.Decorations.Count == 0
            || component.DecoratorSettings is not SplineIntervalDecoratorSettings intervalDecoratorSettings)
        {
            return;
        }

        splineEval ??= new SplineEvaluator(spline);
        float totalSplineDistance = splineEval.GetTotalDistance();
        if (totalSplineDistance <= 0)
        {
            return;
        }

        var random = new Random();      // TODO should allow RNG seed
        float totalIntervalDistance = 0.0f;
        int iteration = 0;

        float minInterval = Math.Max(intervalDecoratorSettings.Interval.X, MathUtil.ZeroTolerance);     // Must always increase
        float maxInterval = Math.Max(intervalDecoratorSettings.Interval.Y, minInterval);
        float intervalRange = maxInterval - minInterval;
        while (iteration < 1000)
        {
            double nextInterval = random.NextDouble() * intervalRange + minInterval;
            totalIntervalDistance += (float)nextInterval;

            if (totalIntervalDistance > totalSplineDistance)
            {
                break;
            }

            float splineT = totalIntervalDistance / totalSplineDistance;
            CreateInstance(component, iteration, splineEval, splineT, random);

            iteration++;
        }
    }
}
