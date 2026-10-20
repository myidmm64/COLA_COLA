"""Download additional fast, dark music originals and retain creator/credit metadata. No playback."""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
from urllib.parse import quote
import hashlib
import json
from download_bgm_candidates import DEST, CATALOG, read

GROUP = '04_Faster_Industrial'
CHOICES = [
    ('USUAN1100384', '빠른 신시사이저 호러 펄스. 우선 비교 후보'),
    ('USUAN1200020', '다크 전자음·산업 댄스 계열. 우선 비교 후보'),
    ('USUAN1100210', '테크노·라이트 인더스트리얼, 거친 기타·신시사이저'),
    ('USUAN1100662', '신시사이저와 강한 타악, 중간 쉼 뒤 재고조'),
    ('USUAN1700048', '음산한 저음·오르간·타악의 추진력'),
    ('USUAN1700047', 'Nightmare Machine과 베이스·워터폰을 공유하는 어두운 후보'),
    ('USUAN1700053', '불안한 합창·금속 타악의 강한 대전 후보'),
]


def main():
    catalog = json.loads(read(CATALOG))
    folder = DEST / GROUP
    folder.mkdir(parents=True, exist_ok=True)

    def download(choice):
        isrc, fit = choice
        row = next(item for item in catalog if item['isrc'] == isrc)
        title = row['title']
        url = 'https://incompetech.com/music/royalty-free/mp3-royaltyfree/' + quote(row['filename'])
        path = folder / row['filename']
        data = path.read_bytes() if path.exists() else read(url)
        if len(data) < 10000 or not (data[:3] == b'ID3' or (data[0] == 255 and data[1] & 224 == 224)):
            raise RuntimeError('Audio transfer failed: ' + title)
        path.write_bytes(data)
        print('Downloaded: ' + title + ' / ' + row['bpm'] + ' BPM / ' + str(len(data)) + ' bytes', flush=True)
        return dict(title=title, artist='Kevin MacLeod', filename=row['filename'],
            local_path=GROUP + '/' + row['filename'], bpm=row['bpm'], duration=row['length'],
            fit=fit, instruments=row['instruments'], feel=row['feel'],
            source='https://incompetech.com/music/royalty-free/index.html?isrc=' + isrc,
            download=url, license='CC BY 4.0', license_url='https://creativecommons.org/licenses/by/4.0/',
            credit=f'"{title}" Kevin MacLeod (incompetech.com)\nLicensed under Creative Commons: By Attribution 4.0 License\nhttps://creativecommons.org/licenses/by/4.0/',
            bytes=len(data), sha256=hashlib.sha256(data).hexdigest())

    with ThreadPoolExecutor(max_workers=3) as pool:
        tracks = list(pool.map(download, CHOICES))
    (folder / 'manifest.json').write_text(json.dumps(dict(retrieved_at=datetime.now(timezone.utc).isoformat(),
        tracks=tracks), ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    lines = ['# 빠른 산업·전자·호러 BGM 추가 후보 7곡', '',
        '2026-10-20 제작자 공식 MP3 원본 다운로드. 게임에는 Bleak Terminal만 적용하며 이 폴더는 비교 후보다.',
        '분위기는 제작자 설명·악기·태그를 바탕으로 선정했다. 청취·Play 검증은 실행하지 않았다.',
        'Bleak Terminal은 공식 BPM이 없어 속도 차이를 수치로 단정하지 않는다. 추가 후보는 제작자 표기 135~165 BPM이다.', '',
        '| 곡 | 템포 | 길이 | 선정 이유 |', '|---|---:|---|---|']
    for item in tracks:
        lines.append(f"| [{item['title']}]({quote(item['filename'])}) | {item['bpm']} BPM | {item['duration']} | {item['fit']} |")
    lines += ['', '전자·산업적 질감부터 비교하려면 Chase Pulse Faster → Club Diver → Harmful or Fatal 순서로 들어볼 수 있다.',
        '뒤의 세 곡은 전자음보다 오르간·관악·합창·타악 비중이 있는 호러 대안이다.', '',
        '모두 무료 CC BY 4.0이며 실제 게임에 채택하면 크레딧 표기가 필요하다. [출처·크레딧](LICENSES.md).',
        '템포 변경·편집 없이 원본을 보관했다. 게임 Resources에 추가하지 않아 후보가 자동 재생되거나 빌드에 포함되지 않는다.']
    (folder / 'README.md').write_text('\n'.join(lines) + '\n', encoding='utf-8')
    credits = ['# 추가 후보 출처·크레딧', '', '확인일: 2026-10-20. 원본 MP3, 편집 없음. 현재 게임에 미적용.',
        '현재 제작자 곡별 CC BY 4.0 표기를 기준으로 기록했다.', '']
    for item in tracks:
        credits += ['## ' + item['title'], '', '- 원본: ' + item['source'], '- 다운로드: ' + item['download'],
            '- 라이선스: ' + item['license_url'], '', '```text', item['credit'], '```', '']
    (folder / 'LICENSES.md').write_text('\n'.join(credits) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
