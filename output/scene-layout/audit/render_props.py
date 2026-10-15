import io, contextlib, sys, math
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw,ImageFont
with contextlib.redirect_stdout(io.StringIO()):
    from audit_assets import read_fbx
root=Path(r'C:\Work\COLA_COLA')
nodes=read_fbx(root/'Assets/2.Assets/04_Pizzeria/Models/Pizzeria_Props.fbx')
objs=next(n for n in nodes if n['name']=='Objects')['children'];con=next(n for n in nodes if n['name']=='Connections')['children']
models={n['props'][0]:n for n in objs if n['name']=='Model'};geos={n['props'][0]:n for n in objs if n['name']=='Geometry'}
selection=['Chair_Table.001','Chair_Table','Preparation_table.018','Pizza_oven','Soda_machine','tongs']
canvas=Image.new('RGB',(1800,1000),(45,48,48));draw=ImageDraw.Draw(canvas)
for mkey,name in enumerate(selection):
  mid=next(k for k,n in models.items() if n['props'][1].split('\0')[0]==name); gid=next(n['props'][1] for n in con if len(n['props'])>2 and n['props'][2]==mid and n['props'][1] in geos);g=geos[gid]
  v=next(n['props'][0] for n in g['children'] if n['name']=='Vertices').reshape(-1,3);inds=next(n['props'][0] for n in g['children'] if n['name']=='PolygonVertexIndex');tri=[];curr=[]
  for i in inds:
    curr.append(int(i) if i>=0 else ~int(i))
    if i<0:
      tri.extend([[curr[0],curr[j],curr[j+1]] for j in range(1,len(curr)-1)]);curr=[]
  v=v@np.array([[-1,0,0],[0,0,-1],[0,1,0]])
  v-=((v.min(0)+v.max(0))/2)
  ca,sa=math.cos(.65),math.sin(.65);R=np.array([[ca,0,sa],[0,1,0],[-sa,0,ca]]);v=v@R.T
  # slight downward view
  a=.23;R=np.array([[1,0,0],[0,math.cos(a),-math.sin(a)],[0,math.sin(a),math.cos(a)]]);v=v@R.T
  scale=400/max(v[:,0].ptp() if hasattr(v[:,0],'ptp') else np.ptp(v[:,0]),np.ptp(v[:,1]));off=np.array([(mkey%3)*600+300,(mkey//3)*500+255]);s=np.c_[v[:,0]*scale+off[0],-v[:,1]*scale+off[1]]
  order=sorted(tri,key=lambda tr:v[tr,2].mean())
  for t in order:
    n=np.cross(v[t[1]]-v[t[0]],v[t[2]]-v[t[0]]);n/=max(np.linalg.norm(n),1e-12);c=int(80+150*abs(n@np.array([-.4,.6,.7])));draw.polygon([tuple(p) for p in s[t]],fill=(c,int(c*.85),int(c*.7)))
  draw.text(((mkey%3)*600+20,(mkey//3)*500+15),name,fill='white')
canvas.save(root/'output/scene-layout/audit/props-shapes.png')
