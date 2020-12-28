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

Right click to the test and run (or debug). After the test success, test explorer indicates success;

![japan_created](https://dev-to-uploads.s3.amazonaws.com/i/puytqu8f0392zwwz8bhc.PNG)

Let's check the testdb

![japan_country_db](https://dev-to-uploads.s3.amazonaws.com/i/16qgli6e0khx61ndrdli.PNG)

Now let's create an exception case and test it. To do this, we'll add `unique` constraints to `name` fields of country & city tables in DatabaseContext.


`Repositories/DatabaseContext.cs`
```c#
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<City>(entity =>
            {
                entity.Property(e => e.creation_date).HasDefaultValueSql("(now())");
                entity.HasIndex(e => e.name).IsUnique(true);
               
            });
           

            modelBuilder.Entity<Country>(entity =>
            {
                entity.Property(e => e.creation_date).HasDefaultValueSql("(now())");
                entity.HasIndex(e => e.name).IsUnique(true);
            });
        }
```

Then add a new migration with `dotnet ef migrations add UniqNames`. Now kill the databases with `docker-compose down` and recreate them `docker-compose up -d`.

First run of our test is supposed to success as usual. If we run it for the second time, text explorer will suffer;

![no_second_japan](https://dev-to-uploads.s3.amazonaws.com/i/8kwhhxgaltof6sq3j9ow.PNG)

Entity framework exception is raised as expected. However, what makes a test real unit, is its independence from previous runs. i.e., consecutive executions of a unit test must result in the same result. Let's change our test;

```c#
        public void Creates_Country(string countryName)
        {   
            var newCountry = _countryHelper.CreateCountry($"{countryName}{TestClassFixture.RandomString(5)}");

            Assert.True(newCountry != default(Country));
            Assert.True(newCountry.id != 0);
        }
```
Now we concatenate countryName parameter with a randomly generated string to create unique country name. After running the test three times our country table is as follows;

![many_japan](https://dev-to-uploads.s3.amazonaws.com/i/284w7eje161d56scq93p.PNG)

Now we want to handle above exception by deliberately raising it and test. Let's change the `CreateCountry` helper method as below. First we need to make sure that the exception caught is caused by `duplicate key value`. Other exceptions are not handled in this scenario. `ExecutionError` comes from GraphQL package. This is a special exception class for GraphQL applications. It's easy to handle on the clientside, say using ApolloClient.

```c#
        public Country CreateCountry(string countryName)
        {
            var newCountry = new Country
            {
                name = countryName
            };
            Country createdCountry = null;
            try
            {
                createdCountry = _countryRepository.Insert(newCountry);
            }
            catch (DbUpdateException dbException)
            {
                if (dbException.InnerException.Message.Contains("duplicate key value"))
                {
                    throw new ExecutionError("duplicate_country_not_allowed", dbException.InnerException);
                }
                else
                {
                    throw dbException;
                }
            }
            return createdCountry;
        }
```

Our next test is `Dont_Create_Dublicate_Country`. We handle the duplicate country name situation by catching `DbUpdateException` and checking `duplicate key value` message. We use `Assert.Throws<T>` here.

```c#
        [Theory]
        [InlineData("France")]
        public void Dont_Create_Dublicate_Country(string countryName)
        {
            var uniqCountryName = $"{countryName}{TestClassFixture.RandomString(5)}";

            //first create the country
            var newCountry = _countryHelper.CreateCountry(uniqCountryName);
            Assert.True(newCountry != default(Country));
            Assert.True(newCountry.id != 0);

            //try to create same country!
            ExecutionError testException = Assert.Throws<ExecutionError>(() =>
            {
                _countryHelper.CreateCountry(uniqCountryName);
            });
            Assert.True(testException.Message == "duplicate_country_not_allowed");
        }
```

If any other exception other than `duplicate key value` situtation, the test will of course fail.

These two tests shed enough light onto writing unit tests. Let's write integration tests.

### 5. Creating integration tests

In our graphql api we have queries & mutations. Inside them, we receive http requests and execute the business logic. Their implimentation may vary depending on your design. Generally, I prefer encapsulating business logic in my domain models. In this case, queries and mutations acts like façades. They have their own logic to execute after receiving the request. You may want to execute various checks and controls. In this regard, testing queries & mutations is supposed to be done by sending http requests. Here comes the integration test. 
In `TestClassFixture` constructor we created `Client = Server.CreateClient();`. Simply put, we're creating an http client by using test server. So, we can send query and mutation requests as http posts.

`Tests/QueryTests.cs`
```c#
[Fact]        
        public async Task Queries_Existing_Countries_By_Name()
        {
            var countryName = TestClassFixture.RandomString(5);
            var newCountry1 = _countryHelper.CreateCountry($"{countryName}{TestClassFixture.RandomString(5)}");
            Assert.True(newCountry1 != default(Country));
            Assert.True(newCountry1.id != 0);

            var newCountry2 = _countryHelper.CreateCountry($"{countryName}{TestClassFixture.RandomString(5)}");
            Assert.True(newCountry2 != default(Country));
            Assert.True(newCountry2.id != 0);

            var newCountry3 = _countryHelper.CreateCountry($"{countryName}{TestClassFixture.RandomString(5)}");
            Assert.True(newCountry3 != default(Country));
            Assert.True(newCountry3.id != 0);

            var param = new JObject();
            param["query"] = @"query countries($name:String!){
                                  countries(name:$name){
                                    id
                                    name
                                  }}";

            dynamic variables = new JObject();
            variables.name = countryName;

            param["variables"] = variables;
            var content = new StringContent(JsonConvert.SerializeObject(param), UTF8Encoding.UTF8, "application/json");
            var response = await _fixture.Client.PostAsync("graphql", content);
            var serviceResultJson = await response.Content.ReadAsStringAsync();

            var gqlResult = new GqlResultList<Country>(serviceResultJson, "countries");

            Assert.True(gqlResult.GraphQLError == null);
            Assert.True(gqlResult.Data != null);
            Assert.True(gqlResult.Data.Count == 3);
        }
```

Test scenorio here is to query countries and expect to receive three items. To achieve this, first we create three countries. To be able to run the test consecutively without any problem, we create unique country name and its variations. Using these country names, we create them using our existing country helper. We can rely on our country helper since it's already tested in its unit test.

At this point we construct our query and its variables as JObjects. Below lines are common in all our integration tests;

```c#
            var content = new StringContent(JsonConvert.SerializeObject(param), UTF8Encoding.UTF8, "application/json");
            var response = await _fixture.Client.PostAsync("graphql", content);
            var serviceResultJson = await response.Content.ReadAsStringAsync();
```

Basically, we create our string content from the constructed query, send an http post via http client in our fixture class, and finally read string content to obtain `serviceResultJson`
Now we add assertions. We need to create a typed result to make assertions on it. `GqlResultList<T>` and `GqlResult<T>` are just for that. We may expect a single object or a list of object. Here we can assert that our `gqlResult.Data` is not to be null and has exactly three items in it.
You can run `Queries_Existing_Countries_By_Name` test. You should see success in the test explorer.

### Final words...
Importance of testing in software development is clear to everyone. Written tests are like guarding walls around our code base. We're living in a DevOps world and want to be able to deliver our development as easy & frequent as possible. An indispensible concept in this era, I beleive, is `confidence`. Developers need to develop confidently. Also deployments should be done confidently. Otherwise, undesired situations can happen with your end users and the cost can be beyond anticipated.

One major drawback of writing well structured tests is that developer teams need to spend considerable amount of time for that. In this writing, we tried to show how easy and effective to write unit & integration tests for a GraphQL api using dotnet core 3.1, xUnit & Docker.

Thanks for reading...
