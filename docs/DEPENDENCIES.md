# 외부 구성요소

소스 ZIP에는 외부 실행 파일과 개인 서명 키를 넣지 않습니다. 실행 ZIP에는 다음 파일이 필요합니다.

| 구성요소 | 버전 | 배포 폴더 위치 | 공식 출처 |
|---|---|---|---|
| tun2socks | v2.7.0, Windows amd64 | `tun2socks.exe` | https://github.com/xjasonlyu/tun2socks/releases/tag/v2.7.0 |
| Wintun | 0.14.1, amd64 | `wintun.dll` | https://www.wintun.net/ |
| Google Platform Tools | 배포본의 `source.properties` 참조 | `platform-tools/` | https://developer.android.com/tools/releases/platform-tools |
| Android 앱 | versionCode3 / 2.1 | `USBDataBridge.apk` | 이 저장소에서 빌드 |

Google Platform Tools의 `NOTICE.txt`, tun2socks의 MIT 라이선스, Wintun 배포본의 라이선스도 실행 ZIP에 함께 넣습니다. 외부 구성요소의 사용·배포 조건은 해당 라이선스를 확인하세요.

네트워크 데이터나 통신사 조회값, 실제 사용자 로그는 저장소에 포함하지 않습니다.
