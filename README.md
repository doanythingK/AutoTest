# AutoTest

ERP 매출등록 절차를 Chrome에 연결해 자동화하는 WPF 프로그램입니다.

## 현재 구성

- `src/AutoTest.ErpAutomation`: WPF 앱
- `CommunityToolkit.Mvvm`: MVVM ViewModel, Command, ObservableProperty 구성
- `Microsoft.Playwright`: Chrome CDP 연결과 브라우저 자동화에 사용할 패키지
- `docs/ERP_SALES_AUTOMATION_REQUIREMENTS.md`: 자동화 요구사항 정리 문서

## 실행 전제

Chrome에 ERP 로그인 정보가 저장되어 있어야 합니다. 프로그램은 아이디와 비밀번호를 새로 입력하지 않고 로그인 버튼만 클릭하는 방향으로 구현합니다.

Chrome 연결은 원격 디버깅 포트 `9222`를 기준으로 합니다.

```powershell
chrome.exe --remote-debugging-port=9222 --profile-directory=Default
```
