# 구현 정리

## 구조

- `MainWindow.xaml`: WPF 화면. 코드 비하인드는 `DataContext` 연결만 담당한다.
- `MainWindowViewModel`: `CommunityToolkit.Mvvm` 기반 ViewModel.
  - `[ObservableProperty]`로 입력값과 상태를 관리한다.
  - `[RelayCommand]`로 Chrome 확인, Chrome 실행, 자동화 실행, 중지를 처리한다.
- `AutomationInput`: 입력값 검증과 예상 공급가액/세액 계산을 담당한다.
- `ChromeConnectionService`: Chrome 원격 디버깅 포트 `9222` 연결 확인과 Chrome 실행을 담당한다.
- `AutomationSettingsService`: Chrome 경로, 프로필 디렉터리, 원격 디버깅 포트를 `%LOCALAPPDATA%\AutoTest.ErpAutomation\settings.json`에 저장하고 불러온다.
- `ErpAutomationService`: Playwright CDP 연결 후 ERP 자동화 절차를 실행한다.

## 자동화 방식

ERP 페이지의 정확한 DOM selector를 아직 고정할 수 없으므로, 현재 구현은 다음 기준으로 대상을 찾는다.

- 버튼/메뉴: 화면 텍스트가 일치하거나 포함되는 요소 클릭
- 입력칸: 라벨과 같은 행 또는 라벨 오른쪽에 있는 입력 요소 선택
- 드롭다운: 라벨 주변 select/combobox/button을 클릭한 뒤 옵션 텍스트 클릭
- 검증: 화면 전체 텍스트에서 예상 값 또는 완료 문구 확인

## 안전 조건

- 로그인 아이디/비밀번호 필드는 직접 입력하지 않는다.
- 로그인 버튼만 클릭한다.
- 계산 결과를 확인한 뒤 라인저장을 클릭한다.
- 계산 결과가 안 보이면 수량/단가를 재입력하고 계산을 한 번 재시도한다.
- 자동화 완료 후 Chrome 탭은 닫지 않는다.
- 자동화 실패 시 실패 화면 PNG와 HTML을 `%LOCALAPPDATA%\AutoTest.ErpAutomation\Failures`에 저장한다.

## Chrome 설정

기본값은 다음과 같다.

- Chrome 경로: 비워두면 프로그램이 일반 설치 경로에서 자동 탐색
- 프로필 디렉터리: `Default`
- 원격 디버깅 포트: `9222`

Chrome 설정은 WPF 화면의 `Chrome 연결 설정` 영역에서 수정하고 `설정 저장` 버튼으로 저장한다.

## 추가 보강 후보

- 실제 ERP DOM 확인 후 주요 필드 selector 고정
- 자동화 단계별 대기 시간 설정
- 라인 목록 검증 전용 selector 추가
