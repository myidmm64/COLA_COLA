"""Latest offline item redesign report. Does not run Unity or change gameplay."""
import csv,json,statistics as st
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[2]
DEST=ROOT/'output/verification/latest/campaign-balance'

def duration(v):
 sec=round(v*60);return f'{sec//60}분 {sec%60:02d}초'

def main():
 data=json.loads((DEST/'results.json').read_text(encoding='utf-8'));pilot=json.loads((DEST/'item-drop-comparison.json').read_text(encoding='utf-8'))
 rules=data['rules'];main=data['cases']['abilities'];baseline=data['cases']['previous_rules'];stages=main['stages']
 lines=['# 아이템 개편과 등장 확률 — 개정 8','',
 '오프라인 가상 대전 결과다. 게임 내 동작·화면·번역 표시·메뉴 동작의 Play 검증은 실행하지 않았다. 컴파일 결과는 상위 폴더의 Summary.md와 compilation/Compilation.txt에 따로 기록한다.','',
 '## 적용한 효과','',
 '| 아이템 | 효과 |','|---|---|',
 '| 취소 번호표 | 토큰 +1. 상대 아이템 1개 제거. 상대가 비어 있어도 사용 가능 |',
 '| 교환 집게 | 상대 무작위 아이템 1개 획득. 없으면 무작위 획득 |',
 '| 빨대 | 토큰 +2. 상대에게 있는 만큼 최대 2개를 빼앗고 부족분은 생성 |',
 '| 계산 펀치 | 상대 다음 흔들기 토큰 -3. 중복 가능, 보상 최저 0 |',
 '| 교환권 | 자기 자신을 다른 무작위 아이템으로 교체 |', '',
 '- 자기 아이템을 우클릭으로 0.55초 누르면 반납하고 1토큰을 받는다. 한 번 누르기에 한 개만 반납한다. 자기 턴에만 가능하며 사용·배달 중에는 막는다.',
 '- 반납은 아이템 사용 횟수·재활용 적립·아이템 사용 업적으로 계산하지 않는다. 보스도 공간이 막힐 때 가치가 낮은 아이템을 같은 가격으로 반납할 수 있다.',
 '- 교환·탈취 결과는 사용 중인 원래 모델을 먼저 분리한 뒤 사용 연출이 끝나면 빈 슬롯에 지급한다. 새 아이템을 사용 연출이 가져가 버리지 않도록 분리했다.',
 '- 계산 펀치는 기존 보유 토큰 대신 다음 실제 흔들기의 최종 보상을 줄인다. 폭발 보상·능력 보너스를 합친 뒤 차감하며, 넘기기와 새 콜라에서도 대상이 흔들 때까지 유지한다. 잔고 정리 업적도 이 효과로 보상 0을 만드는 조건으로 바꿨다.', '',
 '## 등장 확률 선택','',
 '같은 시드로 세 구성을 각 3,000회 비교했다. 강화된 취소/탈취/교환의 빈도를 낮추고, 방어 공급을 유지한 moderate 구성을 채택했다. 다른 시드로 채택 구성을 10,000회 분석했다. 차이는 전후 모든 요소를 묶은 비교이며 개별 효과의 인과 분석은 아니다.','',
 '| 구성 | 완주율 | 단계별 평균 시간(분) |','|---|---:|---|']
 for key in ('original_weights','moderate','more_pressure'):
  case=pilot['cases'][key];label={'original_weights':'효과 개편 + 기존 확률','moderate':'취소/탈취 빈도 완화 (채택)','more_pressure':'공격 비중 추가 증가'}[key]
  lines.append(f"| {label} | {case['completion_pct']:.2f}% | "+' / '.join(f"{s['minutes_think4']['mean']:.2f}" for s in case['stages'])+' |')
 lines+=['','### 적용한 가중치','',
 '각 단계 합계 100. 제외 조건을 적용하기 전 첫 추첨 가중치다. 핫소스 제한·피자 시작 제외·동일 배달 내 종류 중복 제외 시 남은 가중치로 다시 계산한다. 아래 수치는 두 개 배달 중 한 번이라도 등장할 확률이 아니다.','',
 '| 아이템 | 1단계 | 2단계 | 3단계 | 4단계 | 5단계 |','|---|---:|---:|---:|---:|---:|']
 for i,(_,name) in enumerate(rules['items']):lines.append('| '+name+' | '+' | '.join(str(row[i])+'%' if row[i] else '—' for row in rules['weights'])+' |')
 lines+=['','## 본 분석: 10,000회','',
 '| 단계 | 도달 | 도달률 | 격파 | 도달 후 격파율 (95% 구간) | 평균 시간 |','|---|---:|---:|---:|---|---|']
 for s in stages:
  lo,hi=s['win_ci95'];lines.append(f"| {s['stage']} | {s['reached']:,} | {s['reach_pct']:.2f}% | {s['won']:,} | {s['win_pct']:.2f}% ({lo:.1f}–{hi:.1f}) | {duration(s['minutes_think4']['mean'])} |")
 lines+=['',f"완주 **{main['completed']:,}/{main['n']:,} = {main['completion_pct']:.2f}%** (95% {main['completion_ci95'][0]:.2f}–{main['completion_ci95'][1]:.2f}%). 완주 도전 평균 {duration(main['completed_minutes']['mean'])}. 모든 종료 도전 평균 {duration(main['total_minutes']['mean'])}.",'',
 '## 해석과 한계','',
 f"- 같은 시작 시드의 이전 규칙 표본은 {baseline['n']:,}회, 완주율 {baseline['completion_pct']:.2f}%. 새 규칙 기본은 {main['n']:,}회, {main['completion_pct']:.2f}%. 표본 수가 다르고 RNG 소비 경로도 바뀌므로 이 차이에 짝지은 검정이나 특정 아이템의 인과 효과를 주장하지 않는다.",
 f"- 토큰 비축 정책은 {data['cases']['stockpile']['completion_pct']:.2f}%, 경제/방어 능력 우선 정책은 {data['cases']['economy_choices']['completion_pct']:.2f}% (각 2,500회). 비축 플레이의 유용성이 유지됐다.",
 f"- 플레이어만 능력이 없는 경우 {data['cases']['no_abilities']['completion_pct']:.2f}% (2,500회). 보스의 고정 능력 0/1/2/4/6개는 유지했다.",
 '- AI 모델은 가치가 있는 아이템을 적극 사용하므로 사용자가 아이템을 보관하며 겪는 슬롯 부족을 충분히 재현하지 못한다. 반납 횟수는 이 정책에서 매우 적다. 슬롯 답답함이 해결됐다는 실제 체감 검증은 아니다.',
 '- 반납은 양쪽 모두 유료 주문을 위한 공간이 부족하고, 자금이 있으며, 남겨 둔 아이템 가치가 낮을 때만 모델링한다. 사람은 원할 때 어떤 아이템도 반납할 수 있다. 재활용 적립에서는 제외했다.',
 '- 기본 능력 선택은 무작위 후보 3개 중 무작위 1개. 경제 정책은 고정 우선순위, 비축 정책은 체력 3 이상에서 주문 후 12토큰을 남기려 한다.',
 '- 공개 압력/한계·체력·토큰·효과로 다음 상대 위험과 보상을 평가한다. 보스는 단계별 판단 노이즈와 공개 상태 아이템 전술을 사용하며 턴당 주문 1회다. 플레이어의 상대 턴 주문·실시간 입력 경합·고민과 학습은 모델링하지 않았다.',
 '- 시간은 동작 길이에 턴 판단 4초, 아이템 판단 1.5초, 벨 주문 1초, 능력 선택 6초를 더한 추정이다. 실제 프레임 성능이나 사람의 소요 시간을 측정하지 않았다.',
 f"- 전체 분석과 후보 비교의 안전 한도 중단은 {sum(c['censored'] for c in data['cases'].values())+sum(c['censored'] for c in pilot['cases'].values())}회. 중단을 승리로 간주하지 않는다.",'',
 '## 재현','', '```powershell','python -B Tools/Balance/rebalance_item_drops.py --runs 3000','python -B Tools/Balance/rebalance_item_drops.py --final --runs 10000','python -B Tools/Balance/build_item_rework_report.py','```','',
 '원시 수치와 실행 시점 소스 해시는 results.json, 후보 비교는 item-drop-comparison.json, 이전 규칙은 comparison.json의 items_before에 있다. 이 분석은 기존 보스 능력 수 비교를 대체하지 않으며 보스 능력 수는 그대로 유지했다.']
 (DEST/'Report.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
 with (DEST/'stages.csv').open('w',encoding='utf-8-sig',newline='') as f:
  w=csv.writer(f);w.writerow(['stage','reached','reach_pct','won','win_pct','ci_low','ci_high','mean_minutes','player_return_mean','boss_return_mean'])
  for s in stages:w.writerow([s['stage'],s['reached'],s['reach_pct'],s['won'],s['win_pct'],*s['win_ci95'],s['minutes_think4']['mean'],s['returned'][0]['mean'],s['returned'][1]['mean']])
 with (DEST/'abilities.csv').open('w',encoding='utf-8-sig',newline='') as f:
  w=csv.writer(f);w.writerow(['id','enum','name','owners','observed_effects_not_causal'])
  for i,a in enumerate(rules['abilities']):w.writerow([i,a[0],a[1],main['effects'][str(i)]['owners'],json.dumps(main['effects'][str(i)],ensure_ascii=False)])
 with (DEST/'items.csv').open('w',encoding='utf-8-sig',newline='') as f:
  w=csv.writer(f);w.writerow(['item','stage1','stage2','stage3','stage4','stage5','generated','player_used','boss_used'])
  for i,a in enumerate(rules['items']):stats=main['item_stats'][str(i)];w.writerow([a[1],*[r[i] for r in rules['weights']],stats['generated'],stats['player_used'],stats['boss_used']])
 draw(data)
 print('Item balance report, chart and CSV updated.')

def draw(data):
 im=Image.new('RGB',(1500,850),'#eeece3');d=ImageDraw.Draw(im)
 def text(x,y,value,size=20,bold=False):d.text((x,y),str(value),font=ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf' if bold else 'C:/Windows/Fonts/malgun.ttf',size),fill='#2b3b30')
 c=data['cases']['abilities'];text(40,25,'COLA_COLA · 아이템 개편 / 개정 8',34,True);text(42,78,'가상 대전 10,000회 · 취소/탈취 비중 완화 · 실제 Unity 플레이 측정 아님',18)
 for box in [(35,125,740,790),(770,125,1465,790)]:d.rounded_rectangle(box,radius=12,fill='#fffdf7')
 text(61,148,'단계 도달률',25,True);text(794,148,'도달 후 격파율과 평균 시간',25,True)
 for i,s in enumerate(c['stages']):
  y=223+i*94;text(62,y,f'{i+1}단계',22)
  d.rectangle((169,y+4,169+s['reach_pct']*4.5,y+34),fill='#668571');text(179+s['reach_pct']*4.5,y,f"{s['reach_pct']:.1f}%",18)
  text(797,y,f'{i+1}단계',22);d.rectangle((900,y+4,900+s['win_pct']*4.2,y+30),fill='#a79c71');text(910+s['win_pct']*4.2,y,f"{s['win_pct']:.1f}%",18)
  text(900,y+39,duration(s['minutes_think4']['mean']),17)
 text(61,720,f"최종 완주 {c['completed']:,}회 / {c['completion_pct']:.2f}%",24,True)
 text(797,720,f"완주 도전 평균 {duration(c['completed_minutes']['mean'])}",22,True)
 im.save(DEST/'campaign-analysis.png')
if __name__=='__main__':main()
