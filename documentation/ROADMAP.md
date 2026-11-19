# Z8 PC CONNECT implementation status

## In current prototype source
- [x] Android Compose UI and LAN pairing flow
- [x] Windows .NET interactive desktop server
- [x] JPEG screen streaming over WebSocket
- [x] Basic mouse and keyboard input
- [x] Android-to-PC file upload
- [x] Clipboard backend endpoints
- [x] Android biometric/device credential prompt for local app identity verification
- [x] Camera permission request and CameraX preview foundation

## Still required before claiming a complete product
- [ ] Compile and test on Windows 11 Pro and Android 14/15/16
- [ ] Replace plaintext LAN transport with authenticated TLS/WebRTC and certificate pinning
- [ ] Persistent, revocable device identity and secure token storage
- [ ] Full PC file browser, download, folder create/rename/move/delete
- [ ] Clipboard UI and synchronization workflow
- [ ] Audio streaming and multi-monitor support
- [ ] Camera hand-landmark model, pinch/drag/swipe classifier, calibration and safety controls
- [ ] Gesture-to-Windows object/file/tab operations
- [ ] Windows Credential Provider integration for an approved phone-assisted sign-in flow; never transmit Windows passwords
- [ ] Internet connectivity via secure signaling and TURN or a supported VPN
- [ ] QR pairing and local discovery
- [ ] Signed Windows installer, Android APK/AAB, update and recovery flow
- [ ] Security review, accessibility review and performance testing

## Windows sign-in design constraint
Android biometrics authenticate the Android device owner to the Android app. They do not automatically authenticate a Windows account. Windows sign-in must be performed by Windows through an approved credential provider/authentication package. No lock-screen bypass is implemented.
