# Portable Windows releases

The solution includes two WPF applications targeting **.NET Framework 4.8**:
- `SimoSim.exe`
- `SimoMaster.exe`

GitHub Actions builds both with Fody/Costura to embed referenced managed DLLs into each executable. These are **single-file applications**, not self-contained .NET runtimes: a Windows PC still needs .NET Framework 4.8 (or newer).

## Required setup before first successful build

`HMI.Controls.dll` was referenced via a path outside this repository. It is intentionally **not committed** because its redistribution rights are not established.

Provide an authorized copy in repository **Settings > Secrets and variables > Actions**, secret name:

`HMI_CONTROLS_DLL_BASE64`

On a trusted Windows PowerShell terminal, generate the value using:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\path\to\HMI.Controls.dll")) | Set-Clipboard
```

Paste the clipboard contents into the secret. Alternatively, for local development place the licensed DLL at `Dependencies/HMI.Controls.dll`. That directory is git-ignored.

## Builds and releases

- Pushes to `main` and pull requests: run the portable build and upload two EXEs as a GitHub Actions artifact.
- `workflow_dispatch`: run manually from Actions.
- Push a version tag (e.g. `v0.1.0`): build and publish the **two EXEs** to GitHub Releases only if the job passes.
- The workflow fails clearly when the HMI dependency is missing or packaging validation fails.

Notes: The build is not a substitute for a Windows GUI smoke test. Check launching both executables, control rendering, Modbus TCP communication, and settings persistence before distributing. The dependency must be licensed for any distribution in compiled software.
