# 구현 정리

## 구조

- `MainWindow.xaml`: WPF 화면. 코드 비하인드는 `DataContext` 연결만 담당한다.
- `MainWindowViewModel`: `CommunityToolkit.Mvvm` 기반 ViewModel.
  - `[ObservableProperty]`로 입력값과 상태를 관리한다.
  - `[RelayCommand]`로 Chrome 확인, Chrome 실행, 자동화 실행, 중지를 처리한다.
- `AutomationInput`: 입력값 검증과 예상 공급가액/세액 계산을 담당한다.
- `ChromeConnectionService`: Chrome 원격 디버깅 포트 `9222` 연결 확인과 Chrome 실행을 담당한다.
- `AutomationSettingsService`: Chrome 경로, 프로필 디렉터리, 원격 디버깅 포트, 단계 대기 시간을 `%LOCALAPPDATA%\AutoTest.ErpAutomation\settings.json`에 저장하고 불러온다.
- `AutomationRunLogService`: 자동화 실행 1회마다 입력값, 예상 계산값, 단계 로그를 `%LOCALAPPDATA%\AutoTest.ErpAutomation\RunLogs`에 저장한다.
- `ErpAutomationService`: Playwright CDP 연결 후 ERP 자동화 절차를 실행한다.

## 자동화 방식

ERP 페이지의 정확한 DOM selector를 아직 고정할 수 없으므로, 현재 구현은 다음 기준으로 대상을 찾는다.

- 실행 로그: 요구사항의 30단계와 맞도록 `[01/30]`부터 `[30/30]`까지 단계 번호를 표시한다.
- 파일 로그: 실행 1회마다 별도 로그 파일을 만들고 화면 로그와 같은 내용을 append한다.
- Chrome 연결: 자동화 실행 시 `ChromeConnectionService.CheckConnectionAsync`를 먼저 호출하고, 실패하면 ERP 페이지로 이동하지 않는다.
- 로그인 성공 판정: 로그인 페이지에도 보일 수 있는 일반 문구는 제외하고 `회계관리`, `로그아웃`처럼 로그인 후 화면에서 기대되는 문구만 사용한다.
- 버튼/메뉴: 화면 텍스트가 일치하거나 포함되는 요소 클릭
- 클릭: 요소 중앙의 현재 화면 좌표를 계산해 pointer/mouse 이벤트를 순서대로 발생시킨다.
- 입력칸: 라벨과 같은 행 또는 라벨 오른쪽에 있는 입력 요소 선택
- 드롭다운: 라벨 주변 select/combobox/button을 클릭한 뒤 옵션 텍스트 클릭
- 검증: 화면 전체 텍스트에서 예상 값 또는 완료 문구 확인
- 숫자 검증: 공급가액/세액/수량/단가는 화면 텍스트와 기대값에서 콤마와 공백을 제거한 뒤 비교하고, 라인 목록에서는 품목, 수량, 단가, 공급가액, 세액 그룹을 각각 확인한다.

## 안전 조건

- 로그인 아이디/비밀번호 필드는 직접 입력하지 않는다.
- 로그인 버튼만 클릭한다.
- 거래일자는 사용자 입력값으로 받지 않고 자동화 실행 직전에 오늘 날짜로 갱신한다.
- 계산 결과를 확인한 뒤 라인저장을 클릭한다.
- 계산 결과가 안 보이거나 공급가액/세액 주변 값이 `0`으로 감지되면 수량/단가를 재입력하고 계산을 한 번 재시도한다.
- 자동화 완료 후 Chrome 탭은 닫지 않는다.
- 자동화 실패 시 실패 화면 PNG와 HTML을 `%LOCALAPPDATA%\AutoTest.ErpAutomation\Failures`에 저장한다.

## Chrome 설정

기본값은 다음과 같다.

- Chrome 경로: 비워두면 프로그램이 일반 설치 경로에서 자동 탐색
- 프로필 디렉터리: `Default`
- 원격 디버깅 포트: `9222`
- 단계 대기 시간: `12`초

Chrome 설정은 WPF 화면의 `Chrome 연결 설정` 영역에서 수정하고 `설정 저장` 버튼으로 저장한다.

## 추가 보강 후보

- 실제 ERP DOM 확인 후 주요 필드 selector 고정
- 라인 목록 검증 전용 selector 추가

## 저장소 제외 정책

- `bin/`, `obj/`, `publish/`, `artifacts/`는 빌드 산출물이므로 제외한다.
- `%LOCALAPPDATA%\AutoTest.ErpAutomation`에 저장되는 설정, 실행 로그, 실패 화면 자료는 로컬 운영 데이터로 취급한다.
- 저장소에는 WPF 소스, 요구사항 문서, 구현 문서만 유지한다.
