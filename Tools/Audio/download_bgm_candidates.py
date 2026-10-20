"""Download six user-requested music candidates and their source/credit notes, without playback or Unity import."""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
from urllib.request import Request, urlopen
from urllib.parse import quote
from datetime import datetime, timezone, timedelta
import hashlib
import json

ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / 'References/BGM_Candidates'
CATALOG = 'https://incompetech.com/music/royalty-free/pieces.json'
HEADERS = {'User-Agent': 'Mozilla/5.0 COLA_COLA music candidate download'}


def read(url):
    with urlopen(Request(url, headers=HEADERS), timeout=60) as response:
        return response.read()


def main():
    catalog = json.loads(read(CATALOG))
    chosen = [
        ('01_Driving', 'Darkling', 'USUAN1700050', '140 BPM의 어둡고 추진력 있는 대전 후보'),
        ('01_Driving', 'Nerves', 'USUAN1100698', '135 BPM, 구간별 변화가 있는 고조형 후보'),
        ('02_Suspense', 'Anxiety', 'USUAN1100328', '전자음 기반의 어두운 서스펜스 후보'),
        ('02_Suspense', 'Unseen Horrors', 'USUAN1400043', '현악·관악 중심의 불안한 분위기 후보'),
    ]
    tracks = []
    for group, title, isrc, fit in chosen:
        item = next(row for row in catalog if row['isrc'] == isrc)
        tracks.append(dict(title=title, artist='Kevin MacLeod', group=group,
            filename=item['filename'], duration=item['length'], bpm=item['bpm'], fit=fit,
            source='https://incompetech.com/music/royalty-free/index.html?isrc='+isrc,
            download='https://incompetech.com/music/royalty-free/mp3-royaltyfree/'+quote(item['filename']),
            license='CC BY 4.0', license_url='https://creativecommons.org/licenses/by/4.0/',
            credit=f'"{title}" Kevin MacLeod (incompetech.com)\nLicensed under Creative Commons: By Attribution 4.0 License\nhttps://creativecommons.org/licenses/by/4.0/'))
    tracks.extend([
        dict(title='Bleak Terminal', artist='Ruskerdax', group='03_Industrial_CC0',
            filename='Bleak Terminal.mp3', duration=None, bpm=None, fit='호러·산업적 질감의 분위기 후보',
            source='https://opengameart.org/content/bleak-terminal',
            download='https://opengameart.org/sites/default/files/ruskerdax_-_bleak_terminal_theme.mp3',
            license='CC0 1.0', license_url='https://creativecommons.org/publicdomain/zero/1.0/',
            credit='Bleak Terminal — Ruskerdax (OpenGameArt). CC0 1.0; attribution optional.'),
        dict(title='Searching', artist='yd', group='03_Industrial_CC0',
            filename='Searching.ogg', duration=None, bpm=None, fit='어두운 산업·전자 드론, 배경 루프 후보',
            source='https://opengameart.org/content/searching',
            download='https://opengameart.org/sites/default/files/Searching.ogg',
            license='CC0 1.0', license_url='https://creativecommons.org/publicdomain/zero/1.0/',
            credit='Searching — yd (OpenGameArt). CC0 1.0; attribution optional.'),
    ])
    DEST.mkdir(parents=True, exist_ok=True)

    def download(item):
        folder = DEST / item['group']
        folder.mkdir(exist_ok=True)
        data = read(item['download'])
        # Reject an error HTML page before saving it as user media. This is transfer handling, not game testing.
        is_audio = data[:3] == b'ID3' or data[:4] == b'OggS' or (len(data)>2 and data[0]==255 and data[1]&224==224)
        if not is_audio or len(data) < 10000:
            raise RuntimeError('The server did not return an audio file for '+item['title'])
        path = folder / item['filename']
        path.write_bytes(data)
        item['local_path'] = path.relative_to(DEST).as_posix()
        item['bytes'] = len(data)
        item['sha256'] = hashlib.sha256(data).hexdigest()
        print('Downloaded: '+item['local_path']+' ('+str(len(data))+' bytes)', flush=True)
        return item

    with ThreadPoolExecutor(max_workers=3) as pool:
        tracks = list(pool.map(download, tracks))
    date = datetime.now(timezone(timedelta(hours=9))).isoformat(timespec='seconds')
    (DEST/'manifest.json').write_text(json.dumps(dict(retrieved_at=date, tracks=tracks),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    rows = ['# COLA_COLA 무료 BGM 후보', '', '2026-10-20 공식 배포 파일을 원본 형식으로 내려받았다. 게임 BGM은 Bleak Terminal 단독이며 다른 곡은 비교 후보다.',
        '분류와 추천 순서는 제작자 설명·태그를 바탕으로 한 판단이며, Play·청취 검증은 실행하지 않았다.', '',
        '| 후보 | 곡 / 작곡가 | 길이·템포(제작자 표기) | 용도 | 조건 |', '|---|---|---|---|---|']
    for item in tracks:
        timing = item['duration'] or '사이트에 별도 길이 미표기'
        if item['bpm']: timing += ' / '+item['bpm']+' BPM'
        rows.append(f"| [{item['title']}]({quote(item['local_path'])}) | {item['title']} / {item['artist']} | {timing} | {item['fit']} | {item['license']} |")
    rows += ['', '빠른 대전용은 Darkling → Nerves, 반복 타격을 줄인 서스펜스 비교는 Anxiety → Unseen Horrors 순서로 들어볼 수 있다.',
        '이는 실제 청취 후 순위가 아닌 설명 기준의 후보 순서다. Bleak Terminal과 Searching은 CC0 대안이다.', '',
        '## 사용 조건', '', 'Kevin MacLeod 4곡은 크레딧 표기가 필요한 무료 CC BY 4.0이다. 게임 크레딧에 아래 LICENSES.md의 해당 곡 표기와 라이선스 링크를 넣고 편집했다면 변경 사실도 적는다.',
        'CC0 2곡은 저작자 표시 의무 없이 상업적 사용·편집이 가능한 배포다. 출처 기록은 함께 보관한다.',
        '[제작자 무료 라이선스 안내](https://incompetech.com/music/royalty-free/licenses/) · [크레딧 및 개별 출처](LICENSES.md)', '',
        '원본 파일 외에 프리뷰·변환 WAV·중복 백업은 만들지 않았다. Bleak Terminal은 게임용 사본을 Resources에 두었다. manifest.json에는 최초 6곡의 원본 링크·용량·SHA-256을 기록했다.', '',
        '[추가 빠른 후보 7곡](04_Faster_Industrial/README.md). 추가 곡의 출처·크레딧·manifest는 해당 폴더에 있다.']
    (DEST/'README.md').write_text('\n'.join(rows)+'\n',encoding='utf-8')
    credits = ['# 출처와 라이선스', '', '확인일: 2026-10-20. 음원은 내려받은 원본이며 변경하지 않았다.',
        'Incompetech의 현재 곡별 표기는 CC BY 4.0이며, 유료 무표기 라이선스를 구매한 것이 아니다.', '']
    for item in tracks:
        credits += ['## '+item['title'], '', '- 작곡가: '+item['artist'], '- 원본 페이지: '+item['source'],
            '- 배포 파일: '+item['download'], '- 라이선스: '+item['license_url'], '', '```text', item['credit'], '```', '']
    (DEST/'LICENSES.md').write_text('\n'.join(credits)+'\n',encoding='utf-8')


if __name__ == '__main__':
    main()
