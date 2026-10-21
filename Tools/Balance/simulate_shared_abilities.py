"""Offline shared-passive balance study. Never starts Unity or runs compilation."""
from __future__ import annotations
import argparse
from concurrent.futures import ProcessPoolExecutor, as_completed
import copy
from datetime import datetime, timezone
import json
import time
from simulate_campaign import DEST, load_rules, simulation_job

PROFILES = {
    'no_boss': [0,0,0,0,0],
    'previous_proposal': [0,1,1,2,3],
    'gentle': [0,1,2,3,4],
    'rising': [0,1,2,4,6],
    'early': [1,2,3,4,5],
    'heavy': [0,2,3,5,7],
    'late': [0,1,2,5,8],
    'late_only': [0,1,1,4,7],
}

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--runs',type=int,default=2000)
    parser.add_argument('--selected')
    parser.add_argument('--seed',type=int,default=202610210)
    parser.add_argument('--workers',type=int,default=3)
    args=parser.parse_args()
    started=time.perf_counter();rules=load_rules()
    frozen=json.loads((DEST/'comparison.json').read_text(encoding='utf-8'))['shared_before']
    jobs=[]
    if args.selected:
        counts=PROFILES[args.selected];rules['boss_ability_counts']=counts
        for label,policy,mode,n in [('abilities','balanced','random',args.runs),
                ('stockpile','stockpile','random',2500),('no_abilities','balanced','none',2500),
                ('economy_choices','balanced','economy',2500)]:
            jobs.append((label,rules,n,args.seed,policy,mode,None,0))
        none=copy.deepcopy(rules);none['boss_ability_counts']=[0]*5
        jobs.append(('shared_without_boss',none,args.runs,args.seed,'balanced','random',None,0))
        jobs.append(('baseline_replay',frozen['rules'],frozen['cases']['abilities']['n'],frozen['seed'],'balanced','random',None,0))
    else:
        for label,counts in PROFILES.items():
            case=copy.deepcopy(rules);case['boss_ability_counts']=counts
            jobs.append((label,case,args.runs,args.seed,'balanced','random',None,0))
    results={}
    with ProcessPoolExecutor(max_workers=args.workers) as executor:
        futures=[executor.submit(simulation_job,job) for job in jobs]
        for future in as_completed(futures):
            label,data=future.result();data.pop('_outcomes',None);results[label]=data
            print(label,round(data['completion_pct'],2),'reach', [round(x['reach_pct'],2) for x in data['stages']],
                'conditional',[round(x['win_pct'],2) for x in data['stages']], 'censored',data['censored'],flush=True)
    output=dict(kind='offline_shared_abilities_not_unity_play',created_utc=datetime.now(timezone.utc).isoformat(),
        seed=args.seed,rules=rules,profiles=PROFILES,selected=args.selected,cases=results,
        elapsed_seconds=time.perf_counter()-started,
        assumptions=frozen['assumptions'])
    if args.selected:
        replay=results['baseline_replay'];old=frozen['cases']['abilities']
        output['baseline_replay_matched']=replay['completed']==old['completed'] and [s['reached'] for s in replay['stages']]==[s['reached'] for s in old['stages']]
    filename='results.json' if args.selected else 'count-comparison.json'
    (DEST/filename).write_text(json.dumps(output,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Saved',filename,'seconds',round(output['elapsed_seconds'],1),flush=True)

if __name__=='__main__':main()
