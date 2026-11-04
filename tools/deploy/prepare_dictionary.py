#!/usr/bin/env python3
"""Создать чистый словарь в отдельных Docker-БД и проверить восстановление. Рабочая БД не используется."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import secrets
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import uuid

from import_neon_dictionary import PG_IMAGE, ROOT, ReleaseError, snapshot_sql, validate_snapshot


def command(args, **kwargs):
    result = subprocess.run(args, capture_output=True, **kwargs)
    if result.returncode:
        raise ReleaseError("Ошибка локальной команды; потенциально секретный вывод скрыт.")
    return result.stdout


def docker(*args):
    return command(["docker", *args]).decode().strip()


def prepare(vector, output, image):
    output = output.resolve()
    if not output.is_absolute() or any((p / ".git").exists() for p in [output, *output.parents]):
        raise ReleaseError("Дамп нужно хранить вне Git-репозиториев.")
    if output.exists():
        raise ReleaseError("Выберите новый каталог: существующие копии не перезаписываются.")
    if not vector.is_file():
        raise ReleaseError("Исходный .vec не найден.")
    output.mkdir(parents=True, mode=0o700)
    suffix = uuid.uuid4().hex[:12]
    net = "gwdict-" + suffix
    db, app, volume = net + "-db", net + "-app", net + "-data"
    containers = []
    has_network = has_volume = False
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    with tempfile.TemporaryDirectory(prefix="guessword-dictionary-") as temporary:
        work = Path(temporary)
        password = secrets.token_hex(24)
        (work / "db.env").write_text(f"POSTGRES_DB=clean_source\nPOSTGRES_USER=releaseprep\nPOSTGRES_PASSWORD={password}\n")
        (work / "db.env").chmod(0o600)

        def sql(database, text):
            value = command(["docker", "exec", "-i", db, "psql", "-XAt", "-v", "ON_ERROR_STOP=1",
                             "-U", "releaseprep", "-d", database], input=text.encode())
            return value.decode().strip()

        def start_api(database, with_vector=False):
            env = work / "app.env"
            env.write_text(f"ConnectionStrings__DefaultConnection=Host={db};Port=5432;Database={database};Username=releaseprep;Password={password}\n"
                           f"Jwt__Key={secrets.token_hex(32)}\nLogging__LogLevel__Default=Warning\n")
            env.chmod(0o600)
            args = ["run", "-d", "--pull", "never", "--name", app, "--network", net, "--memory", "512m",
                    "--env-file", str(env), "-p", "127.0.0.1::8080"]
            if with_vector:
                args += ["-e", "Dictionary__SociationVecPath=/dictionary/input.vec", "--mount",
                         f"type=bind,src={vector.resolve()},dst=/dictionary/input.vec,readonly"]
            docker(*args, image)
            containers.append(app)
            port = int(docker("port", app, "8080").split(":")[-1])
            for _ in range(240):
                try:
                    if opener.open(f"http://127.0.0.1:{port}/health/ready", timeout=3).status == 200:
                        return
                except (OSError, urllib.error.URLError):
                    pass
                if docker("inspect", "--format", "{{.State.Running}}", app) != "true":
                    raise ReleaseError("API не запустился на отдельной БД; рабочая БД не использовалась.")
                time.sleep(1)
            raise ReleaseError("Отдельная БД не достигла готовности.")

        def stop_api():
            docker("rm", "-f", app)
            containers.remove(app)

        try:
            image_id = docker("image", "inspect", "--format", "{{.Id}}", image)
            docker("network", "create", net)
            has_network = True
            docker("volume", "create", volume)
            has_volume = True
            docker("run", "-d", "--pull", "never", "--name", db, "--network", net,
                   "--env-file", str(work / "db.env"), "-v", volume + ":/var/lib/postgresql/data", PG_IMAGE)
            containers.append(db)
            for _ in range(60):
                result = subprocess.run(["docker", "exec", db, "pg_isready", "-U", "releaseprep", "-d", "clean_source"], capture_output=True)
                if result.returncode == 0:
                    break
                time.sleep(1)
            else:
                raise ReleaseError("Отдельный PostgreSQL не запустился.")
            sql("clean_source", "CREATE EXTENSION vector;")
            print("Импорт словаря в новую отдельную PostgreSQL 16...", flush=True)
            start_api("clean_source", with_vector=True)
            stop_api()  # No writes while taking the clean snapshot and dump.
            source = json.loads(sql("clean_source", snapshot_sql()))
            validate_snapshot(source)
            archive = output / "dictionary.dump"
            with archive.open("xb") as stream:
                archive.chmod(0o600)
                result = subprocess.run(["docker", "exec", db, "pg_dump", "-U", "releaseprep", "-d", "clean_source",
                                         "--format=custom", "--no-owner", "--no-privileges"], stdout=stream, stderr=subprocess.PIPE)
            if result.returncode:
                raise ReleaseError("Создание дампа завершилось ошибкой; копия не считается готовой.")
            docker("exec", db, "createdb", "-U", "releaseprep", "clean_restored")
            docker("cp", str(archive), db + ":/tmp/dictionary.dump")
            docker("exec", db, "pg_restore", "-U", "releaseprep", "-d", "clean_restored", "--single-transaction",
                   "--exit-on-error", "--no-owner", "--no-privileges", "/tmp/dictionary.dump")
            restored = json.loads(sql("clean_restored", snapshot_sql()))
            validate_snapshot(restored)
            compare = lambda value: {k: v for k, v in value.items() if k != "database_bytes"}
            if compare(source) != compare(restored):
                raise ReleaseError("Восстановленный словарь отличается от исходного; копия не принята.")
            start_api("clean_restored")  # Readiness with no .vec available in the image.
            stop_api()
            with archive.open("rb") as stream:
                digest = hashlib.file_digest(stream, "sha256").hexdigest()
            manifest = dict(format="guessword-clean-dictionary-v1", created_utc=datetime.now(timezone.utc).isoformat(),
                            postgres_major=16, image=image_id, dump_sha256=digest, snapshot=source,
                            restored_database_bytes=restored["database_bytes"], restored_ready=True)
            (output / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
            (output / "manifest.json").chmod(0o600)
            print(f"Готово: {source['words']} слов, {source['secrets']} секретов; пользовательских данных нет.")
            print(f"БД: {source['database_bytes']} байт; дамп: {archive.stat().st_size} байт; восстановление и ready проверены.")
            print(f"Артефакты: {output}")
            return manifest
        finally:
            for name in reversed(containers):
                subprocess.run(["docker", "rm", "-f", name], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            if has_volume:
                subprocess.run(["docker", "volume", "rm", volume], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            if has_network:
                subprocess.run(["docker", "network", "rm", net], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--vector-file", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--image", default="guessword-cloud:local", help="Предварительно собранный текущий Dockerfile.cloud")
    args = parser.parse_args()
    try:
        prepare(args.vector_file, args.output_dir, args.image)
    except ReleaseError as error:
        raise SystemExit(str(error)) from None
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError):
        raise SystemExit("Ошибка локальной подготовки; потенциально секретные подробности скрыты.") from None
