//! Test-only PowerShell launcher for isolated upstream runtime qualification.
//!
//! The Codex test suite derives login-shell command strings, which start
//! PowerShell without `-NoProfile`. On this workstation that executes the
//! user's interactive profile and contaminates exact-output assertions. Put
//! the compiled binary at the start of the test process PATH as `pwsh.exe` and
//! set `NEOBABYLON_QA_PWSH_REAL` to the actual PowerShell executable. This
//! wrapper adds only `-NoProfile`; it does not change production shell policy.

use std::process::{Command, ExitCode};

fn main() -> ExitCode {
    let Some(real_shell) = std::env::var_os("NEOBABYLON_QA_PWSH_REAL") else {
        eprintln!("NEOBABYLON_QA_PWSH_REAL is not set");
        return ExitCode::from(2);
    };

    match Command::new(real_shell)
        .arg("-NoProfile")
        .args(std::env::args_os().skip(1))
        .status()
    {
        Ok(status) => ExitCode::from(status.code().unwrap_or(1).clamp(0, 255) as u8),
        Err(error) => {
            eprintln!("could not start the test PowerShell executable: {error}");
            ExitCode::from(2)
        }
    }
}
