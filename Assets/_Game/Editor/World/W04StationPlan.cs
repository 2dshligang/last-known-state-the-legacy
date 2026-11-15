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
            public bool CastsShadows;
            public Color LightColor;
        }

        internal static Vector3 Entry(StationLayout layout) => new Vector3(0, 0, layout.InteriorEnd + 1.1f);
        internal static Vector3 LoopNear(StationLayout layout) => new Vector3(0, 0, layout.InteriorEnd + 2.0f);
        internal static Vector3 LoopFar(StationLayout layout) => new Vector3(0, 0, layout.InteriorEnd + layout.ExteriorDepth - 1.5f);
        internal static float LoopSide(StationLayout layout) => layout.ExteriorWidth / 2 - 1.3f;

        private static readonly (float Side, float Along, float Intensity, float Range, Color Color)[] Practicals =
        {
            ( .44f, .28f, 4.6f, 5.6f, new Color(1f, .86f, .71f)),
            ( .25f, .57f, 4.8f, 5.3f, new Color(1f, .80f, .63f)),
            (-.43f, .55f, 5.2f, 5.8f, new Color(.74f, .88f, 1f)),
            (-.49f, .53f, 3.8f, 5.2f, new Color(.88f, .96f, 1f)),
            ( .40f, .48f, 5.3f, 5.6f, new Color(.92f, .96f, 1f)),
            (-.46f, .64f, 4.4f, 5.5f, new Color(1f, .78f, .61f))
        };

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
            var titles = new[] { "01  ARRIVAL", "02  POWER", "03  ECHO ARCHIVE  >",
                "04  SHELTER  >", "05  LABORATORY", "06  AIRLOCK  >" };
            for (int i = 0; i < bayNames.Length; i++)
            {
                string id = "bay." + bayNames[i];
                float start = i * bay;
                float mid = start + bay / 2;
                Box(id + ".floor", P(0, -.10f, mid), P(width + 2 * wall, .20f, bay), "Floor", castsShadows: false);
                Box(id + ".ceiling", P(0, height + .10f, mid), P(width + 2 * wall, .20f, bay), "Metal");
                Box(id + ".wall-left", P(-width/2-wall/2, height/2, mid), P(wall, height, bay), "Shell");
                Box(id + ".wall-right", P(width/2+wall/2, height/2, mid), P(wall, height, bay), "Shell");
                Box(id + ".guide", P(0, .002f, mid), P(.055f, .004f, bay), "Ochre", false, castsShadows: false);
                float signZ = i == 0 ? start + .18f : start - .14f;
                Box(id + ".sign", P(0, 2.58f, signZ), P(2.65f, .34f, .07f), "Metal", false);
                Label(id + ".label", titles[i], P(0, 2.58f, signZ - .041f), Vector3.forward, .15f);
                var practical = Practicals[i];
                float lightZ = start + practical.Along * bay;
                var light = New(id + ".light", Kind.Light, P(practical.Side * width / 4f, height - .31f, lightZ));
                light.Facing = P(-practical.Side * .72f, -1, .12f);
                light.Size = P(practical.Intensity, practical.Range, 0);
                light.LightColor = practical.Color;
                if (i > 0) Opening(id + ".entry", start, width, height, 1.6f);
            }
            Box("station.rear-wall", P(0, height/2, -.10f), P(width + 2*wall, height, wall), "Shell");
            Opening("station.exterior-entry", end, width, height, 1.6f);
            // This heading is seen on the return route through the opposite face.
            Box("return.sign", P(0, 2.62f, end+.14f), P(2.4f,.29f,.07f), "Metal", false);
            Label("return.label", "AIRLOCK  /  TO LAB", P(0,2.62f,end+.181f), Vector3.back, .15f);

            float left = -width/2 + .36f;
            float right = width/2 - .36f;
            Fixture("arrival-console", "ARRIVAL CHECK", left, .55f*bay, 1.10f, Vector3.left, "quest.arrival");
            Fixture("power-console", "POWER CONTROL", left, 1.44f*bay, 1.10f, Vector3.left, "quest.safe-power");
            Fixture("power-recovery", "RESTORE SAFE POWER", right, 1.68f*bay, 1.10f, Vector3.right, "target.power-recovery");
            Fixture("echo", "ECHO  /  ARCHIVE", right, 2.54f*bay, 1.36f, Vector3.right, "quest.echo-evidence");
            Fixture("seedling", "SEEDLING CABINET", left, 3.62f*bay, 1.42f, Vector3.left,
                StationShelterSet.SeedlingTarget.value);
            float cabinet = 3.62f * bay;
            Box("seedling.tray", P(left+.251f,.66f,cabinet), P(.028f,.38f,.52f), "Ochre", false);
            SideLabel("seedling-status", "STANDBY", left+.29f, .64f, cabinet, Vector3.left, .078f);
            Shelter();
            Fixture("analysis", "SAMPLE ANALYSIS", left, 4.33f*bay, 1.10f, Vector3.left, "quest.analysis");
            Fixture("report", "RESEARCH REPORT", right, 4.49f*bay, 1.10f, Vector3.right, "quest.report");
            Fixture("photo", "SURFACE OVERLOOK", -outside/2+.58f, end+1.13f, 1.14f, Vector3.left, "quest.ending");

            Box("exterior.ground", P(0,-.15f,end+depth/2), P(outside+.4f,.30f,depth+.4f), "Dust");
            Box("exterior.back-wall", P(0,1.55f,end+depth+.10f), P(outside+.4f,3.1f,.2f), "Dust");
            Box("exterior.left-wall", P(-outside/2-.10f,1.55f,end+depth/2), P(.20f,3.1f,depth+.4f), "Dust");
            Box("exterior.right-wall", P(outside/2+.10f,1.55f,end+depth/2), P(.20f,3.1f,depth+.4f), "Dust");
            float shoulder = (outside-width)/2;
            Box("exterior.front-left", P(-width/2-shoulder/2,1.55f,end-.10f), P(shoulder,3.1f,.2f), "Dust");
            Box("exterior.front-right", P(width/2+shoulder/2,1.55f,end-.10f), P(shoulder,3.1f,.2f), "Dust");
            Box("exterior.sample-island", P(0,.31f,end+depth/2), P(outside*.42f,.62f,depth*.37f), "Dust");
            Box("exterior.guide-left", P(-LoopSide(layout),.003f,end+depth/2), P(.055f,.006f,depth-3), "Ochre", false, castsShadows: false);
            Box("exterior.guide-right", P(LoopSide(layout),.003f,end+depth/2), P(.055f,.006f,depth-3), "Ochre", false, castsShadows: false);
            var sun = New("exterior.sun", Kind.DirectionalLight, P(0, 6, end + depth/2));
            sun.Size = P(1.38f, 0, 0);
            sun.Facing = P(-.76f, -.60f, .25f);
            sun.LightColor = new Color(1f, .80f, .66f);
            var far = LoopFar(layout);
            Box("exterior.sample-box", P(0,.46f,far.z+.69f), P(1.05f,.92f,.43f), "Metal");
            Box("exterior.sample-sign", P(0,1.53f,far.z+.69f), P(1.7f,.55f,.1f), "Metal");
            Label("exterior.sample-label", "C  /  DUST CHECK", P(0,1.68f,far.z+.632f), Vector3.forward, .102f);
            Anchor("sampling", "quest.sampling", P(0,.76f,far.z+.46f), far);
            ExteriorSites();
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
            void Box(string id, Vector3 position, Vector3 size, string material, bool collides = true, bool castsShadows = true)
            {
                var node = New(id, Kind.Box, position);
                node.Size = size;
                node.Material = material;
                node.Collides = collides;
                node.CastsShadows = castsShadows;
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
            void SideLabel(string id, string text, float x, float y, float z, Vector3 facing, float letterSize = .105f)
            {
                Box("fixture."+id+".sign", P(x,y,z), P(.06f,.23f,1.2f), "Metal", false);
                Label("fixture."+id+".label", text, P(x,y,z)-facing*.037f, facing,letterSize);
            }
            void Fixture(string id, string text, float x, float z, float top, Vector3 facing, string target = null)
            {
                Box("fixture."+id, P(x,top/2,z), P(.48f,top,.64f), "Metal");
                Box("fixture."+id+".face", P(x,top-.14f,z)-facing*.246f, P(.035f,.20f,.54f), "Ochre", false);
                SideLabel(id, text, x - facing.x*.25f, top+.19f, z, facing);
                if (target != null)
                    Anchor(id, target, P(x,top-.14f,z)-facing*.28f, P(x,0,z)-facing*1.13f);
            }

            void ExteriorSites()
            {
                float edge = outside * .21f;
                float z = end + depth * .50f;
                for (int layer = 0; layer < 5; layer++)
                    Box("w12.bed." + layer, P(-edge + .52f, .67f + layer * .09f, z + layer * .045f),
                        P(1.24f - layer * .10f, .087f, 1.22f), layer % 2 == 0 ? "Dust" : "Ochre", false);

                Box("w12.rust.base", P(edge - .48f,.80f,z), P(1.1f,.35f,1.35f), "Dust", false);
                Box("w12.rust.face", P(edge - .45f,.98f,z+.14f), P(.89f,.31f,.82f), "Ochre", false);
                Box("w12.rust.rind", P(edge - .15f,1.13f,z+.18f), P(.30f,.13f,.55f), "Metal", false);

                FieldBoard("bedding", "A  /  LAYERED OUTCROP",
                    "Fiction: Thin bands stack like pages.\nRecord where the rock came from.",
                    -edge, z, Vector3.right, "sample.layered-mudstone");
                FieldBoard("alteration", "B  /  COMPARISON ROCK",
                    "Fiction: The rusty outer coat differs\nfrom the fresh broken face.",
                    edge, z, Vector3.left, "sample.iron-rich-rock");

                Box("w12.dust.tray", P(0,.96f,far.z+.69f), P(.64f,.07f,.30f), "Ochre", false);
                Label("w12.dust.note", "Fiction: Boot dust reached this tray.\nLeave it. Prepare a clean\nblank in the lab.",
                    P(0,1.49f,far.z+.632f), Vector3.forward, .069f);
                Label("w12.setting", "ILLUSTRATIVE MARS  /  ASTERION FIELD LOOP",
                    P(0,2.13f,end+.16f), Vector3.back, .10f);
                Label("w12.entry", "A  <  FIELD LOOP  >  B",
                    P(0,2.40f,end-.115f), Vector3.forward, .10f);
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * LoopSide(layout);
                    string suffix = side < 0 ? ".left" : ".right";
                    Box("w12.route.near" + suffix,
                        P(x/2,.005f,LoopNear(layout).z), P(Mathf.Abs(x),.008f,.055f), "Ochre", false, castsShadows: false);
                    Box("w12.route.far" + suffix,
                        P(x/2,.005f,far.z), P(Mathf.Abs(x),.008f,.055f), "Ochre", false, castsShadows: false);
                }

                float blankZ = 4.55f * bay;
                Fixture("w12-blank", "EMPTY CONTROL", left, blankZ, .84f, Vector3.left, "sample.procedural-blank");
                Label("w12.blank.note", "An empty tray checks for stray material.",
                    P(left+.31f,1.30f,blankZ), Vector3.left, .067f);
            }

            void FieldBoard(string id, string title, string note, float x, float z, Vector3 facing, string target)
            {
                string prefix = "w12." + id;
                Box(prefix + ".post", P(x,.88f,z), P(.11f,1.76f,.12f), "Metal", false);
                Box(prefix + ".board", P(x,1.52f,z), P(.10f,.49f,1.94f), "Metal", false);
                Label(prefix + ".title", title, P(x,1.67f,z)-facing*.056f, facing, .10f);
                Label(prefix + ".note", note, P(x,1.48f,z)-facing*.056f, facing, .075f);
                Anchor(prefix, target, P(x,1.67f,z)-facing*.09f, P(x,0,z)-facing*1.27f);
            }

            void Shelter()
            {
                float start = 3 * bay;
                float front = width / 2 - 1.35f;
                float chamber = front + .74f;
                float console = start + 2.18f;
                float sideEntry = start + bay - .83f;
                Box("shelter.shield-south", P(chamber,1.18f,start+.24f), P(1.49f,2.36f,.20f), "Shell");
                Box("shelter.shield-north", P(chamber,1.18f,start+bay-.21f), P(1.49f,2.36f,.20f), "Shell");
                Box("shelter.shield-roof", P(chamber,2.26f,start+bay/2), P(1.50f,.23f,bay-.45f), "Shell");
                Box("shelter.shield-front-short", P(front,1.09f,start+.48f), P(.25f,2.18f,.68f), "Shell");
                float frontEnd = start + bay - 1.33f;
                Box("shelter.shield-front-long", P(front,1.09f,(start+1.87f+frontEnd)/2),
                    P(.25f,2.18f,frontEnd-start-1.87f), "Shell");
                Box("shelter.shutter", P(front+.13f,1.055f,start+.34f), P(.13f,2.11f,1.13f), "Ochre");
                New("shelter.shutter-stop", Kind.Point, P(front+.13f,1.055f,start+1.35f));
                New("shelter.side-approach", Kind.Point, P(0,0,sideEntry));
                New("shelter.interior", Kind.Point, P(chamber,0,sideEntry));
                Box("fixture.shelter-bench", P(width/2-.25f,.26f,start+1.73f), P(.42f,.52f,1.24f), "Metal");
                Box("shelter.side-route-mark", P(chamber-.37f,.004f,sideEntry), P(1.62f,.008f,.07f), "Ochre", false, castsShadows: false);
                SideLabel("shelter", "SHELTER", front-.23f, 1.86f, console, Vector3.right, .12f);
                SideLabel("shelter-status", "TRAINING READY", front-.19f, 1.51f, console, Vector3.right, .078f);
                Box("shelter.control-face", P(front-.151f,1.21f,console), P(.04f,.24f,.39f), "Ochre", false);
                Anchor("shelter-console", StationShelterSet.ShelterTarget.value, P(front-.191f,1.21f,console),
                    P(front-.70f,0,console));
                SideLabel("shelter-side", "SIDE ACCESS", front-.18f, 2.18f, sideEntry, Vector3.right, .096f);
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
