# CLAUDE.md

이 문서는 Claude Code가 이 저장소에서 작업할 때 따라야 할 아키텍처 원칙, 코딩 규칙 및 주의사항을 정의합니다.

## 프로젝트 개요

**DataTable**은 CSV 기반 게임 데이터를 강타입 C# 클래스로 자동 생성하고, 바이너리 직렬화 및 AES 암호화를 통해 런타임에서 사용할 수 있도록 하는 Unity Package입니다.

기본 데이터 흐름은 다음과 같습니다.

```text
CSV
 ├─ Schema → C# Class Generation
 └─ Data   → Binary Serialization
                    ↓
              AES Encryption
                    ↓
             *_encry.bytes
                    ↓
               IDataLoader
                    ↓
              TableManager
                    ↓
          DynamicDataObject<T>
```

* Unity 2022.3 이상
* C#
* Unity Package Manager 지원
* Runtime / Editor Assembly 분리

가능하면 Unity/.NET 기본 API만 사용하고, 명확한 이유 없이 외부 의존성을 추가하지 않습니다.

---

# 핵심 아키텍처 원칙

## 1. 패키지 독립성 유지

DataTable은 특정 프로젝트의 Application Architecture에 의존하지 않아야 합니다.

다음 의존성을 임의로 추가하지 않습니다.

* `UnityCore`
* Custom Singleton
* `MonoBehaviour` 기반 Manager
* Service Locator
* DI Container
* UniTask
* Addressables
* 외부 Serialization 라이브러리

`TableManager`는 일반 C# 클래스로 유지합니다.

Singleton이나 DI가 필요한 경우 사용하는 프로젝트에서 결정합니다.

```csharp
public static class Tables
{
    public static TableManager Instance { get; } =
        new(new ResourcesDataLoader());
}
```

패키지 자체가 특정 생명주기 관리 방식을 강제하지 않습니다.

---

## 2. Runtime / Editor 경계 유지

`Runtime` 코드에서는 다음 Editor API를 참조하면 안 됩니다.

```text
UnityEditor
AssetDatabase
Selection
SessionState
EditorApplication
EditorUtility
```

CSV Parsing, Code Generation, Binary 생성 등의 Authoring 기능은 `Editor` 영역에 유지합니다.

런타임에는 생성된 데이터를 로드하고 사용하는 데 필요한 기능만 포함합니다.

---

## 3. 데이터 로딩은 교체 가능하게 유지

`TableManager`는 실제 Asset Loading 방식에 직접 의존하지 않습니다.

```text
TableManager
     │
 IDataLoader
     │
 ┌───┼─────────────┐
 ▼   ▼             ▼
Resources    Addressables    CDN
```

기본 구현은 `ResourcesDataLoader`입니다.

```csharp
var tableManager =
    new TableManager(new ResourcesDataLoader());
```

다른 로딩 방식이 필요한 경우 `IDataLoader`를 구현하여 교체합니다.

`TableManager` 내부에 `Resources.Load`, Addressables, AssetBundle 또는 CDN 로딩 코드를 직접 추가하지 않습니다.

단, Editor에서 이미 선택된 `TextAsset`을 처리하는 경우에는 불필요하게 `IDataLoader`를 거치지 않고 Asset 자체를 사용하는 것을 우선합니다.

---

# CSV / Schema 규칙

CSV 첫 번째 행은 Schema를 정의합니다.

```text
id:int|name:string|attack:float
1001|Sword|10.5
1002|GreatSword|25.0
```

Schema 형식:

```text
FieldName:Type
```

`#`으로 시작하는 컬럼은 생성 대상에서 제외합니다.

```text
id:int|name:string|#comment:string|attack:float
```

타입 관련 코드를 변경할 때는 반드시 전체 변환 과정의 호환성을 확인합니다.

```text
CSV Parsing
     ↓
Generated C# Type
     ↓
Binary Write
     ↓
Binary Read
     ↓
CSV Restore
```

새 타입을 추가할 경우 한 단계에만 구현하지 않습니다.

`TypeParser`, Code Generation, Serialization, Deserialization, CSV Restore가 서로 동일한 타입을 처리해야 합니다.

---

# Code Generation

생성되는 테이블 클래스는 기본적으로 다음 구조를 사용합니다.

```csharp
[SerializeField]
private int _id;

public int id => _id;
```

각 클래스는 자신의 Binary Serialization 코드를 가집니다.

```csharp
public void Write(BinaryWriter bw)
public void Read(BinaryReader br)
```

레코드 단위 Serialization을 다시 완전한 Reflection 기반 구조로 변경하지 않습니다.

Reflection은 동적인 Type 탐색이나 Generic Method 호출 등 필요한 영역에서만 사용합니다.

---

## Generated Code 수정 규칙

생성된 C# 파일은 직접 관리하는 소스가 아닙니다.

문제가 발생하면:

```text
Generated File 직접 수정
        X

Generator 수정
        ↓
Code 재생성
        O
```

방식으로 처리합니다.

`DynamicClassGenerator.Generate()`는 생성 결과가 기존 파일과 동일하면 파일을 다시 작성하지 않아야 합니다.

불필요한 파일 변경은 Unity Script Compilation을 발생시키므로 피합니다.

---

# 자동 컴파일 및 생성 재개

Schema가 변경되어 C# 코드가 변경되면 Unity Script Compilation과 Domain Reload가 발생합니다.

현재 생성 흐름은 다음과 같습니다.

```text
CSV
 ↓
Generated Code 비교
 ↓
Code Changed?
 ├─ No
 │   ↓
 │ Binary 생성
 │
 └─ Yes
     ↓
 Pending State 저장
     ↓
 AssetDatabase.Refresh()
     ↓
 Unity Compilation
     ↓
 Domain Reload
     ↓
 작업 자동 재개
     ↓
 Binary 생성
```

이 흐름을 유지합니다.

관련 클래스:

```text
TableGenerationState
TableGenerationResumeHandler
```

컴파일이 끝날 때까지 Busy Waiting 하는 방식으로 변경하지 않습니다.

Domain Reload 이전의 `Selection` 객체 자체를 보존하려 하지 말고 Asset Path와 같이 재사용 가능한 값을 저장합니다.

---

# Binary Format

Binary Table의 기본 구조는 다음과 같습니다.

```text
Record Count
ID Field Name
Record 0
Record 1
Record 2
...
```

ID 필드는 Reflection Field Index가 아니라 **Field Name**으로 저장합니다.

다음과 같은 방식으로 되돌리지 않습니다.

```text
ID Field Index
```

Reflection에서 반환되는 Member 순서는 Serialization Contract로 사용할 수 없습니다.

Binary Format을 변경할 경우 다음 코드를 함께 확인합니다.

* Serialization
* `DeserializeUtil`
* CSV Restore
* Runtime Loading
* Tests
* `CHANGELOG.md`

Backward Compatibility를 제공하지 않는 변경이라면 기존 `.bytes` 파일을 다시 생성해야 한다는 점을 명시합니다.

---

# Serialization 책임

레코드의 Binary 데이터는 생성된 클래스가 담당합니다.

```csharp
record.Write(BinaryWriter);
record.Read(BinaryReader);
```

Table 단위 복원은 `DeserializeUtil`이 담당합니다.

`DeserializeUtil`의 주요 책임:

* Record Count 읽기
* ID Field Name 읽기
* Record 생성
* `List<T>` 구성
* `Map` 구성

`TableManager` 등에 별도의 Binary Deserialization 구현을 복제하지 않습니다.

Binary Format에 대한 지식은 가능한 한 한 곳에서 관리합니다.

---

# TableManager 책임

`TableManager`의 책임:

* `[TableData]` 타입 탐색
* `IDataLoader`를 통한 데이터 로드
* 데이터 복호화
* 데이터 역직렬화
* 로드된 Table 관리
* `Get<T>()`
* `UnloadAll()`

`TableManager`의 책임이 아닌 것:

* Singleton 관리
* GameObject 관리
* Scene Lifecycle
* CSV Parsing
* Code Generation
* Editor Asset 관리

`TableManager`에 새로운 기능을 추가할 때 위 경계를 우선 확인합니다.

내부 Table Key는 문자열 이름보다 `Type` 사용을 우선합니다.

---

# Reflection 사용 규칙

Reflection은 필요한 경우 사용할 수 있습니다.

적절한 사용 예:

* `[TableData]` Type 탐색
* Runtime `Type`을 이용한 Generic Method 호출
* Editor에서 생성된 Type 처리

단 다음 규칙을 지킵니다.

* 레코드별 Hot Path에서는 가능한 한 Reflection을 사용하지 않습니다.
* 반복적으로 사용하는 `MethodInfo`, `FieldInfo`, `PropertyInfo`는 가능하면 캐싱합니다.
* Reflection Member 반환 순서에 의존하지 않습니다.
* 필요한 Method/Field를 찾지 못한 경우 조용히 무시하지 않습니다.
* Assembly Scan 시 `ReflectionTypeLoadException` 가능성을 고려합니다.

---

# Settings

프로젝트마다 달라지는 값을 Package Source에 하드코딩하지 않습니다.

예:

* Generated Class Namespace
* CSV Source Path
* Generated Code Path
* Encrypted Data Path
* AES Key

이 값들은 DataTable Settings를 통해 관리합니다.

생성 파일은 사용하는 프로젝트의 `Assets/` 아래에 생성합니다.

설치된 Package 내부:

```text
Packages/com.causeless3t.datatable/
```

에는 프로젝트별 Generated Code나 Table Data를 생성하지 않습니다.

---

# 보안

AES 암호화는 게임 데이터가 파일에서 그대로 노출되는 것을 어렵게 만들기 위한 용도입니다.

Client에 AES Key가 포함되는 구조이므로 완전한 보안을 제공한다고 가정하지 않습니다.

다음과 같은 민감한 정보를 저장소에 추가하지 않습니다.

* API Secret
* Production Credential
* Signing Key
* Password
* 실제 서비스용 비밀키

---

# Error Handling

CSV 변환 오류는 가능한 한 다음 정보를 포함해야 합니다.

* Table Name
* Row
* Column / Field Name
* Expected Type
* Invalid Value

잘못된 Schema나 지원하지 않는 타입을 조용히 무시하지 않습니다.

Editor에서 Progress Bar를 사용하는 작업은 예외가 발생하더라도 제거될 수 있도록 `try/finally` 사용을 우선합니다.

오류 원인을 숨기면서 단순히 `null`을 반환하는 방식은 피합니다.

---

# 코드 스타일

기존 저장소 스타일을 우선합니다.

일반적인 기준:

* Method는 하나의 책임에 집중합니다.
* Early Return을 선호합니다.
* 타입/Schema 문자열 비교에는 `ToLowerInvariant()`를 사용합니다.
* 경로 결합에는 `Path.Combine()`을 사용합니다.
* 가능한 경우 `nameof()`를 사용합니다.
* 코드 내용을 그대로 설명하는 불필요한 주석은 작성하지 않습니다.
* 실제 필요가 없는 Interface나 Abstraction을 추가하지 않습니다.
* 작은 수정에서 unrelated formatting이나 대규모 리팩터링을 함께 하지 않습니다.

기존 API를 변경할 때는 Package 사용자의 코드에 미치는 영향을 먼저 고려합니다.

---

# 테스트

Parsing, Serialization, Code Generation을 수정할 경우 관련 테스트를 추가하거나 기존 테스트를 확인합니다.

우선순위가 높은 테스트 영역:

### Type Parsing

```text
CSV Value → Expected C# Value
```

지원되는 Primitive, Unity Type, Collection, DateTime을 확인합니다.

### Schema

다음을 확인합니다.

* 정상 Schema
* `#` Ignore Column
* 잘못된 Schema
* 지원하지 않는 Type

### Binary Round Trip

```text
Object
 ↓
Write
 ↓
Binary
 ↓
Read
 ↓
Equivalent Object
```

### DynamicDataObject

Deserialization 후 다음 두 Collection이 올바르게 구성되는지 확인합니다.

```text
List<T>
Map<string, T>
```

### Code Generation

다음을 확인합니다.

* 올바른 C# 코드 생성
* 타입별 올바른 Field 생성
* `Write` / `Read` 대칭성
* 동일 Schema에서 파일을 다시 쓰지 않음
* Schema 변경 시 파일 갱신

Reflection Member 순서에 의존하는 테스트는 작성하지 않습니다.

---

# 작업 완료 전 확인

코드 변경을 완료하기 전에 다음 사항을 확인합니다.

1. Runtime에서 `UnityEditor`를 참조하지 않는가?
2. Editor 전용 기능이 Editor Assembly에 있는가?
3. 새로운 불필요한 Dependency가 추가되지 않았는가?
4. CSV Parsing과 CSV Restore 형식이 서로 호환되는가?
5. Generated `Write` / `Read`가 대칭적인가?
6. Binary Format 변경이 의도된 것인가?
7. Generated C# 파일을 불필요하게 다시 쓰지 않는가?
8. Package 내부에 프로젝트별 파일을 생성하지 않는가?
9. 관련 테스트가 통과하는가?
10. Public API가 변경되었다면 README / CHANGELOG 수정이 필요한가?

---

# 하지 말아야 할 것

명확한 요구사항이 없다면 다음 변경을 하지 않습니다.

* `UnityCore` Dependency 추가
* `TableManager`를 Singleton으로 변경
* `TableManager`를 `MonoBehaviour`로 변경
* UniTask Dependency 추가
* protobuf 등 외부 Serialization Framework 추가
* `TableManager`에서 `Resources`나 Addressables 직접 호출
* ID 정보를 Reflection Field Index로 저장
* 여러 클래스에 Binary Serialization 로직 복제
* Generated Table Class 직접 수정
* 동일한 Generated C# 파일 반복 작성
* Package 디렉터리에 프로젝트별 Generated Asset 생성
* 현재 작업과 관계없는 대규모 리팩터링

---

# 변경 원칙

Architecture 변경이 필요한 경우 현재 Data Flow를 최대한 단순하게 유지합니다.

```text
CSV Authoring
     ↓
Editor Conversion
     ↓
Generated C# + Encrypted Binary
     ↓
Replaceable IDataLoader
     ↓
TableManager
     ↓
Strongly Typed Data
```

새로운 구조가 이 흐름을 복잡하게 만든다면, 그 변경으로 얻는 성능, 안정성, 확장성 또는 유지보수성의 이점이 명확한지 먼저 확인합니다.

명확한 이점이 없다면 더 단순한 구현을 선택합니다.
