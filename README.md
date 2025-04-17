# Database Assistant (Mssql)

[![Unit Test][test-badge]][test-url] [![NuGet Version][nuget-v-badge]][nuget-url] [![NuGet Downloads][nuget-dt-badge]][nuget-url]

![promotion](https://raw.githubusercontent.com/pet-toys/db-assistant-mssql/refs/heads/dev/assets/promotion.png)

***DbAssistant.Mssql*** is the open source .net library with nice wrappers for SqlConnection.

#### Key features:

<!--- High-performance insertion of large data into a table (`COPY table(column definitions) FROM STDIN BINARY;` feature)-->
- Accepts `IEnumerable<TEntity>` and `IAsyncEnumerable<TEntity>`
- Supports mapping of entity properties to table columns (e.g. `MapJson`, `MapMoney`, `MapTimeStamp`, etc.)
- For better performance, it is recommended to insert data into a temporary table that has no indexes or keys. After that, you can copy data from the temporary table to the target table.

#### Usage
```csharp
using PetToys.DbAssistant.Mssql;

await using var connection = new SqlConnection(connectionString);
var result = await connection.CreateBulkContext<BusinessEntity>("table_name")
            .MapJson("column_json", entity => entity.Data)
            .MapMoney("column_money", entity => entity.Money)
            /* ... */
            .InsertAsync(entities);
```

<!--#### Roadmap:
- High-performance binary import/export between table and stream (coming soon)
- CSV import/export (may be)
- There may be something else-->

This package is created for my own needs.
Requests for additional functionality and pull requests are welcome.

---
Provided under the [Apache License, Version 2.0](http://apache.org/licenses/LICENSE-2.0.html).

[nuget-v-badge]: https://img.shields.io/nuget/v/PetToys.DbAssistant.Mssql?style=flat-square&logo=nuget&label=version
[nuget-dt-badge]: https://img.shields.io/nuget/dt/PetToys.DbAssistant.Mssql?style=flat-square&logo=nuget
[nuget-url]: https://www.nuget.org/packages/PetToys.DbAssistant.Mssql/
[test-badge]: https://img.shields.io/github/actions/workflow/status/pet-toys/db-assistant-mssql/test.yml?branch=dev&style=flat-square&logo=github&label=test
[test-url]: https://github.com/pet-toys/db-assistant-mssql/actions?query=workflow%3Atest+branch%3Adev
