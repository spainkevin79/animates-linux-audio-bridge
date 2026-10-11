"""Isolated installer smoke test. Uses fake toolchain and dummy game files."""
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


class InstallSmokeTests(unittest.TestCase):
    def test_install_wrapper_and_uninstall_with_custom_paths(self):
        with tempfile.TemporaryDirectory() as tmp:
            base = Path(tmp)
            home = base / "home with spaces"
            home.mkdir()
            game = base / "game current"
            mock = base / "mock-tools"
            mock.mkdir()
            files = (
                "Animates.exe", "winhttp.dll",
                "BepInEx/core/BepInEx.dll", "BepInEx/core/0Harmony.dll",
                "Animates_Data/Managed/Assembly-CSharp.dll",
                "Animates_Data/Managed/UnityEngine.dll",
                "Animates_Data/Managed/UnityEngine.CoreModule.dll",
                "Animates_Data/Managed/netstandard.dll",
                "BepInEx/plugins/AnimatesLinuxAudio.dll",
            )
            for rel in files:
                file = game / rel
                file.parent.mkdir(parents=True, exist_ok=True)
                file.write_text("previous plugin" if rel.endswith("AnimatesLinuxAudio.dll") else "fixture")
            launch = base / "existing launcher.sh"
            marker = base / "observed environment.txt"
            launch.write_text('#!/usr/bin/env bash\nprintf "%s\\n" "$WINEDLLOVERRIDES" > "$TEST_ENV_MARKER"\n')
            launch.chmod(0o755)
            commands = {
                "pgrep": "#!/usr/bin/env bash\nexit 1\n",
                "monodis": "#!/usr/bin/env bash\nprintf 'Version: 2.1.0.0\\n'\n",
                "pactl": "#!/usr/bin/env bash\nexit 0\n",
                "parec": "#!/usr/bin/env bash\nexit 0\n",
                "mcs": "#!/usr/bin/env python3\nimport pathlib,sys\nout=next(arg[5:] for arg in sys.argv if arg.startswith('-out:'))\npathlib.Path(out).write_text('compiled plugin stub')\n",
            }
            for command, script in commands.items():
                f = mock / command
                f.write_text(script)
                f.chmod(0o755)
            env = {**os.environ,
                   "HOME": str(home),
                   "PATH": f"{mock}:{os.environ['PATH']}",
                   "XDG_DATA_HOME": str(home / ".local/share"),
                   "XDG_CONFIG_HOME": str(home / ".config"),
                   "XDG_STATE_HOME": str(home / ".local/state"),
                   "TEST_ENV_MARKER": str(marker)}
            install = subprocess.run(["bash", str(ROOT / "scripts/install.sh"),
                                      "--app", str(game), "--launcher", str(launch)],
                                     capture_output=True, text=True, env=env, timeout=20)
            self.assertEqual(install.returncode, 0, install.stdout + install.stderr)
            self.assertEqual((game / "BepInEx/plugins/AnimatesLinuxAudio.dll").read_text(),
                             "compiled plugin stub")
            backups = list((game / "BepInEx/bridge-backups").glob("*.dll"))
            self.assertEqual(len(backups), 1)
            self.assertEqual(backups[0].read_text(), "previous plugin")
            wrapper = home / ".local/share/animates-linux-audio-bridge/run-animates-with-audio.sh"
            desktop = home / ".local/share/applications/animates-linux-audio-bridge.desktop"
            self.assertTrue(wrapper.is_file())
            self.assertIn(str(wrapper), desktop.read_text())
            started = subprocess.run(["bash", str(wrapper)], capture_output=True, text=True,
                                     env=env, timeout=20)
            self.assertEqual(started.returncode, 0, started.stderr)
            self.assertIn("winhttp=n,b", marker.read_text())
            removed = subprocess.run(["bash", str(ROOT / "scripts/uninstall.sh")],
                                     capture_output=True, text=True, env={**env, "ANIMATES_APP": str(game)},
                                     timeout=20)
            self.assertEqual(removed.returncode, 0, removed.stderr)
            self.assertFalse(desktop.exists())
            self.assertFalse((game / "BepInEx/plugins/AnimatesLinuxAudio.dll").exists())
            self.assertEqual(len(list((game / "BepInEx/bridge-backups").glob("*.dll"))), 1)


if __name__ == "__main__":
    unittest.main()