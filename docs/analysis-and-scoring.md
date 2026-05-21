# DownCleaner 분석 및 점수 시스템 문서

이 문서는 현재 코드 기준으로 DownCleaner가 파일, 폴더, 프로젝트, 저장공간을 어떻게 분석하고 삭제 후보 점수를 어떻게 계산하는지 정리한 문서입니다.

관련 구현 위치:

- `services/FileScanner.cs`: 파일 스캔, 위험도 점수 계산, 프로젝트 폴더 감지
- `services/RecommendationProfileService.cs`: 사용자 삭제 이력 기반 추천 보정
- `services/StorageService.cs`: 드라이브/폴더 저장공간 분석
- `services/FileUsageService.cs`: 파일 사용 중 여부 및 연결 프로그램 추정
- `viewModels/MainViewModel.cs`: 분석 결과 UI 반영, 삭제 후보 선택, 사전 분석 캐시
- `models/FileItem.cs`: 파일 분석 결과 모델
- `models/AppSettings.cs`: 분석/추천 관련 사용자 설정

## 1. 전체 분석 흐름

DownCleaner의 분석은 크게 네 흐름으로 나뉩니다.

1. 파일 스캔
   - 지정한 폴더에서 파일 목록을 수집합니다.
   - 파일 크기, 마지막 수정일, 마지막 접근일, 확장자, 경로, 사용 중 여부, 연결 프로그램을 읽습니다.
   - 각 파일에 위험도 점수와 위험도 등급을 부여합니다.

2. 삭제 후보 추천
   - 위험도 점수가 낮은 파일을 삭제 후보로 봅니다.
   - 기본 자동 선택 기준은 `RiskScore < AutoSelectRiskThreshold`입니다.
   - 기본 임계값은 `40`입니다.
   - 파일이 현재 사용 중이면 자동 삭제 후보에서 제외됩니다.

3. 사용자 패턴 기반 보정
   - 사용자가 실제로 삭제한 파일 확장자를 로컬에 기록합니다.
   - 자주 삭제한 확장자는 다음 분석에서 점수를 더 낮춰 삭제 후보에 더 잘 올라오게 합니다.
   - 네트워크나 외부 AI 없이 로컬 JSON 파일만 사용합니다.

4. 저장공간/프로젝트 분석
   - 드라이브 사용량, 큰 폴더, 위험 파일 비율, 프로젝트 폴더 분류를 별도 탭에서 보여줍니다.
   - 프로젝트 폴더는 `.csproj`, `package.json`, `Cargo.toml`, Unity 구조 등 로컬 마커 기반으로 감지합니다.

## 2. 파일 스캔 방식

파일 스캔은 `FileScanner.ScanFilesAsync()`에서 수행합니다.

입력:

- `folderPath`: 분석할 폴더
- `includeSubfolders`: 하위 폴더 포함 여부
- `progress`: 진행 상태 메시지 콜백
- `CancellationToken`: 취소 처리

수집하는 파일 정보:

- 파일명
- 전체 경로
- 파일 크기
- 마지막 수정 시간
- 마지막 접근 시간
- 위험도 점수
- 위험도 등급
- 위험도 산정 이유
- 연결 프로그램
- 현재 사용 중 여부
- 사용 여부 검사를 뒤로 미뤘는지 여부

스캔 중 접근할 수 없는 파일이나 폴더는 앱을 중단하지 않고 건너뜁니다.

## 3. 빠른 스캔 최적화

대량 파일을 분석할 때 모든 파일에 대해 사용 중 여부와 레지스트리 기반 연결 프로그램을 상세히 확인하면 느려질 수 있습니다. 그래서 스캔 도중 다음 조건을 만족하면 빠른 스캔 모드로 전환합니다.

전환 조건:

- 처리한 파일 수가 `250개 이상`
- 스캔 경과 시간이 `2초 이상`

빠른 스캔 모드가 켜지면:

- 파일 사용 중 여부 검사를 즉시 하지 않습니다.
- `IsInUse`는 일단 `false`로 둡니다.
- `NeedsUsageCheck`를 `true`로 표시합니다.
- 연결 프로그램은 빠른 확장자 매핑만 사용합니다.
- 위험도 이유에 `빠른 스캔: 사용 여부 미확인`이 추가됩니다.

이후 사용자가 파일을 선택하거나 실제 삭제를 시도할 때 `TryResolveDeferredMetadata()`로 미뤄둔 사용 중 여부를 다시 확인합니다.

## 4. 위험도 점수 개념

DownCleaner의 `RiskScore`는 일반적인 보안 위험 점수가 아니라, “삭제해도 괜찮을 가능성”을 판단하기 위한 보존 점수에 가깝습니다.

중요한 해석:

- 점수가 낮을수록 삭제 후보에 가깝습니다.
- 점수가 높을수록 보존하거나 주의해야 합니다.
- 기본 점수는 `50`에서 시작합니다.
- 모든 보정 후 `0 ~ 100` 사이로 제한됩니다.

등급 기준:

| 점수 범위 | 등급 | 의미 |
| --- | --- | --- |
| `0 ~ 39` | 낮음 (삭제 후보) | 자동 추천 대상이 되기 쉬움 |
| `40 ~ 69` | 중간 | 사용자가 확인 후 판단 |
| `70 ~ 100` | 높음 (삭제 주의) | 삭제 전 주의 필요 |

자동 선택 기준:

```text
RiskScore < Settings.AutoSelectRiskThreshold
&& IsInUse == false
```

기본값:

```text
AutoSelectRiskThreshold = 40
```

## 5. 위험도 점수 계산 공식

위험도 점수 계산은 `FileScanner.ComputeRisk()`에서 수행합니다.

초기값:

```text
score = 50
```

### 5.1 마지막 접근일 기준

파일의 마지막 접근 시간이 오래될수록 삭제 후보에 가까워집니다.

| 조건 | 점수 변화 | 이유 |
| --- | ---: | --- |
| 마지막 접근 후 365일 이상 | `-30` | 오래 사용 안 함 |
| 마지막 접근 후 180일 이상 | `-22` | 장기간 미사용 |
| 마지막 접근 후 60일 이상 | `-10` | 최근 사용 이력 적음 |
| 60일 미만 | `+14` | 최근 사용 파일 |

예시:

```text
기본 50
마지막 접근 400일 전: -30
결과 20
```

이 파일은 다른 보호 요소가 없다면 `낮음 (삭제 후보)`가 됩니다.

### 5.2 파일 크기 기준

큰 파일은 저장공간을 많이 차지하지만, 무조건 삭제 대상이라기보다 사용자가 보존 여부를 확인해야 할 가능성이 높다고 판단합니다. 그래서 점수를 올립니다.

| 조건 | 점수 변화 | 이유 |
| --- | ---: | --- |
| 1GB 이상 | `+18` | 대용량 파일 |
| 300MB 이상 | `+10` | 용량 큼 |

예시:

```text
기본 50
오래 사용 안 함: -30
1GB 이상: +18
결과 38
```

오래 안 쓴 파일이라도 대용량이면 사용자가 확인할 가치가 있으므로 점수가 일부 올라갑니다.

### 5.3 삭제 후보 확장자 기준

다음 확장자는 임시 파일, 로그, 백업, 미완료 다운로드 파일로 보고 삭제 후보 쪽으로 점수를 낮춥니다.

대상 확장자:

```text
.tmp
.temp
.log
.bak
.old
.dmp
.cache
.crdownload
.part
```

점수 변화:

```text
-20
```

이유:

```text
임시/로그 계열 확장자
```

### 5.4 중요 확장자 기준

다음 확장자는 문서, 코드, 프로젝트 파일로 보고 보존 쪽으로 점수를 올립니다.

대상 확장자:

```text
.doc
.docx
.xls
.xlsx
.ppt
.pptx
.pdf
.txt
.md
.cs
.java
.py
.js
.ts
.cpp
.h
.sql
.ps1
.csproj
.sln
```

점수 변화:

```text
+20
```

이유:

```text
문서/코드 중요 확장자
```

### 5.5 시스템 경로 기준

다음 경로에 포함된 파일은 시스템이나 설치 프로그램과 관련될 가능성이 있어 점수를 크게 올립니다.

조건:

```text
경로에 \Windows\ 포함
또는 경로에 \Program Files 포함
```

점수 변화:

```text
+30
```

이유:

```text
시스템 경로
```

### 5.6 다운로드 폴더 임시파일 기준

다운로드 폴더에 있는 임시/부분 다운로드 파일은 삭제 후보 가능성을 더 높게 봅니다.

조건:

```text
경로에 \Users\ 포함
경로에 \Downloads\ 포함
확장자가 삭제 후보 확장자에 포함됨
```

점수 변화:

```text
-8
```

이유:

```text
다운로드 폴더 임시파일
```

### 5.7 사용 중 파일 기준

현재 다른 프로그램이 파일을 잡고 있으면 삭제 주의 대상으로 봅니다.

조건:

```text
IsInUse == true
```

점수 변화:

```text
+25
```

이유:

```text
현재 사용 중
```

## 6. 점수 계산 예시

### 예시 1: 오래된 로그 파일

조건:

- `app.log`
- 마지막 접근 400일 전
- 확장자 `.log`
- 시스템 경로 아님
- 사용 중 아님

계산:

```text
기본 점수: 50
오래 사용 안 함: -30
임시/로그 계열 확장자: -20
최종 점수: 0
```

결과:

```text
낮음 (삭제 후보)
```

### 예시 2: 최근 수정한 TypeScript 파일

조건:

- `MainView.tsx`
- 마지막 접근 10일 전
- 코드 파일 계열
- 사용 중 아님

계산:

```text
기본 점수: 50
최근 사용 파일: +14
문서/코드 중요 확장자: +20
최종 점수: 84
```

결과:

```text
높음 (삭제 주의)
```

참고:

- 현재 중요 확장자 목록에는 `.ts`는 포함되어 있지만 `.tsx`는 직접 포함되어 있지 않습니다.
- `.tsx`는 텍스트 미리보기 쪽에서는 지원되지만, 점수 보호 확장자 목록에는 별도 등록되어 있지 않습니다.
- 필요하면 `.tsx`, `.jsx`, `.env`, `.json`, `.yaml` 등을 중요 확장자에 추가할 수 있습니다.

### 예시 3: 다운로드 폴더의 미완료 다운로드 파일

조건:

- `movie.part`
- 다운로드 폴더 안에 있음
- 마지막 접근 90일 전
- 확장자 `.part`

계산:

```text
기본 점수: 50
최근 사용 이력 적음: -10
임시/로그 계열 확장자: -20
다운로드 폴더 임시파일: -8
최종 점수: 12
```

결과:

```text
낮음 (삭제 후보)
```

### 예시 4: Program Files 안의 오래된 로그 파일

조건:

- `C:\Program Files\App\debug.log`
- 마지막 접근 400일 전
- 확장자 `.log`
- 시스템/프로그램 경로

계산:

```text
기본 점수: 50
오래 사용 안 함: -30
임시/로그 계열 확장자: -20
시스템 경로: +30
최종 점수: 30
```

결과:

```text
낮음 (삭제 후보)
```

주의:

- 현재 규칙상 시스템 경로 보정이 있어도 오래된 로그 파일은 삭제 후보가 될 수 있습니다.
- 실제 삭제 전에는 사용자가 경로와 파일명을 확인하는 것이 안전합니다.

## 7. 위험도 이유 표시 방식

`RiskReason`은 점수 계산 중 추가된 이유를 최대 3개까지 표시합니다.

예시:

```text
오래 사용 안 함, 임시/로그 계열 확장자, 다운로드 폴더 임시파일
```

이유가 하나도 없으면 다음 기본 문구를 사용합니다.

```text
기본 규칙 기반 평가
```

## 8. 사용자 삭제 이력 기반 추천 보정

사용자가 삭제한 파일은 `RecommendationProfileService.RecordDeletedFiles()`를 통해 확장자별 삭제 횟수로 기록됩니다.

저장 위치:

```text
%LocalAppData%\DownCleaner\recommendations.json
```

저장 데이터:

- 확장자별 삭제 횟수
- 마지막 업데이트 시간

확장자가 없는 파일은 다음 키로 기록됩니다.

```text
(no extension)
```

삭제 횟수는 확장자별 최대 `999`까지 누적됩니다.

## 9. 추천 보정 공식

사용자 삭제 이력 기반 보정은 `ApplyLearnedPriority()`에서 수행됩니다.

조건:

```text
Settings.PreferLearnedRecommendations == true
```

보정 공식:

```text
adjustment = min(18, 4 + deletedCount * 2)
RiskScore = clamp(RiskScore - adjustment, 0, 100)
```

최대 보정값:

```text
18점
```

예시:

| 해당 확장자 삭제 횟수 | 보정값 |
| ---: | ---: |
| 1회 | 6점 감소 |
| 2회 | 8점 감소 |
| 3회 | 10점 감소 |
| 5회 | 14점 감소 |
| 7회 이상 | 18점 감소 |

보정 후 점수에 따라 위험도 등급도 다시 계산됩니다.

주의:

- 이 기능은 머신러닝이나 AI 모델이 아닙니다.
- 로컬 삭제 이력을 이용한 규칙/통계 기반 추천 보정입니다.
- 네트워크 연결이 필요 없습니다.

## 10. 삭제 후보 선택 기준

삭제 후보 선택 버튼은 `SelectDangerous()`에서 동작합니다.

선택 조건:

```text
file.RiskScore < Settings.AutoSelectRiskThreshold
&& !file.IsInUse
```

기본 설정에서는 다음과 같습니다.

```text
RiskScore < 40
&& 사용 중 아님
```

삭제 목록 추가 버튼은 현재 파일 목록에서 `IsSelected == true`인 파일만 삭제 목록에 넣습니다.

중복 방지:

- 삭제 목록에 이미 같은 경로가 있으면 다시 추가하지 않습니다.
- 경로 비교는 대소문자를 구분하지 않습니다.

## 11. 실제 삭제 전 보호 로직

삭제는 바로 영구 삭제하지 않고 휴지통으로 이동합니다.

흐름:

1. 삭제 목록에서 체크된 파일을 수집합니다.
2. 사용자에게 확인 메시지를 보여줍니다.
3. `NeedsUsageCheck == true`인 파일은 사용 중 여부를 다시 확인합니다.
4. 사용 중인 파일은 삭제 대상에서 제외합니다.
5. 나머지 파일을 `RecycleBinService.SendToRecycleBin()`으로 휴지통 이동합니다.
6. 삭제 성공 시 추천 프로필에 확장자 삭제 이력을 기록합니다.

즉 빠른 스캔에서 사용 중 여부를 미뤘더라도 실제 삭제 직전에는 다시 검사합니다.

## 12. 사전 분석 캐시

앱 시작 시 `App.xaml.cs`에서 로딩 화면을 띄운 뒤 `PreloadFavoriteFoldersAsync()`를 실행합니다.

사전 분석 대상:

- 바탕화면
- 문서
- 다운로드
- 사진
- 음악
- 비디오

이 목록은 `StorageService.GetQuickAccessFolders()`에서 가져옵니다.

사전 분석 결과는 메모리의 `_preloadedScans`에 저장됩니다.

```text
Dictionary<string, List<FileItem>>
```

폴더 선택 시 동작:

1. 선택한 폴더 경로가 캐시에 정확히 있으면 해당 결과를 즉시 표시합니다.
2. 선택한 폴더가 사전 분석된 상위 폴더의 하위 폴더이면, 상위 캐시에서 해당 경로 아래 파일만 필터링해서 표시합니다.
3. 캐시에 없으면 해당 폴더를 새로 스캔하고 결과를 캐시에 저장합니다.

이 구조 덕분에 자주 쓰는 기본 폴더는 앱 시작 후 폴더 진입 시 로딩이 줄어듭니다.

## 13. 스캔 모드

현재 설정 가능한 스캔 모드는 두 가지입니다.

| 모드 | 동작 |
| --- | --- |
| `Detailed` | 설정의 `IncludeSubfolders` 값에 따라 하위 폴더 포함 |
| `Quick` | 하위 폴더를 포함하지 않고 현재 폴더만 스캔 |

이전의 `Smart` 모드는 사전 분석 구조와 겹쳐 제거되었습니다.

기존 설정 파일에 `Smart`가 남아 있으면 `Detailed`로 보정됩니다.

## 14. 저장공간 분석

저장공간 분석은 `StorageService.AnalyzeFolderSizesAsync()`에서 수행합니다.

분석 대상:

- 빠른 접근 폴더
- 사용자가 추가한 루트 폴더

분석 방식:

1. 중복 경로를 제거합니다.
2. 각 폴더의 전체 파일 크기를 계산합니다.
3. 결과를 크기 내림차순으로 정렬합니다.
4. 가장 큰 폴더를 100%로 두고 상대 비율을 계산합니다.

병렬 처리:

```text
MaxDegreeOfParallelism = clamp(Environment.ProcessorCount / 2, 2, 6)
```

즉 CPU 코어 수의 절반 정도를 쓰되, 최소 2개/최대 6개 작업으로 제한합니다.

폴더 크기 계산 시:

- 접근 불가 파일은 건너뜁니다.
- 접근 불가 폴더는 건너뜁니다.
- 재분석 중 파일이 사라지거나 잠겨도 앱이 중단되지 않습니다.
- `ReparsePoint` 속성이 있는 폴더는 건너뜁니다.

`ReparsePoint`를 건너뛰는 이유:

- 심볼릭 링크
- junction
- OneDrive/시스템 특수 링크

이런 경로는 순환 참조나 중복 계산을 만들 수 있습니다.

## 15. 드라이브 사용량 분석

드라이브 정보는 `StorageService.GetDriveInfo()`에서 가져옵니다.

대상:

- `DriveInfo.GetDrives()`에서 얻은 드라이브
- `IsReady == true`인 드라이브만 포함

수집 정보:

- 드라이브 이름
- 볼륨 라벨
- 전체 용량
- 사용 가능 용량
- 사용량
- 사용률

준비되지 않은 드라이브나 접근 오류가 나는 드라이브는 무시합니다.

## 16. 위험 파일 비율 시각화

위험 파일 비율은 현재 파일 목록 기준으로 계산합니다.

분류:

| 라벨 | 조건 |
| --- | --- |
| 삭제 후보 | `RiskScore < AutoSelectRiskThreshold && !IsInUse` |
| 주의 | `RiskScore >= AutoSelectRiskThreshold && RiskScore < 70` |
| 보존 권장 | `RiskScore >= 70 || IsInUse` |

각 항목의 퍼센트는 현재 파일 목록 전체 개수 대비 비율입니다.

## 17. 프로젝트 폴더 분석

프로젝트 폴더 분석은 `FileScanner.DetectProjectType()`과 `FindProjectFolders()`가 담당합니다.

프로젝트 타입 감지 기준:

| 타입 | 기준 |
| --- | --- |
| Unity | `Assets` 폴더 + `ProjectSettings` 폴더 + `Packages/manifest.json` |
| NodeJS | `package.json` |
| Maven | `pom.xml` |
| Rust | `Cargo.toml` |
| Python | `requirements.txt` 또는 `setup.py` |
| CMake | `CMakeLists.txt` |
| Make | `Makefile` |
| Gradle | `build.gradle` |
| PHP | `composer.json` |
| Ruby | `Gemfile` |
| Go | `go.mod` |
| Flutter | `pubspec.yaml` |
| VisualStudio | `.sln`, `.csproj`, `.vcxproj`, `.vbproj` |
| Git | `.git` 폴더 |

감지 우선순위:

1. Unity 프로젝트 루트
2. 무시해야 하는 내부 폴더인지 확인
3. 프로젝트 마커 파일
4. 솔루션/프로젝트 확장자
5. `.git` 폴더

`.git`을 마지막에 검사하는 이유:

- 대부분의 프로젝트는 Git 저장소이기도 합니다.
- `.git`을 먼저 보면 NodeJS, Unity, .NET 프로젝트가 모두 Git으로만 분류될 수 있습니다.

## 18. 프로젝트 탐색 범위

프로젝트 폴더 탐색은 전체 디스크를 무한정 뒤지지 않습니다.

기본 제한:

```text
maxDepth = 5
maxFolders = 1500
```

즉 빠른 접근 폴더 또는 추가된 루트 폴더 아래에서 최대 5단계, 최대 1500개 폴더까지만 검사합니다.

탐색 제외 폴더:

```text
.git
.vs
.vscode
bin
obj
node_modules
packages
.next
dist
build
target
__pycache__
.venv
venv
Library
PackageCache
Temp
Logs
UserSettings
```

Unity 관련 제외 이유:

- Unity의 `Library/PackageCache` 내부에는 `package.json`이 많습니다.
- 이것은 사용자가 직접 관리하는 Node.js 프로젝트가 아니라 Unity 패키지 캐시입니다.
- 그래서 해당 내부 폴더는 프로젝트 후보에서 제외합니다.

## 19. 파일 사용 중 여부 검사

파일 사용 중 여부는 `FileUsageService.IsFileInUse()`에서 확인합니다.

방식:

```csharp
new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
```

해석:

- 파일을 단독 읽기/쓰기 모드로 열 수 있으면 사용 중이 아니라고 봅니다.
- `IOException`이 발생하면 다른 프로세스가 잡고 있다고 보고 사용 중으로 판단합니다.
- 그 외 예외는 사용 중이 아닌 것으로 처리합니다.

주의:

- 이 방식은 실제 OS 잠금 상태를 기반으로 합니다.
- 모든 사용 상태를 완벽하게 의미하지는 않습니다.
- 권한 문제나 특수 파일은 예외 처리로 인해 사용 중이 아닌 것으로 보일 수 있습니다.

## 20. 연결 프로그램 추정

연결 프로그램은 `FileUsageService.GetAssociatedProgram()`에서 추정합니다.

우선순위:

1. 코드에 등록된 알려진 확장자 매핑
2. Windows Registry의 `HKEY_CLASSES_ROOT`
3. 알 수 없으면 `{확장자} 파일`

빠른 스캔 모드에서는 `GetAssociatedProgramFast()`를 사용합니다.

빠른 모드:

- 알려진 확장자 매핑만 사용
- 레지스트리 조회 생략
- 대량 파일 스캔 속도 개선

## 21. 설정 저장

설정은 `SettingsService`가 JSON으로 저장합니다.

저장 위치:

```text
%LocalAppData%\DownCleaner\settings.json
```

현재 설정 항목:

| 설정 | 기본값 | 설명 |
| --- | --- | --- |
| `AutoSelectRiskThreshold` | `40` | 자동 선택 기준 점수 |
| `DefaultScanMode` | `Detailed` | 기본 스캔 모드 |
| `IncludeSubfolders` | `true` | Detailed 모드에서 하위 폴더 포함 여부 |
| `ShowDetailedProgress` | `true` | 자세한 진행 상태 표시 여부 |
| `PreferLearnedRecommendations` | `true` | 삭제 이력 기반 추천 보정 사용 여부 |

설정은 변경 즉시 저장됩니다.

## 22. 오류 추적

오류는 `ErrorLogService`가 관리합니다.

저장 위치:

```text
%LocalAppData%\DownCleaner\error.log
%LocalAppData%\DownCleaner\recent-errors.json
```

기록 내용:

- 발생 시간
- 오류 발생 위치
- 예외 타입
- 메시지
- 전체 스택 트레이스

최근 오류 이력:

```text
최대 30개
```

로그 기록 중 오류가 발생해도 앱이 중단되지 않도록 logger 내부 예외는 삼킵니다.

## 23. 현재 시스템의 한계

현재 DownCleaner의 추천은 AI 모델이 아니라 규칙 기반입니다.

한계:

- 파일 내용의 의미를 이해하지 않습니다.
- 같은 확장자라도 실제 중요도를 구분하지 못합니다.
- 마지막 접근 시간이 OS 설정이나 파일 시스템 정책에 따라 정확하지 않을 수 있습니다.
- 프로젝트 폴더 감지는 마커 파일 기반이라 특이한 프로젝트 구조는 놓칠 수 있습니다.
- 빠른 스캔 중에는 일부 파일의 사용 중 여부가 나중으로 미뤄집니다.

장점:

- 네트워크가 필요 없습니다.
- 외부 서버로 파일 정보를 보내지 않습니다.
- 동작 기준이 설명 가능하고 예측 가능합니다.
- 사용자의 실제 삭제 이력을 로컬에서만 반영합니다.

## 24. 개선 후보

점수 기능 개선:

- `.tsx`, `.jsx`, `.json`, `.yaml`, `.env`, `.config` 등 보호 확장자 추가 여부 검토
- 시스템 경로 파일은 삭제 후보에서 완전히 제외하는 정책 검토
- 파일 크기 보정 방향 재검토
  - 현재는 큰 파일을 삭제 주의 쪽으로 올림
  - 저장공간 확보 목적이면 오래된 대용량 파일을 별도 후보로 분류할 수도 있음

프로젝트 분석 개선:

- Unity 프로젝트 내부 `Assets`, `Packages`, `ProjectSettings`별 용량 분석
- Unreal 프로젝트 `.uproject` 감지 추가
- Godot 프로젝트 `project.godot` 감지 추가
- Android 프로젝트 `settings.gradle`, `AndroidManifest.xml` 조합 감지

성능 개선:

- 프로젝트 폴더 탐색 결과 캐시
- 저장공간 분석 결과 캐시
- 사전 분석 진행률을 퍼센트 기반으로 개선

안전성 개선:

- 시스템 경로 파일 자동 선택 금지
- 프로젝트 소스 파일 자동 선택 금지
- 삭제 전 후보 요약을 확장자/폴더별로 표시
