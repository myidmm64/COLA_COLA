import re, json, struct, zlib
from pathlib import Path
import numpy as np

ROOT = Path(r'C:\Work\COLA_COLA')

def trs(pos, rot, scale):
    x,y,z,w=rot
    R=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
    M=np.eye(4); M[:3,:3]=R@np.diag(scale); M[:3,3]=pos
    return M

def yaml_prefab(path):
    docs={}
    s=path.read_text(encoding='utf-8')
    for m in re.finditer(r'^--- !u!(\d+) &(-?\d+)\n(.*?)(?=^--- |\Z)',s,re.M|re.S):
        typ,id,body=m.groups(); docs[int(id)]={'typ':int(typ),'body':body}
    gos={}; transforms={}
    for id,d in docs.items():
        b=d['body']
        if d['typ']==1:
            gos[id]=re.search(r'^  m_Name: (.*)$',b,re.M).group(1)
        if d['typ']==4:
            def vec(k):return [float(v) for v in re.search(k+r': \{(.*?)\}',b).group(1).replace('x:','').replace('y:','').replace('z:','').replace('w:','').split(',')]
            go=int(re.search(r'm_GameObject: \{fileID: (-?\d+)\}',b).group(1))
            parent=int(re.search(r'm_Father: \{fileID: (-?\d+)\}',b).group(1))
            transforms[id]={'go':go,'parent':parent,'pos':vec('m_LocalPosition'),'rot':vec('m_LocalRotation'),'scale':vec('m_LocalScale')}
    def world(id):
        t=transforms[id]
        if 'world' not in t:t['world']=(world(t['parent']) if t['parent'] else np.eye(4))@trs(t['pos'],t['rot'],t['scale'])
        return t['world']
    result=[]
    for id,t in transforms.items():
        result.append({'name':gos[t['go']],'parent':gos[transforms[t['parent']]['go']] if t['parent'] else '', 'position':t['pos'],'rotation':t['rot'],'scale':t['scale'],'world_position':np.round(world(id)[:3,3],6).tolist()})
    return result

def read_fbx(path):
    data=path.read_bytes(); version=struct.unpack_from('<I',data,23)[0]; wide=version>=7500
    p=27
    def prop(p):
        t=chr(data[p]);p+=1
        fmts={'Y':'h','C':'?','I':'i','F':'f','D':'d','L':'q'}
        if t in fmts:
            f='<'+fmts[t]; return struct.unpack_from(f,data,p)[0],p+struct.calcsize(f)
        if t in 'SR':
            n=struct.unpack_from('<I',data,p)[0];p+=4; v=data[p:p+n];return v.decode('utf8',errors='replace') if t=='S' else v,p+n
        if t in 'fdlibc':
            n,enc,size=struct.unpack_from('<III',data,p);p+=12;v=data[p:p+size];v=zlib.decompress(v) if enc else v
            dtype={'f':'<f4','d':'<f8','l':'<i8','i':'<i4','b':'u1','c':'i1'}[t]
            return np.frombuffer(v,dtype=dtype),p+size
        raise ValueError(t)
    def node(p):
        end,n,plen,nlen=struct.unpack_from('<QQQB' if wide else '<IIIB',data,p);p+=25 if wide else 13
        if end==0:return None,p
        name=data[p:p+nlen].decode();p+=nlen; vals=[]
        for _ in range(n):v,p=prop(p);vals.append(v)
        children=[]
        while p<end-(25 if wide else 13):
            ch,p=node(p)
            if ch:children.append(ch)
            else:break
        return {'name':name,'props':vals,'children':children},end
    nodes=[]
    while p<len(data):
        n,p=node(p)
        if not n:break
        nodes.append(n)
    return nodes

def fbx_report(path):
    nodes=read_fbx(path); objs=next(n for n in nodes if n['name']=='Objects')['children']; connections=next(n for n in nodes if n['name']=='Connections')['children']
    models={n['props'][0]:n for n in objs if n['name']=='Model'}
    geos={n['props'][0]:n for n in objs if n['name']=='Geometry'}
    result=[]
    for c in connections:
        ps=c['props']
        if len(ps)<3 or ps[1] not in geos or ps[2] not in models:continue
        g,m=geos[ps[1]],models[ps[2]];v=next((n['props'][0] for n in g['children'] if n['name']=='Vertices'),None)
        if v is None or not v.size:continue
        v=v.reshape(-1,3)
        props=next((n['children'] for n in m['children'] if n['name']=='Properties70'),[])
        transforms={n['props'][0]:n['props'][4:] for n in props if n['props'][0] in ('Lcl Translation','Lcl Rotation','Lcl Scaling')}
        result.append({'model':m['props'][1].split('\0')[0],'mesh':g['props'][1].split('\0')[0],'raw_min':np.round(v.min(0),6).tolist(),'raw_max':np.round(v.max(0),6).tolist(),'trs':transforms})
    return result

report={}
for stem in ['Pizzeria_Scene','Pizzeria_Props']:
    f=ROOT/'Assets/2.Assets/04_Pizzeria'
    report[stem+'_prefab']=yaml_prefab(f/'Prefabs'/f'{stem}_fbx.prefab')
    report[stem+'_fbx']=fbx_report(f/'Models'/f'{stem}.fbx')
for folder,stem in [('03_Simple_Character_PSX','simple-character-psx'),('02_Player_Arms','Player_Arms')]:
    f=ROOT/'Assets/2.Assets'/folder
    report[stem+'_prefab']=yaml_prefab(f/'Prefabs'/f'{stem}_fbx.prefab')
    report[stem+'_fbx']=fbx_report(f/'Models'/f'{stem}.fbx')

# GLB bone positions and geometry bounds.
data=(ROOT/'output/concept-art/references/simple-character-psx.glb').read_bytes();p=12;chunks=[]
while p<len(data):
    n,t=struct.unpack_from('<II',data,p);p+=8;chunks.append((t,data[p:p+n]));p+=n
g=json.loads(next(d for t,d in chunks if t==0x4e4f534a)); blob=next(d for t,d in chunks if t==0x004e4942)
def acc(i):
    a=g['accessors'][i];v=g['bufferViews'][a['bufferView']];dt={5120:'i1',5121:'u1',5122:'<i2',5123:'<u2',5125:'<u4',5126:'<f4'}[a['componentType']];n={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']];off=v.get('byteOffset',0)+a.get('byteOffset',0);step=v.get('byteStride',np.dtype(dt).itemsize*n)
    return np.ndarray((a['count'],n),dtype=dt,buffer=blob,offset=off,strides=(step,np.dtype(dt).itemsize)).copy()
allp=[];bones=[]
def walk(i,P):
    n=g['nodes'][i];M=P@(np.array(n['matrix']).reshape(4,4).T if 'matrix'in n else trs(n.get('translation',[0,0,0]),n.get('rotation',[0,0,0,1]),n.get('scale',[1,1,1])))
    bones.append({'name':n.get('name',''),'world_position':np.round(M[:3,3],6).tolist(),'rotation':n.get('rotation',[0,0,0,1])})
    if 'mesh' in n:
        for pr in g['meshes'][n['mesh']]['primitives']:
            v=acc(pr['attributes']['POSITION']);allp.append((M@np.c_[v,np.ones(len(v))].T).T[:,:3])
    for ch in n.get('children',[]):walk(ch,M)
for i in g['scenes'][g.get('scene',0)]['nodes']:walk(i,np.eye(4))
allp=np.concatenate(allp)
report['boss_glb']={'bounds_min':allp.min(0).tolist(),'bounds_max':allp.max(0).tolist(),'bones':bones,'animations':[n.get('name','') for n in g.get('animations',[])]}
(ROOT/'output/scene-layout/audit/assets.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
for k,v in report.items():
    if k.endswith('_fbx'):
        print(k, len(v),'meshes')
        for d in v:
            if any(w.lower() in d['model'].lower() for w in ['Chair','Table','Sign','Soda_machine','Can_Soda','tongs','Pizza_oven','Counter','body','arms']):print(d)
print('BOSS GLB',report['boss_glb']['bounds_min'],report['boss_glb']['bounds_max'],'animations',report['boss_glb']['animations'])
