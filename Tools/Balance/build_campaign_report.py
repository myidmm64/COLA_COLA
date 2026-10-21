"""Render only the latest offline campaign statistics; no Unity or gameplay changes."""
import csv
import json
from pathlib import Path
from statistics import NormalDist

ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / 'output/verification/latest/campaign-balance'


def fmt(v, digits=1): return f'{v:,.{digits}f}'
def duration(v):
    seconds=round(v*60)
    return f'{seconds//60}분 {seconds%60:02d}초'


def interval(wins,n):
    z=1.96;p=wins/n;den=1+z*z/n
    mid=(p+z*z/(2*n))/den
    half=z*(p*(1-p)/n+z*z/(4*n*n))**.5/den
    return max(0,mid-half)*100,min(1,mid+half)*100


def draw_rebalance_charts(before,after,old_medium,design=False):
    from PIL import Image,ImageDraw,ImageFont
    image=Image.new('RGB',(1600,1180),'#f0eee5');draw=ImageDraw.Draw(image)
    def text(x,y,s,size=19,color='#273e32',bold=False):
        f=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf' if bold else 'C:/Windows/Fonts/malgun.ttf',size)
        draw.text((x,y),str(s),font=f,fill=color)
    old,new='#a5aaa1','#547c62';palette=['#6f927a','#c39957','#b36d59']
    a,b=before['cases']['abilities'],after['cases']['abilities']
    text(42,24,'COLA_COLA · 밸런스 변경 전·후',35,bold=True)
    subtitle=('강도 3 능력 +2토큰 · 단계별 보스 시작 자원 차등' if 'previous_design_replay' in after else '압력 2/4/8 · 상대에게 압력 공격 · 방어 중첩 · 아이템 의존 능력 교체') if design else '압력·체력·아이템 확률 유지'
    text(44,74,f"동일 시드 기본 도전 각 {b['n']:,}회 · {subtitle} · 오프라인 모델",16)
    for box in ((40,112,780,570),(820,112,1560,570),(40,600,780,1110),(820,600,1560,1110)):
        draw.rounded_rectangle(box,radius=12,fill='#fffdf7')
    text(65,132,'단계 도달 횟수',25,bold=True)
    draw.rectangle((68,178,88,191),fill=old);text(98,171,'변경 전',18)
    draw.rectangle((245,178,265,191),fill=new);text(275,171,'변경 후',18)
    for i,(x,y) in enumerate(zip(a['stages'],b['stages'])):
        at=232+i*62;text(67,at-3,f'{i+1}단계',22)
        draw.rectangle((168,at-3,168+x['reached']/a['n']*470,at+8),fill=old)
        end=168+y['reached']/b['n']*470;draw.rectangle((168,at+15,end,at+33),fill=new)
        text(end+9,at+12,f"{y['reached']:,}",17)
    text(846,132,'단계 평균 소요 시간',25,bold=True)
    text(846,171,'턴 판단 4초 가정 · 후의 10~90% 구간 표시',18)
    def ty(v):return 503-v/9*287
    draw.rectangle((910,ty(8),1505,ty(5)),fill='#e5ebdf')
    for v in (0,2,4,6,8):
        y=ty(v);draw.line((910,y,1505,y),fill='#d5dbcf');text(856,y-12,f'{v}분',17)
    lines=[[],[]]
    for i,(x,y) in enumerate(zip(a['stages'],b['stages'])):
        at=955+i*126;ta=x['minutes_think4'];tb=y['minutes_think4']
        lines[0].append((at,ty(ta['mean'])));lines[1].append((at,ty(tb['mean'])))
        draw.line((at,ty(tb['p10']),at,ty(tb['p90'])),fill=new,width=2)
        for v in (tb['p10'],tb['p90']):draw.line((at-7,ty(v),at+7,ty(v)),fill=new,width=2)
        draw.ellipse((at-4,ty(tb['mean'])-4,at+4,ty(tb['mean'])+4),fill=new)
        text(at-29,518,f'{i+1}단계',18)
    draw.line(lines[0],fill=old,width=3);draw.line(lines[1],fill=new,width=3)
    text(65,622,'강도 선택 비중',25,bold=True)
    for i,color in enumerate(palette):
        draw.rectangle((70+i*145,669,88+i*145,681),fill=color);text(98+i*145,662,f'강도 {i+1}',18)
    for i,(x,y) in enumerate(zip(a['stages'],b['stages'])):
        at=720+i*72;text(66,at,f'{i+1}단계',20)
        for j,row in enumerate((x,y)):
            x0=211;y0=at+j*25;text(157,y0-2,'전' if j==0 else '후',17)
            for k,v in enumerate(row['player_strength_pct']):
                width=v/100*510;draw.rectangle((x0,y0,x0+width,y0+17),fill=palette[k])
                if width>55:text(x0+width/2-19,y0-3,f'{v:.0f}%',15,color='#fffdf7')
                x0+=width
    text(846,622,'정책별 완주율',25,bold=True)
    text(846,661,'회색: 전 · 녹색: 후 | 선택 방식에 따른 비교',17)
    labels=[('abilities','기본 계산형'),('no_abilities','능력 없음'),('economy_choices','경제 능력 우선'),
        ('always_1','강도 1 고정'),('always_2','강도 2 고정'),('always_3','강도 3 고정'),('precise_adaptive','노이즈 없는 상황 판단')]
    scale=max(20,max(after['cases'][key]['completion_pct'] for key,_ in labels)*1.2)
    for i,(key,label) in enumerate(labels):
        at=718+i*51;prior=old_medium if key=='always_2' else before['cases'][key]
        current=after['cases'][key];text(846,at-2,label,18)
        draw.rectangle((1096,at-3,1096+prior['completion_pct']/scale*332,at+7),fill=old)
        end=1096+current['completion_pct']/scale*332;draw.rectangle((1096,at+13,end,at+28),fill=new)
        text(end+8,at+8,f"{current['completion_pct']:.2f}%",17)
    text(43,1138,'시간은 연출과 판단 시간 가정의 합산입니다. 실제 Play 측정·컴파일 검증 결과가 아닙니다.',20)
    image.save(DEST/'campaign-analysis.png')


def build_design_report(result):
    comparison=json.loads((DEST/'comparison.json').read_text(encoding='utf-8'))
    recent='previous_design_replay' in result
    before=comparison['revision4_before'] if recent else comparison['design_before']
    old=result['previous_design_replay'] if recent else result['baseline_replay']
    base=result['cases']['abilities'];rules=result['rules']
    paired=result['paired_comparisons']['from_previous_design' if recent else 'abilities']
    before_revision=before['rules']['revision']
    lines=[f"# COLA_COLA 방어·비축 중심 밸런스 — 개정 {rules['revision']}",'',
        '**실제 게임 코드에 반영한 뒤 수행한 오프라인 가상 대전이다. Unity Play·컴파일 검사·게임 화면 확인은 실행하지 않았다.**','',
        '## 적용한 규칙','',
        '- 강도 1/2/3 압력 증가 2/5/8 → 2/4/8. 생존 보상 1/3/4, 폭발 보상 총 5토큰 유지.',
        f"- 발포 캔디는 상대의 다음 흔들기 압력 +{rules['CandyIncrease']:g}, 소금은 +{rules['SaltIncrease']:g}. 중복 가능. 내 현재 압력은 바꾸지 않는다.",
        '- 공격 대기 효과는 대상이 실제 흔들 때 적용·소비한다. 계산서로 넘기거나 콜라가 바뀌어도 유지하고 새 보스/재시작 때 초기화한다. 실제 상승한 압력은 이후에도 콜라에 남는다.',
        '- 안전 마개는 개당 다음 자기 흔들기 -15%p로 중첩. 한계 도달과 W 강제 폭발은 항상 100%. 얼음컵 -5와 보냉팩 -2는 유지.',
        '- 짠맛 취향 → 알맞은 힘: 강도 2 생존 +1토큰. 냉각 숙련 → 침착한 손: HP 2 이하일 때 -5%p. 뜨거운 거래 → 여유 자금: 흔들기 확정 시 12토큰 이상이면 생존 +1토큰.',
        f"- 과감한 선택: 강도 3 생존 시 +{rules.get('HighAbilityReward',1):g}토큰. 폭발 시에는 적용하지 않는다.",
        '- 능력 ID는 보존하고 이름/효과를 교체했다. 여유 자금은 확정 시 보너스를 기록하므로 연출 도중 주문하더라도 보상을 잃지 않는다.',
        '- 아이템 무제한 사용, 벨 기본 6토큰/2개, 토큰·인벤토리 이월, HP 5 대 3/4/5/5/5, 한계 25~35, 아이템 가중치는 유지.',
        f"- 단계별 보스 시작 아이템 {rules.get('boss_start_items',[2]*5)}, 시작 토큰 {rules.get('boss_start_tokens',[0]*5)}.",
        '- 확률 곡선은 제곱을 유지한다. 추가 압력·보냉·마개 중첩·공격 확률·패시브를 한 예측 경로에 반영하고 같은 값으로 실제 판정한다. 몰래 확률을 조정하거나 연패 보정하지 않는다.','',
        '## 비교 조건','',
        f"주요 표와 도표는 개정 {before_revision} 동결 규칙과 현재 개정 {rules['revision']}를 동일 시드 {result['seed']}에서 각 {base['n']:,}도전 비교한다. 현재 규칙은 최근 시작 자원 차등과 과감한 선택 +2토큰을 포함한다.",
        f"개정 2 이전 도달 수·완주 수 재현: {result['baseline_replay_matched']}. 개정 4 재현: {result.get('previous_design_replay_matched','해당 없음')}. 기본 능력 선택은 무작위 후보 3개 중 무작위 선택이다. 공개 상태의 단기 효용과 선택 노이즈를 사용하며 인간·최적 전략이 아니다.",
        '플레이어 판단 4초, 아이템 추가 판단 1.5초, 주문 추가 판단 1초, 능력 선택 6초를 가정한다. 시간은 추정이며 상대 턴 주문·입력 경합·학습·중도 포기를 모델링하지 않았다.',
        '과거 개정 4 설계 시 공격 +6/+3, +5/+2, +4/+2를 예비 비교해 +4/+2를 선택했다. 이번 요청에서는 수치를 추가 튜닝하지 않고 현재 규칙 그대로 분석했다.','',
        '## 전체 결과','',
        f"완주율 **{old['completion_pct']:.2f}% → {base['completion_pct']:.2f}%** ({base['completed']:,}/{base['n']:,}). 짝지은 변화 {paired['delta_pp']:+.2f}%p, 근사 95% 구간 {paired['ci95'][0]:+.2f}~{paired['ci95'][1]:+.2f}%p.",
        f"완주한 도전 평균 {duration(old['completed_minutes']['mean'])} → {duration(base['completed_minutes']['mean'])}. 성공·실패를 합친 종료까지 평균 {duration(old['total_minutes']['mean'])} → {duration(base['total_minutes']['mean'])}. 서로 다른 분모다.",
        '', '| 단계 | 도달 전→후 | 격파 전→후 | 조건부 승률 후 | 평균 시간 전→후 | 후 10~90% 시간 | 강도 2 전→후 |',
        '|---|---:|---:|---:|---|---|---:|']
    for a,b in zip(old['stages'],base['stages']):
        t=b['minutes_think4']
        lines.append(f"| {b['stage']} | {a['reached']:,}→{b['reached']:,} | {a['won']:,}→{b['won']:,} | {b['win_pct']:.1f}% | {duration(a['minutes_think4']['mean'])}→{duration(t['mean'])} | {duration(t['p10'])}~{duration(t['p90'])} | {a['player_strength_pct'][1]:.1f}→{b['player_strength_pct'][1]:.1f}% |")
    lines.extend(['','후반 도달자는 이전 단계를 이긴 표본이다. 조건부 승률만으로 후반 보스의 절대 난도를 판단하지 않는다.','',
        '## 선택 전략과 시스템 효과','', '| 정책 | 도전 수 | 완주율 | 95% 구간 | 평균 종료 시간 |', '|---|---:|---:|---|---|'])
    names={'abilities':'기본 상황 판단','no_abilities':'능력 없음','economy_choices':'기존 경제 능력 우선',
        'always_1':'강도 1 고정','always_2':'강도 2 고정','always_3':'강도 3 고정','precise_adaptive':'노이즈 없는 단기 상황 판단','stockpile':'체력 3 이상에서 12토큰 비축','random_strength':'강도 무작위'}
    for key,name in names.items():
        c=result['cases'][key]
        lines.append(f"| {name} | {c['n']:,} | {c['completion_pct']:.2f}% | {c['completion_ci95'][0]:.1f}~{c['completion_ci95'][1]:.1f}% | {duration(c['total_minutes']['mean'])} |")
    lines.extend(['','고정 전략은 그 강도에 맞춰 아이템도 평가한다. 비축 전략은 HP가 3 이상이면 주문 후 12토큰을 남기는 단순 정책이며, 실제 사람의 상황별 저축 전략을 대표하지 않는다. 경제 능력 우선 순위도 최적 선택으로 재학습하지 않았다.'])
    if recent:
        lines.extend(['','## 최근 변경 분리 — 개정 4 대비','', '| 변경 | 도전 수 | 완주율 | 개정 4 대비 | 짝지은 95% 구간 |', '|---|---:|---:|---:|---|'])
        for key,name in (('high_bonus_only','과감한 선택 +2만'),('boss_resources_only','보스 시작 자원만')):
            c=result['recent_ablations'][key];p=result['paired_comparisons'][key]
            lines.append(f"| {name} | {c['n']:,} | {c['completion_pct']:.2f}% | {p['delta_pp']:+.2f}%p | {p['ci95'][0]:+.2f}~{p['ci95'][1]:+.2f}%p |")
        lines.append('\n두 단독 변경의 효과를 단순히 합산해 전체 효과로 해석하지 않는다. 선택과 자원 이월의 상호작용이 있다.')
    lines.extend(['','## 초기 변경 묶음 — 개정 2 대비 참고','', '| 규칙 | 도전 수 | 완주율 | 개정 2 대비 차이 | 짝지은 95% 구간 |', '|---|---:|---:|---:|---|'])
    for key,name in (('stacking_only','마개 중첩만'),('stacking_and_medium','마개 + 강도 2'),('without_ability_rework','위 변경 + 상대 압력 공격')):
        c=result['ablations'][key];p=result['paired_comparisons'][key]
        lines.append(f"| {name} | {c['n']:,} | {c['completion_pct']:.2f}% | {p['delta_pp']:+.2f}%p | {p['ci95'][0]:+.2f}~{p['ci95'][1]:+.2f}%p |")
    lines.extend(['','이 참고 표는 개정 2 대비이며 위의 개정 4→현재 비교와 기준이 다르다. 행끼리의 차이나 능력 개편만의 효과를 이 구간으로 유의하다고 주장하지 않는다.','',
        '## 방어와 압력 공격의 사용','',
        '| 단계 | 내 마개 2개 이상 사용한 흔들기 평균 | 내 0% 흔들기 평균 | 내/보스 아이템 평균 | 내 주문 평균 | 받은 추가 압력 내/보스 평균 |',
        '|---|---:|---:|---|---:|---|'])
    for s in base['stages']:
        lines.append(f"| {s['stage']} | {s['stacked_safety_shakes'][0]['mean']:.2f} | {s['zero_risk_shakes'][0]['mean']:.2f} | {s['player_items']['mean']:.1f}/{s['boss_items']['mean']:.1f} | {s['player_orders']['mean']:.1f} | {s['incoming_pressure_received'][0]['mean']:.1f}/{s['incoming_pressure_received'][1]['mean']:.1f} |")
    if 'guaranteed_bursts' in base['stages'][0]:
        lines.extend(['','| 단계 | 내 폭발 중 한계 도달 비율 | 보스 폭발 중 한계 도달 비율 | 진입 토큰 평균 | 진입 능력 평균 |','|---|---:|---:|---:|---:|'])
        for s in base['stages']:
            own=s['guaranteed_bursts'][0]['mean']/max(1e-9,s['player_bursts']['mean'])*100
            enemy=s['guaranteed_bursts'][1]['mean']/max(1e-9,s['boss_bursts']['mean'])*100
            lines.append(f"| {s['stage']} | {own:.1f}% | {enemy:.1f}% | {s['entry_tokens']['mean']:.1f} | {s['entry_abilities']['mean']:.1f} |")
        lines.append('\n한계 도달 폭발은 W를 사용하지 않은 일반 대전에서 압력이 한계 이상이 되어 100%로 판정된 사건이다. 마개가 이를 막지 못하는 규칙과 공격 압력이 맞물리는 정도를 본다.')
    lines.extend(['','| 아이템 | 이전 사용/생성 비율 | 현재 사용/생성 비율 |', '|---|---:|---:|'])
    for i in range(len(rules['items'])):
        lines.append(f"| {rules['items'][i][1]} | {old['item_stats'][str(i)]['used_pct']:.1f}% | {base['item_stats'][str(i)]['used_pct']:.1f}% |")
    lines.extend(['','양쪽 합산 사용/생성 비율이다. 전투 종료 시 남은 아이템·취소·교환도 영향을 준다. 사용률이 재미나 최적성을 직접 증명하지 않는다.','',
        '## 능력 발동 관찰','', '| 능력 | 보유 도전 수 | 보유 도전당 추가 토큰 | 추가 지급 아이템 | 낮은 체력 보호 판정 수 |', '|---|---:|---:|---:|---:|'])
    for i,definition in enumerate(rules['abilities']):
        e=base['effects'][str(i)];n=max(1,e['owners'])
        lines.append(f"| {definition[1]} | {e['owners']:,} | {e.get('tokens',0)/n:.2f} | {e.get('items',0)/n:.2f} | {e.get('protected_shakes',0)/n:.2f} |")
    lines.extend(['','획득 시점과 살아남은 기간이 섞인 관찰량이다. 침착한 손의 보호 판정 수는 해당 능력이 활성인 흔들기 수이며 실제 생명을 구한 횟수가 아니다. 수납 확장은 토큰/아이템을 직접 지급하지 않아 위 표에서 0으로 보인다.','',
        '## 능력의 통제 비교',''])
    experiment=result.get('ability_experiment')
    if experiment:
        critical=NormalDist().inv_cdf(1-.05/(2*12))
        lines.extend([f"별도 시드 {experiment['attempts']:,}도전 중 첫 정상 능력 선택까지 도달한 {experiment['eligible']:,}상태를 복제했다. 해당 능력을 강제로 선택한 경우와 그 보상 한 번만 건너뛴 경우를 비교하고 이후 능력은 양쪽 모두 무작위다. 이미 초반을 통과한 표본에서의 전체 후속 완주 효과다.",
            '', '| 능력 | 완주율 | 보상 건너뛰기 대비 | 개별 95% 구간 | 12종 동시 보수 구간 |','|---|---:|---:|---|---|'])
        for row in experiment['abilities']:
            delta=row['delta_pp'];se=(row['delta_ci95'][1]-row['delta_ci95'][0])/3.92
            row['family95']=[delta-critical*se,delta+critical*se]
            lines.append(f"| {rules['abilities'][row['ability']][1]} | {row['completion_pct']:.2f}% | {delta:+.2f}%p | {row['delta_ci95'][0]:+.2f}~{row['delta_ci95'][1]:+.2f}%p | {row['family95'][0]:+.2f}~{row['family95'][1]:+.2f}%p |")
        lines.append('\n같은 초기 상태라도 개입 후 행동·소모 RNG 순서는 달라진다. 소표본의 능력 순위를 확정하지 않는다. 획득 시점별 가치와 사람이 직접 비축하는 운영은 별도다.')
    lines.extend(['','## 확률을 선택으로 읽는 예시','',
        '한계 30, 능력 없음. 최종 확률 = (흔든 뒤 압력 / 30)² + 공격 확률 − 방어 확률. 0~100%로 제한하며 한계 도달은 방어와 관계없이 100%다.',
        '', '| 현재 압력 | 강도 | 흔든 뒤 압력 | 기본 폭발 확률 | 마개 1개 사용 | 마개 2개 사용 | 생존 보상 |','|---|---:|---:|---:|---:|---:|---:|'])
    rows=result['probability_review']['rows']
    for pressure in (6,12,18,24):
        for strength in (1,2,3):
            variants=[next(x for x in rows if x['pressure']==pressure and x['strength']==strength and x['incoming']==0 and x['caps']==caps and not x['chilled']) for caps in (0,1,2)]
            x=variants[0]
            lines.append(f"| {pressure} | {strength} | {x['after']} | {x['risk_pct']:.1f}% | {variants[1]['risk_pct']:.1f}% | {variants[2]['risk_pct']:.1f}% | {x['safe_tokens']} |")
    lines.extend(['','공격·보냉·마개를 함께 적용한 조합과 상대의 다음 저강도 위험 예시는 `results.json`의 `probability_review`에 보관한다. 그 예시는 상대 방어를 가정하지 않으며 실제 대전 모델은 방어를 허용한다. 독립 추첨이므로 낮은 확률의 연속 폭발은 여전히 가능하다.'])
    if 'risk_bins' in base['stages'][0]:
        lines.extend(['','## 실제 선택한 강도의 결과 — 모델 내 집계','', '| 강도 | 흔들기 수 | 선택 비중 | 평균 판정 확률 | 실제 폭발 비율 | 흔들기당 토큰 |', '|---|---:|---:|---:|---:|---:|'])
        outcomes=[[sum(s['strength_outcomes'][0][strength][j] for s in base['stages']) for j in range(4)] for strength in range(3)]
        total=sum(x[0] for x in outcomes)
        for strength,(count,bursts,risks,tokens) in enumerate(outcomes,1):
            lines.append(f"| {strength} | {count:,} | {count/max(1,total)*100:.1f}% | {risks/max(1,count)*100:.1f}% | {bursts/max(1,count)*100:.1f}% | {tokens/max(1,count):.2f} |")
        lines.extend(['','위험한 상황에서 저강도를 고르고 방어 후 고강도를 고르는 등 조건이 섞여 있다. 이 표로 강도 자체의 인과적 안전성을 비교하지 않는다. 토큰은 폭발 보상과 해당 시점 능력·스탬프를 포함한다.','',
            '## 예측 확률과 추첨 결과 — 오프라인 모델','', '| 확률 구간 | 양쪽 판정 수 | 평균 예상 확률 | 관측 폭발률 | 관측률 Wilson 95% |', '|---|---:|---:|---:|---|'])
        for bucket in range(12):
            count,bursts,risks=[sum(s['risk_bins'][actor][bucket][j] for s in base['stages'] for actor in (0,1)) for j in range(3)]
            if not count:continue
            if bucket in (0,11):label='0%' if bucket==0 else '100%'
            else:label=f'{(bucket-1)*10}~{bucket*10}% 미만'
            lo,hi=interval(bursts,count)
            lines.append(f'| {label} | {count:,} | {risks/count*100:.2f}% | {bursts/count*100:.2f}% | {lo:.2f}~{hi:.2f}% |')
        lines.append('\n추첨 빈도를 살펴보는 모델 분석이다. Unity에 표시된 확률이나 실제 C# RNG를 검증한 결과가 아니다. 0~10% 구간은 정확히 0%를 제외하고 90~100% 구간은 정확히 100%를 제외한다.')
    lines.extend(['',
        '## 독립 단계와 시간 가정','', '| 단계 | 이월 없는 승률 | 평균 시간 | 실제 판단 2초/8초 가정 | 5분 미만 비율 / 8분 초과 비율 |', '|---|---:|---|---|---|'])
    isolated={x['stage']:x for x in result['isolated_stages']}
    for s in base['stages']:
        v=isolated.get(s['stage']);win=f"{v['win_pct']:.1f}% (n={v['n']:,})" if v else '미실행'
        lines.append(f"| {s['stage']} | {win} | {duration(s['minutes_think4']['mean'])} | {duration(s['minutes_think2']['mean'])}/{duration(s['minutes_think8']['mean'])} | {s['under5_pct']:.1f}% / {s['over8_pct']:.1f}% |")
    middle=[s['player_strength_pct'][1] for s in base['stages']]
    stock=result['cases']['stockpile'];off=result['cases']['no_abilities']
    savings_base=base['effects']['8'];savings_stock=stock['effects']['8']
    lines.extend(['','## 이번 결과의 해석','',
        f"- 기본 정책의 강도 2 비중은 단계별 {min(middle):.1f}~{max(middle):.1f}%다. 강도 2 고정의 완주율은 {result['cases']['always_2']['completion_pct']:.2f}%, 상황 판단은 {base['completion_pct']:.2f}%다. 한 버튼을 고르는 일이 최적이라는 증명은 아니다.",
        f"- 비축 정책 {stock['completion_pct']:.2f}%는 기본 주문 정책 {base['completion_pct']:.2f}%와 다르다. 주문 시점·인벤토리·보상 발동이 함께 바뀐 결과이므로 여유 자금 하나의 인과 효과로 해석하지 않는다.",
        f"- 여유 자금의 보유 도전당 발동 보상은 기본 정책 {savings_base.get('tokens',0)/max(1,savings_base['owners']):.2f}토큰, 비축 정책 {savings_stock.get('tokens',0)/max(1,savings_stock['owners']):.2f}토큰이다. 일반 정책은 돈이 생기는 즉시 주문하므로 이 능력을 과소평가할 수 있다. 서로 다른 정책의 관찰 집계이며 능력 자체의 통제 비교가 아니다.",
        '- 한 번에 마개 2개 이상 쓰는 행동은 기본/비축 모델 모두 드물다. 물건을 모아 특정 순간에 몰아 쓰는 사람의 계획을 단기 효용 모델이 충분히 재현했다고 볼 수 없다.',
        f"- 능력 없음 {off['completion_pct']:.2f}%와 능력 있음 {base['completion_pct']:.2f}%의 차이는 전체 능력 시스템 효과다. 개별 능력의 첫 선택 실험과 분모가 다르다.",
        '- 공격 아이템을 상대에게 예약하면 계산서 없이도 이익을 만들 수 있다. 즉시 자신의 압력을 높이는 손해가 사라지는 대신 양쪽의 공격 공급과 방어 선택이 중요해졌다.',
        '- 저위험 판정이 0%가 되는 것은 마개를 소비해 얻는 공개된 결과다. 한계 도달은 여전히 막을 수 없어서 얼음컵·보냉팩·저강도 선택의 역할이 남는다.',
        f"- 개정 {before_revision} 대비 완주율 변화는 {paired['delta_pp']:+.2f}%p다. 원래와 같은 난도를 유지했다고 주장하지 않으며 실제 긴장감이 충분한지는 사용자의 Play 경험이 필요하다.",
        '- 모든 단계가 5~8분에 들어왔다고 주장하지 않는다. 체력과 애니메이션을 늘려 시간을 강제로 맞추지는 않았다.',
        '- 후반 독립 승률과 도달자 승률을 함께 보면 누적 자원이 진행에 큰 영향을 줄 수 있다. 보스 자체의 단계 차이가 충분한지는 별도 과제다.'])
    censored=sum(x['censored'] for x in result['cases'].values())+sum(x['censored'] for x in result['ablations'].values())+sum(x['censored'] for x in result['isolated_stages'])+sum(x['censored'] for x in result.get('recent_ablations',{}).values())
    lines.extend(['','독립 단계는 플레이어 토큰 0·시작 아이템 2개·능력 없음이며, 보스는 위에 기록한 단계별 시작 자원을 사용한다. 시간의 5~8분 목표 충족 여부는 위 분포와 가정을 함께 봐야 한다.',
        f'안전 한도 중단: {censored}회. 오프라인 모델은 재미·조작감·화면·Unity 동작을 판정하지 않는다.',
        '', '## 결과와 재실행','',
        '`results.json`: 최신 규칙·소스 해시·전체 통계·확률 조합. `comparison.json`: 이전 규칙과 예비 비교. `stages.csv`, `abilities.csv`, `campaign-analysis.png`: 최신 결과.',
        '', '`simulate_campaign.py --design-rebalance --runs 10000 --secondary 2500 --isolated 1000 --ability-runs 1000 --workers 3` 후 `build_campaign_report.py`.'])
    (DEST/'Report.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    with (DEST/'stages.csv').open('w',encoding='utf-8-sig',newline='') as f:
        writer=csv.writer(f);writer.writerow(['revision','policy','n','stage','reached','won','win_pct','mean_minutes','strength1_pct','strength2_pct','strength3_pct'])
        for revision,cases in (('before',{'abilities':old}),('after',result['cases'])):
            for key,case in cases.items():
                for s in case['stages']:writer.writerow([revision,key,case['n'],s['stage'],s['reached'],s['won'],s['win_pct'],s['minutes_think4']['mean'],*s['player_strength_pct']])
    with (DEST/'abilities.csv').open('w',encoding='utf-8-sig',newline='') as f:
        writer=csv.writer(f);writer.writerow(['ability','n','completion_pct','control_pct','delta_pp','ci_low','ci_high','family_low','family_high'])
        for row in (experiment or {}).get('abilities',[]):writer.writerow([rules['abilities'][row['ability']][1],row['n'],row['completion_pct'],row['control_pct'],row['delta_pp'],*row['delta_ci95'],*row['family95']])
    draw_rebalance_charts(before,result,before['cases']['always_2'],design=True)
    print(DEST/'Report.md')


def build_rebalance_report(result):
    comparison=json.loads((DEST/'comparison.json').read_text(encoding='utf-8'))
    before=comparison['before'];a=before['cases']['abilities'];b=result['cases']['abilities']
    old_medium=comparison['baseline_always_2'];pairs=result['paired_comparisons']
    total=pairs['abilities'];r=result['rules']
    lines=['# COLA_COLA 밸런스 변경 전·후 — 2026-10-21','',
        '**게임에 변경을 적용한 뒤 수행한 오프라인 가상 대전이다. Unity Play·컴파일 검사는 실행하지 않았다. 시간은 실측이 아닌 추정이다.**','',
        '## 적용한 변경','',
        '- 양쪽 생존 기본 보상 1/2/3 → 1/3/4토큰. 폭발 기본 보상은 총 5토큰 유지.',
        '- 위험 수당: 75% 이상 생존 +2 → 50% 이상 생존 +1토큰.',
        '- 첫 주문 서비스: 획득 후 다음 주문부터 보스마다 1회 추가 배달. 빈 칸이 2개면 일반 주문 2개를 허용하고 보너스는 보관한다.',
        '- 정리 쿠폰: 획득 시에도 1개 지급. 이후 새 보스마다 1개. 가득 찬 경우 빈 칸이 생길 때까지 예약한다.',
        '- 4단계부터 공격 아이템→계산서 연계를 계획한다. 5단계는 효과가 작은 방어 사용을 보류하고 공개된 플레이어 능력 보상도 응수 계산에 반영한다. 후반 강도 선택 노이즈를 줄였다.',
        '- 체력 5 대 3/4/5/5/5, 압력 증가 2/5/8, 한계 25~35, 제곱 확률 곡선, 아이템 가중치와 벨 기본 가격/개수는 유지한다.','',
        '## 비교 조건','',
        f"기본 조건 전·후 각 {b['n']:,}회. 같은 시드 {result['seed']}, 같은 가상 플레이 정책과 시간 가정을 사용했다. 능력 제거도 각 10,000회, 보조 정책은 각 2,500회다. 압력 선택 판단 4초·아이템 추가 판단 1.5초·벨 추가 판단 1초·능력 선택 6초를 기본으로 한다. Python RNG는 C# System.Random과 같은 시드의 개별 결과 재생을 보장하지 않는다.",
        f"동결한 이전 수치로 재실행한 도달 수와 완주 수가 이전 보고서와 일치했는지: **{result['baseline_replay_matched']}**. 이는 오프라인 모델의 재현 확인이며 Unity 동작 검증은 아니다.",
        '플레이어는 자신의 턴에만 주문한다. 상대 턴 주문, 입력 경합, 실제 사람이 배우는 과정과 중도 이탈은 모델링하지 않는다. 기본 정책은 공개 상태의 단기 효용 평가와 선택 노이즈를 사용하며 장기 최적 전략이 아니다.','',
        '## 단계 도달과 시간','',
        '| 단계 | 도달 전→후 | 후 도달률 | 격파 전→후 | 조건부 승률 전→후 | 평균 시간 전→후 | 후 중앙값 | 후 10~90% 시간 |',
        '|---|---:|---:|---:|---:|---|---|---|']
    for x,y in zip(a['stages'],b['stages']):
        ta,tb=x['minutes_think4'],y['minutes_think4']
        lines.append(f"| {y['stage']} | {x['reached']:,}→{y['reached']:,} | {y['reach_pct']:.1f}% | {x['won']:,}→{y['won']:,} | {x['win_pct']:.1f}→{y['win_pct']:.1f}% | {duration(ta['mean'])}→{duration(tb['mean'])} | {duration(tb['median'])} | {duration(tb['p10'])}~{duration(tb['p90'])} |")
    lines.extend(['','조건부 승률은 그 단계에 도달한 도전만 분모로 한다. 시간에는 그 단계에서 패배한 도전도 포함한다. 후반 도달자는 앞 단계를 이긴 표본이므로 조건부 승률만으로 절대 난도를 판단하지 않는다.','',
        f"완주율 **{a['completion_pct']:.2f}% → {b['completion_pct']:.2f}%** ({b['completed']:,}/{b['n']:,}회). 동일 시드별 완주 변화 {total['delta_pp']:+.2f}%p, 근사 95% 구간 {total['ci95'][0]:+.2f}~{total['ci95'][1]:+.2f}%p.",
        f"성공/실패를 합친 도전 종료까지 평균 {duration(a['total_minutes']['mean'])} → {duration(b['total_minutes']['mean'])}. 완주한 도전만의 평균 {duration(a['completed_minutes']['mean'])} → {duration(b['completed_minutes']['mean'])}. 서로 다른 분모다.", '',
        '## 선택 정책 비교','',
        '| 정책 | 전 완주율 | 후 완주율 | 후 도전 수 | 후 평균 종료 시간 |', '|---|---:|---:|---:|---|'])
    names={'abilities':'기본 계산형','no_abilities':'능력 없음','economy_choices':'경제 능력 우선','always_1':'강도 1 고정',
        'always_2':'강도 2 고정','always_3':'강도 3 고정','random_strength':'강도 무작위','precise_adaptive':'노이즈 없는 단기 상황 판단'}
    for key,name in names.items():
        x=old_medium if key=='always_2' else before['cases'][key];y=result['cases'][key]
        lines.append(f"| {name} | {x['completion_pct']:.2f}% | {y['completion_pct']:.2f}% | {y['n']:,} | {duration(y['total_minutes']['mean'])} |")
    lines.extend(['','## 변경 묶음을 나눈 비교','',
        '아래 비교는 동일 시드 앞 2,500회만 사용한다. 기본 10,000회 결과와 표본 크기가 다르다. 보상→능력→보스 변경의 순서로 효과를 구분하며, 순서에 따라 증분 효과가 달라질 수 있다.','',
        '| 조건 | 완주율 |', '|---|---:|'])
    old_prefix=result['ablations']['rewards_only']['completion_pct']-pairs['rewards_only']['delta_pp']
    final_prefix=result['ablations']['rewards_and_abilities']['completion_pct']+pairs['brain_increment']['delta_pp']
    for label,value in [('이전 수치',old_prefix),('생존 보상만 변경',result['ablations']['rewards_only']['completion_pct']),
        ('생존 보상 + 능력 변경',result['ablations']['rewards_and_abilities']['completion_pct']),('모두 적용',final_prefix)]:
        lines.append(f'| {label} | {value:.2f}% |')
    lines.extend(['',f"후반 보스 판단 추가의 증분은 {pairs['brain_increment']['delta_pp']:+.2f}%p (같은 2,500시드 비교, 근사 95% 구간 {pairs['brain_increment']['ci95'][0]:+.2f}~{pairs['brain_increment']['ci95'][1]:+.2f}%p). 구간에 0이 포함되면 차이를 단정하지 않는다.", '',
        '## 강도 선택 비중','', '| 단계 | 전 1 / 2 / 3 | 후 1 / 2 / 3 |', '|---|---|---|'])
    for x,y in zip(a['stages'],b['stages']):
        lines.append(f"| {y['stage']} | {' / '.join(f'{p:.1f}%' for p in x['player_strength_pct'])} | {' / '.join(f'{p:.1f}%' for p in y['player_strength_pct'])} |")
    lines.extend(['','## 능력 발동량','',
        '해당 능력을 한 번이라도 보유한 도전당 평균이다. 획득 시점·생존 시간·다른 능력이 섞여 있으므로 능력 자체의 인과적 강도 순위가 아니다. 이번에는 첫 능력 12종 각각의 강제 선택 실험을 반복하지 않았다.','',
        '| 능력 | 전→후 보유 도전 | 보유 도전당 추가·절약 토큰 전→후 | 추가 아이템 전→후 |', '|---|---:|---:|---:|'])
    for i,ability in enumerate(r['abilities']):
        x,y=a['effects'][str(i)],b['effects'][str(i)];nx,ny=max(1,x['owners']),max(1,y['owners'])
        lines.append(f"| {ability[1]} | {x['owners']:,}→{y['owners']:,} | {x.get('tokens',0)/nx:.2f}→{y.get('tokens',0)/ny:.2f} | {x.get('items',0)/nx:.2f}→{y.get('items',0)/ny:.2f} |")
    lines.extend(['','## 후반 보스 판단이 실제로 선택된 횟수 — 모델 안의 집계','',
        '| 단계 | 평균 공격 연계 아이템 수 | 평균 연계 후 넘기기 횟수 | 방어 보류 검토 수 평균 |', '|---|---:|---:|---:|'])
    for row in b['stages']:
        lines.append(f"| {row['stage']} | {row['boss_combo_items']['mean']:.2f} | {row['boss_combo_passes']['mean']:.2f} | {row['boss_defenses_saved']['mean']:.2f} |")
    lines.extend(['','방어 보류 검토 수는 같은 아이템을 여러 판단 시점에서 다시 살펴본 횟수도 포함한다. 서로 다른 아이템 수가 아니다.','',
        '## 이월 없는 독립 대전','',
        '플레이어 HP 5·토큰 0·현 단계 시작 아이템 2개·능력 없음으로 각 단계를 독립 시작한다. 각 2,000회이며 이전과 같은 시드를 사용한다.','',
        '| 단계 | 전 승률 | 후 승률 | 후 95% 구간 |', '|---|---:|---:|---|'])
    for x,y in zip(before['diagnostics']['isolated_stages'],result['isolated_stages']):
        lines.append(f"| {y['stage']} | {x['win_pct']:.2f}% | {y['win_pct']:.2f}% | {y['ci95'][0]:.1f}~{y['ci95'][1]:.1f}% |")
    lines.extend(['','## 시간 가정의 민감도','', '| 단계 | 자동 연출만 | 판단 2초 | 판단 4초 | 판단 8초 |', '|---|---|---|---|---|'])
    for row in b['stages']:
        lines.append(f"| {row['stage']} | {duration(row['automatic_minutes']['mean'])} | {duration(row['minutes_think2']['mean'])} | {duration(row['minutes_think4']['mean'])} | {duration(row['minutes_think8']['mean'])} |")
    lines.extend(['','## 이번 변경의 해석','',
        '- 전체 완주율 변화의 짝지은 95% 구간이 0을 포함한다. 전체 난도가 확실히 높아지거나 낮아졌다고 단정하지 않는다.',
        '- 강도 1 고정의 점추정 완주율은 15.36%→11.60%, 노이즈 없는 상황 판단은 13.24%→13.08%다. 이전의 강도 1 고정 우위는 줄었다. 실제 사람의 최적 전략을 증명하는 결과는 아니다.',
        '- 강도 2 선택 비중은 각 단계에서 약 3~5%p 증가했다. 강도 3은 상황 판단 정책에서 사용 비중을 유지하지만 강도 3 고정 정책은 여전히 낮은 완주율이다.',
        '- 위험 수당의 보유 도전당 평균 추가 토큰은 0.13→1.08, 첫 주문 서비스의 추가 아이템은 1.19→1.76, 정리 쿠폰은 1.19→2.16으로 늘었다. 획득 시점이 섞인 관찰 집계이므로 능력의 인과적 강도 순위는 아니다.',
        '- 후반 보스의 연계와 방어 보류는 모델에서 실제로 선택됐다. 그러나 보스 변경만 추가한 효과의 구간은 0을 포함하고 독립 대전의 4/5단계 승률도 가깝다. 후반 난도가 확연히 상승했다고 주장할 근거는 부족하다.',
        '- 보스별 평균은 여전히 약 4~5분, 완주 전체는 약 22분이다. 체력을 일괄 올려 시간을 늘리지는 않았다.',
        '', '## 해석 범위','',
        '- 고정 강도 정책은 그 강도에 맞춰 아이템 효용도 평가한다. 한 버튼이 모든 실제 상황에서 최적이라는 증명이 아니다.',
        '- 더 많은 토큰은 양쪽 모두에게 주문 기회를 준다. 보상 증가가 자동으로 플레이어의 완주율을 올리지는 않는다.',
        '- 전투 시간은 애니메이션을 합산한 근사치다. 동시 동작과 실제 프레임 오버헤드를 완전히 재현하지 않는다.',
        '- 재미·긴장감·실제 플레이어의 학습·능력 선택 취향은 측정하지 않았다. Unity의 동작 검증은 별도다.',
        f"- 정책별 전체 도전 중 안전 한도 중단 {sum(x['censored'] for x in result['cases'].values())}회. 독립 대전 중단 {sum(x['censored'] for x in result['isolated_stages'])}회.",
        '- `comparison.json`은 변경 전 근거, `results.json`은 현재 수치·소스 해시·분리 비교 결과다. CSV와 도표는 최신 비교만 유지한다.', '',
        '재실행: `simulate_campaign.py --rebalance --runs 10000 --secondary 2500 --isolated 2000 --workers 3`, 이후 `build_campaign_report.py`.'])
    (DEST/'Report.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    with (DEST/'stages.csv').open('w',encoding='utf-8-sig',newline='') as f:
        writer=csv.writer(f);writer.writerow(['revision','policy','n','stage','reached','won','lost','win_pct','mean_minutes','median_minutes','strength1_pct','strength2_pct','strength3_pct'])
        for revision,cases in [('before',dict(before['cases'],always_2=old_medium)),('after',result['cases'])]:
            for key,case in cases.items():
                for row in case['stages']:
                    writer.writerow([revision,key,case['n'],row['stage'],row['reached'],row['won'],row['lost'],row['win_pct'],row['minutes_think4']['mean'],row['minutes_think4']['median'],*row['player_strength_pct']])
    with (DEST/'abilities.csv').open('w',encoding='utf-8-sig',newline='') as f:
        writer=csv.writer(f);writer.writerow(['ability','before_owners','after_owners','before_tokens_per_owner','after_tokens_per_owner','before_items_per_owner','after_items_per_owner'])
        for i,ability in enumerate(r['abilities']):
            x,y=a['effects'][str(i)],b['effects'][str(i)];nx,ny=max(1,x['owners']),max(1,y['owners'])
            writer.writerow([ability[1],x['owners'],y['owners'],x.get('tokens',0)/nx,y.get('tokens',0)/ny,x.get('items',0)/nx,y.get('items',0)/ny])
    draw_rebalance_charts(before,result,old_medium)
    print(DEST/'Report.md')


def draw_charts(result):
    from PIL import Image, ImageDraw, ImageFont
    image=Image.new('RGB',(1600,1190),'#f0eee5');draw=ImageDraw.Draw(image)
    def font(size,bold=False): return ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf' if bold else 'C:/Windows/Fonts/malgun.ttf',size)
    def text(x,y,value,size=20,color='#263c30',bold=False):
        draw.text((x,y),str(value),font=font(size,bold),fill=color,stroke_width=0)
    green,orange,gray='#5d8067','#be8e44','#989e91'
    base=result['cases']['abilities'];off=result['cases']['no_abilities'];abilities=result['rules']['abilities']
    text(42,25,'COLA_COLA · 가상 대전 분석',35,bold=True)
    text(44,72,'현재 19종 아이템 · 12종 능력 · 5단계 도전 | 기본 조건 10,000회',19)
    for box in ((40,112,780,575),(820,112,1560,575),(40,610,780,1125),(820,610,1560,1125)):
        draw.rounded_rectangle(box,radius=12,fill='#fffdf7')
    text(65,133,'단계에 도달한 횟수',25,bold=True)
    draw.rectangle((67,179,87,192),fill=green);text(95,172,'능력 있음',18)
    draw.rectangle((225,179,245,192),fill=orange);text(253,172,'능력 없음',18)
    for i,row in enumerate(base['stages']):
        y=230+i*63;scale=470/base['n']
        text(66,y-2,f'{i+1}단계',22)
        draw.rectangle((170,y,170+row['reached']*scale,y+20),fill=green)
        count=off['stages'][i]['reached']/off['n']*base['n']
        draw.rectangle((170,y+27,170+count*scale,y+36),fill=orange)
        text(180+row['reached']*scale,y-4,f"{row['reached']:,}",18)
    text(846,133,'단계 소요 시간',25,bold=True)
    text(846,173,'중앙값과 10~90% 구간 · 턴 판단 4초 가정',18)
    x0,x1,y0,y1=910,1508,505,222
    def ty(v): return y0-v/9*(y0-y1)
    draw.rectangle((x0,ty(8),x1,ty(5)),fill='#e2e9de')
    text(1320,ty(8)+6,'목표 5~8분',16)
    for value in (0,2,4,6,8):
        y=ty(value);draw.line((x0,y,x1,y),fill='#d7d9ce',width=1);text(858,y-12,f'{value}분',17)
    points=[]
    for i,row in enumerate(base['stages']):
        x=x0+46+i*126;t=row['minutes_think4'];y=ty(t['median'])
        draw.line((x,ty(t['p10']),x,ty(t['p90'])),fill=green,width=3)
        for v in (t['p10'],t['p90']):draw.line((x-9,ty(v),x+9,ty(v)),fill=green,width=3)
        points.append((x,y));draw.ellipse((x-5,y-5,x+5,y+5),fill=green)
        text(x-28,520,f'{i+1}단계',18)
    draw.line(points,fill=green,width=3)
    text(65,632,'첫 능력 하나의 완주율 변화',25,bold=True)
    text(65,673,'첫 보상만 비운 경우 대비 %p · 12종 동시 95% 구간',17)
    rows=sorted(result['first_ability_experiment']['abilities'],key=lambda r:r['delta_pp'],reverse=True)
    bound=max(4,max(abs(v) for r in rows for v in r['delta_ci_family95'])+.5)
    def tx(v):return 496+v/bound*225
    for v in (-4,-2,0,2,4):
        x=tx(v);draw.line((x,713,x,1072),fill='#c2c8bc' if v==0 else '#e2e5dc',width=2 if v==0 else 1)
        text(x-13,1080,f'{v:+d}',16)
    for i,row in enumerate(rows):
        y=722+i*29
        text(65,y-12,abilities[row['ability']][1],18)
        lo,hi=row['delta_ci_family95'];color=green if lo>0 else '#ad6654' if hi<0 else gray
        draw.rectangle((min(tx(0),tx(row['delta_pp'])),y-7,max(tx(0),tx(row['delta_pp']))+.1,y+7),fill=color)
        draw.line((tx(lo),y,tx(hi),y),fill='#425146',width=2)
        for v in (lo,hi):draw.line((tx(v),y-4,tx(v),y+4),fill='#425146',width=2)
        draw.ellipse((tx(row['delta_pp'])-3,y-3,tx(row['delta_pp'])+3,y+3),fill='#425146')
    text(846,632,'선택 정책별 완주율',25,bold=True)
    text(846,673,'능력은 기본 무작위 선택 · 경제 우선은 별도 표시',17)
    labels=[('abilities','기본 계산형'),('no_abilities','능력 없음'),('economy_choices','경제 능력 우선'),
            ('always_1','강도 1 고정'),('always_3','강도 3 고정'),('random_strength','강도 무작위'),('precise_adaptive','노이즈 없는 상황 판단')]
    for i,(key,label) in enumerate(labels):
        if key not in result['cases']:continue
        y=731+i*49;value=result['cases'][key]['completion_pct'];x=1098+value/20*335
        text(846,y-13,label,18)
        draw.rectangle((1098,y-9,x,y+12),fill=orange if key=='no_abilities' else green)
        text(x+10,y-14,f'{value:.2f}%',18)
    text(43,1145,'오프라인 모델 결과입니다. 실제 사람의 승률·플레이 시간 또는 Unity Play 검증 결과가 아닙니다.',20)
    image.save(DEST/'campaign-analysis.png')


def main():
    result=json.loads((DEST/'results.json').read_text(encoding='utf-8'))
    if result['kind']=='offline_item_rework_not_unity_play':
        from build_item_rework_report import main as build_item_report
        build_item_report()
        return
    if result['kind']=='offline_shared_abilities_not_unity_play':
        from build_shared_abilities_report import main as build_shared_report
        build_shared_report()
        return
    if result['kind']=='offline_design_rebalance_not_unity_play':
        build_design_report(result)
        return
    if result['kind']=='offline_rebalance_model_not_unity_play':
        build_rebalance_report(result)
        return
    diagnostic_path=DEST/'diagnostics.json'
    if diagnostic_path.exists():
        diagnostic=json.loads(diagnostic_path.read_text(encoding='utf-8'))
        if 'precise_policy' in diagnostic: result['cases']['precise_adaptive']=diagnostic['precise_policy']
    cases=result['cases']; base=cases['abilities']; off=cases['no_abilities']
    abilities=result['rules']['abilities']; items=result['rules']['items']
    experiments=result.get('first_ability_experiment',{}).get('abilities',[])
    # Exploratory twelve-way comparison: also retain simultaneous family-wise
    # 95% Bonferroni intervals, instead of presenting twelve unadjusted positives.
    critical=NormalDist().inv_cdf(1-.05/(2*max(1,len(experiments))))
    for row in experiments:
        se=(row['delta_ci95'][1]-row['delta_ci95'][0])/(2*1.96)
        row['delta_ci_family95']=[row['delta_pp']-critical*se,row['delta_pp']+critical*se]
    result['multiple_comparison_adjustment']='Bonferroni normal intervals for twelve paired first-ability effects; individual intervals also retained.'
    (DEST/'results.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    labels={'abilities':'능력 무작위 선택','no_abilities':'능력 없음','economy_choices':'경제 능력 우선',
            'always_1':'강도 1 고정','always_3':'강도 3 고정','random_strength':'강도 무작위',
            'precise_adaptive':'상황 판단 · 강도 선택 노이즈 없음'}
    report=['# COLA_COLA 가상 대전 분석', '',
        f"생성: {result['created_utc']} / 기본 시드: {result['seed']}", '',
        '**오프라인 통계 모델이다. Unity Play, 실제 플레이 시간 측정, 화면/애니메이션 검증을 수행하지 않았다. 게임 설정은 변경하지 않았다.**', '',
        '## 실험 조건', '',
        f"- 기본 도전 {base['n']:,}회, 능력 제거 비교 {off['n']:,}회. 보조 선택 정책은 각 {cases['always_1']['n']:,}회.",
        '- 현재 C# 소스에서 19종 아이템, 12종 능력, 단계별 가중치와 수치를 읽었다. 소스 SHA-256은 results.json에 저장했다.',
        f"- 한계 압력 {result['rules']['minimum']}~{result['rules']['maximum']}, 확률 지수 {result['rules']['exponent']:g}, 강도별 압력 +{result['rules']['steps']}. 플레이어 체력 {result['rules']['player_hp']}, 보스 체력 {result['rules']['boss_hp']}.",
        '- 기본 플레이어는 공개 압력·체력·효과·토큰으로 다음 수를 평가하는 계산형 정책이다. 사람의 행동/실력을 대표하거나 최적 플레이를 증명하지 않는다.',
        '- 보스는 현행 C#의 공개 상태 평가, 단계별 선택 노이즈, 아이템 평가 및 턴당 최대 1회 벨 구매를 반영했다. 플레이어는 자신의 턴에 돈과 공간이 되면 제한 없이 주문한다. 상대 턴 주문과 동시 입력 경합은 모델링하지 않았다.',
        '- 능력은 합산 폭발 3회마다 미보유 3종을 제시하고 무작위로 하나 선택한다. 경제 우선 정책은 별도 비교한다. 아이템 사용 횟수 제한은 없다.',
        '- 폭발한 쪽이 새 콜라의 첫 턴을 갖는다. 새 보스에서 플레이어 HP는 회복하고 토큰·아이템·능력은 이어진다. 보스 지갑/인벤토리는 초기화한다. 핫소스는 양쪽 합산 콜라당 1회 자연 추첨한다.',
        '- Python RNG를 독립 스트림으로 사용한다. C# System.Random과 같은 시드의 개별 결과가 일치하는 재생은 아니다.',
        '- 기본 시간: 플레이어 턴 판단 4초, 아이템당 추가 판단 1.5초, 벨당 추가 판단 1초, 능력 선택 6초. 흔들기·결과 대기·폭발·암전·페이드·배달은 소스의 연출 시간을 합산했다. 프레임 지연, 일시정지, 별도 기록부 열람과 다음 보스 클릭 대기는 제외한다.',
        '- 표의 진입 수는 최초 도전 대비 횟수이고, 단계 승률은 해당 단계 진입자만을 분모로 한다. 단계 시간에는 해당 단계에서 패배한 도전도 포함한다.', '',
        '## 단계 도달과 소요 시간', '',
        '| 단계 | 도달 | 최초 대비 | 격파 | 해당 단계 패배 | 조건부 승률 | 평균 시간 | 중앙값 | 10~90% 구간 | 격파한 경우 평균 |',
        '|---|---:|---:|---:|---:|---:|---|---|---|---|']
    for row in base['stages']:
        t=row['minutes_think4'];w=row['won_minutes']
        report.append(f"| {row['stage']} | {row['reached']:,} | {row['reach_pct']:.1f}% | {row['won']:,} | {row['lost']:,} | {row['win_pct']:.1f}% | {duration(t['mean'])} | {duration(t['median'])} | {duration(t['p10'])}~{duration(t['p90'])} | {duration(w['mean']) if w else '—'} |")
    report.extend(['',f"완주: **{base['completed']:,}/{base['n']:,}회 ({base['completion_pct']:.2f}%, Wilson 95% 구간 {base['completion_ci95'][0]:.2f}~{base['completion_ci95'][1]:.2f}%)**.",
        f"전체 도전 종료까지 평균 {duration(base['total_minutes']['mean'])}, 중앙값 {duration(base['total_minutes']['median'])}. 완주한 도전만의 평균은 {duration(base['completed_minutes']['mean'])}이다. 이 둘은 분모가 다르다.", '',
        '## 시간 가정에 따른 차이', '',
        '| 단계 | 자동 연출만 평균 | 턴 판단 2초 | 턴 판단 4초 | 턴 판단 8초 | 4초 기준 5분 미만 | 8분 초과 |',
        '|---|---|---|---|---|---:|---:|'])
    for row in base['stages']:
        report.append(f"| {row['stage']} | {duration(row['automatic_minutes']['mean'])} | {duration(row['minutes_think2']['mean'])} | {duration(row['minutes_think4']['mean'])} | {duration(row['minutes_think8']['mean'])} | {row['under5_pct']:.1f}% | {row['over8_pct']:.1f}% |")
    report.extend(['','2/4/8초 열은 압력 선택 판단 시간만 바꾸고 아이템·벨·능력 선택 가정은 유지한다. 자동 연출 열에는 사람의 판단 시간을 넣지 않았다.', '',
        '## 능력 유무와 선택 정책', '',
        '| 정책 | 도전 수 | 완주율 | 도전 종료 평균 | 완주 도전 평균 | 평균 보유 능력 |',
        '|---|---:|---:|---|---|---:|'])
    for key,case in cases.items():
        report.append(f"| {labels[key]} | {case['n']:,} | {case['completion_pct']:.2f}% | {duration(case['total_minutes']['mean'])} | {duration(case['completed_minutes']['mean']) if case['completed_minutes'] else '—'} | {case['ability_count']['mean']:.2f} |")
    report.extend(['',f"기본 능력 시스템의 완주율 차이는 능력 없음 대비 **{base['completion_pct']-off['completion_pct']:+.2f}%p**다. 무작위 능력과 경제 우선 선택의 차이는 서로 다른 선택 정책의 결과이며 능력 하나의 인과 효과와 같지 않다.", '',
        '## 개별 능력의 통제 비교', '',
        '첫 능력 선택 직전의 동일 상태를 복제해, 해당 능력을 첫 보상으로 고정한 경우와 그 첫 보상만 비운 경우를 비교했다. 이후 폭발/아이템 규칙과 무작위 능력 선택은 정상 유지한다. 따라서 “초기에 이 능력을 받는 효과”이며, 모든 상황의 능력 순위나 최적 빌드 평가가 아니다. 통제군도 나중에 이 능력을 받을 수 있다. 아래 구간은 12종 동시 비교를 Bonferroni로 보정한 근사 95% 구간이다. 구간에 0이 포함되면 이번 표본만으로 차이를 단정하지 않는다. 개별 95% 구간도 JSON/CSV에 함께 보관한다.', '',
        '| 능력 | 비교 표본 | 완주율 | 통제 대비 | 동시 비교 95% 구간 | 판독 |',
        '|---|---:|---:|---:|---|---|'])
    if experiments:
        report.insert(len(report)-3, f"첫 보상만 비운 통제군은 {experiments[0]['n']:,}상태 중 {experiments[0]['control_pct']:.2f}%가 완주했다. 이 통제군은 ‘도전 내내 능력 없음’ 조건과 다르다.")
    for row in sorted(experiments,key=lambda x:x['delta_pp'],reverse=True):
        lo,hi=row['delta_ci_family95'];signal='양의 차이' if lo>0 else '음의 차이' if hi<0 else '불확실'
        report.append(f"| {abilities[row['ability']][1]} | {row['n']:,} | {row['completion_pct']:.2f}% | {row['delta_pp']:+.2f}%p | {lo:+.2f}~{hi:+.2f}%p | {signal} |")
    report.extend(['','## 실제 발생한 능력 보너스 — 모델 안의 집계', '',
        f"아래는 기본 {base['n']:,}회에서 해당 능력을 한 번이라도 보유한 도전당 평균이다. 능력을 늦게 받거나 일찍 탈락한 경우도 포함하므로, 능력 자체의 고정 성능 수치로 해석하지 않는다.", '',
        '| 능력 | 보유한 도전 | 추가 토큰/보유 도전 | 추가 아이템/보유 도전 | 기타 발동 합계 |',
        '|---|---:|---:|---:|---|'])
    for i,ability in enumerate(abilities):
        effect=base['effects'][str(i)];n=max(1,effect['owners'])
        extra={k:v for k,v in effect.items() if k not in ('owners','turns_owned','tokens','items')}
        report.append(f"| {ability[1]} | {effect['owners']:,} | {effect.get('tokens',0)/n:.2f} | {effect.get('items',0)/n:.2f} | {json.dumps(extra,ensure_ascii=False) if extra else '—'} |")
    report.extend(['','## 진행·경제와 선택', '',
        '| 단계 | 진입 시 평균 토큰 | 평균 보유 능력 | 내 평균 벨 주문 | 내 평균 아이템 사용 | 흔들기 1/2/3 비중 |',
        '|---|---:|---:|---:|---:|---|'])
    for row in base['stages']:
        report.append(f"| {row['stage']} | {row['entry_tokens']['mean']:.1f} | {row['entry_abilities']['mean']:.2f} | {row['player_orders']['mean']:.2f} | {row['player_items']['mean']:.2f} | {' / '.join(f'{v:.1f}%' for v in row['player_strength_pct'])} |")
    report.extend(['','## 아이템 사용 빈도', '',
        '자연 생성된 아이템 대비 양쪽의 사용 비율이다. 취소 번호표로 제거, 끝까지 보유, 교환으로 교체된 아이템도 분모에 남는다. 저사용률은 이 모델의 판단 정책 영향도 받으며 재미를 직접 측정하지 않는다.', '',
        '| 아이템 | 생성 | 플레이어 사용 | 보스 사용 | 생성 대비 사용 |', '|---|---:|---:|---:|---:|'])
    for i,item in enumerate(items):
        row=base['item_stats'][str(i)]
        report.append(f"| {item[1]} | {row['generated']:,} | {row['player_used']:,} | {row['boss_used']:,} | {row['used_pct']:.1f}% |")
    report.extend(['','## 설계상 알아둘 변수', '',
        '- 스테이지별 최대 HP 차이는 보스 3/4/5/5/5다. 3~5단계의 차이는 HP 외에 아이템 풀, AI 선택 노이즈, 누적 자원과 능력에서 생긴다. 후반 도달자는 이미 앞 단계를 이긴 표본이므로 후반 조건부 승률만으로 절대 난도를 비교하면 안 된다.',
        '- 단골 할인은 반복 주문마다, 가벼운 손·과감한 선택은 강도 선택마다 자원을 바꾼다. 능력은 폭발 운뿐 아니라 아이템 공급량을 통해 간접적으로 생존/공격을 바꾼다.',
        '- 냉각 숙련은 강도 1에서 기본 보냉팩만으로 증가량이 이미 0이라 추가로 줄일 수 없다. 강도 2/3에서만 압력 -1의 실제 추가 효과가 있다.',
        '- 위험 수당은 75% 이상에서 살아야 발동한다. 임계치에서조차 생존 확률은 최대 25%다. 더 큰 위험을 감수하도록 유도하지만 단순 안전 플레이에는 거의 자원을 주지 않는다.',
        '- 첫 주문 서비스를 첫 벨 주문 후 획득하면 그 보스에서는 발동하지 않는다. 정리 쿠폰을 5단계에서 받으면 다음 보스가 없어 그 도전에서는 발동할 수 없다.',
        '- 수납 확장은 트레이가 실제로 막힐 때만 주문 선택을 바꾼다. 즉시 유효 아이템을 소비하는 정책에서는 가치가 과소평가될 수 있다.',
        '- 짠맛 취향은 소금의 압력을 늘리므로 내 다음 흔들기도 위험하게 만들 수 있다. 계산서와의 조합이나 상대에게 차례를 넘기는 맥락이 중요하다.',
        '- 생존·완주율은 이 모델의 성과 지표이고 긴장감·조작감·초보자의 이해도·실제 재미는 평가하지 못한다. 시간 가정의 오차는 표본을 늘려도 사라지지 않는다.',
        f"- 안전 한도로 중단된 가상 도전: {sum(c['censored'] for c in cases.values())}회. 주요 비교에 중단 표본이 있으면 결과 해석에 포함해야 한다.",
        '', '## 결과 파일', '', '- `results.json`: 수치, 모델 가정, 소스 해시와 실험 결과.',
        '- `stages.csv`: 모든 정책의 단계별 통계.', '- `abilities.csv`: 첫 능력 통제 비교.',
        '- `campaign-analysis.png`: 주요 결과 도표.', '',
        '재실행: `Tools/Balance/simulate_campaign.py` 후 `Tools/Balance/build_campaign_report.py`. 게임 코드와 에셋을 변경하지 않는다.'])
    diagnostic_path=DEST/'diagnostics.json'
    if diagnostic_path.exists():
        diag=json.loads(diagnostic_path.read_text(encoding='utf-8'))
        report.extend(['','## 이월 자원을 제거한 단계별 대조', '',
            '각 단계를 독립적으로 시작한다. 플레이어 HP 5·토큰 0·현 단계 무작위 아이템 2개·능력 없음으로 맞췄으며, 보스도 현 단계 시작 상태다. 기본 도달 통계와 달리 이전 단계의 생존자 선택과 자원 이월이 없다.', '',
            '| 단계 | 독립 대전 수 | 플레이어 승리 | 승률 | 95% 구간 | 평균 시간 |', '|---|---:|---:|---:|---|---|'])
        for row in diag['isolated_stages']:
            report.append(f"| {row['stage']} | {row['n']:,} | {row['wins']:,} | {row['win_pct']:.1f}% | {row['ci95'][0]:.1f}~{row['ci95'][1]:.1f}% | {duration(row['minutes']['mean'])} |")
        first=diag['first_actor_stage3'];late=diag['late_choice_probe']
        report.extend(['',
            f"3단계 독립 상태 {first['n']:,}쌍에서 플레이어 선공 승률 {first['player_starts_pct']:.1f}%, 보스 선공 {first['boss_starts_pct']:.1f}%. 같은 초기 상태의 차이는 {first['delta_pp']:+.2f}%p (근사 95% 구간 {first['delta_ci95'][0]:+.2f}~{first['delta_ci95'][1]:+.2f}%p). 정책은 그대로 유지하므로 인간에게 보편적인 선공 이득을 뜻하지 않는다.",
            f"별도 {late['n']:,}도전의 선택 시점 점검: 정리 쿠폰 선택 {late['tidy_picks']}회 중 5단계 선택 {late['tidy_stage5']}회(해당 도전에서 다음 보스 없음). 첫 주문 서비스 선택 {late['service_picks']}회 중 그 보스의 첫 주문 뒤에 받은 경우 {late['service_after_first_order']}회(다음 보스까지 대기).", '',
            '추가 수치는 `diagnostics.json`에 보관했다. 재실행은 `simulate_campaign.py --diagnostics-only 2000`이다.'])
        if 'precise_policy' in diag:
            precise=diag['precise_policy']
            report.extend(['',f"강도 선택의 무작위성만 없앤 단기 상황 판단 정책 {precise['n']:,}회는 완주율 {precise['completion_pct']:.2f}%다. 기본 계산형의 .065 온도/탐험 노이즈 때문에 발생하는 선택 실수와 강도 1 고정의 효과를 구분하기 위한 보조 실험이다. 장기 최적 전략은 아니며 아이템 평가의 변동은 유지했다. 재실행: `simulate_campaign.py --precision-only 2500`."])
    conclusions=[
        '## 먼저 볼 결론', '',
        f"1. 능력 무작위 선택의 완주율은 {base['completion_pct']:.2f}%, 능력 없음은 {off['completion_pct']:.2f}%다. 여러 능력이 누적되는 전체 시스템의 차이는 {base['completion_pct']-off['completion_pct']:+.2f}%p다.",
        f"2. 경제 능력 우선 선택은 {cases['economy_choices']['completion_pct']:.2f}%. 어떤 능력을 고르느냐도 진행에 영향을 준다. 다만 이는 사전에 정한 선택 정책의 비교이며 최적 빌드 증명은 아니다.",
        f"3. 강도 1 고정 정책은 {cases['always_1']['completion_pct']:.2f}%, 강도 3 고정은 {cases['always_3']['completion_pct']:.2f}%다. 강도 선택과 그에 맞춘 아이템 판단이 함께 달라지는 실험이다. 현재 정책 아래에서는 고강도 고정의 위험이 수입 이득을 크게 앞선다.",
        '4. 가벼운 손은 강도 1의 기본 생존 수입을 1→2(+100%)로, 과감한 선택은 강도 3을 3→4(+33%)로 올린다. 같은 +1이라도 선택에 주는 상대적 영향이 다르다.',
        '5. 5~8분 목표와의 비교에는 평균뿐 아니라 짧게 끝나는 비율, 판단 시간 민감도와 패배한 도전을 포함했는지를 함께 봐야 한다.', '',
        '이 분석을 근거로 게임 밸런스 수치를 자동 변경하지 않았다.', '']
    at=report.index('## 실험 조건');report[at:at]=conclusions
    report.extend(['','## 능력의 수치적 작용', '',
        '| 능력 | 직접 변화와 주의할 맥락 |', '|---|---|',
        '| 단골 할인 | 벨 6→5토큰. 같은 30토큰으로 5→6회 주문할 수 있음 |',
        '| 가벼운 손 | 강도 1 생존 수입 1→2토큰. 기본 수입의 +100% |',
        '| 과감한 선택 | 강도 3 생존 수입 3→4토큰. 기본 수입의 +33% |',
        '| 파손 보상 | 내 폭발 수입 5→7토큰. 체력 피해는 그대로 |',
        '| 첫 주문 서비스 | 각 보스의 첫 벨 배달 2→3개. 그 주문 전 획득해야 즉시 적용 |',
        '| 정리 쿠폰 | 다음 보스부터 시작 시 교환권 1개. 공간이 없거나 다음 보스가 없으면 지급 없음 |',
        '| 짠맛 취향 | 소금의 압력 +3→+4. 내 흔들기 전 압력도 높아짐 |',
        '| 냉각 숙련 | 보냉팩 적용 증가량 0/3/6→0/2/5. 강도 1에는 추가 효과 없음 |',
        '| 뜨거운 거래 | 핫소스 콜라에서 생존 수입 +1. 핫소스 공급과 사용이 필요 |',
        '| 재활용 적립 | 획득 후 사용한 아이템 3개마다 1토큰. 중간에 획득하면 이전 사용분은 계산하지 않음 |',
        '| 넉넉한 수납 | 8→10칸(+25%). 공간 부족으로 주문을 못 할 때 실질 가치가 생김 |',
        '| 위험 수당 | 75% 이상에서 생존 시 +2토큰. 75% 지점에서 보너스 기대값은 흔들기당 0.5토큰 |'])
    probability=base['completion_pct']/100
    report.extend(['','## 반복 도전으로 환산하면', '',
        f"동일한 정책·실력을 유지하고 모든 실패 후 새 도전을 한다는 가정에서 첫 완주까지 필요한 도전 횟수의 기대값은 1/p = {1/probability:.2f}회다. 실패/성공 도전 시간을 함께 반영한 첫 완주까지의 기대 누적 시간은 {duration(base['total_minutes']['mean']/probability)}다. 학습·중도 포기·능력 선택 취향 변화·휴식은 포함하지 않아 실제 사람의 클리어 시간 예측으로 사용할 수 없다."])
    (DEST/'Report.md').write_text('\n'.join(report)+'\n',encoding='utf-8')
    with (DEST/'stages.csv').open('w',encoding='utf-8-sig',newline='') as f:
        writer=csv.writer(f);writer.writerow(['case','stage','runs','reached','won','lost','reach_pct','conditional_win_pct','mean_minutes','median_minutes','p10_minutes','p90_minutes','auto_minutes'])
        for label,case in cases.items():
            for row in case['stages']:
                t=row['minutes_think4'];writer.writerow([label,row['stage'],case['n'],row['reached'],row['won'],row['lost'],row['reach_pct'],row['win_pct'],t['mean'],t['median'],t['p10'],t['p90'],row['automatic_minutes']['mean']])
    with (DEST/'abilities.csv').open('w',encoding='utf-8-sig',newline='') as f:
        writer=csv.writer(f);writer.writerow(['ability','n','completion_pct','control_pct','delta_pp','delta_ci95_low','delta_ci95_high','family95_low','family95_high'])
        for row in experiments: writer.writerow([abilities[row['ability']][1],row['n'],row['completion_pct'],row['control_pct'],row['delta_pp'],*row['delta_ci95'],*row['delta_ci_family95']])
    draw_charts(result)
    print(DEST/'Report.md')


if __name__=='__main__':main()
