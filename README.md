# DownCleaner
다운로드 및 여러 이외의 폴더를 분석하고 삭제목록을 분류하여 폴더를 정리해주는 클리너 프로그램입니다.

이 프로젝트는 **WPF 기반 Windows 데스크톱 파일 정리 프로그램**입니다. 이름은 DownCleaner이고, 네임스페이스는 FileCleaner로 되어 있습니다. 목적은 폴더/파일을 스캔해서 삭제 후보를 추천하고, 저장공간을 분석하고, 사용자가 고른 파일을 휴지통으로 보내는 것입니다.

## 프로그램 사용법

### 1. 실행

DownCleaner를 실행하면 시작 화면이 먼저 표시됩니다.

- 기본 즐겨찾기 폴더를 미리 분석합니다.
- 작업표시줄에서도 로딩 진행률을 확인할 수 있습니다.
- 파일 수가 많은 PC에서는 첫 실행 또는 시작 분석이 다소 오래 걸릴 수 있습니다.

기본 분석 대상은 Windows 빠른 접근 폴더입니다.

- 바탕화면
- 문서
- 다운로드
- 사진
- 음악
- 비디오

### 2. 파일 탐색기 탭 사용

`파일 탐색기` 탭에서는 왼쪽 폴더 트리에서 분석할 폴더를 선택합니다.

폴더를 선택하면 오른쪽에 파일과 하위 폴더가 트리 형태로 표시됩니다.

표시 정보:

- 선택 체크박스
- 폴더/파일 이름
- 크기
- 위험도 점수
- 파일 정보 또는 연결 프로그램

위험도는 다음 기준으로 표시됩니다.

| 점수 | 표시 | 의미 |
| --- | --- | --- |
| 0~39점 | 낮음 (삭제 후보) | 삭제 후보로 추천 가능 |
| 40~69점 | 중간 | 사용자가 직접 확인 필요 |
| 70~100점 | 높음 (삭제 주의) | 보존 권장 |

`삭제 후보 선택` 버튼을 누르면 낮음 등급 파일만 자동 선택됩니다. 중간 등급 파일은 자동 선택되지 않습니다.

### 3. 미리보기 사용

파일을 선택하면 오른쪽 미리보기 영역에서 내용을 확인할 수 있습니다.

지원 예시:

- 이미지 파일: `jpg`, `png`, `bmp`, `gif`, `webp`
- 텍스트/코드 파일: `txt`, `log`, `md`, `json`, `xml`, `cs`, `xaml`, `js`, `ts`, `tsx`, `jsx`, `env` 등
- 3D 파일: `obj`, `stl`

3D 미리보기는 마우스 드래그로 시점을 돌리고, 휠로 확대/축소할 수 있습니다.

### 4. 삭제 목록에 추가

파일 또는 폴더를 체크한 뒤 `삭제 목록에 추가` 버튼을 누르면 삭제 목록으로 이동합니다.

주의:

- 중간/높음/사용 중 파일이 포함되어 있으면 확인 창이 표시됩니다.
- 확인 창에서 `아니오`를 선택하면 낮음 등급 파일만 추가됩니다.
- `취소`를 선택하면 삭제 목록 추가 작업을 중단합니다.

우클릭 메뉴에서도 삭제 목록 추가, 경로 복사, Windows 파일 탐색기에서 보기 같은 작업을 할 수 있습니다.

### 5. 삭제 목록 탭 사용

`삭제 목록` 탭에서는 삭제 예정 파일을 출처 폴더 구조와 함께 확인할 수 있습니다.

가능한 작업:

- 삭제 예정 파일 확인
- 파일 미리보기
- 삭제 목록에서 제거
- 선택한 항목을 휴지통으로 이동

DownCleaner는 파일을 완전 삭제하지 않고 Windows 휴지통으로 이동합니다. 휴지통에서 복구할 수 있지만, 중요한 파일은 삭제 전 반드시 미리보기와 경로를 확인하는 것이 좋습니다.

### 6. 저장공간 탭 사용

`저장공간` 탭에서는 다음 정보를 볼 수 있습니다.

- 드라이브 사용량
- 현재 분석된 파일의 위험도 비율
- 큰 폴더 분석 결과

상단의 `저장공간 분석` 버튼을 누르면 빠른 접근 폴더와 사용자가 추가한 루트 폴더의 크기를 계산합니다.

### 7. 프로젝트 폴더 탭 사용

`프로젝트 폴더` 탭에서는 감지된 개발 프로젝트 폴더를 확인할 수 있습니다.

분류 예시:

- Unity 프로젝트
- Node.js 프로젝트
- Visual Studio/.NET 프로젝트
- Python 프로젝트
- Rust 프로젝트
- Go 프로젝트
- Flutter 프로젝트

Unity의 `Library`, `PackageCache`, Node의 `node_modules`, .NET의 `bin/obj` 같은 생성/캐시 폴더는 일반 분석에서 제외해 성능 저하와 오탐을 줄입니다.

### 8. 설정 탭 사용

`설정` 탭에서는 추천과 스캔 동작을 조정할 수 있습니다.

- 자동 선택 기준 점수
  - 낮음 등급 안에서만 조정됩니다.
  - 기본값은 40점 미만입니다.
- 기본 스캔 모드
  - `Detailed`: 하위 폴더 포함 설정을 따릅니다.
  - `Quick`: 현재 폴더만 분석합니다.
- 하위 폴더 포함
- 삭제 이력 기반 추천 우선순위 사용
- 자세한 진행 상태 표시
- 최근 오류 이력 확인

설정은 자동으로 저장되며 앱을 다시 실행해도 유지됩니다.

### 9. 안전하게 사용하는 팁

- `낮음 (삭제 후보)`도 무조건 삭제해도 된다는 뜻은 아닙니다.
- `중간`이나 `높음` 파일은 삭제 전 경로와 미리보기를 꼭 확인하세요.
- 프로젝트 폴더 안의 소스 파일, 설정 파일, `.env` 파일은 보호 점수가 적용됩니다.
- 시스템 경로(`Windows`, `Program Files`, `ProgramData`)의 파일은 삭제 후보로 잘 올라오지 않도록 보호됩니다.
- 삭제는 휴지통 이동 방식이지만, 휴지통을 비우면 복구가 어려울 수 있습니다.

**프로젝트 종류**

DownCleaner.csproj 기준으로:

xml

`<OutputType>WinExe</OutputType>
<TargetFramework>net8.0-windows</TargetFramework>
<UseWPF>true</UseWPF>
<UseWindowsForms>true</UseWindowsForms>`

즉:

- .NET 8
- Windows 전용 앱
- WPF UI
- 일부 Windows Forms 사용
- 실행 파일 형태의 데스크톱 앱

Windows Forms는 주로 폴더 선택 다이얼로그, 즉 FolderBrowserDialog에 사용됩니다.

**사용 라이브러리**

외부 NuGet 패키지는 없습니다. 현재는 전부 .NET 기본 라이브러리와 Windows API 기반입니다.

사용 중인 주요 .NET/Windows 기술:

- WPF
    - Window, Grid, TreeView, DataGrid, TabControl, ProgressBar, StatusBar
    - XAML 기반 UI
    - 데이터 바인딩
    - BooleanToVisibilityConverter
- MVVM 패턴
    - MainViewModel
    - INotifyPropertyChanged
    - ICommand
    - RelayCommand, AsyncRelayCommand
- ObservableCollection
    - UI 목록 자동 갱신
- 커스텀 BulkObservableCollection
    - 대량 데이터 갱신 시 UI 이벤트를 줄이기 위한 컬렉션
- System.Text.Json
    - 설정 저장
    - 추천 프로필 저장
    - 최근 오류 이력 저장
- System.IO
    - 파일/폴더 스캔
    - 파일 크기, 수정일, 접근일 확인
- Microsoft.Win32.Registry
    - 확장자별 연결 프로그램 추정
- P/Invoke
    - shell32.dll의 SHFileOperation
    - 파일을 완전 삭제하지 않고 휴지통으로 이동
- WPF Media
    - 이미지 미리보기
    - Brush/Color 기반 위험도 표시
- WPF Media3D
    - OBJ/STL 3D 파일 미리보기
    - Viewport3D, Model3DGroup, MeshGeometry3D
- Task, async/await, CancellationToken
    - 긴 스캔 작업 비동기 처리
    - 폴더 변경 시 이전 작업 취소
- Parallel.ForEach
    - 저장공간 분석 병렬 처리

**전체 구조**

프로젝트는 대략 이렇게 나뉩니다.

text

`models/
  화면과 서비스가 공유하는 데이터 모델

services/
  파일 스캔, 저장공간 분석, 설정 저장, 오류 기록, 휴지통 이동 등 기능 로직

helpers/
  Command, Converter, 대량 ObservableCollection

viewModels/
  MainViewModel.cs
  UI 상태와 명령을 관리하는 중심 ViewModel

MainWindow.xaml
  실제 WPF 화면 구성`

**핵심 기능**

1. 파일/폴더 스캔FileScanner.cs가 담당합니다.
    - 폴더를 안전하게 순회
    - 프로젝트 폴더 감지
    - 파일 크기, 수정일, 접근일 확인
    - 파일 사용 중 여부 확인
    - 위험도 점수 계산
    - 생성/캐시 폴더를 제외해 성능 저하를 줄임

프로젝트 폴더 감지 기준은 .git, package.json, .sln, .csproj, go.mod, Cargo.toml 같은 마커 파일입니다.

1. 삭제 후보 점수 계산FileScanner.cs (line 450)의 ComputeRisk()가 담당합니다.

기본 점수는 50이고, 낮을수록 삭제 후보입니다.

예시:

- 오래 안 쓴 파일: 점수 감소
- .tmp, .log, .bak, .cache: 점수 감소
- 문서/코드 파일: 점수 증가
- Windows/Program Files 경로: 점수 증가
- 사용 중 파일: 점수 증가
1. 사용자 삭제 이력 기반 추천RecommendationProfileService.cs가 담당합니다.

사용자가 실제로 삭제한 파일 확장자를 로컬 JSON에 기록하고, 다음 스캔 때 같은 확장자의 점수를 낮춰 삭제 후보에 더 잘 들어오게 합니다.

저장 위치는:

text

`%LocalAppData%\DownCleaner\recommendations.json`

1. 설정 저장SettingsService.cs가 담당합니다.

저장되는 설정 예시:

- 자동 선택 기준 점수
- 기본 스캔 모드
- 하위 폴더 포함 여부
- 삭제 이력 기반 추천 사용 여부
- 자세한 진행 상태 표시 여부

저장 위치:

text

`%LocalAppData%\DownCleaner\settings.json`

1. 저장공간 분석StorageService.cs가 담당합니다.
    - 드라이브 사용량 조회
    - 빠른 접근 폴더 분석
    - 폴더별 크기 계산
    - Parallel.ForEach로 병렬 분석
    - 차트형 ProgressBar UI로 표시
2. 파일 미리보기MainViewModel.cs에 구현되어 있습니다.

현재 지원:

- 이미지: .jpg, .png, .bmp, .gif, .webp 등
- 텍스트: .txt, .log, .md, .json, .xml, .cs, .xaml, .js, .py 등
- 3D: .obj, .stl

FBX, GLTF, GLB 같은 포맷은 현재 외부 3D 로더가 없어서 안내 메시지만 표시합니다.

1. 휴지통 이동RecycleBinService.cs가 담당합니다.

SHFileOperation을 P/Invoke로 호출해서 파일을 휴지통으로 보냅니다. 완전 삭제가 아니라 복구 가능한 삭제 방식입니다.

1. 오류 추적ErrorLogService.cs와 App.xaml.cs가 담당합니다.
    - UI 스레드 예외
    - 비UI 스레드 예외
    - 스캔/미리보기/설정 저장 중 오류

저장 위치:

text

`%LocalAppData%\DownCleaner\error.log
%LocalAppData%\DownCleaner\recent-errors.json`

**UI 구성**

MainWindow.xaml 기준으로 탭 구조는 다음과 같습니다.

- 파일 탐색기
    - 폴더 트리
    - 스캔 결과 트리
    - 파일 미리보기
- 삭제 목록
    - 삭제 예정 파일을 폴더 트리 형태로 표시
    - 선택 항목 휴지통 이동
    - 미리보기
- 저장공간
    - 드라이브 사용량
    - 위험 파일 비율
    - 큰 폴더 분석
- 설정
    - 자동 선택 기준
    - 스캔 모드
    - 추천 옵션
    - 최근 오류 이력

**아키텍처 특징**

이 프로젝트는 전형적인 WPF MVVM 구조입니다.

- View: MainWindow.xaml
- ViewModel: MainViewModel.cs
- Model: models/*.cs
- Service: services/*.cs
- Helper: helpers/*.cs

장점은 기능별 분리가 비교적 명확하다는 점입니다.

파일 스캔, 저장공간 분석, 추천 프로필, 오류 기록 같은 기능이 각각 서비스로 분리되어 있습니다.

다만 현재 MainViewModel.cs가 상당히 커졌습니다. 미리보기 로직, 삭제목록 트리 구성, 설정 처리, 스캔 처리, 저장공간 분석까지 모두 들어 있어서 앞으로는 다음처럼 나누면 더 좋아집니다.

- PreviewService
- CleanupTreeBuilder
- DeleteListService
- ScanCoordinator
- StorageAnalysisViewModel

요약하면, DownCleaner는 **외부 패키지 없이 .NET 8 + WPF + Windows API만으로 만든 로컬 파일 정리/삭제 추천 도구**입니다. 핵심 기술은 MVVM, 비동기 파일 스캔, 위험도 점수 기반 추천, 로컬 JSON 설정 저장, WPF 데이터 바인딩, Windows 휴지통 API, WPF 3D 미리보기입니다.
