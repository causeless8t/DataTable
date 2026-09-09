# Unity DataTable

Unity 프로젝트에서 CSV 기반 게임 데이터를 **강타입 C# 테이블로 생성하고, 바이너리 직렬화 및 암호화하여 런타임에서 사용할 수 있도록 하는 데이터 테이블 패키지**입니다.

CSV의 첫 번째 행을 스키마로 사용하여 C# 클래스를 자동 생성하며, 실제 데이터는 바이너리로 직렬화한 뒤 AES 암호화하여 저장합니다.

런타임에서는 생성된 타입을 기반으로 데이터를 복원하여 `DynamicDataObject<T>` 형태로 사용할 수 있습니다.

## Features

* CSV 기반 데이터 테이블 관리
* CSV Schema 기반 C# 클래스 자동 생성
* Strongly Typed Table Data
* Binary Serialization / Deserialization
* AES 기반 데이터 암호화
* ID 기반 Dictionary / List 동시 제공
* Unity Project Settings를 통한 경로 및 Namespace 설정
* `IDataLoader`를 통한 데이터 로딩 방식 확장
* 기본 `ResourcesDataLoader` 제공
* Binary → CSV 복원 지원
* 외부 Serialization 라이브러리 불필요
* UniTask 의존성 없음
* 특정 Singleton / DI Framework에 종속되지 않는 구조

---

## Data Pipeline

```text
              ┌──────────────────────┐
              │       CSV File       │
              │                      │
              │ id:int|name:string   │
              └──────────┬───────────┘
                         │
              ┌──────────▼───────────┐
              │    Schema Parsing    │
              └──────────┬───────────┘
                         │
                  Schema Changed?
                    /          \
                  Yes           No
                   │             │
                   ▼             │
          ┌─────────────────┐    │
          │ C# Code Generate│    │
          └────────┬────────┘    │
                   │             │
            Unity Compile        │
                   │             │
                   └──────┬──────┘
                          ▼
                ┌──────────────────┐
                │ CSV → Data Object│
                └────────┬─────────┘
                         │
                         ▼
                Binary Serialization
                         │
                         ▼
                   AES Encryption
                         │
                         ▼
                *_encry.bytes
                         │
                         ▼
                    IDataLoader
                         │
                         ▼
                AES Decryption
                         │
                         ▼
                  Deserialization
                         │
                         ▼
              DynamicDataObject<T>
```

스키마가 변경된 경우에만 C# 코드를 다시 생성합니다.

생성된 코드가 기존 코드와 동일하다면 불필요한 Unity Script Compilation을 발생시키지 않고 바로 데이터를 변환합니다.

---

# Installation

Unity Package Manager에서 Git URL을 이용하여 설치할 수 있습니다.

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

을 선택하고 위 Git URL을 입력합니다.

---

# CSV Format

CSV의 첫 번째 행은 데이터의 스키마를 정의합니다.

기본 구분자는 `|`입니다.

```text
id:int|name:string|attack:float|description:string
1|Sword|10.5|Basic Sword
2|GreatSword|25.0|Heavy Sword
3|Bow|8.5|Basic Bow
```

스키마는 다음 형식을 사용합니다.

```text
FieldName:Type
```

예:

```text
id:int
name:string
attack:float
```

여러 필드는 `|`로 구분합니다.

```text
id:int|name:string|attack:float
```

---

## Ignored Columns

필드 이름이 `#`으로 시작하는 컬럼은 데이터 생성에서 제외됩니다.

```text
id:int|name:string|#comment:string|attack:float
```

이를 이용하여 게임에서는 필요하지 않지만 CSV 작업에는 필요한 메모나 관리용 컬럼을 추가할 수 있습니다.

---

# Supported Types

현재 다음 타입을 지원합니다.

| CSV Type       | C# Type        |
| -------------- | -------------- |
| `int`          | `int`          |
| `long`         | `long`         |
| `float`        | `float`        |
| `double`       | `double`       |
| `string`       | `string`       |
| `datetime`     | `DateTime`     |
| `vector2`      | `Vector2`      |
| `vector3`      | `Vector3`      |
| `list<int>`    | `List<int>`    |
| `list<float>`  | `List<float>`  |
| `list<string>` | `List<string>` |

지원 타입은 `TypeParser`를 통해 확장할 수 있습니다.

---

# Code Generation

CSV의 Schema를 분석하여 테이블 클래스를 자동으로 생성합니다.

예를 들어 다음 CSV가 있다면:

```text
id:int|name:string|attack:float
```

다음과 같은 클래스가 생성됩니다.

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

`Write()` / `Read()` 코드까지 생성하기 때문에 별도의 Reflection 기반 field serialization을 런타임에서 수행하지 않습니다.

---

# Generate Table

Project 창에서 변환할 CSV 파일을 선택합니다.

```text
Right Click
└─ 테이블 생성
```

또는 Assets 메뉴에서 실행할 수 있습니다.

```text
Assets
└─ 테이블 생성
```

변환 과정은 다음과 같습니다.

```text
CSV
 ↓
Schema 검사
 ↓
Generated C# 검사
 ↓
Schema 변경 시 C# 재생성
 ↓
Unity Script Compilation
 ↓
CSV Parsing
 ↓
Binary Serialization
 ↓
AES Encryption
 ↓
*.bytes
```

여러 CSV 파일을 동시에 선택하여 생성할 수도 있습니다.

---

## Automatic Script Compilation Handling

CSV Schema가 변경되면 생성된 C# 클래스도 변경되어 Unity Script Compilation이 필요합니다.

Unity DataTable은 코드 생성 여부를 확인하여 필요한 경우 변환 상태를 보존하고, Unity의 Script Compilation이 끝난 뒤 데이터 생성을 이어서 수행할 수 있도록 구성되어 있습니다.

따라서 일반적인 작업 흐름은:

```text
CSV 수정
 ↓
테이블 생성
 ↓
완료
```

으로 유지할 수 있습니다.

스키마가 변경되지 않고 데이터만 변경된 경우에는 C# 파일을 다시 생성하지 않습니다.

---

# Generated Data

생성된 데이터는 다음과 같은 형태로 저장됩니다.

```text
ItemTable_encry.bytes
CharacterTable_encry.bytes
StageTable_encry.bytes
```

바이너리 데이터에는 레코드와 함께 Dictionary 생성에 사용할 **ID Field Name**이 저장됩니다.

개념적으로:

```text
Record Count
ID Field Name
Record 0
Record 1
Record 2
...
```

형태입니다.

ID를 Reflection field index가 아닌 이름으로 저장하여 런타임 Reflection의 필드 반환 순서에 의존하지 않습니다.

---

# Project Settings

패키지 설정은 Unity Project Settings에서 관리할 수 있습니다.

```text
Edit
└─ Project Settings
   └─ Data Table
```

예:

```text
Code Generation

Namespace
    Game.Data

Generated Code Path
    Assets/Scripts/Generated/DataTable


Table Data

CSV Source Path
    Assets/DataTable/CSV

Encrypted Data Path
    Assets/Resources/Table


Encryption

AES Key
    ••••••••••••••••
```

프로젝트마다 다른 Namespace와 출력 경로를 패키지 코드에 하드코딩하지 않고 설정할 수 있습니다.

---

# Runtime Loading

`TableManager`는 생성된 `[TableData]` 타입을 검색하여 각 테이블을 로드합니다.

로드된 데이터는:

```csharp
DynamicDataObject<T>
```

형태로 관리됩니다.

예:

```csharp
var tableManager =
    new TableManager(new ResourcesDataLoader());

await tableManager.LoadAll();
```

테이블 데이터는 다음과 같이 가져올 수 있습니다.

```csharp
var itemTable =
    tableManager.Get<ItemTable>();
```

---

# DynamicDataObject

각 테이블은 두 가지 형태의 접근 방법을 제공합니다.

```csharp
DynamicDataObject<T>
```

내부에는:

```csharp
List<T> List;
Dictionary<string, T> Map;
```

이 존재합니다.

따라서 순차 데이터 접근은:

```csharp
foreach (var item in itemTable.List)
{
    Debug.Log(item.name);
}
```

ID 기반 접근은:

```csharp
if (itemTable.Map.TryGetValue(
        "1001",
        out var item))
{
    Debug.Log(item.name);
}
```

처럼 사용할 수 있습니다.

---

# Data Loader

`TableManager`는 특정 데이터 저장 방식에 직접 의존하지 않고 `IDataLoader`를 통해 데이터를 읽습니다.

```csharp
public interface IDataLoader
{
    Task<byte[]> LoadAsync(string path);
}
```

기본적으로 `ResourcesDataLoader`를 제공합니다.

```csharp
var tableManager =
    new TableManager(
        new ResourcesDataLoader());
```

프로젝트에서 다른 데이터 시스템을 사용한다면 `IDataLoader`를 구현하여 교체할 수 있습니다.

예:

```csharp
public sealed class AddressablesDataLoader
    : IDataLoader
{
    public async Task<byte[]> LoadAsync(
        string path)
    {
        // Addressables implementation
    }
}
```

```csharp
var tableManager =
    new TableManager(
        new AddressablesDataLoader());
```

이를 통해 TableManager는 다음 시스템에 직접 의존하지 않습니다.

```text
Resources
Addressables
AssetBundle
CDN
Local File
Custom Asset System
```

---

# No Singleton Dependency

`TableManager`는 특정 Singleton 구현에 의존하지 않습니다.

```csharp
var tableManager =
    new TableManager(
        new ResourcesDataLoader());
```

애플리케이션에서 Singleton 접근이 필요한 경우 프로젝트 레벨에서 관리할 수 있습니다.

```csharp
public static class Tables
{
    public static TableManager Instance { get; } =
        new(new ResourcesDataLoader());
}
```

또는 사용하는 프로젝트의 DI Container / Service Locator / Bootstrap 구조에 등록할 수 있습니다.

패키지는 특정 애플리케이션 아키텍처를 강제하지 않습니다.

---

# Binary → CSV

암호화된 테이블 데이터를 다시 CSV로 복원할 수도 있습니다.

Project 창에서:

```text
*_encry.bytes
```

파일을 선택한 뒤:

```text
Assets
└─ 테이블 복원
```

을 실행합니다.

처리 과정:

```text
Encrypted Binary
 ↓
AES Decryption
 ↓
Binary Deserialization
 ↓
Generated Table Object
 ↓
CSV
```

이 기능은 데이터 확인이나 디버깅에 사용할 수 있습니다.

---

# Encryption

생성된 Binary 데이터는 AES를 이용하여 암호화할 수 있습니다.

```text
CSV
 ↓
Binary
 ↓
AES Encrypt
 ↓
*_encry.bytes
```

런타임에서는 반대로:

```text
*_encry.bytes
 ↓
AES Decrypt
 ↓
Binary Deserialize
 ↓
Table
```

순서로 데이터를 복원합니다.

> AES 키를 클라이언트 애플리케이션에 포함하는 방식은 데이터의 완전한 기밀성을 보장하지 않습니다.

클라이언트에 키가 포함되어 있다면 분석을 통해 키를 추출할 가능성이 있으므로, 이 기능은 민감한 비밀정보를 안전하게 저장하기 위한 보안 시스템이라기보다 게임 데이터의 단순 노출을 어렵게 만드는 용도로 사용하는 것을 권장합니다.

---

# Design Goals

이 패키지는 다음 원칙을 중심으로 설계했습니다.

### Strongly Typed Data

런타임에서 Dictionary나 JSON 객체를 반복적으로 해석하지 않고 생성된 C# 타입을 사용합니다.

```csharp
item.id
item.name
item.attack
```

IDE 자동완성과 컴파일 타임 타입 검사의 이점을 얻을 수 있습니다.

### Minimal Runtime Reflection

Reflection은 주로 테이블 타입 탐색과 동적 generic 호출에 사용합니다.

실제 레코드의 Binary Read / Write 코드는 CSV Schema에서 생성하므로 각 필드를 Reflection으로 직렬화하지 않습니다.

### Minimal Dependencies

테이블 시스템 자체가 특정 Singleton Framework, DI Container 또는 UniTask에 의존하지 않도록 구성합니다.

### Replaceable Data Loading

데이터가 어디에 저장되는지는 테이블 시스템과 분리합니다.

```text
TableManager
     │
 IDataLoader
     │
 ┌───┼───────────┐
 ↓   ↓           ↓
Resources   Addressables   CDN
```

### Editor / Runtime Separation

CSV Parsing, Code Generation, Binary Generation 등의 Authoring 기능은 Editor 영역에서 처리하고, Runtime에는 실제 테이블을 읽고 사용하는 데 필요한 코드만 포함합니다.

---

# Package Structure

```text
DataTable
├─ Runtime
│  ├─ Data
│  │  ├─ DynamicDataObject.cs
│  │  └─ TableDataAttribute.cs
│  │
│  ├─ Loader
│  │  ├─ IDataLoader.cs
│  │  └─ ResourcesDataLoader.cs
│  │
│  ├─ Serialization
│  │  ├─ DeserializeUtil.cs
│  │  └─ AesEncryption.cs
│  │
│  └─ TableManager.cs
│
└─ Editor
   ├─ CsvToBinaryConverter.cs
   ├─ DynamicClassGenerator.cs
   ├─ TableGenerator.cs
   │
   └─ Settings
      ├─ DataTableSettingsProvider.cs
      ├─ DataTableProjectSettings.cs
      └─ DataTableEditorGUI.cs
```

실제 디렉터리 구성에 따라 이름은 조정할 수 있습니다.

---

# Example Workflow

### 1. CSV 작성

```text
id:int|name:string|price:int
1001|Potion|100
1002|HighPotion|500
1003|Elixir|1000
```

### 2. 테이블 생성

Project 창에서 CSV를 선택하고:

```text
Assets → 테이블 생성
```

을 실행합니다.

### 3. 클래스 자동 생성

```csharp
public class ItemTable
{
    public int id => _id;
    public string name => _name;
    public int price => _price;

    ...
}
```

### 4. 암호화 데이터 생성

```text
ItemTable_encry.bytes
```

### 5. 런타임 로드

```csharp
var tables =
    new TableManager(
        new ResourcesDataLoader());

await tables.LoadAll();
```

### 6. 데이터 사용

```csharp
var items =
    tables.Get<ItemTable>();

var potion =
    items.Map["1001"];

Debug.Log(potion.name);
Debug.Log(potion.price);
```

---

# Extending

새로운 CSV 타입을 지원하려면 타입 변환과 Binary Read / Write 생성을 확장할 수 있습니다.

주요 확장 지점:

```text
TypeParser
DynamicClassGenerator
CSV Restore
```

새로운 데이터 저장 방식을 지원하려면:

```csharp
IDataLoader
```

를 구현하면 됩니다.

이를 통해 테이블 데이터 구조와 실제 Asset Loading 방식을 독립적으로 확장할 수 있습니다.

---

# Requirements

* Unity
* C#
* 별도의 외부 Serialization 라이브러리 불필요

현재 구현은 Unity의 Editor API와 Runtime API를 사용합니다.

---

# Notes

* 생성된 C# 파일은 직접 수정하지 않는 것을 권장합니다.
* CSV Schema가 변경되면 생성 클래스도 자동으로 갱신됩니다.
* 데이터만 변경된 경우 동일한 C# 코드를 다시 생성하지 않습니다.
* Binary format 변경 시 기존 생성 데이터와의 호환성을 확인해야 합니다.
* AES 암호화는 클라이언트 내부의 민감한 비밀정보 보호 수단으로 간주해서는 안 됩니다.

---

# License

라이선스 정보는 저장소의 `LICENSE` 파일을 참고하세요.
