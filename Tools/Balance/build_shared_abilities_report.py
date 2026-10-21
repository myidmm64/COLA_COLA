"""Write the retained report and plot for the shared-ability offline study."""
import csv
import hashlib
import json
from pathlib import Path
from datetime import datetime, timezone
from PIL import Image, ImageDraw, ImageFont
from simulate_campaign import DEST, ROOT, wilson


def duration(v):
    sec=round(v*60)
    return f'{sec//60}분 {sec%60:02d}초'


def main():
    data=json.loads((DEST/'results.json').read_text(encoding='utf-8'))
    pilot=json.loads((DEST/'count-comparison.json').read_text(encoding='utf-8'))
    rules=data['rules'];cases=data['cases'];main=cases['abilities'];stages=main['stages']
    counts=rules['boss_ability_counts'];defs=rules['abilities'];priority=rules['boss_ability_priority']
    no_boss=cases['shared_without_boss'];before=cases['baseline_replay']
    lines=['# 공용 능력과 보스 능력 수 — 개정 7', '',
        '이 보고서는 오프라인 가상 대전 결과다. Unity Play, 컴파일 검사, Game 뷰 확인은 실행하지 않았다.', '',
        f"8개 능력 수 구성을 각 {pilot['cases']['rising']['n']:,}회 비교한 뒤 **{' / '.join(map(str,counts))}개**를 선택했다. 선택에 쓰지 않은 별도 시드로 현재 구성과 공용 능력만 추가한 대조군을 각 {main['n']:,}회 분석했다. 최적 개수의 보편적 증명이 아니라 아래 고정 조합과 정책에 대한 추천이다.", '',
        '## 적용 내용', '',
        '- 안전 점검 능력은 추가하지 않았다. 기존 동명의 안전 마개 업적은 유지한다.',
        '- 새 능력 5개를 추가해 전체 17개를 플레이어·보스 공용으로 처리한다. 보상, 할인, 확률, 수납, 아이템 지급 효과를 같은 경로로 계산한다.',
        '- 플레이어는 기존처럼 합산 폭발 3회마다 미보유 3개 중 1개를 고른다. 후보는 17개 전체다. 보스는 아래 고정 능력으로 시작하며 매 단계 초기화한다.',
        '- TAB 기록부에 상대 능력 탭을 추가했다. 상시 효과 텍스트는 추가하지 않았다.',
        '- 체력, 압력 곡선, 강도별 압력/기본 보상, 아이템 드롭 가중치, 시작 자원은 유지했다.', '',
        '| 새 공용 능력 | 효과 |', '|---|---|',
        '| 정기 납품 | 새 콜라마다 무작위 아이템 1개. 가득 차면 빈 칸이 날 때 지급 |',
        '| 거친 손맛 | 강도 3으로 생존하면 상대의 다음 흔들기 압력 +2 |',
        '| 위기 대처 | 체력 2 이하에서 폭발 확률 −10%p. 한계 도달 시 100% |',
        '| 선불 계약 | 콜라마다 첫 벨 주문 가격 −2토큰 |',
        '| 사고 수당 | 자신이 폭발하면 토큰 +4 |', '',
        '침착한 손과 위기 대처는 합쳐 −15%p, 단골 할인과 선불 계약은 첫 주문 3토큰/그 뒤 5토큰으로 적용된다. 파손 보상과 사고 수당을 함께 가지면 폭발 보상은 11토큰이다. 거친 손맛의 예약 압력은 상대가 실제로 흔들 때 소비하며 넘기기·콜라 교체로 사라지지 않는다. 지급 대기 아이템은 R 디버그 삭제 시 함께 지운다.', '',
        '## 선택한 보스 구성', '', '| 단계 | 능력 수 | 고정 능력 |', '|---|---:|---|']
    for i,n in enumerate(counts):
        lines.append(f"| {i+1} | {n} | {', '.join(defs[j][1] for j in priority[:n]) or '없음'} |")
    lines += ['', '기존의 제안 0/1/1/2/3개보다 능력을 늘리되 1단계는 규칙을 익히도록 비워 둔다. 보스는 숨은 판정값이나 미래 입력을 읽지 않는다.', '',
        '## 능력 수 비교 — 탐색 표본', '',
        f"각 {pilot['cases']['rising']['n']:,}회, 같은 시작 시드 {pilot['seed']}. 같은 능력 우선순위의 앞부분만 늘려 비교했다. 같은 시드를 써도 추가 지급 때문에 이후 아이템 추첨 경로는 달라질 수 있다.", '',
        '| 보스 능력 수 (1→5단계) | 2단계 도달 | 3단계 도달 | 4단계 도달 | 5단계 도달 | 완주 | 5단계 조건부 승률 |',
        '|---|---:|---:|---:|---:|---:|---:|']
    for label,profile in data['profiles'].items():
        r=pilot['cases'][label]
        name='/'.join(map(str,profile))+(' **채택**' if label==data['selected'] else '')
        lines.append(f"| {name} | " + ' | '.join(f"{x['reach_pct']:.1f}%" for x in r['stages'][1:])+f" | {r['completion_pct']:.2f}% | {r['stages'][4]['win_pct']:.1f}% |")
    lines += ['', '**선택 이유:** 이전 제안은 누적 능력·자원을 가진 생존자의 후반 승률이 다시 높아졌다. 0/1/2/4/6개는 초반 접근성을 유지하면서 후반 승률을 단계적으로 낮췄다. 1단계부터 주거나 2·3단계부터 많이 주는 안은 초중반 탈락을 늘렸다. 7~8개를 후반에 주는 안도 가능하지만, 현재는 더 낮은 완주율보다 4→6개의 단계 차이를 우선했다. 조합을 바꾸면 같은 개수라도 재분석해야 한다.', '',
        '## 별도 시드 본 분석 — 단계 도달과 승률', '',
        f"시드 {data['seed']}, 기본 도전 {main['n']:,}회. 도달률은 전체 시작 도전 중 비율, 조건부 승률은 해당 단계에 도달한 도전 중 격파 비율이다. 괄호는 이항 Wilson 95% 구간이다.", '',
        '| 단계 | 능력 수 | 도달 횟수 | 도달률 | 격파 횟수 | 조건부 승률 (95%) | 평균 시간 |',
        '|---|---:|---:|---:|---:|---|---|']
    for i,s in enumerate(stages):
        lo,hi=s['win_ci95']
        lines.append(f"| {i+1} | {counts[i]} | {s['reached']:,} | {s['reach_pct']:.2f}% | {s['won']:,} | {s['win_pct']:.2f}% ({lo:.1f}–{hi:.1f}) | {duration(s['minutes_think4']['mean'])} |")
    lines += ['',f"**완주 {main['completed']:,}/{main['n']:,} = {main['completion_pct']:.2f}%** (95% {main['completion_ci95'][0]:.2f}–{main['completion_ci95'][1]:.2f}%). 완주한 도전 평균 {duration(main['completed_minutes']['mean'])}, 모든 종료 도전 평균 {duration(main['total_minutes']['mean'])}.", '',
        '## 추가로 보이는 점', '',
        f"- 공용 17개만 추가하고 보스 능력을 0개로 두면 완주율 {no_boss['completion_pct']:.2f}%. 보스 강화까지 넣은 현재 구성은 {main['completion_pct']:.2f}%. 보스 능력 부여가 새 플레이어 능력의 성장을 상대하는 역할을 한다.",
        f"- 이전 개정 6 재현 완주율 {before['completion_pct']:.2f}% ({before['n']:,}회). 이전 저장 결과와 단계 도달/완주 횟수 일치: {data['baseline_replay_matched']}. 이전 규칙 재현은 과거 시드, 새 규칙은 별도 시드이므로 이 차이를 짝지은 효과 추정으로 해석하지 않는다.",
        f"- 비축 정책 완주율 {cases['stockpile']['completion_pct']:.2f}% ({cases['stockpile']['n']:,}회). 토큰을 모으는 방식의 실용성이 남아 있다.",
        f"- 경제 능력 우선 선택 정책 완주율 {cases['economy_choices']['completion_pct']:.2f}% ({cases['economy_choices']['n']:,}회). 정기 납품·할인·수당과 방어 능력을 우선하는 이 정책이 무작위 선택보다 유리했다. 이는 개별 능력의 인과 효과나 모든 사람의 정답 순위를 입증하지 않는다.",
        f"- 플레이어만 능력을 받지 않는 정책의 완주율 {cases['no_abilities']['completion_pct']:.2f}%. 보스 능력은 유지했다. 후반은 능력 선택을 적극 활용하는 도전으로 바뀌었다.",
        '- 생존자 선택과 플레이어의 능력·아이템·토큰 이월이 단계별 승률에 섞인다. 연속 도전의 5단계 승률을 무자원 독립 5단계의 난도로 읽으면 안 된다.', '',
        '## 단계별 플레이 흐름', '',
        '| 단계 | 입장 시 플레이어 능력 | 플레이어/보스 사용 아이템 | 플레이어/보스 주문 | 강도 1/2/3 |',
        '|---|---:|---:|---:|---|']
    for s in stages:
        lines.append(f"| {s['stage']} | {s['entry_abilities']['mean']:.2f} | {s['player_items']['mean']:.1f} / {s['boss_items']['mean']:.1f} | {s['player_orders']['mean']:.1f} / {s['boss_orders']['mean']:.1f} | " + '/'.join(f'{x:.1f}%' for x in s['player_strength_pct'])+' |')
    lines += ['', '## 분석 범위와 재현', '',
        '- 기본 플레이어는 공개 압력·체력·토큰·효과로 위험과 다음 상대 위험/보상을 평가한다. 사람의 숙련도나 최적 전략을 대신하지 않는다.',
        '- 능력 기본 선택은 무작위 미보유 후보 3개 중 무작위 1개다. 비축 정책은 체력 3 이상이면 주문 후 12토큰을 남기려 한다. 경제 정책은 3개 후보 중 고정 우선순위를 사용한다.',
        '- 보스는 실제 C#과 같은 공개 상태 효용, 단계별 판단 노이즈, 아이템 점수/연계, 턴당 최대 1회 주문을 모델링한다. 플레이어는 구매 가능하면 자기 턴에 주문한다. 실제 게임의 상대 턴 주문·실시간 경합은 모델링하지 않았다.',
        '- 시간은 제작된 동작 길이에 턴 판단 4초, 아이템 선택 1.5초, 주문 1초, 능력 선택 6초를 더한 추정이다. 렌더링 성능·실제 읽기 시간·게임 중 휴식은 제외한다.',
        '- 아이템 핫소스는 병당 제한을 지키며 지급은 기존 단계 가중치를 쓴다. 확률은 공개된 제곱 곡선, 가감 효과와 한계 도달 100%로 계산한다. 새 능력 때문에 RNG 소비 경로가 달라지는 것은 정상이다.',
        f"- 분석된 전체 구성의 안전 한도 중단은 {sum(c['censored'] for c in cases.values())+sum(c['censored'] for c in pilot['cases'].values())}회다. 중단된 대전을 승리로 간주하지 않는다.",
        '- Unity Play·컴파일 검사·시각 확인은 하지 않았다. 오프라인 모델의 실행 성공을 실제 게임 동작 검증으로 보고하지 않는다.', '',
        '```powershell',
        'python -B Tools/Balance/simulate_shared_abilities.py --runs 2000 --workers 3',
        'python -B Tools/Balance/simulate_shared_abilities.py --selected rising --runs 10000 --seed 2026102180 --workers 3',
        'python -B Tools/Balance/build_shared_abilities_report.py', '```', '',
        '소스 해시와 전체 수치는 results.json, 후보별 전체 수치는 count-comparison.json, 동결한 이전 규칙은 comparison.json에 있다. 표 형식은 stages.csv와 abilities.csv에 있다.']
    (DEST/'Report.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    with (DEST/'stages.csv').open('w',encoding='utf-8-sig',newline='') as f:
        writer=csv.writer(f);writer.writerow(['stage','boss_abilities','reached','reach_pct','won','win_pct','win_ci95_low','win_ci95_high','mean_minutes','entry_player_abilities'])
        for i,s in enumerate(stages):writer.writerow([i+1,counts[i],s['reached'],s['reach_pct'],s['won'],s['win_pct'],*s['win_ci95'],s['minutes_think4']['mean'],s['entry_abilities']['mean']])
    with (DEST/'abilities.csv').open('w',encoding='utf-8-sig',newline='') as f:
        writer=csv.writer(f);writer.writerow(['id','enum','name','description','player_owners_observed','observed_player_effects_not_causal'])
        for i,entry in enumerate(defs):
            e=main['effects'][str(i)];desc=entry[2]
            if i==11:desc=f"예상 폭발 확률 {rules['danger_threshold']*100:.0f}% 이상에서 생존하면 토큰 +{rules['danger_bonus']}"
            writer.writerow([i,*entry[:2],desc,e['owners'],json.dumps(e,ensure_ascii=False)])
    draw_chart(data,pilot)
    now=datetime.now(timezone.utc).isoformat()
    summary=['# 최근 분석 — 공용 능력 / 개정 7','',
        '안전 점검을 제외한 5개를 추가하고 전체 17개를 공용 능력으로 처리했다. TAB에 상대 능력 탭을 추가했다.', '',
        f"보스 시작 능력은 **{'/ '.join(map(str,counts))}개**로 적용했다. 8개 구성을 각 2,000회 탐색하고 별도 시드로 본 구성 10,000회, 공용 능력만 있고 보스 능력이 없는 대조군 10,000회, 이전 개정 재현 10,000회, 보조 정책 각 2,500회를 실행했다.", '',
        f"기본 완주율 {main['completion_pct']:.2f}%, 보스 능력 없는 대조군 {no_boss['completion_pct']:.2f}%, 비축 {cases['stockpile']['completion_pct']:.2f}%, 경제 능력 우선 {cases['economy_choices']['completion_pct']:.2f}%.", '',
        '| 단계 | 보스 능력 | 도달 | 도달률 | 조건부 승률 | 평균 시간 |','|---|---:|---:|---:|---:|---|']
    for i,s in enumerate(stages):summary.append(f"| {i+1} | {counts[i]} | {s['reached']:,} | {s['reach_pct']:.2f}% | {s['win_pct']:.2f}% | {duration(s['minutes_think4']['mean'])} |")
    summary+=['','시간은 턴 판단 4초 등을 가정한 추정이다. 사람의 실제 승률이 아니며 Unity Play·컴파일 검사·게임 화면 확인은 실행하지 않았다.','', '[상세 보고서](C:/Work/COLA_COLA/output/verification/latest/campaign-balance/Report.md)']
    (DEST.parent/'Summary.md').write_text('\n'.join(summary)+'\n',encoding='utf-8')
    status=dict(id='shared-abilities-revision-7-20261021',suite='campaign-balance',kind='offline_shared_abilities_analysis',
        phase='analysis_completed',currentSourceRevision=7,analysisRevision=7,offlineAnalysisCompleted=True,
        unityPlayExecuted=False,compileExecuted=False,gameViewInspected=False,latestChangesValidated=False,
        completedUtc=now,mainRuns=main['n'],pilotRunsPerProfile=2000,bossAbilityCounts=counts,
        completionPct=main['completion_pct'],baselineReplayMatched=data['baseline_replay_matched'],
        report='campaign-balance/Report.md',modelSha256=hashlib.sha256((ROOT/'Tools/Balance/simulate_campaign.py').read_bytes()).hexdigest(),
        message='Shared abilities and fixed boss counts implemented; offline campaigns analyzed. Unity behavior/UI not verified.')
    (DEST.parent/'status.json').write_text(json.dumps(status,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (DEST.parent/'verification.log').write_text(f"{now} Offline shared ability analysis complete.\n"+
        f"Boss counts={counts}; main n={main['n']}; complete={main['completed']}; rate={main['completion_pct']:.2f}%.\n"+
        f"Baseline replay matched={data['baseline_replay_matched']}.\nUnity Play=false; compilation=false; Game view inspected=false.\nReport=campaign-balance/Report.md\n",encoding='utf-8')
    print('Report, chart, CSV and latest summary written.')


def draw_chart(data,pilot):
    image=Image.new('RGB',(1550,1130),'#efede5');d=ImageDraw.Draw(image)
    def text(x,y,s,size=20,color='#273b32',bold=False):
        font=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf' if bold else 'C:/Windows/Fonts/malgun.ttf',size)
        d.text((x,y),s,fill=color,font=font)
    main=data['cases']['abilities'];none=data['cases']['shared_without_boss']
    text(42,22,'COLA_COLA · 공용 능력과 보스 성장',34,bold=True)
    text(44,76,'탐색 8개 구성 × 2,000회 · 별도 시드 본 분석 10,000회 · 오프라인 모델, 실제 플레이 아님',18)
    for box in ((36,122,752,632),(780,122,1514,632),(36,660,1514,1092)):
        d.rounded_rectangle(box,radius=14,fill='#fffdf7')
    text(62,140,'단계 도달과 도전 완료',26,bold=True)
    text(62,184,'회색: 보스 능력 0개   녹색: 0/1/2/4/6개',18)
    for i in range(6):
        y=237+i*58
        text(62,y, f'{i+1}단계' if i<5 else '완주',21,bold=i==5)
        a=none['stages'][i]['reach_pct'] if i<5 else none['completion_pct']
        b=main['stages'][i]['reach_pct'] if i<5 else main['completion_pct']
        d.rectangle((160,y,160+4.45*a,y+11),fill='#b7b9b0')
        d.rectangle((160,y+17,160+4.45*b,y+35),fill='#587d66')
        text(173+4.45*b,y+14,f'{b:.1f}%',17)
    text(805,140,'도달 후 격파 확률',26,bold=True)
    text(805,184,'녹색: 채택 구성 · 세로선: 95% 통계 구간',18)
    left,top,width,height=858,245,588,280
    for v in (0,20,40,60,80,100):
        y=top+height*(1-v/100);d.line((left,y,left+width,y),fill='#dddcd2');text(805,y-12,str(v)+'%',15)
    points=[]
    for i,s in enumerate(main['stages']):
        x=left+35+i*128;value=s['win_pct'];y=top+height*(1-value/100);points.append((x,y))
        lo,hi=s['win_ci95'];d.line((x,top+height*(1-hi/100),x,top+height*(1-lo/100)),fill='#587d66',width=3)
        d.ellipse((x-5,y-5,x+5,y+5),fill='#587d66');text(x-23,y-31,f'{value:.1f}',17)
        text(x-29,549,f'{i+1}단계',17);text(x-23,577,f"{data['rules']['boss_ability_counts'][i]}개",17)
    d.line(points,fill='#587d66',width=3)
    text(63,678,'능력 수 후보별 완주율',26,bold=True)
    text(63,722,'후보 비교는 각 2,000회 탐색 표본 · 고정 조합 앞부분을 늘림 · 초록색 구성 채택',18)
    for i,(label,counts) in enumerate(data['profiles'].items()):
        row=i%4;col=i//4;x=66+col*725;y=784+row*67
        val=pilot['cases'][label]['completion_pct'];chosen=label==data['selected']
        text(x,y,'/'.join(map(str,counts)),20,bold=chosen)
        d.rectangle((x+150,y+4,x+150+val*9,y+29),fill='#587d66' if chosen else '#a7afa1')
        text(x+162+val*9,y,f'{val:.2f}%',18,bold=chosen)
    image.save(DEST/'campaign-analysis.png')

if __name__=='__main__':main()
