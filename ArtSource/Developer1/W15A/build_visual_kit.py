"""Author the original ASTERION visuals in meters. Run with Blender 5.3."""
from pathlib import Path
import math
import random
import bpy

source = Path(__file__).resolve().parent
target = source.parents[2] / "Assets" / "_Game" / "Art" / "W15A" / "Original"
target.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"

palette = {
    "Porcelain": (.65,.65,.60,1), "Graphite": (.16,.20,.22,1), "Alloy": (.35,.41,.42,1),
    "Seal": (.075,.095,.105,1), "Ochre": (.85,.44,.14,1), "Petrol": (.16,.43,.47,1),
    "Diffuser": (.60,.76,.74,1), "Tray": (.23,.32,.30,1), "Leaf": (.32,.48,.27,1),
    "Stone": (.53,.30,.20,1), "Stratum": (.64,.39,.25,1), "Rind": (.38,.21,.15,1),
    "Dust": (.60,.35,.23,1),
}
materials={}
for name,color in palette.items():
    m=bpy.data.materials.new(name)
    m.diffuse_color=color
    materials[name]=m


class Model:
    def __init__(self, name):
        self.name=name
        self.vertices=[]
        self.faces=[]
        self.colors=[]

    def solid(self, rings, sides, cap):
        start=len(self.vertices)
        count=len(rings[0])
        for ring in rings:
            self.vertices.extend((-x,depth,height) for x,height,depth in ring)

        def face(indices, material):
            self.faces.append(tuple(start+i for i in indices))
            self.colors.append(material)

        face(tuple(reversed(range(count))),cap)
        for level in range(len(rings)-1):
            for side in range(count):
                a=level*count+side
                b=level*count+(side+1)%count
                face((a,b,b+count,a+count),sides[level])
        offset=(len(rings)-1)*count
        face(tuple(range(offset,offset+count)),cap)

    def box(self, material, x,y,z, w,h,d, bevel=.018):
        w,h,d=w/2,h/2,d/2
        edge=min(bevel,w*.35,h*.35,d*.40)
        def ring(rx,rz,yy):
            e=min(bevel,rx*.35,rz*.35)
            # Rotate the horizontal axes to face +Z in Unity; winding stays CCW.
            return [(x+dx, yy, z+dz) for dx,dz in [
                (-rx+e,-rz),(-rx,-rz+e),(-rx,rz-e),(-rx+e,rz),
                (rx-e,rz),(rx,rz-e),(rx,-rz+e),(rx-e,-rz)]]
        self.solid([ring(w-edge,d-edge,y-h),ring(w,d,y-h+edge),
                    ring(w,d,y+h-edge),ring(w-edge,d-edge,y+h)], [material]*3,material)

    def rock(self, x,y,z,w,h,d, seed, mats):
        rng=random.Random(seed)
        count=9
        rim=[(math.cos(-i*math.tau/count)*(.80+.20*rng.random()),
              math.sin(-i*math.tau/count)*(.78+.22*rng.random())) for i in range(count)]
        rings=[]
        for level,scale,offset in [(0,.83,0),(.18,1,.01),(.37,.90,.06),(.58,.84,-.03),(.77,.73,.04),(1,.51,.12)]:
            rings.append([(x+(a*scale+offset)*w/2,y+(level+a*.065*level)*h,z+b*scale*d/2) for a,b in rim])
        self.solid(rings,[mats[i%len(mats)] for i in range(len(rings)-1)],mats[0])

    def export(self):
        mesh=bpy.data.meshes.new(self.name)
        mesh.from_pydata(self.vertices,[],self.faces)
        mesh.update()
        obj=bpy.data.objects.new(self.name,mesh)
        scene.collection.objects.link(obj)
        slots=list(dict.fromkeys(self.colors))
        for name in slots:
            mesh.materials.append(materials[name])
        uv=mesh.uv_layers.new(name="Meter UV")
        for polygon, name in zip(mesh.polygons,self.colors,strict=True):
            polygon.material_index=slots.index(name)
            axis=max(range(3),key=lambda i:abs(polygon.normal[i]))
            u,v=[i for i in range(3) if i!=axis]
            for idx in polygon.loop_indices:
                point=mesh.vertices[mesh.loops[idx].vertex_index].co
                uv.data[idx].uv=(point[u],point[v])
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active=obj
        bpy.ops.export_scene.fbx(
            filepath=str(target/(self.name+".fbx")),use_selection=True,object_types={"MESH"},
            global_scale=1,apply_unit_scale=True,apply_scale_options="FBX_SCALE_UNITS",
            axis_forward="-Z",axis_up="Y",bake_space_transform=True,
            mesh_smooth_type="OFF",bake_anim=False,add_leaf_bones=False,use_metadata=False)
        return len(mesh.polygons)

models=[]
for index in range(3):
    m=Model("Wall"+chr(65+index))
    m.box("Graphite",0,.25,.027,3.91,.48,.053)
    m.box("Seal",0,.54,.040,3.87,.029,.053)
    for section in [-1,1]:
        x=section*.975
        m.box("Porcelain",x,1.47,.034,1.896,1.80,.062,.032)
        m.box("Alloy",x,.65,.066,1.81,.077,.047)
        m.box("Alloy",x,2.28,.048,1.81,.074,.049)
        m.box("Petrol",x,2.15,.070,1.81,.088,.016)
        for f in [-.82,.82]:
            m.box("Seal",x+f,.78,.085,.046,.038,.021,.008)
            m.box("Seal",x+f,2.16,.083,.045,.038,.020,.008)
    m.box("Graphite",0,2.62,.069,3.91,.37,.138)
    for y,c in [(2.57,"Alloy"),(2.69,"Seal"),(2.78,"Ochre")]:
        m.box(c,0,y,.149,3.79,.030,.064)
    if index==1:
        m.box("Graphite",.94,1.76,.085,.95,.44,.067)
        for slot in range(5):
            m.box("Seal",.94,1.61+.075*slot,.129,.80,.031,.035)
    if index==2:
        m.box("Seal",-.89,1.59,.079,.74,.59,.070)
        m.box("Alloy",-.89,1.59,.121,.68,.53,.034)
        m.box("Graphite",-.89,1.59,.146,.48,.35,.027)
        m.box("Ochre",-.62,1.59,.155,.028,.20,.025)
    models.append(m)

m=Model("Portal")
for side in [-1,1]:
    m.box("Graphite",side*.963,1.26,0,.244,2.52,.25,.033)
    m.box("Alloy",side*.873,1.14,-.148,.071,2.24,.08)
    m.box("Ochre",side*.93,.34,-.155,.135,.39,.073)
    m.box("Petrol",side*.98,2.01,-.156,.095,.21,.071)
    m.box("Graphite",side*1.17,2.57,0,.52,.27,.31)
m.box("Graphite",0,2.414,0,2.34,.21,.29,.032)
m.box("Diffuser",0,2.306,0,1.38,.022,.18)
models.append(m)

m=Model("Deck")
for side in [-1,1]:
    m.box("Graphite",side*1.30,2.893,0,.29,.15,3.92)
    m.box("Alloy",side*1.30,2.809,0,.20,.042,3.85)
    m.box("Seal",side*.92,2.927,0,.13,.075,3.94)
    for k in [-1.38,1.38]:
        m.box("Graphite",side*.49,2.846,k,.49,.095,1.14)
        m.box("Diffuser",side*.49,2.791,k,.39,.031,.96)
    m.box("Alloy",side*1.57,.014,0,.14,.026,3.82)
    m.box("Seal",side*1.70,.008,0,.043,.014,3.88)
    for k in [-1.15,0,1.15]:
        m.box("Ochre",side*1.55,.028,k,.12,.015,.28)
models.append(m)

for name, turn in [("PowerStack","power"),("EchoStack","echo"),("LabBench","lab"),("ReportDesk","report"),
                   ("Recovery","recovery"),("EntranceTerminal","entrance")]:
    m=Model(name)
    height={"echo":1.36,"report":1.10}.get(turn,1.10)
    m.box("Graphite",0,.13,.02,.72,.24,.44,.033)
    m.box("Alloy",0,.48,.028,.64,.62,.47,.031)
    m.box("Graphite",0,.59,.264,.53,.42,.062)
    m.box("Porcelain",0,height-.11,.022,.87,.23,.35,.042)
    m.box("Seal",0,height-.327,.294,.69,.139,.043)
    m.box("Petrol",-.045,height-.322,.325,.49,.092,.037)
    m.box("Ochre",.285,height-.322,.329,.061,.066,.044)
    for x in [-.34,.34]:
        m.box("Alloy",x,.48,.28,.054,.54,.061)
    for y in [.42,.48,.54,.60,.66]:
        m.box("Seal",.025,y,.313,.39,.031,.054)
    if turn=="echo":
        m.box("Graphite",0,.87,.031,1.20,.53,.47,.046)
        for x in [-.46,.46]:
            m.box("Alloy",x,.95,.286,.11,.40,.073)
            m.box("Petrol",x,.98,.331,.041,.26,.036)
        m.box("Seal",0,.96,.284,.68,.30,.10)
        m.box("Petrol",0,.98,.348,.58,.18,.055)
    elif turn=="lab":
        m.box("Graphite",-.48,.65,.04,.27,.70,.46)
        m.box("Tray",.50,.76,.107,.26,.18,.37)
        for i in range(3):
            m.box("Alloy",.43+i*.063,.887,.17,.048,.15,.07,.009)
        m.box("Porcelain",-.49,1.12,.058,.23,.37,.25)
    elif turn=="report":
        m.box("Graphite",-.43,.70,.03,.25,.72,.44)
        m.box("Porcelain",-.44,1.12,.119,.27,.13,.36)
        for i in range(3):
            m.box("Alloy",-.44,1.20+i*.017,.121,.22,.014,.25)
    elif turn=="power":
        for x in [-.49,.49]:
            m.box("Seal",x,.47,-.005,.20,.86,.37,.035)
            m.box("Alloy",x,.63,.19,.13,.22,.07)
    # Face panels stop below the existing interactive orange strip.
    m.box("Graphite",0,.41,.249,.656,.74,.063,.025)
    m.box("Alloy",0,.39,.285,.59,.62,.029,.020)
    m.box("Seal",0,.14,.312,.51,.081,.043)
    if turn=="echo":
        m.box("Seal",0,.51,.323,.49,.36,.074,.03)
        m.box("Petrol",0,.55,.369,.385,.173,.029,.013)
        for x in [-.215,.215]:
            m.box("Alloy",x,.53,.362,.021,.214,.021,.005)
        m.box("Ochre",0,.403,.369,.077,.022,.016,.004)
    elif turn=="power":
        m.box("Seal",0,.54,.314,.48,.247,.06)
        for x in [-.14,0,.14]:
            m.box("Alloy",x,.558,.352,.105,.131,.023,.013)
            m.box("Petrol",x,.566,.367,.061,.068,.014,.009)
        m.box("Ochre",0,.323,.313,.29,.042,.034,.005)
    else:
        m.box("Graphite",0,.49,.310,.47,.27,.063)
        m.box("Petrol",-.112,.49,.348,.176,.085,.024)
        m.box("Alloy",.115,.485,.348,.149,.034,.024)
    models.append(m)

m=Model("LabBlank")
m.box("Graphite",0,.43,.08,.70,.84,.30,.03)
m.box("Porcelain",0,.32,.267,.63,.47,.05,.021)
m.box("Seal",0,.34,.30,.48,.18,.039)
m.box("Alloy",0,.40,.327,.51,.036,.051)
m.box("Porcelain",0,.862,.08,.71,.045,.44)
m.box("Tray",0,.901,.12,.51,.024,.33)
for side in [-1,1]:
    m.box("Alloy",side*.245,.92,.12,.029,.059,.335)
for z in [-.037,.277]:
    m.box("Alloy",0,.92,z,.51,.059,.028)
models.append(m)

m=Model("SeedlingRack")
for x in [-.47,.47]:
    m.box("Porcelain",x,.69,.04,.12,1.35,.47,.024)
for y in [.25,.54,.90,1.19]:
    m.box("Graphite",0,y,.012,.93,.10,.42)
for y in [.56,.91]:
    for x in [-.29,0,.29]:
        m.box("Tray",x,y+.08,.208,.22,.108,.36)
        for shift in [-.066,0,.066]:
            m.box("Leaf",x+shift,y+.171,.22,.032,.12,.044,.013)
            m.box("Leaf",x+shift+.028,y+.205,.213,.069,.038,.050,.015)
    m.box("Diffuser",0,y+.30,.28,.84,.027,.090)
    for side in [-1,1]:
        m.box("Tray",side*.398,y+.064,.239,.139,.12,.32)
        m.box("Leaf",side*.404,y+.139,.30,.052,.092,.062)
        m.box("Leaf",side*.404,y+.180,.267,.095,.037,.051)
models.append(m)

m=Model("ShelterHeader")
m.box("Seal",0,.18,0,1.48,.25,.25)
m.box("Alloy",0,.20,.144,1.45,.112,.087)
for x in [-.62,-.47,.47,.62]:
    m.box("Ochre",x,.20,.207,.079,.093,.040)
models.append(m)

m=Model("Overlook")
for x in [-.30,.30]:
    m.box("Graphite",x,.77,.025,.095,1.49,.24)
m.box("Porcelain",0,1.61,.050,.43,.42,.099,.025)
m.box("Graphite",0,1.61,.110,.34,.34,.066)
m.box("Alloy",0,1.38,.138,.45,.064,.19)
m.box("Ochre",0,.36,.027,.66,.12,.45)
models.append(m)

m=Model("FieldCrate")
m.box("Graphite",0,.19,0,.76,.37,.56,.04)
m.box("Porcelain",0,.41,0,.82,.13,.63,.031)
m.box("Seal",0,.485,0,.66,.056,.44)
for x in [-.23,.23]:
    m.box("Ochre",x,.24,.305,.12,.22,.055)
models.append(m)

m=Model("Ridge")
m.rock(0,-.06,-.12,4.0,2.68,1.8,7,["Stone","Rind","Stratum"])
m.rock(-1.6,-.06,-.07,2.1,1.37,1.46,19,["Rind","Stone","Stratum"])
m.rock(1.6,-.06,-.18,2.4,1.69,1.61,34,["Stone","Stratum","Rind"])
models.append(m)
m=Model("Regolith")
for specs in [(-.45,-.02,0,1.08,.51,.96,2),(.46,-.02,.22,.94,.36,.75,9),(.27,-.02,-.38,.65,.23,.58,15)]:
    m.rock(*specs,["Stone","Stratum","Rind"])
models.append(m)
m=Model("Island")
m.box("Rind",0,.18,0,5.08,.35,5.0,.14)
m.box("Stone",0,.36,0,5.08,.27,4.98,.16)
m.box("Stratum",0,.518,0,4.98,.17,4.91,.13)
m.rock(-1.21,.23,.81,1.5,.394,1.94,62,["Stone","Stratum","Dust"])
m.rock(1.26,.25,-.92,1.68,.374,1.70,73,["Stone","Rind","Dust"])
models.append(m)
m=Model("TerrainBed")
m.box("Dust",0,-.19,0,32,.35,40,.01)
models.append(m)

for model in models:
    print(f"W15A_MESH {model.name} {model.export()} faces")
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(source/"AsterionVisualKit.blend"),compress=True)
print("W15A_BLEND_PASS",bpy.app.version_string,bpy.app.build_hash.decode(),len(models))
