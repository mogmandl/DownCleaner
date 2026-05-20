# DownCleaner
다운로드 및 여러 이외의 폴더를 분석하고 삭제목록을 분류하여 폴더를 정리해주는 클리너 프로그램입니다.

이 프로젝트는 **WPF 기반 Windows 데스크톱 파일 정리 프로그램**입니다. 이름은 DownCleaner이고, 네임스페이스는 FileCleaner로 되어 있습니다. 목적은 폴더/파일을 스캔해서 삭제 후보를 추천하고, 저장공간을 분석하고, 사용자가 고른 파일을 휴지통으로 보내는 것입니다.

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
    - 빠른 스캔 모드에서 무거운 파일 점유 검사를 나중으로 미룸

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
