using Stride.Core.Quantum;
using Stride.Core.Reflection;

namespace SplineTest.GameStudioExt.Assets;

// Helper object to get the path of a modified AssetPropertyGraph node.
public class NodePathFinderGraphVisitor : GraphVisitorBase
{
    private readonly IGraphNode targetNode;

    public MemberPath? FoundPath = null;

    public NodePathFinderGraphVisitor(IGraphNode targetNode)
    {
        this.targetNode = targetNode;
    }

    protected override void VisitNode(IGraphNode node)
    {
        if (targetNode == node)
        {
            FoundPath = CurrentPath.ToMemberPath();
            return;
        }
        base.VisitNode(node);
    }
}
