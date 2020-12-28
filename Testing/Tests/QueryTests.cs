using GraphQL.WebApi.Interfaces;
using GraphQL.WebApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Testing.Helpers;
using Xunit;

namespace Testing.Tests
{
    public class QueryTests : IClassFixture<TestClassFixture>
    {
        private readonly TestClassFixture _fixture;
        private readonly ICountryHelper _countryHelper;
        public QueryTests(TestClassFixture fixture)
        {
            _fixture = fixture;
            _countryHelper = (ICountryHelper)_fixture.Server.Host.Services.GetService(typeof(ICountryHelper));
        }

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
    }
}
