using System;
using d60.Cirqus.Aggregates;
using d60.Cirqus.Events;

namespace d60.Cirqus.Tests.Contracts.Views.Models.LoadingStuffDuringViewLocation
{
    public class Node : AggregateRoot, IEmit<NodeAttachedToParentNode>, IEmit<NodeCreated>
    {
        public string ParentNodeId { get; private set; }

        public void AttachTo(Node parentNode)
        {
            if (ParentNodeId != null)
            {
                throw new InvalidOperationException($"Cannot attach node {Id} to {parentNode.Id} because it's already attached to {ParentNodeId}");
            }
            Emit(new NodeAttachedToParentNode { ParentNodeId = parentNode.Id });
        }

        public void Apply(NodeAttachedToParentNode e)
        {
            ParentNodeId = e.ParentNodeId;
        }

        protected override void Created()
        {
            Emit(new NodeCreated());
        }

        public void Apply(NodeCreated e)
        {
        }
    }
}