#!/usr/bin/env python3
"""Sends 48-kHz mono float32 samples from the default PipeWire monitor to Animates.

Only listens on localhost. Never captures the microphone. Requires parec/pactl.
Run this as your regular Linux user; stop with Ctrl+C.
"""
import shutil
import signal
import socket
import subprocess
import sys

PORT = 47883


def command(*args):
    return subprocess.check_output(args, text=True, stderr=subprocess.STDOUT).strip()


def monitor_name():
    sink = command("pactl", "get-default-sink")
    name = sink + ".monitor"
    sources = command("pactl", "list", "short", "sources")
    if name not in sources:
        raise RuntimeError("Output monitor not available: " + name)
    return name


def main():
    for exe in ("pactl", "parec"):
        if shutil.which(exe) is None:
            raise RuntimeError("Missing '" + exe + "'. On Ubuntu install: sudo apt install pulseaudio-utils")
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server:
        server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        server.bind(("127.0.0.1", PORT))
        server.listen(1)
        print("[relay] Waiting for Animates on 127.0.0.1:{} (Ctrl+C to stop)".format(PORT), flush=True)
        while True:
            conn, addr = server.accept()
            with conn:
                device = monitor_name()
                print("[relay] Client connected; recording output monitor: " + device, flush=True)
                args = ["parec", "--raw", "--device=" + device,
                        "--format=float32le", "--rate=48000", "--channels=1"]
                proc = subprocess.Popen(args, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                        bufsize=0)
                try:
                    while True:
                        chunk = proc.stdout.read(8192)
                        if not chunk:
                            msg = proc.stderr.read(800).decode("utf-8", "replace")
                            print("[relay] parec ended: " + msg, flush=True)
                            break
                        conn.sendall(chunk)
                except (BrokenPipeError, ConnectionResetError, OSError):
                    print("[relay] Client disconnected.", flush=True)
                finally:
                    proc.terminate()
                    try:
                        proc.wait(timeout=2)
                    except subprocess.TimeoutExpired:
                        proc.kill()
                        proc.wait()


def stop_gracefully(signum, frame):
    # Allows the launcher to stop the relay cleanly, including a live parec child.
    raise KeyboardInterrupt


if __name__ == "__main__":
    signal.signal(signal.SIGTERM, stop_gracefully)
    try:
        main()
    except KeyboardInterrupt:
        print("\n[relay] Stopped.")
    except Exception as error:
        sys.exit("[relay] ERROR: " + str(error))