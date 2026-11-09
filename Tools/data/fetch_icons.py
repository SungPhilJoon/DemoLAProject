"""IconManifest.csv 의 아이콘(game-icons.net, CC BY 3.0)을 PNG 로 받는다.

  python Tools/data/fetch_icons.py            # 없는 것만 받기
  python Tools/data/fetch_icons.py --force    # 전부 다시 받기

결과: Assets/_Project/Art/Icons/Glyphs/<author>/<name>.png (흰 글리프 + 투명 배경, 512px)
      Assets/_Project/Art/Icons/CREDITS.md (저작자 표기 — CC BY 3.0 조건)
Unity 에서 DemoLA/Data/아이콘 라이브러리 갱신 을 실행하면 IconLibrary.asset 에 반영된다.
"""
import csv
import os
import sys
import time
import urllib.request

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
ICONS = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'Icons')
MANIFEST = os.path.join(ICONS, 'IconManifest.csv')
GLYPHS = os.path.join(ICONS, 'Glyphs')
URL = 'https://game-icons.net/icons/ffffff/transparent/1x1/{icon}.png'


def main():
    force = '--force' in sys.argv
    with open(MANIFEST, encoding='utf-8') as f:
        icons = sorted({row['Icon'].strip() for row in csv.DictReader(f) if row['Icon'].strip()})

    fetched = 0
    failed = []
    for icon in icons:
        path = os.path.join(GLYPHS, *icon.split('/')) + '.png'
        if os.path.exists(path) and not force:
            continue
        os.makedirs(os.path.dirname(path), exist_ok=True)
        request = urllib.request.Request(URL.format(icon=icon), headers={'User-Agent': 'DemoLA-icon-fetch'})
        try:
            with urllib.request.urlopen(request, timeout=30) as response:
                data = response.read()
            if data[:8] != b'\x89PNG\r\n\x1a\n':
                raise ValueError('PNG 아님')
            with open(path, 'wb') as out:
                out.write(data)
            fetched += 1
            time.sleep(0.1)
        except Exception as e:  # noqa: BLE001
            failed.append(f'{icon}: {e}')

    authors = sorted({icon.split('/')[0] for icon in icons})
    with open(os.path.join(ICONS, 'CREDITS.md'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('# 아이콘 출처\n\n')
        f.write('Glyphs/ 의 아이콘은 [game-icons.net](https://game-icons.net) 에서 받았으며 '
                '[CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) 라이선스다. '
                '게임 내 색 · 테두리 · 배경은 IconFactory 가 입힌다 (변경 사항).\n\n')
        f.write('저작자: ' + ', '.join(authors) + '\n\n')
        f.write('| 아이콘 | 저작자 | 원본 |\n|---|---|---|\n')
        for icon in icons:
            author, name = icon.split('/')
            f.write(f'| {name} | {author} | https://game-icons.net/1x1/{author}/{name}.html |\n')

    print(f'아이콘 {len(icons)}종 · 새로 받음 {fetched} · 실패 {len(failed)}')
    for line in failed:
        print('  실패', line)
    return 1 if failed else 0


if __name__ == '__main__':
    sys.exit(main())
