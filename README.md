# Z8 PC CONNECT

A phone-to-Windows desktop remote-control prototype for a trusted local network.

## Windows host
1. Install the .NET 8 Desktop Runtime.
2. Extract the Windows package and run `Z8PCConnect.Desktop.exe`.
3. Keep the host app open. Its centered card shows the PC LAN address, port 8765, one-time pairing code, connection state, and server log.
4. Allow TCP 8765 on Windows Firewall for Private networks only.

## Android phone
Install `Z8_PC_CONNECT_Android.apk`. Enter the PC address and current six-digit code in the connection card and tap Pair and Connect.

After pairing, the Desktop page provides:
- Full primary-display capture fitted to the phone screen (not a cropped corner).
- Tap-to-click, long-press right-click, drag-to-move, and swipe-to-scroll.
- Windows Start button, Esc, Enter, and Tab.
- Live keyboard entry: characters and backspaces are sent as they are typed; there is no Send button.
- Copy selected PC text with Ctrl+C and read the Windows clipboard back on the phone.
- Phone-to-PC file upload.

## Network and sleep
Wi-Fi works when both devices are on the same trusted LAN. USB tethering can also provide a shared network; use the PC address on that tethered network. This is not USB debugging or a direct USB display cable.

The Windows host must be running in an interactive, signed-in Windows session for screen capture and input. If the laptop is asleep or shut down, the live connection cannot remain active. Wake-on-LAN is hardware/firmware/network dependent and is not implemented in this build. Keep the laptop awake for continuous access.

## Windows sign-in
This app does not bypass the Windows lock screen, does not collect Microsoft account passwords, and does not unlock Windows through a phone. Windows sign-in requires a separately designed, signed, security-reviewed Windows Credential Provider integration. Do not enter your Microsoft password into this app.

## Security limitations
This is a LAN prototype using unencrypted HTTP/WebSocket on TCP 8765. Use a trusted private network only. Never port-forward the port or expose it to the public internet. Pairing codes are one-time and session tokens are random, but transport encryption and production hardening are still required.

## Not implemented
- Windows lock-screen sign-in / Microsoft account authentication.
- Wake-on-LAN.
- Linux PC-host receiver (the included Linux script is a phone-mirroring helper, not a Linux remote-desktop host).
- Remote audio, multiple monitor selection, TLS, persistent service/session capture, and background operation while Windows is logged out.