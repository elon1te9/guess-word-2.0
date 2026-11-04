#!/usr/bin/env python3
"""Проверка настоящего nginx на loopback; временный TLS, без Docker и рабочих данных."""
import argparse
import http.client
import http.server
import json
from pathlib import Path
import socket
import ssl
import subprocess
import tempfile
import threading
import time

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--nginx', required=True, help='nginx с SSL и rewrite modules')
args = parser.parse_args()
repo = Path(__file__).resolve().parents[2]


def free_port():
    with socket.socket() as s:
        s.bind(('127.0.0.1', 0))
        return s.getsockname()[1]


class Api(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        self.send_response(200)
        self.end_headers()
        self.wfile.write(b'ok')

    do_POST = do_GET

    def log_message(self, *_):
        pass


api = http.server.ThreadingHTTPServer(('127.0.0.1', 0), Api)
threading.Thread(target=api.serve_forever, daemon=True).start()
context = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
context.check_hostname = False
context.verify_mode = ssl.CERT_NONE  # Только временный self-signed сертификат loopback-стенда.
marker = 'AUDIT_SYNTHETIC_NOT_A_TOKEN'
checks = []
try:
    with tempfile.TemporaryDirectory(prefix='guessword-proxy-') as directory:
        work = Path(directory)
        (work / 'logs').mkdir()
        (work / 'index.html').write_text('client')
        subprocess.run(['openssl', 'req', '-x509', '-newkey', 'rsa:2048', '-nodes',
                        '-keyout', str(work / 'privkey.pem'), '-out', str(work / 'fullchain.pem'),
                        '-days', '1', '-subj', '/CN=localhost'], check=True,
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        for name in ['nginx.conf', 'nginx.server.conf']:
            http_port, tls_port = free_port(), free_port()
            source = (repo / 'deploy/nginx' / name).read_text()
            for include in ['security.conf', 'locations.conf']:
                source = source.replace('include /etc/nginx/guessword/' + include + ';',
                                        (repo / 'deploy/nginx' / include).read_text())
            source = source.replace('listen 80;', f'listen 127.0.0.1:{http_port};')
            source = source.replace('listen 443 ssl;', f'listen 127.0.0.1:{tls_port} ssl;')
            source = source.replace('/etc/nginx/tls/', str(work) + '/')
            source = source.replace('/usr/share/nginx/html', str(work))
            source = source.replace('/var/log/nginx/access.log', str(work / 'access.log'))
            source = source.replace('http://api:8080', f'http://127.0.0.1:{api.server_port}')
            config = work / 'nginx.conf'
            config.write_text(f'pid {work}/nginx.pid; error_log {work}/error.log; events {{}} http {{ access_log {work}/inherited.log combined;\n' + source + '\n}')
            command = [args.nginx, '-p', str(work), '-c', str(config)]
            subprocess.run(command + ['-t'], check=True, capture_output=True)
            subprocess.run(command, check=True, capture_output=True)

            def request(path, *, plain=False, method='GET'):
                tls = name == 'nginx.server.conf' and not plain
                conn = (http.client.HTTPSConnection('127.0.0.1', tls_port, context=context, timeout=5)
                        if tls else http.client.HTTPConnection('127.0.0.1', http_port, timeout=5))
                try:
                    conn.request(method, path)
                    response = conn.getresponse()
                    result = response.status, response.getheader('Location')
                    response.read()
                    return result
                finally:
                    conn.close()

            try:
                if name == 'nginx.server.conf':
                    status, location = request('/game?access_token=' + marker, plain=True)
                    assert status == 308 and location == 'https://127.0.0.1/game', (status, location)
                    checks.append('HTTP 308 without query; TLS handshake')
                assert request('/')[0] == 200
                assert request('/hubs/game?access_token=' + marker)[0] == 200
                statuses = [request(path, method='POST')[0] for path in
                            ['/api/User/login', '/API/USER/REGISTER/', '/api/user/login'] * 5]
                assert 200 in statuses and 429 in statuses, statuses
                time.sleep(6.2)
                assert request('/api/user/login', method='POST')[0] == 200
                statuses = [request('/HeAlTh/ReAdY/')[0] for _ in range(10)]
                assert 200 in statuses and 429 in statuses, statuses
                assert request('/health/live')[0] == 200
                # Force an upstream failure specifically on the token-bearing hub route.
                with socket.socket() as unused:
                    unused.bind(('127.0.0.1', 0))
                    dead_port = unused.getsockname()[1]
                checks.append(name + ': auth 429, recovery, route casing, ready 429, live 200')
            finally:
                subprocess.run(command + ['-s', 'quit'], check=True, capture_output=True)
                for _ in range(100):
                    if not (work / 'nginx.pid').exists():
                        break
                    time.sleep(.02)
            # Test failed upstream without changing production logging directives.
            config.write_text(config.read_text().replace(f'http://127.0.0.1:{api.server_port}', f'http://127.0.0.1:{dead_port}'))
            subprocess.run(command, check=True, capture_output=True)
            try:
                assert request('/hubs/game?access_token=' + marker)[0] == 502
            finally:
                subprocess.run(command + ['-s', 'quit'], check=True, capture_output=True)
                for _ in range(100):
                    if not (work / 'nginx.pid').exists():
                        break
                    time.sleep(.02)
            for log in ['access.log', 'error.log', 'inherited.log']:
                assert marker not in ((work / log).read_text() if (work / log).exists() else ''), 'Token leaked to ' + log
            checks.append(name + ': no synthetic token in access/error logs, including upstream 502')
finally:
    api.shutdown()
    api.server_close()
print(json.dumps({'passed': checks}, ensure_ascii=False, indent=2))
