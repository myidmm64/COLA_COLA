import fs from 'node:fs/promises';
import path from 'node:path';
import {createRequire} from 'node:module';
import {pathToFileURL, fileURLToPath} from 'node:url';
const require = createRequire('C:/Users/myidm/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bootstrap.cjs');
const {Workbook, SpreadsheetFile, FileBlob} = await import(pathToFileURL(require.resolve('@oai/artifact-tool')).href);
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const destination = path.join(root, 'Assets/Localization/COLA_COLA_Translations.xlsx');
if (process.argv.includes('--return-click') || process.argv.includes('--preview-return')) {
 const workbook=await SpreadsheetFile.importXlsx(await FileBlob.load(destination));
 const sheet=workbook.worksheets.getItemAt(0), grid=sheet.getUsedRange().values;
 const header=grid.findIndex(row=>row[0]==='Key'), index=grid.findIndex(row=>row[0]==='ui.return_item');
 if(header<0||index<0)throw Error('Return-item translation row missing');
 const codes=grid[header].slice(2).filter(Boolean);
 const folder=path.join(root,'output/verification/latest/localization');await fs.mkdir(folder,{recursive:true});
 if(process.argv.includes('--return-click')) {
  const texts={ko:'우클릭: 반납 +1토큰',en:'Right-click: return for 1 token',ja:'右クリック：返却で1トークン','zh-Hans':'右键：退回并获得1枚代币','zh-Hant':'右鍵：退回並獲得1枚代幣',fr:'Clic droit : rendre pour 1 jeton',de:'Rechtsklick: Rückgabe für 1 Marke',es:'Clic derecho: devuelve por 1 ficha','pt-BR':'Clique direito: devolva por 1 ficha',ru:'ПКМ: вернуть за 1 жетон'};
  for(let i=0;i<codes.length;i++)if(texts[codes[i]])sheet.getRangeByIndexes(index,i+2,1,1).values=[[texts[codes[i]]]];
  workbook.recalculate();
  await(await SpreadsheetFile.exportXlsx(workbook)).save(destination);
  const rows=sheet.getUsedRange().values.slice(header+1).filter(row=>typeof row[0]==='string'&&row[0]).map(row=>({key:row[0],languages:codes,texts:codes.map((_,i)=>String(row[i+2]??'').replaceAll('\\n','\n'))}));
  await fs.writeFile(path.join(root,'Assets/_COLA_COLA/Resources/Localization/Strings.json'),JSON.stringify({rows},null,2));
  console.log('Updated immediate-return hint in workbook and runtime for '+Object.keys(texts).length+' languages.');
 }
 const preview=await workbook.render({sheetName:sheet.name,range:`C${index+1}:F${index+1}`,scale:1,format:'png'});
 await fs.writeFile(path.join(folder,'return-hint-working.png'),new Uint8Array(await preview.arrayBuffer()));
 process.exit(0);
}
// Synchronize an already-translated workbook without reconstructing its rows or sheets.
if (process.argv.includes('--sync-workbook')) {
 const workbook=await SpreadsheetFile.importXlsx(await FileBlob.load(destination));
 const sheet=workbook.worksheets.getItemAt(0), grid=sheet.getUsedRange().values;
 const header=grid.findIndex(row=>row[0]==='Key');
 if(header<0)throw Error('Translation header missing');
 const languages=grid[header].slice(2).filter(Boolean), rows=[];
 for(const row of grid.slice(header+1)) {
  if(typeof row[0]!=='string'||!row[0])continue;
  rows.push({key:row[0],languages,texts:languages.map((_,i)=>String(row[i+2]??'').replaceAll('\\n','\n'))});
 }
 sheet.getRangeByIndexes(header+1,2,grid.length-header-1,languages.length).format.autofitRows();
 workbook.recalculate();
 const folder=path.join(root,'output/verification/latest/localization');await fs.mkdir(folder,{recursive:true});
 const preview=await workbook.render({sheetName:sheet.name,range:'E44:H47',scale:1,format:'png'});
 await fs.writeFile(path.join(folder,'working-after.png'),new Uint8Array(await preview.arrayBuffer()));
 await(await SpreadsheetFile.exportXlsx(workbook)).save(destination);
 await fs.writeFile(path.join(root,'Assets/_COLA_COLA/Resources/Localization/Strings.json'),JSON.stringify({rows},null,2));
 const missing=rows.reduce((n,row)=>n+row.texts.filter(text=>!text.trim()).length,0);
 await fs.writeFile(path.join(folder,'Workbook.txt'),`FIZZERIA: ${rows.length} keys, ${languages.length} languages, ${missing} empty translation cells. Worksheet structure preserved; translated-row heights fitted for the new text. Draft translations; native speaker review is not claimed.\n`);
 console.log(`Synchronized ${rows.length} rows; ${languages.length} languages; ${missing} empty cells.`);
 process.exit(0);
}
// Narrow updates preserve all translator edits, custom language columns and existing workbook formatting.
if (process.argv.includes('--menu-labels')) {
 const workbook=await SpreadsheetFile.importXlsx(await FileBlob.load(destination));
 const sheet=workbook.worksheets.getItemAt(0);
 const grid=sheet.getUsedRange().values;
 const header=grid.findIndex(row=>row[0]==='Key');
 if(header<0)throw new Error('Translation header missing');
 const codes=grid[header].slice(2).filter(x=>typeof x==='string'&&x.trim());
 const existing=grid.slice(header+1).findIndex(row=>row[0]==='ui.language');
 let addedRow=grid.length+1;
 if(existing<0){
  const values=['ui.language','메뉴/설정',...codes.map(code=>code==='ko'?'언어':code==='en'?'Language':'')];
  const table=sheet.tables.items.find(x=>x.name==='GameTranslations');
  if(!table)throw new Error('Translation table missing');
  table.rows.add(null,[values]);
  const cell=sheet.getRangeByIndexes(addedRow-1,0,1,values.length);
  cell.format.font={name:'Arial',size:11,color:'#29362E'};
  cell.format.rowHeight=34;cell.format.wrapText=true;cell.format.verticalAlignment='center';
 }else addedRow=header+2+existing;
 workbook.worksheets.getItem('사용법').getRange('B6').values=[['게임 설정의 언어 드롭다운에서 변경합니다. 기본 언어는 한국어이며 선택값을 저장합니다.']];
 workbook.recalculate();
 console.log((await workbook.inspect({kind:'table',range:`Translations!A${addedRow}:D${addedRow}`,include:'values',tableMaxRows:1,tableMaxCols:4,maxChars:900})).ndjson);
 await (await SpreadsheetFile.exportXlsx(workbook)).save(destination);
 const entries=sheet.getUsedRange().values.slice(header+1).filter(row=>typeof row[0]==='string'&&row[0]);
 await fs.writeFile(path.join(root,'Assets/_COLA_COLA/Resources/Localization/Strings.json'),JSON.stringify({rows:entries.map(row=>({key:row[0],languages:codes,texts:codes.map((_,i)=>String(row[i+2]??'').replaceAll('\\n','\n'))}))},null,2));
 const review=path.join(root,'output/verification/latest/localization');
 await fs.mkdir(review,{recursive:true});
 for(const [name,range,out]of[['Translations',`A${Math.max(6,addedRow-1)}:D${addedRow}`,'menu-labels-after.png'],['사용법','A1:B10','menu-guide-after.png']]){
  const preview=await workbook.render({sheetName:name,range,scale:1,format:'png'});
  await fs.writeFile(path.join(review,out),new Uint8Array(await preview.arrayBuffer()));
 }
 await fs.writeFile(path.join(review,'Workbook.txt'),`Translations: ${entries.length} keys, ${codes.length} language columns. Language menu label added; translator edits and original table preserved. No Unity validation executed for this change.\n`);
 console.log(`Menu labels updated: ${entries.length} keys; ${codes.join(', ')}`);
 process.exit(0);
}
const languages = ['ko','en','ja','zh-Hans','zh-Hant','fr','de','es','pt-BR','ru'];
const sources = path.join(root,'Assets/_COLA_COLA/Scripts');
const balance = await fs.readFile(path.join(sources,'Core/ColaItemBalance.cs'),'utf8');
const constants = Object.fromEntries([...balance.matchAll(/public const (?:int|double) (\w+) = ([\d.]+);/g)].map(m=>[m[1],Number(m[2])]));
function expand(s) {
  return s.replace(/\{ColaItemBalance\.(\w+)(?: \* (\d+))?(?::0)?\}/g,(_,key,mul)=>String(constants[key]*(Number(mul)||1)))
    .replaceAll('\\n','\n');
}
const records = new Map();
function add(key,ko,en='',context='') { records.set(key,{key,context:context||key.split('.')[0],texts:[expand(ko),en,...Array(8).fill('')]}); }
const itemEnglish = {
 IceCup:['Ice Cup','Pressure -5.'],FizzCandy:['Fizz Candy',"Opponent's next shake: pressure +4."],
 QueueTicket:['Cancellation Ticket','Gain 1 token. Remove 1 enemy item.'],Bill:['Bill','Pass your turn without shaking.'],
 SpicyOrder:['Spicy Order',"Opponent's next shake: burst chance +15pp."],SafetyCap:['Safety Cap','Next shake: burst chance -15pp. Stacks. Limit: 100%.'],
 PizzaSlice:['Pizza Slice','Restore 1 HP.'],HotSauce:['Hot Sauce','This cola: burst damage +1 for both players.'],
 DoubleStamp:['Double Stamp','Survive your next shake: gain 3 extra tokens.'],BottleOpener:['Bottle Opener','Cola pressure limit -4.'],
 Straw:['Straw','Gain 2 tokens, taking what the opponent has.'],SodaWater:['Soda Water','Set pressure to half the limit.'],
 CoinCup:['Coin Cup','Gain 3 tokens.'],SwapTongs:['Serving Tongs','Take 1 enemy item. If none, gain a random item.'],
 SaltPacket:['Salt Packet',"Opponent's next shake: pressure +2."],CoolingPack:['Cooling Pack','Next shake: pressure increase -2.'],
 TokenPunch:['Token Punch',"Opponent's next shake: token reward -3."],FreshLid:['Fresh Lid','Cola pressure limit +3.'],
 ExchangeCoupon:['Exchange Coupon','Replace with a different random item.']
};
const catalog=await fs.readFile(path.join(sources,'Core/ColaItemCatalog.cs'),'utf8');
for (const m of catalog.matchAll(/new\(ColaItemId\.(\w+), "([^"]+)", \d+, \d+, \$?"([^"]+)"\)/g)) {
 const en=itemEnglish[m[1]]||['',''];add(`item.${m[1]}.name`,m[2],en[0],'아이템 이름');add(`item.${m[1]}.effect`,m[3],en[1],'아이템 효과');
}
const abilityEnglish=[
['Regular Discount','Bell orders cost 1 less token.'],['Light Hand','Survive strength 1: gain 1 extra token.'],
['Bold Choice','Survive strength 3: gain 2 extra tokens.'],['Breakage Pay','Burst on your turn: gain 2 extra tokens.'],
['First Order Service','One bell delivery per stage contains 1 extra item when there is room.'],
['Tidy Coupon','Gain an exchange coupon on acquisition and each new stage. Waits for space.'],
['Measured Force','Survive strength 2: gain 1 extra token.'],['Steady Hand','At 2 HP or less: burst chance -5pp. Limit: 100%.'],
['Spare Change','Survive a shake started with 12+ tokens: gain 1 extra token.'],['Recycling Credit','Every 3 items used after acquisition: gain 1 token.'],
['Extra Storage','Tray capacity +2.'],['Danger Pay','Survive at 50%+ predicted burst chance: gain 1 extra token.'],
['Regular Supply','New cola: gain 1 random item. Waits for space.'],['Rough Hands',"Survive strength 3: opponent's next shake pressure +2."],
['Crisis Management','At 2 HP or less: burst chance -10pp. Limit: 100%.'],['Prepaid Contract','First bell order per cola costs 2 fewer tokens.'],
['Accident Subsidy','Burst on your turn: gain 4 extra tokens.']];
const progression=await fs.readFile(path.join(sources,'Core/ColaProgressionCatalog.cs'),'utf8');
let i=0;
for (const m of progression.split('Achievements =')[0].matchAll(/\(ColaAbilityId\.(\w+), "([^"]+)", \$?"([^"]+)"\)/g)) {
 add(`ability.${m[1]}.name`,m[2],abilityEnglish[i]?.[0]||'','능력 이름');add(`ability.${m[1]}.description`,m[3],abilityEnglish[i]?.[1]||'','능력 설명');i++;
}
const achievementNames=['First Guest','Delivery Complete','Chef Off Duty','Manager Approved','Closing Time','Accident Free','Last Sip','Daring Sip','Paper Thin','Bell Regular','Self Sufficient','Head On','Gentle Shakes','Strong Shakes','Full Course','Menu Exchange','First Order','Menu Explorer','Safe Streak','Emergency Cooling','Safety Check','Empty Wallet','Spicy Victory','Double Tension','Exchange Regular','Ability Collector'];
const achievementDescriptions=['Defeat stage 1.','Defeat stage 2.','Defeat stage 3.','Defeat stage 4.','Complete all 5 stages.','Defeat a boss without bursting.','Defeat a boss with 1 HP left.','Survive a predicted burst chance of 75% or more.','Survive with pressure exactly 1 below the limit.','Use the bell 5 times in one run.','Defeat a boss without bell orders.','Defeat a boss without using items.','Defeat a boss using only strength 1.','Defeat a boss using only strength 3.','Use all 19 item types across your records.','Use an item obtained with an exchange coupon.','Place your first bell order.','Use 5 different item types across your records.','Survive 5 consecutive shakes in one run.','Use an ice cup at 75% pressure or more.','Use a safety cap.','Empty the opponent\'s token balance with a token punch.','Defeat a boss with a hot-sauce burst.','Survive a shake affected by a spicy order.','Use 3 exchange coupons in one run.','Obtain 4 abilities in one run.'];
i=0;
for (const m of progression.split('Achievements =')[1].matchAll(/\(ColaAchievementId\.(\w+), "([^"]+)", "([^"]+)"\)/g)) {
 let ko=m[3],en=achievementDescriptions[i]||'';
 if(m[1]==='EmptyWallet'){ko='계산 펀치로 상대 흔들기 보상 0 만들기';en='Reduce an opponent\'s shake reward to zero with a token punch.';}
 add(`achievement.${m[1]}.name`,m[2],achievementNames[i]||'','업적 이름');add(`achievement.${m[1]}.description`,ko,en,'업적 조건');i++;
}
const uiEnglish={bell_offer:'Service bell: {0} tokens for {1} random items',tray_full:'Not enough space ({0}/{1})',tokens_required:'{0} tokens required',pressure:'Pressure {0} / Limit {1}',shake_preview:'Pressure +{0} · Safe {1} tokens · Burst {2} tokens / Damage {3}',run_resources:'HP {0}/{1}    ·    Tokens {2}',start:'Start Game',settings:'Settings',quit:'Quit Game',credits:'Credits',master_volume:'Master Volume',resolution:'Resolution',fullscreen:'Fullscreen',main_menu:'Main Menu',back:'Back',my_abilities:'My Abilities',boss_abilities:'Opponent Abilities',achievements:'Achievements',ability_choice:'On the House',ability_debug:'Ability Test',achievement_debug:'Achievement Test',no_abilities:'No abilities yet',no_boss_abilities:'No opponent abilities',ability_hint:'Choose an ability every 3 combined bursts.',unlock_all:'Unlock All',grant_all:'Get All',select:'Choose',earned:'Earned',add:'Add',run_complete:'Challenge Complete',boss_defeated:'Boss Defeated',run_over:'Run Over',next_boss:'Next Boss',new_run:'New Run',enter_next:'ENTER  Next Boss',enter_new:'ENTER  New Run',cola:'Cola',choose_strength:'Select a strength, then shake.',return_item:'Right-click: return for 1 token',tab_hint:'TAB Abilities / Achievements'};
for (const name of await fs.readdir(path.join(sources,'Presentation'))) {
 if(!name.endsWith('.cs'))continue;
 const source=await fs.readFile(path.join(sources,'Presentation',name),'utf8');
 for(const m of source.matchAll(/ColaLocalization\.Get\("(ui\.[^"]+)", "([^"]*)"\)/g))add(m[1],m[2],uiEnglish[m[1].slice(3)]||'','UI');
 if(name==='ColaFrontEnd.cs')for(const m of source.matchAll(/L\("([^"]+)", "([^"]*)"\)/g))add('ui.'+m[1],m[2],uiEnglish[m[1]]||'','메뉴/설정');
}
for(const [n,ko,en] of [[1,'단골 손님','Regular Guest'],[2,'배달원','Delivery Driver'],[3,'주방장','Chef'],[4,'지배인','Manager'],[5,'사장','Owner']])add(`boss.${n}`,ko,en,'단계 이름');
// Preserve translator edits on subsequent builder runs, keyed rather than row-position based.
try {
 const old=await SpreadsheetFile.importXlsx(await FileBlob.load(destination));
 let header=false;
 for(const row of old.worksheets.getItemAt(0).getUsedRange().values) {
  if(row[0]==='Key'){header=true;continue;}
  if(header&&typeof row[0]==='string'&&row[0]) {
   if(!records.has(row[0]))records.set(row[0],{key:row[0],context:row[1]||'UI',texts:languages.map((_,c)=>String(row[c+2]??''))});
   for(let c=0;c<languages.length;c++)if(!(c===0&&process.argv.includes('--refresh-source'))&&typeof row[c+2]==='string'&&row[c+2])records.get(row[0]).texts[c]=row[c+2];
  }
 }
} catch(e) { if(e.code!=='ENOENT' && !String(e.message).includes('ENOENT'))throw e; }
const workbook=Workbook.create();const sheet=workbook.worksheets.add('Translations');sheet.showGridLines=false;
const rows=[...records.values()];const last=rows.length+5;
sheet.getRange(`A1:L${last}`).format.font={name:'Arial',size:11,color:'#29362E'};
sheet.getRange('A2').values=[['FIZZERIA 번역']];sheet.getRange('A2').format.font={size:16,bold:true};
sheet.getRange('A3').values=[['언어별 칸을 수정하고 저장하면 Unity가 자동 반영합니다. 빈 번역은 한국어로 표시됩니다. Key와 언어 코드는 유지하세요.']];
sheet.getRange('A3:L3').format.font={size:10,color:'#74776B'};
sheet.getRange('A5:L5').values=[['Key','Context',...languages]];
sheet.getRange(`A6:L${last}`).values=rows.map(r=>[r.key,r.context,...r.texts]);
sheet.getRange(`A5:L${last}`).format.rowHeight=34;
sheet.getRange(`A5:L${last}`).format.verticalAlignment='center';sheet.getRange(`A6:L${last}`).format.wrapText=true;
sheet.getRange('A5:L5').format={fill:'#293D31',font:{color:'#FFFFFF',bold:true},horizontalAlignment:'center',rowHeight:26};
sheet.getRange(`A6:B${last}`).format.fill='#F0F0E9';sheet.getRange(`E6:L${last}`).format.fill='#FFF7DF';
sheet.getRange(`A5:A${last}`).format.columnWidth=48;sheet.getRange(`B5:B${last}`).format.columnWidth=17;
sheet.getRange(`C5:D${last}`).format.columnWidth=63;sheet.getRange(`E5:L${last}`).format.columnWidth=58;
sheet.getRange(`C6:L${last}`).format.autofitRows();
const table=sheet.tables.add(`A5:L${last}`,true,'GameTranslations');table.style='TableStyleLight1';sheet.freezePanes.freezeRows(5);sheet.freezePanes.freezeColumns(2);
const guide=workbook.worksheets.add('사용법');guide.showGridLines=false;
const guideRows=[['번역 작업 방법',''],['1','Translations 시트의 언어별 셀을 수정합니다. ko는 한국어 원문, en은 영어 초안입니다.'],['2','Key와 언어 코드 헤더는 변경하지 않습니다. 새 언어는 오른쪽 열에 언어 코드를 추가합니다.'],['3','원본 XLSX를 Assets/Localization에 저장하면 Unity가 번역 JSON을 자동 갱신합니다.'],['4','Unity 메뉴 COLA_COLA > Localization > Import translation workbook으로 수동 반영할 수도 있습니다.'],['5','기본 언어는 ko입니다. 언어 변경은 ColaLocalization.SetLanguage("en") 등을 호출합니다.'],['6','한국어/영어/일본어는 Unity 메뉴 COLA_COLA > Localization > Language에서 선택할 수 있습니다.'],['7','빈 번역은 한국어로 표시됩니다. 영어는 초안이며 나머지 언어는 번역 입력용 빈 칸입니다.'],['8','수치 변경 시 설명의 숫자도 갱신하고 {0} 등의 표시를 유지하세요. \\n은 줄바꿈입니다.'],['출처','현재 프로젝트의 아이템·능력·업적 카탈로그와 UI 소스. 모델 표면에 새긴 글자와 진단 로그는 제외합니다.']];
guideRows[1][1]='Translations 시트의 언어별 셀을 수정합니다. 번역 문구와 치환 표기를 함께 유지하세요.';
guideRows[6][1]='게임 설정의 언어 드롭다운에서 언어를 선택합니다.';
guideRows[7][1]='빈 번역은 한국어로 표시됩니다. 번역 초안은 배포 전 문맥 감수를 권장합니다.';
guide.getRange('A1:B10').values=guideRows;guide.getRange('A1:B10').format={font:{name:'Arial',size:11,color:'#29362E'},rowHeight:38,verticalAlignment:'center',wrapText:true};guide.getRange('A1:B1').format.font={bold:true,size:16};guide.getRange('A1:A10').format.columnWidth=23;guide.getRange('B1:B10').format.columnWidth=110;
workbook.recalculate();
console.log((await workbook.inspect({kind:'table',range:'Translations!A5:D9',include:'values',tableMaxRows:5,tableMaxCols:4,maxChars:1800})).ndjson);
await fs.mkdir(path.dirname(destination),{recursive:true});await (await SpreadsheetFile.exportXlsx(workbook)).save(destination);
const resource=path.join(root,'Assets/_COLA_COLA/Resources/Localization/Strings.json');
await fs.writeFile(resource,JSON.stringify({rows:rows.map(r=>({key:r.key,languages,texts:r.texts}))},null,2),'utf8');
const review=path.join(root,'output/verification/latest/localization');await fs.mkdir(review,{recursive:true});
for(const [name,range]of[['Translations','A1:D11'],['사용법','A1:B10']]){
 const preview=await workbook.render({sheetName:name,range,scale:1,format:'png'});
 await fs.writeFile(path.join(review,name==='Translations'?'translations-preview.png':'guide-preview.png'),new Uint8Array(await preview.arrayBuffer()));
}
await fs.writeFile(path.join(review,'Workbook.txt'),`Translations: ${rows.length} stable keys, ${languages.length} language columns. Korean + English draft. Other languages blank. XLSX exported; no Unity Play validation.\n`);
console.log(`Saved ${destination}; ${rows.length} keys.`);
