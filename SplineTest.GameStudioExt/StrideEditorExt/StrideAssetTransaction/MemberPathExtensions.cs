using Stride.Core.Reflection;
using System.Diagnostics;

namespace SplineTest.GameStudioExt.StrideEditorExt.StrideAssetTransaction;

public static class MemberPathExtensions
{
    public static bool IsSameOrSubpath(MemberPath basePath, MemberPath subpath)
    {
        bool isSamePath = basePath.Match(subpath);
        if (isSamePath)
        {
            return true;
        }
        bool isSubpath = IsSubpath(basePath, subpath);
        return isSubpath;
    }

    public static bool IsSubpath(MemberPath basePath, MemberPath subpath)
    {
        var basePathComponents = basePath.Decompose();
        var subpathComponents = subpath.Decompose();
        if (basePathComponents.Count >= subpathComponents.Count)
        {
            return false;
        }

        for (int i = 0; i < basePathComponents.Count; i++)
        {
            var basePathComp = basePathComponents[i];
            var subpathComp = subpathComponents[i];
            if (!basePathComp.Equals(subpathComp))
            {
                return false;
            }
        }
        return true;
    }

    internal static MemberPath BuildReroutedPath(MemberPath pathToReroute, MemberPath originalBasePath, MemberPath newBasePath)
    {
        EnsureIsSameOrSubpath(originalBasePath, pathToReroute);

        var pathToRerouteComponents = pathToReroute.Decompose();
        var originalBasePathComponents = originalBasePath.Decompose();
        var newBasePathComponents = newBasePath.Decompose();

        var reroutedPath = new MemberPath(newBasePathComponents.Count + (pathToRerouteComponents.Count - originalBasePathComponents.Count));
        // Add the rerouted path sub-paths
        foreach (var pathItem in newBasePathComponents)
        {
            PushPathItem(reroutedPath, pathItem);
        }
        // Add the remain sub-paths
        for (int i = originalBasePathComponents.Count; i < pathToRerouteComponents.Count; i++)
        {
            var pathItem = pathToRerouteComponents[i];
            PushPathItem(reroutedPath, pathItem);
        }

        return reroutedPath;

        static void PushPathItem(MemberPath path, MemberPath.MemberPathItem pathItem)
        {
            if (pathItem is MemberPath.SpecialMemberPathItemBase)
            {
                if (pathItem.TypeDescriptor is null)
                {
                    throw new NotSupportedException($"TypeDescriptor was expected for path item: {pathItem.GetType().Name}");
                }
                var pathItemIndex = pathItem.GetIndex();
                if (pathItemIndex is null)
                {
                    throw new NotSupportedException($"Key or index was expected for path item: {pathItem.GetType().Name}");
                }
                path.Push(pathItem.TypeDescriptor, key: pathItemIndex);
            }
            else
            {
                path.Push(pathItem.MemberDescriptor);
            }
        }
    }

    [Conditional("DEBUG")]
    private static void EnsureIsSameOrSubpath(MemberPath basePath, MemberPath subpath)
    {
        if (!IsSameOrSubpath(basePath, subpath))
        {
            throw new ArgumentException($"subpath '{subpath}' is not part of basePath '{basePath}'.");
        }
    }
}
