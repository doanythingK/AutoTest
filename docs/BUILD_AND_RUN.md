# WPF 앱 실행 및 게시 절차

## 개발 실행

Windows PowerShell에서 저장소 루트로 이동한 뒤 실행한다.

```powershell
dotnet run --project .\src\AutoTest.ErpAutomation\AutoTest.ErpAutomation.csproj
```

WSL에서는 WPF 앱이 직접 실행되지 않는다. Windows 터미널이나 PowerShell에서 실행한다.

## 게시 파일 생성

운영 PC에 복사할 실행 파일은 Windows PowerShell에서 아래 스크립트로 만든다.

```powershell
.\scripts\publish-windows.ps1
```

스크립트가 실행하는 게시 명령은 아래와 같다.

```powershell
dotnet publish .\src\AutoTest.ErpAutomation\AutoTest.ErpAutomation.csproj -c Release -r win-x64 --self-contained false -o .\publish\AutoTest.ErpAutomation
```

게시 결과 실행 파일:

```text
publish\AutoTest.ErpAutomation\AutoTest.ErpAutomation.exe
```

## 운영 PC 실행 전 확인

1. 운영 PC에 .NET 8 Desktop Runtime이 설치되어 있는지 확인한다.
2. Chrome에 ERP 아이디와 비밀번호가 저장되어 있는지 확인한다.
3. WPF 앱에서 `Chrome 실행` 또는 `Chrome 연결 확인`을 먼저 사용한다.
4. 원격 디버깅 포트 기본값은 `9222`이다.

## 운영 데이터 위치

앱 실행 중 생성되는 설정, 로그, 실패 자료는 저장소나 게시 폴더가 아니라 아래 로컬 경로에 저장된다.

```text
%LOCALAPPDATA%\AutoTest.ErpAutomation
```

주요 하위 폴더:

- `RunLogs`: 실행 로그
- `Failures`: 실패 화면 PNG와 전체 프레임 HTML
- `settings.json`: Chrome 연결 설정

## 주의

- 게시 폴더의 `bin`, `obj`, `publish` 산출물은 Git에 커밋하지 않는다.
- ERP 아이디와 비밀번호는 앱에서 입력하지 않는다.
- 자동화 완료 후 Chrome 탭은 닫지 않는다.
