# 소스 빌드

Windows는 .NET Framework C#5와 Windows Forms를 사용합니다. 관리자 권한은 앱 실행에 필요하며, 빌드에는 필요하지 않습니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-windows.ps1
```

Android는 Python3, JDK17, Android SDK platform35 / build-tools35.0.0이 필요합니다. 자신의 서명 키를 사용하고, 서명 암호는 `USB_BRIDGE_STOREPASS`와 `USB_BRIDGE_KEYPASS` 환경 변수에 설정합니다.

```powershell
python scripts/build-android.py --sdk C:/Android/Sdk --java-bin C:/Java/jdk17/bin --keystore C:/private/release.jks --alias usbbridge
```

새 서명 키로 빌드한 APK는 이전 키로 서명된 앱 위에 설치할 수 없습니다. 업데이트를 배포하려면 같은 서명 키를 유지하세요. 서명 키·암호·로컬 로그는 저장소에 올리지 않습니다.

빌드 산출물은 `dist/`에 생성됩니다. 완성된 실행 ZIP에는 [외부 구성요소 안내](DEPENDENCIES.md)의 파일과 라이선스도 포함해야 합니다.

연결은 일반 TCP·UDP 전달과 IPv4·IPv6를 대상으로 합니다. 공개 DNS 질의는 휴대폰의 TCP·HTTPS 통로를 통해 Cloudflare DNS로 전달합니다. ICMP, 브로드캐스트, 일부 VPN, 특정 어댑터에 고정된 앱의 호환성을 보장하지 않습니다.
