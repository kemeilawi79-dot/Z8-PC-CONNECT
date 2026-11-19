# Z8 PC CONNECT

Z8 PC CONNECT is a phone-to-Windows remote-control prototype for a trusted local network. The current Windows host and Android client are built from this repository.

## Windows host
1. Install the .NET 8 Desktop Runtime.
2. Extract the complete Windows ZIP package.
3. Run `Z8PCConnect.Desktop.exe`.
4. The PC card shows the LAN IP address, TCP port 8765, current six-digit pairing code, and service state.
5. Keep the host app open and Windows awake. The app does not register itself in Windows startup; closing it stops the local service.

## Android client
Install `Z8_PC_CONNECT_Android.apk` (the current package is a debug-signed test build; a production signing key is still needed before a Play Store release). On the Desktop page, enter the PC IP address, tap **CHECK PC / REFRESH PAIRING CODE**, then tap **PAIR AND CONNECT**. Both devices must be on the same trusted Wi-Fi or tethered network.

## PC control
- The phone shows the entire primary PC display fitted to the available area. Rotate to landscape for a screen-only view.
- Tap to click, long-press for right-click, drag to move/drag items, and swipe vertically to scroll.
- Use the Windows Start, Esc, Enter, and Tab controls in portrait mode.
- Open the PC keyboard to send characters directly to the active Windows app.
- Read the PC text clipboard or copy phone text to the PC clipboard.

## Phone screen on the PC
The Windows package bundles the Android Debug Bridge and official scrcpy Windows tools. Authorize USB debugging or connect ADB over Wi-Fi. In the PC app, **OPEN IN-APP PHONE CONTROL** shows a phone preview inside its card; click/tap, drag/swipe, use the mouse wheel to scroll, or type after focusing the preview. **OPEN SEPARATE PHONE CAST** launches a separate scrcpy window. The embedded preview uses ADB screenshots and is less fluid than the separate scrcpy cast.

## File transfer
On the phone's Files page, choose **Downloads**, **Desktop**, or **Documents**, then select a file. Photos, videos, APKs, music, documents, and other files are uploaded to the selected PC folder. Existing files are not overwritten; duplicate names receive a numbered suffix. The PC app lists recent incoming transfers and has a shortcut to the laptop Downloads folder.

## Tutorials and screenshots
The Android Tutorial page explains installation, pairing, landscape PC control, and the in-app phone preview. It includes screenshots of the phone client, full-screen PC view, and embedded phone preview. Google Play instructions are shown in the Windows app; the phone tutorial is for the GitHub release and setup flow.

## Network and sleep
Wi-Fi works when both devices are on the same trusted LAN. USB tethering can also provide a shared network; use the PC address on that tethered network. This is not USB debugging or a direct USB display cable.

The Windows host must be running in an interactive, signed-in Windows session for screen capture and input. If the laptop is asleep or shut down, the live connection cannot remain active. Wake-on-LAN is hardware/firmware/network dependent and is not implemented in this build.

## Windows sign-in
This app does not bypass the Windows lock screen, does not collect Microsoft account passwords, and does not unlock Windows through a phone. Windows sign-in requires a separately designed, signed, security-reviewed Windows Credential Provider integration. Do not enter your Microsoft password into this app.

## Security limitations
This is a LAN prototype using unencrypted HTTP/WebSocket on TCP 8765. Use a trusted private network only. Never port-forward the port or expose it to the public internet. Pairing codes are short-lived per server session and session tokens are random, but transport encryption and production hardening are still required.

## Not implemented
- Windows lock-screen sign-in / Microsoft account authentication.
- Wake-on-LAN.
- Linux PC-host receiver (the included Linux script is a phone-mirroring helper, not a Linux remote-desktop host).
- Remote audio, multiple monitor selection, TLS, and background operation while Windows is logged out.
