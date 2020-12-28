# Introduction

We had created a GraphQL api using dotnet core 3.1 in our previous post [creating-dotnet-core-3-1-graphql-api-using-ef-core-postgresql-docker](https://dev.to/muratas/creating-dotnet-core-3-1-graphql-api-using-ef-core-postgresql-docker-4b3m) You can clone this project and continue from here.

We'll be adding xunit testing to our project. Complete github repo is [here](https://github.com/killjoy2013/xunit-net-core-graphql).

### 1. Adding a test project

Firstly, we need to add a new project to our existing Visual Studio solution using project template `xUnit Test Project (.NET Core)`. Project name is `Testing`. Upon creation, we update the .csproj file as below;

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netcoreapp3.1</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="16.8.0" />
    <PackageReference Include="xunit" Version="2.4.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.4.1" />
    <PackageReference Include="Microsoft.AspNetCore.TestHost" Version="3.1.10" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\GraphQL.WebApi\GraphQL.WebApi.csproj" />
  </ItemGroup>
  <ItemGroup>
    <None Update="appsettings.Development.json">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

We'll be adding tests to this project. We can build the whole solution.

### 2. Adding a test database
When writing tests, main problem is to create test data. We may have so many tests and don't want them pollute our existing development db. Traditional way to achive this is to create a mock db context. However, maintaining such a mock context can be quite boring. Because we may need to update it when we change the db models. A much more efficient approach is to run a test db using Docker compose. Our project is on code-first basis and already have a `docker-compose.yaml` to create my development db. Why don't we use this infrastructure to create a test db and apply our db migrations on it, finally run the test easily?

`docker-compose.yaml`
```yml
version: "3.3"
networks:
  graph-starter:
services:
  graphdb:
    restart: always
    image: postgres:12.2-alpine
    ports:
      - "5432:5432"
    environment:
      - POSTGRES_USER=postgres
      - POSTGRES_PASSWORD=postgres
      - POSTGRES_DB=graphdb
    volumes:
      - /var/lib/postgresql/data
    networks:
      - graph-starter 
  testdb:
    restart: always
    image: postgres:12.2-alpine
    ports:
      - "5433:5432"
    environment:
      - POSTGRES_USER=postgres
      - POSTGRES_PASSWORD=postgres
      - POSTGRES_DB=testdb
    volumes:
      - /var/lib/postgresql/data
    networks:
      - graph-starter    
```
As you can see, we added `testdb` into services. Then running `docker-compose up -d` we have both graphdb & testdb. 

![docker-compose-up](https://dev-to-uploads.s3.amazonaws.com/i/zyly0cn3annc6nai8y4l.PNG)

### 3. Completing Testing scaffolding & applying db migrations to test database

Now we will update testing project so that all the migrations will be applied when we run a dummy test. In testing project, we need to add the db connection string. Our testdb is running on port 5433;

`appsettings.Development.json`
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "ConnectionString": "Server=localhost;Port=5433;Database=testdb;Username=postgres;Password=postgres"
}
```

We'd like to create a test server to mimic our web api server and create testdb context. First part of the TestClassFixture constructor is to configure the webHostBuilder to use `appsettings.Development.json`. Then we're creating `Server`, `Client` and `DbContext`. TestServer is supplied by `Microsoft.AspNetCore.TestHost` which we already added to .csproj file. 
It's important to note that we're creating the test server using our `GraphQL.WebApi`'s original `Startup` file. So, we don't need to maintain a separate startup logic for the test environment! Then we created `GqlResult<T>` and `GqlResultList<T>` we'll come to them later.

`Helpers/TestClassFixture.cs`
```c#
using GraphQL.WebApi;
using GraphQL.WebApi.Repository;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;

namespace Testing.Helpers
{
    public class TestClassFixture : IDisposable
    {      
        public DatabaseContext DbContext { get; set; }
        public TestServer Server { get; set; }
        public HttpClient Client { get; set; } 
        public TestClassFixture()
        {
            var webHostBuilder = WebHost.CreateDefaultBuilder();
            webHostBuilder.UseDefaultServiceProvider(options => options.ValidateScopes = false);
            webHostBuilder.UseEnvironment("Development");

            webHostBuilder.ConfigureAppConfiguration((builderContext, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile($"appsettings.Development.json", optional: false, reloadOnChange: true)
                             .AddEnvironmentVariables();                

            });

            Server = new TestServer(webHostBuilder.UseStartup<Startup>());
            Client = Server.CreateClient();
            DbContext = Server.Host.Services.GetService(typeof(DatabaseContext)) as DatabaseContext;
        }       
        public void Dispose()
        {
            DbContext.Dispose();
            Client.Dispose();
            Server.Dispose();
        }
    }
    public class GraphQLError
    {
        public string Key { get; set; }
        public string Value { get; set; }

    }
    public class GqlResult<T>
    {
        public GqlResult(string serviceResultJson, string queryName)
        {
            var rawResultJObject = JObject.Parse(serviceResultJson);
            var dataJObject = rawResultJObject["data"];
            Data = dataJObject == null ? default(T) : dataJObject[queryName].ToObject<T>();
            var graphQLErrorsJArray = (JArray)rawResultJObject["graphQLErrors"];
            GraphQLError = graphQLErrorsJArray == null ? null : graphQLErrorsJArray.ToObject<IList<GraphQLError>>()[0];

        }

        public T Data { get; set; }
        public GraphQLError GraphQLError { get; set; }
    }
    public class GqlResultList<T>
    {
        public GqlResultList(string serviceResultJson, string queryName)
        {
            var rawResultJObject = JObject.Parse(serviceResultJson);

            var dataJObject = rawResultJObject["data"];
            var dataArray = dataJObject == null ? new JArray() : (JArray)rawResultJObject["data"][queryName];
            Data = dataArray.ToObject<IList<T>>();
            var graphQLErrorsJArray = (JArray)rawResultJObject["graphQLErrors"];
            GraphQLError = graphQLErrorsJArray == null ? null : graphQLErrorsJArray.ToObject<IList<GraphQLError>>()[0];

        }

        public IList<T> Data { get; set; }
        public GraphQLError GraphQLError { get; set; }
    }
}

```

Let's add & run our very first test. `UnitTest1.cs` is already created when we add `Testing` project. Let's use it.

`UnitTest1.cs`
```c#
using Testing.Helpers;
using Xunit;

namespace Testing
{
    public class UnitTest1: IClassFixture<TestClassFixture>
    {
        [Fact]
        public void Test1()
        {
            Assert.True(1 == 1);
        }
    }
}
```
Upon building the solution, we'll be seeing our test in the test explorer inside Visual Studio. Writing and running xunit tests in Visual Studio is awesome! 

(![test_explorer_not_run](https://dev-to-uploads.s3.amazonaws.com/i/ga043fjr1lajuf47srgx.PNG))

Using this fantastic test we'd like to see `TestClassFixture` in action. In the test explorer right click test1 and select run. If everyting goes well, test explorer is supposed to seen as below;

![test_explorer_run](https://dev-to-uploads.s3.amazonaws.com/i/598ry63kt5ety3tvup5y.PNG)


Our fantastic test has passed. Let's check `testdb`

![test_db](https://dev-to-uploads.s3.amazonaws.com/i/u7sd98yf1kovu7tehz03.PNG)

Very well, our db migrations are applied to `testdb`.

We can generally create two types of tests; unit tests & integration tests. If we invoke a class method directly, we'll call it unit test. If we use an HttpClient to send http requests, we'll be referring them as integration tests.

### 4. Creating unit tests

Let's say we have a `CountryHelper` and it has two methods, `CreateCountry` & `QueryCountry`. Testing those two methods can be good example for unit tests. Let's create the helper;

`Interfaces/ICountryHelper.cs`
```c#
using GraphQL.WebApi.Models;

namespace GraphQL.WebApi.Interfaces
{
    public interface ICountryHelper
    {
        Country CreateCountry(string countryName);
        Country QueryCountry(string countryName);
    }
}
```

`Helpers/CountryHelper.cs`
```c#
using GraphQL.WebApi.Interfaces;
using GraphQL.WebApi.Models;
using System.Linq;

namespace GraphQL.WebApi.Helpers
{
    public class CountryHelper: ICountryHelper
    {   
        private readonly IGenericRepository<Country> _countryRepository;
        public CountryHelper(IGenericRepository<Country> countryRepository)
        {
            _countryRepository = countryRepository;                
        }

        public Country CreateCountry(string countryName)
        {
            var newCountry = new Country
            {
                name = countryName
            };
            return _countryRepository.Insert(newCountry);
        }
        public Country QueryCountry(string countryName)
        {
            return _countryRepository.GetAll().FirstOrDefault(c => c.name == countryName);
        }
    }
}
```

Now let's create `CountryTests` class and add the initial `Creates_Country` test;

`Tests/CountryTests.cs`
```c#
using GraphQL.WebApi.Interfaces;
using GraphQL.WebApi.Models;
using Testing.Helpers;
using Xunit;

namespace Testing.Tests
{
    public class CountryTests: IClassFixture<TestClassFixture>
    {
        private readonly TestClassFixture _fixture;
        private readonly ICountryHelper _countryHelper;
        public CountryTests(TestClassFixture fixture, ICountryHelper countryHelper)
        {
            _fixture = fixture;
            _countryHelper = countryHelper;
        }    

        [Theory]
        [InlineData("Japan")]
        public void Creates_Country(string countryName)
        {   
            var newCountry = _countryHelper.CreateCountry(countryName);

            Assert.True(newCountry != default(Country));
            Assert.True(newCountry.id != 0);
        }
    }
}
```

Since we need to supply test parameters to the test, we used `InlineData` here. If we don't need to supply data, we can use `Fact` instead. Detailed usage of xUnit is beyond our scope, tough.








![create_japan](https://dev-to-uploads.s3.amazonaws.com/i/nllduvqtdtiinsw12wt8.PNG)
