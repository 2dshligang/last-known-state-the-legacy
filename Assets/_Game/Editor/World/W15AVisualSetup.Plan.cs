using System.Collections.Generic;
using LKS.World;
using UnityEngine;

namespace LKS.Editor.World
{
    public static partial class W15AVisualSetup
    {
        private sealed class Placement
        {
            internal string Id;
            internal string Model;
            internal Vector3 Position;
            internal Vector3 Facing;
            internal Vector3 Scale;
        }

        private static List<Placement> Plan(StationGenerationManifest manifest)
        {
            var layout = manifest.Layout;
            layout.Validate();
            float bay = layout.BayLength;
            float width = layout.InnerWidth;
            float height = layout.CeilingHeight;
            float end = layout.InteriorEnd;
            float depth = layout.ExteriorDepth;
            float edge = layout.ExteriorWidth / 2;
            string[] walls = { "WallA", "WallB", "WallC" };
            var result = new List<Placement>();
            for (int index = 0; index < 6; index++)
            {
                float center = (index + .5f) * bay;
                var wallScale = new Vector3(bay / 4, height / 3, 1);
                Add($"wall.west.{index}", walls[index % 3], new Vector3(-width / 2 + .006f, 0, center),
                    Vector3.right, wallScale);
                Add($"wall.east.{index}", walls[(index + 1) % 3], new Vector3(width / 2 - .006f, 0, center),
                    Vector3.left, wallScale);
                Add($"deck.{index}", "Deck", new Vector3(0, 0, center), Vector3.forward,
                    new Vector3(width / 4, height / 3, bay / 4));
                if (index != 0) Add($"portal.{index}", "Portal", new Vector3(0, 0, index * bay), Vector3.forward);
            }
            Add("portal.surface", "Portal", new Vector3(0, 0, end), Vector3.forward);

            Fixture("arrival-console", "EntranceTerminal", Vector3.right);
            Fixture("power-console", "PowerStack", Vector3.right);
            Fixture("power-recovery", "Recovery", Vector3.left);
            Fixture("echo", "EchoStack", Vector3.left);
            Fixture("seedling", "SeedlingRack", Vector3.right);
            Fixture("analysis", "LabBench", Vector3.right);
            Fixture("w12-blank", "LabBlank", Vector3.right);
            Fixture("report", "ReportDesk", Vector3.left);
            Fixture("photo", "Overlook", Vector3.right);

            var shutter = Node(manifest, "shelter.shutter").transform.position;
            var stop = Node(manifest, "shelter.shutter-stop").transform.position;
            var track = (shutter + stop) / 2;
            track.y = 2.01f;
            Add("shelter.track", "ShelterHeader", track, Vector3.left);

            // Bank meshes dress the perimeter while its original collider keeps authority.
            int sections = Mathf.CeilToInt(depth / 3.2f);
            float length = depth / sections;
            for (int index = 0; index < sections; index++)
            {
                float z = end + (index + .5f) * length;
                Add($"bank.west.{index}", "Ridge", new Vector3(-edge - .35f, -.05f, z), Vector3.right,
                    new Vector3(length / 3.9f, .71f + (index % 3) * .21f, 1));
                Add($"bank.east.{index}", "Ridge", new Vector3(edge + .45f, -.09f, z), Vector3.left,
                    new Vector3(length / 3.9f, .68f + ((index + 2) % 3) * .23f, 1));
            }
            for (int index = 0; index < 3; index++)
                Add($"bank.far.{index}", "Ridge", new Vector3((index - 1) * edge * .68f, -.09f, end + depth + .48f),
                    Vector3.back, new Vector3(edge / 5.5f, .86f + (index % 2) * .27f, 1.14f));
            Add("terrain.island", "Island", new Vector3(0, 0, end + depth * .50f), Vector3.forward,
                new Vector3(layout.ExteriorWidth * .42f / 5.08f, 1, depth * .37f / 5.0f));
            Add("terrain.underside", "TerrainBed", new Vector3(0, 0, end + depth * .66f), Vector3.forward);
            Add("terrain.shelf", "Regolith", new Vector3(-.31f, .62f, end + depth * .40f), Vector3.forward,
                new Vector3(1.20f, 1.04f, 1.33f));
            Add("terrain.rubble", "Regolith", new Vector3(.24f, .62f, end + depth * .63f), Vector3.back,
                new Vector3(1.42f, 1.15f, 1.08f));
            var dust = Node(manifest, "exterior.sample-box").transform.position;
            Add("field.station", "FieldCrate", new Vector3(dust.x, .06f, dust.z-.025f), Vector3.back);
            return result;

            void Add(string id, string model, Vector3 position, Vector3 facing, Vector3? scale = null) =>
                result.Add(new Placement { Id = id, Model = model, Position = position, Facing = facing,
                    Scale = scale ?? Vector3.one });

            void Fixture(string id, string model, Vector3 facing)
            {
                var position = Node(manifest, "fixture." + id).transform.position;
                position.y = 0;
                Add("fixture." + id, model, position, facing);
            }
        }
    }
}
