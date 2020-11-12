using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DroHub.Helpers {
    public static class RunProcess {
        public static Task<Process> runProcess(string executable_path, string arguments) {
            var tcs = new TaskCompletionSource<Process>();
            var process = new Process {
                StartInfo = {
                    UseShellExecute = false,
                    FileName = executable_path,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };

            process.Exited += (sender, args) => {
                if (process.ExitCode == 0) {
                    tcs.SetResult(process);
                }
                else {
                    tcs.SetException(new InvalidOperationException(
                        $"{process.StartInfo.FileName} {process.StartInfo.Arguments} was not successful and exited with {process.ExitCode}. Stderr {( process.StandardError.ReadToEnd()).Trim()}"));
                }
            };
            process.Start();

            return tcs.Task;
        }

        public static async Task<string> getProcessOutput(string executable_path, string arguments) {
            using var p = await runProcess(executable_path, arguments);
            var output = (await p.StandardOutput.ReadToEndAsync()).Trim();
            if (string.IsNullOrEmpty(output))
                throw new InvalidDataException($"No output from {executable_path} as would be expected");

            return output;
        }

    }
}