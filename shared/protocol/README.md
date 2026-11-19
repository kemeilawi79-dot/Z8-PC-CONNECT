# Z8 PC CONNECT protocol draft

## Pairing flow (to implement)
1. Companion creates a short-lived pairing challenge and displays QR containing PC public identity, ephemeral endpoint, nonce and protocol version.
2. Android scans QR and verifies the PC identity via an out-of-band six-digit confirmation shown on both screens.
3. Devices establish an authenticated session using ephemeral key agreement; derive session keys and bind them to device identity.
4. PC user explicitly approves the Android device. Store only public device identity and revocation metadata.
5. Rotate session keys and expire pairing challenges. Never encode account passwords or reusable bearer secrets in QR codes.

## Transport channels (to implement)
- WebRTC video: desktop capture encoded H.264.
- WebRTC data channel: mouse/keyboard events and session control.
- Separate authenticated file-transfer channel with per-folder authorization and transfer limits.
- Audio media track from supported Windows audio capture.

## Security rules
- Require authenticated signaling and TLS.
- Verify peer identity; do not trust LAN membership alone.
- Do not open RDP 3389 to the public internet.
- Never collect Microsoft/Windows passwords in Android.
- Windows controls remain constrained by current user's rights, UAC and secure desktop.
