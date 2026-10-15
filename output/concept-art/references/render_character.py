import json,struct,zipfile,io,math
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

ROOT=Path(__file__).parent
ROOT.mkdir(parents=True,exist_ok=True)
with zipfile.ZipFile(r'C:\Users\myidm\Downloads\assets_cola\Simple_Character_PSX.zip') as z:
    data=z.read('Simple_Character_PSX/model/glb/simple-character-psx.glb')
(ROOT/'simple-character-psx.glb').write_bytes(data)
chunks=[]; p=12
while p<len(data):
    n,t=struct.unpack_from('<II',data,p);p+=8;chunks.append((t,data[p:p+n]));p+=n
g=json.loads(next(d for t,d in chunks if t==0x4E4F534A));blob=next(d for t,d in chunks if t==0x004E4942)
def acc(i):
    a=g['accessors'][i];v=g['bufferViews'][a['bufferView']];dt={5120:'i1',5121:'u1',5122:'<i2',5123:'<u2',5125:'<u4',5126:'<f4'}[a['componentType']];n={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']];off=v.get('byteOffset',0)+a.get('byteOffset',0);step=v.get('byteStride',np.dtype(dt).itemsize*n)
    return np.ndarray((a['count'],n),dtype=dt,buffer=blob,offset=off,strides=(step,np.dtype(dt).itemsize)).copy()
def mat(n):
    if 'matrix' in n:return np.array(n['matrix']).reshape(4,4).T
    x,y,z,w=n.get('rotation',[0,0,0,1]);R=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
    M=np.eye(4);M[:3,:3]=R@np.diag(n.get('scale',[1,1,1]));M[:3,3]=n.get('translation',[0,0,0]);return M
images=[]
for im in g.get('images',[]):
    v=g['bufferViews'][im['bufferView']];b=blob[v.get('byteOffset',0):v.get('byteOffset',0)+v['byteLength']];images.append(np.array(Image.open(io.BytesIO(b)).convert('RGB')))
geoms=[]
def walk(i,P):
    n=g['nodes'][i];M=P@mat(n)
    if 'mesh' in n:
      for pr in g['meshes'][n['mesh']]['primitives']:
        pos=acc(pr['attributes']['POSITION']);pos=(M@np.c_[pos,np.ones(len(pos))].T).T[:,:3];uv=acc(pr['attributes']['TEXCOORD_0']) if 'TEXCOORD_0' in pr['attributes'] else np.zeros((len(pos),2));idx=acc(pr['indices']).reshape(-1,3) if 'indices' in pr else np.arange(len(pos)).reshape(-1,3)
        m=g.get('materials',[{}])[pr.get('material',0)];pb=m.get('pbrMetallicRoughness',{});tex=images[g['textures'][pb['baseColorTexture']['index']]['source']] if 'baseColorTexture' in pb else np.full((2,2,3),np.array(pb.get('baseColorFactor',[.65,.65,.65,1])[:3])*255,dtype=np.uint8)
        geoms.append((pos,uv,idx,tex))
    for c in n.get('children',[]):walk(c,M)
for i in g['scenes'][g.get('scene',0)]['nodes']:walk(i,np.eye(4))
allp=np.concatenate([a[0] for a in geoms]);mn=allp.min(0);mx=allp.max(0);center=(mn+mx)/2;extent=(mx-mn).max()
print('bounds',mn,mx,'triangles',sum(len(a[2]) for a in geoms),'images',len(images),flush=True)
def render(angle,W=700,H=900):
    bg=np.zeros((H,W,3),dtype=np.uint8);bg[:]=[40,42,43];depth=np.full((H,W),-1e20);ca,sa=np.cos(angle),np.sin(angle);R=np.array([[ca,0,sa],[0,1,0],[-sa,0,ca]]);scale=min(W,H)*.79/extent
    for pos,uv,idx,tex in geoms:
      q=(pos-center)@R.T;screen=np.c_[q[:,0]*scale+W/2,-q[:,1]*scale+H/2,q[:,2]]
      for tri in idx:
        pts=screen[tri];lo=np.maximum(np.floor(pts[:,:2].min(0)).astype(int),0);hi=np.minimum(np.ceil(pts[:,:2].max(0)).astype(int),[W-1,H-1]);
        if np.any(hi<lo):continue
        a,b,c=pts;den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
        if abs(den)<1e-8:continue
        xx,yy=np.meshgrid(np.arange(lo[0],hi[0]+1)+.5,np.arange(lo[1],hi[1]+1)+.5);w0=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den;w1=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den;w2=1-w0-w1;zz=w0*a[2]+w1*b[2]+w2*c[2];ds=depth[lo[1]:hi[1]+1,lo[0]:hi[0]+1];mask=(w0>=0)&(w1>=0)&(w2>=0)&(zz>ds)
        if not mask.any():continue
        tu=w0*uv[tri[0],0]+w1*uv[tri[1],0]+w2*uv[tri[2],0];tv=w0*uv[tri[0],1]+w1*uv[tri[1],1]+w2*uv[tri[2],1];tx=np.clip((tu*tex.shape[1]).astype(int),0,tex.shape[1]-1);ty=np.clip((tv*tex.shape[0]).astype(int),0,tex.shape[0]-1);normal=np.cross(q[tri[1]]-q[tri[0]],q[tri[2]]-q[tri[0]]);normal/=max(np.linalg.norm(normal),1e-10);light=.68+.32*abs(np.dot(normal,np.array([-.35,.55,.76])));rgb=np.clip(tex[ty,tx]*light,0,255).astype('u1');out=bg[lo[1]:hi[1]+1,lo[0]:hi[0]+1];out[mask]=rgb[mask];ds[mask]=zz[mask]
    return Image.fromarray(bg)
sheet=Image.new('RGB',(1400,980),(40,42,43));sheet.paste(render(0),(0,45));sheet.paste(render(.6),(700,45));d=ImageDraw.Draw(sheet);d.text((35,15),'PROJECT ASSET REFERENCE - SIMPLE CHARACTER PSX',fill=(225,215,190));d.text((35,950),'Original GLB mesh + embedded texture / software-rendered reference',fill=(170,175,175));sheet.save(ROOT/'character_asset_reference.png')
print(ROOT/'character_asset_reference.png',flush=True)
