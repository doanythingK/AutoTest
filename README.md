# AutoTest

ERP 매출등록 절차를 Chrome에 연결해 자동화하는 WPF 프로그램입니다.

## 현재 구성

- `src/AutoTest.ErpAutomation`: WPF 앱
- `CommunityToolkit.Mvvm`: MVVM ViewModel, Command, ObservableProperty 구성
- `Microsoft.Playwright`: Chrome CDP 연결과 브라우저 자동화
- `docs/ERP_SALES_AUTOMATION_REQUIREMENTS.md`: 자동화 요구사항 정리 문서

## 실행 전제

Chrome에 ERP 로그인 정보가 저장되어 있어야 합니다. 프로그램은 아이디와 비밀번호를 새로 입력하지 않고 로그인 버튼만 클릭하는 방향으로 구현합니다.

Chrome 연결은 원격 디버깅 포트 `9222`를 기준으로 합니다.

```powershell
chrome.exe --remote-debugging-port=9222 --profile-directory=Default
```

## 구현된 자동화 흐름

1. WPF 화면에서 수량, 단가, 거래처코드, 계정코드, 거래일자를 입력한다.
2. `Chrome 연결 확인`으로 원격 디버깅 포트 연결 상태를 확인한다.
3. `자동화 실행`을 누르면 ERP 로그인 페이지로 이동한다.
4. 저장된 계정 정보는 건드리지 않고 로그인 버튼만 클릭한다.
5. 회계관리, 거래전표, 매출등록, 원화 메뉴로 이동한다.
6. 거래일자, 차변, 매출구분, 거래처코드, 담당부서, 세금계산서 발송구분을 입력한다.
7. 품목명 `차피 압축`, 수량, 단가를 입력하고 계산한다.
8. 공급가액과 세액이 화면에 반영되었는지 확인한다.
9. 계정코드(대변)를 입력하고 라인저장 전 계산 결과를 다시 확인한다.
10. 라인저장, 거래전기, 회계전표 동일자생성, 원장전기 완료까지 진행한다.

## 구현 메모

- ERP 화면 selector가 확정되어 있지 않아 텍스트 버튼과 라벨 주변 입력칸을 찾는 방식으로 동작한다.
- ERP 화면 구조가 바뀌거나 동일한 라벨이 여러 개 있으면 정확한 selector 보강이 필요하다.
- 프로그램은 자동화 중 기존 Chrome 탭을 닫지 않는다.
- 로그인 아이디/비밀번호 입력 필드는 자동화 코드에서 채우지 않는다.
- 자동화 실패 시 `%LOCALAPPDATA%\AutoTest.ErpAutomation\Failures`에 화면 PNG와 HTML을 저장한다.
