"""Offline balance of the usable-item redesign and drop weights; no Unity Play."""
import argparse,copy,json,time
from concurrent.futures import ProcessPoolExecutor,as_completed
from datetime import datetime,timezone
from simulate_campaign import DEST,load_rules,simulation_job

MODERATE=[
[18,28,10,0,0,18,4,0,14,0,0,0,8,0,0,0,0,0,0],
[10,22,8,5,0,14,3,0,10,0,5,0,6,0,11,6,0,0,0],
[8,18,7,4,0,11,3,7,8,6,4,3,4,0,7,6,4,0,0],
[6,16,5,4,10,9,2,7,6,6,4,2,3,2,5,6,3,4,0],
[5,14,5,4,11,10,2,8,6,6,4,2,2,2,5,5,3,3,3]]

def main():
 p=argparse.ArgumentParser();p.add_argument('--runs',type=int,default=3000);p.add_argument('--final',action='store_true');args=p.parse_args()
 r=load_rules();frozen=json.loads((DEST/'comparison.json').read_text(encoding='utf-8'))['items_before'];jobs=[]
 if args.final:
  for label,policy,mode,n in [('abilities','balanced','random',args.runs),('stockpile','stockpile','random',2500),('economy_choices','balanced','economy',2500),('no_abilities','balanced','none',2500)]:jobs.append((label,r,n,2026102190,policy,mode,None,0))
  jobs.append(('previous_rules',frozen['rules'],3000,2026102190,'balanced','random',None,0))
 else:
  profiles={'original_weights':r['weights'],'moderate':MODERATE}
  pressure=copy.deepcopy(MODERATE)
  for row in pressure:row[0]-=2;row[1]+=2
  profiles['more_pressure']=pressure
  for label,weights in profiles.items():
   case=copy.deepcopy(r);case['weights']=weights
   assert all(sum(row)==100 for row in weights)
   jobs.append((label,case,args.runs,2026102185,'balanced','random',None,0))
 output=dict(kind='offline_item_rework_not_unity_play',created_utc=datetime.now(timezone.utc).isoformat(),seed=2026102190 if args.final else 2026102185,rules=r,cases={})
 if not args.final:output['profiles']=profiles
 started=time.perf_counter()
 with ProcessPoolExecutor(max_workers=3) as pool:
  for future in as_completed([pool.submit(simulation_job,j) for j in jobs]):
   label,data=future.result();data.pop('_outcomes',None);output['cases'][label]=data
   print(label,'complete',data['completion_pct'],'reach',[round(s['reach_pct'],1) for s in data['stages']],'conditional',[round(s['win_pct'],1) for s in data['stages']], 'minutes',[round(s['minutes_think4']['mean'],2) for s in data['stages']],flush=True)
 output['elapsed_seconds']=time.perf_counter()-started
 filename='results.json' if args.final else 'item-drop-comparison.json'
 (DEST/filename).write_text(json.dumps(output,ensure_ascii=False,indent=2),encoding='utf-8')
 print('Saved',filename,flush=True)
if __name__=='__main__':main()
