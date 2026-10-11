using System.Collections.Generic;
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class RouteContextTests
    {
        [Fact]
        public void Wires_are_classified_by_their_ends()
        {
            var b = new SceneBuilder().Node("S", 0, 0).Node("T", 300, 0);
            b.Wire("S", "T", new Pt(80, 20), new Pt(300, 60), WireStyle.Diagonal);    // lane
            b.Wire("S", "T", new Pt(80, 20), new Pt(300, 20.3), WireStyle.Diagonal);  // straight
            b.Wire("S", "T", new Pt(80, 20), new Pt(87, 60), WireStyle.Diagonal);     // dx 7: back
            var ctx = new RouteContext(b.Build(), null);
            Assert.Equal(new[] { WireKind.Lane, WireKind.Straight, WireKind.Back },
                new[] { ctx.Jobs[0].Kind, ctx.Jobs[1].Kind, ctx.Jobs[2].Kind });
        }

        [Fact]
        public void Jobs_know_their_nodes_and_index()
        {
            var b = new SceneBuilder().Node("S", 0, 0).Node("T", 300, 0);
            b.Wire("S", "T", WireStyle.Orthogonal);
            var ctx = new RouteContext(b.Build(), null);
            var j = ctx.Jobs[0];
            Assert.Equal(0, j.Index);
            Assert.Equal(new Rect(0, 0, 80, 40).ToString(), j.A.ToString());
            Assert.Equal(new Rect(300, 0, 380, 40).ToString(), j.B.ToString());
            Assert.Equal(ctx.NodeRects[j.ANode].ToString(), j.A.ToString());
        }

        [Fact]
        public void Previous_lanes_come_from_the_state()
        {
            var b = new SceneBuilder().Node("S", 0, 0).Node("T", 300, 0);
            string id = b.Wire("S", "T", WireStyle.Orthogonal);
            var state = new RouterState();
            state.Replace(new[] { new KeyValuePair<string, double>(id, 150) });
            Assert.Equal(150, new RouteContext(b.Build(), state).Jobs[0].PrevLane);
            Assert.Null(new RouteContext(b.Build(), null).Jobs[0].PrevLane);
        }

        [Fact]
        public void A_missing_node_becomes_a_point_at_the_port()
        {
            var b = new SceneBuilder().Node("S", 0, 0);
            b.Wire("S", "gone", new Pt(80, 20), new Pt(300, 60), WireStyle.Diagonal);
            var j = new RouteContext(b.Build(), null).Jobs[0];
            Assert.Equal(-1, j.BNode);
            Assert.Equal(new Rect(300, 60, 300, 60).ToString(), j.B.ToString());
        }
    }
}
