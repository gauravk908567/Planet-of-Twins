using NUnit.Framework.Interfaces;
using System.Collections;

namespace BehaviourTree
{
    public interface IBTFlowNode : IBTNode, IEnumerable
    {
        IBTNode AddChild(IBTNode InNode);
    }
}