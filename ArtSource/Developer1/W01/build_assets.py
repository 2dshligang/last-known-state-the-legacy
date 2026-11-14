"""Original W01 geometry. Run with Blender --background --factory-startup.

Default: create once, then verify hashes without rewriting artist edits.
Use --verify to check source geometry and a Blender FBX round trip.
Use --preview PATH to render a contact sheet outside the Unity asset tree.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys
import uuid

import bpy
from mathutils import Vector

if not bpy.app.background:
    raise RuntimeError("Run in a dedicated Blender --background process; never replace an artist's open scene.")

SOURCE = Path(__file__).resolve().parent
ROOT = SOURCE.parents[2]
ART = ROOT / "Assets/_Game/Art/W01"
MANIFEST = SOURCE / "inventory.json"
WALL = "LKS_Wall_Straight_4m"
EXPORT = dict(use_selection=True, object_types={"MESH"}, global_scale=1.0,
              apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
              axis_forward="-Z", axis_up="Y", use_space_transform=True,
              bake_space_transform=False, use_mesh_modifiers=True,
              mesh_smooth_type="FACE", use_triangles=True, add_leaf_bones=False,
              bake_anim=False, path_mode="AUTO", embed_textures=False)
FACES = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
         (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def box(x, y, z, ox=0, oy=0, oz=0):
    return ([(ox+a*x, oy+b*y, oz+c*z) for a, b, c in
             [(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0),
              (0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)]], FACES)


def join_boxes(specs):
    verts, faces = [], []
    for spec in specs:
        v, f = box(*spec)
        faces.extend(tuple(i+len(verts) for i in face) for face in f)
        verts.extend(v)
    return verts, faces


def rock(rings, sides):
    verts = [(rx*math.cos(i*2*math.pi/sides),
              ry*math.sin(i*2*math.pi/sides), z)
             for rx, ry, z in rings for i in range(sides)]
    faces = [tuple(reversed(range(sides)))]
    for j in range(len(rings)-1):
        for i in range(sides):
            a, b = j*sides+i, j*sides+(i+1)%sides
            faces.append((a, b, b+sides, a+sides))
    faces.append(tuple(range((len(rings)-1)*sides, len(rings)*sides)))
    return verts, faces


def specs():
    return [
        (WALL, "wall-straight-4m.fbx", box(4, .2, 3, 0, -.1, 0)),
        ("LKS_Floor_4m", "floor-4m.obj", box(4, 4, .2, 0, 0, -.2)),
        ("LKS_Equipment_Rack", "equipment-rack.obj", join_boxes([
            (.08, .6, 2, -.4, -.3, 0), (.08, .6, 2, .32, -.3, 0),
            (.64, .6, .08, -.32, -.3, 0), (.64, .6, .08, -.32, -.3, .64),
            (.64, .6, .08, -.32, -.3, 1.28), (.64, .6, .08, -.32, -.3, 1.92)])),
        ("LKS_Lab_Bench", "lab-bench.obj", join_boxes([
            (2, .8, .1, -1, -.4, .8), (.12, .65, .8, -.9, -.325, 0),
            (.12, .65, .8, .78, -.325, 0)])),
        ("LKS_Solar_Panel", "solar-panel.obj", box(2, 1, .08, -1, -.5, 0)),
        ("LKS_Rock_Layered", "rock-layered.obj", rock([
            (.28, .2, 0), (.4, .27, .08), (.33, .23, .12),
            (.38, .26, .19), (.27, .18, .24)], 8)),
        ("LKS_Rock_Angular", "rock-angular.obj", rock([
            (.22, .18, 0), (.32, .25, .22), (.12, .09, .56)], 4)),
        ("LKS_Rock_Rounded", "rock-rounded.obj", rock([
            (.17, .14, 0), (.29, .23, .07), (.34, .27, .2),
            (.28, .22, .34), (.12, .1, .42)], 12)),
    ]


def mesh_object(name, geometry):
    mesh = bpy.data.meshes.new(name+"_Mesh")
    mesh.from_pydata(*[geometry[0], [], geometry[1]])
    mesh.update()
    if mesh.validate():
        raise RuntimeError("Invalid geometry: "+name)
    uv = mesh.uv_layers.new(name="UVMap")
    for face in mesh.polygons:
        axes = sorted(range(3), key=lambda a: abs(face.normal[a]))[:2]
        coords = [mesh.vertices[mesh.loops[i].vertex_index].co for i in face.loop_indices]
        lo = [min(v[a] for v in coords) for a in axes]
        span = [max(v[a] for v in coords)-lo[k] for k, a in enumerate(axes)]
        for loop, vertex in zip(face.loop_indices, coords):
            uv.data[loop].uv = [(vertex[a]-lo[k])/max(span[k], 1e-9) for k, a in enumerate(axes)]
        if face.area <= 0:
            raise RuntimeError("Degenerate face: "+name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def reset_scene():
    # Blender runs in a dedicated background process, never in an artist's open file.
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1


def select_only(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def obj_text(obj):
    # Convert Blender Z-up to Unity Y-up with a proper rotation (no reflection).
    lines = ["# LKS original procedural placeholder. Units: meters; Y up.", "o "+obj.name]
    lines += ["v %.8f %.8f %.8f" % (v.co.x, v.co.z, -v.co.y) for v in obj.data.vertices]
    lines += ["vt %.8f %.8f" % tuple(uv.uv) for uv in obj.data.uv_layers.active.data]
    for poly in obj.data.polygons:
        lines.append("f "+" ".join("%d/%d" % (obj.data.loops[i].vertex_index+1, i+1)
                                   for i in poly.loop_indices))
    return "\n".join(lines)+"\n"


def meta(path, folder=False):
    rel = path.relative_to(ROOT).as_posix()
    guid = uuid.uuid5(uuid.NAMESPACE_URL, "lks:world:art:w01:"+rel).hex
    body = "fileFormatVersion: 2\nguid: "+guid+"\n"
    body += ("folderAsset: yes\nDefaultImporter:\n" if folder else "ModelImporter:\n")
    return body+"  externalObjects: {}\n  userData: LKS.W01.OriginalPlaceholder\n  assetBundleName:\n  assetBundleVariant:\n"


def assert_inventory():
    inventory = json.loads(MANIFEST.read_text(encoding="utf-8"))
    if inventory["generatorSha256"] != sha(Path(__file__)):
        raise RuntimeError("Generator changed; use a reviewed new output version, never overwrite existing assets.")
    for record in inventory["files"]:
        path = (ROOT / record["path"]).resolve()
        if not path.is_relative_to(ROOT.resolve()) or not path.is_file() or sha(path) != record["sha256"]:
            raise RuntimeError("Refusing changed/missing output: "+record["path"])
    return inventory


def generate():
    if MANIFEST.exists():
        inventory = assert_inventory()
        print("PASS: verified %d existing outputs; no files rewritten." % len(inventory["files"]))
        return
    targets = [SOURCE / "wall-straight-4m.blend", ART.with_suffix(".meta")]
    targets += [ART / name for _, name, _ in specs()]
    targets += [p.with_suffix(p.suffix+".meta") for p in list(targets) if p.suffix in {".fbx", ".obj"}]
    for path in targets:
        if path.exists():
            raise RuntimeError("Refusing existing output without manifest: "+str(path))
    ART.mkdir(parents=True, exist_ok=True)
    reset_scene()
    entries = []
    for name, filename, geometry in specs():
        obj = mesh_object(name, geometry)
        select_only(obj)
        path = ART / filename
        if name == WALL:
            bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "wall-straight-4m.blend"))
            bpy.ops.export_scene.fbx(filepath=str(path), **EXPORT)
        else:
            path.write_text(obj_text(obj), encoding="utf-8", newline="\n")
        vertices = [v.co for v in obj.data.vertices]
        dims = [max(v[a] for v in vertices)-min(v[a] for v in vertices) for a in (0, 2, 1)]
        entries.append(dict(id=name, path=path.relative_to(ROOT).as_posix(),
                            dimensionsMetersUnityXYZ=[round(v, 6) for v in dims],
                            origin="original-procedural", author="LKS project / Developer 1",
                            license="Project-owned original placeholder; no third-party asset content",
                            status="prepared-for-W02-not-Unity-validated"))
        path.with_suffix(path.suffix+".meta").write_text(meta(path), encoding="utf-8", newline="\n")
        bpy.data.objects.remove(obj, do_unlink=True)
    ART.with_suffix(".meta").write_text(meta(ART, True), encoding="utf-8", newline="\n")
    inventory = dict(schemaVersion=1, blenderVersion=bpy.app.version_string,
                     generatorSha256=sha(Path(__file__)), entries=entries,
                     exportSettings={k:sorted(v) if isinstance(v, set) else v for k,v in EXPORT.items()},
                     wallPivot="Bottom left snap edge: Blender (0,0,0), bounds X[0,4], Y[-0.1,0.1], Z[0,3].",
                     files=[dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p)) for p in targets])
    MANIFEST.write_text(json.dumps(inventory, indent=2)+"\n", encoding="utf-8", newline="\n")
    print("PASS: created 8 original meshes, editable wall source, and stable Unity metadata.")


def verify():
    assert_inventory()
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE / "wall-straight-4m.blend"))
    wall = bpy.data.objects[WALL]
    assert len(bpy.context.scene.objects) == 1
    assert tuple(wall.location) == (0, 0, 0) and tuple(wall.scale) == (1, 1, 1)
    assert all(abs(a-b) < 1e-5 for a,b in zip(wall.dimensions, (4,.2,3)))
    assert len(wall.data.uv_layers.active.data) == 24
    center = Vector((2, 0, 1.5))
    assert all(p.normal.dot(p.center-center) > 0 for p in wall.data.polygons)
    reset_scene()
    bpy.ops.import_scene.fbx(filepath=str(ART / "wall-straight-4m.fbx"))
    wall = bpy.data.objects[WALL]
    points = [wall.matrix_world @ v.co for v in wall.data.vertices]
    actual = [max(v[a] for v in points)-min(v[a] for v in points) for a in range(3)]
    assert all(abs(a-b) < 1e-4 for a,b in zip(actual, (4,.2,3))), actual
    assert wall.matrix_world.translation.length < 1e-5
    assert wall.data.uv_layers.active is not None
    assert len(wall.data.polygons) == 12
    for name, filename, geometry in specs():
        if name == WALL:
            continue
        obj = mesh_object(name, geometry)
        assert (ART / filename).read_text(encoding="utf-8") == obj_text(obj)
    print("PASS: source dimensions, snap pivot, outward normals, UVs, 12 FBX triangles, Blender FBX round trip and 7 deterministic OBJs.")
    print("NOT VERIFIED: Unity importer, URP, colliders, prefab references or runtime behavior.")


def preview(output):
    reset_scene()
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 24
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.world = bpy.data.worlds.new("W01_Preview_World")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (.22,.26,.32,1)
    scene.world.node_tree.nodes["Background"].inputs[1].default_value = .5
    positions = [(0,3,0), (-5,3,0), (5,3,0), (7,3,0), (0,-1,.1), (3,-1,0), (5,-1,0), (7,-1,0)]
    colors = [(.42,.47,.5,1), (.27,.31,.34,1), (.36,.42,.45,1), (.49,.46,.39,1),
              (.1,.2,.3,1), (.52,.3,.16,1), (.35,.31,.29,1), (.57,.45,.3,1)]
    for (name, _, geometry), pos, color in zip(specs(), positions, colors):
        obj = mesh_object(name, geometry)
        obj.location = pos
        material = bpy.data.materials.new(name+"_PreviewOnly")
        material.diffuse_color = color
        obj.data.materials.append(material)
    floor = mesh_object("PreviewGround", box(25,18,.1,-10,-7,-.3))
    camera = bpy.data.objects.new("PreviewCamera", bpy.data.cameras.new("PreviewCamera"))
    scene.collection.objects.link(camera)
    camera.location = (12,-17,12)
    camera.rotation_euler = (Vector((2,2,1))-camera.location).to_track_quat("-Z","Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 18
    scene.camera = camera
    light = bpy.data.objects.new("PreviewKey", bpy.data.lights.new("PreviewKey", "AREA"))
    scene.collection.objects.link(light)
    light.location = (1,-3,10)
    light.data.energy = 2200
    light.data.shape = "DISK"
    light.data.size = 8
    output = Path(output).resolve()
    if output.exists():
        raise RuntimeError("Preview already exists; choose a new path.")
    output.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--verify", action="store_true")
    parser.add_argument("--preview")
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    if args.verify:
        verify()
    elif args.preview:
        assert_inventory()
        preview(args.preview)
    else:
        generate()
