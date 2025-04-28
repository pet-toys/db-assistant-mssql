## Database Assistant (Mssql)

[![Unit Test][test-badge]][test-url] [![NuGet Version][nuget-v-badge]][nuget-url] [![NuGet Downloads][nuget-dt-badge]][nuget-url]

![promotion](https://raw.githubusercontent.com/pet-toys/db-assistant-mssql/refs/heads/dev/assets/promotion.png)

***DbAssistant.Mssql*** is the open source .net library with nice wrappers for SqlConnection.

### Key features:

- Accepts `IEnumerable<TEntity>`
- Supports mapping of entity properties to table columns
- Supports nullable value type properties
- Supports reference types with nullable context. If the entity class does not have a nullable context, use an extra parameter in the converter.
- Supports following property types:
  - `bool`
  - `char`
  - `string`
  - `byte`
  - `short`
  - `int`
  - `long`
  - `float`
  - `double`
  - `decimal`
  - `DateTime`
  - `Guid`
  - `byte[]`
  - `char[]`
- For better performance, it is recommended to insert data into a temporary table that has no indexes or keys. After that, you can copy data from the temporary table to the target table.

### Usage
```csharp
using PetToys.DbAssistant.Mssql;

await using var connection = new SqlConnection(connectionString);
var result = await connection.CreateBulkContext<Entity>(tableName)
            .MapProperty(e => e.Int0)
            .MapProperty(e => e.Int1, "alias")
            .MapProperty(e => e.Date0)
            .MapProperty(e => e.Date1)
            .MapProperty(e => e.Str0)
            .MapProperty(e => e.Str1, referenceNullable: true)
            .MapProperty(e => e.Arr0)
            .MapProperty(e => e.Arr1)
            .WriteDataAsync(data, options =>
            {
                options.BulkCopyTimeout = 30;
            })
```

This package is created for my own needs.
Requests for additional functionality and pull requests are welcome.

---
Provided under the [Apache License, Version 2.0](http://apache.org/licenses/LICENSE-2.0.html).

[nuget-v-badge]: https://img.shields.io/nuget/v/PetToys.DbAssistant.Mssql?style=flat-square&logo=nuget&label=version
[nuget-dt-badge]: https://img.shields.io/nuget/dt/PetToys.DbAssistant.Mssql?style=flat-square&logo=nuget
[nuget-url]: https://www.nuget.org/packages/PetToys.DbAssistant.Mssql/
[test-badge]: https://img.shields.io/github/actions/workflow/status/pet-toys/db-assistant-mssql/test.yml?branch=dev&style=flat-square&logo=github&label=test
[test-url]: https://github.com/pet-toys/db-assistant-mssql/actions?query=workflow%3Atest+branch%3Adev
