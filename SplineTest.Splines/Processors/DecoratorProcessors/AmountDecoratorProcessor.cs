// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Engine.Splines.Components;
using Stride.Engine.Splines.Models;
using Stride.Engine.Splines.Models.Decorators;

namespace Stride.Engine.Splines.Processors.DecoratorProcessors;

public class AmountDecoratorProcessor : BaseSplineDecoratorProcessor
{
    /// <summary>
    /// Decorates the given spline from the Spline component with a fixed amount of decorations, which are evenly distributed along the spline
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
            || component.DecoratorSettings is not SplineAmountDecoratorSettings amountDecorator || amountDecorator.Amount <= 0)
        {
            return;
        }

        splineEval ??= new SplineEvaluator(spline);
        float totalSplineDistance = splineEval.GetTotalDistance();
        if (totalSplineDistance <= 0)
        {
            return;
        }

        var random = new Random();
        for (int iteration = 0; iteration < amountDecorator.Amount; iteration++)
        {
            float splineT = iteration / (float)(amountDecorator.Amount - 1);
            CreateInstance(component, iteration, splineEval, splineT, random);
        }
    }
}
