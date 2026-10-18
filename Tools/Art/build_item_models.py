"""Author COLA_COLA's eleven original tabletop props, in metres with a shared flat-colour atlas.

Run to regenerate the source assets. This script does not launch Unity or create previews/tests.
"""
from pathlib import Path
import math
import struct
import uuid
import zlib

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Assets/_COLA_COLA/Resources/ItemModels"
PALETTE = {
    "ink": (40, 45, 34), "paper": (224, 211, 166), "paper_edge": (167, 141, 96),
    "cream": (242, 225, 180), "red": (163, 46, 29), "red_dark": (95, 34, 24),
    "teal": (46, 129, 131), "teal_dark": (29, 76, 78), "ice": (156, 202, 202),
    "ice_light": (207, 228, 214), "steel": (132, 145, 128), "brass": (179, 133, 58),
    "gold": (220, 172, 67), "wood": (110, 69, 39), "kraft": (179, 143, 90),
    "cheese": (231, 180, 65), "crust": (169, 100, 37), "crust_light": (207, 146, 66),
    "green": (72, 119, 62), "sauce": (150, 57, 21), "orange": (218, 105, 32),
    "purple": (111, 77, 113), "rubber": (53, 67, 52), "silver_light": (191, 196, 165),
}
NAMES = list(PALETTE)


def add(a, b): return tuple(x + y for x, y in zip(a, b))
def sub(a, b): return tuple(x - y for x, y in zip(a, b))
def mul(a, n): return tuple(x * n for x in a)
def dot(a, b): return sum(x * y for x, y in zip(a, b))
def cross(a, b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def unit(a): return mul(a, 1 / max(1e-12, math.sqrt(dot(a, a))))


def rotate(p, angles):
    x, y, z = p
    rx, ry, rz = (math.radians(v) for v in angles)
    y, z = y*math.cos(rx)-z*math.sin(rx), y*math.sin(rx)+z*math.cos(rx)
    x, z = x*math.cos(ry)+z*math.sin(ry), -x*math.sin(ry)+z*math.cos(ry)
    return (x*math.cos(rz)-y*math.sin(rz), x*math.sin(rz)+y*math.cos(rz), z)


class Mesh:
    def __init__(self, name):
        self.name, self.triangles = name, []

    def tri(self, a, b, c, color, outward=None):
        n = cross(sub(b, a), sub(c, a))
        if dot(n, n) < 1e-20:
            return
        if outward is not None and dot(n, outward) < 0:
            b, c = c, b
            n = mul(n, -1)
        self.triangles.append(((a, b, c), unit(n), color))

    def face(self, points, color, outward=None):
        for i in range(1, len(points) - 1):
            self.tri(points[0], points[i], points[i+1], color, outward)

    def fan(self, points, center, color, outward):
        for i, a in enumerate(points):
            self.tri(center, a, points[(i+1) % len(points)], color, outward)

    def loft(self, rings, color, center=(0, 0, 0), rotation=(0, 0, 0), cap=True):
        def world(p): return add(center, rotate(p, rotation))
        for low, high in zip(rings, rings[1:]):
            for i in range(len(low)):
                j = (i+1) % len(low)
                radial = rotate((low[i][0]+low[j][0]+high[i][0]+high[j][0], 0,
                                 low[i][2]+low[j][2]+high[i][2]+high[j][2]), rotation)
                self.face([world(low[i]), world(low[j]), world(high[j]), world(high[i])], color, radial)
        if cap:
            self.face([world(p) for p in rings[0]], color, rotate((0, -1, 0), rotation))
            self.face([world(p) for p in rings[-1]], color, rotate((0, 1, 0), rotation))

    def lathe(self, center, profile, color, sides=10, rotation=(0, 0, 0), cap=True):
        rings = [[(r*math.cos(i*math.tau/sides), y, r*math.sin(i*math.tau/sides))
                  for i in range(sides)] for y, r in profile]
        self.loft(rings, color, center, rotation, cap)

    def box(self, center, size, color, bevel=.002, rotation=(0, 0, 0)):
        w, h, d = (v / 2 for v in size)
        b = min(bevel, w*.4, h*.4, d*.4)
        def ring(y, inset):
            x, z = w-inset, d-inset
            c = max(.00001, min(bevel, x*.3, z*.3))
            return [(-x+c,y,-z),(x-c,y,-z),(x,y,-z+c),(x,y,z-c),
                    (x-c,y,z),(-x+c,y,z),(-x,y,z-c),(-x,y,-z+c)]
        self.loft([ring(-h,b),ring(-h+b,0),ring(h-b,0),ring(h,b)], color, center, rotation)

    def prism(self, outline, bottom, top, color):
        self.loft([[(x,bottom,z) for x,z in outline],[(x,top,z) for x,z in outline]], color)

    def disk(self, center, radius, color, normal=(0, 1, 0), sides=10):
        n = unit(normal)
        u = unit(cross(n, (0,1,0) if abs(n[1]) < .9 else (1,0,0)))
        v = cross(n, u)
        points = [add(center, add(mul(u, radius*math.cos(i*math.tau/sides)),
                                  mul(v, radius*math.sin(i*math.tau/sides)))) for i in range(sides)]
        self.fan(points, center, color, n)

    def save(self):
        lines = ["# Original COLA_COLA prop. Y up, metres. Flat normals and shared palette UVs.",
                 "o " + self.name, "s off"]
        # Each triangle occupies a small patch inside one palette cell; no text or texture sampling ambiguity.
        for points, normal, color in self.triangles:
            index = NAMES.index(color)
            u, v = (index % 8 + .5) / 8, 1 - (index // 8 + .5) / 4
            for p, du, dv in zip(points, (-.012,.012,0), (-.022,-.022,.022)):
                lines.append("v %.7f %.7f %.7f" % p)
                lines.append("vt %.7f %.7f" % (u+du,v+dv))
                lines.append("vn %.7f %.7f %.7f" % normal)
        for i in range(len(self.triangles)):
            indices = [i*3+j+1 for j in range(3)]
            lines.append("f " + " ".join(f"{v}/{v}/{v}" for v in indices))
        path = OUTPUT / (self.name + ".obj")
        path.write_text("\n".join(lines) + "\n", encoding="utf-8")
        write_meta(path, """ModelImporter:
  serializedVersion: 24600
  internalIDToNameTable: []
  externalObjects: {}
  materials:
    materialImportMode: 0
  meshes:
    globalScale: 1
    meshCompression: 0
    addColliders: 0
    importCameras: 0
    importLights: 0
    weldVertices: 1
    preserveHierarchy: 0
  tangentSpace:
    normalImportMode: 0
  importAnimation: 0
  userData:
  assetBundleName:
  assetBundleVariant:
""")


def write_meta(path, body=""):
    meta = Path(str(path) + ".meta")
    if not meta.exists():
        guid = uuid.uuid5(uuid.NAMESPACE_URL, "cola-cola/" + path.relative_to(ROOT).as_posix()).hex
        meta.write_text(f"fileFormatVersion: 2\nguid: {guid}\n" + body, encoding="utf-8")


def plane(origin, u, v):
    n = unit(cross(v, u))
    def point(x, y, lift=.0012): return add(origin, add(add(mul(u,x),mul(v,y)),mul(n,lift)))
    return point, n


def panel(m, origin, width, height, color, u=(1,0,0), v=(0,.6,.8), thickness=.0015):
    p, n = plane(origin, u, v)
    xy = [(-width/2,0),(width/2,0),(width/2,height),(-width/2,height)]
    front = [p(x,y,thickness/2) for x,y in xy]
    back = [p(x,y,-thickness/2) for x,y in xy]
    m.face(front, color, n); m.face(back, color, mul(n,-1))
    for i in range(4):
        j=(i+1)%4
        m.face([front[i],front[j],back[j],back[i]], color)
    return p, n


def print_rect(m, p, n, x, y, w, h, color):
    m.face([p(x-w/2,y-h/2),p(x+w/2,y-h/2),p(x+w/2,y+h/2),p(x-w/2,y+h/2)],color,n)


def chili(m, p, n, x=0, y=0, scale=1):
    outline=[(-.016,.018),(.003,.020),(.015,.010),(.016,-.006),(.005,-.023),
             (-.011,-.033),(-.004,-.016),(-.004,.001),(-.014,.007)]
    pts=[p(x+a*scale,y+b*scale,.0016) for a,b in outline]
    m.fan(pts,p(x+.001*scale,y-.002*scale,.0016),"red",n)
    m.face([p(x-.009*scale,y+.017*scale,.0019),p(x-.006*scale,y+.031*scale,.0019),
            p(x+.003*scale,y+.031*scale,.0019),p(x+.003*scale,y+.019*scale,.0019)],"green",n)


def ice_cup(m):
    m.lathe((0,0,0),[(.002,.030),(.007,.033),(.077,.043),(.081,.043)],"cream",12)
    m.lathe((0,0,0),[(.024,.036),(.062,.0414)],"teal",12)
    m.lathe((0,0,0),[(.078,.045),(.083,.045)],"cream",12)
    m.disk((0,.0815,0),.039,"teal_dark",sides=12)
    for i,(x,z) in enumerate([(-.016,-.015),(.014,-.012),(-.013,.014),(.016,.016)]):
        m.box((x,.086+i%2*.005,z),(.024,.022,.024),"ice" if i%2 else "ice_light",.004,(0,i*31,8))
    m.lathe((.020,.075,.006),[(0,.0038),(.042,.0038)],"red",8,(0,0,-12))
    p,n=plane((0,.045,-.0418),(1,0,0),(0,1,0))
    for angle in (0,60,120):
        a=math.radians(angle)
        u,v=(math.cos(a),math.sin(a)),(-math.sin(a),math.cos(a))
        m.face([p(u[0]*s+v[0]*t,u[1]*s+v[1]*t) for s,t in
                [(-.012,-.0016),(.012,-.0016),(.012,.0016),(-.012,.0016)]],"cream",n)


def candy(m):
    m.lathe((0,.027,0),[(-.039,.008),(-.029,.023),(.029,.023),(.039,.008)],"red",8,(0,0,90))
    m.lathe((0,.027,0),[(-.008,.0236),(.008,.0236)],"cream",8,(0,0,90))
    for side in (-1,1):
        a=(side*.038,.027,0)
        pts=[(side*.063,.014,-.022),(side*.068,.037,-.012),(side*.066,.042,.014),(side*.062,.012,.023)]
        for i in range(3):
            m.tri(a,pts[i],pts[i+1],"cream" if i%2==0 else "orange")
            m.tri(a,pts[i+1],pts[i],"cream" if i%2==0 else "orange")
        m.lathe((side*.038,.027,0),[(-.003,.009),(.003,.009)],"teal",8,(0,0,90))
    p,n=plane((0,.027,-.024),(1,0,0),(0,1,0))
    m.face([p(-.004,.013),p(.007,.002),p(.001,.002),p(.004,-.011),p(-.007,.001),p(-.001,.001)],"red_dark",n)


def syrup(m):
    m.lathe((0,0,0),[(.001,.028),(.006,.033),(.071,.033),(.084,.018),(.094,.018)],"teal_dark",10)
    m.lathe((0,0,0),[(.014,.0336),(.067,.0336)],"teal",10)
    m.lathe((0,0,0),[(.089,.021),(.100,.021)],"cream",10)
    m.lathe((0,0,0),[(.099,.007),(.112,.007)],"steel",8)
    m.box((.011,.116,0),(.047,.010,.020),"ink",.002)
    m.box((.031,.111,0),(.010,.013,.012),"ink",.001)
    p,n=panel(m,(-.001,.025,-.0342),.043,.033,"paper",v=(0,1,0),thickness=.0008)
    for x,y,r in [(-.010,.011,.0045),(.006,.021,.006),(.012,.008,.003)]:
        m.disk(p(x,y,.0012),r,"teal",n,10)


def ticket(m):
    p,n=panel(m,(0,.006,-.055),.111,.080,"red",v=(0,.62,.7846))
    panel(m,(0,.006,.07054),.111,.080,"red_dark",v=(0,.62,-.7846))
    print_rect(m,p,n,0,.038,.083,.054,"paper")
    # Two bold 'skip' chevrons, made from ink geometry rather than a floating number.
    for x in (-.018,.013):
        m.face([p(x-.012,.025,.0018),p(x+.001,.039,.0018),p(x-.012,.053,.0018),
                p(x-.004,.053,.0018),p(x+.010,.039,.0018),p(x-.004,.025,.0018)],"red_dark",n)
    for x in (-.048,.048):
        for y in (.013,.032,.051,.070): m.disk(p(x,y,.0018),.0027,"red_dark",n,6)


def bill(m):
    m.box((0,.005,0),(.085,.010,.121),"ink",.004)
    p,n=panel(m,(0,.012,-.057),.073,.119,"paper",v=(0,.35,.93675))
    print_rect(m,p,n,0,.099,.049,.006,"red")
    for i in range(6):
        y=.080-i*.009
        print_rect(m,p,n,-.009,y,.034-(i%3)*.005,.0018,"ink")
        print_rect(m,p,n,.023,y,.010,.0018,"ink")
    for i in range(12): print_rect(m,p,n,-.026+i*.0046,.012,.0015+(i%3)*.0004,.012,"ink")
    m.box((0,.057,.052),(.046,.008,.014),"steel",.001)
    m.box((0,.063,.052),(.025,.006,.009),"ink",.001)


def spicy_order(m):
    panel(m,(0,.005,-.027),.095,.103,"red_dark",v=(0,.90,.4359))
    p,n=panel(m,(0,.007,-.029),.087,.096,"paper",v=(0,.90,.4359))
    panel(m,(0,.005,.0628),.087,.103,"red",v=(0,.90,-.4359))
    print_rect(m,p,n,0,.082,.071,.011,"red")
    chili(m,p,n,y=.044,scale=.88)
    for x in (-.027,-.010,.007,.024): print_rect(m,p,n,x,.012,.010,.002,"red_dark")
    m.box((0,.096,.014),(.034,.009,.013),"ink",.001)


def safety_cap(m):
    m.lathe((0,0,0),[(.001,.036),(.007,.043),(.011,.043)],"rubber",12)
    m.lathe((0,0,0),[(.009,.042),(.015,.044),(.041,.044),(.048,.038)],"cream",12)
    for i in range(12):
        a=i*math.tau/12
        m.box((math.sin(a)*.043,.027,math.cos(a)*.043),(.006,.023,.005),"paper_edge",.0008,(0,math.degrees(a),0))
    m.lathe((0,0,0),[(.045,.034),(.051,.034)],"green",12)
    m.box((0,.052,0),(.048,.003,.009),"cream",.001)
    m.box((0,.052,0),(.009,.003,.048),"cream",.001)
    for x in (-.049,.049): m.box((x,.022,0),(.011,.025,.021),"steel",.001)
    m.lathe((0,0,0),[(.011,.0448),(.016,.0448)],"steel",12)


def pizza(m):
    outline=[(-.057,.046),(.057,.046),(0,-.066)]
    m.prism(outline,.002,.014,"crust")
    m.prism([(-.054,.043),(.054,.043),(0,-.062)],.014,.017,"red_dark")
    m.prism([(-.052,.041),(.052,.041),(0,-.059)],.017,.023,"cheese")
    m.lathe((0,.022,.045),[(-.059,.007),(-.053,.013),(.053,.013),(.059,.007)],"crust_light",8,(0,0,90))
    for x,z,r in [(-.022,.018,.011),(.023,.018,.011),(0,-.022,.010)]:
        m.lathe((x,.023,z),[(0,r),(.002,r)],"red",10)
        m.disk((x-.002,.0254,z+.002),r*.34,"red_dark",sides=7)
    for x,z in [(-.011,-.005),(.010,.032),(.018,-.004),(-.026,.032)]:
        m.box((x,.0245,z),(.005,.0015,.010),"green",.0003,(0,30,0))


def hot_sauce(m):
    m.lathe((0,0,0),[(.001,.020),(.006,.024),(.071,.024),(.085,.012),(.105,.012)],"sauce",10)
    m.lathe((0,0,0),[(.012,.0245),(.061,.0245)],"red_dark",10)
    m.lathe((0,0,0),[(.100,.015),(.108,.015),(.124,.009),(.129,.004)],"red",10)
    m.lathe((0,0,0),[(.101,.0156),(.105,.0156)],"cream",10)
    p,n=panel(m,(0,.021,-.0253),.034,.039,"cream",v=(0,1,0),thickness=.0008)
    chili(m,p,n,y=.021,scale=.49)


def stamp(m):
    m.box((0,.006,0),(.091,.012,.067),"red_dark",.003)
    m.box((0,.019,0),(.094,.019,.070),"wood",.004)
    m.box((0,.031,0),(.080,.006,.057),"steel",.002)
    m.lathe((0,0,0),[(.032,.012),(.049,.012)],"brass",8)
    m.lathe((0,0,0),[(.047,.013),(.056,.024),(.083,.026),(.096,.018),(.101,.009)],"red_dark",10)
    m.lathe((0,0,0),[(.060,.0265),(.067,.027)],"red",10)
    p,n=plane((0,.020,-.0355),(1,0,0),(0,1,0))
    for x in (-.012,.012): print_rect(m,p,n,x,0,.010,.011,"cream")


def waiter_bell(m):
    m.lathe((0,0,0),[(.001,.073),(.005,.083),(.012,.083),(.018,.074)],"ink",16)
    m.lathe((0,0,0),[(.013,.078),(.020,.078),(.024,.070)],"brass",16)
    m.lathe((0,0,0),[(.022,.069),(.038,.065),(.056,.055),(.071,.040),(.081,.020),(.084,.010)],"steel",16)
    m.lathe((0,0,0),[(.023,.071),(.027,.071)],"silver_light",16)
    for a in (-42,42):
        r=math.radians(a)
        m.box((math.sin(r)*.074,.014,-math.cos(r)*.074),(.014,.006,.004),"brass",.0007,(0,-a,0))


def waiter_plunger(m):
    m.lathe((0,0,0),[(.080,.008),(.094,.008)],"brass",10)
    m.lathe((0,0,0),[(.093,.018),(.096,.024),(.103,.024),(.107,.018)],"silver_light",12)


def save_palette():
    width,height=64,32
    rows=[]
    for y in range(height):
        row=bytearray([0])
        for x in range(width):
            index=(y//8)*8+x//8
            row.extend(PALETTE[NAMES[index]] if index<len(NAMES) else (224,211,166))
        rows.append(bytes(row))
    def chunk(kind,data):
        return struct.pack(">I",len(data))+kind+data+struct.pack(">I",zlib.crc32(kind+data)&0xffffffff)
    data=b"\x89PNG\r\n\x1a\n"+chunk(b"IHDR",struct.pack(">IIBBBBB",width,height,8,2,0,0,0))
    data+=chunk(b"IDAT",zlib.compress(b"".join(rows),9))+chunk(b"IEND",b"")
    path=OUTPUT/"PizzeriaItemPalette.png"
    path.write_bytes(data)
    write_meta(path,"""TextureImporter:
  serializedVersion: 13
  externalObjects: {}
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 0
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  textureCompression: 0
  maxTextureSize: 64
  textureType: 0
  textureShape: 1
  alphaIsTransparency: 0
  userData:
  assetBundleName:
  assetBundleVariant:
""")


def main():
    OUTPUT.mkdir(parents=True,exist_ok=True)
    write_meta(OUTPUT,"folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n")
    save_palette()
    builders=[("01_IceCup",ice_cup),("02_FizzCandy",candy),("03_CarbonationSyrup",syrup),
              ("04_QueueTicket",ticket),("05_Bill",bill),("06_SpicyOrder",spicy_order),
              ("07_SafetyCap",safety_cap),("08_PizzaSlice",pizza),("09_HotSauce",hot_sauce),
              ("10_DoubleStamp",stamp)]
    for name,build in builders:
        model=Mesh(name)
        build(model)
        model.save()
    for name,build in [("WaiterBell",waiter_bell),("WaiterBellPlunger",waiter_plunger)]:
        model=Mesh(name)
        build(model)
        model.save()
    print("Authored 10 item meshes, waiter bell/body plunger and one shared palette. No Unity or validation run.")


if __name__=="__main__":
    main()
