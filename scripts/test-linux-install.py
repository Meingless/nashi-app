#!/usr/bin/env python3
"""Validate app registration and real launcher argument boundaries in private fixtures."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import time

root = Path(__file__).resolve().parents[1]
with tempfile.TemporaryDirectory(prefix="nashi-install-") as temporary:
    base = Path(temporary)
    sentinel = base / "sentinel"
    payload = '$(IFS=;touch $NASHI_INSTALL_TEST_SENTINEL)'
    sentinel_env = dict(os.environ, NASHI_INSTALL_TEST_SENTINEL=str(sentinel))
    # Prove the same payload would trip the guard if a launcher used a shell.
    subprocess.run(["/bin/sh", "-c", 'printf %s "' + payload + '"'],
                   env=sentinel_env, cwd="/", check=True, capture_output=True)
    assert sentinel.is_file()
    sentinel.unlink()
    for index, name in enumerate(["portable folder", 'space $ dollar ` tick % percent " quote \\ slash']):
        app = base / name
        app.mkdir()
        shutil.copy2(root / "linux/packaging/install.sh", app / "install.sh")
        (app / "nashi.png").write_bytes(b"fixture")
        (app / "Nashi").write_text(
            '#!/usr/bin/env python3\nimport json,os,sys,tempfile,time\n'
            'from pathlib import Path\n'
            'log = Path(os.environ["NASHI_INSTALL_TEST_LOG"])\n'
            'with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", dir=log.parent, delete=False) as f:\n'
            '    temporary = f.name\n'
            '    json.dump(sys.argv,f)\n'
            'try:\n'
            '    Path(str(log)+".ready").touch()\n'
            '    deadline = time.monotonic()+5\n'
            '    while not Path(str(log)+".release").exists():\n'
            '        if time.monotonic() >= deadline: raise TimeoutError("Publication gate timed out")\n'
            '        time.sleep(.01)\n'
            '    os.replace(temporary,log)\n'
            'finally:\n'
            '    if os.path.exists(temporary): os.unlink(temporary)\n', encoding="utf-8")
        (app / "Nashi").chmod(0o755)
        log = base / f"arguments-{index}.json"
        env = dict(sentinel_env, XDG_DATA_HOME=str(base / "xdg"), NASHI_INSTALL_TEST_LOG=str(log))
        subprocess.run(["bash", str(app / "install.sh")], check=True, env=env, capture_output=True)
        desktop = base / "xdg/applications/io.github.orkunisitmak.Nashi.desktop"
        subprocess.run(["desktop-file-validate", str(desktop)], check=True)
        fixture = base / ("file ; " + payload + ".png")
        fixture.write_bytes(b"fixture")
        launched = subprocess.Popen(["gio", "launch", str(desktop), str(fixture)],
                                    env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        deadline = time.monotonic() + 5
        ready = Path(str(log) + ".ready")
        while not ready.exists() and time.monotonic() < deadline:
            time.sleep(.01)
        assert ready.exists(), "Launcher did not stage its JSON log"
        assert not log.exists(), "JSON log was visible before atomic publication"
        Path(str(log) + ".release").touch()
        _, error = launched.communicate(timeout=5)
        if launched.returncode: raise RuntimeError(str(index) + " " + desktop.read_text() + error)
        deadline = time.monotonic() + 5
        while not log.exists() and time.monotonic() < deadline:
            time.sleep(.05)
        assert json.loads(log.read_text()) == [str(app / "Nashi"), str(fixture)]
        assert not sentinel.exists()
        desktop.unlink()
    print("PASS desktop registration: spaces, reserved characters and literal file arguments; atomic JSON publication")
