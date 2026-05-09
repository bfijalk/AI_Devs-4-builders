import os
import json
import uuid
from http.server import BaseHTTPRequestHandler, HTTPServer
from socketserver import ThreadingMixIn
from dotenv import load_dotenv

import log
import sessions
import ai

load_dotenv()

_server_run_id = uuid.uuid4().hex[:8]
_connection_counter = 0
_connection_sessions: dict[str, str] = {}


def _get_session_id(client_session_id: str) -> str:
    """Map a client-provided sessionID to a unique per-server-run session."""
    global _connection_counter
    if client_session_id not in _connection_sessions:
        _connection_counter += 1
        _connection_sessions[client_session_id] = f"{_server_run_id}-{_connection_counter:03d}"
    return _connection_sessions[client_session_id]


class ThreadingHTTPServer(ThreadingMixIn, HTTPServer):
    daemon_threads = True


class RequestHandler(BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        pass  # HTTP access log handled by log.request

    def send_json(self, status: int, data: dict) -> None:
        body = json.dumps(data).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_POST(self):
        if self.path != "/":
            self.send_json(404, {"error": "Not found"})
            return

        content_length = int(self.headers.get("Content-Length", 0))
        raw_body = self.rfile.read(content_length)

        try:
            body = json.loads(raw_body)
        except json.JSONDecodeError:
            self.send_json(400, {"error": "Invalid JSON"})
            return

        session_id = body.get("sessionID")
        message = body.get("msg")

        if not session_id or not message:
            self.send_json(400, {"error": "Both 'sessionID' and 'msg' fields are required"})
            return

        session_id = _get_session_id(session_id)
        log.request(session_id, message)

        try:
            sessions.restore(session_id)
            sessions.append(session_id, "user", message)
            history = sessions.get_history(session_id)
            reply = ai.complete(history)
            sessions.append(session_id, "assistant", reply)
            log.response(session_id, reply)
            self.send_json(200, {"msg": reply})
        except Exception as e:
            log.error("Request failed", e)
            self.send_json(500, {"error": str(e)})


def main():
    host = os.getenv("HOST", "0.0.0.0")
    port = int(os.getenv("PORT", "3000"))

    server = ThreadingHTTPServer((host, port), RequestHandler)
    log.server_start(host, port, ai.MODEL)
    print(f"  Session prefix: {_server_run_id}\n")

    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nZatrzymano serwer.")
        server.server_close()


if __name__ == "__main__":
    main()
