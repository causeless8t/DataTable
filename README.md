# Unity DataTable

CSV 데이터를 기반으로 **강타입 C# 테이블 클래스를 자동 생성하고, 데이터를 바이너리 직렬화 및 AES 암호화하여 런타임에서 사용할 수 있도록 하는 Unity 패키지**입니다.

별도의 Serialization 라이브러리 없이 CSV Schema로부터 C# 코드를 생성하며, 런타임에서는 `DynamicDataObject<T>`를 통해 List 순회와 ID 기반 조회를 모두 지원합니다.

## Features

* CSV Schema 기반 C# 클래스 자동 생성
* Strongly Typed Table Data
* Binary Serialization / Deserialization
* AES 기반 데이터 암호화
* `List<T>` / `Dictionary<string, T>` 동시 제공
* Schema 변경 시 자동 Script Compilation 및 변환 재개
* Unity Project Settings 기반 설정
* `IDataLoader`를 통한 데이터 로딩 방식 확장
* 기본 `ResourcesDataLoader` 제공
* Binary → CSV 복원
* Runtime / Editor Assembly 분리
* 외부 Serialization 라이브러리 불필요
* UniTask 의존성 없음
* 특정 Singleton / DI Framework에 종속되지 않음

---

## Installation

Unity Package Manager에서 Git URL을 통해 설치할 수 있습니다.

```text
https://github.com/causeless8t/DataTable.git
```

Unity에서:

```text
Window
└─ Package Manager
   └─ +
      └─ Add package from git URL...
```

을 선택하고 위 URL을 입력합니다.

특정 버전을 사용하려면 Git tag를 지정할 수도 있습니다.

```text
https://github.com/causeless8t/DataTable.git#1.0.1
```

---

# Quick Start

## 1. Project Settings

설치 후 다음 메뉴에서 DataTable 설정을 확인합니다.

```text
Edit
└─ Project Settings
   └─ Data Table
```

기본 설정은 다음과 같습니다.

```text
Namespace
    Causeless3t.Table

CSV Source Path
    Assets/CSV

Generated Code Path
    Assets/Scripts/Generated/Tables

Encrypted Data Path
    Assets/Resources/Table
```

설정은 프로젝트의 `Assets/Resources/DataTableSettings.asset`에 저장됩니다.

프로젝트 구조에 맞게 Namespace와 각 경로를 변경할 수 있습니다.

---

## 2. CSV 작성

CSV의 첫 번째 행에 필드 이름과 타입을 정의합니다.

```text
id:int|name:string|attack:float
1001|Sword|10.5
1002|GreatSword|25
1003|Bow|8.5
```

Schema 형식은 다음과 같습니다.

```text
FieldName:Type
```

컬럼 구분자는 `|`입니다.

---

## 3. 테이블 생성

Project 창에서 하나 이상의 CSV 파일을 선택하고:

```text
Assets
└─ 테이블 생성
```

을 실행합니다.

CSV Schema를 분석하여 C# 클래스와 암호화된 Binary Table을 생성합니다.

```text
ItemTable.csv
      │
      ├──── Schema ────→ ItemTable.cs
      │
      └──── Data ──────→ ItemTable_encry.bytes
```

---

## 4. Runtime에서 로드

기본 `ResourcesDataLoader`를 사용할 수 있습니다.

```csharp
var tableManager = new TableManager(
    new ResourcesDataLoader());

await tableManager.LoadAll();
```

로드된 테이블은 타입으로 가져옵니다.

```csharp
var items = tableManager.Get<ItemTable>();
```

전체 데이터를 순회하려면:

```csharp
foreach (var item in items.List)
{
    Debug.Log(item.name);
}
```

ID로 데이터를 찾으려면:

```csharp
if (items.Map.TryGetValue("1001", out var item))
{
    Debug.Log(item.name);
}
```

---

# CSV Format

## Schema

첫 번째 행은 테이블 Schema입니다.

```text
id:int|name:string|price:int|description:string
```

각 컬럼은:

```text
FieldName:Type
```

형식으로 작성합니다.

예:

```text
id:int
name:string
price:int
```

---

## Ignored Columns

`#`으로 시작하는 Schema 컬럼은 생성 대상에서 제외됩니다.

```text
id:int|name:string|#comment:string|price:int
```

이를 이용해 런타임에서는 필요하지 않은 기획 메모나 관리용 컬럼을 CSV에 유지할 수 있습니다.

---

## Supported Types

| CSV Type       | Generated C# Type | CSV Example           |
| -------------- | ----------------- | --------------------- |
| `int`          | `int`             | `100`                 |
| `long`         | `long`            | `100000`              |
| `float`        | `float`           | `10.5`                |
| `double`       | `double`          | `10.123`              |
| `string`       | `string`          | `Sword`               |
| `datetime`     | `DateTime`        | `2026-09-10 12:00:00` |
| `vector2`      | `Vector2`         | `(1.0,2.0)`           |
| `vector3`      | `Vector3`         | `(1.0,2.0,3.0)`       |
| `list<int>`    | `List<int>`       | `1,2,3`               |
| `list<float>`  | `List<float>`     | `1.0,2.0,3.0`         |
| `list<string>` | `List<string>`    | `Sword,Armor,Potion`  |
| `bignum`       | `double`          | `1000000`             |
| `bignumber`    | `double`          | `1000000`             |

`datetime`은 다음 형식을 지원합니다.

```text
yyyy-MM-dd
yyyy-MM-dd HH:mm:ss
```

---

# Code Generation

예를 들어 다음 Schema가 있다면:

```text
id:int|name:string|attack:float
```

다음과 같은 형태의 C# 클래스가 자동 생성됩니다.

```csharp
[Serializable]
[TableData]
public class ItemTable
{
    [SerializeField]
    private int _id;

    public int id => _id;

    [SerializeField]
    private string _name;

    public string name => _name;

    [SerializeField]
    private float _attack;

    public float attack => _attack;

    public void Write(BinaryWriter bw)
    {
        bw.Write(_id);
        bw.Write(_name);
        bw.Write(_attack);
    }

    public void Read(BinaryReader br)
    {
        _id = br.ReadInt32();
        _name = br.ReadString();
        _attack = br.ReadSingle();
    }
}
```

생성된 클래스가 Binary Serialization 방법을 직접 가지고 있기 때문에 각 레코드의 모든 필드를 Reflection으로 순회하며 직렬화할 필요가 없습니다.

---

# Generation Pipeline

테이블 생성은 크게 두 단계로 이루어집니다.

```text
CSV
 │
 ▼
Schema Parsing
 │
 ▼
Generated Class 검사
 │
 ├── 변경 없음 ─────────────────┐
 │                              │
 └── 변경됨                     │
      │                         │
      ▼                         │
 C# Code Generation             │
      │                         │
      ▼                         │
 Unity Script Compilation       │
      │                         │
      └─────────────┬───────────┘
                    ▼
              CSV Parsing
                    │
                    ▼
          Binary Serialization
                    │
                    ▼
             AES Encryption
                    │
                    ▼
           *_encry.bytes
```

## Automatic Compilation Resume

Schema 변경으로 생성되는 C# 코드가 달라지면 Unity Script Compilation이 필요합니다.

이 경우 선택한 CSV Asset 경로와 작업 상태를 `SessionState`에 보존하고 Script Compilation이 끝난 뒤 자동으로 데이터 생성을 재개합니다.

따라서 사용자는 Schema를 변경한 경우에도 다시 `테이블 생성`을 실행할 필요가 없습니다.

반대로 데이터만 변경되어 생성 코드가 동일한 경우에는 Script Compilation 없이 바로 Binary Table을 다시 생성합니다.

---

# Generated Binary Format

Binary Table은 개념적으로 다음 구조를 가집니다.

```text
Record Count
ID Field Name

Record 0
Record 1
Record 2
...
```

ID 컬럼은 Reflection Field Index가 아닌 **Field Name**으로 저장됩니다.

이를 통해 `GetFields()`가 반환하는 필드 순서에 Binary Format이 의존하지 않도록 구성합니다.

생성된 Binary는 AES 암호화를 거쳐 다음과 같이 저장됩니다.

```text
ItemTable_encry.bytes
CharacterTable_encry.bytes
StageTable_encry.bytes
```

---

# DynamicDataObject

역직렬화된 테이블은 `DynamicDataObject<T>`로 관리됩니다.

```csharp
public class DynamicDataObject<T>
{
    public Dictionary<string, T> Map = new();
    public List<T> List = new();
}
```

따라서 하나의 테이블에 대해 두 가지 접근 방식을 사용할 수 있습니다.

순차 처리:

```csharp
foreach (var item in items.List)
{
    // ...
}
```

ID 조회:

```csharp
var item = items.Map["1001"];
```

---

# TableManager

`TableManager`는 `[TableData]`가 지정된 테이블 타입을 검색하고 데이터를 로드합니다.

```csharp
var tables = new TableManager(
    new ResourcesDataLoader());

await tables.LoadAll();
```

로드 상태는 다음으로 확인할 수 있습니다.

```csharp
tables.IsLoaded
```

테이블을 가져올 때는:

```csharp
var items = tables.Get<ItemTable>();
```

사용이 끝난 테이블은 제거할 수 있습니다.

```csharp
tables.UnloadAll();
```

`TableManager`는 Singleton을 상속하지 않는 일반 C# 클래스입니다.

따라서 패키지가 특정 Singleton 구현이나 Application Framework에 의존하지 않습니다.

프로젝트에서 Singleton 형태가 필요하다면 애플리케이션 레벨에서 관리할 수 있습니다.

```csharp
public static class Tables
{
    public static TableManager Instance { get; } =
        new(new ResourcesDataLoader());
}
```

DI Container를 사용하는 프로젝트에서는 `TableManager`를 서비스로 등록하여 사용할 수도 있습니다.

---

# Custom Data Loader

`TableManager`는 실제 Asset Loading 방법을 직접 구현하지 않고 `IDataLoader`에 위임합니다.

```csharp
public interface IDataLoader
{
    Task<byte[]> LoadAsync(string path);
}
```

패키지는 기본 구현으로 `ResourcesDataLoader`를 제공합니다.

```csharp
var tables = new TableManager(
    new ResourcesDataLoader());
```

필요하다면 프로젝트에서 다른 Loader를 구현할 수 있습니다.

```csharp
public sealed class AddressablesDataLoader : IDataLoader
{
    public async Task<byte[]> LoadAsync(string path)
    {
        // Addressables loading
    }
}
```

그리고 `TableManager` 생성 시 주입합니다.

```csharp
var tables = new TableManager(
    new AddressablesDataLoader());
```

같은 방식으로 다음과 같은 데이터 소스를 사용할 수 있습니다.

```text
Resources
Addressables
AssetBundle
Local File
CDN
Custom Asset System
```

테이블 시스템은 실제 데이터가 어디에서 로드되는지 알 필요가 없습니다.

---

# Binary → CSV

생성된 암호화 테이블을 다시 CSV로 복원할 수 있습니다.

Project 창에서:

```text
*_encry.bytes
```

파일을 선택하고:

```text
Assets
└─ 테이블 복원
```

을 실행합니다.

처리 과정은 다음과 같습니다.

```text
Encrypted Binary
       │
       ▼
 AES Decryption
       │
       ▼
 Deserialization
       │
       ▼
Generated Table Object
       │
       ▼
      CSV
```

복원된 CSV는 Project Settings의 `CSV Source Path`에 생성됩니다.

---

# Project Settings

DataTable 설정은:

```text
Edit
└─ Project Settings
   └─ Data Table
```

에서 관리합니다.

설정은 `DataTableSettings` ScriptableObject로 저장됩니다.

| Setting             | Description               |
| ------------------- | ------------------------- |
| Namespace           | 자동 생성되는 테이블 클래스 Namespace |
| CSV Source Path     | CSV 원본 및 복원 파일 경로         |
| Generated Code Path | 자동 생성 C# 코드 경로            |
| Encrypted Data Path | 암호화 Binary Table 출력 경로    |
| AES Key             | Binary 암호화 / 복호화에 사용할 Key |

기본 Asset은 다음 위치에 생성됩니다.

```text
Assets/Resources/DataTableSettings.asset
```

`ResourcesDataLoader`와 런타임 복호화 과정에서도 동일한 설정을 사용합니다.

---

# Encryption

CSV 데이터는 Binary Serialization 이후 AES 암호화를 거쳐 저장됩니다.

```text
CSV
 ↓
Binary
 ↓
AES Encryption
 ↓
*_encry.bytes
```

런타임에서는 반대로 처리합니다.

```text
*_encry.bytes
 ↓
AES Decryption
 ↓
Binary Deserialization
 ↓
DynamicDataObject<T>
```

> AES Key가 클라이언트 애플리케이션에 포함되어 있기 때문에 이 기능은 완전한 기밀성을 보장하지 않습니다.

민감한 비밀정보를 저장하기 위한 보안 시스템보다는 게임 데이터가 파일에서 그대로 노출되는 것을 어렵게 만드는 용도로 사용하는 것이 적절합니다.

---

# Architecture

패키지는 Editor와 Runtime을 별도의 Assembly로 분리합니다.

```text
DataTable/
├─ Editor/
│  ├─ Settings/
│  │  ├─ DataTableEditorGUI.cs
│  │  ├─ DataTableProjectSettings.cs
│  │  ├─ DataTableSettingProvider.cs
│  │  └─ DataTableSettings.cs
│  │
│  ├─ CsvToBinaryConverter.cs
│  ├─ DynamicClassGenerator.cs
│  ├─ DynamicTypeGenerator.cs
│  ├─ TableGenerationResumeHandler.cs
│  ├─ TableGenerationState.cs
│  ├─ TableGenerator.cs
│  ├─ TypeParser.cs
│  └─ Causeless3t.DataTable.Editor.asmdef
│
├─ Runtime/
│  ├─ Loader/
│  │  ├─ IDataLoader.cs
│  │  └─ ResourcesDataLoader.cs
│  │
│  ├─ Security/
│  │  └─ ...
│  │
│  ├─ Settings/
│  │  └─ ...
│  │
│  ├─ DeserializeUtil.cs
│  ├─ DynamicDataObject.cs
│  ├─ TableManager.cs
│  └─ Causeless3t.DataTable.asmdef
│
├─ package.json
├─ README.md
├─ CHANGELOG.md
└─ LICENSE
```

---

# Design Goals

## Strongly Typed Data

런타임에서 문자열 기반 JSON이나 Dictionary를 반복적으로 해석하는 대신 실제 C# 타입을 사용합니다.

```csharp
item.id
item.name
item.attack
```

IDE 자동완성 및 컴파일 타임 타입 검사의 이점을 얻을 수 있습니다.

## Generated Serialization

CSV Schema에서 `Write(BinaryWriter)` / `Read(BinaryReader)` 코드를 생성합니다.

데이터 구조와 Binary Serialization 코드가 함께 생성되므로 런타임에서 모든 필드를 동적으로 해석하는 구조를 피합니다.

## Minimal Dependencies

패키지는 특정 Singleton Framework, DI Container, UniTask 또는 외부 Serialization 라이브러리에 의존하지 않는 것을 목표로 합니다.

## Replaceable Data Loading

```text
                TableManager
                     │
                IDataLoader
                     │
        ┌────────────┼────────────┐
        ▼            ▼            ▼
    Resources    Addressables    CDN
```

TableManager와 실제 Asset Loading 방식을 분리하여 프로젝트 환경에 맞는 Loader를 사용할 수 있습니다.

## Editor / Runtime Separation

CSV Parsing, Code Generation, Binary Generation 등의 Authoring 기능은 Editor Assembly에서 처리합니다.

Runtime Assembly에는 생성된 데이터를 로드하고 사용하는 데 필요한 기능만 포함합니다.

---

# Requirements

* Unity 2022.3 or later
* C#

별도의 외부 Serialization 패키지는 필요하지 않습니다.

---

# Notes

* 생성된 C# 파일은 직접 수정하지 않는 것을 권장합니다.
* CSV Schema가 변경되면 생성 클래스가 자동으로 갱신됩니다.
* Schema 변경으로 Script Compilation이 발생하면 컴파일 완료 후 Binary 생성을 자동으로 재개합니다.
* 데이터만 변경된 경우 생성 C# 파일을 다시 쓰지 않습니다.
* Binary Format이 변경된 버전에서는 기존 `*_encry.bytes` 파일을 다시 생성해야 할 수 있습니다.
* AES 암호화는 클라이언트 내부의 민감한 비밀정보를 보호하기 위한 보안 수단으로 간주해서는 안 됩니다.

---

# License

This project is licensed under the MIT License.

See `LICENSE` for details.
