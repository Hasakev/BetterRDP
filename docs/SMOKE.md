# Manual smoke checklist

Some guarantees can't be unit-tested — they depend on `mstsc.exe`, a real RDP server, and
physical monitors. Run these by hand. Where a manual check has a durable unit-test
byproduct, that test is named so the manual step crystallizes into something permanent.

## S1 — The DPAPI password-injection trick (DO THIS FIRST, before implementing)

This validates the single assumption the whole project rests on: that `mstsc` will log in
silently from a `.rdp` that carries a DPAPI `password 51:b:` blob.

1. In PowerShell, generate the blob for a known password bound to your Windows user:
   ```powershell
   Add-Type -AssemblyName System.Security
   $pw = [Text.Encoding]::Unicode.GetBytes("YOUR_TEST_PASSWORD")
   $blob = [Security.Cryptography.ProtectedData]::Protect($pw, $null, 'CurrentUser')
   ($blob | ForEach-Object { $_.ToString("X2") }) -join ''
   ```
2. Hand-write a `smoke.rdp`:
   ```
   full address:s:YOUR_TEST_SERVER
   username:s:YOUR_TEST_USER
   password 51:b:<HEX FROM STEP 1>
   screen mode id:i:1
   ```
3. `mstsc.exe smoke.rdp`
4. **PASS** = it connects with **no password prompt**. **FAIL** = it prompts → the trick
   doesn't work on this machine/server; stop and rethink before building further.
5. Delete `smoke.rdp`.

> Durable byproduct: `tests/test_rdp_generation.py::test_password_blob_roundtrips_via_dpapi`

## S2 — Monitor selection lands on the right physical screens

After implementing Display Profiles. The `selectedmonitors` IDs are mstsc-internal and may
not equal Windows display numbers — this is the calibration risk from grilling Q5.

1. Build a FULLSCREEN_MULTIMON profile selecting a subset (e.g. only the left monitor).
2. Launch. **PASS** = the session opens on exactly the intended physical monitor(s).
3. If wrong, compare against `mstsc.exe /l` (lists mstsc's monitor IDs) and adjust the
   index→ID mapping.

## S3 — Temp .rdp is gone after launch

1. Launch a real connection.
2. Check the temp dir during and after. **PASS** = the `.rdp` is deleted post-launch.

> Durable byproduct: `tests/test_launch.py::test_temp_rdp_is_deleted_after_launch`

## S4 — Two accounts on one server, side by side

1. Launch Server X as Credential A, then again as Credential B.
2. **PASS** = two independent sessions, no Credential Manager prompt, no re-typing.
