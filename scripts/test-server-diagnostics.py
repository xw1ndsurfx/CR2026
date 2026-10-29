"""Integration checks for the Linux supervisor; no production server required."""
import pathlib
import signal
import subprocess
import tempfile
import time
import unittest


SCRIPT = pathlib.Path(__file__).with_name("start-server-diagnostics.sh").resolve()


class SupervisorTests(unittest.TestCase):
    def test_exit_code_arguments_and_stdin(self):
        with tempfile.TemporaryDirectory() as directory:
            result = subprocess.run(
                ["bash", str(SCRIPT), "bash", "-c",
                 'read -r value; printf "%s:%s" "$1" "$value"; exit 23', "fixture", "space argument"],
                cwd=directory, input="input value\n", text=True, capture_output=True, timeout=10,
            )
            self.assertEqual(result.returncode, 23)
            self.assertEqual(result.stdout, "space argument:input value")
            log = next(pathlib.Path(directory, "logs").glob("host-exit-*.log")).read_text()
            self.assertIn("shellExitCode=23", log)
            self.assertNotIn("space argument", log)

    def test_sigkill_observed_without_claiming_oom(self):
        with tempfile.TemporaryDirectory() as directory:
            result = subprocess.run(
                ["bash", str(SCRIPT), "bash", "-c", "kill -KILL $$"],
                cwd=directory, capture_output=True, timeout=10,
            )
            self.assertEqual(result.returncode, 137)
            log = next(pathlib.Path(directory, "logs").glob("host-exit-*.log")).read_text()
            self.assertIn("shellExitCode=137", log)
            self.assertIn("does not establish the cause or prove OOM", log)

    def test_sigterm_forwarded_and_shutdown_waited_for(self):
        with tempfile.TemporaryDirectory() as directory:
            # Child deliberately takes time to close after the signal, exercising interrupted wait.
            child = subprocess.Popen(
                ["bash", str(SCRIPT), "bash", "-c",
                 "trap 'sleep 0.2; echo closed; exit 42' TERM; echo ready; while true; do sleep 0.05; done"],
                cwd=directory, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True,
            )
            try:
                self.assertEqual(child.stdout.readline().strip(), "ready")
                child.send_signal(signal.SIGTERM)
                output, _ = child.communicate(timeout=10)
                self.assertEqual(child.returncode, 42)
                self.assertIn("closed", output)
                log = next(pathlib.Path(directory, "logs").glob("host-exit-*.log")).read_text()
                self.assertIn("received TERM", log)
                self.assertIn("shellExitCode=42", log)
            finally:
                if child.poll() is None:
                    child.kill()
                    child.communicate()


if __name__ == "__main__":
    unittest.main()
