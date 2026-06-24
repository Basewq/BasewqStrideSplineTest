// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Reflection;

namespace SplineTest.Splines;

internal class LibraryModule
{
    [ModuleInitializer]
    public static void Initialize()
    {
        const string AssemblyCommonCategoriesName = "assets";
        // Can't use Stride.Core.Reflection.AssemblyCommonCategories.Assets because
        // this class is duplicated in two different dlls causing namespace conflict
        AssemblyRegistry.Register(typeof(LibraryModule).Assembly, AssemblyCommonCategoriesName);
    }
}
