"""Offline behavior checks; no PipeWire server or third-party packages required."""
import importlib.util
from pathlib import Path
import unittest
from unittest.mock import patch

RELAY = Path(__file__).resolve().parents[1] / "relay" / "pipewire-relay.py"
spec = importlib.util.spec_from_file_location("animates_pipewire_relay", RELAY)
relay = importlib.util.module_from_spec(spec)
spec.loader.exec_module(relay)


class MonitorSelectionTests(unittest.TestCase):
    def test_uses_default_sink_monitor(self):
        def fake(*args):
            if args == ("pactl", "get-default-sink"):
                return "alsa_output.test.analog-stereo"
            if args == ("pactl", "list", "short", "sources"):
                return "11\talsa_input.test\tPipeWire\n12\talsa_output.test.analog-stereo.monitor\tPipeWire"
            raise AssertionError(args)

        with patch.object(relay, "command", side_effect=fake):
            self.assertEqual(relay.monitor_name(), "alsa_output.test.analog-stereo.monitor")

    def test_fails_when_monitor_is_missing(self):
        def fake(*args):
            if args == ("pactl", "get-default-sink"):
                return "alsa_output.test"
            if args == ("pactl", "list", "short", "sources"):
                return "4\talsa_input.test\tPipeWire"
            raise AssertionError(args)

        with patch.object(relay, "command", side_effect=fake):
            with self.assertRaisesRegex(RuntimeError, "Output monitor not available"):
                relay.monitor_name()

    def test_loopback_socket_is_localhost_only(self):
        source = RELAY.read_text()
        self.assertIn('server.bind(("127.0.0.1", PORT))', source)
        self.assertIn('"--format=float32le"', source)
        self.assertIn('"--channels=1"', source)


if __name__ == "__main__":
    unittest.main()