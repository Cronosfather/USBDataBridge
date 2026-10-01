# USB Data Bridge

USB로 연결한 Android 휴대폰의 모바일 인터넷을 Windows 노트북에서 사용하는 앱입니다.

![Windows 화면](docs/windows-idle.png)

## 사용 방법

1. Release ZIP을 **전부 압축 해제**합니다.
2. 휴대폰에서 모바일 데이터와 USB 디버깅을 켜고, 데이터 전송이 가능한 USB 케이블로 연결합니다. 기본 USB 테더링·핫스팟은 끕니다.
3. `USBDataBridge.exe`를 실행하고 관리자 권한을 허용합니다. 가상 어댑터와 인터넷 경로 설정에 필요합니다.
4. 처음에는 **휴대폰 앱 설치**를 누르고 휴대폰의 USB 디버깅 허용 창을 확인합니다.
5. **인터넷 연결**을 누릅니다. 사용 후에는 **연결 끊기**를 누릅니다.

**연결 복구**는 이 앱이 만든 인터넷 경로를 정리합니다. 앱을 열 수 없다면 배포 폴더의 `인터넷경로복구.exe`를 실행합니다.

**데이터 사용량 확인**은 통신사 조회값의 연결 전·후 차이를 수동으로 비교하는 기능입니다. 현재 안내는 마이케이티 기준입니다. 일반/테더링 데이터의 차감을 자동으로 판별하지 않습니다.

## 지원 범위

- Windows 10/11 x64, Android 8(API26) 이상. Android 앱 targetSdk34.
- 일반 TCP·UDP 전달과 IPv4·IPv6. 가상 어댑터를 통한 연결을 사용합니다.
- 공개 DNS 질의는 휴대폰의 TCP·HTTPS 통로를 통해 Cloudflare DNS로 전달합니다.
- 진단·로그 복사 버튼과 개발 로그 창은 일반 사용자 화면에서 제공하지 않습니다.

개발 버전입니다. ICMP, 브로드캐스트, 일부 VPN, 특정 어댑터에 고정된 앱, 모든 통신사·기기의 호환성을 보장하지 않습니다. 휴대폰 UDP 통로가 작동하지 않으면 게임·음성·QUIC 등 일부 통신이 실패할 수 있습니다. 실제 기기에서 테스트한 이후 배포 범위를 정하세요.

모바일 데이터 요금은 통신사 기준으로 적용됩니다. 이 앱은 통신사의 일반/공유 데이터 차감 구분을 보장하지 않습니다.

## 소스 빌드

Windows는 .NET Framework C#5와 Windows Forms를 사용합니다. 관리자 권한은 앱 실행에 필요하며, 빌드에는 필요하지 않습니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-windows.ps1
```

Android는 Python3, JDK17, Android SDK platform35 / build-tools35.0.0이 필요합니다. 자신의 서명 키를 사용하고, 서명 암호는 `USB_BRIDGE_STOREPASS`와 `USB_BRIDGE_KEYPASS` 환경 변수에 설정합니다.

```powershell
python scripts/build-android.py --sdk C:/Android/Sdk --java-bin C:/Java/jdk17/bin --keystore C:/private/release.jks --alias usbbridge
```

새 서명 키로 빌드한 APK는 이전 키로 서명된 앱 위에 설치할 수 없습니다. 업데이트를 배포하려면 같은 서명 키를 유지하세요. 서명 키·암호·로컬 로그는 저장소에 올리지 않습니다.

빌드 산출물은 `dist/`에 생성됩니다. 완성된 실행 ZIP을 만들려면 [외부 구성요소 안내](docs/DEPENDENCIES.md)의 파일과 라이선스도 포함해야 합니다. GitHub 저장소에는 소스를 올리고, 실행 ZIP은 Releases의 첨부 파일로 제공할 수 있습니다.

## 라이선스

직접 제작한 소스는 [MIT](LICENSE)입니다. ADB, tun2socks, Wintun에는 각 프로젝트의 별도 라이선스가 적용됩니다.
