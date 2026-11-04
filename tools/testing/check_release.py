#!/usr/bin/env python3
"""Проверка локального пакета без вывода секретных значений. Только стандартная библиотека."""
import argparse
import hashlib
import json
from pathlib import Path

repo = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--root', type=Path, default=repo / 'artifacts/release')
args = parser.parse_args()
root = args.root.resolve()
assert (root / 'api/GuessWord.Api.dll').is_file(), 'Сначала опубликуйте API'
assert (root / 'client/wwwroot/index.html').is_file(), 'Сначала опубликуйте Client'
public = json.loads((root / 'client/wwwroot/appsettings.json').read_text())
assert not public.get('Api', {}).get('BaseUrl'), 'Публикация должна использовать origin сайта'
local = repo / 'src/GuessWord.Api/appsettings.Development.json'
secrets = []
if local.exists():
    config = json.loads(local.read_text())
    for value in [config.get('Jwt', {}).get('Key'), config.get('ConnectionStrings', {}).get('DefaultConnection')]:
        if value and '<' not in value and len(value) >= 16:
            secrets.append(value.encode())
errors = []
manifest = []
for file in sorted(root.rglob('*')):
    if not file.is_file() or file == root / 'SHA256SUMS':
        continue
    relative = file.relative_to(root)
    if (file.name in {'appsettings.Development.json', 'secrets.json', '.env'}
            or file.suffix in {'.vec', '.dump', '.sql', '.tsv'}
            or 'Test' in file.name or 'testhost' in file.name):
        errors.append(f'Запрещённый файл: {relative}')
    data = file.read_bytes()
    if any(value in data for value in secrets):
        errors.append(f'Секретное значение в {relative}')
    if b'e2e_' in data:
        errors.append(f'Тестовые данные в {relative}')
    if 'client' in relative.parts and b'localhost:5280' in data:
        errors.append(f'Локальный адрес API в {relative}')
    manifest.append(f'{hashlib.sha256(data).hexdigest()}  {relative.as_posix()}')
if errors:
    raise SystemExit('\n'.join(errors))
(root / 'SHA256SUMS').write_text('\n'.join(manifest) + '\n')
print(f'Пакет проверен: {len(manifest)} файлов; секреты/локальные настройки/векторы/дампы/тестовые данные не обнаружены; SHA256SUMS записан.')
