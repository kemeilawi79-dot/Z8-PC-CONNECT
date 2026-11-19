# Z8 PC CONNECT
Phone-to-PC remote desktop over a trusted local network.

## Windows
Install the .NET 8 Desktop Runtime, then run Z8PCConnect.Desktop.exe. The desktop window shows the PC IPv4 address and a one-time pairing code. Allow TCP port 8765 on Windows Firewall for your private network only.

## Android
Install the Android APK, enter the PC IPv4 address and current pairing code, and tap Pair and connect. Rotate the phone to use landscape or portrait. Tap to click, long-press for right-click, drag to move the pointer, and use the text field to send keyboard text.

## Cable
Connect both devices through USB tethering and use the PC address assigned on that tethered network. USB debugging is not required for this remote desktop transport.

## Linux
The Linux package is a separate phone-mirroring helper based on scrcpy/ADB. It mirrors an Android phone to Linux; it is not the Windows-host remote desktop receiver. Install scrcpy and adb from your distribution packages, enable Android USB debugging, connect by USB once, then run `scrcpy --tcpip` for wireless phone mirroring.

## Security and limitations
This prototype is LAN-only and currently uses unencrypted HTTP/WebSocket. Use trusted private networks only; never port-forward TCP 8765 to the internet. Pairing code is one-time and session tokens are random. It does not implement Windows lock-screen sign-in, a Windows Credential Provider, or Microsoft account password handling. The optional Android biometric page verifies locally on the phone only. Clipboard sync, remote audio, multi-monitor selection, Linux remote-desktop hosting, and production TLS are not implemented yet.
