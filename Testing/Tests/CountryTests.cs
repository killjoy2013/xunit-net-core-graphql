using GraphQL;
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
        public CountryTests(TestClassFixture fixture)
        {
            _fixture = fixture;
            _countryHelper = (ICountryHelper)_fixture.Server.Host.Services.GetService(typeof(ICountryHelper));
        }    

        [Theory]
        [InlineData("Japan")]
        public void Creates_Country(string countryName)
        {   
            var newCountry = _countryHelper.CreateCountry($"{countryName}{TestClassFixture.RandomString(5)}");

            Assert.True(newCountry != default(Country));
            Assert.True(newCountry.id != 0);
        }


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
    }
}
