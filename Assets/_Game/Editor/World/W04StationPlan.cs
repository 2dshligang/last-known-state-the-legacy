using System;
using System.Collections.Generic;
using LKS.World;
using UnityEngine;

namespace LKS.Editor.World
{
    internal static class W04StationPlan
    {
        internal enum Kind { Point, Box, Anchor, Label, Camera, Light, DirectionalLight }
        internal sealed class Node
        {
            public StableId Id;
            public Kind Type;
            public Vector3 Position;
            public Vector3 Size;
            public Vector3 Facing;
            public string Material;
            public string Text;
            public StableId Target;
            public bool Collides;
        }

        internal static Vector3 Entry(StationLayout layout) => new Vector3(0, 0, layout.InteriorEnd + 1.1f);
        internal static Vector3 LoopNear(StationLayout layout) => new Vector3(0, 0, layout.InteriorEnd + 2.0f);
        internal static Vector3 LoopFar(StationLayout layout) => new Vector3(0, 0, layout.InteriorEnd + layout.ExteriorDepth - 1.5f);
        internal static float LoopSide(StationLayout layout) => layout.ExteriorWidth / 2 - 1.3f;

        internal static List<Node> Build(StationLayout layout)
        {
            layout.Validate();
            var nodes = new List<Node>();
            float bay = layout.BayLength;
            float width = layout.InnerWidth;
            float height = layout.CeilingHeight;
            float end = layout.InteriorEnd;
            float depth = layout.ExteriorDepth;
            float outside = layout.ExteriorWidth;
            const float wall = .20f;
            var bayNames = new[] { "arrival", "power", "archive", "shelter", "laboratory", "exit" };
            var titles = new[] { "01  ARRIVAL", "02  POWER", "03  ECHO ARCHIVE",
                "04  SHELTER", "05  LABORATORY", "06  EXTERIOR LOOP" };
            for (int i = 0; i < bayNames.Length; i++)
            {
                string id = "bay." + bayNames[i];
                float start = i * bay;
                float mid = start + bay / 2;
                Box(id + ".floor", P(0, -.10f, mid), P(width + 2 * wall, .20f, bay), "Floor");
                Box(id + ".ceiling", P(0, height + .10f, mid), P(width + 2 * wall, .20f, bay), "Metal");
                Box(id + ".wall-left", P(-width/2-wall/2, height/2, mid), P(wall, height, bay), "Shell");
                Box(id + ".wall-right", P(width/2+wall/2, height/2, mid), P(wall, height, bay), "Shell");
                Box(id + ".guide", P(0, .002f, mid), P(.055f, .004f, bay), "Ochre", false);
                float signZ = i == 0 ? start + .18f : start - .14f;
                Box(id + ".sign", P(0, 2.58f, signZ), P(2.21f, .29f, .07f), "Metal", false);
                Label(id + ".label", titles[i], P(0, 2.58f, signZ - .041f), Vector3.forward, .15f);
                var light = New(id + ".light", Kind.Light, P(0, height - .55f, mid));
                light.Size = P(4.1f, 7, 0);
                if (i > 0) Opening(id + ".entry", start, width, height, 1.6f);
            }
            Box("station.rear-wall", P(0, height/2, -.10f), P(width + 2*wall, height, wall), "Shell");
            Opening("station.exterior-entry", end, width, height, 1.6f);
            // This heading is seen on the return route through the opposite face.
            Box("return.sign", P(0, 2.62f, end+.14f), P(2.4f,.29f,.07f), "Metal", false);
            Label("return.label", "RETURN  /  LAB", P(0,2.62f,end+.181f), Vector3.back, .15f);

            float left = -width/2 + .36f;
            float right = width/2 - .36f;
            Fixture("arrival-console", "ARRIVAL CHECK", left, .55f*bay, 1.10f, Vector3.left, "quest.arrival");
            Fixture("power-console", "POWER CONTROL", left, 1.44f*bay, 1.10f, Vector3.left, "quest.safe-power");
            Fixture("power-recovery", "RESTORE SAFE POWER", right, 1.68f*bay, 1.10f, Vector3.right, "target.power-recovery");
            Fixture("echo", "ECHO ARCHIVE", right, 2.54f*bay, 1.36f, Vector3.right, "quest.echo-evidence");
            Fixture("seedling", "SEEDLING CABINET", left, 3.62f*bay, 1.42f, Vector3.left);
            Box("fixture.shelter-bench", P(right,.27f,3.50f*bay), P(.54f,.54f,1.3f), "Metal");
            SideLabel("shelter", "SHELTER", right-.36f, 1.55f, 3.50f*bay, Vector3.right);
            Fixture("analysis", "SAMPLE ANALYSIS", left, 4.33f*bay, 1.10f, Vector3.left, "quest.analysis");
            Fixture("report", "RESEARCH REPORT", right, 4.49f*bay, 1.10f, Vector3.right, "quest.report");
            Fixture("photo", "DAD'S PHOTO", left, 4.79f*bay, 1.10f, Vector3.left, "quest.ending");

            Box("exterior.ground", P(0,-.15f,end+depth/2), P(outside+.4f,.30f,depth+.4f), "Dust");
            Box("exterior.back-wall", P(0,1.55f,end+depth+.10f), P(outside+.4f,3.1f,.2f), "Dust");
            Box("exterior.left-wall", P(-outside/2-.10f,1.55f,end+depth/2), P(.20f,3.1f,depth+.4f), "Dust");
            Box("exterior.right-wall", P(outside/2+.10f,1.55f,end+depth/2), P(.20f,3.1f,depth+.4f), "Dust");
            float shoulder = (outside-width)/2;
            Box("exterior.front-left", P(-width/2-shoulder/2,1.55f,end-.10f), P(shoulder,3.1f,.2f), "Dust");
            Box("exterior.front-right", P(width/2+shoulder/2,1.55f,end-.10f), P(shoulder,3.1f,.2f), "Dust");
            Box("exterior.sample-island", P(0,.31f,end+depth/2), P(outside*.42f,.62f,depth*.37f), "Dust");
            Box("exterior.guide-left", P(-LoopSide(layout),.003f,end+depth/2), P(.055f,.006f,depth-3), "Ochre", false);
            Box("exterior.guide-right", P(LoopSide(layout),.003f,end+depth/2), P(.055f,.006f,depth-3), "Ochre", false);
            var sun = New("exterior.sun", Kind.DirectionalLight, P(0, 6, end + depth/2));
            sun.Size = P(1.25f, 0, 0);
            sun.Facing = new Vector3(.30f,-.87f,-.39f);
            var far = LoopFar(layout);
            Box("exterior.sample-box", P(0,.46f,far.z+.69f), P(1.05f,.92f,.43f), "Metal");
            Box("exterior.sample-sign", P(0,1.24f,far.z+.69f), P(1.7f,.27f,.1f), "Metal");
            Label("exterior.sample-label", "SAMPLE PICKUP", P(0,1.24f,far.z+.632f), Vector3.forward, .14f);
            Anchor("sampling", "quest.sampling", P(0,.76f,far.z+.46f), far);
            New("station.spawn", Kind.Point, layout.Spawn);
            var camera = New("station.preview-camera", Kind.Camera, layout.Spawn + P(0,1.62f,0));
            camera.Facing = P(0,-.015f,1);
            return nodes;

            Node New(string id, Kind kind, Vector3 position)
            {
                var node = new Node { Id = new StableId(id), Type = kind, Position = position };
                nodes.Add(node);
                return node;
            }
            void Box(string id, Vector3 position, Vector3 size, string material, bool collides = true)
            {
                var node = New(id, Kind.Box, position);
                node.Size = size;
                node.Material = material;
                node.Collides = collides;
            }
            void Opening(string id, float z, float span, float top, float clear)
            {
                float jamb = (span-clear)/2;
                Box(id+".left", P(-clear/2-jamb/2,1.1f,z), P(jamb,2.2f,wall), "Ochre");
                Box(id+".right", P(clear/2+jamb/2,1.1f,z), P(jamb,2.2f,wall), "Ochre");
                Box(id+".head", P(0,(top+2.2f)/2,z), P(span,top-2.2f,wall), "Shell");
            }
            void Label(string id, string text, Vector3 position, Vector3 facing, float letterSize)
            {
                var node = New(id, Kind.Label, position);
                node.Text = text;
                node.Facing = facing;
                node.Size = P(letterSize,0,0);
            }
            void SideLabel(string id, string text, float x, float y, float z, Vector3 facing)
            {
                Box("fixture."+id+".sign", P(x,y,z), P(.06f,.23f,1.2f), "Metal", false);
                Label("fixture."+id+".label", text, P(x,y,z)-facing*.037f, facing,.105f);
            }
            void Fixture(string id, string text, float x, float z, float top, Vector3 facing, string target = null)
            {
                Box("fixture."+id, P(x,top/2,z), P(.48f,top,.64f), "Metal");
                Box("fixture."+id+".face", P(x,top-.14f,z)-facing*.246f, P(.035f,.20f,.54f), "Ochre", false);
                SideLabel(id, text, x - facing.x*.25f, top+.19f, z, facing);
                if (target != null)
                    Anchor(id, target, P(x,top-.14f,z)-facing*.28f, P(x,0,z)-facing*1.13f);
            }
            void Anchor(string id, string target, Vector3 position, Vector3 approach)
            {
                New("approach."+id, Kind.Point, approach);
                var node = New("anchor."+id, Kind.Anchor, position);
                node.Target = new StableId(target);
            }
        }

        private static Vector3 P(float x, float y, float z) => new Vector3(x,y,z);
    }
}
