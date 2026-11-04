#!/usr/bin/env python3
"""Проверка и атомарный импорт собственного чистого дампа в новую Neon. Без зависимостей Python."""
import argparse
from contextlib import contextmanager
from dataclasses import dataclass
import hashlib
import json
import os
from pathlib import Path
import re
import ssl
import stat
import subprocess
import tempfile
from urllib.parse import parse_qs, unquote, urlsplit

ROOT = Path(__file__).resolve().parents[2]
PG_IMAGE = "pgvector/pgvector:pg16"
TABLES = sorted(["Users", "Rooms", "RoomMembers", "Games", "GamePlayers", "GameAttempts",
                 "Words", "ActiveGameParticipations", "__EFMigrationsHistory"])
PRIVATE_TABLES = [t for t in TABLES if t not in {"Words", "__EFMigrationsHistory"}]


class ReleaseError(Exception):
    pass


def literal(value):
    return "E'" + str(value).replace("\\", "\\\\").replace("'", "''") + "'"


def run(command, **kwargs):
    result = subprocess.run(command, capture_output=True, **kwargs)
    if result.returncode:
        # libpq/restore errors can contain a connection or COPY row: never echo them.
        raise ReleaseError("Команда завершилась ошибкой; её потенциально секретный вывод скрыт.")
    return result.stdout


@dataclass(frozen=True)
class Target:
    host: str
    database: str
    username: str
    password: str
    port: int = 5432


def load_target(path):
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
    with os.fdopen(fd) as stream:
        info = os.fstat(stream.fileno())
        if not stat.S_ISREG(info.st_mode) or stat.S_IMODE(info.st_mode) != 0o600 or info.st_uid != os.getuid():
            raise ReleaseError("Файл настроек должен принадлежать текущему пользователю и иметь права 600.")
        values = {}
        for line in stream:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            key, sep, value = line.partition("=")
            if not sep or key in values:
                raise ReleaseError("Неверный формат файла настроек или повтор ключа.")
            if len(value) >= 2 and value[0] == value[-1] and value[0] in "\"'":
                value = value[1:-1]
            values[key] = value
    expected = {"NEON_DATABASE_URL", "NEON_EXPECTED_HOST", "NEON_EXPECTED_DATABASE"}
    if set(values) != expected or any(not v or "<" in v for v in values.values()):
        raise ReleaseError("Заполните три поля шаблона Neon; дополнительные ключи не допускаются.")
    url = urlsplit(values["NEON_DATABASE_URL"])
    host = url.hostname or ""
    if (url.scheme not in {"postgres", "postgresql"} or url.fragment
            or not re.fullmatch(r"ep-[a-z0-9-]+\.[a-z0-9.-]+\.neon\.tech", host)
            or "-pooler" in host.split(".")[0] or url.port not in {None, 5432}):
        raise ReleaseError("Нужен прямой адрес Neon на порту 5432, без pooler.")
    query = parse_qs(url.query, keep_blank_values=True)
    if (set(query) - {"sslmode", "channel_binding"}
            or any(len(v) != 1 for v in query.values())
            or query.get("sslmode", ["require"])[0] not in {"require", "verify-ca", "verify-full"}
            or query.get("channel_binding", ["require"])[0] not in {"require", "prefer"}):
        raise ReleaseError("В URI обнаружены неподдерживаемые или небезопасные параметры.")
    target = Target(host, unquote(url.path.removeprefix("/")), unquote(url.username or ""), unquote(url.password or ""))
    if (target.host != values["NEON_EXPECTED_HOST"] or target.database != values["NEON_EXPECTED_DATABASE"]
            or not all([target.database, target.username, target.password])
            or any(c in target.database + target.username + target.password for c in "\r\n\0")):
        raise ReleaseError("URI не совпадает с отдельно указанной целью либо содержит недопустимые поля.")
    return target


@contextmanager
def connection_env(target, ca_file=None):
    ca = Path(ca_file or ssl.get_default_verify_paths().cafile or "/etc/ssl/certs/ca-certificates.crt")
    if not ca.is_file():
        raise ReleaseError("Не найдено доверенное хранилище CA; проверка TLS не отключается.")
    with tempfile.TemporaryDirectory(prefix="guessword-pgpass-") as directory:
        path = Path(directory) / "pgpass"
        fields = [target.host, str(target.port), target.database, target.username, target.password]
        path.write_text(":".join(v.replace("\\", "\\\\").replace(":", "\\:") for v in fields) + "\n")
        path.chmod(0o600)
        env = {k: v for k, v in os.environ.items() if not k.startswith("PG")}
        env.update(PGHOST=target.host, PGPORT=str(target.port), PGDATABASE=target.database,
                   PGUSER=target.username, PGPASSFILE=str(path), PGSSLMODE="verify-full",
                   PGSSLROOTCERT=str(ca), PGCHANNELBINDING="require", PGCONNECT_TIMEOUT="15",
                   PGAPPNAME="guessword-clean-dictionary")
        yield env


def psql(sql, env):
    return run(["psql", "--no-psqlrc", "--no-password", "--quiet", "--no-align", "--tuples-only",
                "--set", "ON_ERROR_STOP=1"], input=("SET search_path=pg_catalog;\n" + sql).encode(), env=env).decode().strip()


def empty_guard(target):
    # Check objects, not just row counts: even an empty foreign table must be preserved.
    return f"""
DO $guard$ BEGIN
 IF current_database() <> {literal(target.database)} OR current_user <> {literal(target.username)} THEN
  RAISE EXCEPTION 'Unexpected database or role'; END IF;
 IF current_setting('server_version_num')::int / 10000 <> 16 THEN
  RAISE EXCEPTION 'PostgreSQL 16 required'; END IF;
 IF NOT EXISTS (SELECT 1 FROM pg_available_extensions WHERE name='vector') THEN
  RAISE EXCEPTION 'pgvector unavailable'; END IF;
 IF EXISTS (SELECT 1 FROM pg_namespace WHERE nspname NOT IN ('public','information_schema')
            AND nspname !~ '^pg_') THEN RAISE EXCEPTION 'Foreign schema'; END IF;
 IF EXISTS (SELECT 1 FROM pg_extension WHERE extname NOT IN ('plpgsql','vector')) THEN
  RAISE EXCEPTION 'Foreign extension'; END IF;
 IF EXISTS (SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
            WHERE n.nspname='public' AND c.relkind IN ('r','p','v','m','S','f')) THEN
  RAISE EXCEPTION 'Database contains objects'; END IF;
 IF EXISTS (SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
            WHERE n.nspname='public' AND NOT EXISTS (SELECT 1 FROM pg_depend d
             WHERE d.classid='pg_proc'::regclass AND d.objid=p.oid AND d.deptype='e')) THEN
  RAISE EXCEPTION 'Foreign function'; END IF;
 IF EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid=t.typnamespace
            WHERE n.nspname='public' AND t.typelem=0 AND NOT EXISTS (SELECT 1 FROM pg_depend d
             WHERE d.classid='pg_type'::regclass AND d.objid=t.oid AND d.deptype='e')) THEN
  RAISE EXCEPTION 'Foreign type'; END IF;
END $guard$;
"""


def snapshot_sql():
    counts = ",".join(f"{literal(t)},(SELECT count(*) FROM public.\"{t}\")" for t in PRIVATE_TABLES)
    return f"""SELECT json_build_object(
 'tables',(SELECT json_agg(tablename ORDER BY tablename) FROM pg_tables WHERE schemaname='public'),
 'migrations',(SELECT json_agg("MigrationId" ORDER BY "MigrationId") FROM public."__EFMigrationsHistory"),
 'words',(SELECT count(*) FROM public."Words"),
 'secrets',(SELECT count(*) FROM public."Words" WHERE "CanBeSecret"),
 'invalid_vectors',(SELECT count(*) FROM public."Words" WHERE "Embedding" IS NULL
    OR public.vector_dims("Embedding")<>800 OR public.vector_norm("Embedding")<=0),
 'fingerprint',(SELECT md5(string_agg(md5("Id"::text||'|'||"Text"||'|'||"CanBeSecret"::text||'|'||"Embedding"::text),'' ORDER BY "Id")) FROM public."Words"),
 'private_rows',json_build_object({counts}),
 'database_bytes',pg_database_size(current_database()));"""


def validate_snapshot(snapshot):
    migrations = sorted(re.findall(r'\[Migration\("([^"\n]+)"\)\]', "\n".join(
        p.read_text() for p in (ROOT / "src/GuessWord.Api/Infrastructure/Persistence/Migrations").glob("*.Designer.cs"))))
    resources = ROOT / "src/GuessWord.Api/Resources"
    size = lambda name: len({s.strip().lower().replace("ё", "е") for s in (resources / name).read_text().splitlines() if s.strip()})
    if (snapshot["tables"] != TABLES or snapshot["migrations"] != migrations
            or snapshot["words"] != size("all-words.txt") or snapshot["secrets"] != size("secret-words.txt")
            or snapshot["invalid_vectors"] != 0 or set(snapshot["private_rows"]) != set(PRIVATE_TABLES)
            or any(snapshot["private_rows"].values())
            or not re.fullmatch(r"[0-9a-f]{32}", snapshot["fingerprint"])):
        raise ReleaseError("Снимок не является чистым словарём текущей версии.")


def load_manifest(path, archive):
    manifest = json.loads(path.read_text())
    with archive.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    if (manifest["format"] != "guessword-clean-dictionary-v1" or manifest["postgres_major"] != 16
            or manifest["restored_ready"] is not True
            or digest != manifest["dump_sha256"]):
        raise ReleaseError("Формат или SHA256 дампа не совпадает с проверенным манифестом.")
    validate_snapshot(manifest["snapshot"])
    return manifest


def post_guard(snapshot):
    # Compare the restored contents before COMMIT, so mismatches roll everything back.
    expected = {k: v for k, v in snapshot.items() if k != "database_bytes"}
    query = snapshot_sql().removeprefix("SELECT ").removesuffix(";")
    return f"""DO $verify$ BEGIN
 IF (({query})::jsonb - 'database_bytes') IS DISTINCT FROM {literal(json.dumps(expected))}::jsonb THEN
  RAISE EXCEPTION 'Restored contents do not match verified dictionary'; END IF;
END $verify$;"""


def restore(target, archive, manifest, ca_file=None):
    # Verify TLS with libpq. Backend pg_stat_ssl cannot prove that the client validated
    # a proxy's certificate, so it must not replace client-side verification.
    with connection_env(target, ca_file) as env:
        # Preflight is read-only; the identical guard runs again inside the restore transaction.
        psql("BEGIN READ ONLY;" + empty_guard(target) + "COMMIT;", env)
        sql = run(["docker", "run", "--rm", "--pull", "never", "--network", "none", "--mount",
                   f"type=bind,src={archive.resolve()},dst=/input/dictionary.dump,readonly", PG_IMAGE,
                   "pg_restore", "--no-owner", "--no-privileges", "--file=-", "/input/dictionary.dump"])
        transaction = ("BEGIN; SET LOCAL statement_timeout='10min'; SET LOCAL lock_timeout='15s';\n"
                       "SELECT pg_advisory_xact_lock(708240916);\n" + empty_guard(target)).encode()
        transaction += sql + ("\n" + post_guard(manifest["snapshot"]) + "\nCOMMIT;\n" + snapshot_sql()).encode()
        output = psql(transaction.decode(), env)
        return json.loads(output.splitlines()[-1])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--env-file", type=Path, default=ROOT / ".env.deploy.local")
    parser.add_argument("--dump", type=Path)
    parser.add_argument("--manifest", type=Path)
    parser.add_argument("--apply", action="store_true", help="Восстановить; без флага только read-only проверка цели")
    parser.add_argument("--confirm-target", help="Независимое подтверждение host/database, без пароля")
    args = parser.parse_args()
    target = load_target(args.env_file)
    if not args.apply:
        with connection_env(target) as env:
            psql("BEGIN READ ONLY;" + empty_guard(target) + "COMMIT;", env)
        print("Цель проверена: прямой Neon, PostgreSQL 16, verify-full TLS, база пуста. Данные не изменены.")
        return
    if args.confirm_target != f"{target.host}/{target.database}" or not args.dump or not args.manifest:
        raise ReleaseError("Для записи укажите дамп, манифест и точное подтверждение host/database.")
    manifest = load_manifest(args.manifest, args.dump)
    snapshot = restore(target, args.dump, manifest)
    print(f"Чистый словарь восстановлен: {snapshot['words']} слов; размер БД {snapshot['database_bytes']} байт.")


if __name__ == "__main__":
    try:
        main()
    except ReleaseError as error:
        raise SystemExit(str(error)) from None
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError):
        raise SystemExit("Ошибка входных данных, TLS или инструментов; потенциально секретные подробности скрыты.") from None
